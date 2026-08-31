// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Linq;
using System.Text;
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
    /// Golden-file round-trip proof for the SDK wire-fix program: REAL C# SDK
    /// emissions must ingest green on the hardened node.
    ///
    /// <para><b>Fixture provenance (regenerate from here):</b> the JSONs in
    /// <c>Fixtures/SdkGolden/</c> are byte-for-byte captures of the
    /// <c>{uuid}.json</c> handoff files written by the SDK's WinPC pipeline
    /// (<c>Initializer.Initialize</c> -> statement updates ->
    /// <c>StatementHandler.EndSimulation</c>/<c>GetSendableUpdate</c> ->
    /// <c>WinPCHandler.WriteFile</c>) in the workshop repo at commit
    /// <c>c2369f4e</c> ("fix(sim-sdk): stamp the handoff ReferenceUUID into
    /// the wire statement id (SDKV-19/20 SDK side)") -- i.e. POST-Wave-1
    /// spec-cased emissions (SDKV-1..5/11..13) WITH the newly stamped wire
    /// <c>id</c>. To regenerate: build a console harness referencing
    /// <c>simulationintegration/FebrisCSharp</c>, redirect the library's file
    /// paths via <c>ExternalFileSystemMethods.ExternalFileSystemRectifier</c>,
    /// drive <c>Initialize(new[]{"-febrisData=" + authoredJson},
    /// ExpectedOperatingSystem.WindowsPC)</c> plus the updates noted per
    /// fixture below, and copy the written handoff file verbatim.</para>
    ///
    /// <list type="bullet">
    ///   <item><c>sdk_full_featured</c> -- actor w/ account, verb, object w/
    ///     definition + correctResponsesPattern, result w/ score + ISO
    ///     duration, context w/ instructor + group + contextActivities, one
    ///     attachment; terminal emission after
    ///     <c>EndSimulation(true, true, 95.5f, 90.5s)</c> (verb auto-calcs to
    ///     Pass). Statement id <c>1bbb2b18-...</c> == its handoff filename.</item>
    ///   <item><c>sdk_minimal</c> -- bare actor/verb/object; every other
    ///     region is the SDK's own defaults. Initial emission only.</item>
    ///   <item><c>sdk_group_actor</c> -- Group actor with a populated member
    ///     wrapper (the SDKV-14 dialect shape); re-emitted once via
    ///     <c>SimulationPassed(true)</c> + <c>GetSendableUpdate()</c>, proving
    ///     the stamped id survives the rewrite.</item>
    /// </list>
    ///
    /// <para>What the tests prove, per fixture: (1) the REAL /Submit binder
    /// (<see cref="XApiStatementBinding.ReadAsync"/>) binds the raw bytes --
    /// DtoBound, no ParseError, verbatim raw-bytes capture; (2) the EndUser
    /// <see cref="StatementFactor.FactorStatement(JObject)"/> over the raw
    /// JObject with InMemory-backed LOCAL queries (seeded vocabulary +
    /// actors, persist-on-miss objects) populates actor/verb/object/result/
    /// context -- nothing silently null -- with the attachment language map
    /// bound and the ISO duration parsed; (3) <see cref="StatementLogic"/>
    /// ingest of the SAME bytes twice dedupes on the SDK-stamped statement id
    /// (SDKV-19/20) -- no double insert.</para>
    /// </summary>
    public class GoldenWireRoundTripTests
    {
        // Stamped wire ids == the {uuid}.json handoff filenames the SDK wrote.
        private const string FullFeaturedId = "1bbb2b18-a9ed-4733-ac15-f0735599238c";
        private const string MinimalId = "5c22e090-a7b7-4700-8d3e-3c2ec8ed2d2f";
        private const string GroupActorId = "43a6eb6e-567c-49d2-9952-206770b316a1";

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static byte[] LoadFixtureBytes(string name)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SdkGolden", name + ".json");
            File.Exists(path).Should().BeTrue("fixture {0} must be copied to the test output (csproj None include)", path);
            return File.ReadAllBytes(path);
        }

        private static async Task<XApiStatementSubmission> BindAsync(byte[] bytes)
        {
            DefaultHttpContext httpContext = new DefaultHttpContext();
            httpContext.Request.Body = new MemoryStream(bytes);
            httpContext.Request.ContentType = "application/json";
            return await XApiStatementBinding.ReadAsync(httpContext.Request);
        }

        /// <summary>
        /// Real EndUser factor over InMemory-backed LOCAL stores (the
        /// XApiVocabularyLocalStoreTests pattern): the standard vocabulary is
        /// seeded (the fixtures' febr.is verb IRIs resolve locally), the
        /// fixtures' actors are seeded so the id-keyed actor resolution finds
        /// real rows, and objects register via persist-on-miss.
        /// </summary>
        private static (StatementFactor Factor, XApiDbContext Context) BuildFactorEnvironment(string dbName)
        {
            DbContextOptions<XApiDbContext> options = new DbContextOptionsBuilder<XApiDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            XApiDbContext context = new XApiDbContext(options);
            XApiVocabularySeeder.Seed(context);

            // The actors the SDK harness authored (resolution is by wire id).
            context.Actor.AddRange(
                new XM.Actor { Id = 3, UUID = Guid.Parse("672896d1-d9f7-48d8-ac22-d4efa4e94902"), ObjectType = "Agent", Name = "Kiera Vale" },
                new XM.Actor { Id = 5, UUID = Guid.Parse("7a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d"), ObjectType = "Agent", Name = "Min Imal" },
                new XM.Actor { Id = 9, UUID = Guid.Parse("5d6e7f80-9a0b-4c1d-8e2f-3a4b5c6d7e8f"), ObjectType = "Group", Name = "Surgical Team Blue" },
                new XM.Actor { Id = 11, UUID = Guid.Parse("0a742f7d-64f0-4b02-97ab-52ac37642a71"), ObjectType = "Agent", Name = "Instructor Reyes" },
                new XM.Actor { Id = 21, UUID = Guid.Parse("33d1f0aa-96c4-4b6e-8b1f-5f2f7f3f9c01"), ObjectType = "Agent", Name = "Cohort Member One" },
                new XM.Actor { Id = 22, UUID = Guid.Parse("44e2a1bb-a7d5-4c7f-9c2a-6a3a8a4aad12"), ObjectType = "Agent", Name = "Cohort Member Two" });
            context.SaveChanges();

            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            StatementFactor factor = new StatementFactor(
                accessor.Object,
                new Mock<IStatementQueries>().Object,
                new ActorQueries(context),
                new Mock<IMemberQueries>().Object,
                new ObjectQueries(context),
                new VerbQueries(context),
                new Mock<IVersionQueries>().Object,
                new Mock<IExtensionsQueries>().Object);
            return (factor, context);
        }

        /// <summary>
        /// StatementLogic wired the Wave-2 dedupe-test way: REAL
        /// StatementQueries on the EF InMemory provider (the dedupe lookup +
        /// insert path is exercised end-to-end), vocabulary/actor lookups
        /// mocked to resolve.
        /// </summary>
        private static (StatementLogic Logic, XApiDbContext Context) BuildLogic(string dbName)
        {
            DbContextOptions<XApiDbContext> options = new DbContextOptionsBuilder<XApiDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            XApiDbContext context = new XApiDbContext(options);
            StatementQueries statementQueries = new StatementQueries(context);

            XM.Verb verb = new XM.Verb { Key = 1, UUID = Guid.NewGuid(), Id = new Uri("https://febr.is/Verb/Details/Pass") };
            XM.Object xObject = new XM.Object { Key = 2, UUID = Guid.NewGuid(), Id = new Uri("https://febr.is/Module/460a5ddf-02c3-4fb8-9d7c-0ef0da64925d") };
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

        // ------------------------------------------------------------------
        // 1) The REAL /Submit binder over the raw fixture bytes
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("sdk_full_featured")]
        [InlineData("sdk_minimal")]
        [InlineData("sdk_group_actor")]
        public async Task Binding_ReadAsync_BindsRealSdkEmission(string fixture)
        {
            byte[] bytes = LoadFixtureBytes(fixture);

            XApiStatementSubmission submission = await BindAsync(bytes);

            submission.DtoBound.Should().BeTrue(
                "the golden SDK emission {0} must bind on the hardened node; ParseError: {1}",
                fixture, submission.ParseError);
            submission.ParseError.Should().BeNull();
            submission.Dto.Should().NotBeNull();
            submission.RawBody.Should().Equal(bytes, "raw-bytes audit capture must stay verbatim");
        }

        [Fact]
        public async Task Binding_FullFeaturedFixture_BindsEveryRegion()
        {
            XApiStatementSubmission submission = await BindAsync(LoadFixtureBytes("sdk_full_featured"));
            XApiStatementDto dto = submission.Dto;

            dto.Id.Should().Be(FullFeaturedId, "the SDK-stamped wire id must survive the bind (SDKV-19/20)");
            dto.Actor.Name.Should().Be("Kiera Vale");
            dto.Actor.Account.Should().NotBeNull();
            dto.Actor.Account.HomePage.Should().Be("https://tenant.febr.is", "the spec-cased homePage IFI must bind (SDKV-12)");
            dto.Verb.Id.Should().Be("https://febr.is/Verb/Details/Pass", "EndSimulation auto-calced the terminal verb");
            dto.Object.Definition.CorrectResponsesPattern.Should().Equal("step1", "step2");
            dto.Result.Success.Should().BeTrue();
            dto.Result.Completion.Should().BeTrue();
            dto.Result.Duration.Should().Be("PT1M30.5S", "the SDK emits ISO 8601 durations (SDKV-5)");
            dto.Result.Score.Raw.Should().Be(95.5m);
            dto.Context.Group.Should().HaveCount(2, "the dialect group ARRAY must bind (SDKV-15)");
            dto.Context.Group[0].Name.Should().Be("Cohort Member One");
            dto.Context.Instructor.Name.Should().Be("Instructor Reyes");
            dto.Context.ContextActivities.Should().NotBeNull();
            dto.Context.ContextActivities.Parent.Should().ContainSingle()
                .Which.Id.Should().Be("https://febr.is/Curricula/9f2c1e34-1111-4222-8333-444455556666");
            dto.Attachments.Should().ContainSingle();
            dto.Attachments[0].Display.Should().ContainKey("en-us")
                .WhoseValue.Should().Be("Video Review", "the attachment display Language Map must bind (SDKV-2)");
            dto.Attachments[0].ContentType.Should().Be("video/mpeg");
        }

        [Fact]
        public async Task Binding_GroupActorFixture_BindsThePopulatedMemberWrapper()
        {
            XApiStatementSubmission submission = await BindAsync(LoadFixtureBytes("sdk_group_actor"));
            XApiStatementDto dto = submission.Dto;

            dto.Id.Should().Be(GroupActorId);
            dto.Actor.ObjectType.Should().Be("Group");
            dto.Actor.Name.Should().Be("Surgical Team Blue");
            dto.Actor.Member.Should().HaveCount(2, "the dialect member wrapper OBJECT must bind into the list (SDKV-14)");
            dto.Actor.Member[0].Name.Should().Be("Team Lead");
            dto.Actor.Member[0].Mbox.Should().Be("mailto:lead@tenant.febr.is");
            dto.Actor.Member[1].MboxSha1Sum.Should().Be("4a544776e93b80615f77a462b0126c7976865fc6");
        }

        [Fact]
        public async Task Binding_MinimalFixture_BindsWithSdkDefaults()
        {
            XApiStatementSubmission submission = await BindAsync(LoadFixtureBytes("sdk_minimal"));
            XApiStatementDto dto = submission.Dto;

            dto.Id.Should().Be(MinimalId);
            dto.Actor.Mbox.Should().Be("mailto:min@tenant.febr.is");
            dto.Actor.Member.Should().NotBeNull().And.BeEmpty("the SDK always emits the member wrapper, empty here");
            dto.Verb.Id.Should().Be("https://febr.is/Verb/Details/Initialized");
            dto.Result.Duration.Should().Be("PT0S", "the SDK's init default is a valid ISO duration (SDKV-5)");
            dto.Context.Group.Should().NotBeNull().And.BeEmpty();
        }

        // ------------------------------------------------------------------
        // 2) The REAL EndUser JObject factor over the raw fixture JSON
        // ------------------------------------------------------------------

        [Fact]
        public async Task Factor_FullFeaturedFixture_PopulatesEveryRegion()
        {
            var (factor, context) = BuildFactorEnvironment(nameof(Factor_FullFeaturedFixture_PopulatesEveryRegion));
            JObject raw = JObject.Parse(Encoding.UTF8.GetString(LoadFixtureBytes("sdk_full_featured")));

            var result = await factor.FactorStatement(raw);

            result.ParsedCorrectly.Should().BeTrue();
            result.Statement.Should().NotBeNull();

            result.Statement.Actor.Should().NotBeNull("the actor must not be silently null");
            result.Statement.Actor.Name.Should().Be("Kiera Vale", "the wire actor id must resolve the seeded local row");

            result.Statement.Verb.Should().NotBeNull();
            result.Statement.Verb.Id.Should().Be(new Uri("https://febr.is/Verb/Details/Pass"));
            result.Statement.Verb.Key.Should().NotBe(0, "the febr.is verb must resolve from the seeded LOCAL vocabulary");

            result.Statement.Object.Should().NotBeNull();
            result.Statement.Object.Key.Should().NotBe(0, "the unseen activity must be REGISTERED locally (persist-on-miss)");
            context.Object.Count().Should().Be(1);

            result.Statement.Result.Should().NotBeNull("the result must not be silently null");
            result.Statement.Result.Success.Should().BeTrue();
            result.Statement.Result.Completion.Should().BeTrue();
            result.Statement.Result.Duration.Should().Be(TimeSpan.FromSeconds(90.5), "the ISO duration must parse (SDKV-5 node side)");
            result.Statement.Result.Score.Should().NotBeNull();
            result.Statement.Result.Score.Raw.Should().Be(95.5f);

            result.Statement.Context.Should().NotBeNull("the context must not be silently null");
            result.Statement.Context.ContextActivities.Should().NotBeNull("contextActivities must parse (SDKV-18)");
            result.Statement.Context.ContextActivities.Parent.Should().Be("https://febr.is/Curricula/9f2c1e34-1111-4222-8333-444455556666");
            result.Statement.Context.Group.Should().HaveCount(2);
            result.Statement.Context.Group[0].Name.Should().Be("Cohort Member One");
            result.Statement.Context.Instructor.Should().NotBeNull();
            result.Statement.Context.Instructor.Name.Should().Be("Instructor Reyes");

            result.Statement.Attachments.Should().ContainSingle("the attachment must not be silently dropped (SDKV-17)");
            result.Statement.Attachments[0].ContentType.Should().Be("video/mpeg");
            result.Statement.Attachments[0].Display.Values.Should().Contain("Video Review",
                "the emitted display Language Map must bind through to the domain attachment");
            result.Statement.Attachments[0].FileURL.Should().Be(new Uri("https://tenant.febr.is/media/8c9d0e1f.mp4"));
        }

        [Fact]
        public async Task Factor_MinimalFixture_PopulatesRequiredRegions_WithSdkDefaults()
        {
            var (factor, context) = BuildFactorEnvironment(nameof(Factor_MinimalFixture_PopulatesRequiredRegions_WithSdkDefaults));
            JObject raw = JObject.Parse(Encoding.UTF8.GetString(LoadFixtureBytes("sdk_minimal")));

            var result = await factor.FactorStatement(raw);

            result.ParsedCorrectly.Should().BeTrue();
            result.Statement.Actor.Should().NotBeNull();
            result.Statement.Actor.Name.Should().Be("Min Imal");
            result.Statement.Verb.Should().NotBeNull();
            result.Statement.Verb.Key.Should().NotBe(0, "Initialized resolves from the seeded local vocabulary");
            result.Statement.Object.Should().NotBeNull();
            result.Statement.Object.Key.Should().NotBe(0, "persist-on-miss must register the module activity");
            result.Statement.Result.Should().NotBeNull("the SDK's default result region must factor, not vanish");
            result.Statement.Result.Success.Should().BeFalse();
            result.Statement.Result.Duration.Should().Be(TimeSpan.Zero, "PT0S must parse");
            result.Statement.Context.Should().NotBeNull();
            result.Statement.Attachments.Should().NotBeNull().And.BeEmpty("the SDK emits an empty attachments array");
        }

        [Fact]
        public async Task Factor_GroupActorFixture_ResolvesTheGroupActor()
        {
            var (factor, context) = BuildFactorEnvironment(nameof(Factor_GroupActorFixture_ResolvesTheGroupActor));
            JObject raw = JObject.Parse(Encoding.UTF8.GetString(LoadFixtureBytes("sdk_group_actor")));

            var result = await factor.FactorStatement(raw);

            result.ParsedCorrectly.Should().BeTrue();
            result.Statement.Actor.Should().NotBeNull();
            result.Statement.Actor.Name.Should().Be("Surgical Team Blue",
                "the Group actor's wire id must resolve the seeded local row");
            result.Statement.Actor.ObjectType.Should().Be("Group");
            // NOTE: the wire member array itself is consumed on the typed DTO
            // path (see Binding_GroupActorFixture...); the JObject factor
            // resolves the stored actor row by id -- member factoring from the
            // wire is deliberately disabled there (historical region).
            result.Statement.Verb.Should().NotBeNull();
            result.Statement.Verb.Id.Should().Be(new Uri("https://febr.is/Verb/Details/Initialized"));
            result.Statement.Object.Should().NotBeNull();
            result.Statement.Object.Key.Should().NotBe(0);
            result.Statement.Result.Should().NotBeNull();
            result.Statement.Result.Success.Should().BeTrue("SimulationPassed(true) was driven before the re-emission");
        }

        // ------------------------------------------------------------------
        // 3) Ingest idempotency on the SDK-stamped id (SDKV-19/20 round trip)
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("sdk_full_featured", FullFeaturedId)]
        [InlineData("sdk_minimal", MinimalId)]
        [InlineData("sdk_group_actor", GroupActorId)]
        public async Task Submit_SameFixtureBytesTwice_DoesNotDoubleInsert(string fixture, string expectedId)
        {
            var (logic, context) = BuildLogic(nameof(Submit_SameFixtureBytesTwice_DoesNotDoubleInsert) + "_" + fixture);
            byte[] bytes = LoadFixtureBytes(fixture);

            // The REAL retry shape: the host re-POSTs the SAME bytes; each
            // submit re-binds them exactly like the /Submit route does.
            XM.Statement first = await logic.Submit(await BindAsync(bytes));
            XM.Statement retry = await logic.Submit(await BindAsync(bytes));

            first.Should().NotBeNull("the first ingest of the golden emission must succeed");
            context.LocalStatement.Count().Should().Be(1,
                "a second submit of the same SDK-stamped bytes must not double-insert (SDKV-19/20)");
            context.LocalStatement.Single().UUID.Should().Be(Guid.Parse(expectedId),
                "the statement must persist under the SDK-stamped wire id");
            retry.Should().NotBeNull("the retry must be treated as SUCCESS");
            retry.Id.Should().Be(context.LocalStatement.Single().Id, "the retry must return the EXISTING record");
        }
    }
}
