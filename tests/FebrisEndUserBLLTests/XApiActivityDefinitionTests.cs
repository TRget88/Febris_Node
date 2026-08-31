// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    /// T3, third member: the Activity Definition was never stored, on either path.
    ///
    /// <para>
    /// <c>SetupObjectDefinition</c> was entirely commented out and returned an EMPTY
    /// <c>Definition</c>, and <c>SetupObjectDefinitionFromDto</c> was a literal stub that ignored
    /// its argument and did the same. Because <c>SetupObject</c> persists an activity on first
    /// sight, the node wrote a BLANK Definition row for every activity it had never seen, and threw
    /// away the name, description, activity type, moreInfo, interaction type, correct-responses
    /// pattern and interaction components that were sitting in the payload.
    /// </para>
    ///
    /// <para>
    /// The commented block could not be uncommented: it is stale against the current model.
    /// <c>Name</c> and <c>Description</c> are typed language maps now rather than strings, and
    /// <c>CorrectResponsesPattern</c> is a <c>List&lt;string&gt;</c>. It was also written in the
    /// throw-on-missing style that was destroying whole Results, so every read is null-tolerant.
    /// </para>
    ///
    /// <para>
    /// The object-level extensions read in <c>SetupObject</c> was a dead store: it assigned a local
    /// nothing read, and there is nowhere for it to go, because <c>Object</c> has no Extensions
    /// property and the table has no such column. Per spec an Activity's extensions belong inside
    /// its definition, where they are now read.
    /// </para>
    /// </summary>
    public class XApiActivityDefinitionTests
    {
        private static StatementFactor BuildFactor()
        {
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

            // SetupObject registers an unseen activity on first sight. Echo it back so the factor
            // sees a persisted object rather than the mock's default null.
            Mock<IObjectQueries> objectQueries = new Mock<IObjectQueries>();
            objectQueries.Setup(q => q.Create(It.IsAny<XM.Object>()))
                .ReturnsAsync((XM.Object o) => o);

            return new StatementFactor(
                accessor.Object,
                new Mock<IStatementQueries>().Object,
                new Mock<IActorQueries>().Object,
                new Mock<IMemberQueries>().Object,
                objectQueries.Object,
                new Mock<IVerbQueries>().Object,
                new Mock<IVersionQueries>().Object,
                new Mock<IExtensionsQueries>().Object);
        }

        private static async Task<XM.Definition> FactorDefinitionFromJson(string objectJson)
        {
            JObject input = JObject.Parse(@"{
                ""actor"": { ""objectType"": ""Agent"", ""name"": ""T3"", ""mbox"": ""mailto:t3@example.com"" },
                ""verb"": { ""id"": ""http://adlnet.gov/expapi/verbs/completed"" },
                ""object"": " + objectJson + @"
            }");

            (XM.Statement Statement, bool ParsedCorrectly) factored = await BuildFactor().FactorStatement(input);
            return factored.Statement?.Object?.Definition;
        }

        // ------------------------------------------------------------------
        // JObject path
        // ------------------------------------------------------------------

        [Fact]
        public async Task JObject_FullDefinition_IsStored()
        {
            XM.Definition definition = await FactorDefinitionFromJson(@"{
                ""objectType"": ""Activity"",
                ""id"": ""http://example.com/activities/quiz-1"",
                ""definition"": {
                    ""name"": { ""en-US"": ""Safety Quiz"" },
                    ""description"": { ""en-US"": ""Module 4 assessment"" },
                    ""type"": ""http://adlnet.gov/expapi/activities/assessment"",
                    ""moreInfo"": ""http://example.com/info/quiz-1"",
                    ""interactionType"": ""choice"",
                    ""correctResponsesPattern"": [ ""a"", ""b"" ]
                }
            }");

            definition.Should().NotBeNull("the definition is the human-readable record of what the learner did");
            definition.Name.Should().ContainKey("en-US").WhoseValue.Should().Be("Safety Quiz");
            definition.Description.Should().ContainKey("en-US").WhoseValue.Should().Be("Module 4 assessment");
            definition.Type.Should().Be(new Uri("http://adlnet.gov/expapi/activities/assessment"));
            definition.MoreInfo.Should().Be(new Uri("http://example.com/info/quiz-1"));
            definition.InteractionType.Should().Be("choice");
            definition.CorrectResponsesPattern.Should().Equal("a", "b");
        }

        [Fact]
        public async Task JObject_AbsentDefinition_ProducesNoBlankDefinition()
        {
            // The old code returned an empty Definition unconditionally, and SetupObject persists
            // on first sight, so a blank row was written for every new activity.
            XM.Definition definition = await FactorDefinitionFromJson(@"{
                ""objectType"": ""Activity"",
                ""id"": ""http://example.com/activities/no-definition""
            }");

            definition.Should().BeNull("no definition in the payload means no Definition row");
        }

        [Fact]
        public async Task JObject_MalformedType_DoesNotDestroyTheDefinition()
        {
            // Same rule as the context registration: a bad optional field costs that field only.
            XM.Definition definition = await FactorDefinitionFromJson(@"{
                ""objectType"": ""Activity"",
                ""id"": ""http://example.com/activities/quiz-2"",
                ""definition"": {
                    ""name"": { ""en-US"": ""Still Here"" },
                    ""type"": """"
                }
            }");

            definition.Should().NotBeNull();
            definition.Type.Should().BeNull("an empty type is no type");
            definition.Name.Should().ContainKey("en-US").WhoseValue.Should().Be("Still Here",
                "the fields beside it were valid and must survive");
        }

        [Fact]
        public async Task JObject_BareStringName_IsToleratedAsTheUndeterminedLocale()
        {
            // Matches the tolerance SetupVerb already applies to verb display: dialect producers
            // send a bare string where the spec sends a language map.
            XM.Definition definition = await FactorDefinitionFromJson(@"{
                ""objectType"": ""Activity"",
                ""id"": ""http://example.com/activities/quiz-3"",
                ""definition"": { ""name"": ""Bare String Name"" }
            }");

            definition.Should().NotBeNull();
            definition.Name.Should().ContainKey("und").WhoseValue.Should().Be("Bare String Name");
        }

        [Fact]
        public async Task JObject_SingleCorrectResponse_BecomesAOneElementList()
        {
            XM.Definition definition = await FactorDefinitionFromJson(@"{
                ""objectType"": ""Activity"",
                ""id"": ""http://example.com/activities/quiz-4"",
                ""definition"": { ""correctResponsesPattern"": ""only-answer"" }
            }");

            definition.Should().NotBeNull();
            definition.CorrectResponsesPattern.Should().Equal("only-answer");
        }

        [Fact]
        public async Task JObject_InteractionComponents_ArePreservedAsJson()
        {
            // The model carries one InteractionComponents string against five spec lists, so the
            // present ones are folded into that column rather than dropped.
            XM.Definition definition = await FactorDefinitionFromJson(@"{
                ""objectType"": ""Activity"",
                ""id"": ""http://example.com/activities/quiz-5"",
                ""definition"": {
                    ""interactionType"": ""choice"",
                    ""choices"": [ { ""id"": ""a"", ""description"": { ""en-US"": ""Alpha"" } } ]
                }
            }");

            definition.Should().NotBeNull();
            definition.InteractionComponents.Should().NotBeNull();
            JObject stored = JObject.Parse(definition.InteractionComponents);
            stored["choices"].Should().NotBeNull();
            stored["choices"][0]["id"].Value<string>().Should().Be("a");
        }

        [Fact]
        public async Task JObject_NoInteractionComponents_StoresNothing()
        {
            XM.Definition definition = await FactorDefinitionFromJson(@"{
                ""objectType"": ""Activity"",
                ""id"": ""http://example.com/activities/quiz-6"",
                ""definition"": { ""name"": { ""en-US"": ""Plain"" } }
            }");

            definition.Should().NotBeNull();
            definition.InteractionComponents.Should().BeNull("an empty component set is not an empty JSON object");
        }

        // ------------------------------------------------------------------
        // Typed DTO path, which was a stub returning new Definition()
        // ------------------------------------------------------------------

        private static async Task<XM.Definition> FactorDefinitionFromDto(XApiObjectDto objectDto)
        {
            XApiStatementDto dto = new XApiStatementDto
            {
                Id = "aaaaaaaa-bbbb-cccc-dddd-t3def00000001",
                Actor = new XApiActorDto { ObjectType = "Agent", Name = "T3", Mbox = "mailto:t3@example.com" },
                Verb = new XApiVerbDto { Id = "http://adlnet.gov/expapi/verbs/completed" },
                Object = objectDto
            };

            (XM.Statement Statement, bool ParsedCorrectly) factored = await BuildFactor().FactorStatementFromDto(dto);
            return factored.Statement?.Object?.Definition;
        }

        [Fact]
        public async Task Dto_FullDefinition_IsStored()
        {
            XM.Definition definition = await FactorDefinitionFromDto(new XApiObjectDto
            {
                ObjectType = "Activity",
                Id = "http://example.com/activities/quiz-7",
                Definition = new XApiActivityDefinitionDto
                {
                    Name = new Dictionary<string, string> { ["en-US"] = "Safety Quiz" },
                    Description = new Dictionary<string, string> { ["en-US"] = "Module 4 assessment" },
                    Type = "http://adlnet.gov/expapi/activities/assessment",
                    MoreInfo = "http://example.com/info/quiz-7",
                    InteractionType = "choice",
                    CorrectResponsesPattern = new List<string> { "a", "b" }
                }
            });

            definition.Should().NotBeNull("the typed path returned an empty Definition regardless of input");
            definition.Name.Should().ContainKey("en-US").WhoseValue.Should().Be("Safety Quiz");
            definition.Description.Should().ContainKey("en-US").WhoseValue.Should().Be("Module 4 assessment");
            definition.Type.Should().Be(new Uri("http://adlnet.gov/expapi/activities/assessment"));
            definition.MoreInfo.Should().Be(new Uri("http://example.com/info/quiz-7"));
            definition.InteractionType.Should().Be("choice");
            definition.CorrectResponsesPattern.Should().Equal("a", "b");
        }

        [Fact]
        public async Task Dto_InteractionComponents_ArePreservedAsJson()
        {
            XM.Definition definition = await FactorDefinitionFromDto(new XApiObjectDto
            {
                ObjectType = "Activity",
                Id = "http://example.com/activities/quiz-8",
                Definition = new XApiActivityDefinitionDto
                {
                    InteractionType = "choice",
                    Choices = new List<XApiInteractionComponentDto>
                    {
                        new XApiInteractionComponentDto
                        {
                            Id = "a",
                            Description = new Dictionary<string, string> { ["en-US"] = "Alpha" }
                        }
                    }
                }
            });

            definition.Should().NotBeNull();
            definition.InteractionComponents.Should().NotBeNull();
            JObject stored = JObject.Parse(definition.InteractionComponents);
            stored["choices"][0]["id"].Value<string>().Should().Be("a");
        }

        [Fact]
        public async Task Dto_AbsentDefinition_ProducesNoBlankDefinition()
        {
            XM.Definition definition = await FactorDefinitionFromDto(new XApiObjectDto
            {
                ObjectType = "Activity",
                Id = "http://example.com/activities/quiz-9"
            });

            definition.Should().BeNull();
        }
    }
}
