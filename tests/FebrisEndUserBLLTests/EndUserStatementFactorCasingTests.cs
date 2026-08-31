// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading.Tasks;
using Febris.ModelLibrary.ViewModels.XApi;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.UserNode.DataAccessLayer.Queries.XAPIQueries;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using XM = Febris.ModelLibrary.Models.XApiModels;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// SDKV-17 / SDKV-18 regression guards: the EndUser JObject factor must
    /// read keys case-INsensitively. The default /Submit route bridges the
    /// bound DTO through <c>JObject.FromObject(submission.Dto)</c>, which
    /// emits the DTO's camelCase JsonProperty names (<c>usageType</c>,
    /// <c>contentType</c>, <c>fileUrl</c>, <c>contextActivities</c>), while
    /// the factor historically indexed lowercase dialect keys with the
    /// case-sensitive JObject indexer: <c>item["contenttype"]</c> came back
    /// null, <c>.ToString()</c> threw NRE, and the catch silently dropped
    /// every attachment (and camelCase context activities never matched).
    /// The deliberately-preserved <c>contextactivites</c> dialect typo alias
    /// must also keep parsing.
    /// </summary>
    public class EndUserStatementFactorCasingTests
    {
        private static StatementFactor BuildFactor(IObjectQueries objectQueries = null, IVerbQueries verbQueries = null)
        {
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            return new StatementFactor(
                accessor.Object,
                new Mock<IStatementQueries>().Object,
                new Mock<IActorQueries>().Object,
                new Mock<IMemberQueries>().Object,
                objectQueries ?? new Mock<IObjectQueries>().Object,
                verbQueries ?? new Mock<IVerbQueries>().Object,
                new Mock<IVersionQueries>().Object,
                new Mock<IExtensionsQueries>().Object);
        }

        // ------------------------------------------------------------------
        // SDKV-17: attachments
        // ------------------------------------------------------------------

        [Fact]
        public async Task Attachments_CamelCaseDtoBridgeKeys_AreParsedNotDropped()
        {
            // Exactly the key casing JObject.FromObject(dto) emits on /Submit.
            var input = JObject.Parse(@"{
                ""attachments"": [ {
                    ""usageType"": ""http://example.com/usage/video-review"",
                    ""display"": { ""en"": ""Video Review"" },
                    ""description"": { ""en"": ""Session capture"" },
                    ""contentType"": ""video/mp4"",
                    ""length"": 123456,
                    ""sha2"": ""abc123"",
                    ""fileUrl"": ""http://example.com/files/review.mp4""
                } ]
            }");

            var result = await BuildFactor().FactorStatement(input);

            result.ParsedCorrectly.Should().BeTrue();
            result.Statement.Attachments.Should().NotBeNull("camelCase keys must not NRE-drop the attachment list (SDKV-17)").And.HaveCount(1);
            var attachment = result.Statement.Attachments[0];
            attachment.UsageType.Should().Be(new Uri("http://example.com/usage/video-review"));
            attachment.ContentType.Should().Be("video/mp4");
            attachment.Length.Should().Be(123456);
            attachment.Sha2.Should().Be("abc123");
            attachment.FileURL.Should().Be(new Uri("http://example.com/files/review.mp4"));
        }

        [Fact]
        public async Task Attachments_LowercaseDialectKeys_StillParse()
        {
            var input = JObject.Parse(@"{
                ""attachments"": [ {
                    ""usagetype"": ""http://example.com/usage/video-review"",
                    ""display"": ""Video Review"",
                    ""description"": ""capture"",
                    ""contenttype"": ""video/mp4"",
                    ""length"": 1,
                    ""sha2"": ""abc"",
                    ""fileurl"": ""http://example.com/f.mp4""
                } ]
            }");

            var result = await BuildFactor().FactorStatement(input);

            result.Statement.Attachments.Should().HaveCount(1);
            result.Statement.Attachments[0].ContentType.Should().Be("video/mp4");
            result.Statement.Attachments[0].Display.Values.Should().Contain("Video Review");
        }

        [Fact]
        public async Task Attachments_MissingOptionalFields_NoLongerDropTheWholeList()
        {
            // Historically ANY absent key (e.g. no description) threw NRE and
            // the catch returned null -- every attachment silently lost.
            var input = JObject.Parse(@"{
                ""attachments"": [ { ""usageType"": ""http://example.com/usage/sig"", ""contentType"": ""application/octet-stream"" } ]
            }");

            var result = await BuildFactor().FactorStatement(input);

            result.Statement.Attachments.Should().HaveCount(1);
            result.Statement.Attachments[0].ContentType.Should().Be("application/octet-stream");
            result.Statement.Attachments[0].Description.Should().BeNull();
        }

        // ------------------------------------------------------------------
        // SDKV-18: contextActivities casing + the preserved typo alias
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("contextActivities")] // DTO-bridge / spec casing
        [InlineData("contextactivities")] // lowercased dialect casing
        [InlineData("contextactivites")]  // deliberately-preserved dialect typo alias
        public async Task ContextActivities_AllKeySpellings_AreParsed(string keyName)
        {
            var input = new JObject
            {
                ["context"] = new JObject
                {
                    [keyName] = new JObject
                    {
                        ["parent"] = "http://example.com/activity/parent",
                        ["grouping"] = "http://example.com/activity/grouping",
                        ["category"] = "http://example.com/activity/category",
                        ["other"] = "http://example.com/activity/other",
                    },
                },
            };

            var result = await BuildFactor().FactorStatement(input);

            result.ParsedCorrectly.Should().BeTrue();
            result.Statement.Context.Should().NotBeNull();
            result.Statement.Context.ContextActivities.Should().NotBeNull(
                "the '{0}' spelling must parse (SDKV-18 + preserved dialect alias)", keyName);
            result.Statement.Context.ContextActivities.Parent.Should().Be("http://example.com/activity/parent");
        }

        [Fact]
        public async Task ContextActivities_PartialSet_IsParsedInsteadOfDropped()
        {
            // Historically all four keys had to be PRESENT (missing key -> NRE
            // -> catch -> null); spec producers sending only parent lost it.
            var input = JObject.Parse(@"{
                ""context"": { ""contextActivities"": { ""parent"": ""http://example.com/activity/parent"" } }
            }");

            var result = await BuildFactor().FactorStatement(input);

            result.Statement.Context.Should().NotBeNull();
            result.Statement.Context.ContextActivities.Should().NotBeNull();
            result.Statement.Context.ContextActivities.Parent.Should().Be("http://example.com/activity/parent");
            result.Statement.Context.ContextActivities.Grouping.Should().BeNull();
        }

        [Fact]
        public async Task ContextActivities_SpecArrayShape_ExtractsTheActivityIri()
        {
            // The /Submit DTO bridge re-emits each slot as an ARRAY of activity
            // objects; the domain column is a single string. A one-element
            // array must store the same IRI the dialect wire carried.
            var input = JObject.Parse(@"{
                ""context"": { ""contextActivities"": { ""parent"": [ { ""id"": ""http://example.com/activity/parent"" } ] } }
            }");

            var result = await BuildFactor().FactorStatement(input);

            result.Statement.Context.ContextActivities.Should().NotBeNull();
            result.Statement.Context.ContextActivities.Parent.Should().Be("http://example.com/activity/parent");
        }

        // ------------------------------------------------------------------
        // General casing sweep
        // ------------------------------------------------------------------

        [Fact]
        public async Task ObjectType_LowercaseDialectKey_IsCapturedOnPersistOnMiss()
        {
            // The old read was (string)input["objectType"] -- camelCase-only --
            // so the lowercased dialect "objecttype" silently produced a null
            // ObjectType on every persist-on-miss registration.
            var objectMock = new Mock<IObjectQueries>();
            objectMock.Setup(x => x.Create(It.IsAny<XM.Object>())).ReturnsAsync((XM.Object o) => o);
            var input = JObject.Parse(@"{
                ""object"": { ""id"": ""http://example.com/activity/sim-1"", ""objecttype"": ""Activity"" }
            }");

            var result = await BuildFactor(objectQueries: objectMock.Object).FactorStatement(input);

            result.Statement.Object.Should().NotBeNull();
            result.Statement.Object.ObjectType.Should().Be("Activity");
        }

        [Fact]
        public async Task TransientVerb_WithoutDisplay_NoLongerDropsTheStatement()
        {
            // Old code: (string)input["display"].ToString() NRE'd for a
            // display-less unseen verb -> SetupVerb catch -> null verb.
            var input = JObject.Parse(@"{ ""verb"": { ""id"": ""http://example.com/verbs/custom"" } }");

            var result = await BuildFactor().FactorStatement(input);

            result.ParsedCorrectly.Should().BeTrue();
            result.Statement.Verb.Should().NotBeNull();
            result.Statement.Verb.Id.Should().Be(new Uri("http://example.com/verbs/custom"));
            result.Statement.Verb.Display.Should().BeNull();
        }

        [Fact]
        public async Task ExplicitNullTokens_FromTheDtoBridge_AreTreatedAsAbsent()
        {
            // JObject.FromObject(dto) emits "result": null / "timestamp": null
            // for unset DTO members (NullValueHandling.Include default).
            var input = JObject.Parse(@"{
                ""timestamp"": null, ""actor"": null, ""verb"": null, ""object"": null,
                ""result"": null, ""context"": null, ""authority"": null, ""attachments"": null
            }");

            var result = await BuildFactor().FactorStatement(input);

            result.ParsedCorrectly.Should().BeTrue();
            result.Statement.Timestamp.Should().Be(default(DateTime));
            result.Statement.Result.Should().BeNull();
            result.Statement.Object.Should().BeNull("a null object token must not become a transient Key-0 object");
            result.Statement.Verb.Should().BeNull("a null verb token must not become a transient empty verb");
        }

        /// <summary>
        /// End-to-end /Submit shape: dialect JSON -> tolerant DTO bind ->
        /// JObject bridge (camelCase re-emission) -> JObject factor. This is
        /// the exact route where SDKV-17/18 lost attachments and context
        /// activities even after the DTO bind was fixed.
        /// </summary>
        [Fact]
        public async Task DialectStatement_ThroughDtoBridge_KeepsAttachmentsAndContextActivities()
        {
            string dialectJson = @"{
                ""actor"": { ""mbox"": ""mailto:learner@example.com"", ""member"": { ""actors"": [] } },
                ""verb"": { ""id"": ""https://febr.is/Verb/Details/Completed"", ""display"": { ""en-us"": ""completed"" } },
                ""object"": { ""id"": ""http://example.com/activity/sim-1"", ""objecttype"": ""Activity"" },
                ""context"": {
                    ""group"": [],
                    ""contextactivities"": { ""parent"": ""http://example.com/activity/parent"", ""grouping"": null, ""category"": null, ""other"": null }
                },
                ""attachments"": [ { ""usagetype"": ""http://example.com/usage/video"", ""display"": ""Video Review"", ""description"": ""capture"", ""contenttype"": ""video/mp4"", ""length"": 5, ""sha2"": ""ff"", ""fileurl"": ""http://example.com/v.mp4"" } ]
            }";
            XApiStatementDto dto = JsonConvert.DeserializeObject<XApiStatementDto>(dialectJson);
            dto.Should().NotBeNull("the dialect must bind (SDKV-14/15/16/2)");
            JObject bridged = JObject.FromObject(dto); // same bridge as StatementLogic.Submit

            var objectMock = new Mock<IObjectQueries>();
            objectMock.Setup(x => x.Create(It.IsAny<XM.Object>())).ReturnsAsync((XM.Object o) => o);
            var result = await BuildFactor(objectQueries: objectMock.Object).FactorStatement(bridged);

            result.ParsedCorrectly.Should().BeTrue();
            result.Statement.Attachments.Should().HaveCount(1, "bridged camelCase attachment keys must parse (SDKV-17)");
            result.Statement.Attachments[0].ContentType.Should().Be("video/mp4");
            result.Statement.Context.Should().NotBeNull();
            result.Statement.Context.ContextActivities.Should().NotBeNull("bridged contextActivities must parse (SDKV-18)");
            result.Statement.Context.ContextActivities.Parent.Should().Be("http://example.com/activity/parent");
        }
    }
}
