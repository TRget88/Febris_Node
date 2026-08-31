// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.SharedServices;
using Febris.UserNode.Portal.Controllers.xAPI;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// The statement JSON download, RESTORED. It was implemented and wired in the 2021 Portal as
    /// <c>xAPIController.StatementDownloader</c> and did not survive the port, leaving four
    /// independent breaks: no controller action (and no <c>XAPI</c> controller at all, so the whole
    /// <c>/XAPI/*</c> route family was dead), a <c>LoadStatementAction</c> helper called from three
    /// portals and defined in none, no view rendering the button, and a write-only
    /// <c>IStatementFileHandler</c>.
    ///
    /// <para>
    /// THE SECURITY PROPERTY THESE TESTS EXIST FOR: a download must disclose no more than viewing
    /// does. It therefore follows the READ scope rather than the void gate, which is why the action
    /// carries no role attribute and why the logic goes through <c>IStatementLogic.Get</c> instead
    /// of re-implementing the per-role filter. A caller who may not see a statement must never reach
    /// the file system.
    /// </para>
    /// </summary>
    public class StatementDownloadTests
    {
        private static readonly Guid TargetUuid = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        private static readonly byte[] StoredBytes = Encoding.UTF8.GetBytes("{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"}");

        private sealed class Harness
        {
            public Mock<IStatementLogic> Statements = new Mock<IStatementLogic>();
            public Mock<IStatementFileHandler> Files = new Mock<IStatementFileHandler>();
            public StatementDownloadLogic Logic;
        }

        private static Harness Build(Statement visible, byte[] stored = null)
        {
            Harness h = new Harness();

            // EXACT-MATCH lookup. A stub answering It.IsAny regardless of argument cannot catch the
            // logic asking for the wrong statement, which is how an entitlement defect shipped green
            // earlier in this audit.
            h.Statements.Setup(x => x.Get(It.IsAny<Guid?>()))
                .ReturnsAsync((Guid? asked) => asked == TargetUuid ? visible : null);

            h.Files.Setup(x => x.DownloadPackage(It.IsAny<string>()))
                .ReturnsAsync((string uuid) => uuid == TargetUuid.ToString() ? stored : null);

            h.Logic = new StatementDownloadLogic(h.Statements.Object, h.Files.Object);
            return h;
        }

        private static Statement Visible() => new Statement { Id = 5, UUID = TargetUuid };

        // ------------------------------------------------------------------
        // Access: a download discloses exactly what viewing discloses
        // ------------------------------------------------------------------

        [Fact]
        public async Task AVisibleStatementIsReturned()
        {
            Harness h = Build(Visible(), StoredBytes);

            byte[] content = await h.Logic.Get(TargetUuid);

            content.Should().Equal(StoredBytes);
            h.Files.Verify(x => x.DownloadPackage(TargetUuid.ToString()), Times.Once);
        }

        [Fact]
        public async Task AStatementTheCallerMayNotSeeIsRefusedWithoutTouchingTheFileSystem()
        {
            // THE gate. StatementLogic.Get returns null when its per-role filter denies the caller.
            // Reaching the file handler anyway would hand a learner another learner's record even
            // though every other read path correctly hides it.
            Harness h = Build(visible: null, stored: StoredBytes);

            byte[] content = await h.Logic.Get(TargetUuid);

            content.Should().BeNull();
            h.Files.Verify(x => x.DownloadPackage(It.IsAny<string>()), Times.Never,
                "a denied caller must never reach the file system at all");
        }

        [Fact]
        public async Task AnUnknownStatementIsRefused()
        {
            Harness h = Build(Visible(), StoredBytes);

            byte[] content = await h.Logic.Get(Guid.Parse("99999999-9999-9999-9999-999999999999"));

            content.Should().BeNull();
            h.Files.Verify(x => x.DownloadPackage(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AThrowingReadIsARefusalNotACrash()
        {
            // StatementLogic.Get rethrows for a statement that does not exist, so an unknown uuid
            // arrives as an exception rather than a null.
            Harness h = Build(Visible(), StoredBytes);
            h.Statements.Setup(x => x.Get(It.IsAny<Guid?>())).ThrowsAsync(new InvalidOperationException("boom"));

            byte[] content = await h.Logic.Get(TargetUuid);

            content.Should().BeNull();
            h.Files.Verify(x => x.DownloadPackage(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AnEmptyUuidIsRefusedBeforeAnyLookup()
        {
            Harness h = Build(Visible(), StoredBytes);

            (await h.Logic.Get(Guid.Empty)).Should().BeNull();

            h.Statements.Verify(x => x.Get(It.IsAny<Guid?>()), Times.Never);
            h.Files.Verify(x => x.DownloadPackage(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AVisibleStatementWithNoStoredFileIsNotAnError()
        {
            // Statements ingested before the JSON copy was being written have a row and no file.
            Harness h = Build(Visible(), stored: null);

            (await h.Logic.Get(TargetUuid)).Should().BeNull();
        }

        // ------------------------------------------------------------------
        // The Portal action
        // ------------------------------------------------------------------

        private static MethodInfo DownloadAction()
        {
            MethodInfo action = typeof(StatementController).GetMethod(
                "StatementDownload", BindingFlags.Public | BindingFlags.Instance);

            action.Should().NotBeNull("the Portal must expose a StatementDownload action, or the button is dead again");
            return action;
        }

        private static StatementController Controller(Mock<IStatementDownloadLogic> download)
        {
            return new StatementController(
                new Mock<IStatementLogic>().Object,
                new Mock<IStatementVoidingLogic>().Object,
                download.Object,
                NullLogger<StatementController>.Instance);
        }

        [Fact]
        public void TheDownloadActionIsAGetAndCarriesNoAntiforgeryToken()
        {
            // Unlike voiding. This reads and changes nothing, so audit C-07 does not apply, and a
            // download the browser cannot simply navigate to is not a download.
            DownloadAction().GetCustomAttributes<HttpGetAttribute>().Should().NotBeEmpty();
            DownloadAction().GetCustomAttributes<HttpPostAttribute>().Should().BeEmpty();
            DownloadAction().GetCustomAttributes<ValidateAntiForgeryTokenAttribute>().Should().BeEmpty();
        }

        [Fact]
        public void TheDownloadActionIsNotRestrictedToAdmins()
        {
            // DELIBERATE, and the opposite of the void action. Anyone who may view a statement may
            // export it, and the per-role filter in the BLL is what decides who that is. Adding a
            // role attribute here would silently remove the download from every educator and
            // learner, which is what the 2021 arrangement explicitly did not do.
            DownloadAction().GetCustomAttributes<AuthorizeAttribute>().Should().BeEmpty();
        }

        [Fact]
        public void TheDownloadActionTakesTheStatementUuid()
        {
            ParameterInfo[] parameters = DownloadAction().GetParameters();

            parameters.Should().HaveCount(1);
            parameters[0].ParameterType.Should().Be(typeof(Guid));
            parameters[0].Name.Should().Be("statementId");
        }

        [Fact]
        public async Task TheActionServesTheBytesAsJsonWithAPerStatementFilename()
        {
            // The 2021 version returned text/plain and had its file extension explicitly commented
            // out, so downloads arrived mistyped and unnamed.
            Mock<IStatementDownloadLogic> download = new Mock<IStatementDownloadLogic>();
            download.Setup(d => d.Get(It.IsAny<Guid>())).ReturnsAsync(StoredBytes);

            IActionResult result = await Controller(download).StatementDownload(TargetUuid);

            FileContentResult file = result.Should().BeOfType<FileContentResult>().Subject;
            file.FileContents.Should().Equal(StoredBytes);
            file.ContentType.Should().Be("application/json");
            file.FileDownloadName.Should().Be(TargetUuid + ".json");
            download.Verify(d => d.Get(TargetUuid), Times.Once);
        }

        [Fact]
        public async Task ARefusedDownloadIs404NotAnError()
        {
            // Refused and missing are the SAME response on purpose, so this cannot be used to probe
            // which statements exist.
            Mock<IStatementDownloadLogic> download = new Mock<IStatementDownloadLogic>();
            download.Setup(d => d.Get(It.IsAny<Guid>())).ReturnsAsync((byte[])null);

            IActionResult result = await Controller(download).StatementDownload(TargetUuid);

            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task AThrowingDownloadLogicProducesA404NotA500()
        {
            Mock<IStatementDownloadLogic> download = new Mock<IStatementDownloadLogic>();
            download.Setup(d => d.Get(It.IsAny<Guid>())).ThrowsAsync(new InvalidOperationException("boom"));

            IActionResult result = await Controller(download).StatementDownload(TargetUuid);

            result.Should().BeOfType<NotFoundResult>();
        }
    }
}
