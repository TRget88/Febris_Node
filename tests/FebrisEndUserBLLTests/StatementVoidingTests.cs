// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.Models.XApiModels.ModifiedForSharing;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.UserNode.DataAccessLayer.Queries.XAPIQueries;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// T5: xAPI voiding. A statement that turns out to be wrong is RETRACTED -- never edited, never
    /// deleted -- and stops counting.
    ///
    /// <para>
    /// This restores a feature that existed in the 2021 Portal and did not survive the port into
    /// this repo. Three things that version did are pinned here as things NOT to do: it overwrote
    /// the target's verb with <c>voided</c> (destroying what the learner actually did), wrote the
    /// voiding statement to a JSON file instead of the table, and never excluded voided statements
    /// from any query.
    /// </para>
    ///
    /// <para>
    /// Owner rulings pinned: <b>Admin and up</b> may void, and there is <b>no unvoid</b> -- voiding
    /// is one-way, so a repeat is a no-op and <c>VoidedAt</c> is never cleared.
    /// </para>
    /// </summary>
    public class StatementVoidingTests
    {
        private static readonly Guid TargetUuid = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        private static readonly Guid OperatorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static ClaimsPrincipal Principal(string role, Guid? actor = null)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, role),
                new Claim(ClaimTypes.NameIdentifier, OperatorUserId.ToString()),
            };
            if (actor.HasValue) claims.Add(new Claim("Actor", actor.Value.ToString()));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        private sealed class Harness
        {
            public Mock<IStatementQueries> Statements = new Mock<IStatementQueries>();
            public Mock<IVerbQueries> Verbs = new Mock<IVerbQueries>();
            public Mock<IObjectQueries> Objects = new Mock<IObjectQueries>();
            public Mock<IActorQueries> Actors = new Mock<IActorQueries>();
            public StatementVoidingLogic Logic;
        }

        private static Harness Build(ClaimsPrincipal user, LocalStatement stored, bool verbSeeded = true, Actor adminActor = null)
        {
            Harness h = new Harness();

            DefaultHttpContext http = new DefaultHttpContext();
            if (user != null) http.User = user;
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(http);

            // EXACT-MATCH lookup. A stub that answers It.IsAny regardless of argument cannot test a
            // lookup -- that is how an earlier entitlement defect shipped green.
            h.Statements.Setup(x => x.GetIncludingVoided(It.IsAny<Guid?>()))
                .ReturnsAsync((Guid? asked) => stored != null && asked == stored.UUID ? stored : null);
            h.Statements.Setup(x => x.MarkVoided(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<Guid?>()))
                .ReturnsAsync(true);
            h.Statements.Setup(x => x.Create(It.IsAny<LocalStatement>()))
                .ReturnsAsync((LocalStatement s) => s);

            h.Verbs.Setup(x => x.Get(It.IsAny<Uri>()))
                .ReturnsAsync(verbSeeded ? new Verb { Key = 3, UUID = Guid.NewGuid(), Id = new Uri(StatementVoidingLogic.VoidedVerbId) } : null);

            h.Objects.Setup(x => x.Create(It.IsAny<Febris.ModelLibrary.Models.XApiModels.Object>()))
                .ReturnsAsync((Febris.ModelLibrary.Models.XApiModels.Object o) => { o.Key = 42; return o; });

            h.Actors.Setup(x => x.Get(It.IsAny<Guid>())).ReturnsAsync(adminActor);

            h.Logic = new StatementVoidingLogic(
                accessor.Object, h.Statements.Object, h.Verbs.Object, h.Objects.Object, h.Actors.Object);
            return h;
        }

        private static LocalStatement Target(DateTime? voidedAt = null) => new LocalStatement
        {
            Id = 5,
            UUID = TargetUuid,
            VerbId = 9,
            VerbUUID = Guid.NewGuid(),
            VoidedAt = voidedAt,
        };

        // ------------------------------------------------------------------
        // Authorization: Admin and up
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("Admin")]
        [InlineData("ITAdmin")]
        public async Task AdminAndUpMayVoid(string role)
        {
            Harness h = Build(Principal(role), Target());

            (await h.Logic.Void(TargetUuid)).Should().BeTrue();
            h.Statements.Verify(x => x.MarkVoided(5, It.IsAny<DateTime>(), OperatorUserId), Times.Once);
        }

        [Theory]
        [InlineData("Educator")]
        [InlineData("User")]
        [InlineData("UserParent")]
        public async Task BelowAdminMayNotVoid(string role)
        {
            // THE gate. An educator manages users but does not retract learning records.
            Harness h = Build(Principal(role), Target());

            (await h.Logic.Void(TargetUuid)).Should().BeFalse();
            h.Statements.Verify(x => x.MarkVoided(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task AnUnauthenticatedRequestMayNotVoid()
        {
            Harness h = Build(user: null, stored: Target());

            (await h.Logic.Void(TargetUuid)).Should().BeFalse();
            h.Statements.Verify(x => x.MarkVoided(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<Guid?>()), Times.Never);
        }

        // ------------------------------------------------------------------
        // The target is retracted, never altered
        // ------------------------------------------------------------------

        [Fact]
        public async Task TheTargetsVerbIsNeverOverwritten()
        {
            // THE 2021 defect. It set statement.Verb = voidedVerb, destroying the record of what the
            // learner actually did and leaving its own Unvoid nothing to restore.
            LocalStatement target = Target();
            Guid originalVerb = target.VerbUUID;
            long originalVerbId = target.VerbId;

            Harness h = Build(Principal("Admin"), target);
            await h.Logic.Void(TargetUuid);

            target.VerbUUID.Should().Be(originalVerb, "the target records what the learner did and must not be rewritten");
            target.VerbId.Should().Be(originalVerbId);
        }

        [Fact]
        public async Task AVoidingStatementIsWrittenToTheTableWithTheVoidedVerb()
        {
            // The 2021 version wrote this to a JSON file, so nothing could query or export it.
            Actor adminActor = new Actor { Id = 2, UUID = Guid.NewGuid() };
            Harness h = Build(Principal("Admin", actor: adminActor.UUID), Target(), adminActor: adminActor);

            await h.Logic.Void(TargetUuid);

            h.Statements.Verify(x => x.Create(It.Is<LocalStatement>(s => s.VerbId == 3 && s.ObjectId == 42)), Times.Once);
        }

        [Fact]
        public async Task TheVoidingStatementsObjectReferencesTheTarget()
        {
            Actor adminActor = new Actor { Id = 2, UUID = Guid.NewGuid() };
            Harness h = Build(Principal("Admin", actor: adminActor.UUID), Target(), adminActor: adminActor);

            await h.Logic.Void(TargetUuid);

            h.Objects.Verify(x => x.Create(It.Is<Febris.ModelLibrary.Models.XApiModels.Object>(
                o => o.ObjectType == "StatementRef" && o.Id.ToString() == "urn:uuid:" + TargetUuid)), Times.Once);
        }

        [Fact]
        public async Task AnAdminWithNoActorStillVoidsButRecordsNoVoidingStatement()
        {
            // Attributing the void to the TARGET's actor would assert the learner retracted their
            // own record, which is false. No misleading artifact is better than a wrong one, and the
            // operator stays attributable through VoidedByUserId.
            Harness h = Build(Principal("Admin"), Target(), adminActor: null);

            (await h.Logic.Void(TargetUuid)).Should().BeTrue("the retraction itself must still happen");
            h.Statements.Verify(x => x.MarkVoided(5, It.IsAny<DateTime>(), OperatorUserId), Times.Once);
            h.Statements.Verify(x => x.Create(It.IsAny<LocalStatement>()), Times.Never);
        }

        [Fact]
        public async Task AMissingVoidedVerbDoesNotBlockTheRetraction()
        {
            Harness h = Build(Principal("Admin"), Target(), verbSeeded: false);

            (await h.Logic.Void(TargetUuid)).Should().BeTrue();
            h.Statements.Verify(x => x.MarkVoided(5, It.IsAny<DateTime>(), OperatorUserId), Times.Once);
        }

        // ------------------------------------------------------------------
        // One-way
        // ------------------------------------------------------------------

        [Fact]
        public async Task VoidingAnAlreadyVoidedStatementIsANoOp()
        {
            // Owner ruling: no unvoid, so a repeat must not re-stamp the marker or write a second
            // voiding statement.
            Harness h = Build(Principal("Admin"), Target(voidedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

            (await h.Logic.Void(TargetUuid)).Should().BeFalse();
            h.Statements.Verify(x => x.MarkVoided(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<Guid?>()), Times.Never);
            h.Statements.Verify(x => x.Create(It.IsAny<LocalStatement>()), Times.Never);
        }

        [Fact]
        public async Task AnUnknownStatementIsRefused()
        {
            Harness h = Build(Principal("Admin"), Target());

            (await h.Logic.Void(Guid.Parse("99999999-9999-9999-9999-999999999999"))).Should().BeFalse();
            h.Statements.Verify(x => x.MarkVoided(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task TheLookupMustSeeVoidedStatements()
        {
            // The global query filter hides voided rows from every ordinary read. If this used the
            // filtered lookup, a second void would report "no such statement" instead of the no-op
            // it is, and an admin would have no way to tell the two apart.
            Harness h = Build(Principal("Admin"), Target());

            await h.Logic.Void(TargetUuid);

            h.Statements.Verify(x => x.GetIncludingVoided(TargetUuid), Times.Once);
            h.Statements.Verify(x => x.Get(It.IsAny<Guid?>()), Times.Never, "the filtered read would hide an already-voided target");
        }
    }
}
