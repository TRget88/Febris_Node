// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.UserModels;
using Febris.ModelLibrary.ViewModels;
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
    /// T10: bulk user creation abandoned the whole batch on one item's failure and then reported
    /// zeros for everything it had already done.
    ///
    /// <para>
    /// <c>UserLogic.Create</c> had a <c>return default</c> INSIDE its per-item loop, on the
    /// role-assignment failure branch. Nothing already committed was undone, because nothing could
    /// be: earlier users were already in Identity, already had Actors, and had already been sent
    /// verification emails. The cohort-linking block sits BELOW the loop, so it never ran and not
    /// one created user received their membership. And the return type is a value tuple of ints, so
    /// <c>default</c> is <c>(0,0,0,0)</c>, which the controller prints straight to the admin as
    /// "0 added, 0 not added, 0 cohort links" while dozens of real accounts existed.
    /// </para>
    ///
    /// <para>
    /// This is NOT a transaction defect and no transaction would have fixed it. Identity writes and
    /// outbound email are not rollback-able, so the answer is to compensate for the one item that
    /// failed and carry on with the rest, reporting honest counts.
    /// </para>
    /// </summary>
    public class BulkUserBatchAbandonmentTests
    {
        private static Mock<UserManager<LocalApplicationUser>> MockUserManager()
        {
            Mock<IUserStore<LocalApplicationUser>> store = new Mock<IUserStore<LocalApplicationUser>>();
            return new Mock<UserManager<LocalApplicationUser>>(
                store.Object, null, null, null, null, null, null, null, null);
        }

        private static IHttpContextAccessor AdminAccessor()
        {
            // Admin, so the role-rank policy allows creating User accounts and the entry guard at
            // the top of Create passes. A principal that failed the guard would return default for a
            // completely different reason and the test would pass for the wrong one.
            ClaimsIdentity identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Role, InstitutionUserAccountType.Admin.ToString()) }, "test");
            DefaultHttpContext context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.SetupGet(a => a.HttpContext).Returns(context);
            return accessor.Object;
        }

        private static BulkUserCreationSubmitListViewModel Batch(params string[] emails)
        {
            List<BulkUserCreationSubmitViewModel> list = new List<BulkUserCreationSubmitViewModel>();
            foreach (string e in emails)
            {
                list.Add(new BulkUserCreationSubmitViewModel
                {
                    FirstName = "Test",
                    LastName = "User",
                    EmailAddress = e,
                });
            }
            return new BulkUserCreationSubmitListViewModel
            {
                AccountType = InstitutionUserAccountType.User,
                SubmissionList = list,
                SelectedCohortList = new List<Guid?>(),
            };
        }

        private static UserLogic BuildLogic(Mock<UserManager<LocalApplicationUser>> users)
        {
            // Create pre-resolves existing Actors by hashed mbox and then iterates the result. Moq
            // hands back a completed Task whose value is a NULL list by default, which NREs on the
            // foreach before the loop under test is ever reached.
            Mock<IActorLogic> actors = new Mock<IActorLogic>();
            actors.Setup(a => a.GetByHashedMboxList(It.IsAny<List<string>>()))
                .ReturnsAsync(new List<Febris.ModelLibrary.Models.XApiModels.Actor>());

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

        /// <summary>
        /// Every item fails role assignment. Deliberate: a user that SUCCEEDS runs on to build a
        /// confirmation email from static configuration, which a unit test cannot exercise. The
        /// failure branch returns before that point, so this isolates the batch-control behaviour
        /// that regressed without dragging the mail path in.
        /// </summary>
        private static Mock<UserManager<LocalApplicationUser>> UserManagerWhereEveryRoleAddFails()
        {
            Mock<UserManager<LocalApplicationUser>> users = MockUserManager();

            users.Setup(u => u.CreateAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.RemoveFromRolesAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.AddToRoleAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "role missing" }));
            users.Setup(u => u.DeleteAsync(It.IsAny<LocalApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((LocalApplicationUser)null);

            // Create pre-loads existing users with ToListAsync() to detect duplicate addresses. A
            // plain List.AsQueryable() throws on the async operator, so this has to be an
            // async-capable queryable. Empty, so nothing in the batch counts as a duplicate.
            users.Setup(u => u.Users)
                .Returns(TestAsyncQueryable.From(new List<LocalApplicationUser>()));

            return users;
        }

        [Fact]
        public async Task OneFailedItemDoesNotAbandonTheRestOfTheBatch()
        {
            // THE regression. The old code returned from inside the loop on the FIRST failure, so
            // items two and three were never attempted at all.
            Mock<UserManager<LocalApplicationUser>> users = UserManagerWhereEveryRoleAddFails();

            await BuildLogic(users).Create(Batch("a@example.com", "b@example.com", "c@example.com"));

            users.Verify(u => u.CreateAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<string>()), Times.Exactly(3),
                "every item in the batch must be attempted, not just the ones before the first failure");
        }

        [Fact]
        public async Task TheCountsAreHonestRatherThanAZeroTuple()
        {
            // "default" on this return type is (0,0,0,0), which the controller prints verbatim as
            // "0 added, 0 not added". An admin was told nothing had happened while accounts existed.
            Mock<UserManager<LocalApplicationUser>> users = UserManagerWhereEveryRoleAddFails();

            (int UsersAdded, int UsersNotAdded, int cohortLinksMade, int DuplicateEmailAddresses) result =
                await BuildLogic(users).Create(Batch("a@example.com", "b@example.com", "c@example.com"));

            result.UsersAdded.Should().Be(0);
            result.UsersNotAdded.Should().Be(3, "each failed item must be counted, not silently dropped");
        }

        [Fact]
        public async Task APartCreatedUserIsRemovedRatherThanLeftWithoutARole()
        {
            // CreateAsync has already committed a row by this point. Left behind, it is an account
            // that can authenticate but satisfies no role check anywhere on the node. Deleting is
            // safe here specifically because the confirmation email is sent further down, after the
            // role step, so nobody has been told about an account that is about to vanish.
            Mock<UserManager<LocalApplicationUser>> users = UserManagerWhereEveryRoleAddFails();

            await BuildLogic(users).Create(Batch("a@example.com", "b@example.com"));

            users.Verify(u => u.DeleteAsync(It.IsAny<LocalApplicationUser>()), Times.Exactly(2),
                "each part-created account must be compensated for");
        }

        [Fact]
        public async Task AFailedCompensationStillLetsTheBatchFinish()
        {
            // If the cleanup delete itself fails there is nothing further to be done in-process. It
            // is logged for manual removal, and the remaining items must still be attempted rather
            // than the batch dying on the way out.
            Mock<UserManager<LocalApplicationUser>> users = UserManagerWhereEveryRoleAddFails();
            users.Setup(u => u.DeleteAsync(It.IsAny<LocalApplicationUser>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "delete failed" }));

            (int UsersAdded, int UsersNotAdded, int cohortLinksMade, int DuplicateEmailAddresses) result =
                await BuildLogic(users).Create(Batch("a@example.com", "b@example.com"));

            result.UsersNotAdded.Should().Be(2);
            users.Verify(u => u.CreateAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<string>()), Times.Exactly(2));
        }
    }
}
