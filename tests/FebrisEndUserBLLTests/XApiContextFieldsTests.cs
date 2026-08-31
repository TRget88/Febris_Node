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
using Newtonsoft.Json.Linq;
using Xunit;
using XM = Febris.ModelLibrary.Models.XApiModels;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// T3, second member: <c>context.registration</c>, <c>revision</c>, <c>platform</c> and
    /// <c>language</c> were never stored.
    ///
    /// <para>
    /// All four assignments sat commented out in both factor paths. The model carries all four
    /// properties and the <c>Context</c> table already has all four columns, so nothing was missing
    /// except the four lines that connect them. The audit named only <c>registration</c>.
    /// </para>
    ///
    /// <para>
    /// It looked like working code because the dead-context guard reads every one of them:
    /// </para>
    /// <code>
    /// if (context.ContextActivities == null &amp;&amp; context.Registration == Guid.Empty
    ///     &amp;&amp; context.Revision == null &amp;&amp; ... &amp;&amp; context.Language == null) return null;
    /// </code>
    /// <para>
    /// Every one of those four terms was testing a constant. The consequence is worse than losing
    /// four fields: a context carrying ONLY a registration satisfied the whole guard and the entire
    /// Context was discarded, taking the registration with it. A registration is what ties a set of
    /// statements into one attempt, so losing it silently un-groups a learner's session.
    /// </para>
    ///
    /// <para>
    /// The restored registration read uses <c>Guid.TryParse</c>, NOT the commented
    /// <c>(Guid)input["registration"]</c>. That cast throws on a malformed value and the outer
    /// catch returns null for the whole Context, which is the same shape as the optional
    /// success/completion cast that was destroying whole Results.
    /// </para>
    /// </summary>
    public class XApiContextFieldsTests
    {
        private const string Registration = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

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

        private static async Task<XM.Context> FactorContextFromJson(string contextJson)
        {
            JObject input = JObject.Parse(@"{
                ""actor"": { ""objectType"": ""Agent"", ""name"": ""T3"", ""mbox"": ""mailto:t3@example.com"" },
                ""verb"": { ""id"": ""http://adlnet.gov/expapi/verbs/completed"" },
                ""object"": { ""objectType"": ""Activity"", ""id"": ""http://example.com/activities/t3"" },
                ""context"": " + contextJson + @"
            }");

            (XM.Statement Statement, bool ParsedCorrectly) factored = await BuildFactor().FactorStatement(input);
            return factored.Statement?.Context;
        }

        // ------------------------------------------------------------------
        // JObject path
        // ------------------------------------------------------------------

        [Fact]
        public async Task JObject_RegistrationOnlyContext_IsNotDiscardedWholesale()
        {
            // THE consequence. Every other context field is absent, so before the fix the guard
            // saw an entirely empty Context and returned null -- discarding the registration that
            // was right there in the payload.
            XM.Context context = await FactorContextFromJson(@"{ ""registration"": """ + Registration + @""" }");

            context.Should().NotBeNull(
                "a context carrying only a registration is a real context, and the guard was testing constants");
            context.Registration.Should().Be(Guid.Parse(Registration));
        }

        [Fact]
        public async Task JObject_AllFourFields_AreStored()
        {
            XM.Context context = await FactorContextFromJson(@"{
                ""registration"": """ + Registration + @""",
                ""revision"": ""rev-4"",
                ""platform"": ""FebrisPC"",
                ""language"": ""en-US""
            }");

            context.Registration.Should().Be(Guid.Parse(Registration));
            context.Revision.Should().Be("rev-4");
            context.Platform.Should().Be("FebrisPC");
            context.Language.Should().Be("en-US");
        }

        [Fact]
        public async Task JObject_MalformedRegistration_DoesNotDestroyTheContext()
        {
            // The reason TryParse replaced the commented (Guid) cast. A bad registration must cost
            // the registration, not the platform and language sitting beside it.
            XM.Context context = await FactorContextFromJson(@"{
                ""registration"": ""not-a-guid"",
                ""platform"": ""FebrisPC"",
                ""language"": ""en-US""
            }");

            context.Should().NotBeNull("a malformed optional field must not take the whole context with it");
            context.Registration.Should().Be(Guid.Empty, "an unparseable registration is no registration");
            context.Platform.Should().Be("FebrisPC", "the fields beside it were valid and must survive");
            context.Language.Should().Be("en-US");
        }

        [Fact]
        public async Task JObject_AbsentFields_StayAbsent()
        {
            // Nothing is fabricated. This context is genuinely empty and is still dropped, which is
            // the guard's real purpose and must keep working.
            XM.Context context = await FactorContextFromJson(@"{ }");

            context.Should().BeNull("an actually-empty context is still nothing, and the guard must still say so");
        }

        [Fact]
        public async Task JObject_RegistrationIsReadCaseInsensitively()
        {
            // Matches the SDKV-17/18 sweep: the /Submit DTO bridge emits spec casing.
            XM.Context context = await FactorContextFromJson(@"{ ""Registration"": """ + Registration + @""" }");

            context.Should().NotBeNull();
            context.Registration.Should().Be(Guid.Parse(Registration));
        }

        // ------------------------------------------------------------------
        // Typed DTO path
        // ------------------------------------------------------------------

        private static async Task<XM.Context> FactorContextFromDto(XApiContextDto contextDto)
        {
            XApiStatementDto dto = new XApiStatementDto
            {
                Id = "aaaaaaaa-bbbb-cccc-dddd-t3ctx00000001",
                Actor = new XApiActorDto { ObjectType = "Agent", Name = "T3", Mbox = "mailto:t3@example.com" },
                Verb = new XApiVerbDto { Id = "http://adlnet.gov/expapi/verbs/completed" },
                Object = new XApiObjectDto { ObjectType = "Activity", Id = "http://example.com/activities/t3" },
                Context = contextDto
            };

            (XM.Statement Statement, bool ParsedCorrectly) factored = await BuildFactor().FactorStatementFromDto(dto);
            return factored.Statement?.Context;
        }

        [Fact]
        public async Task Dto_AllFourFields_AreStored()
        {
            // The DTO parsed all four off the wire correctly and the factor dropped them, exactly
            // like the JObject path.
            XM.Context context = await FactorContextFromDto(new XApiContextDto
            {
                Registration = Registration,
                Revision = "rev-4",
                Platform = "FebrisPC",
                Language = "en-US"
            });

            context.Should().NotBeNull();
            context.Registration.Should().Be(Guid.Parse(Registration));
            context.Revision.Should().Be("rev-4");
            context.Platform.Should().Be("FebrisPC");
            context.Language.Should().Be("en-US");
        }

        [Fact]
        public async Task Dto_RegistrationOnlyContext_IsNotDiscardedWholesale()
        {
            XM.Context context = await FactorContextFromDto(new XApiContextDto { Registration = Registration });

            context.Should().NotBeNull();
            context.Registration.Should().Be(Guid.Parse(Registration));
        }

        [Fact]
        public async Task Dto_MalformedRegistration_DoesNotDestroyTheContext()
        {
            XM.Context context = await FactorContextFromDto(new XApiContextDto
            {
                Registration = "not-a-guid",
                Platform = "FebrisPC"
            });

            context.Should().NotBeNull();
            context.Registration.Should().Be(Guid.Empty);
            context.Platform.Should().Be("FebrisPC");
        }

        [Fact]
        public async Task Dto_EmptyContext_IsStillDropped()
        {
            XM.Context context = await FactorContextFromDto(new XApiContextDto());

            context.Should().BeNull();
        }
    }
}
