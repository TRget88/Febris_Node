// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using System.Threading.Tasks;
using Febris.ModelLibrary.ViewModels.XApi;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.UserNode.DataAccessLayer.Queries.XAPIQueries;
using Febris.SharedServices;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using XM = Febris.ModelLibrary.Models.XApiModels;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// SDKV-19/20 regression guards: ingest must be idempotent on the
    /// producer-assigned statement UUID. The PC StatementManager and mobile
    /// Server upload at-least-once (a lost /Submit response re-POSTs the same
    /// body to /Backup; a crash before the sent-file move re-uploads on the
    /// next poll) -- without node-side dedupe every retry blind-inserted a
    /// second LocalStatement row. Now: a statement whose id/uuid is already
    /// persisted returns the EXISTING record as success (no second insert),
    /// while statements carrying no usable identifier (the current SDK
    /// dialect emits id:0 / uuid:00000000-...) keep the historical
    /// insert-always behavior. Uses the real StatementQueries on the EF
    /// InMemory provider so the dedupe lookup + insert path is exercised
    /// end-to-end through the BLL.
    /// </summary>
    public class XApiIngestDedupeTests
    {
        private static readonly Uri VerbUri = new Uri("https://febr.is/Verb/Details/Completed");
        private static readonly Uri ObjectUri = new Uri("http://example.com/activity/sim-1");

        private static (StatementLogic Logic, XApiDbContext Context) BuildLogic(string dbName)
        {
            DbContextOptions<XApiDbContext> options = new DbContextOptionsBuilder<XApiDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            XApiDbContext context = new XApiDbContext(options);
            StatementQueries statementQueries = new StatementQueries(context);

            XM.Verb verb = new XM.Verb { Key = 1, UUID = Guid.NewGuid(), Id = VerbUri };
            XM.Object xObject = new XM.Object { Key = 2, UUID = Guid.NewGuid(), Id = ObjectUri };
            XM.Actor actor = new XM.Actor { Name = "learner" };

            var verbMock = new Mock<IVerbQueries>();
            verbMock.Setup(x => x.Get(It.IsAny<Uri>())).ReturnsAsync(verb);
            verbMock.Setup(x => x.Get(It.IsAny<long?>())).ReturnsAsync(verb);

            var objectMock = new Mock<IObjectQueries>();
            objectMock.Setup(x => x.Get(It.IsAny<Uri>())).ReturnsAsync(xObject);
            objectMock.Setup(x => x.Get(It.IsAny<long>())).ReturnsAsync(xObject);

            var actorMock = new Mock<IActorQueries>();
            actorMock.Setup(x => x.Get(It.IsAny<long>())).ReturnsAsync(actor);
            actorMock.Setup(x => x.Get(It.IsAny<long?>())).ReturnsAsync(actor);

            var versionMock = new Mock<IVersionQueries>();
            versionMock.Setup(x => x.GetLast()).ReturnsAsync(new XM.Version { Id = 3, UUID = Guid.NewGuid() });

            var fileHandlerMock = new Mock<IStatementFileHandler>();
            fileHandlerMock.Setup(x => x.UploadPackage(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

            StatementLogic logic = new StatementLogic(
                accessor.Object,
                statementQueries,
                verbMock.Object,
                versionMock.Object,
                objectMock.Object,
                new Mock<IXApiResultExtrasQueries>().Object,
                fileHandlerMock.Object,
                actorMock.Object,
                new Mock<IMemberQueries>().Object,
                new Mock<IExtensionsQueries>().Object);

            return (logic, context);
        }

        private static JObject StatementJson(Guid? statementId)
        {
            var statement = new JObject
            {
                ["actor"] = new JObject { ["id"] = 5 },
                ["verb"] = new JObject { ["id"] = VerbUri.ToString() },
                ["object"] = new JObject { ["id"] = ObjectUri.ToString() },
            };
            if (statementId.HasValue)
            {
                statement["id"] = statementId.Value.ToString();
            }
            return statement;
        }

        [Fact]
        public async Task Submit_SameStatementIdTwice_InsertsOnce_AndReturnsTheExistingRecord()
        {
            var (logic, context) = BuildLogic(nameof(Submit_SameStatementIdTwice_InsertsOnce_AndReturnsTheExistingRecord));
            Guid statementId = Guid.NewGuid();

            XM.Statement first = await logic.Submit(StatementJson(statementId));
            XM.Statement retry = await logic.Submit(StatementJson(statementId));

            context.LocalStatement.Count().Should().Be(1, "a host retry of the same statement id must not double-insert (SDKV-19/20)");
            context.LocalStatement.Single().UUID.Should().Be(statementId, "the producer-assigned statement UUID must be persisted so retries can match");
            retry.Should().NotBeNull("the retry must be treated as SUCCESS, not an error");
            retry.UUID.Should().Be(statementId);
            retry.Id.Should().Be(context.LocalStatement.Single().Id, "the retry must return the EXISTING record");
            retry.Verb.Should().NotBeNull("the deduped statement is compiled with its vocabulary like the read paths");
        }

        [Fact]
        public async Task Submit_DialectUuidSlot_AlsoDedupes()
        {
            // The dialect wire carries the statement GUID in "uuid" (top-level
            // "id" is the numeric DB id) -- both slots must dedupe.
            var (logic, context) = BuildLogic(nameof(Submit_DialectUuidSlot_AlsoDedupes));
            Guid statementId = Guid.NewGuid();
            JObject dialect = StatementJson(null);
            dialect["id"] = 0;
            dialect["uuid"] = statementId.ToString();

            await logic.Submit((JObject)dialect.DeepClone());
            await logic.Submit((JObject)dialect.DeepClone());

            context.LocalStatement.Count().Should().Be(1);
            context.LocalStatement.Single().UUID.Should().Be(statementId);
        }

        [Fact]
        public async Task Submit_NoStatementId_KeepsInsertAlwaysBehavior()
        {
            // No usable identifier -> no dedupe (the pre-fix behavior). The
            // current SDK dialect ships id:0 / uuid:00000000-... placeholders.
            var (logic, context) = BuildLogic(nameof(Submit_NoStatementId_KeepsInsertAlwaysBehavior));
            JObject placeholderIds = StatementJson(null);
            placeholderIds["id"] = 0;
            placeholderIds["uuid"] = Guid.Empty.ToString();

            await logic.Submit(StatementJson(null));
            await logic.Submit(StatementJson(null));
            await logic.Submit((JObject)placeholderIds.DeepClone());

            context.LocalStatement.Count().Should().Be(3, "statements with no usable id must keep the historical insert-always behavior");
        }

        [Fact]
        public async Task Submit_TypedStatementOverload_DedupesOnUuid()
        {
            var (logic, context) = BuildLogic(nameof(Submit_TypedStatementOverload_DedupesOnUuid));
            Guid statementId = Guid.NewGuid();
            XM.Statement Build() => new XM.Statement
            {
                UUID = statementId,
                Timestamp = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc),
                Actor = new XM.Actor { Name = "learner" },
                Verb = new XM.Verb { Id = VerbUri },
                Object = new XM.Object { Key = 2, UUID = Guid.NewGuid(), Id = ObjectUri },
            };

            await logic.Submit(Build());
            XM.Statement retry = await logic.Submit(Build());

            context.LocalStatement.Count().Should().Be(1, "the legacy typed-Statement route must dedupe too");
            context.LocalStatement.Single().UUID.Should().Be(statementId);
            retry.UUID.Should().Be(statementId);
        }

        [Fact]
        public async Task Submit_TypedDtoPath_DedupesOnDtoId()
        {
            // Exercises the UseTypedXApiFactor branch's own dedupe check.
            bool previous = StatementLogic.UseTypedXApiFactor;
            StatementLogic.UseTypedXApiFactor = true;
            try
            {
                var (logic, context) = BuildLogic(nameof(Submit_TypedDtoPath_DedupesOnDtoId));
                Guid statementId = Guid.NewGuid();
                XApiStatementSubmission Build() => new XApiStatementSubmission
                {
                    Dto = new XApiStatementDto
                    {
                        Id = statementId.ToString(),
                        Actor = new XApiActorDto { Id = 5 },
                        Verb = new XApiVerbDto { Id = VerbUri.ToString() },
                        Object = new XApiObjectDto { Id = ObjectUri.ToString() },
                    },
                };

                await logic.Submit(Build());
                XM.Statement retry = await logic.Submit(Build());

                context.LocalStatement.Count().Should().Be(1, "the typed-DTO branch must dedupe before factoring");
                context.LocalStatement.Single().UUID.Should().Be(statementId);
                retry.UUID.Should().Be(statementId);
            }
            finally
            {
                StatementLogic.UseTypedXApiFactor = previous;
            }
        }
    }
}
