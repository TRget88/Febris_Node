// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading.Tasks;
using Febris.UserNode.Portal.IdentityPolicy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// AccountLifecycle.PurgeAfterDays enforcement (<see cref="SoftDeletedUserPurger"/>): the retention
    /// cutoff (fails SAFE -- null/non-positive =&gt; no purge) and the disabled fast-path (returns 0 without
    /// touching the store). The actual expired-row deletion query is covered by the adversarial review (the
    /// ApplicationDbContext static-config initializer blocks constructing an in-memory DbContext here).
    /// </summary>
    public class PurgeAfterDaysTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-30)]
        public void Cutoff_DisabledOrNonPositive_IsNull_FailsSafe(int? days)
        {
            SoftDeletedUserPurger.Cutoff(days, Now).Should().BeNull();
        }

        [Fact]
        public void Cutoff_Positive_IsNowMinusDays()
        {
            SoftDeletedUserPurger.Cutoff(90, Now).Should().Be(Now.AddDays(-90));
        }

        [Fact]
        public async Task PurgeExpiredAsync_WhenDisabled_ReturnsZero_WithoutTouchingTheStore()
        {
            // PurgeAfterDays null -> the method returns before using the context, so a null context is safe
            // (and avoids constructing ApplicationDbContext, whose static config init throws in-test).
            IOptions<IdentityPolicyOptions> options = Microsoft.Extensions.Options.Options.Create(
                new IdentityPolicyOptions { AccountLifecycle = new AccountLifecycleOptions { PurgeAfterDays = null } });
            var purger = new SoftDeletedUserPurger(null, options, NullLogger<SoftDeletedUserPurger>.Instance, ActorsMock());

            int purged = await purger.PurgeExpiredAsync(Now);

            purged.Should().Be(0);
        }

        /// <summary>
        /// The purger pseudonymises each account's xAPI actor before the rows go. Actors are never
        /// deleted: the statement FK cascades, so deleting one would take the learner's whole record.
        /// </summary>
        private static Febris.PrimaryLogicLayer.Logic.XApiLogic.IActorLogic ActorsMock()
        {
            var actors = new Mock<Febris.PrimaryLogicLayer.Logic.XApiLogic.IActorLogic>();
            actors.Setup(a => a.Pseudonymise(It.IsAny<System.Guid>())).ReturnsAsync(true);
            return actors.Object;
        }

    }
}
