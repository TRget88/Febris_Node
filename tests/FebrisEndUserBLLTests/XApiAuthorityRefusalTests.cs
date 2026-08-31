// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System.Threading.Tasks;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.UserNode.DataAccessLayer.Queries.XAPIQueries;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using XM = Febris.ModelLibrary.Models.XApiModels;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// T2: the submitter does not get to choose who vouched for a statement.
    ///
    /// <para>
    /// In xAPI the ACTOR is who performed the activity and the AUTHORITY is who asserts the
    /// statement is true. <c>SetupAuthority</c> used to accept any authority object carrying an
    /// <c>id</c> or <c>uuid</c> and hand the whole token to <c>SetupActor</c>, which resolves an
    /// existing Actor by id, uuid, mbox OR mbox_sha1sum. A caller holding any device token could
    /// therefore attach an authority naming a real instructor or administrator provisioned on the
    /// node, and that false attribution rendered and exported as part of the record.
    /// </para>
    ///
    /// <para>
    /// Storing NOTHING is strictly better than storing a lie. An absent authority honestly says the
    /// node does not record who vouched. A client-chosen one is indistinguishable from a real
    /// endorsement afterwards, which is exactly the property a learning record store exists to have.
    /// </para>
    ///
    /// <para>
    /// This is deliberately HALF the answer. The correct end state is the LRS STAMPING an authority
    /// from the submitting credentials, which needs the authenticated device identity in this layer.
    /// That is the same plumbing as binding the actor to the caller, which is a product decision
    /// about shared classroom devices. See docs/BUGS.md.
    /// </para>
    /// </summary>
    public class XApiAuthorityRefusalTests
    {
        private static StatementFactor BuildFactor(Mock<IActorQueries> actors = null)
        {
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            return new StatementFactor(
                accessor.Object,
                new Mock<IStatementQueries>().Object,
                (actors ?? new Mock<IActorQueries>()).Object,
                new Mock<IMemberQueries>().Object,
                new Mock<IObjectQueries>().Object,
                new Mock<IVerbQueries>().Object,
                new Mock<IVersionQueries>().Object,
                new Mock<IExtensionsQueries>().Object);
        }

        private static JObject StatementWith(string authorityJson)
        {
            string authority = authorityJson == null ? string.Empty : @", ""authority"": " + authorityJson;
            return JObject.Parse(@"{
                ""actor"": { ""objectType"": ""Agent"", ""name"": ""Learner"", ""mbox"": ""mailto:learner@example.com"" },
                ""verb"": { ""id"": ""http://adlnet.gov/expapi/verbs/completed"" },
                ""object"": { ""objectType"": ""Activity"", ""id"": ""http://example.com/activities/t2"" }"
                + authority + "}");
        }

        [Fact]
        public async Task AClientSuppliedAuthorityIsDiscarded()
        {
            // The impersonation route. A provisioned administrator named as the authority made the
            // statement look endorsed by them.
            (XM.Statement Statement, bool ParsedCorrectly) result = await BuildFactor().FactorStatement(
                StatementWith(@"{ ""objectType"": ""Agent"", ""name"": ""Principal"", ""mbox"": ""mailto:principal@school.edu"", ""uuid"": ""3f2504e0-4f89-11d3-9a0c-0305e82c3301"" }"));

            result.Statement.Should().NotBeNull("a statement with a rejected authority must still ingest");
            result.Statement.Authority.Should().BeNull("the submitter does not get to say who vouched for it");
        }

        [Fact]
        public async Task TheIdZeroFallthroughIsAlsoRefused()
        {
            // The specific bypass shape found in review: an integer id of 0 is non-null to the
            // token reader, but the id branch tests `!= 0` and falls THROUGH to the mbox lookup, so
            // "id": 0 plus an mbox resolved a real actor. Pinned separately because it is the case
            // a naive "require an id" guard would still let through.
            (XM.Statement Statement, bool ParsedCorrectly) result = await BuildFactor().FactorStatement(
                StatementWith(@"{ ""id"": 0, ""mbox"": ""mailto:principal@school.edu"" }"));

            result.Statement.Authority.Should().BeNull();
        }

        [Fact]
        public async Task TheAuthorityIsNeverResolvedAgainstTheActorTable()
        {
            // Stronger than asserting the result is null: the AUTHORITY's identity must never be
            // looked up at all. If the factor still resolved an Actor and merely dropped it later, a
            // refactor that reinstated the assignment would silently restore the hole.
            //
            // Asserted against the AUTHORITY's address specifically. The statement's own actor is
            // client-supplied by design and legitimately resolves through the same method, so a bare
            // "GetByMbox was never called" would fail for the wrong reason. A first version of this
            // test did exactly that.
            Mock<IActorQueries> actors = ActorsThatResolve();

            await BuildFactor(actors).FactorStatement(
                StatementWith(@"{ ""uuid"": ""3f2504e0-4f89-11d3-9a0c-0305e82c3301"", ""mbox"": ""mailto:principal@school.edu"" }"));

            actors.Verify(a => a.GetByMbox(It.Is<System.Uri>(u => u.ToString().Contains("principal@school.edu"))), Times.Never,
                "the authority's identity must never be resolved");
            actors.Verify(a => a.Get(It.Is<System.Guid>(g => g == System.Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"))), Times.Never,
                "nor by its uuid");
        }

        [Fact]
        public async Task AStatementWithNoAuthorityStillIngestsCleanly()
        {
            // The ordinary case, pinned so the refusal cannot be implemented as a rejection of the
            // whole statement. Producers that send no authority are the norm.
            (XM.Statement Statement, bool ParsedCorrectly) result = await BuildFactor(ActorsThatResolve()).FactorStatement(
                StatementWith(null));

            result.Statement.Should().NotBeNull();
            result.Statement.Authority.Should().BeNull();
            result.Statement.Actor.Should().NotBeNull("the ACTOR is client-supplied by design and must survive");
        }

        /// <summary>
        /// The statement's own actor must resolve, or the factor returns a statement with no actor
        /// and the assertions above pass or fail for reasons unrelated to authority.
        /// </summary>
        private static Mock<IActorQueries> ActorsThatResolve()
        {
            Mock<IActorQueries> actors = new Mock<IActorQueries>();
            actors.Setup(a => a.GetByMbox(It.IsAny<System.Uri>()))
                .ReturnsAsync(new XM.Actor { Id = 1, UUID = System.Guid.NewGuid(), Name = "Learner" });
            return actors;
        }
    }
}
