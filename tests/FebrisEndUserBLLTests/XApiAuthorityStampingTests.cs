// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
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
    /// T2 proposals 1 and 2: the LRS stamps the authority from the submitting credential, and the
    /// submitting device is recorded on the row.
    ///
    /// <para>
    /// THE MODEL. The device is the AUTHORITY, never the actor. In xAPI the actor is who performed
    /// the activity and the authority is who asserts it happened, so a shared classroom device
    /// submitting for thirty learners in sequence is ordinary: one authority, many actors. Making
    /// the device the actor is what would break shared devices, educator-on-behalf submission,
    /// offline batch upload and the launcher, which is why that is NOT what this does.
    /// </para>
    ///
    /// <para>
    /// ATTRIBUTION, NOT CONSTRAINT. Nothing here rejects a statement. The owner ruling of 2026-08-10
    /// rejected binding writes through mutable membership state and that objection stands. Recording
    /// who submitted something is a different act from refusing it, and it is the half that makes a
    /// forged record investigable rather than indistinguishable from a real one.
    /// </para>
    /// </summary>
    public class XApiAuthorityStampingTests
    {
        private static readonly Guid DeviceUuid = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private static StatementFactor BuildFactor(
            Mock<IActorQueries> actors, Hardware hardware)
        {
            DefaultHttpContext http = new DefaultHttpContext();
            if (hardware != null)
            {
                // The same seam the removed _hardware field read from.
                http.Items["Hardware"] = hardware;
            }

            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(http);

            return new StatementFactor(
                accessor.Object,
                new Mock<IStatementQueries>().Object,
                actors.Object,
                new Mock<IMemberQueries>().Object,
                new Mock<IObjectQueries>().Object,
                new Mock<IVerbQueries>().Object,
                new Mock<IVersionQueries>().Object,
                new Mock<IExtensionsQueries>().Object);
        }

        /// <summary>Learner actor resolves; the device actor is absent until created.</summary>
        private static Mock<IActorQueries> Actors(XM.Actor existingDeviceActor = null)
        {
            Mock<IActorQueries> actors = new Mock<IActorQueries>();
            actors.Setup(a => a.GetByMbox(It.IsAny<Uri>()))
                .ReturnsAsync(new XM.Actor { Id = 1, UUID = Guid.NewGuid(), Name = "Learner" });
            actors.Setup(a => a.Get(It.IsAny<Guid>())).ReturnsAsync(existingDeviceActor);
            actors.Setup(a => a.Create(It.IsAny<XM.Actor>()))
                .ReturnsAsync((XM.Actor a) => { a.Id = 99; return a; });
            return actors;
        }

        private static JObject Statement() => JObject.Parse(@"{
            ""actor"": { ""objectType"": ""Agent"", ""name"": ""Learner"", ""mbox"": ""mailto:learner@example.com"" },
            ""verb"": { ""id"": ""http://adlnet.gov/expapi/verbs/completed"" },
            ""object"": { ""objectType"": ""Activity"", ""id"": ""http://example.com/activities/t2"" },
            ""authority"": { ""objectType"": ""Agent"", ""name"": ""Principal"", ""mbox"": ""mailto:principal@school.edu"", ""uuid"": ""3f2504e0-4f89-11d3-9a0c-0305e82c3301"" }
        }");

        [Fact]
        public async Task TheAuthorityIsStampedFromTheSubmittingDevice()
        {
            Mock<IActorQueries> actors = Actors();
            StatementFactor factor = BuildFactor(actors, new Hardware { UUID = DeviceUuid });

            (XM.Statement Statement, bool ParsedCorrectly) result = await factor.FactorStatement(Statement());

            result.Statement.Authority.Should().NotBeNull("an LRS states who vouched for a record");
            result.Statement.Authority.Actor.UUID.Should().Be(DeviceUuid,
                "the authority is the submitting DEVICE, not whoever the client named");
        }

        [Fact]
        public async Task TheClientNamedAuthorityIsStillIgnored()
        {
            // The stamp must REPLACE the client's value, not merely coexist with it. The statement
            // above names a principal, and that must never survive.
            Mock<IActorQueries> actors = Actors();
            StatementFactor factor = BuildFactor(actors, new Hardware { UUID = DeviceUuid });

            (XM.Statement Statement, bool ParsedCorrectly) result = await factor.FactorStatement(Statement());

            result.Statement.Authority.Actor.Name.Should().NotBe("Principal");
            actors.Verify(a => a.GetByMbox(It.Is<Uri>(u => u.ToString().Contains("principal@school.edu"))), Times.Never,
                "the client's authority must never even be resolved");
        }

        [Fact]
        public async Task TheDeviceAgentIsCreatedOnceAndReused()
        {
            // Deterministic UUID means the row self-deduplicates: one Actor per device, created on
            // its first statement. Without this a busy node would mint a device agent per request.
            XM.Actor existing = new XM.Actor { Id = 42, UUID = DeviceUuid, ObjectType = "Agent" };
            Mock<IActorQueries> actors = Actors(existingDeviceActor: existing);
            StatementFactor factor = BuildFactor(actors, new Hardware { UUID = DeviceUuid });

            await factor.FactorStatement(Statement());

            actors.Verify(a => a.Create(It.IsAny<XM.Actor>()), Times.Never,
                "an existing device agent must be reused, not duplicated");
        }

        [Fact]
        public async Task TheDeviceAgentCarriesAnAccountIfi()
        {
            // An xAPI Agent needs exactly one Inverse Functional Identifier. A device has no
            // mailbox, so account is the correct one, and without it the agent is not spec-valid.
            Mock<IActorQueries> actors = Actors();
            StatementFactor factor = BuildFactor(actors, new Hardware { UUID = DeviceUuid });

            await factor.FactorStatement(Statement());

            actors.Verify(a => a.Create(It.Is<XM.Actor>(
                x => x.ObjectType == "Agent"
                     && x.Account != null
                     && x.Account.Name == DeviceUuid.ToString()
                     && x.Account.HomePage != null)), Times.Once);
        }

        [Fact]
        public async Task NoDeviceCredentialMeansNoAuthorityRatherThanAFailure()
        {
            // A Portal-originated statement, a seed or an import genuinely has nothing vouching for
            // it. An absent authority says so honestly, and the statement must still ingest.
            Mock<IActorQueries> actors = Actors();
            StatementFactor factor = BuildFactor(actors, hardware: null);

            (XM.Statement Statement, bool ParsedCorrectly) result = await factor.FactorStatement(Statement());

            result.Statement.Should().NotBeNull("a statement with no device credential must still ingest");
            result.Statement.Authority.Should().BeNull();
            actors.Verify(a => a.Create(It.IsAny<XM.Actor>()), Times.Never);
        }

        [Fact]
        public async Task AnEmptyHardwareUuidIsTreatedAsNoCredential()
        {
            // Guid.Empty is what an unpopulated Hardware looks like. Minting an agent for it would
            // create a single shared "device" that every unattributed statement pointed at, which
            // is worse than no authority because it looks like real attribution.
            Mock<IActorQueries> actors = Actors();
            StatementFactor factor = BuildFactor(actors, new Hardware { UUID = Guid.Empty });

            (XM.Statement Statement, bool ParsedCorrectly) result = await factor.FactorStatement(Statement());

            result.Statement.Authority.Should().BeNull();
            actors.Verify(a => a.Create(It.IsAny<XM.Actor>()), Times.Never);
        }

        [Fact]
        public async Task AFailureToMintTheAuthorityDoesNotLoseTheStatement()
        {
            // Never fail an ingest because the authority could not be minted. A statement with no
            // authority is worse than one with, and far better than a lost learning record.
            Mock<IActorQueries> actors = Actors();
            actors.Setup(a => a.Create(It.IsAny<XM.Actor>())).ThrowsAsync(new InvalidOperationException("db down"));
            StatementFactor factor = BuildFactor(actors, new Hardware { UUID = DeviceUuid });

            (XM.Statement Statement, bool ParsedCorrectly) result = await factor.FactorStatement(Statement());

            result.Statement.Should().NotBeNull();
            result.Statement.Actor.Should().NotBeNull();
            result.Statement.Authority.Should().BeNull();
        }
    }
}
