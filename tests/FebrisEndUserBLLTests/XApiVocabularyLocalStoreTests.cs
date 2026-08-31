// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using XM = Febris.ModelLibrary.Models.XApiModels;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the node-local xAPI vocabulary store: the node OWNS
    /// Verb/Object/Version in its own XApiDbContext -- seeded at startup, resolved with zero
    /// HTTP -- instead of re-fetching them from central on every ingest/read. Uses the EF
    /// InMemory provider: relational/Npgsql annotations (uuid defaults) are metadata-only there,
    /// so entities set their own UUIDs where the database would normally do it.
    /// </summary>
    public class XApiVocabularyLocalStoreTests
    {
        private static XApiDbContext BuildContext(string dbName)
        {
            DbContextOptions<XApiDbContext> options = new DbContextOptionsBuilder<XApiDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new XApiDbContext(options);
        }

        [Fact]
        public void Seeder_SeedsStandardVerbsAndDefaultVersion_AndIsIdempotent()
        {
            using XApiDbContext context = BuildContext(nameof(Seeder_SeedsStandardVerbsAndDefaultVersion_AndIsIdempotent));

            XApiVocabularySeeder.Seed(context);

            context.Verb.Count().Should().Be(7, "the standard central seed is 7 VerbEnums verbs");
            context.Verb.AsEnumerable().Should().OnlyContain(v => v.Id != null && v.Display.ContainsKey("en"));
            context.Version.Single().VersionNumber.Should().Be("2.0");

            // Re-running at every startup must never duplicate or overwrite.
            XApiVocabularySeeder.Seed(context);

            context.Verb.Count().Should().Be(7);
            context.Version.Count().Should().Be(1);
        }

        [Fact]
        public async Task VerbQueries_ResolveSeededVerbLocally_ByUriAndBatch()
        {
            using XApiDbContext context = BuildContext(nameof(VerbQueries_ResolveSeededVerbLocally_ByUriAndBatch));
            XApiVocabularySeeder.Seed(context);
            VerbQueries queries = new VerbQueries(context);

            // The ingest path (StatementFactor.SetupVerb) resolves by IRI.
            Uri completedIri = context.Verb.AsEnumerable().Select(v => v.Id).Single(u => u.ToString().Contains("Completed"));
            XM.Verb byUri = await queries.Get(completedIri);
            byUri.Should().NotBeNull("a seeded verb must resolve locally with zero HTTP");
            byUri.Id.Should().Be(completedIri);

            // The dashboard read path (CompileStatementList) resolves by key batch.
            List<long> keys = context.Verb.Select(v => v.Key).Take(3).ToList();
            List<XM.Verb> batch = await queries.Get(keys);
            batch.Should().HaveCount(3);
            batch.Select(v => v.Key).Should().BeEquivalentTo(keys);
        }

        [Fact]
        public async Task VerbQueries_Get_ReturnsNullOnMiss_SoTheTransientVerbFallbackStillWorks()
        {
            using XApiDbContext context = BuildContext(nameof(VerbQueries_Get_ReturnsNullOnMiss_SoTheTransientVerbFallbackStillWorks));
            XApiVocabularySeeder.Seed(context);
            VerbQueries queries = new VerbQueries(context);

            // Content-authored verbs not (yet) in the local store must come back null so
            // StatementFactor.SetupVerb builds its transient in-memory verb from the payload IRI.
            XM.Verb miss = await queries.Get(new Uri("http://example.org/verbs/not-in-store"));
            miss.Should().BeNull();
        }

        [Fact]
        public async Task VersionQueries_GetLast_ReturnsTheLatestVersionLocally()
        {
            using XApiDbContext context = BuildContext(nameof(VersionQueries_GetLast_ReturnsTheLatestVersionLocally));
            VersionQueries queries = new VersionQueries(context);

            await queries.Create(new XM.Version() { VersionNumber = "2.0" });
            await queries.Create(new XM.Version() { VersionNumber = "2.1" });

            // StatementLogic stamps input.Version from GetLast() on ingest; it must be the
            // newest local row (explicitly ordered -- bare LastOrDefault is untranslatable).
            XM.Version last = await queries.GetLast();
            last.Should().NotBeNull();
            last.VersionNumber.Should().Be("2.1");
        }

        [Fact]
        public async Task StatementFactor_PersistsUnseenObjectLocally_SoReadsResolveIt()
        {
            // Review finding (vocab-severance review, behavior-parity MAJOR): after the local-EF
            // conversion, nothing populated the local Object store, so every read lost its
            // required xAPI Object. Persist-on-miss registers a content-emitted activity on
            // first ingest: it gets a real Key (for LocalStatement.ObjectId) and resolves on
            // every later read. Second sight of the same IRI reuses the row (no duplicate).
            using XApiDbContext context = BuildContext(nameof(StatementFactor_PersistsUnseenObjectLocally_SoReadsResolveIt));
            ObjectQueries objectQueries = new ObjectQueries(context);
            var accessor = new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new Microsoft.AspNetCore.Http.DefaultHttpContext());
            var factor = new PrimaryLogicLayer.Logic.XApiLogic.StatementFactor(
                accessor.Object,
                new Moq.Mock<IStatementQueries>().Object,
                new Moq.Mock<Febris.UserNode.DataAccessLayer.Queries.XAPIQueries.IActorQueries>().Object,
                new Moq.Mock<Febris.UserNode.DataAccessLayer.Queries.XAPIQueries.IMemberQueries>().Object,
                objectQueries,
                new Moq.Mock<IVerbQueries>().Object,
                new Moq.Mock<IVersionQueries>().Object,
                new Moq.Mock<IExtensionsQueries>().Object);

            var input = Newtonsoft.Json.Linq.JObject.Parse(
                "{ \"object\": { \"id\": \"http://example.org/activities/lockout-tagout\", \"objectType\": \"Activity\" } }");
            var result = await factor.FactorStatement(input);

            result.ParsedCorrectly.Should().BeTrue();
            result.Statement.Object.Should().NotBeNull();
            result.Statement.Object.Key.Should().NotBe(0, "the unseen activity must be REGISTERED locally, not left transient");
            context.Object.Count().Should().Be(1);

            // Same IRI again -> resolved from the store, not re-created.
            var second = await factor.FactorStatement(input);
            second.Statement.Object.Key.Should().Be(result.Statement.Object.Key);
            context.Object.Count().Should().Be(1);
        }

        [Fact]
        public async Task ObjectQueries_Get_IncludesDefinition_MatchingTheGraphCentralReturned()
        {
            using XApiDbContext context = BuildContext(nameof(ObjectQueries_Get_IncludesDefinition_MatchingTheGraphCentralReturned));
            XM.Object stored = new XM.Object()
            {
                Id = new Uri("http://example.org/activities/welding-101"),
                ObjectType = "Activity",
                Definition = new XM.Definition() { Name = new Dictionary<string, string> { ["en"] = "Welding 101" } }
            };
            context.Object.Add(stored);
            context.SaveChanges();
            ObjectQueries queries = new ObjectQueries(context);

            XM.Object byKey = await queries.Get(stored.Key);

            byKey.Should().NotBeNull();
            byKey.Definition.Should().NotBeNull("the remote path returned the full serialized graph; the local twin must Include it");
            byKey.Definition.Name.Values.Should().Contain("Welding 101");
        }
    }
}
