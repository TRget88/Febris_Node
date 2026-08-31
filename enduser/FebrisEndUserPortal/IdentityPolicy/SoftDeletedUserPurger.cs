// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Febris.UserNode.Portal.IdentityPolicy
{
    /// <summary>Enforces <c>AccountLifecycle.PurgeAfterDays</c>: hard-delete soft-deleted accounts once retained past the cap.</summary>
    public interface ISoftDeletedUserPurger
    {
        /// <summary>
        /// Hard-delete every soft-deleted account whose <c>DeletedUtc</c> is older than
        /// <c>PurgeAfterDays</c>. Returns the number purged. Fails SAFE: a null or non-positive
        /// PurgeAfterDays purges nothing (accounts are retained indefinitely).
        /// </summary>
        Task<int> PurgeExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    }

    /// <inheritdoc cref="ISoftDeletedUserPurger"/>
    public class SoftDeletedUserPurger : ISoftDeletedUserPurger
    {
        private readonly ApplicationDbContext _context;
        private readonly int? _purgeAfterDays;
        private readonly ILogger<SoftDeletedUserPurger> _logger;
        private readonly Febris.PrimaryLogicLayer.Logic.XApiLogic.IActorLogic _actorLogic;

        public SoftDeletedUserPurger(
            ApplicationDbContext context,
            IOptions<IdentityPolicyOptions> identityPolicy,
            ILogger<SoftDeletedUserPurger> logger,
            Febris.PrimaryLogicLayer.Logic.XApiLogic.IActorLogic actorLogic)
        {
            _context = context;
            _purgeAfterDays = identityPolicy?.Value?.AccountLifecycle?.PurgeAfterDays;
            _logger = logger;
            _actorLogic = actorLogic;
        }

        /// <summary>
        /// The retention cutoff: soft-deleted accounts with <c>DeletedUtc</c> strictly before this are purged.
        /// Returns null when purging is disabled (null or non-positive <paramref name="purgeAfterDays"/>) --
        /// the FAILS-SAFE direction, so a mis-set knob never deletes retained data.
        /// </summary>
        public static DateTimeOffset? Cutoff(int? purgeAfterDays, DateTimeOffset nowUtc)
        {
            if (!purgeAfterDays.HasValue || purgeAfterDays.Value <= 0)
            {
                return null;
            }
            return nowUtc.AddDays(-purgeAfterDays.Value);
        }

        [EnforcesGate("AccountLifecycle.PurgeAfterDays")]
        public async Task<int> PurgeExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            DateTimeOffset? cutoff = Cutoff(_purgeAfterDays, nowUtc);
            if (cutoff == null)
            {
                return 0; // purging disabled -> retain everything (fails safe)
            }

            // Only SOFT-DELETED, expired rows. There is no global query filter, so this sees the retained
            // rows directly; active accounts (IsDeleted == false) can never match.
            var expired = await _context.Users
                .Where(u => u.IsDeleted && u.DeletedUtc != null && u.DeletedUtc < cutoff.Value)
                .ToListAsync(cancellationToken);

            if (expired.Count == 0)
            {
                return 0;
            }

            // BEFORE the rows go, strip the learner identity from each account's xAPI Actor. Once
            // AspNetUsers is deleted the link from user to actor is gone, so this cannot be done
            // afterwards.
            //
            // PSEUDONYMISE, never delete. FK_LocalStatement_Actor_ActorId is ON DELETE CASCADE over a
            // NOT NULL column, so removing an Actor would delete every statement that learner ever
            // produced. Mbox_sha1sum is retained, which keeps the Actor a valid xAPI Agent, since it
            // is a legal Inverse Functional Identifier on its own, and keeps the statements
            // attributable. Best-effort: a purge must not fail because the xAPI database was
            // briefly unreachable, and the next run retries whatever was missed.
            int pseudonymised = 0;
            foreach (var account in expired)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (!account.Actor.HasValue || account.Actor.Value == Guid.Empty) continue;
                try
                {
                    if (await _actorLogic.Pseudonymise(account.Actor.Value)) pseudonymised++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "PurgeAfterDays: failed pseudonymising the xAPI actor for account {UserId}.", account.Id);
                }
            }

            // Hard delete. The AspNetUsers FK cascade removes the dependent Identity rows (roles/claims/
            // logins/tokens) for each purged account.
            _context.Users.RemoveRange(expired);
            await _context.SaveChangesAsync(cancellationToken);

            if (pseudonymised > 0)
            {
                _logger.LogInformation(
                    "PurgeAfterDays: pseudonymised {Count} xAPI actor(s). Their statements are retained.",
                    pseudonymised);
            }

            _logger.LogInformation(
                "PurgeAfterDays: hard-deleted {Count} soft-deleted account(s) retained past {Days} day(s).",
                expired.Count, _purgeAfterDays.Value);
            return expired.Count;
        }
    }
}
