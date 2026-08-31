// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Reflection;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels.XApi;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.UserNode.DataAccessLayer.Queries.XAPIQueries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using XM = Febris.ModelLibrary.Models.XApiModels;
using LS = Febris.ModelLibrary.Models.XApiModels.ModifiedForSharing;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// T3: <c>result.success</c> and <c>result.completion</c> are OPTIONAL in xAPI 1.0.3, and the
    /// node treated their absence as a malformed statement.
    ///
    /// <para>
    /// The two factor paths failed DIFFERENTLY, so both are pinned here.
    /// </para>
    ///
    /// <para>
    /// The JObject path (the route both shipping clients actually use) cast with <c>(bool)</c>.
    /// On an absent token that throws, the outer catch swallowed it and returned null for the
    /// WHOLE Result. Measured against a running node, two statements identical apart from those
    /// two optional fields:
    /// </para>
    /// <code>
    /// A  with success+completion   ResultId=1     Success=t  Response=answer-text  Duration=00:05:30  Raw=87
    /// B  score only                ResultId=NULL     --           --                   --              --
    /// </code>
    /// <para>
    /// Both were answered <c>200 {"success":true}</c>. The node reported success while destroying
    /// the score, the response, the duration and the extensions.
    /// </para>
    ///
    /// <para>
    /// The typed DTO path used <c>?? false</c> instead. It did not lose the Result, but it recorded
    /// an assertion the producer never made -- and the DTO's own <c>bool?</c> properties prove the
    /// wire format had parsed the absence faithfully before the factor flattened it.
    /// </para>
    ///
    /// <para>
    /// The governing rule for readers: absent is NOT an assertion of true, and it is not an
    /// assertion of false either. A credential requiring success is still not awarded on silence.
    /// </para>
    /// </summary>
    public class XApiOptionalResultFlagsTests
    {
        private static StatementFactor BuildFactor()
        {
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            return new StatementFactor(
                accessor.Object,
                new Mock<IStatementQueries>().Object,
                new Mock<IActorQueries>().Object,
                new Mock<IMemberQueries>().Object,
                new Mock<IObjectQueries>().Object,
                new Mock<IVerbQueries>().Object,
                new Mock<IVersionQueries>().Object,
                new Mock<IExtensionsQueries>().Object);
        }

        private static async Task<XM.Result> FactorResultFromJson(string resultJson)
        {
            JObject input = JObject.Parse(@"{
                ""actor"": { ""objectType"": ""Agent"", ""name"": ""T3"", ""mbox"": ""mailto:t3@example.com"" },
                ""verb"": { ""id"": ""http://adlnet.gov/expapi/verbs/completed"" },
                ""object"": { ""objectType"": ""Activity"", ""id"": ""http://example.com/activities/t3"" },
                ""result"": " + resultJson + @"
            }");

            (XM.Statement Statement, bool ParsedCorrectly) factored = await BuildFactor().FactorStatement(input);
            return factored.Statement?.Result;
        }

        // ------------------------------------------------------------------
        // JObject path -- the regression that destroyed data
        // ------------------------------------------------------------------

        [Fact]
        public async Task JObject_ScoreOnly_KeepsTheResultInsteadOfDiscardingIt()
        {
            // THE bug. A spec-valid statement carrying a score and no optional flags.
            XM.Result result = await FactorResultFromJson(
                @"{ ""score"": { ""raw"": 87, ""min"": 0, ""max"": 100 }, ""duration"": ""PT5M30S"", ""response"": ""answer-text"" }");

            result.Should().NotBeNull(
                "omitting two OPTIONAL fields must not discard the Result -- this returned null, and with it the score");
            result.Score.Should().NotBeNull("the score is the payload that was being lost");
            result.Score.Raw.Should().Be(87f);
            result.Response.Should().Be("answer-text");
            result.Duration.Should().Be(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(30));
        }

        [Fact]
        public async Task JObject_AbsentFlags_StayNullRatherThanBecomingFalse()
        {
            // Absence must not be rewritten into an assertion. Recording false here would state
            // that the learner did not succeed, which the producer never said.
            XM.Result result = await FactorResultFromJson(@"{ ""score"": { ""raw"": 87 } }");

            result.Success.Should().BeNull("silence is not a claim of failure");
            result.Completion.Should().BeNull("silence is not a claim of failure");
        }

        [Fact]
        public async Task JObject_PresentFlags_AreStillRead()
        {
            XM.Result result = await FactorResultFromJson(@"{ ""success"": true, ""completion"": true }");

            result.Success.Should().BeTrue();
            result.Completion.Should().BeTrue();
        }

        [Fact]
        public async Task JObject_ExplicitFalse_IsDistinctFromAbsent()
        {
            // The whole point of the nullable widening: false and absent are different facts and
            // must not collapse into each other.
            XM.Result result = await FactorResultFromJson(@"{ ""success"": false, ""completion"": false }");

            result.Success.Should().BeFalse("an explicit false is a real assertion and must survive");
            result.Completion.Should().BeFalse();
        }

        [Fact]
        public async Task JObject_DialectStringBooleans_StillCoerce()
        {
            // The original cast was documented as being about dialect coercion. Json.NET converts
            // "true"/"false" strings, and widening to (bool?) must not cost that.
            XM.Result result = await FactorResultFromJson(@"{ ""success"": ""true"", ""completion"": ""false"" }");

            result.Success.Should().BeTrue("string booleans are the dialect the cast existed to absorb");
            result.Completion.Should().BeFalse();
        }

        // ------------------------------------------------------------------
        // DTO path -- fabricated false rather than losing the Result
        // ------------------------------------------------------------------

        [Fact]
        public async Task Dto_AbsentFlags_AreNotFabricatedAsFalse()
        {
            XApiStatementDto dto = new XApiStatementDto
            {
                Id = "aaaaaaaa-bbbb-cccc-dddd-t3dto00000001",
                Actor = new XApiActorDto { ObjectType = "Agent", Name = "T3", Mbox = "mailto:t3@example.com" },
                Verb = new XApiVerbDto { Id = "http://adlnet.gov/expapi/verbs/completed" },
                Object = new XApiObjectDto { ObjectType = "Activity", Id = "http://example.com/activities/t3" },
                Result = new XApiResultDto { Score = new XApiScoreDto { Raw = 87 }, Response = "answer-text" }
            };

            (XM.Statement Statement, bool ParsedCorrectly) factored = await BuildFactor().FactorStatementFromDto(dto);

            factored.Statement.Result.Should().NotBeNull();
            factored.Statement.Result.Success.Should().BeNull(
                "the DTO already parsed absence into a bool? null -- the factor was flattening information the wire layer had preserved");
            factored.Statement.Result.Completion.Should().BeNull();
            factored.Statement.Result.Score.Raw.Should().Be(87f);
        }

        [Fact]
        public async Task Dto_ExplicitValues_AreCarriedThrough()
        {
            XApiStatementDto dto = new XApiStatementDto
            {
                Id = "aaaaaaaa-bbbb-cccc-dddd-t3dto00000002",
                Actor = new XApiActorDto { ObjectType = "Agent", Name = "T3", Mbox = "mailto:t3@example.com" },
                Verb = new XApiVerbDto { Id = "http://adlnet.gov/expapi/verbs/completed" },
                Object = new XApiObjectDto { ObjectType = "Activity", Id = "http://example.com/activities/t3" },
                Result = new XApiResultDto { Success = false, Completion = true }
            };

            (XM.Statement Statement, bool ParsedCorrectly) factored = await BuildFactor().FactorStatementFromDto(dto);

            factored.Statement.Result.Success.Should().BeFalse();
            factored.Statement.Result.Completion.Should().BeTrue();
        }
        // The award-gate reader tests were removed with the microcredential feature
        // (2026-08-18). They pinned how nullable Success/Completion flags satisfied a
        // credential's criteria, and there is no credential to satisfy any more. The xAPI
        // flag semantics above are unaffected and remain the point of this file.
    }
}
