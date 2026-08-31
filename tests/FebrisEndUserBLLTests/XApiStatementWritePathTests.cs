// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Migrations.XApiDb;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.UserNode.DataAccessLayer.Queries.XAPIQueries;
using Febris.SharedServices;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using XM = Febris.ModelLibrary.Models.XApiModels;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Audit C-01 / C-02 / C-03 and the T3 timestamp discard -- the statement WRITE path.
    ///
    /// <para>
    /// These four defects had to be fixed together, and this file exists to keep them that way.
    /// The store-generated columns had no database default, so on a node provisioned from
    /// migrations the INSERT could not complete; that failure was MASKING the other three. Repair
    /// the write alone and the node starts storing wrong data instead of no data: an
    /// acknowledgement protocol that is inverted in both directions, a producer timestamp that is
    /// parsed and dropped, and actor lookups that can never miss.
    /// </para>
    ///
    /// <para>
    /// Statement assertions run the real <c>StatementQueries</c> over the EF InMemory provider, so
    /// the BLL insert path is exercised end-to-end. InMemory treats relational defaults as metadata
    /// only, so the C-01 schema half is asserted against the migration's own operations instead --
    /// that is the artifact that was wrong, and it is checkable without a database.
    /// </para>
    /// </summary>
    public class XApiStatementWritePathTests
    {
        private static readonly Uri VerbUri = new Uri("https://febr.is/Verb/Details/Completed");
        private static readonly Uri ObjectUri = new Uri("http://example.com/activity/sim-1");

        private static (StatementLogic Logic, XApiDbContext Context) BuildLogic(string dbName)
        {
            DbContextOptions<XApiDbContext> options = new DbContextOptionsBuilder<XApiDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            XApiDbContext context = new XApiDbContext(options);

            XM.Verb verb = new XM.Verb { Key = 1, UUID = Guid.NewGuid(), Id = VerbUri };
            XM.Object xObject = new XM.Object { Key = 2, UUID = Guid.NewGuid(), Id = ObjectUri };

            var verbMock = new Mock<IVerbQueries>();
            verbMock.Setup(x => x.Get(It.IsAny<Uri>())).ReturnsAsync(verb);
            verbMock.Setup(x => x.Get(It.IsAny<long?>())).ReturnsAsync(verb);

            var objectMock = new Mock<IObjectQueries>();
            objectMock.Setup(x => x.Get(It.IsAny<Uri>())).ReturnsAsync(xObject);
            objectMock.Setup(x => x.Get(It.IsAny<long>())).ReturnsAsync(xObject);

            // A provisioned learner. The unprovisioned case C-03 is about is asserted directly
            // against the real ActorQueries below, where the miss actually happens.
            XM.Actor actor = new XM.Actor { Name = "learner" };
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
                new StatementQueries(context),
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

        private static JObject StatementJson(DateTime? timestamp = null)
        {
            var statement = new JObject
            {
                ["actor"] = new JObject { ["id"] = 5 },
                ["verb"] = new JObject { ["id"] = VerbUri.ToString() },
                ["object"] = new JObject { ["id"] = ObjectUri.ToString() },
            };
            if (timestamp.HasValue)
            {
                statement["timestamp"] = timestamp.Value;
            }
            return statement;
        }

        [Fact]
        public void Migration_GivesLocalStatementStoredAndTimestamp_TheirMissingDatabaseDefaults()
        {
            // C-01. XApiDbContext has always declared both columns
            // HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd(), and the model snapshot
            // records that -- but the 2022 Initial migration created them NOT NULL with no default,
            // so EF omitted them from the INSERT and the database supplied nothing. `Stored` is
            // never assigned anywhere in the BLL, so on a node provisioned from migrations alone
            // EVERY statement insert violated NOT NULL. Statement ingest is the node's reason to
            // exist, so this is asserted structurally rather than left to a live-database run.
            List<AlterColumnOperation> operations = new StatementTimestampDefaults()
                .UpOperations
                .OfType<AlterColumnOperation>()
                .Where(o => o.Table == "LocalStatement")
                .ToList();

            operations.Should().HaveCount(2);
            operations.Should().OnlyContain(o => o.DefaultValueSql == "CURRENT_TIMESTAMP" && !o.IsNullable);
            operations.Select(o => o.Name).Should().BeEquivalentTo(new[] { "Stored", "Timestamp" });
        }

        [Fact]
        public async Task Submit_JObject_BackFillsTheStoreAssignedKey_SoAcknowledgementCanBeTruthful()
        {
            // C-02. The persisted key was never copied onto the returned statement, so
            // StatementController's `statement.Id != default` was false for a statement that DID
            // persist. The client then never drained its outbox and retried forever.
            var (logic, context) = BuildLogic(nameof(Submit_JObject_BackFillsTheStoreAssignedKey_SoAcknowledgementCanBeTruthful));

            XM.Statement result = await logic.Submit(StatementJson());

            context.LocalStatement.Should().HaveCount(1);
            long storedId = context.LocalStatement.Single().Id;
            storedId.Should().NotBe(default(long));
            result.Should().NotBeNull();
            result.Id.Should().Be(storedId, "the route decides Success from this value");

            // The condition all three ingest routes now share.
            (result != null && result.Id != default).Should().BeTrue();
        }

        [Fact]
        public async Task Submit_Statement_ReturnsTheSubmittedStatement_NotABlankOne()
        {
            // C-02, second half. This overload declared `Statement output = new Statement()` and
            // never assigned it, so it returned an EMPTY statement -- which is also what the JSON
            // backup serialized. Id 0 made the route report failure unconditionally.
            var (logic, context) = BuildLogic(nameof(Submit_Statement_ReturnsTheSubmittedStatement_NotABlankOne));
            DateTime produced = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

            XM.Statement result = await logic.Submit(new XM.Statement
            {
                UUID = Guid.NewGuid(),
                Timestamp = produced,
                Actor = new XM.Actor { Name = "learner" },
                Verb = new XM.Verb { Key = 1, UUID = Guid.NewGuid(), Id = VerbUri },
                Object = new XM.Object { Key = 2, UUID = Guid.NewGuid(), Id = ObjectUri }
            });

            result.Should().NotBeNull();
            result.Id.Should().Be(context.LocalStatement.Single().Id);
            result.Id.Should().NotBe(default(long));
            result.Actor.Should().NotBeNull("a blank Statement was being returned in place of the submitted one");
            result.Timestamp.Should().Be(produced);
        }

        [Fact]
        public async Task Submit_JObject_PersistsTheProducerTimestamp_InsteadOfDiscardingIt()
        {
            // T3. The JObject path built its LocalStatement with NO Timestamp at all, so the
            // producer timestamp the factor had already parsed was dropped and EF treated the CLR
            // default as "let the store generate it". Under xAPI 1.0.3 `timestamp` is the
            // producer's time and `stored` is the LRS's; conflating them loses when the learning
            // actually happened.
            var (logic, context) = BuildLogic(nameof(Submit_JObject_PersistsTheProducerTimestamp_InsteadOfDiscardingIt));
            DateTime produced = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            await logic.Submit(StatementJson(produced));

            context.LocalStatement.Single().Timestamp.Should().BeCloseTo(produced, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Submit_JObject_WithNoProducerTimestamp_StampsOneRatherThanLeavingTheDefault()
        {
            // The fallback half of the same fix: a statement that genuinely carried no timestamp
            // must not be written with DateTime.MinValue, which is what the CLR default is.
            var (logic, context) = BuildLogic(nameof(Submit_JObject_WithNoProducerTimestamp_StampsOneRatherThanLeavingTheDefault));

            await logic.Submit(StatementJson());

            context.LocalStatement.Single().Timestamp.Should().NotBe(default(DateTime));
        }

        [Fact]
        public async Task Submit_ForAnUnprovisionedActor_IsREJECTED_NotWrittenAndNotThrown()
        {
            // C-03, second half. Found by running a real statement through the PC uploader on
            // 2026-08-05 -- no unit test could have caught it, because every one of them (including
            // the others in this file) mocks IActorQueries to RETURN an actor.
            //
            // Making the DAL return null on a miss was necessary but not sufficient: nothing
            // rejected the null, so it was written onto LocalStatement and EF failed with
            // "violates foreign key constraint FK_LocalStatement_Actor_ActorId". That converted the
            // original SILENT defect (a blank Actor cascading into an IFI-less ghost row) into an
            // unhandled exception rather than into the rejection the audit asked for.
            //
            // This is the audit's stated C-03 verification: ingest a statement for an unprovisioned
            // actor and assert rejection.
            DbContextOptions<XApiDbContext> options = new DbContextOptionsBuilder<XApiDbContext>()
                .UseInMemoryDatabase(nameof(Submit_ForAnUnprovisionedActor_IsREJECTED_NotWrittenAndNotThrown))
                .Options;
            using XApiDbContext context = new XApiDbContext(options);

            XM.Verb verb = new XM.Verb { Key = 1, UUID = Guid.NewGuid(), Id = VerbUri };
            XM.Object xObject = new XM.Object { Key = 2, UUID = Guid.NewGuid(), Id = ObjectUri };
            var verbMock = new Mock<IVerbQueries>();
            verbMock.Setup(x => x.Get(It.IsAny<Uri>())).ReturnsAsync(verb);
            var objectMock = new Mock<IObjectQueries>();
            objectMock.Setup(x => x.Get(It.IsAny<Uri>())).ReturnsAsync(xObject);

            // The unprovisioned case: every lookup misses, which is what the real DAL now returns.
            var actorMock = new Mock<IActorQueries>();
            actorMock.Setup(x => x.Get(It.IsAny<long>())).ReturnsAsync((XM.Actor)null);
            actorMock.Setup(x => x.GetByMbox(It.IsAny<Uri>())).ReturnsAsync((XM.Actor)null);
            actorMock.Setup(x => x.GetByHashedMbox(It.IsAny<string>())).ReturnsAsync((XM.Actor)null);

            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            var versionMock = new Mock<IVersionQueries>();
            versionMock.Setup(x => x.GetLast()).ReturnsAsync(new XM.Version { Id = 3, UUID = Guid.NewGuid() });

            StatementLogic logic = new StatementLogic(
                accessor.Object,
                new StatementQueries(context),
                verbMock.Object,
                versionMock.Object,
                objectMock.Object,
                new Mock<IXApiResultExtrasQueries>().Object,
                new Mock<IStatementFileHandler>().Object,
                actorMock.Object,
                new Mock<IMemberQueries>().Object,
                new Mock<IExtensionsQueries>().Object);

            var unprovisioned = new JObject
            {
                ["actor"] = new JObject { ["mbox"] = "mailto:nobody@example.org" },
                ["verb"] = new JObject { ["id"] = VerbUri.ToString() },
                ["object"] = new JObject { ["id"] = ObjectUri.ToString() },
            };

            XM.Statement result = await logic.Submit(unprovisioned);

            result.Should().BeNull("an unprovisioned actor must be REJECTED");
            context.LocalStatement.Should().BeEmpty("and nothing may be written against a person who does not exist");

            // The controllers all decide Success from `statement != null && statement.Id != default`,
            // so a null return is reported honestly as failure rather than as silent success.
            (result != null && result.Id != default).Should().BeFalse();
        }

        [Fact]
        public async Task ActorLookup_OnAMiss_ReturnsNull_SoTheRejectionGuardIsReachable()
        {
            // C-03. Every single-entity lookup used FirstAsync() against a field pre-initialised to
            // `new Actor()`, with the throw swallowed -- so a miss returned a BLANK NON-NULL Actor
            // and StatementFactor's `if (!actorFound) return null;` was unreachable dead code.
            // Records were stored against IFI-less ghost rows no read path can reach.
            DbContextOptions<XApiDbContext> options = new DbContextOptionsBuilder<XApiDbContext>()
                .UseInMemoryDatabase(nameof(ActorLookup_OnAMiss_ReturnsNull_SoTheRejectionGuardIsReachable))
                .Options;
            using XApiDbContext context = new XApiDbContext(options);
            ActorQueries actors = new ActorQueries(context);

            (await actors.Get(4242L)).Should().BeNull("an unprovisioned actor must not resolve to a blank Actor");
            (await actors.Get(Guid.NewGuid())).Should().BeNull();
            (await actors.GetByMbox(new Uri("mailto:nobody@example.com"))).Should().BeNull();
            (await actors.GetByHashedMbox("not-a-real-hash")).Should().BeNull();
        }
    }
}
