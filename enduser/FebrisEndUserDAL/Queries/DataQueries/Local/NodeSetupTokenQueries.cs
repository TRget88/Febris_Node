// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    /// <summary>
    /// Store surface for the node's first-run setup token (<see cref="NodeSetupToken"/>).
    /// Single-use, expiring, looked up BY HASH like the invitation store, which is why that hash
    /// has to be deterministic.
    /// </summary>
    public interface INodeSetupTokenQueries
    {
        /// <summary>
        /// Mint a fresh token, first discarding any outstanding unconsumed one. There is at most
        /// ONE live token at a time on purpose: every boot of an unclaimed node prints a token, and
        /// leaving the earlier ones live would mean several valid claim secrets in several log
        /// scrollbacks. Consumed rows are KEPT as the audit record of the claim.
        /// </summary>
        Task<NodeSetupToken> Issue(string tokenHash, DateTime expiresAtUtc);

        /// <summary>The token whose hash matches, in ANY state, or null. Returned even when spent
        /// or lapsed so the setup page can say which rather than a flat refusal.</summary>
        Task<NodeSetupToken> GetByTokenHash(string tokenHash);

        /// <summary>Whether a token exists that is unconsumed and unexpired right now.</summary>
        Task<bool> HasLiveToken();

        /// <summary>
        /// ATOMIC single-use claim: marks the token consumed ONLY if it is still unconsumed and
        /// unexpired, and reports whether this call is the one that won. The check and the write are
        /// one statement, because a read-then-write here is a window in which a node could be
        /// claimed twice.
        /// </summary>
        Task<bool> MarkConsumed(Guid uuid, Guid consumedByUserId, string consumedByEmail, DateTime consumedAtUtc);
    }

    /// <summary>
    /// DI-only implementation over the tenant's own <see cref="DataDbContext"/>. Greenfield node
    /// code, so deliberately NO legacy self-newing constructor. Swept into DI by the
    /// <c>AddFebrisUserNodeDataAccess</c> naming convention.
    /// </summary>
    public class NodeSetupTokenQueries : INodeSetupTokenQueries
    {
        private readonly DataDbContext _dataDbContext;

        /// <summary>DI constructor (the only one).</summary>
        public NodeSetupTokenQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }

        /// <inheritdoc />
        public async Task<NodeSetupToken> Issue(string tokenHash, DateTime expiresAtUtc)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tokenHash))
                {
                    throw new ArgumentException("A token hash is required.", nameof(tokenHash));
                }

                // Retire anything still outstanding BEFORE minting, so exactly one token is live.
                var outstanding = await _dataDbContext.NodeSetupToken
                    .Where(t => t.ConsumedAt == null)
                    .ToListAsync();
                if (outstanding.Count > 0)
                {
                    _dataDbContext.NodeSetupToken.RemoveRange(outstanding);
                }

                NodeSetupToken issued = new NodeSetupToken()
                {
                    // Explicit UUID (rather than the column default) so the row is complete on
                    // provider-neutral stores, and because the UUID is the handle the claim uses.
                    UUID = Guid.NewGuid(),
                    TokenHash = tokenHash,
                    ExpiresAt = expiresAtUtc
                };
                _dataDbContext.NodeSetupToken.Add(issued);
                await _dataDbContext.SaveChangesAsync();
                return issued;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<NodeSetupToken> GetByTokenHash(string tokenHash)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tokenHash))
                {
                    // An empty hash must never match a row.
                    return null;
                }

                return await _dataDbContext.NodeSetupToken
                    .AsNoTracking()
                    .OrderBy(t => t.Id)
                    .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> HasLiveToken()
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                return await _dataDbContext.NodeSetupToken
                    .AsNoTracking()
                    .AnyAsync(t => t.ConsumedAt == null && t.ExpiresAt > now);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> MarkConsumed(
            Guid uuid, Guid consumedByUserId, string consumedByEmail, DateTime consumedAtUtc)
        {
            try
            {
                // TRACKED read, and the where clause repeats every redeemability condition rather
                // than trusting the caller's earlier read -- that earlier read is exactly the window
                // a second claim slips through.
                NodeSetupToken token = await _dataDbContext.NodeSetupToken
                    .FirstOrDefaultAsync(t => t.UUID == uuid
                                           && t.ConsumedAt == null
                                           && t.ExpiresAt > consumedAtUtc);
                if (token == null)
                {
                    return false;
                }

                token.ConsumedAt = consumedAtUtc;
                token.ConsumedByUserId = consumedByUserId;
                token.ConsumedByEmail = string.IsNullOrWhiteSpace(consumedByEmail) ? null : consumedByEmail.Trim();
                await _dataDbContext.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Another request claimed the node between the read and the write. Losing that race
                // is a legitimate outcome: report it so the caller rolls back rather than proceeding
                // with an admin account it did not earn.
                Febris.SharedServices.FebrisLog.Warn(
                    "[node-setup] concurrent claim lost the race for " + uuid + ": " + ex.GetType().Name);
                return false;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
    }
}
