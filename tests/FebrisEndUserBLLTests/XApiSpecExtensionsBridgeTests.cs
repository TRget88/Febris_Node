// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.ViewModels.XApi;
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
    /// T3, final member: spec-shaped <c>extensions</c> were dropped on ingest.
    ///
    /// <para>
    /// The node stores extensions as a single comma-delimited <c>iri:value</c> string
    /// (<c>Extensions.ExtensionMap</c>) that feeds <c>ExtensionIRIOptions</c> and the
    /// <c>XApiResultExtras</c> pipeline. <c>SetupExtensions</c> only ever read the Febris dialect
    /// <c>extensionmap</c> key or an existing row <c>id</c>, so a spec producer sending
    /// <c>"extensions": {"http://example.com/ext": "42"}</c> got null back and lost the lot.
    /// </para>
    ///
    /// <para>
    /// On the owner's ruling the spec shape is bridged into the existing dialect string rather than
    /// the column being widened, so the whole downstream pipeline keeps working unchanged and no
    /// migration is needed.
    /// </para>
    ///
    /// <para>
    /// The format is lossy by construction: entries split on commas and the value is the THIRD
    /// colon-separated part. A value containing a comma would read back as two entries, and one
    /// containing a colon would read back truncated. Storing data that reads back as something the
    /// producer never sent is the exact failure class as the rest of T3, so every candidate entry
    /// is round-tripped through the real reader and skipped if it would not survive.
    /// </para>
    /// </summary>
    public class XApiSpecExtensionsBridgeTests
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

        private static async Task<XM.Extensions> FactorExtensionsFromJson(string extensionsJson)
        {
            JObject input = JObject.Parse(@"{
                ""actor"": { ""objectType"": ""Agent"", ""name"": ""T3"", ""mbox"": ""mailto:t3@example.com"" },
                ""verb"": { ""id"": ""http://adlnet.gov/expapi/verbs/completed"" },
                ""object"": { ""objectType"": ""Activity"", ""id"": ""http://example.com/activities/t3"" },
                ""result"": { ""extensions"": " + extensionsJson + @" }
            }");

            (XM.Statement Statement, bool ParsedCorrectly) factored = await BuildFactor().FactorStatement(input);
            return factored.Statement?.Result?.Extensions;
        }

        // ------------------------------------------------------------------
        // JObject path
        // ------------------------------------------------------------------

        [Fact]
        public async Task JObject_SpecExtensionsObject_IsBridgedIntoTheDialectString()
        {
            XM.Extensions extensions = await FactorExtensionsFromJson(
                @"{ ""http://example.com/ext"": ""42"" }");

            extensions.Should().NotBeNull("a spec extensions object used to be dropped entirely");
            extensions.ExtensionMap.Should().Be("http://example.com/ext:42");
        }

        [Fact]
        public async Task JObject_BridgedEntry_ReadsBackThroughTheRealParser()
        {
            // The assertion that matters: what is stored is what the existing pipeline reads.
            XM.Extensions extensions = await FactorExtensionsFromJson(
                @"{ ""http://example.com/ext"": ""42"" }");

            ExtensionIRIOptions iri;
            string value;
            bool parsed = ExtensionMapParsing.TryParseExtensionEntry(extensions.ExtensionMap, out iri, out value);

            parsed.Should().BeTrue("the stored entry must be readable by the parser the pipeline uses");
            value.Should().Be("42", "and it must read back as the value the producer sent");
        }

        [Fact]
        public async Task JObject_MultipleExtensions_AreCommaJoined()
        {
            XM.Extensions extensions = await FactorExtensionsFromJson(
                @"{ ""http://example.com/a"": ""1"", ""http://example.com/b"": ""2"" }");

            extensions.Should().NotBeNull();
            extensions.ExtensionMap.Should().Be("http://example.com/a:1,http://example.com/b:2");
        }

        [Fact]
        public async Task JObject_ValueContainingAColon_IsSkippedRatherThanTruncated()
        {
            // The value is read back as the third colon-separated part, so "12:30" would return
            // "12". Storing that would attribute a value to the learner that was never sent.
            XM.Extensions extensions = await FactorExtensionsFromJson(
                @"{ ""http://example.com/duration"": ""12:30"" }");

            extensions.Should().BeNull("nothing round-tripped, so there is no map to store");
        }

        [Fact]
        public async Task JObject_ValueContainingAComma_IsSkipped()
        {
            // A comma would be read back as an entry separator, splitting one value into two.
            XM.Extensions extensions = await FactorExtensionsFromJson(
                @"{ ""http://example.com/list"": ""a,b"" }");

            extensions.Should().BeNull();
        }

        [Fact]
        public async Task JObject_TheSurvivingEntriesAreKeptWhenOneIsSkipped()
        {
            // A single unstorable entry must not cost the others.
            XM.Extensions extensions = await FactorExtensionsFromJson(
                @"{ ""http://example.com/bad"": ""12:30"", ""http://example.com/good"": ""7"" }");

            extensions.Should().NotBeNull();
            extensions.ExtensionMap.Should().Be("http://example.com/good:7");
        }

        [Fact]
        public async Task JObject_DialectExtensionMap_StillWins()
        {
            // The existing producers keep working untouched.
            XM.Extensions extensions = await FactorExtensionsFromJson(
                @"{ ""extensionmap"": ""http://example.com/ext:99"" }");

            extensions.Should().NotBeNull();
            extensions.ExtensionMap.Should().Be("http://example.com/ext:99");
        }

        [Fact]
        public async Task JObject_EmptyExtensions_StillYieldNothing()
        {
            XM.Extensions extensions = await FactorExtensionsFromJson(@"{ }");

            extensions.Should().BeNull();
        }

        // ------------------------------------------------------------------
        // Typed DTO path
        // ------------------------------------------------------------------

        [Fact]
        public async Task Dto_SpecExtensionsObject_IsBridgedIntoTheDialectString()
        {
            XApiStatementDto dto = new XApiStatementDto
            {
                Id = "aaaaaaaa-bbbb-cccc-dddd-t3ext00000001",
                Actor = new XApiActorDto { ObjectType = "Agent", Name = "T3", Mbox = "mailto:t3@example.com" },
                Verb = new XApiVerbDto { Id = "http://adlnet.gov/expapi/verbs/completed" },
                Object = new XApiObjectDto { ObjectType = "Activity", Id = "http://example.com/activities/t3" },
                Result = new XApiResultDto
                {
                    Extensions = new Dictionary<string, JToken> { ["http://example.com/ext"] = "42" }
                }
            };

            (XM.Statement Statement, bool ParsedCorrectly) factored = await BuildFactor().FactorStatementFromDto(dto);

            factored.Statement.Result.Extensions.Should().NotBeNull();
            factored.Statement.Result.Extensions.ExtensionMap.Should().Be("http://example.com/ext:42");
        }

        [Fact]
        public async Task Dto_DialectExtensionMap_StillWins()
        {
            XApiStatementDto dto = new XApiStatementDto
            {
                Id = "aaaaaaaa-bbbb-cccc-dddd-t3ext00000002",
                Actor = new XApiActorDto { ObjectType = "Agent", Name = "T3", Mbox = "mailto:t3@example.com" },
                Verb = new XApiVerbDto { Id = "http://adlnet.gov/expapi/verbs/completed" },
                Object = new XApiObjectDto { ObjectType = "Activity", Id = "http://example.com/activities/t3" },
                Result = new XApiResultDto
                {
                    Extensions = new Dictionary<string, JToken> { ["extensionmap"] = "http://example.com/ext:99" }
                }
            };

            (XM.Statement Statement, bool ParsedCorrectly) factored = await BuildFactor().FactorStatementFromDto(dto);

            factored.Statement.Result.Extensions.ExtensionMap.Should().Be("http://example.com/ext:99");
        }
    }
}
