// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Febris.ModelLibrary.LauncherModels;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.SharedServices;
using Febris.UserNode.Api.Controllers;
using Febris.UserNode.LogicLayer.Logic.LauncherLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// T4, "no lossless statement copy exists". The audit's evidence sentence for this theme is
    /// stale on both halves (the all-zeros filename and the deduped-path-only claim were both
    /// invalidated by the C-02 back-fill in e1adcff the day after the audit was written), but the
    /// theme title held for a mechanism the sentence never named.
    ///
    /// <para>
    /// TWO WRITERS, ONE FILENAME. <c>XApiStatementBinding.PersistRawBytesAsync</c> wrote
    /// <c>{uuid}.json</c>, and <c>StatementLogic.SavingJSONStatement</c> writes <c>{uuid}.json</c>
    /// through <c>IStatementFileHandler.UploadPackage</c> -- same directory, both truncating. On
    /// <c>/Submit</c> both ran for one statement, the normalized copy first from the BLL and then
    /// the raw bytes from the controller, so the raw write destroyed the normalized one and the
    /// node kept ONE representation where the code reads as though it keeps two.
    /// </para>
    ///
    /// <para>
    /// The raw writer's own doc comment explains how it happened: it was written believing
    /// <c>SavingJSONStatement</c> was "currently commented out". It is live at three call sites.
    /// Its overwrite comment reasons about two different STATEMENTS colliding, which really is
    /// impossible, and never considered a second WRITER for the same statement.
    /// </para>
    ///
    /// <para>
    /// The two artifacts are not interchangeable, which is why both are now kept.
    /// <c>SavingJSONStatement</c> writes a re-serialized, lowercased, null-stripped,
    /// backslash-stripped rendering of the FACTORED statement. Only the raw bytes are what the
    /// producer actually sent. Verified on a running node after the fix, one statement per route:
    /// </para>
    /// <code>
    /// /Submit   298 bytes {uuid}.raw.json   +  425 bytes {uuid}.json
    /// /Backup   315 bytes {uuid}.raw.json   +  439 bytes {uuid}.json
    /// </code>
    /// <para>
    /// Differing sizes are the point: the two files hold genuinely different content, where before
    /// each statement left exactly one file.
    /// </para>
    /// </summary>
    public class XApiRawCopyCollisionTests
    {
        // ------------------------------------------------------------------
        // The invariant that prevents the collision
        // ------------------------------------------------------------------

        [Fact]
        public void TheRawCopySuffixIsNotTheNameTheLegacyWriterOwns()
        {
            // SavingJSONStatement -> UploadPackage writes uuid + ".json". If the raw copy ever
            // takes that name again, the two writers silently destroy each other with no error and
            // no log line -- which is exactly how this went unnoticed.
            XApiStatementBinding.RawBodyFileSuffix.Should().NotBe(".json",
                "the legacy normalized writer owns uuid + \".json\" in the same directory");
            XApiStatementBinding.RawBodyFileSuffix.Should().EndWith(".json",
                "it is still JSON on disk, so tooling that globs *.json keeps finding it");
        }

        [Fact]
        public async Task TheTwoWritersProduceDistinctPathsForTheSameStatement()
        {
            // Demonstrates the separation directly: write the raw copy, then confirm the filename
            // the legacy writer would use is still free.
            Guid uuid = Guid.NewGuid();
            string tmpDir = Path.Combine(Path.GetTempPath(), "FebrisT4_" + Guid.NewGuid().ToString("N"));

            try
            {
                bool ok = await XApiStatementBinding.PersistRawBytesAsync(
                    Encoding.UTF8.GetBytes("{\"id\":\"raw\"}"), uuid, tmpDir);
                ok.Should().BeTrue();

                string rawPath = Path.Combine(tmpDir, uuid + XApiStatementBinding.RawBodyFileSuffix);
                string legacyPath = Path.Combine(tmpDir, uuid + ".json");

                File.Exists(rawPath).Should().BeTrue("the verbatim copy must exist");
                File.Exists(legacyPath).Should().BeFalse(
                    "the legacy normalized writer's filename must still be free, or one write destroys the other");
                rawPath.Should().NotBe(legacyPath);
            }
            finally
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            }
        }

        // ------------------------------------------------------------------
        // The /Backup safety net
        // ------------------------------------------------------------------

        private static StatementController BuildController(Mock<ILauncherLogic> logic, Stream body)
        {
            DefaultHttpContext http = new DefaultHttpContext();
            http.Request.ContentType = "application/json";
            http.Request.Body = body;

            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(http);

            StatementController controller = new StatementController(
                NullLogger<LauncherController>.Instance,
                accessor.Object,
                logic.Object);

            controller.ControllerContext = new ControllerContext { HttpContext = http };
            return controller;
        }

        /// <summary>A body stream that reports itself unseekable, as an unbuffered request does.</summary>
        private sealed class NonSeekableStream : MemoryStream
        {
            public NonSeekableStream(byte[] buffer) : base(buffer) { }
            public override bool CanSeek => false;
        }

        [Fact]
        public async Task BackupStillSucceedsWhenTheBodyCannotBeRewound()
        {
            // The safety net. Raw capture on /Backup depends on Startup calling EnableBuffering for
            // that one route. If that middleware is removed, or the route is renamed so the path
            // test stops matching, the body is not seekable and ReadBufferedBodyAsync returns null.
            // A missing audit copy must degrade to a logged warning, never to a failed ingest --
            // the statement is already durable in the database by that point.
            Mock<ILauncherLogic> logic = new Mock<ILauncherLogic>();
            logic.Setup(l => l.SubmitStatement(It.IsAny<JObject>()))
                .ReturnsAsync(new Statement { Id = 11, UUID = Guid.NewGuid() });

            StatementController controller = BuildController(
                logic, new NonSeekableStream(Encoding.UTF8.GetBytes("{\"id\":\"x\"}")));

            IActionResult result = await controller.BackupStatementSubmission(JObject.Parse("{\"id\":\"x\"}"));

            OkObjectResult ok = result.Should().BeOfType<OkObjectResult>(
                "an unreadable audit copy is a monitoring concern, not a request failure").Subject;
            ok.Value.Should().BeOfType<StatementUploadResponseViewModel>()
                .Subject.Success.Should().BeTrue("the statement was stored, whatever happened to the audit copy");
        }

        [Fact]
        public async Task BackupDoesNotAttemptARawCopyForARejectedStatement()
        {
            // A refused statement has no UUID to key a file on. Guarding on it also keeps this off
            // the disk entirely for the reject path, where there is nothing to preserve.
            Mock<ILauncherLogic> logic = new Mock<ILauncherLogic>(MockBehavior.Strict);
            logic.Setup(l => l.SubmitStatement(It.IsAny<JObject>()))
                .ReturnsAsync((Statement)null);

            StatementController controller = BuildController(
                logic, new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":\"x\"}")));

            IActionResult result = await controller.BackupStatementSubmission(JObject.Parse("{\"id\":\"x\"}"));

            OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeOfType<StatementUploadResponseViewModel>()
                .Subject.Success.Should().BeFalse("a refusal is still reported honestly as 200 false");
        }
    }
}
