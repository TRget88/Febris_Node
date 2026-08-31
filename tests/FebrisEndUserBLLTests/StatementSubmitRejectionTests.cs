// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.LauncherModels;
using Febris.UserNode.LogicLayer.Logic.LauncherLogic;
using Febris.UserNode.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// The tracked "typed route 400s where the legacy route succeeds" defect, and it was NOT what
    /// the note said it was.
    ///
    /// <para>
    /// It had been recorded as "a binding/validation refusal rather than a crash". Reproduced against
    /// a running node, it was the opposite: a crash. The BLL correctly REJECTED a statement whose
    /// actor is not provisioned and returned null, and then
    /// <c>StatementController.SubmitStatement</c> dereferenced that null.
    /// </para>
    ///
    /// <para>
    /// The guard read <c>if (statement?.UUID != Guid.Empty &amp;&amp; ...)</c>. When
    /// <c>statement</c> is null, <c>statement?.UUID</c> lifts to <c>(Guid?)null</c>, and
    /// <c>null != Guid.Empty</c> is TRUE -- so the branch was entered precisely when there was no
    /// statement, and the next line touched <c>statement.UUID</c>. It is the SAME null-lifting
    /// mistake as the <c>(long?)0 != null</c> one that the C-02 comment two lines below it
    /// describes; that sweep fixed the Success line on all three routes and missed this guard.
    /// </para>
    ///
    /// <para>
    /// Why it mattered: both shipping clients use <c>/Submit</c> as their PRIMARY path, and an
    /// unprovisioned actor is an ordinary, expected rejection. Every one of those became a 400
    /// instead of an honest <c>{"success":false}</c>, and the clients' fallback to <c>/Backup</c>
    /// masked it.
    /// </para>
    /// </summary>
    public class StatementSubmitRejectionTests
    {
        private const string ValidStatementJson = @"{
            ""id"": ""aaaaaaaa-bbbb-cccc-dddd-eeeeeeee9001"",
            ""actor"": { ""objectType"": ""Agent"", ""name"": ""Probe"", ""mbox"": ""mailto:probe@example.com"" },
            ""verb"": { ""id"": ""http://adlnet.gov/expapi/verbs/completed"" },
            ""object"": { ""objectType"": ""Activity"", ""id"": ""http://example.com/activities/probe"" }
        }";

        private static StatementController BuildController(Mock<ILauncherLogic> logic, string body)
        {
            DefaultHttpContext http = new DefaultHttpContext();
            http.Request.ContentType = "application/json";
            http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(http);

            StatementController controller = new StatementController(
                NullLogger<LauncherController>.Instance,
                accessor.Object,
                logic.Object);

            controller.ControllerContext = new ControllerContext { HttpContext = http };
            return controller;
        }

        [Fact]
        public async Task ARejectedStatementReports200False_RatherThanCrashing()
        {
            // THE regression. A null return is the BLL's way of saying "refused" -- most commonly
            // because the actor is not provisioned on this node -- and it must surface as an honest
            // failure, not as a 400 the client reads as a protocol error.
            Mock<ILauncherLogic> logic = new Mock<ILauncherLogic>();
            logic.Setup(l => l.SubmitStatement(It.IsAny<Febris.ModelLibrary.ViewModels.XApi.XApiStatementSubmission>()))
                .ReturnsAsync((Statement)null);

            StatementController controller = BuildController(logic, ValidStatementJson);

            IActionResult result = await controller.SubmitStatement();

            OkObjectResult ok = result.Should().BeOfType<OkObjectResult>(
                "a refused statement is a 200 with success=false on the other two routes, and the typed route must agree")
                .Subject;

            StatementUploadResponseViewModel body = ok.Value.Should().BeOfType<StatementUploadResponseViewModel>().Subject;
            body.Success.Should().BeFalse("the statement was refused, and saying otherwise would make a client delete its copy");
        }

        [Fact]
        public async Task AnAcceptedStatementReports200True()
        {
            // The other side, so the fix cannot be "always report failure".
            //
            // Housekeeping (T4): an accepted statement with a real UUID makes the controller's
            // raw-bytes audit write fire for real, against the process-wide
            // StaticDetails.JSONStatementFileSystemPath. PersistRawBytesAsync accepts a directory
            // override but the controller does not pass one, so it is not substitutable at this
            // boundary and this test cannot redirect it. It therefore deletes the one file it
            // creates, rather than leaving a fresh artifact in a real directory on every run.
            Guid acceptedUuid = Guid.NewGuid();
            Mock<ILauncherLogic> logic = new Mock<ILauncherLogic>();
            logic.Setup(l => l.SubmitStatement(It.IsAny<Febris.ModelLibrary.ViewModels.XApi.XApiStatementSubmission>()))
                .ReturnsAsync(new Statement { Id = 7, UUID = acceptedUuid });

            StatementController controller = BuildController(logic, ValidStatementJson);

            try
            {
                IActionResult result = await controller.SubmitStatement();

                OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
                ok.Value.Should().BeOfType<StatementUploadResponseViewModel>()
                    .Subject.Success.Should().BeTrue();
            }
            finally
            {
                string dir = Febris.SharedServices.StaticDetails.JSONStatementFileSystemPath;
                if (!string.IsNullOrEmpty(dir))
                {
                    string written = Path.Combine(
                        dir,
                        acceptedUuid + Febris.SharedServices.XApiStatementBinding.RawBodyFileSuffix);
                    if (File.Exists(written)) File.Delete(written);
                }
            }
        }

        [Fact]
        public async Task MalformedJsonIsStillARealBadRequest()
        {
            // The behaviour the tracked note ASSUMED was happening. It must keep working: a genuine
            // parse failure is a 400, and it is distinguishable from a refusal because it never
            // reaches the BLL at all.
            Mock<ILauncherLogic> logic = new Mock<ILauncherLogic>(MockBehavior.Strict);

            StatementController controller = BuildController(logic, "{ this is not json ");

            IActionResult result = await controller.SubmitStatement();

            result.Should().BeOfType<BadRequestObjectResult>("unparseable input is a client error, unlike a refusal");
        }
    }
}
