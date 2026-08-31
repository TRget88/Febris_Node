// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using Febris.ModelLibrary.Models.XApiModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Durability guard for the xAPI language-map / interaction-array typing (the Option-B uplift).
    ///
    /// <para>
    /// xAPI 1.0.3 defines Verb.display, Attachment.display/description, and Definition.name/description
    /// as <b>Language Map objects</b> ({"en-US":"..."}) and Definition.correctResponsesPattern as a
    /// <b>string array</b>. Modelling them as a flat <c>string</c> forced the node to store hand-built
    /// JSON in a text column and then <c>JsonConvert.SerializeObject</c> that string on the wire, which
    /// double-encodes it (an escaped JSON scalar, not a nested object) -- a spec violation flagged in
    /// StatementFactor as "//this is the problem".
    /// </para>
    ///
    /// <para>
    /// These tests assert the spec-correct emitted shape. They FAIL on the pre-uplift string types
    /// (proving the double-encode) and PASS once the POCOs carry their true xAPI types.
    /// </para>
    /// </summary>
    public class XApiLanguageMapWireTests
    {
        [Fact]
        public void Verb_Display_serializes_as_a_nested_language_map_object_not_an_escaped_string()
        {
            var verb = new Verb
            {
                Id = new Uri("http://adlnet.gov/expapi/verbs/completed"),
                Display = new Dictionary<string, string> { ["en"] = "completed" },
            };

            JToken display = JObject.Parse(JsonConvert.SerializeObject(verb))["Display"];

            Assert.Equal(JTokenType.Object, display.Type);
            Assert.Equal("completed", (string)display["en"]);
        }

        [Fact]
        public void Definition_CorrectResponsesPattern_serializes_as_a_json_array_not_a_string()
        {
            var definition = new Definition
            {
                CorrectResponsesPattern = new List<string> { "golf", "tennis" },
            };

            JToken crp = JObject.Parse(JsonConvert.SerializeObject(definition))["CorrectResponsesPattern"];

            Assert.Equal(JTokenType.Array, crp.Type);
            Assert.Equal("golf", (string)crp[0]);
        }
    }
}
