// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.IdentityLogic;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the store side of the node's registration policy (node initialization design
    /// 2026-08-18): single-row create-or-update semantics, the auto-close window arithmetic, the
    /// domain-list tidying, and the audit stamp.
    /// <para>
    /// The resolver's fail-closed behavior is pinned separately in
    /// <see cref="NodeRegistrationPolicyResolverTests"/>; this file is about what actually lands in
    /// the row.
    /// </para>
    /// </summary>
    public class NodeRegistrationSettingsLogicTests
    {
        private static DataDbContext BuildDataContext(string dbName)
        {
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new DataDbContext(options);
        }

        private static NodeRegistrationSettingsLogic BuildLogic(DataDbContext context)
        {
            return new NodeRegistrationSettingsLogic(new NodeRegistrationConfigQueries(context));
        }

        private static RegistrationSettingsInputModel Input(
            string domains = null, int? openForHours = null,
            bool requireAdminApproval = false, bool autoProvisionJit = false)
        {
            return new RegistrationSettingsInputModel
            {
                AllowedEmailDomains = domains,
                OpenForHours = openForHours,
                RequireAdminApproval = requireAdminApproval,
                AutoProvisionJit = autoProvisionJit
            };
        }

        // ---- 1. Absence is a normal state, not a failure ----------------------------------------

        [Fact]
        public async Task GetStored_OnAnUntouchedNode_ReportsNothingStored()
        {
            using DataDbContext context = BuildDataContext(nameof(GetStored_OnAnUntouchedNode_ReportsNothingStored));

            StoredRegistrationPolicy stored = await BuildLogic(context).GetStored();

            // Distinguishable from a read FAILURE, which throws. The resolver relies on exactly
            // this difference: absent hands governance to configuration, unreadable fails closed.
            stored.Should().NotBeNull();
            stored.HasStoredSettings.Should().BeFalse();
            stored.Mode.Should().BeNull();
        }

        // ---- 2. Single-row create-or-update ------------------------------------------------------

        [Fact]
        public async Task Save_CreatesExactlyOneRow_AndUpdatesItInPlace()
        {
            using DataDbContext context = BuildDataContext(nameof(Save_CreatesExactlyOneRow_AndUpdatesItInPlace));
            NodeRegistrationSettingsLogic logic = BuildLogic(context);

            await logic.Save(Input(), "Open", "admin@example.com");
            await logic.Save(Input(), "AdminOnly", "other@example.com");
            await logic.Save(Input(), "Invite", "third@example.com");

            NodeRegistrationConfig row = await context.NodeRegistrationConfig.SingleAsync();
            row.Mode.Should().Be("Invite", "saves update the single row rather than appending");
            row.UpdatedByEmail.Should().Be("third@example.com");
            row.UUID.Should().NotBeEmpty(
                "the UUID is set explicitly so the row is complete on providers with no uuid_generate_v4()");
        }

        [Fact]
        public async Task Save_RoundTripsEveryStoredField()
        {
            using DataDbContext context = BuildDataContext(nameof(Save_RoundTripsEveryStoredField));
            NodeRegistrationSettingsLogic logic = BuildLogic(context);

            await logic.Save(
                Input(domains: "acme.com", openForHours: 4, requireAdminApproval: true, autoProvisionJit: true),
                "DomainAllowlist",
                "admin@example.com");

            StoredRegistrationPolicy stored = await logic.GetStored();
            stored.HasStoredSettings.Should().BeTrue();
            stored.Mode.Should().Be("DomainAllowlist");
            stored.AllowedEmailDomains.Should().Be("acme.com");
            stored.RequireAdminApproval.Should().BeTrue();
            stored.AutoProvisionJit.Should().BeTrue();
            stored.OpenUntilUtc.Should().NotBeNull();
            stored.UpdatedByEmail.Should().Be("admin@example.com");
            stored.UpdatedAtUtc.Should().NotBeNull();
        }

        [Fact]
        public async Task Save_RejectsAMissingModeName()
        {
            using DataDbContext context = BuildDataContext(nameof(Save_RejectsAMissingModeName));
            NodeRegistrationSettingsLogic logic = BuildLogic(context);

            await Assert.ThrowsAsync<ArgumentException>(() => logic.Save(Input(), "  ", "admin@example.com"));
            await Assert.ThrowsAsync<ArgumentException>(() => logic.Save(Input(), null, "admin@example.com"));

            // Nothing partial was written.
            (await logic.GetStored()).HasStoredSettings.Should().BeFalse();
        }

        // ---- 3. The audit stamp ------------------------------------------------------------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Save_RecordsNoActor_RatherThanInventingOne(string actor)
        {
            using DataDbContext context = BuildDataContext(nameof(Save_RecordsNoActor_RatherThanInventingOne) + actor);

            await BuildLogic(context).Save(Input(), "AdminOnly", actor);

            (await BuildLogic(context).GetStored()).UpdatedByEmail.Should().BeNull();
        }

        [Fact]
        public async Task Save_TrimsTheActorEmail()
        {
            using DataDbContext context = BuildDataContext(nameof(Save_TrimsTheActorEmail));

            await BuildLogic(context).Save(Input(), "AdminOnly", "  admin@example.com  ");

            (await BuildLogic(context).GetStored()).UpdatedByEmail.Should().Be("admin@example.com");
        }

        // ---- 4. The auto-close window -----------------------------------------------------------

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task Save_WithNoPositiveWindow_StoresNoExpiry(int? hours)
        {
            using DataDbContext context = BuildDataContext(nameof(Save_WithNoPositiveWindow_StoresNoExpiry) + hours);

            await BuildLogic(context).Save(Input(openForHours: hours), "Open", "admin@example.com");

            (await BuildLogic(context).GetStored()).OpenUntilUtc.Should()
                .BeNull("null, zero and negative all mean open-ended");
        }

        [Fact]
        public async Task Save_WithAWindow_StoresAnAbsoluteUtcExpiry()
        {
            using DataDbContext context = BuildDataContext(nameof(Save_WithAWindow_StoresAnAbsoluteUtcExpiry));
            DateTime before = DateTime.UtcNow;

            await BuildLogic(context).Save(Input(openForHours: 3), "Open", "admin@example.com");

            DateTime? until = (await BuildLogic(context).GetStored()).OpenUntilUtc;
            until.Should().NotBeNull();
            until.Value.Should().BeOnOrAfter(before.AddHours(3))
                .And.BeOnOrBefore(DateTime.UtcNow.AddHours(3));
        }

        [Fact]
        public async Task Save_ClampsAnAbsurdWindow_RatherThanRejectingIt()
        {
            using DataDbContext context = BuildDataContext(nameof(Save_ClampsAnAbsurdWindow_RatherThanRejectingIt));
            DateTime before = DateTime.UtcNow;

            await BuildLogic(context).Save(Input(openForHours: 99999), "Open", "admin@example.com");

            // Clamped, not rejected: a validation error here would tempt the operator into picking
            // "no window at all", which is the outcome the window exists to avoid.
            DateTime? until = (await BuildLogic(context).GetStored()).OpenUntilUtc;
            until.Should().NotBeNull();
            until.Value.Should().BeOnOrAfter(before.AddHours(NodeRegistrationSettingsLogic.MaxOpenForHours))
                .And.BeOnOrBefore(DateTime.UtcNow.AddHours(NodeRegistrationSettingsLogic.MaxOpenForHours));
        }

        // ---- 5. Domain-list tidying --------------------------------------------------------------

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("   ", null)]
        [InlineData(",,  ,", null)]
        [InlineData("acme.com", "acme.com")]
        [InlineData("  acme.com  ", "acme.com")]
        [InlineData("@acme.com", "acme.com")]
        [InlineData("ACME.com", "acme.com")]
        [InlineData("acme.com,beta.org", "acme.com,beta.org")]
        [InlineData("acme.com, beta.org", "acme.com,beta.org")]
        [InlineData("acme.com\nbeta.org", "acme.com,beta.org")]
        [InlineData("acme.com; beta.org", "acme.com,beta.org")]
        [InlineData("acme.com,ACME.com,@acme.com", "acme.com")]
        [InlineData("beta.org,acme.com", "beta.org,acme.com")]
        public void NormalizeDomains_TidiesWithoutChangingMeaning(string raw, string expected)
        {
            // The field is a textarea, so operators paste commas, semicolons and newlines. The last
            // case pins ORDER preservation: de-duplication must not silently re-sort the list.
            NodeRegistrationSettingsLogic.NormalizeDomains(raw).Should().Be(expected);
        }

        [Fact]
        public async Task Save_StoresTheTidiedDomainList()
        {
            using DataDbContext context = BuildDataContext(nameof(Save_StoresTheTidiedDomainList));

            await BuildLogic(context).Save(
                Input(domains: " @ACME.com \n beta.org , acme.com "), "DomainAllowlist", "admin@example.com");

            (await BuildLogic(context).GetStored()).AllowedEmailDomains.Should().Be("acme.com,beta.org");
        }

        [Fact]
        public async Task Save_ClearingTheDomainBox_StoresNull()
        {
            using DataDbContext context = BuildDataContext(nameof(Save_ClearingTheDomainBox_StoresNull));
            NodeRegistrationSettingsLogic logic = BuildLogic(context);

            await logic.Save(Input(domains: "acme.com"), "DomainAllowlist", "admin@example.com");
            await logic.Save(Input(domains: "   "), "DomainAllowlist", "admin@example.com");

            (await logic.GetStored()).AllowedEmailDomains.Should()
                .BeNull("a cleared box and a never-filled box must read the same");
        }
    }
}
