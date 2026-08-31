// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.UserModels;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.UserNode.Portal.Areas.Identity.Pages.Account.Manage;
using Febris.UserNode.Portal.IdentityPolicy;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// AccountLifecycle.SoftDeleteOnly enforcement in DeletePersonalDataModel.OnPostAsync: when on (default),
    /// self-service deletion RETAINS the row (IsDeleted + DeletedUtc) and LOCKS it (LockoutEnd=MaxValue,
    /// blocking sign-in) via UpdateAsync instead of a hard DeleteAsync; when off, it hard-deletes. The
    /// AllowSelfServiceDeletion gate still guards the whole action.
    /// </summary>
    public class SoftDeleteAccountTests
    {
        private static IOptions<IdentityPolicyOptions> Options(bool softDeleteOnly, bool allowSelfDelete = true) =>
            Microsoft.Extensions.Options.Options.Create(new IdentityPolicyOptions
            {
                AccountLifecycle = new AccountLifecycleOptions
                {
                    SoftDeleteOnly = softDeleteOnly,
                    AllowSelfServiceDeletion = allowSelfDelete
                }
            });

        private static Mock<UserManager<LocalApplicationUser>> MockUserManager(LocalApplicationUser user)
        {
            var store = new Mock<IUserStore<LocalApplicationUser>>();
            var um = new Mock<UserManager<LocalApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
            um.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
            um.Setup(m => m.GetUserIdAsync(user)).ReturnsAsync(user.Id.ToString());
            um.Setup(m => m.HasPasswordAsync(user)).ReturnsAsync(false); // no password prompt on this path
            um.Setup(m => m.UpdateAsync(It.IsAny<LocalApplicationUser>())).ReturnsAsync(IdentityResult.Success);
            um.Setup(m => m.DeleteAsync(It.IsAny<LocalApplicationUser>())).ReturnsAsync(IdentityResult.Success);
            return um;
        }

        private static SignInManager<LocalApplicationUser> MockSignInManager(UserManager<LocalApplicationUser> um)
        {
            var sim = new Mock<SignInManager<LocalApplicationUser>>(
                um,
                new Mock<IHttpContextAccessor>().Object,
                new Mock<IUserClaimsPrincipalFactory<LocalApplicationUser>>().Object,
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                NullLogger<SignInManager<LocalApplicationUser>>.Instance,
                new Mock<IAuthenticationSchemeProvider>().Object,
                new Mock<IUserConfirmation<LocalApplicationUser>>().Object);
            sim.Setup(s => s.SignOutAsync()).Returns(Task.CompletedTask);
            return sim.Object;
        }

        private static DeletePersonalDataModel BuildModel(
            Mock<UserManager<LocalApplicationUser>> um, IOptions<IdentityPolicyOptions> options,
            Mock<ICohortMemberLogic> cohort = null,
            Mock<Febris.SharedServices.IImageFileHandler> images = null,
            Mock<Febris.PrimaryLogicLayer.Logic.XApiLogic.IActorLogic> actors = null)
        {
            cohort ??= new Mock<ICohortMemberLogic>();
            cohort.Setup(c => c.RemoveAllForUser(It.IsAny<Guid>())).ReturnsAsync(0);
            images ??= new Mock<Febris.SharedServices.IImageFileHandler>();
            images.Setup(i => i.DeleteProfileImage(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(true);
            actors ??= new Mock<Febris.PrimaryLogicLayer.Logic.XApiLogic.IActorLogic>();
            actors.Setup(a => a.Pseudonymise(It.IsAny<Guid>())).ReturnsAsync(true);
            return new DeletePersonalDataModel(um.Object, MockSignInManager(um.Object),
                NullLogger<DeletePersonalDataModel>.Instance, cohort.Object, options, images.Object, actors.Object)
            {
                PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
            };
        }

        [Fact]
        public async Task SoftDeleteOnly_RetainsAndLocks_DoesNotHardDelete()
        {
            var user = new LocalApplicationUser { Id = Guid.NewGuid() };
            var um = MockUserManager(user);
            var model = BuildModel(um, Options(softDeleteOnly: true));

            IActionResult result = await model.OnPostAsync();

            user.IsDeleted.Should().BeTrue();
            user.DeletedUtc.Should().NotBeNull();
            user.LockoutEnabled.Should().BeTrue();
            user.LockoutEnd.Should().Be(DateTimeOffset.MaxValue);
            um.Verify(m => m.UpdateAsync(user), Times.Once);
            um.Verify(m => m.DeleteAsync(It.IsAny<LocalApplicationUser>()), Times.Never,
                "SoftDeleteOnly must NOT hard-delete the row");
            result.Should().BeOfType<RedirectResult>();
        }

        [Fact]
        public async Task SoftDelete_RemovesAllCohortMemberships()
        {
            var user = new LocalApplicationUser { Id = Guid.NewGuid() };
            var um = MockUserManager(user);
            var cohort = new Mock<ICohortMemberLogic>();
            var model = BuildModel(um, Options(softDeleteOnly: true), cohort);

            await model.OnPostAsync();

            cohort.Verify(c => c.RemoveAllForUser(user.Id), Times.Once,
                "a deleted account cannot remain a cohort member");
        }

        [Fact]
        public async Task SoftDeleteOff_HardDeletes()
        {
            var user = new LocalApplicationUser { Id = Guid.NewGuid() };
            var um = MockUserManager(user);
            var model = BuildModel(um, Options(softDeleteOnly: false));

            IActionResult result = await model.OnPostAsync();

            um.Verify(m => m.DeleteAsync(user), Times.Once);
            um.Verify(m => m.UpdateAsync(It.IsAny<LocalApplicationUser>()), Times.Never);
            user.IsDeleted.Should().BeFalse();
            result.Should().BeOfType<RedirectResult>();
        }

        [Fact]
        public async Task SelfServiceDeletionDisabled_ReturnsNotFound_AndTouchesNothing()
        {
            var user = new LocalApplicationUser { Id = Guid.NewGuid() };
            var um = MockUserManager(user);
            var model = BuildModel(um, Options(softDeleteOnly: true, allowSelfDelete: false));

            IActionResult result = await model.OnPostAsync();

            result.Should().BeOfType<NotFoundResult>();
            um.Verify(m => m.UpdateAsync(It.IsAny<LocalApplicationUser>()), Times.Never);
            um.Verify(m => m.DeleteAsync(It.IsAny<LocalApplicationUser>()), Times.Never);
        }

        // ------------------------------------------------------------------
        // Profile photographs: an ERASURE gap, not a retention one
        // ------------------------------------------------------------------

        [Fact]
        public async Task SoftDeleteRemovesTheProfilePhotograph()
        {
            // Deleting an account left the photograph on disk in BOTH branches, and no image delete
            // existed anywhere in the repo. Any learner can upload one, it is stored under their own
            // user id and served to browsers, so a "deleted" account still had a photograph of its
            // owner on disk indefinitely.
            //
            // It goes on SOFT delete too, deliberately. The row is retained for xAPI history and a
            // photograph contributes nothing to that, and this node ships SoftDeleteOnly with
            // PurgeAfterDays unset, so deferring to purge time would mean never.
            var user = new LocalApplicationUser { Id = Guid.NewGuid(), ProfilePicturePath = "photo.png" };
            var um = MockUserManager(user);
            var images = new Mock<Febris.SharedServices.IImageFileHandler>();
            images.Setup(i => i.DeleteProfileImage(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(true);

            await BuildModel(um, Options(softDeleteOnly: true), images: images).OnPostAsync();

            images.Verify(i => i.DeleteProfileImage(user.Id, "photo.png"), Times.Once);
        }

        [Fact]
        public async Task HardDeleteRemovesTheProfilePhotograph()
        {
            var user = new LocalApplicationUser { Id = Guid.NewGuid(), ProfilePicturePath = "photo.jpg" };
            var um = MockUserManager(user);
            var images = new Mock<Febris.SharedServices.IImageFileHandler>();
            images.Setup(i => i.DeleteProfileImage(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(true);

            await BuildModel(um, Options(softDeleteOnly: false), images: images).OnPostAsync();

            images.Verify(i => i.DeleteProfileImage(user.Id, "photo.jpg"), Times.Once);
        }

        [Fact]
        public async Task AFailedPhotographDeleteDoesNotFailTheAccountDeletion()
        {
            // Best-effort, matching the cohort cleanup beside it. Refusing to delete someone's
            // account because an image file was locked would be the worse outcome.
            var user = new LocalApplicationUser { Id = Guid.NewGuid(), ProfilePicturePath = "photo.png" };
            var um = MockUserManager(user);
            var images = new Mock<Febris.SharedServices.IImageFileHandler>();
            images.Setup(i => i.DeleteProfileImage(It.IsAny<Guid>(), It.IsAny<string>()))
                .ThrowsAsync(new System.IO.IOException("file locked"));

            IActionResult result = await BuildModel(um, Options(softDeleteOnly: true), images: images).OnPostAsync();

            result.Should().NotBeNull();
            user.IsDeleted.Should().BeTrue("the account deletion must still have happened");
        }


        // ------------------------------------------------------------------
        // xAPI Actor: pseudonymised, never deleted
        // ------------------------------------------------------------------

        [Fact]
        public async Task HardDeletePseudonymisesTheLearnersActor()
        {
            // The Identity row is gone, so the learner's name and address must not survive in the
            // xAPI Actor. It is pseudonymised rather than deleted because
            // FK_LocalStatement_Actor_ActorId is ON DELETE CASCADE over a NOT NULL column: removing
            // the Actor would delete every statement that learner ever produced.
            Guid actorUuid = Guid.NewGuid();
            var user = new LocalApplicationUser { Id = Guid.NewGuid(), Actor = actorUuid };
            var um = MockUserManager(user);
            var actors = new Mock<Febris.PrimaryLogicLayer.Logic.XApiLogic.IActorLogic>();
            actors.Setup(a => a.Pseudonymise(It.IsAny<Guid>())).ReturnsAsync(true);

            await BuildModel(um, Options(softDeleteOnly: false), actors: actors).OnPostAsync();

            actors.Verify(a => a.Pseudonymise(actorUuid), Times.Once);
        }

        [Fact]
        public async Task SoftDeleteLeavesTheActorAlone()
        {
            // Deliberately NOT done on the soft branch. There the account row is retained on
            // purpose, and the actor's name is part of what makes that retained history readable.
            Guid actorUuid = Guid.NewGuid();
            var user = new LocalApplicationUser { Id = Guid.NewGuid(), Actor = actorUuid };
            var um = MockUserManager(user);
            var actors = new Mock<Febris.PrimaryLogicLayer.Logic.XApiLogic.IActorLogic>();

            await BuildModel(um, Options(softDeleteOnly: true), actors: actors).OnPostAsync();

            actors.Verify(a => a.Pseudonymise(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task AFailedPseudonymisationDoesNotFailTheAccountDeletion()
        {
            Guid actorUuid = Guid.NewGuid();
            var user = new LocalApplicationUser { Id = Guid.NewGuid(), Actor = actorUuid };
            var um = MockUserManager(user);
            var actors = new Mock<Febris.PrimaryLogicLayer.Logic.XApiLogic.IActorLogic>();
            actors.Setup(a => a.Pseudonymise(It.IsAny<Guid>())).ThrowsAsync(new InvalidOperationException("xapi db down"));

            IActionResult result = await BuildModel(um, Options(softDeleteOnly: false), actors: actors).OnPostAsync();

            result.Should().NotBeNull();
            um.Verify(u => u.DeleteAsync(user), Times.Once, "the account deletion must still have happened");
        }

    }
}
