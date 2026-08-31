// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.IdentityLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the node's account-invitation flow (invitation flow 2026-08-21).
    ///
    /// <para>
    /// The invitation model is copied from the central developer-org flow, which documents THREE
    /// unfixed defects in its own source: the token is stored readable, the recipient-email binding
    /// ships deliberately uncalled, and there is no revocation. Each of the three has a test here
    /// named after it, because a copy of a known-flawed design is exactly the thing that silently
    /// reverts to the original.
    /// </para>
    /// </summary>
    public class NodeInvitationLogicTests
    {
        private static DataDbContext BuildDataContext(string dbName)
        {
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new DataDbContext(options);
        }

        /// <summary>An accessor whose principal holds the given roles, which is how the rank policy
        /// learns what the acting operator may grant.</summary>
        private static IHttpContextAccessor AccessorForRoles(params string[] roles)
        {
            var identity = new ClaimsIdentity(
                roles.Select(r => new Claim(ClaimTypes.Role, r)), "TestAuth");
            var context = new DefaultHttpContext() { User = new ClaimsPrincipal(identity) };
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(context);
            return accessor.Object;
        }

        private static NodeInvitationLogic BuildLogic(DataDbContext context, params string[] actorRoles)
        {
            // The cohort stores are the REAL query classes over the same InMemory context, not
            // mocks. The linkage this exercises is a write through EF, and a mock would only prove
            // the logic calls something.
            return new NodeInvitationLogic(
                new NodeUserInviteQueries(context),
                AccessorForRoles(actorRoles),
                new CohortQueries(context),
                new CohortMemberQueries(context));
        }

        /// <summary>Persist a cohort so an invitation can point at it.</summary>
        private static async Task<Cohort> SeedCohort(DataDbContext context, string name)
        {
            Cohort cohort = new Cohort() { UUID = Guid.NewGuid(), Name = name };
            context.Cohort.Add(cohort);
            await context.SaveChangesAsync();
            return cohort;
        }

        private static InvitationIssueInputModel Input(
            string email = "learner@school.example",
            string role = "User",
            int? expiresInDays = null)
        {
            return new InvitationIssueInputModel
            {
                Email = email,
                FirstName = "Ada",
                LastName = "Lovelace",
                Role = role,
                ExpiresInDays = expiresInDays
            };
        }

        // ---- 1. The token is never stored (central defect 1) -------------------------------------

        [Fact]
        public async Task IssuedToken_IsStoredOnlyAsAHash_NeverInAnyColumn()
        {
            // The central invite stores its token as the row's UUID in plaintext, so anyone who can
            // read the table can redeem any outstanding invitation. This asserts the whole row,
            // field by field, rather than just checking TokenHash -- the failure mode being guarded
            // against is the token appearing SOMEWHERE ELSE, which a targeted assertion would miss.
            using DataDbContext context = BuildDataContext(nameof(IssuedToken_IsStoredOnlyAsAHash_NeverInAnyColumn));

            NodeInviteIssueResult result = await BuildLogic(context, "Admin")
                .Issue(Input(), Guid.NewGuid(), "admin@example.com");

            result.Success.Should().BeTrue();
            result.RawToken.Should().NotBeNullOrWhiteSpace();

            NodeUserInvite row = await context.NodeUserInvite.SingleAsync();
            row.TokenHash.Should().Be(DeviceCredential.Hash(result.RawToken));
            row.TokenHash.Should().NotBe(result.RawToken);

            string[] everyStringColumn =
            {
                row.Email, row.TokenHash, row.Role, row.FirstName, row.LastName,
                row.IssuedByEmail, row.RevokedByEmail
            };
            everyStringColumn.Should().NotContain(result.RawToken,
                "the raw token must not appear in ANY column, not merely outside TokenHash");
            row.UUID.ToString().Should().NotBe(result.RawToken,
                "the central flow's defect is that the UUID IS the token");
        }

        [Fact]
        public async Task Validate_FindsTheInvitation_ByHashingThePresentedToken()
        {
            using DataDbContext context = BuildDataContext(nameof(Validate_FindsTheInvitation_ByHashingThePresentedToken));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");

            NodeInviteIssueResult issued = await logic.Issue(Input(), Guid.NewGuid(), "admin@example.com");

            NodeInviteValidation valid = await logic.Validate(issued.RawToken);
            valid.State.Should().Be(InviteState.Active);
            valid.Invite.Should().NotBeNull();
            valid.Invite.Email.Should().Be("learner@school.example");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("not-a-real-token")]
        public async Task Validate_RejectsAnythingThatIsNotTheToken(string presented)
        {
            using DataDbContext context = BuildDataContext(nameof(Validate_RejectsAnythingThatIsNotTheToken) + presented);
            NodeInvitationLogic logic = BuildLogic(context, "Admin");
            await logic.Issue(Input(), Guid.NewGuid(), "admin@example.com");

            NodeInviteValidation result = await logic.Validate(presented);
            result.State.Should().Be(InviteState.NotFound);
            result.Invite.Should().BeNull();
        }

        [Fact]
        public async Task Validate_LeaksNothing_AboutADeadInvitation()
        {
            // A dead token must not hand back the address it was issued to, or the accept page
            // becomes an oracle for "was this person invited".
            using DataDbContext context = BuildDataContext(nameof(Validate_LeaksNothing_AboutADeadInvitation));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");
            NodeInviteIssueResult issued = await logic.Issue(Input(), Guid.NewGuid(), "admin@example.com");

            await logic.Revoke(issued.Invite.UUID, "admin@example.com");

            NodeInviteValidation result = await logic.Validate(issued.RawToken);
            result.State.Should().Be(InviteState.Revoked);
            result.Invite.Should().BeNull("only the ACTIVE path carries the row");
            result.Message.Should().NotContain("learner@school.example");
        }

        // ---- 2. Recipient binding (central defect 2) --------------------------------------------

        [Fact]
        public void RecipientBinding_MatchesTheInvitedAddressOnly()
        {
            // The helper the central flow ships and deliberately does NOT call. This test exists so
            // the node's use of it is pinned independently of the accept page, and so the node's
            // behaviour is documented even though the helper lives in the shared library.
            InviteRecipientMatch.RecipientEmailMatches("learner@school.example", "learner@school.example")
                .Should().BeTrue();
            InviteRecipientMatch.RecipientEmailMatches("learner@school.example", "  LEARNER@School.Example ")
                .Should().BeTrue("case and surrounding space are not meaningful in an address");
            InviteRecipientMatch.RecipientEmailMatches("learner@school.example", "someone.else@school.example")
                .Should().BeFalse();
            InviteRecipientMatch.RecipientEmailMatches("learner@school.example", null)
                .Should().BeFalse("a missing field fails closed");
            InviteRecipientMatch.RecipientEmailMatches("learner@school.example", "   ")
                .Should().BeFalse();
        }

        // ---- 3. Revocation (central defect 3) ---------------------------------------------------

        [Fact]
        public async Task Revoke_StopsTheInvitation_AndRecordsWho()
        {
            using DataDbContext context = BuildDataContext(nameof(Revoke_StopsTheInvitation_AndRecordsWho));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");
            NodeInviteIssueResult issued = await logic.Issue(Input(), Guid.NewGuid(), "admin@example.com");

            (await logic.Revoke(issued.Invite.UUID, "admin@example.com")).Should().BeTrue();
            (await logic.Validate(issued.RawToken)).State.Should().Be(InviteState.Revoked);

            NodeUserInvite row = await context.NodeUserInvite.AsNoTracking().SingleAsync();
            row.RevokedAt.Should().NotBeNull();
            row.RevokedByEmail.Should().Be("admin@example.com");
        }

        [Fact]
        public async Task Revoke_IsIdempotentAndDoesNotOverwriteTheFirstRevoker()
        {
            using DataDbContext context = BuildDataContext(nameof(Revoke_IsIdempotentAndDoesNotOverwriteTheFirstRevoker));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");
            NodeInviteIssueResult issued = await logic.Issue(Input(), Guid.NewGuid(), "admin@example.com");

            (await logic.Revoke(issued.Invite.UUID, "first@example.com")).Should().BeTrue();
            (await logic.Revoke(issued.Invite.UUID, "second@example.com")).Should()
                .BeFalse("a double click must not rewrite who cancelled it");

            NodeUserInvite row = await context.NodeUserInvite.AsNoTracking().SingleAsync();
            row.RevokedByEmail.Should().Be("first@example.com");
        }

        [Fact]
        public async Task Revoke_RefusesAnInvitationThatWasAlreadyAccepted()
        {
            using DataDbContext context = BuildDataContext(nameof(Revoke_RefusesAnInvitationThatWasAlreadyAccepted));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");
            NodeInviteIssueResult issued = await logic.Issue(Input(), Guid.NewGuid(), "admin@example.com");

            (await logic.Consume(issued.Invite.UUID, Guid.NewGuid())).Should().BeTrue();
            (await logic.Revoke(issued.Invite.UUID, "admin@example.com")).Should()
                .BeFalse("cancelling a used invitation would imply the account can be taken back");
        }

        // ---- 4. Single use ----------------------------------------------------------------------

        [Fact]
        public async Task Consume_SucceedsExactlyOnce()
        {
            // The whole point of an atomic consume: a second redemption of the same link must lose,
            // so it cannot produce a second account.
            using DataDbContext context = BuildDataContext(nameof(Consume_SucceedsExactlyOnce));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");
            NodeInviteIssueResult issued = await logic.Issue(Input(), Guid.NewGuid(), "admin@example.com");

            (await logic.Consume(issued.Invite.UUID, Guid.NewGuid())).Should().BeTrue();
            (await logic.Consume(issued.Invite.UUID, Guid.NewGuid())).Should().BeFalse();

            (await logic.Validate(issued.RawToken)).State.Should().Be(InviteState.AlreadyConsumed);
        }

        [Fact]
        public async Task Consume_RefusesARevokedInvitation()
        {
            using DataDbContext context = BuildDataContext(nameof(Consume_RefusesARevokedInvitation));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");
            NodeInviteIssueResult issued = await logic.Issue(Input(), Guid.NewGuid(), "admin@example.com");

            await logic.Revoke(issued.Invite.UUID, "admin@example.com");

            (await logic.Consume(issued.Invite.UUID, Guid.NewGuid())).Should()
                .BeFalse("this is the check that makes the accept page roll its account back");
        }

        [Fact]
        public async Task Consume_RefusesAnExpiredInvitation()
        {
            using DataDbContext context = BuildDataContext(nameof(Consume_RefusesAnExpiredInvitation));

            // Written straight to the store: the logic layer will not mint one already expired, and
            // the consume guard has to hold against a row however it got there.
            var queries = new NodeUserInviteQueries(context);
            NodeUserInvite expired = await queries.Create(new NodeUserInvite()
            {
                Email = "late@school.example",
                TokenHash = DeviceCredential.Hash("whatever"),
                Role = "User",
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            });

            (await queries.MarkConsumed(expired.UUID, Guid.NewGuid(), DateTime.UtcNow)).Should().BeFalse();
        }

        // ---- 5. Role rank: an invitation is an escalation door -----------------------------------

        [Theory]
        [InlineData("Educator", "User", true)]
        [InlineData("Educator", "Admin", false)]
        [InlineData("Educator", "ITAdmin", false)]
        [InlineData("Admin", "User", true)]
        [InlineData("Admin", "Educator", true)]
        public async Task Issue_AppliesTheSameRankGateAsDirectAccountCreation(
            string actorRole, string requestedRole, bool expectedAllowed)
        {
            // An invitation grants a role, so it is a second door onto the same escalation the
            // UserLogic.Create rank gate guards. If this ever stopped being checked, an educator
            // could mint an admin by invitation instead of by the create form.
            using DataDbContext context = BuildDataContext(
                nameof(Issue_AppliesTheSameRankGateAsDirectAccountCreation) + actorRole + requestedRole);

            NodeInviteIssueResult result = await BuildLogic(context, actorRole)
                .Issue(Input(role: requestedRole), Guid.NewGuid(), "actor@example.com");

            result.Success.Should().Be(expectedAllowed);
            (await context.NodeUserInvite.CountAsync()).Should().Be(expectedAllowed ? 1 : 0,
                "a refused invitation must not leave a row behind");
        }

        [Fact]
        public async Task Issue_RefusesAnActorWithNoRolesAtAll()
        {
            using DataDbContext context = BuildDataContext(nameof(Issue_RefusesAnActorWithNoRolesAtAll));

            NodeInviteIssueResult result = await BuildLogic(context)
                .Issue(Input(), Guid.NewGuid(), "nobody@example.com");

            result.Success.Should().BeFalse();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("NotARole")]
        [InlineData("SuperAdmin")]
        public async Task Issue_RefusesAnUnrecognizedRole(string role)
        {
            // THIS TEST FOUND A REAL BUG. The rank policy ALLOWS an unknown role name: RankOf
            // returns no rank for a string it does not recognize, and an unranked role is below
            // every actor, so "NotARole" passed CanAssign. The invitation would have been stored and
            // then thrown on ACCEPTANCE days later, in front of the invitee, because
            // UserManager.AddToRoleAsync throws rather than returning a failed result for a role
            // that does not exist. Fixed by checking NodeIdentityRoles.Required first.
            //
            // SuperAdmin is in the theory on purpose: it is a real role name elsewhere in the repo
            // but was deliberately removed from the node's role list, so it is exactly the sort of
            // plausible-looking value that would slip through a check written against an enum
            // rather than against the roles this node actually seeds.
            using DataDbContext context = BuildDataContext(nameof(Issue_RefusesAnUnrecognizedRole) + role);

            NodeInviteIssueResult result = await BuildLogic(context, "Admin")
                .Issue(Input(role: role), Guid.NewGuid(), "admin@example.com");

            result.Success.Should().BeFalse();
        }

        // ---- 6. Issue-time housekeeping ----------------------------------------------------------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-an-email")]
        [InlineData("@nolocalpart.example")]
        public async Task Issue_RefusesAnAddressItCannotSendTo(string email)
        {
            using DataDbContext context = BuildDataContext(nameof(Issue_RefusesAnAddressItCannotSendTo) + email);

            NodeInviteIssueResult result = await BuildLogic(context, "Admin")
                .Issue(Input(email: email), Guid.NewGuid(), "admin@example.com");

            result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task Issue_RefusesToStackASecondOutstandingInvitationOnOnePerson()
        {
            // Several live links for one person means only one can ever be redeemed and the rest
            // look broken to whoever holds them.
            using DataDbContext context = BuildDataContext(nameof(Issue_RefusesToStackASecondOutstandingInvitationOnOnePerson));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");

            (await logic.Issue(Input(), Guid.NewGuid(), "admin@example.com")).Success.Should().BeTrue();

            NodeInviteIssueResult second = await logic.Issue(Input(), Guid.NewGuid(), "admin@example.com");
            second.Success.Should().BeFalse();
            second.Error.Should().Contain("Revoke");
        }

        [Fact]
        public async Task Issue_AllowsANewInvitation_OnceTheOldOneIsRevoked()
        {
            using DataDbContext context = BuildDataContext(nameof(Issue_AllowsANewInvitation_OnceTheOldOneIsRevoked));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");

            NodeInviteIssueResult first = await logic.Issue(Input(), Guid.NewGuid(), "admin@example.com");
            await logic.Revoke(first.Invite.UUID, "admin@example.com");

            NodeInviteIssueResult second = await logic.Issue(Input(), Guid.NewGuid(), "admin@example.com");
            second.Success.Should().BeTrue("revoking is the documented way to re-send");
            second.RawToken.Should().NotBe(first.RawToken, "a re-send must mint a NEW secret");
        }

        [Fact]
        public async Task Issue_TreatsTheAddressCaseInsensitively_WhenBlockingDuplicates()
        {
            using DataDbContext context = BuildDataContext(nameof(Issue_TreatsTheAddressCaseInsensitively_WhenBlockingDuplicates));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");

            await logic.Issue(Input(email: "learner@school.example"), Guid.NewGuid(), "admin@example.com");

            NodeInviteIssueResult second = await logic.Issue(
                Input(email: "LEARNER@School.Example"), Guid.NewGuid(), "admin@example.com");
            second.Success.Should().BeFalse("addresses differing only in case are the same person");
        }

        [Theory]
        [InlineData(null, NodeInvitationLogic.DefaultExpiryDays)]
        [InlineData(0, NodeInvitationLogic.DefaultExpiryDays)]
        [InlineData(-3, NodeInvitationLogic.DefaultExpiryDays)]
        [InlineData(3, 3)]
        [InlineData(30, 30)]
        [InlineData(9999, NodeInvitationLogic.MaxExpiryDays)]
        public void ClampExpiryDays_DefaultsAndClamps(int? requested, int expected)
        {
            NodeInvitationLogic.ClampExpiryDays(requested).Should().Be(expected);
        }

        [Fact]
        public async Task Issue_StampsAnExpiryFromTheRequestedWindow()
        {
            using DataDbContext context = BuildDataContext(nameof(Issue_StampsAnExpiryFromTheRequestedWindow));
            DateTime before = DateTime.UtcNow;

            NodeInviteIssueResult result = await BuildLogic(context, "Admin")
                .Issue(Input(expiresInDays: 3), Guid.NewGuid(), "admin@example.com");

            result.Invite.ExpiresAt.Should().BeOnOrAfter(before.AddDays(3))
                .And.BeOnOrBefore(DateTime.UtcNow.AddDays(3));
        }

        [Fact]
        public async Task Issue_RecordsTheIssuer_ForAudit()
        {
            using DataDbContext context = BuildDataContext(nameof(Issue_RecordsTheIssuer_ForAudit));
            Guid issuer = Guid.NewGuid();

            NodeInviteIssueResult result = await BuildLogic(context, "Admin")
                .Issue(Input(), issuer, "  admin@example.com  ");

            result.Invite.IssuedByUserId.Should().Be(issuer);
            result.Invite.IssuedByEmail.Should().Be("admin@example.com");
        }

        // ---- 7. Counting, for the registration page's warning ------------------------------------

        [Fact]
        public async Task CountActive_CountsOnlyRedeemableInvitations()
        {
            using DataDbContext context = BuildDataContext(nameof(CountActive_CountsOnlyRedeemableInvitations));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");

            NodeInviteIssueResult live = await logic.Issue(Input(email: "a@school.example"), Guid.NewGuid(), "admin@example.com");
            NodeInviteIssueResult revoked = await logic.Issue(Input(email: "b@school.example"), Guid.NewGuid(), "admin@example.com");
            NodeInviteIssueResult used = await logic.Issue(Input(email: "c@school.example"), Guid.NewGuid(), "admin@example.com");

            await logic.Revoke(revoked.Invite.UUID, "admin@example.com");
            await logic.Consume(used.Invite.UUID, Guid.NewGuid());

            (await logic.CountActive()).Should().Be(1);
            live.Success.Should().BeTrue();
        }

        // ---- 8. Optional cohort linkage ---------------------------------------------------------

        [Fact]
        public async Task Invitation_WithNoCohort_LinksNothing()
        {
            // The default, and the pre-existing behaviour. Optional means optional.
            using DataDbContext context = BuildDataContext(nameof(Invitation_WithNoCohort_LinksNothing));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");

            NodeInviteIssueResult issued = await logic.Issue(Input(), Guid.NewGuid(), "admin@example.com");

            issued.Invite.CohortUUID.Should().BeNull();
            (await logic.LinkAcceptedUserToCohort(issued.Invite, Guid.NewGuid())).Should().BeFalse();
            (await context.CohortMember.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task Invitation_WithACohort_AddsTheAcceptedAccountToIt()
        {
            // The whole point: inviting a class becomes one step instead of two.
            using DataDbContext context = BuildDataContext(nameof(Invitation_WithACohort_AddsTheAcceptedAccountToIt));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");
            Cohort cohort = await SeedCohort(context, "Period 3 Biology");
            Guid accepted = Guid.NewGuid();

            InvitationIssueInputModel input = Input();
            input.CohortUUID = cohort.UUID;
            NodeInviteIssueResult issued = await logic.Issue(input, Guid.NewGuid(), "admin@example.com");

            issued.Invite.CohortUUID.Should().Be(cohort.UUID);
            (await logic.LinkAcceptedUserToCohort(issued.Invite, accepted)).Should().BeTrue();

            CohortMember member = await context.CohortMember.AsNoTracking().SingleAsync();
            member.UserId.Should().Be(accepted);
            member.CohortUUID.Should().Be(cohort.UUID);
        }

        [Fact]
        public async Task LinkingIsSkipped_WhenTheCohortWasDeletedAfterTheInvitationWasSent()
        {
            // Days pass between issue and acceptance. A cohort tidied up in the meantime must not
            // make the invitation unredeemable -- the account stands, only the linkage is skipped.
            using DataDbContext context = BuildDataContext(nameof(LinkingIsSkipped_WhenTheCohortWasDeletedAfterTheInvitationWasSent));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");
            Cohort cohort = await SeedCohort(context, "Doomed");

            InvitationIssueInputModel input = Input();
            input.CohortUUID = cohort.UUID;
            NodeInviteIssueResult issued = await logic.Issue(input, Guid.NewGuid(), "admin@example.com");

            context.Cohort.Remove(cohort);
            await context.SaveChangesAsync();

            (await logic.LinkAcceptedUserToCohort(issued.Invite, Guid.NewGuid())).Should()
                .BeFalse("a missing cohort is reported, not thrown");
            (await context.CohortMember.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task LinkingRefuses_AnEmptyUserId()
        {
            using DataDbContext context = BuildDataContext(nameof(LinkingRefuses_AnEmptyUserId));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");
            Cohort cohort = await SeedCohort(context, "Period 3 Biology");

            InvitationIssueInputModel input = Input();
            input.CohortUUID = cohort.UUID;
            NodeInviteIssueResult issued = await logic.Issue(input, Guid.NewGuid(), "admin@example.com");

            (await logic.LinkAcceptedUserToCohort(issued.Invite, Guid.Empty)).Should().BeFalse();
            (await logic.LinkAcceptedUserToCohort(null, Guid.NewGuid())).Should().BeFalse();
            (await context.CohortMember.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task CohortOptions_OffersTheNodesCohorts()
        {
            using DataDbContext context = BuildDataContext(nameof(CohortOptions_OffersTheNodesCohorts));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");

            (await logic.CohortOptions()).Should().BeEmpty("a node with no cohorts offers no picker");

            Cohort a = await SeedCohort(context, "Period 3 Biology");
            await SeedCohort(context, "Period 4 Chemistry");

            var options = await logic.CohortOptions();
            options.Should().HaveCount(2);
            options.Should().Contain(o => o.Uuid == a.UUID && o.Name == "Period 3 Biology");

            var names = await logic.CohortNames();
            names[a.UUID].Should().Be("Period 3 Biology");
        }

        // ---- 9. Every token is different --------------------------------------------------------

        [Fact]
        public async Task EveryIssuedToken_IsDistinct()
        {
            using DataDbContext context = BuildDataContext(nameof(EveryIssuedToken_IsDistinct));
            NodeInvitationLogic logic = BuildLogic(context, "Admin");

            var tokens = new List<string>();
            for (int i = 0; i < 25; i++)
            {
                NodeInviteIssueResult issued = await logic.Issue(
                    Input(email: "person" + i + "@school.example"), Guid.NewGuid(), "admin@example.com");
                issued.Success.Should().BeTrue();
                tokens.Add(issued.RawToken);
            }

            tokens.Distinct().Should().HaveCount(25);
            tokens.Should().OnlyContain(t => t.Length >= 40,
                "the token carries 256 bits of entropy, base64url encoded");
        }
    }
}
