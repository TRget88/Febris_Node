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
using Febris.UserNode.Portal.Controllers.User;
using Febris.UserNode.Portal.IdentityPolicy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// A malformed bulk user payload must produce a clean 400, not an unhandled 500.
    ///
    /// <para>
    /// FOUND AT RUNTIME on 2026-08-09 while verifying the antiforgery fix. A POST to
    /// <c>/User/BulkCreatePost</c> whose JSON property names do not match
    /// <c>BulkUserCreationSubmitListViewModel</c> deserializes to a model with a null
    /// <c>SubmissionList</c>. <c>UserLogic.Create</c> then called <c>Enumerable.Count()</c> on it:
    /// </para>
    /// <code>
    /// System.ArgumentNullException: Value cannot be null. (Parameter 'source')
    ///    at System.Linq.Enumerable.Count[TSource](IEnumerable`1 source)
    ///    at UserLogic.Create(...) UserLogic.cs:483
    ///    at UserController.BulkCreatePost(...) UserController.cs:454
    /// </code>
    ///
    /// <para>
    /// The controller's catch block does <c>throw;</c>, so the exception escaped as a 500 rather
    /// than reaching the <c>BadRequest(...)</c> at the bottom of the method that was already there
    /// for bad input. <c>UserLogic.Removal</c> shares the view model and had the same defect by a
    /// different route: <c>foreach (var i in bulkInput.SubmissionList)</c>, a NullReferenceException.
    /// </para>
    ///
    /// <para>
    /// The fix guards at BOTH layers, and these tests pin both. The controller returns 400 because
    /// that is the user-facing contract and it mirrors the guard <c>BulkCreateCsvPost</c> already
    /// had. The BLL additionally refuses without throwing, so no other caller can crash it.
    /// </para>
    ///
    /// <para>
    /// EMPTY is treated as an error, not a no-op. Returning "0 were added" for an empty paste is the
    /// silent-success shape this audit removed elsewhere: it reads as a completed operation.
    /// </para>
    /// </summary>
    public class BulkUserNullPayloadTests
    {
        // ---------- controller-level: the user-facing 400 ----------

        private static Mock<UserManager<LocalApplicationUser>> MockUserManager()
        {
            Mock<IUserStore<LocalApplicationUser>> store = new Mock<IUserStore<LocalApplicationUser>>();
            return new Mock<UserManager<LocalApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
        }

        private static UserController BuildController(Mock<IUserLogic> logic)
        {
            return new UserController(
                logic.Object,
                NullLogger<UserController>.Instance,
                MockUserManager().Object,
                new ConfigurationBuilder().Build(),
                Mock.Of<ICohortMemberLogic>(),
                Mock.Of<ICsvUserImporter>());
        }

        public static IEnumerable<object[]> MalformedBatches()
        {
            yield return new object[] { null, "a null model (empty or unparseable body)" };
            yield return new object[] { new BulkUserCreationSubmitListViewModel(), "a model whose SubmissionList never bound" };
            yield return new object[]
            {
                new BulkUserCreationSubmitListViewModel { SubmissionList = new List<BulkUserCreationSubmitViewModel>() },
                "an empty submission list"
            };
        }

        [Theory]
        [MemberData(nameof(MalformedBatches))]
        public async Task BulkCreatePost_Returns400_AndNeverReachesTheLogic(
            BulkUserCreationSubmitListViewModel batch, string because)
        {
            Mock<IUserLogic> logic = new Mock<IUserLogic>(MockBehavior.Strict);
            UserController controller = BuildController(logic);

            IActionResult result = await controller.BulkCreatePost(batch);

            result.Should().BeOfType<BadRequestObjectResult>("bulk create must refuse " + because + " with a 400");

            // MockBehavior.Strict would already throw on any call, but assert it explicitly: the
            // whole point is that the throwing code is never entered.
            logic.Verify(l => l.Create(It.IsAny<BulkUserCreationSubmitListViewModel>()), Times.Never);
        }

        [Theory]
        [MemberData(nameof(MalformedBatches))]
        public async Task BulkRemovalPost_Returns400_AndNeverReachesTheLogic(
            BulkUserCreationSubmitListViewModel batch, string because)
        {
            Mock<IUserLogic> logic = new Mock<IUserLogic>(MockBehavior.Strict);
            UserController controller = BuildController(logic);

            IActionResult result = await controller.BulkRemovalPost(batch);

            result.Should().BeOfType<BadRequestObjectResult>("bulk removal must refuse " + because + " with a 400");

            logic.Verify(l => l.Removal(It.IsAny<BulkUserCreationSubmitListViewModel>()), Times.Never);
        }

        // ---------- BLL-level: refuse without throwing ----------

        /// <summary>
        /// SuperAdmin, because the role filter runs BEFORE the null guard. Without an authorized
        /// principal the method would return at the filter and the guard would never be exercised,
        /// so the test would pass for the wrong reason.
        /// </summary>
        private static IHttpContextAccessor AdminAccessor()
        {
            ClaimsIdentity identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Role, FebrisUserType.SuperAdmin.ToString()) }, "test");
            DefaultHttpContext context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.SetupGet(a => a.HttpContext).Returns(context);
            return accessor.Object;
        }

        private static UserLogic BuildLogic()
        {
            return new UserLogic(
                AdminAccessor(),
                MockUserManager().Object,
                Mock.Of<IUrlHelperFactory>(),
                Mock.Of<IActionContextAccessor>(),
                Mock.Of<IPasswordGenerator>(),
                Mock.Of<IImageFileHandler>(),
                Mock.Of<IActorLogic>(),
                Mock.Of<ICohortQueries>(),
                Mock.Of<ICohortMemberQueries>(),
                Mock.Of<IParentLinkLogic>(),
                Mock.Of<IEmailSender>());
        }

        [Fact]
        public async Task UserLogic_Create_RefusesANullBatch_WithoutThrowing()
        {
            UserLogic logic = BuildLogic();

            (int UsersAdded, int UsersNotAdded, int cohortLinksMade, int DuplicateEmailAddresses) result =
                await logic.Create((BulkUserCreationSubmitListViewModel)null);

            result.UsersAdded.Should().Be(0);
            result.UsersNotAdded.Should().Be(0);
            result.cohortLinksMade.Should().Be(0);
            result.DuplicateEmailAddresses.Should().Be(0);
        }

        [Fact]
        public async Task UserLogic_Create_RefusesANullSubmissionList_WithoutThrowing()
        {
            UserLogic logic = BuildLogic();

            // This is the exact shape the runtime 500 came from: the model bound, the list did not.
            (int UsersAdded, int UsersNotAdded, int cohortLinksMade, int DuplicateEmailAddresses) result =
                await logic.Create(new BulkUserCreationSubmitListViewModel());

            result.UsersAdded.Should().Be(0);
            result.UsersNotAdded.Should().Be(0);
        }

        [Fact]
        public async Task UserLogic_Removal_RefusesANullBatch_WithoutThrowing()
        {
            UserLogic logic = BuildLogic();

            (int UsersAdded, int UsersNotAdded, int cohortLinksMade, int DuplicateEmailAddresses) result =
                await logic.Removal((BulkUserCreationSubmitListViewModel)null);

            result.UsersAdded.Should().Be(0);
            result.UsersNotAdded.Should().Be(0);
        }

        [Fact]
        public async Task UserLogic_Removal_RefusesANullSubmissionList_WithoutThrowing()
        {
            UserLogic logic = BuildLogic();

            (int UsersAdded, int UsersNotAdded, int cohortLinksMade, int DuplicateEmailAddresses) result =
                await logic.Removal(new BulkUserCreationSubmitListViewModel());

            result.UsersAdded.Should().Be(0);
            result.UsersNotAdded.Should().Be(0);
        }
    }
}
