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

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// LMS-B1 regression guard (EndUser twin). FactorStatement built the terminal Statement with a hard
    /// <c>(DateTime)statementToken["timestamp"]</c> cast of the OPTIONAL xAPI timestamp. When a producer
    /// omitted timestamp, that cast threw and FactorStatement's own catch swallowed it, returning
    /// <c>(null, false)</c> -- the whole learning record was silently DROPPED. The shared StatementFactor
    /// twin was fixed; this pins the EndUser twin, which the reconciliation sweep found still hard-casting.
    /// An empty statement JObject is enough: every SetupX returns null for its absent token, so the only
    /// code path exercised is the timestamp read.
    /// </summary>
    public class EndUserStatementFactorTimestampTests
    {
        private static StatementFactor BuildFactor()
        {
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            // Every query is a default mock: an empty input JObject means no lookup is ever invoked.
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

        [Fact]
        public async Task FactorStatement_WithNoTimestamp_ParsesInsteadOfSilentlyDropping()
        {
            var result = await BuildFactor().FactorStatement(new JObject());

            result.ParsedCorrectly.Should().BeTrue(
                "a statement omitting the optional xAPI timestamp must still factor (LMS-B1); before the fix the (DateTime) cast threw and the record was silently dropped");
            result.Statement.Should().NotBeNull();
            result.Statement.Timestamp.Should().Be(default(DateTime));
        }
    }
}
