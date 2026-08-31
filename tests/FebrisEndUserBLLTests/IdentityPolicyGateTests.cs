// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using Febris.UserNode.Portal.IdentityPolicy;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Gates the tenant portal's identity policy surface (<see cref="IdentityPolicyOptions"/> and
    /// <see cref="RegistrationPolicy"/>). Three properties are proven:
    /// <list type="number">
    ///   <item>An EMPTY configuration binds to the SAFE, locked-down defaults (admin-only
    ///   registration, confirmed email required, lockout on, soft-delete only, no self-service
    ///   deletion). A misconfigured or absent "Identity" section must never silently open the portal.</item>
    ///   <item>Explicit configuration keys override those defaults through the standard binder.</item>
    ///   <item><see cref="RegistrationPolicy"/> translates the configured
    ///   <see cref="RegistrationMode"/> into self-registration and per-email admission decisions.</item>
    /// </list>
    /// </summary>
    public class IdentityPolicyGateTests
    {
        // ---- 1. Safe defaults from an EMPTY configuration ---------------------------------------

        [Fact]
        public void EmptyConfiguration_BindsSafeDefaults()
        {
            // Bind an empty "Identity" section through the real DI/IOptions pipeline the portal uses.
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>())
                .Build();

            using ServiceProvider provider = new ServiceCollection()
                .Configure<IdentityPolicyOptions>(config.GetSection(IdentityPolicyOptions.SectionName))
                .BuildServiceProvider();

            IdentityPolicyOptions options =
                provider.GetRequiredService<IOptions<IdentityPolicyOptions>>().Value;

            // Registration is locked down by default.
            options.Registration.Mode.Should().Be(RegistrationMode.AdminOnly);
            options.Registration.RequireConfirmedEmail.Should().BeTrue();

            // Lockout: 5 attempts / 15 minutes / enabled for new users.
            options.Lockout.MaxFailedAttempts.Should().Be(5);
            options.Lockout.LockoutMinutes.Should().Be(15);
            options.Lockout.EnabledForNewUsers.Should().BeTrue();

            // Session: 60 minutes, sliding, no absolute cap.
            options.Session.LifetimeMinutes.Should().Be(60);
            options.Session.Sliding.Should().BeTrue();
            options.Session.AbsoluteTimeoutMinutes.Should().BeNull();

            // Account lifecycle: soft-delete only, no self-service deletion.
            options.AccountLifecycle.SoftDeleteOnly.Should().BeTrue();
            options.AccountLifecycle.AllowSelfServiceDeletion.Should().BeFalse();

            // Local password login remains available by default.
            options.Login.AllowLocalPassword.Should().BeTrue();
        }

        // ---- 2. Explicit configuration overrides the defaults -----------------------------------

        [Fact]
        public void ConfiguredValues_OverrideDefaults()
        {
            var settings = new Dictionary<string, string>
            {
                ["Identity:Registration:Mode"] = "Open",
                ["Identity:Registration:AllowedEmailDomains:0"] = "acme.com",
                ["Identity:Registration:AllowedEmailDomains:1"] = "beta.org",
                ["Identity:Registration:RequireConfirmedEmail"] = "false",
                ["Identity:Lockout:MaxFailedAttempts"] = "10",
                ["Identity:Lockout:LockoutMinutes"] = "30",
                ["Identity:Session:LifetimeMinutes"] = "120",
                ["Identity:Session:Sliding"] = "false",
                ["Identity:Session:AbsoluteTimeoutMinutes"] = "480",
                ["Identity:TwoFactor:Enforcement"] = "AllRequired",
                ["Identity:AccountLifecycle:AllowSelfServiceDeletion"] = "true",
                ["Identity:AccountLifecycle:PurgeAfterDays"] = "90",
            };

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            // Bind via the ConfigurationBinder ( .Get<T>() ) route.
            IdentityPolicyOptions options =
                config.GetSection(IdentityPolicyOptions.SectionName).Get<IdentityPolicyOptions>();

            options.Should().NotBeNull();
            options.Registration.Mode.Should().Be(RegistrationMode.Open);
            options.Registration.AllowedEmailDomains.Should().Equal("acme.com", "beta.org");
            options.Registration.RequireConfirmedEmail.Should().BeFalse();
            options.Lockout.MaxFailedAttempts.Should().Be(10);
            options.Lockout.LockoutMinutes.Should().Be(30);
            options.Session.LifetimeMinutes.Should().Be(120);
            options.Session.Sliding.Should().BeFalse();
            options.Session.AbsoluteTimeoutMinutes.Should().Be(480);
            options.TwoFactor.Enforcement.Should().Be(TwoFactorEnforcement.AllRequired);
            options.AccountLifecycle.AllowSelfServiceDeletion.Should().BeTrue();
            options.AccountLifecycle.PurgeAfterDays.Should().Be(90);
        }

        // ---- 3. RegistrationPolicy decisions over IdentityPolicyOptions --------------------------

        private static RegistrationPolicy PolicyFor(RegistrationOptions registration)
        {
            var options = new IdentityPolicyOptions { Registration = registration };
            return new RegistrationPolicy(Options.Create(options));
        }

        [Fact]
        public void AdminOnly_DisablesSelfRegistration_AndAdmitsNoEmail()
        {
            RegistrationPolicy policy = PolicyFor(new RegistrationOptions
            {
                Mode = RegistrationMode.AdminOnly,
            });

            policy.SelfRegistrationEnabled.Should().BeFalse();
            policy.IsEmailAllowed("x@acme.com").Should().BeFalse();
        }

        [Fact]
        public void Open_EnablesSelfRegistration_AndAdmitsAnyWellFormedEmail()
        {
            RegistrationPolicy policy = PolicyFor(new RegistrationOptions
            {
                Mode = RegistrationMode.Open,
            });

            policy.SelfRegistrationEnabled.Should().BeTrue();
            policy.IsEmailAllowed("x@acme.com").Should().BeTrue();
            policy.IsEmailAllowed("someone@any-other-domain.example").Should().BeTrue();
        }

        [Fact]
        public void DomainAllowlist_AdmitsListedDomain_RejectsEverythingElse()
        {
            RegistrationPolicy policy = PolicyFor(new RegistrationOptions
            {
                Mode = RegistrationMode.DomainAllowlist,
                AllowedEmailDomains = new[] { "acme.com" },
            });

            policy.SelfRegistrationEnabled.Should().BeTrue();
            policy.IsEmailAllowed("x@acme.com").Should().BeTrue();
            policy.IsEmailAllowed("x@evil.com").Should().BeFalse();
            policy.IsEmailAllowed(null).Should().BeFalse();
            policy.IsEmailAllowed(string.Empty).Should().BeFalse();
        }

        [Fact]
        public void AutoProvisionJit_DefaultsFalse_AndReflectsConfig()
        {
            // Default (unset) -> closed SSO. Unknown first-login users are turned away and must be
            // pre-provisioned by an admin. This default is deliberate and is asserted, not incidental:
            // a self-hoster who wires up an IdP should not silently inherit an account for everyone
            // that IdP is willing to authenticate.
            PolicyFor(new RegistrationOptions()).AutoProvisionJitEnabled.Should().BeFalse();
            // Opting in re-enables JIT auto-provisioning on first external login.
            PolicyFor(new RegistrationOptions { AutoProvisionJit = true }).AutoProvisionJitEnabled.Should().BeTrue();
        }
    }
}
