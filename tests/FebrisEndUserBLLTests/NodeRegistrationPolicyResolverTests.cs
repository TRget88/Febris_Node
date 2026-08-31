// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading.Tasks;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.LogicLayer.Logic.IdentityLogic;
using Febris.UserNode.Portal.IdentityPolicy;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the node's DB-first registration policy (node initialization design 2026-08-18): the
    /// resolver that made <c>Identity:Registration:Mode</c> turnable at runtime instead of
    /// requiring a JSON edit and a host restart.
    ///
    /// <para>
    /// The tests that matter most here are the FAIL-CLOSED ones. The resolver deliberately breaks
    /// symmetry with its sibling <c>HubFederationSettingsResolver</c>, which degrades to
    /// configuration on any database problem. Doing that here would mean a node whose stored policy
    /// says AdminOnly, but whose configuration file still says Open, re-opens to the public on every
    /// database blip. Every "unreadable" path below is asserted against a configuration of
    /// <see cref="RegistrationMode.Open"/> precisely so that a regression to config-fallback shows
    /// up as an OPEN node, not as a silently equivalent AdminOnly.
    /// </para>
    /// </summary>
    public class NodeRegistrationPolicyResolverTests
    {
        // ---- Fakes ------------------------------------------------------------------------------

        /// <summary>A settings logic that returns a fixed snapshot, or throws.</summary>
        private sealed class FakeSettingsLogic : INodeRegistrationSettingsLogic
        {
            private readonly StoredRegistrationPolicy _stored;
            private readonly Exception _throw;

            public FakeSettingsLogic(StoredRegistrationPolicy stored) { _stored = stored; }
            public FakeSettingsLogic(Exception toThrow) { _throw = toThrow; }

            /// <summary>How many times the store was actually consulted -- proves the TTL cache
            /// serves repeats and that Invalidate forces a re-read.</summary>
            public int GetCount { get; private set; }

            public Task<StoredRegistrationPolicy> GetStored()
            {
                GetCount++;
                if (_throw != null)
                {
                    throw _throw;
                }
                return Task.FromResult(_stored);
            }

            public Task<StoredRegistrationPolicy> Save(
                RegistrationSettingsInputModel input, string modeName, string actorEmail)
            {
                throw new NotSupportedException("not exercised by these tests");
            }
        }

        // ---- Builders ---------------------------------------------------------------------------

        private static IdentityPolicyOptions Configured(RegistrationMode mode, params string[] domains)
        {
            return new IdentityPolicyOptions
            {
                Registration = new RegistrationOptions
                {
                    Mode = mode,
                    AllowedEmailDomains = domains ?? Array.Empty<string>()
                }
            };
        }

        /// <summary>Build the resolver over a real scope factory, with the given logic registered
        /// (or nothing registered at all when <paramref name="logic"/> is null, which is the
        /// wiring-fault case).</summary>
        private static NodeRegistrationPolicyResolver Build(
            INodeRegistrationSettingsLogic logic, IdentityPolicyOptions configured)
        {
            var services = new ServiceCollection();
            if (logic != null)
            {
                services.AddScoped<INodeRegistrationSettingsLogic>(_ => logic);
            }
            ServiceProvider provider = services.BuildServiceProvider();
            return new NodeRegistrationPolicyResolver(
                provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(configured));
        }

        private static StoredRegistrationPolicy Stored(
            string mode, string domains = null, DateTime? openUntilUtc = null,
            bool requireAdminApproval = false, bool autoProvisionJit = false)
        {
            return new StoredRegistrationPolicy
            {
                HasStoredSettings = true,
                Mode = mode,
                AllowedEmailDomains = domains,
                OpenUntilUtc = openUntilUtc,
                RequireAdminApproval = requireAdminApproval,
                AutoProvisionJit = autoProvisionJit
            };
        }

        // ---- 1. Precedence: stored beats configured ---------------------------------------------

        [Fact]
        public void StoredPolicy_Governs_OverConfiguration()
        {
            // Configuration is the locked-down default; the operator opened it from the page.
            NodeRegistrationPolicyResolver resolver = Build(
                new FakeSettingsLogic(Stored(nameof(RegistrationMode.Open))),
                Configured(RegistrationMode.AdminOnly));

            resolver.Mode.Should().Be(RegistrationMode.Open);
            resolver.SelfRegistrationEnabled.Should().BeTrue();
            resolver.IsEmailAllowed("someone@anywhere.example").Should().BeTrue();
        }

        [Fact]
        public void StoredPolicy_Governs_WhenItIsTheMoreRestrictiveOne()
        {
            // The direction that actually matters operationally: configuration was left Open in a
            // file nobody edits, and the admin closed the node from the page.
            NodeRegistrationPolicyResolver resolver = Build(
                new FakeSettingsLogic(Stored(nameof(RegistrationMode.AdminOnly))),
                Configured(RegistrationMode.Open));

            resolver.Mode.Should().Be(RegistrationMode.AdminOnly);
            resolver.SelfRegistrationEnabled.Should().BeFalse();
            resolver.IsEmailAllowed("someone@anywhere.example").Should().BeFalse();
        }

        [Fact]
        public void NothingStored_LeavesConfigurationGoverning()
        {
            // A deployment that never opens the page must behave exactly as it did before this
            // feature existed -- this is the back-compat assertion.
            NodeRegistrationPolicyResolver resolver = Build(
                new FakeSettingsLogic(new StoredRegistrationPolicy { HasStoredSettings = false }),
                Configured(RegistrationMode.DomainAllowlist, "acme.com"));

            resolver.Mode.Should().Be(RegistrationMode.DomainAllowlist);
            resolver.IsEmailAllowed("x@acme.com").Should().BeTrue();
            resolver.IsEmailAllowed("x@evil.com").Should().BeFalse();
        }

        // ---- 2. Fail closed ---------------------------------------------------------------------

        [Fact]
        public void UnreadableStore_ResolvesAdminOnly_EvenWhenConfigurationSaysOpen()
        {
            // THE test. A database failure must not hand governance back to a configuration file
            // that says Open. If this ever regresses to the federation resolver's config-fallback
            // posture, this assertion fails as Open rather than passing by coincidence.
            NodeRegistrationPolicyResolver resolver = Build(
                new FakeSettingsLogic(new InvalidOperationException("relation does not exist")),
                Configured(RegistrationMode.Open));

            resolver.Mode.Should().Be(RegistrationMode.AdminOnly);
            resolver.SelfRegistrationEnabled.Should().BeFalse();
            resolver.IsEmailAllowed("someone@anywhere.example").Should().BeFalse();
        }

        [Fact]
        public void MissingSettingsLogic_ResolvesAdminOnly_EvenWhenConfigurationSaysOpen()
        {
            // A missing collaborator is a wiring fault, not an absent row. Same fail direction.
            NodeRegistrationPolicyResolver resolver = Build(null, Configured(RegistrationMode.Open));

            resolver.Mode.Should().Be(RegistrationMode.AdminOnly);
            resolver.SelfRegistrationEnabled.Should().BeFalse();
        }

        [Theory]
        [InlineData("Openn")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public void UnparseableStoredMode_ResolvesAdminOnly(string storedMode)
        {
            NodeRegistrationPolicyResolver resolver = Build(
                new FakeSettingsLogic(Stored(storedMode)), Configured(RegistrationMode.Open));

            resolver.Mode.Should().Be(RegistrationMode.AdminOnly);
            resolver.SelfRegistrationEnabled.Should().BeFalse();
        }

        [Fact]
        public void NumericStoredMode_IsRejected_NotTreatedAsAnOrdinal()
        {
            // THIS TEST FOUND A REAL BUG. The first implementation used Enum.TryParse plus
            // Enum.IsDefined, which looks airtight and is not: TryParse("2") SUCCEEDS and yields
            // Open, and IsDefined then agrees, because 2 really is a defined value. So an ordinal
            // was accepted -- defeating the entire reason the mode is stored as a NAME, which is
            // that an ordinal stops meaning what it meant the moment anyone inserts a member into
            // the enum. The fix was to parse against Enum.GetNames only.
            //
            // Both shapes are asserted: an UNDEFINED number (17), which IsDefined alone would have
            // caught, and a DEFINED one (2), which it would not.
            Build(new FakeSettingsLogic(Stored("17")), Configured(RegistrationMode.AdminOnly))
                .Mode.Should().Be(RegistrationMode.AdminOnly);

            NodeRegistrationPolicyResolver numericOpen = Build(
                new FakeSettingsLogic(Stored(((int)RegistrationMode.Open).ToString())),
                Configured(RegistrationMode.AdminOnly));
            numericOpen.Mode.Should().Be(RegistrationMode.AdminOnly);
            numericOpen.SelfRegistrationEnabled.Should().BeFalse();
        }

        [Theory]
        [InlineData("AdminOnly", true)]
        [InlineData("adminonly", true)]
        [InlineData("  Open  ", true)]
        [InlineData("DomainAllowlist", true)]
        [InlineData("Invite", true)]
        [InlineData("0", false)]
        [InlineData("2", false)]
        [InlineData("17", false)]
        [InlineData("-1", false)]
        [InlineData("Open,AdminOnly", false)]
        [InlineData("Openn", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void TryParseModeName_AcceptsNamesOnly(string input, bool expected)
        {
            // Pinned directly as well as through the resolver, because the admin controller shares
            // this helper: a value the page accepts must never be one the resolver later refuses.
            RegistrationMode mode;
            NodeRegistrationPolicyResolver.TryParseModeName(input, out mode).Should().Be(expected);
            if (!expected)
            {
                mode.Should().Be(RegistrationMode.AdminOnly, "the out value fails closed too");
            }
        }

        [Fact]
        public void StoredModeName_IsCaseInsensitive()
        {
            // Rows may be written by hand during support work; casing must not close a node.
            Build(new FakeSettingsLogic(Stored("open")), Configured(RegistrationMode.AdminOnly))
                .Mode.Should().Be(RegistrationMode.Open);
        }

        // ---- 3. The auto-close window -----------------------------------------------------------

        [Fact]
        public void OpenWindow_StillRunning_KeepsTheStoredMode()
        {
            NodeRegistrationPolicyResolver resolver = Build(
                new FakeSettingsLogic(Stored(
                    nameof(RegistrationMode.Open), openUntilUtc: DateTime.UtcNow.AddHours(1))),
                Configured(RegistrationMode.AdminOnly));

            resolver.Mode.Should().Be(RegistrationMode.Open);
            resolver.SelfRegistrationEnabled.Should().BeTrue();
        }

        [Fact]
        public void OpenWindow_Elapsed_ClosesTheNodeWithoutAnyoneActing()
        {
            // The point of the window: the hole closes itself even if nobody remembers.
            NodeRegistrationPolicyResolver resolver = Build(
                new FakeSettingsLogic(Stored(
                    nameof(RegistrationMode.Open), openUntilUtc: DateTime.UtcNow.AddSeconds(-1))),
                Configured(RegistrationMode.Open));

            resolver.Mode.Should().Be(RegistrationMode.AdminOnly);
            resolver.SelfRegistrationEnabled.Should().BeFalse();
        }

        [Fact]
        public void OpenWindow_Elapsed_AlsoClosesDomainAllowlist()
        {
            NodeRegistrationPolicyResolver resolver = Build(
                new FakeSettingsLogic(Stored(
                    nameof(RegistrationMode.DomainAllowlist), domains: "acme.com",
                    openUntilUtc: DateTime.UtcNow.AddSeconds(-1))),
                Configured(RegistrationMode.AdminOnly));

            resolver.Mode.Should().Be(RegistrationMode.AdminOnly);
            resolver.IsEmailAllowed("x@acme.com").Should().BeFalse();
        }

        [Theory]
        [InlineData(nameof(RegistrationMode.AdminOnly))]
        [InlineData(nameof(RegistrationMode.Invite))]
        public void ElapsedWindow_IsIgnored_ForModesThatDoNotSelfRegister(string mode)
        {
            // An expiry on a closed mode means nothing, and rewriting Invite to AdminOnly would
            // silently change a policy the operator did not ask to have changed. Asserted so the
            // "just reset everything to AdminOnly on expiry" simplification cannot creep back in.
            NodeRegistrationPolicyResolver resolver = Build(
                new FakeSettingsLogic(Stored(mode, openUntilUtc: DateTime.UtcNow.AddSeconds(-1))),
                Configured(RegistrationMode.Open));

            resolver.Mode.Should().Be(Enum.Parse<RegistrationMode>(mode));
            resolver.SelfRegistrationEnabled.Should().BeFalse();
        }

        // ---- 4. The stored values reach the decisions -------------------------------------------

        [Fact]
        public void StoredDomains_DriveAdmission()
        {
            NodeRegistrationPolicyResolver resolver = Build(
                new FakeSettingsLogic(Stored(
                    nameof(RegistrationMode.DomainAllowlist), domains: "acme.com,beta.org")),
                Configured(RegistrationMode.DomainAllowlist, "ignored.example"));

            resolver.IsEmailAllowed("x@acme.com").Should().BeTrue();
            resolver.IsEmailAllowed("x@beta.org").Should().BeTrue();
            resolver.IsEmailAllowed("x@ignored.example").Should()
                .BeFalse("the STORED list governs, not the configured one");
            resolver.IsEmailAllowed(null).Should().BeFalse();
            resolver.IsEmailAllowed("not-an-email").Should().BeFalse();
        }

        [Fact]
        public void StoredFlags_DriveApprovalAndJit()
        {
            NodeRegistrationPolicyResolver resolver = Build(
                new FakeSettingsLogic(Stored(
                    nameof(RegistrationMode.Open), requireAdminApproval: true, autoProvisionJit: true)),
                Configured(RegistrationMode.AdminOnly));

            resolver.RequiresAdminApproval.Should().BeTrue();
            resolver.AutoProvisionJitEnabled.Should().BeTrue();
        }

        [Fact]
        public void FailClosed_DoesNotInheritJitFromAnUnreadableStore()
        {
            // Closed means closed on every axis the policy exposes: a fault must not leave external
            // logins auto-provisioning accounts just because configuration happened to allow it.
            var configured = Configured(RegistrationMode.Open);
            configured.Registration.AutoProvisionJit = true;
            configured.Registration.RequireAdminApproval = false;

            NodeRegistrationPolicyResolver resolver = Build(
                new FakeSettingsLogic(new InvalidOperationException("boom")), configured);

            resolver.AutoProvisionJitEnabled.Should().BeFalse();
        }

        // ---- 5. Caching and invalidation --------------------------------------------------------

        [Fact]
        public void RepeatedReads_ServeTheCachedSnapshot()
        {
            var logic = new FakeSettingsLogic(Stored(nameof(RegistrationMode.Open)));
            NodeRegistrationPolicyResolver resolver = Build(logic, Configured(RegistrationMode.AdminOnly));

            for (int i = 0; i < 20; i++)
            {
                resolver.SelfRegistrationEnabled.Should().BeTrue();
            }

            logic.GetCount.Should().Be(1, "the TTL snapshot must not re-read the store per consultation");
        }

        [Fact]
        public void Invalidate_ForcesAReRead_SoASaveAppliesImmediately()
        {
            var logic = new FakeSettingsLogic(Stored(nameof(RegistrationMode.Open)));
            NodeRegistrationPolicyResolver resolver = Build(logic, Configured(RegistrationMode.AdminOnly));

            resolver.SelfRegistrationEnabled.Should().BeTrue();
            logic.GetCount.Should().Be(1);

            resolver.Invalidate();
            resolver.SelfRegistrationEnabled.Should().BeTrue();
            logic.GetCount.Should().Be(2, "the admin save path relies on this to apply without waiting out the TTL");
        }

        // ---- 6. The configured view the admin page reads ----------------------------------------

        [Fact]
        public void ConfiguredRegistration_ExposesTheFallback_WithoutRereadingConfiguration()
        {
            NodeRegistrationPolicyResolver resolver = Build(
                new FakeSettingsLogic(Stored(nameof(RegistrationMode.Open))),
                Configured(RegistrationMode.DomainAllowlist, "acme.com"));

            // Effective mode is the stored one; the page still needs to show what a reset falls
            // back to, and it must come from the same options instance the resolver used.
            resolver.Mode.Should().Be(RegistrationMode.Open);
            resolver.ConfiguredRegistration.Mode.Should().Be(RegistrationMode.DomainAllowlist);
            resolver.ConfiguredRegistration.AllowedEmailDomains.Should().Equal("acme.com");
        }

        [Fact]
        public void NullOptions_DoNotThrow_AndResolveTheSafeDefault()
        {
            // Mirrors RegistrationPolicy's own tolerance of a missing Identity section.
            var services = new ServiceCollection();
            services.AddScoped<INodeRegistrationSettingsLogic>(
                _ => new FakeSettingsLogic(new StoredRegistrationPolicy { HasStoredSettings = false }));
            ServiceProvider provider = services.BuildServiceProvider();

            var resolver = new NodeRegistrationPolicyResolver(
                provider.GetRequiredService<IServiceScopeFactory>(), null);

            resolver.Mode.Should().Be(RegistrationMode.AdminOnly);
            resolver.SelfRegistrationEnabled.Should().BeFalse();
        }
    }
}
