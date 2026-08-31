// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
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
    /// Pins the tenant xAPI factor to the platform's federation design (central is the source of truth):
    ///   - Verb / Version / Object are owned by CENTRAL; the tenant fetches them over HTTP.
    ///   - The tenant does NOT set xAPI Version -- central owns it (SetupVersion was removed).
    ///   - A Verb that central returns must be USED, not fabricated (the SetupVerb `verb != null` fix;
    ///     it was previously inverted to `verb == null`, so central-known verbs were being discarded).
    /// These are the two anomalies a prior session introduced that had no test to catch them.
    /// </summary>
    public class EndUserStatementFactorFederationTests
    {
        private static StatementFactor BuildFactor(IVerbQueries verbQueries = null)
        {
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            return new StatementFactor(
                accessor.Object,
                new Mock<IStatementQueries>().Object,
                new Mock<IActorQueries>().Object,
                new Mock<IMemberQueries>().Object,
                new Mock<IObjectQueries>().Object,
                verbQueries ?? new Mock<IVerbQueries>().Object,
                new Mock<IVersionQueries>().Object,
                new Mock<IExtensionsQueries>().Object);
        }

        [Fact]
        public async Task FactorStatement_DoesNotSetVersion_CentralOwnsIt()
        {
            // The tenant is a federated client; xAPI Version is set by central, never here.
            var result = await BuildFactor().FactorStatement(new JObject());

            result.ParsedCorrectly.Should().BeTrue();
            result.Statement.Version.Should().BeNull("the tenant must not set xAPI Version -- central owns it");
        }

        [Fact]
        public async Task SetupVerb_WhenCentralReturnsTheVerb_UsesItInsteadOfFabricatingOne()
        {
            // Central returns a real verb (the tenant fetched it over HTTP). Before the fix, the inverted
            // `verb == null` check treated a found verb as NOT found and fabricated a replacement from input.
            var centralVerbId = new Uri("http://adlnet.gov/expapi/verbs/completed");
            var verbQueries = new Mock<IVerbQueries>();
            verbQueries.Setup(v => v.Get(It.IsAny<long>()))
                .ReturnsAsync(new XM.Verb { Id = centralVerbId });

            var input = JObject.Parse("{ \"verb\": { \"key\": 7 } }");
            var result = await BuildFactor(verbQueries.Object).FactorStatement(input);

            result.ParsedCorrectly.Should().BeTrue();
            result.Statement.Verb.Should().NotBeNull();
            result.Statement.Verb.Id.Should().Be(centralVerbId,
                "a verb that central returns must be used, not discarded and rebuilt from the raw input");
        }
    }
}
