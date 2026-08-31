// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Febris.ModelLibrary.ViewModels.XApi;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// SDKV-14 / SDKV-15 / SDKV-16 / SDKV-2 regression guards: the node's typed
    /// ingest DTO must bind BOTH the Febris wire dialect (what real SDK / PC /
    /// mobile clients send: lowercased keys, actor.member as a wrapper OBJECT
    /// <c>{id, uuid, actors:[...]}</c>, context.group as an ARRAY of actors,
    /// correctResponsesPattern and attachment display/description as bare
    /// STRINGS) and spec-correct xAPI 1.0.3 payloads. Before the tolerant
    /// converters, default Newtonsoft binding threw on every dialect shape and
    /// the binder turned that into ParseError -> 400 at /Submit -- every real
    /// SDK statement was rejected wholesale.
    /// Deserialization here mirrors XApiStatementBinding.ReadAsync exactly
    /// (JsonConvert.DeserializeObject with default settings); one test drives
    /// the real binder end-to-end.
    /// </summary>
    public class XApiWireDialectBindingTests
    {
        private static XApiStatementDto Bind(string json)
        {
            // Same call as XApiStatementBinding.ReadAsync step 3.
            return JsonConvert.DeserializeObject<XApiStatementDto>(json);
        }

        // ------------------------------------------------------------------
        // SDKV-14: actor.member -- dialect wrapper object vs spec bare array
        // ------------------------------------------------------------------

        [Fact]
        public void Member_DialectWrapperObject_BindsActorsIntoList()
        {
            XApiStatementDto dto = Bind(@"{
                ""actor"": {
                    ""name"": ""learner"",
                    ""member"": {
                        ""id"": 3,
                        ""uuid"": ""7f6e4bc0-0000-0000-0000-000000000001"",
                        ""actors"": [ { ""name"": ""a1"", ""mbox"": ""mailto:a1@example.com"" },
                                      { ""name"": ""a2"" } ]
                    }
                }
            }");

            dto.Should().NotBeNull("the dialect member wrapper must not 400 the statement (SDKV-14)");
            dto.Actor.Member.Should().HaveCount(2);
            dto.Actor.Member[0].Mbox.Should().Be("mailto:a1@example.com");
            dto.Actor.Member[1].Name.Should().Be("a2");
        }

        [Fact]
        public void Member_SpecBareArray_BindsAsIs()
        {
            XApiStatementDto dto = Bind(@"{
                ""actor"": { ""objectType"": ""Group"", ""member"": [ { ""mbox"": ""mailto:a1@example.com"" } ] }
            }");

            dto.Actor.Member.Should().HaveCount(1);
            dto.Actor.Member[0].Mbox.Should().Be("mailto:a1@example.com");
        }

        [Fact]
        public void Member_Null_BindsNull()
        {
            XApiStatementDto dto = Bind(@"{ ""actor"": { ""name"": ""learner"", ""member"": null } }");

            dto.Actor.Member.Should().BeNull();
        }

        [Fact]
        public void Member_DialectWrapperWithEmptyActors_BindsEmptyList()
        {
            // The SDK's SetupMember ALWAYS emits the wrapper, even for an
            // empty membership: {"id":0,"uuid":"00000000-...","actors":[]}.
            XApiStatementDto dto = Bind(@"{
                ""actor"": { ""member"": { ""id"": 0, ""uuid"": ""00000000-0000-0000-0000-000000000000"", ""actors"": [] } }
            }");

            dto.Should().NotBeNull();
            dto.Actor.Member.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void InstructorMember_DialectWrapper_BindsThroughSameConverter()
        {
            // context.instructor is an XApiActorDto, so its member slot rides
            // the same tolerant converter (SDKV-15 note on instructor.member).
            XApiStatementDto dto = Bind(@"{
                ""context"": { ""instructor"": { ""name"": ""teach"", ""member"": { ""actors"": [ { ""name"": ""m1"" } ] } } }
            }");

            dto.Context.Instructor.Member.Should().HaveCount(1);
            dto.Context.Instructor.Member[0].Name.Should().Be("m1");
        }

        // ------------------------------------------------------------------
        // SDKV-15: context.group -- dialect array vs single object
        // ------------------------------------------------------------------

        [Fact]
        public void ContextGroup_DialectArrayOfActors_Binds()
        {
            XApiStatementDto dto = Bind(@"{
                ""context"": { ""group"": [ { ""name"": ""g1"", ""mbox"": ""mailto:g1@example.com"" }, { ""name"": ""g2"" } ] }
            }");

            dto.Should().NotBeNull("the dialect group array must not 400 the statement (SDKV-15)");
            dto.Context.Group.Should().HaveCount(2);
            dto.Context.Group[0].Mbox.Should().Be("mailto:g1@example.com");
        }

        [Fact]
        public void ContextGroup_EmptyArray_BindsEmptyList()
        {
            // The SDK emits "group":[] on essentially every statement.
            XApiStatementDto dto = Bind(@"{ ""context"": { ""group"": [] } }");

            dto.Should().NotBeNull();
            dto.Context.Group.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void ContextGroup_SingleActorObject_WrapsToOneElementList()
        {
            // Spec team-style single Group object sent in the dialect slot.
            XApiStatementDto dto = Bind(@"{
                ""context"": { ""group"": { ""objectType"": ""Group"", ""name"": ""cohort"", ""member"": [ { ""name"": ""m1"" } ] } }
            }");

            dto.Context.Group.Should().HaveCount(1);
            dto.Context.Group[0].Name.Should().Be("cohort");
            dto.Context.Group[0].Member.Should().HaveCount(1);
        }

        // ------------------------------------------------------------------
        // contextActivities slots -- dialect IRI string vs spec object/array
        // (same reject class as SDKV-14/15; found while regression-testing
        // the SDKV-18 factor fix)
        // ------------------------------------------------------------------

        [Fact]
        public void ContextActivities_DialectStringSlots_BindAsSingleActivityLists()
        {
            XApiStatementDto dto = Bind(@"{
                ""context"": { ""contextactivities"": {
                    ""parent"": ""http://example.com/activity/parent"",
                    ""grouping"": null, ""category"": null, ""other"": null
                } }
            }");

            // Note: the lowercase correctly-spelled "contextactivities" key
            // binds to the spec property via Newtonsoft's case-insensitive
            // fallback; the ContextActivitesTyped alias only catches the
            // dialect TYPO spelling ("contextactivites").
            dto.Should().NotBeNull("dialect IRI-string context-activity slots must not 400 the statement");
            dto.Context.ContextActivities.Should().NotBeNull();
            dto.Context.ContextActivities.Parent.Should().HaveCount(1);
            dto.Context.ContextActivities.Parent[0].Id.Should().Be("http://example.com/activity/parent");
            dto.Context.ContextActivities.Grouping.Should().BeNull();
        }

        [Fact]
        public void ContextActivities_SpecArrayAndSingleObject_Bind()
        {
            XApiStatementDto dto = Bind(@"{
                ""context"": { ""contextActivities"": {
                    ""parent"": [ { ""id"": ""http://example.com/activity/p1"" }, { ""id"": ""http://example.com/activity/p2"" } ],
                    ""category"": { ""id"": ""http://example.com/activity/cat"" }
                } }
            }");

            dto.Context.ContextActivities.Parent.Should().HaveCount(2);
            dto.Context.ContextActivities.Category.Should().HaveCount(1);
            dto.Context.ContextActivities.Category[0].Id.Should().Be("http://example.com/activity/cat");
        }

        // ------------------------------------------------------------------
        // SDKV-16: correctResponsesPattern -- string vs array
        // ------------------------------------------------------------------

        [Fact]
        public void CorrectResponsesPattern_LoneString_WrapsToSingleElementList()
        {
            // Real SDK output: TokenToString collapses the value; test modules
            // author the literal "[,]".
            XApiStatementDto dto = Bind(@"{
                ""object"": { ""id"": ""http://example.com/activity/1"", ""definition"": { ""correctresponsespattern"": ""[,]"" } }
            }");

            dto.Should().NotBeNull("a stringified correctResponsesPattern must not 400 the statement (SDKV-16)");
            dto.Object.Definition.CorrectResponsesPattern.Should().Equal("[,]");
        }

        [Fact]
        public void CorrectResponsesPattern_SpecArray_PassesThrough()
        {
            XApiStatementDto dto = Bind(@"{
                ""object"": { ""definition"": { ""correctResponsesPattern"": [ ""golf"", ""tetris"" ] } }
            }");

            dto.Object.Definition.CorrectResponsesPattern.Should().Equal("golf", "tetris");
        }

        // ------------------------------------------------------------------
        // SDKV-2 (node side): attachment display/description -- string vs map
        // ------------------------------------------------------------------

        [Fact]
        public void AttachmentDisplayAndDescription_LoneStrings_WrapToEnLanguageMap()
        {
            XApiStatementDto dto = Bind(@"{
                ""attachments"": [ {
                    ""usagetype"": ""http://example.com/usage/video-review"",
                    ""display"": ""Video Review"",
                    ""description"": ""Session capture"",
                    ""contenttype"": ""video/mp4""
                } ]
            }");

            dto.Should().NotBeNull("string attachment display/description must not 400 the statement (SDKV-2)");
            dto.Attachments[0].Display.Should().ContainKey("en").WhoseValue.Should().Be("Video Review");
            dto.Attachments[0].Description.Should().ContainKey("en").WhoseValue.Should().Be("Session capture");
        }

        [Fact]
        public void AttachmentDisplayAndDescription_SpecLanguageMaps_PassThrough()
        {
            XApiStatementDto dto = Bind(@"{
                ""attachments"": [ { ""display"": { ""en-US"": ""Video Review"", ""fr"": ""Revue vidéo"" } } ]
            }");

            dto.Attachments[0].Display.Should().HaveCount(2);
            dto.Attachments[0].Display["en-US"].Should().Be("Video Review");
        }

        // ------------------------------------------------------------------
        // End-to-end pins
        // ------------------------------------------------------------------

        /// <summary>
        /// A statement shaped like actual SDK output (member wrapper, group
        /// array, stringified CRP, string attachment display/description,
        /// booleans-as-strings, all-lowercase keys). Every one of these shapes
        /// individually 400'd the whole statement before; together they are
        /// the "every real SDK statement is rejected" scenario of SDKV-14.
        /// </summary>
        [Fact]
        public void FullSdkDialectStatement_BindsWithoutThrowing()
        {
            XApiStatementDto dto = Bind(@"{
                ""id"": 0,
                ""uuid"": ""00000000-0000-0000-0000-000000000000"",
                ""actor"": {
                    ""objecttype"": ""Agent"",
                    ""name"": ""learner"",
                    ""mbox"": ""mailto:learner@example.com"",
                    ""member"": { ""id"": 0, ""uuid"": ""00000000-0000-0000-0000-000000000000"", ""actors"": [] }
                },
                ""verb"": { ""id"": ""https://febr.is/Verb/Details/Initialized"", ""display"": { ""en-us"": ""initialized"" } },
                ""object"": {
                    ""id"": ""http://example.com/activity/sim-1"",
                    ""objecttype"": ""Activity"",
                    ""definition"": { ""interactiontype"": ""performance"", ""correctresponsespattern"": ""[,]"" }
                },
                ""result"": { ""success"": ""true"", ""completion"": ""true"", ""duration"": ""00:00:00"" },
                ""context"": { ""group"": [], ""platform"": ""Unity"", ""language"": ""en-US"" },
                ""attachments"": [ { ""usagetype"": ""http://example.com/usage/video"", ""display"": ""Video Review"", ""description"": ""capture"", ""contenttype"": ""video/mp4"" } ]
            }");

            dto.Should().NotBeNull("the full SDK dialect statement must bind (SDKV-14/15/16/2)");
            dto.Actor.Member.Should().BeEmpty();
            dto.Context.Group.Should().BeEmpty();
            dto.Object.Definition.CorrectResponsesPattern.Should().Equal("[,]");
            dto.Result.Success.Should().BeTrue("Newtonsoft coerces the dialect's \"true\" string");
            dto.Attachments[0].Display.Should().ContainKey("en");
        }

        [Fact]
        public async Task XApiStatementBinding_ReadAsync_BindsTheDialectMemberAndGroupShapes()
        {
            // Drive the REAL /Submit binder (raw-bytes capture + typed parse).
            string json = @"{
                ""actor"": { ""mbox"": ""mailto:learner@example.com"", ""member"": { ""actors"": [ { ""name"": ""m1"" } ] } },
                ""verb"": { ""id"": ""https://febr.is/Verb/Details/Completed"" },
                ""object"": { ""id"": ""http://example.com/activity/sim-1"" },
                ""context"": { ""group"": [ { ""name"": ""g1"" } ] }
            }";
            DefaultHttpContext httpContext = new DefaultHttpContext();
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            httpContext.Request.Body = new MemoryStream(bytes);
            httpContext.Request.ContentType = "application/json";

            XApiStatementSubmission submission =
                await Febris.SharedServices.XApiStatementBinding.ReadAsync(httpContext.Request);

            submission.DtoBound.Should().BeTrue(
                "the binder must not reject dialect member/group shapes (SDKV-14/15); ParseError: {0}", submission.ParseError);
            submission.Dto.Actor.Member.Should().HaveCount(1);
            submission.Dto.Context.Group.Should().HaveCount(1);
            submission.RawBody.Should().Equal(bytes, "raw-bytes audit capture must stay verbatim");
        }

        /// <summary>
        /// The converters are read-only: the JObject bridge in
        /// StatementLogic.Submit (JObject.FromObject(dto)) must keep emitting
        /// the DTO's canonical shapes (member/group as ARRAYS, display as an
        /// OBJECT) so the legacy JObject factor sees what it always saw.
        /// </summary>
        [Fact]
        public void WriteSide_JObjectBridge_EmitsCanonicalShapes()
        {
            XApiStatementDto dto = Bind(@"{
                ""actor"": { ""member"": { ""actors"": [ { ""name"": ""m1"" } ] } },
                ""context"": { ""group"": { ""name"": ""cohort"" } },
                ""attachments"": [ { ""display"": ""Video Review"" } ]
            }");

            JObject bridged = JObject.FromObject(dto);

            bridged["actor"]["member"].Type.Should().Be(JTokenType.Array);
            bridged["context"]["group"].Type.Should().Be(JTokenType.Array);
            ((JArray)bridged["context"]["group"]).Should().HaveCount(1);
            bridged["attachments"][0]["display"].Type.Should().Be(JTokenType.Object);
            ((string)bridged["attachments"][0]["display"]["en"]).Should().Be("Video Review");
        }
    }
}
