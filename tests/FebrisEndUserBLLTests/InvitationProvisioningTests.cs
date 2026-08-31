// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.UserModels;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.PrimaryLogicLayer.Logic.UserLogic;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins <c>ProvisionUserAsync</c> across all THREE of its callers (invitation flow 2026-08-21).
    ///
    /// <para>
    /// The invitation work added two optional parameters to that primitive -- a role and an
    /// email-confirmed flag -- rather than copying its hundred lines of Actor-first ordering and
    /// rollback into a fourth place. That is the right call, and it is also the kind of change that
    /// quietly alters two existing security-relevant paths, so both of them are asserted here
    /// UNCHANGED alongside the new one.
    /// </para>
    ///
    /// <para>
    /// The two properties that must not drift for self-service: the role is the least-privileged
    /// <c>User</c>, and the email starts UNCONFIRMED so sign-in is gated until the address is
    /// proved. Invitation acceptance legitimately differs on both, and the reason it may is that
    /// redeeming a token delivered solely to that address IS the proof of the address, and an
    /// operator already chose the role under the rank policy.
    /// </para>
    /// </summary>
    public class InvitationProvisioningTests
    {
        private static Mock<UserManager<LocalApplicationUser>> MockUserManager()
        {
            Mock<IUserStore<LocalApplicationUser>> store = new Mock<IUserStore<LocalApplicationUser>>();
            return new Mock<UserManager<LocalApplicationUser>>(
                store.Object, null, null, null, null, null, null, null, null);
        }

        private static IHttpContextAccessor AdminAccessor()
        {
            ClaimsIdentity identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Role, InstitutionUserAccountType.Admin.ToString()) }, "test");
            DefaultHttpContext context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.SetupGet(a => a.HttpContext).Returns(context);
            return accessor.Object;
        }

        /// <summary>Captures what the provisioning primitive actually built and asked for.</summary>
        private sealed class Capture
        {
            public LocalApplicationUser CreatedUser;
            public string GrantedRole;
            public string SuppliedPassword;
        }

        private static UserLogic BuildLogic(Capture capture)
        {
            Mock<UserManager<LocalApplicationUser>> users = MockUserManager();

            // No existing account for the address, so provisioning proceeds past its reservation check.
            users.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((LocalApplicationUser)null);

            users.Setup(u => u.CreateAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<string>()))
                .Callback<LocalApplicationUser, string>((u, p) => { capture.CreatedUser = u; capture.SuppliedPassword = p; })
                .ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.CreateAsync(It.IsAny<LocalApplicationUser>()))
                .Callback<LocalApplicationUser>(u => capture.CreatedUser = u)
                .ReturnsAsync(IdentityResult.Success);

            users.Setup(u => u.AddToRoleAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<string>()))
                .Callback<LocalApplicationUser, string>((u, r) => capture.GrantedRole = r)
                .ReturnsAsync(IdentityResult.Success);

            users.Setup(u => u.AddLoginAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<UserLoginInfo>()))
                .ReturnsAsync(IdentityResult.Success);

            Mock<IActorLogic> actors = new Mock<IActorLogic>();
            actors.Setup(a => a.Create(It.IsAny<Actor>()))
                .ReturnsAsync(new Actor() { UUID = Guid.NewGuid() });

            return new UserLogic(
                AdminAccessor(),
                users.Object,
                Mock.Of<IUrlHelperFactory>(),
                Mock.Of<IActionContextAccessor>(),
                Mock.Of<IPasswordGenerator>(),
                Mock.Of<IImageFileHandler>(),
                actors.Object,
                Mock.Of<ICohortQueries>(),
                Mock.Of<ICohortMemberQueries>(),
                Mock.Of<IParentLinkLogic>(),
                Mock.Of<IEmailSender>());
        }

        // ---- The two existing callers are UNCHANGED ---------------------------------------------

        [Fact]
        public async Task SelfRegistration_StillGrantsOnlyUser_AndStillStartsUnconfirmed()
        {
            Capture capture = new Capture();

            var (user, errors) = await BuildLogic(capture).CreateSelfRegistered(
                "Ada", "Lovelace", "ada@school.example", "P@ssw0rd!", requireApproval: false);

            user.Should().NotBeNull();
            capture.GrantedRole.Should().Be(InstitutionUserAccountType.User.ToString(),
                "adding a role parameter must not have changed what self-registration grants");
            capture.CreatedUser.EmailConfirmed.Should().BeFalse(
                "self-registration must still prove the address separately");
        }

        [Fact]
        public async Task ExternalProvisioning_StillGrantsOnlyUser_AndStillStartsUnconfirmed()
        {
            Capture capture = new Capture();

            var (user, errors) = await BuildLogic(capture).CreateExternallyProvisioned(
                "Ada", "Lovelace", "ada@school.example",
                new UserLoginInfo("TestIdp", "key", "TestIdp"), requireApproval: false);

            user.Should().NotBeNull();
            capture.GrantedRole.Should().Be(InstitutionUserAccountType.User.ToString());
            capture.CreatedUser.EmailConfirmed.Should().BeFalse();
        }

        [Fact]
        public async Task ApprovalHold_StillLocksTheAccount()
        {
            // LockoutEnabled is load-bearing: IsLockedOutAsync ignores LockoutEnd without it, which
            // would make RequireAdminApproval a silent no-op. Asserted here because the invitation
            // change touched the same construction block.
            Capture capture = new Capture();

            await BuildLogic(capture).CreateSelfRegistered(
                "Ada", "Lovelace", "ada@school.example", "P@ssw0rd!", requireApproval: true);

            capture.CreatedUser.LockoutEnabled.Should().BeTrue();
            capture.CreatedUser.LockoutEnd.Should().Be(DateTimeOffset.MaxValue);
        }

        // ---- The invitation caller ---------------------------------------------------------------

        [Theory]
        [InlineData("User")]
        [InlineData("Educator")]
        [InlineData("Admin")]
        [InlineData("ITAdmin")]
        public async Task InvitationAcceptance_GrantsTheInvitedRole(string role)
        {
            Capture capture = new Capture();

            var (user, errors) = await BuildLogic(capture).CreateFromInvitation(
                "Ada", "Lovelace", "ada@school.example", "P@ssw0rd!", role);

            user.Should().NotBeNull();
            capture.GrantedRole.Should().Be(role,
                "the role was chosen by an operator the rank policy already cleared");
        }

        [Fact]
        public async Task InvitationAcceptance_CreatesTheAccountWithAConfirmedEmail()
        {
            // Redeeming a token that was only ever delivered to that address IS what email
            // confirmation proves, so sending a confirmation mail afterwards would be theatre and
            // would leave the account unusable until a second round trip.
            Capture capture = new Capture();

            await BuildLogic(capture).CreateFromInvitation(
                "Ada", "Lovelace", "ada@school.example", "P@ssw0rd!", "User");

            capture.CreatedUser.EmailConfirmed.Should().BeTrue();
        }

        [Fact]
        public async Task InvitationAcceptance_DoesNotApplyTheApprovalHold()
        {
            // An invited person was named by an operator. That IS the approval, and holding them
            // behind a second one would strand them behind an approval queue whose UI does not work.
            Capture capture = new Capture();

            await BuildLogic(capture).CreateFromInvitation(
                "Ada", "Lovelace", "ada@school.example", "P@ssw0rd!", "User");

            capture.CreatedUser.LockoutEnd.Should().NotBe(DateTimeOffset.MaxValue);
        }

        [Fact]
        public async Task InvitationAcceptance_SetsThePasswordTheInviteeChose()
        {
            Capture capture = new Capture();

            await BuildLogic(capture).CreateFromInvitation(
                "Ada", "Lovelace", "ada@school.example", "ThePasswordTheyChose1!", "User");

            capture.SuppliedPassword.Should().Be("ThePasswordTheyChose1!",
                "the invitee sets their own password -- nothing is generated and mailed");
        }

        [Fact]
        public async Task InvitationAcceptance_StillLinksAnXapiActor()
        {
            // No orphan accounts: the same invariant both self-service paths uphold. An invited
            // learner whose statements had nowhere to attach would be a silent data defect.
            Capture capture = new Capture();

            await BuildLogic(capture).CreateFromInvitation(
                "Ada", "Lovelace", "ada@school.example", "P@ssw0rd!", "User");

            capture.CreatedUser.Actor.Should().NotBeEmpty();
        }

        [Fact]
        public async Task InvitationAcceptance_RefusesAnAddressThatIsAlreadyTaken()
        {
            // The window between issuing an invitation and redeeming it is days long, so the address
            // really can be claimed in between.
            Capture capture = new Capture();
            Mock<UserManager<LocalApplicationUser>> users = MockUserManager();
            users.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(new LocalApplicationUser() { Email = "ada@school.example" });

            Mock<IActorLogic> actors = new Mock<IActorLogic>();
            UserLogic logic = new UserLogic(
                AdminAccessor(), users.Object, Mock.Of<IUrlHelperFactory>(), Mock.Of<IActionContextAccessor>(),
                Mock.Of<IPasswordGenerator>(), Mock.Of<IImageFileHandler>(), actors.Object,
                Mock.Of<ICohortQueries>(), Mock.Of<ICohortMemberQueries>(), Mock.Of<IParentLinkLogic>(),
                Mock.Of<IEmailSender>());

            var (user, errors) = await logic.CreateFromInvitation(
                "Ada", "Lovelace", "ada@school.example", "P@ssw0rd!", "User");

            user.Should().BeNull();
            errors.Should().Contain(e => e.Code == "DuplicateEmail");
            actors.Verify(a => a.Create(It.IsAny<Actor>()), Times.Never,
                "the address is reserved BEFORE an Actor is minted, so a duplicate leaves no orphan");
        }
    }
}
