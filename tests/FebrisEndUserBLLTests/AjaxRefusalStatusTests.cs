// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading.Tasks;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.PrimaryLogicLayer.Logic.DataLogic;
using Febris.PrimaryLogicLayer.Logic.UserLogic;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.Portal.Controllers.Data;
using Febris.UserNode.Portal.Controllers.Data.Local;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// ROADMAP 20, second pass: a mutating action must not answer a FAILURE with a success status.
    ///
    /// <para>
    /// WHY THIS EXISTS, and it is not a hypothetical. ROADMAP 20 gave the three Manage*Index pages a
    /// success toast on their add/remove calls, replacing a <c>confirm()</c> that fired on the same
    /// callback. Fact-checking the pull request turned up what that actually shipped: all six of
    /// those actions SWALLOW their exception and return <c>HTTP 200</c> with the string "No new Item
    /// was added". So the browser could not tell a failure from a success, and the new toast
    /// confidently announced one. That is strictly worse than the silence it replaced, and it is the
    /// exact "reports success while dropping rows" defect ROADMAP 20 exists to remove.
    /// </para>
    ///
    /// <para>
    /// Fixed on the SERVER rather than by sniffing the response string in the browser. The string is
    /// the server's prose, and a client that parses it starts lying the day somebody rewords it --
    /// the same reasoning already written into the bulk-import call site.
    /// </para>
    ///
    /// <para>
    /// The two archive toggles were the same shape by a different route: both computed a boolean and
    /// then DISCARDED it, returning <c>Ok()</c> either way. Honouring it exposed a second defect one
    /// layer down, which <c>MessageBoardLogic_ToggleArchive_reports_success_when_it_succeeds</c>
    /// pins: the logic method never assigned its result variable, so it reported failure on every
    /// call including the ones that worked. Reading the boolean without fixing that would have
    /// turned a working feature into a 404 on every click.
    /// </para>
    /// </summary>
    public class AjaxRefusalStatusTests
    {
        /// <summary>
        /// TempData has to be real: these actions assign the <c>StatusMessage</c> property, which
        /// writes through to <c>TempData</c>, and an unset TempData throws before the status code
        /// under test is ever produced.
        /// </summary>
        private static void GiveTempData(Controller controller)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
                RouteData = new RouteData(),
                ActionDescriptor = new ControllerActionDescriptor()
            };
            controller.TempData = new TempDataDictionary(
                controller.ControllerContext.HttpContext, Mock.Of<ITempDataProvider>());
        }

        private static int StatusOf(IActionResult result)
        {
            switch (result)
            {
                case ObjectResult o when o.StatusCode.HasValue:
                    return o.StatusCode.Value;
                case StatusCodeResult s:
                    return s.StatusCode;
                case JsonResult:
                    // Json() with no explicit status is a 200.
                    return StatusCodes.Status200OK;
                case ForbidResult:
                    // ForbidResult carries no status code of its own; the auth handler turns it into
                    // a 403. Reported as 403 here so the assertions read the way the wire does.
                    return StatusCodes.Status403Forbidden;
                default:
                    throw new InvalidOperationException(
                        "unhandled result type " + result.GetType().Name);
            }
        }

        // ------------------------------------------------------------------
        // Cohort member add / remove
        // ------------------------------------------------------------------

        private static CohortController BuildCohort(Mock<ICohortMemberLogic> members, Mock<ICohortLogic> cohorts = null)
        {
            CohortController c = new CohortController(
                (cohorts ?? new Mock<ICohortLogic>()).Object,
                members.Object,
                new Mock<IUserLogic>().Object,
                NullLogger<CohortController>.Instance);
            GiveTempData(c);
            return c;
        }

        [Fact]
        public async Task AddMember_reports_a_failure_as_a_failure()
        {
            // THE defect. This used to be 200 + "No new Item was added", and the browser said
            // "Member added."
            Mock<ICohortMemberLogic> members = new Mock<ICohortMemberLogic>();
            members.Setup(m => m.Create(It.IsAny<long>(), It.IsAny<Guid>()))
                   .ThrowsAsync(new InvalidOperationException("duplicate link"));

            IActionResult result = await BuildCohort(members).AddMember(7, Guid.NewGuid());

            StatusOf(result).Should().Be(StatusCodes.Status500InternalServerError,
                "a swallowed exception must not reach the browser as a success");
        }

        [Fact]
        public async Task AddMember_still_returns_json_on_success()
        {
            Mock<ICohortMemberLogic> members = new Mock<ICohortMemberLogic>();
            members.Setup(m => m.Create(It.IsAny<long>(), It.IsAny<Guid>()))
                   .ReturnsAsync(new CohortMember());

            IActionResult result = await BuildCohort(members).AddMember(7, Guid.NewGuid());

            result.Should().BeOfType<JsonResult>("the success shape is unchanged");
            StatusOf(result).Should().Be(StatusCodes.Status200OK);
        }

        [Fact]
        public async Task RemoveMember_separates_a_missing_link_from_a_server_error()
        {
            // Remove returning false without throwing means there was no such link. That is a 404,
            // not a 500, and neither is a 200.
            Mock<ICohortMemberLogic> notThere = new Mock<ICohortMemberLogic>();
            notThere.Setup(m => m.Remove(It.IsAny<CohortMember>())).ReturnsAsync(false);
            StatusOf(await BuildCohort(notThere).RemoveMember(7))
                .Should().Be(StatusCodes.Status404NotFound);

            Mock<ICohortMemberLogic> broke = new Mock<ICohortMemberLogic>();
            broke.Setup(m => m.Remove(It.IsAny<CohortMember>()))
                 .ThrowsAsync(new InvalidOperationException("database is on fire"));
            StatusOf(await BuildCohort(broke).RemoveMember(7))
                .Should().Be(StatusCodes.Status500InternalServerError);

            Mock<ICohortMemberLogic> ok = new Mock<ICohortMemberLogic>();
            ok.Setup(m => m.Remove(It.IsAny<CohortMember>())).ReturnsAsync(true);
            (await BuildCohort(ok).RemoveMember(7)).Should().BeOfType<JsonResult>();
        }

        // ------------------------------------------------------------------
        // Hardware module / cohort add and remove -- same shape, four more actions
        // ------------------------------------------------------------------

        private static HardwareController BuildHardware(
            Mock<IHardwareLinkedModuleLogic> modules = null,
            Mock<IHardwareLinkedCohortLogic> cohorts = null)
        {
            HardwareController c = new HardwareController(
                new Mock<IHardwareLogic>().Object,
                new Mock<IModuleLogic>().Object,
                NullLogger<HardwareController>.Instance,
                (modules ?? new Mock<IHardwareLinkedModuleLogic>()).Object,
                (cohorts ?? new Mock<IHardwareLinkedCohortLogic>()).Object,
                new Mock<ICohortLogic>().Object,
                new Mock<Febris.PrimaryLogicLayer.Logic.XApiLogic.IStatementLogic>().Object,
                new Mock<IRecordingLogic>().Object);
            GiveTempData(c);
            return c;
        }

        [Fact]
        public async Task AddModule_and_AddCohort_report_a_failure_as_a_failure()
        {
            Mock<IHardwareLinkedModuleLogic> modules = new Mock<IHardwareLinkedModuleLogic>();
            modules.Setup(m => m.Create(It.IsAny<long>(), It.IsAny<long>()))
                   .ThrowsAsync(new InvalidOperationException("nope"));
            StatusOf(await BuildHardware(modules: modules).AddModule(1, 2))
                .Should().Be(StatusCodes.Status500InternalServerError);

            Mock<IHardwareLinkedCohortLogic> cohorts = new Mock<IHardwareLinkedCohortLogic>();
            cohorts.Setup(m => m.Create(It.IsAny<long>(), It.IsAny<long>()))
                   .ThrowsAsync(new InvalidOperationException("nope"));
            StatusOf(await BuildHardware(cohorts: cohorts).AddCohort(1, 2))
                .Should().Be(StatusCodes.Status500InternalServerError);
        }

        [Fact]
        public async Task RemoveModule_and_RemoveCohort_report_a_missing_link_as_a_404()
        {
            Mock<IHardwareLinkedModuleLogic> modules = new Mock<IHardwareLinkedModuleLogic>();
            modules.Setup(m => m.Remove(It.IsAny<LocalHardwareLinkedModule>())).ReturnsAsync(false);
            StatusOf(await BuildHardware(modules: modules).RemoveModule(3))
                .Should().Be(StatusCodes.Status404NotFound);

            Mock<IHardwareLinkedCohortLogic> cohorts = new Mock<IHardwareLinkedCohortLogic>();
            cohorts.Setup(m => m.Remove(It.IsAny<HardwareLinkedCohort>())).ReturnsAsync(false);
            StatusOf(await BuildHardware(cohorts: cohorts).RemoveCohort(3))
                .Should().Be(StatusCodes.Status404NotFound);
        }

        // ------------------------------------------------------------------
        // The two archive toggles, which discarded the answer they computed
        // ------------------------------------------------------------------

        [Fact]
        public async Task Cohort_ArchiveToggle_forbids_rather_than_reporting_success()
        {
            // CohortLogic.ArchiveToggle sets output = true after a successful update and rethrows on
            // any exception, so false can ONLY mean its authorization filter refused. That is a 403.
            // Answering 404 would tell a learner the cohort does not exist when it does.
            Mock<ICohortLogic> refuses = new Mock<ICohortLogic>();
            refuses.Setup(c => c.ArchiveToggle(It.IsAny<long>())).ReturnsAsync(false);
            IActionResult refused = await BuildCohort(new Mock<ICohortMemberLogic>(), refuses)
                .ArchiveToggle(9);
            refused.Should().BeOfType<ForbidResult>();

            Mock<ICohortLogic> allows = new Mock<ICohortLogic>();
            allows.Setup(c => c.ArchiveToggle(It.IsAny<long>())).ReturnsAsync(true);
            IActionResult allowed = await BuildCohort(new Mock<ICohortMemberLogic>(), allows)
                .ArchiveToggle(9);
            StatusOf(allowed).Should().Be(StatusCodes.Status200OK);
        }

        [Fact]
        public async Task Cohort_ArchiveToggle_rejects_an_invalid_id_instead_of_returning_ok()
        {
            // This branch used to `return Ok()` after setting a TempData StatusMessage that an AJAX
            // caller never sees, so the browser announced a success for a request that did nothing.
            IActionResult result = await BuildCohort(new Mock<ICohortMemberLogic>()).ArchiveToggle(0);
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task MessageBoard_ToggleArchive_reports_its_outcome()
        {
            Mock<IMessageBoardLogic> works = new Mock<IMessageBoardLogic>();
            works.Setup(m => m.ToggleArchive(It.IsAny<long>())).ReturnsAsync(true);
            MessageBoardController ok = new MessageBoardController(
                works.Object, NullLogger<MessageBoardController>.Instance);
            GiveTempData(ok);
            StatusOf(await ok.ToggleArchive(4)).Should().Be(StatusCodes.Status200OK);

            Mock<IMessageBoardLogic> refuses = new Mock<IMessageBoardLogic>();
            refuses.Setup(m => m.ToggleArchive(It.IsAny<long>())).ReturnsAsync(false);
            MessageBoardController no = new MessageBoardController(
                refuses.Object, NullLogger<MessageBoardController>.Instance);
            GiveTempData(no);
            StatusOf(await no.ToggleArchive(4)).Should().Be(StatusCodes.Status404NotFound);

            MessageBoardController bad = new MessageBoardController(
                works.Object, NullLogger<MessageBoardController>.Instance);
            GiveTempData(bad);
            (await bad.ToggleArchive(0)).Should().BeOfType<BadRequestObjectResult>();
        }

        // ------------------------------------------------------------------
        // The defect one layer down, found only because the toggle started
        // reading the boolean it had been discarding
        // ------------------------------------------------------------------

        [Fact]
        public async Task MessageBoardLogic_ToggleArchive_reports_success_when_it_succeeds()
        {
            // `bool output = false;` was declared, never assigned, and returned. The archive DID
            // happen and the method reported failure anyway -- write side with no read side, the
            // dominant defect family in this audit. Nobody noticed because the only caller threw the
            // answer away, and making that caller honour it (ROADMAP 20) would have produced a 404
            // on every successful click.
            MessageBoard stored = new MessageBoard { Id = 4, Archive = false };

            Mock<IMessageBoardQueries> queries = new Mock<IMessageBoardQueries>();
            queries.Setup(q => q.Get(It.IsAny<long?>())).ReturnsAsync(stored);
            queries.Setup(q => q.Update(It.IsAny<MessageBoard>()))
                   .ReturnsAsync((MessageBoard m) => m);

            Mock<IHttpContextAccessor> http = new Mock<IHttpContextAccessor>();
            http.SetupGet(h => h.HttpContext).Returns(new DefaultHttpContext());

            MessageBoardLogic logic = new MessageBoardLogic(http.Object, queries.Object);

            bool result = await logic.ToggleArchive(4);

            result.Should().BeTrue("the archive was applied, so the method must say so");
            stored.Archive.Should().BeTrue("the flag is toggled before the update");
            queries.Verify(q => q.Update(It.IsAny<MessageBoard>()), Times.Once);
        }
    }
}
