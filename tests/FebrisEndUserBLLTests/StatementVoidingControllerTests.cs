// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.UserNode.Portal.Controllers.xAPI;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// T5, second half: the Portal action that makes voiding reachable.
    ///
    /// <para>
    /// The engine landed first with NOTHING invoking it. <c>StatementVoidingLogic</c> was fully
    /// tested and completely unreachable outside its own tests, because the only markup that
    /// referenced voiding was an orphaned partial rendered by zero views, pointing at a
    /// <c>/XAPI/VoidStatement</c> route with no controller behind it. These tests pin the wiring
    /// that closes that gap.
    /// </para>
    ///
    /// <para>
    /// <b>Why reflection on the attributes.</b> The gate that matters is in the BLL and is tested in
    /// <c>StatementVoidingTests</c>. What can silently regress HERE is the transport contract: an
    /// action that loses <c>[HttpPost]</c> becomes GET-reachable again (audit C-07), one that loses
    /// <c>[ValidateAntiForgeryToken]</c> stops being covered by the architecture guard, and one
    /// whose parameter type drifts from Guid to long binds every request to <c>Guid.Empty</c> and
    /// refuses silently. None of those change behaviour any unit test of the BLL would notice.
    /// </para>
    /// </summary>
    public class StatementVoidingControllerTests
    {
        private static readonly Guid TargetUuid = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

        private static MethodInfo VoidAction()
        {
            MethodInfo action = typeof(StatementController).GetMethod(
                "VoidStatement", BindingFlags.Public | BindingFlags.Instance);

            action.Should().NotBeNull("the Portal must expose a VoidStatement action, or voiding is unreachable");
            return action;
        }

        private static StatementController Build(Mock<IStatementVoidingLogic> voiding)
        {
            return new StatementController(
                new Mock<IStatementLogic>().Object,
                voiding.Object,
                new Mock<IStatementDownloadLogic>().Object,
                NullLogger<StatementController>.Instance);
        }

        private static bool SuccessOf(IActionResult result)
        {
            JsonResult json = result.Should().BeOfType<JsonResult>().Subject;
            object value = json.Value;
            PropertyInfo success = value.GetType().GetProperty("success");
            success.Should().NotBeNull("the browser reads data.success");
            return (bool)success.GetValue(value);
        }

        // ------------------------------------------------------------------
        // Transport contract
        // ------------------------------------------------------------------

        [Fact]
        public void TheActionIsPostOnly()
        {
            // Audit C-07. The 2021 route was GET /XAPI/VoidStatement?statementId=, which fires from
            // any page a logged-in admin visits. Voiding is irreversible, so this matters more here
            // than on any other action on the node.
            VoidAction().GetCustomAttributes<HttpPostAttribute>().Should().NotBeEmpty();
            VoidAction().GetCustomAttributes<HttpGetAttribute>().Should().BeEmpty();
        }

        [Fact]
        public void TheActionValidatesTheAntiforgeryToken()
        {
            VoidAction().GetCustomAttributes<ValidateAntiForgeryTokenAttribute>().Should().NotBeEmpty();
        }

        [Fact]
        public void TheActionIsRestrictedToAdminAndUp()
        {
            // Matches IsLocalAdmin() in the BLL: Admin and ITAdmin. If these two ever disagree, the
            // outer door and the real gate are enforcing different policies.
            AuthorizeAttribute authorize = VoidAction().GetCustomAttributes<AuthorizeAttribute>().SingleOrDefault();

            authorize.Should().NotBeNull("an unattributed action inherits only the controller's EndUserAll, which is every learner");
            authorize.Roles.Should().Be("Admin,ITAdmin");
        }

        [Fact]
        public void TheActionTakesTheStatementUuidNotThePrimaryKey()
        {
            // Every other action on this controller takes the long key, so this is easy to "fix"
            // wrongly. A long here would bind to Guid.Empty in the BLL and refuse every void, which
            // is the same shape of identifier mismatch that shipped a broken video entitlement gate
            // earlier in this audit.
            ParameterInfo[] parameters = VoidAction().GetParameters();

            parameters.Should().HaveCount(1);
            parameters[0].ParameterType.Should().Be(typeof(Guid));
            parameters[0].Name.Should().Be("statementId", "the posted field name binds by parameter name");
        }

        // ------------------------------------------------------------------
        // Behaviour
        // ------------------------------------------------------------------

        [Fact]
        public async Task AVoidIsDelegatedToTheBllWithTheSubmittedUuid()
        {
            Mock<IStatementVoidingLogic> voiding = new Mock<IStatementVoidingLogic>();
            voiding.Setup(v => v.Void(It.IsAny<Guid>())).ReturnsAsync(true);

            IActionResult result = await Build(voiding).VoidStatement(TargetUuid);

            // EXACT argument. A stub verified with It.IsAny cannot catch the controller passing the
            // wrong identifier through.
            voiding.Verify(v => v.Void(TargetUuid), Times.Once);
            SuccessOf(result).Should().BeTrue();
        }

        [Fact]
        public async Task ARefusedVoidReportsFailureRatherThanThrowing()
        {
            // The BLL returns false for all three refusals -- not an admin, no such statement,
            // already voided -- and the browser is told only that it did not happen. Which of the
            // three it was is not disclosed.
            Mock<IStatementVoidingLogic> voiding = new Mock<IStatementVoidingLogic>();
            voiding.Setup(v => v.Void(It.IsAny<Guid>())).ReturnsAsync(false);

            IActionResult result = await Build(voiding).VoidStatement(TargetUuid);

            SuccessOf(result).Should().BeFalse();
        }

        [Fact]
        public async Task AThrowingBllProducesACleanFailureNotA500()
        {
            // BulkUserNullPayloadTests found the opposite shape on this node: a controller catch
            // block doing "throw;" turned a bad payload into an unhandled 500.
            Mock<IStatementVoidingLogic> voiding = new Mock<IStatementVoidingLogic>();
            voiding.Setup(v => v.Void(It.IsAny<Guid>())).ThrowsAsync(new InvalidOperationException("boom"));

            IActionResult result = await Build(voiding).VoidStatement(TargetUuid);

            SuccessOf(result).Should().BeFalse();
        }

        // ------------------------------------------------------------------
        // The list the void button is reached through
        // ------------------------------------------------------------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AnUnfilteredListReturnsEveryStatement(string searchString)
        {
            // FOUND AT RUNTIME on 2026-08-15. These branches were inverted, so the default view of
            // the statement index called SearchGet(null) and rendered EMPTY. Voiding is reached by
            // opening a row's details modal, so an always-empty list made the button unreachable no
            // matter how correctly it was wired.
            Mock<IStatementLogic> statements = new Mock<IStatementLogic>();
            statements.Setup(s => s.Get()).ReturnsAsync(new List<Statement>());
            StatementController controller = new StatementController(
                statements.Object, new Mock<IStatementVoidingLogic>().Object,
                new Mock<IStatementDownloadLogic>().Object, NullLogger<StatementController>.Instance);
            controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());

            await controller.IndexPartial(null, searchString, null);

            statements.Verify(s => s.Get(), Times.Once, "an empty search box must list everything");
            statements.Verify(s => s.SearchGet(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ASearchTermIsActuallyUsedToSearch()
        {
            // The other half of the same inversion: a search term used to call Get(), which ignores
            // the term and returns every statement, so searching appeared to "work" while silently
            // filtering nothing.
            Mock<IStatementLogic> statements = new Mock<IStatementLogic>();
            statements.Setup(s => s.SearchGet(It.IsAny<string>())).ReturnsAsync(new List<Statement>());
            StatementController controller = new StatementController(
                statements.Object, new Mock<IStatementVoidingLogic>().Object,
                new Mock<IStatementDownloadLogic>().Object, NullLogger<StatementController>.Instance);
            controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());

            await controller.IndexPartial(null, "wire", null);

            statements.Verify(s => s.SearchGet("wire"), Times.Once);
            statements.Verify(s => s.Get(), Times.Never);
        }

        [Fact]
        public async Task AnEmptyUuidIsPassedThroughAndRefused()
        {
            // Model binding yields Guid.Empty for a missing or malformed field. The BLL refuses it;
            // the controller must not treat it as a special case and must not crash on it.
            Mock<IStatementVoidingLogic> voiding = new Mock<IStatementVoidingLogic>();
            voiding.Setup(v => v.Void(Guid.Empty)).ReturnsAsync(false);

            IActionResult result = await Build(voiding).VoidStatement(Guid.Empty);

            SuccessOf(result).Should().BeFalse();
        }
    }
}
