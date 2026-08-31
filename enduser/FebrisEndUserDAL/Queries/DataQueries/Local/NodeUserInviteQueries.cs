// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    /// <summary>
    /// Store surface for node account invitations (<see cref="NodeUserInvite"/>).
    ///
    /// <para>
    /// The lookup is BY TOKEN HASH, which is why the hash has to be deterministic: there is no
    /// other handle. Nothing here ever accepts or returns a raw token.
    /// </para>
    /// </summary>
    public interface INodeUserInviteQueries
    {
        /// <summary>Every invitation, newest first, for the admin list.</summary>
        Task<List<NodeUserInvite>> Get();

        /// <summary>The invitation whose token hashes to <paramref name="tokenHash"/>, or null.
        /// Returns the row in ANY state (consumed, revoked, expired) so the caller can tell the
        /// invitee which of those it is rather than a flat "not found".</summary>
        Task<NodeUserInvite> GetByTokenHash(string tokenHash);

        /// <summary>One invitation by its UUID, for admin actions such as revoke.</summary>
        Task<NodeUserInvite> GetByUuid(Guid uuid);

        /// <summary>
        /// Outstanding invitations for an address: not consumed, not revoked, not yet expired.
        /// Used to stop an operator stacking duplicate invitations on one person, and to warn
        /// before registration is closed under people who hold one.
        /// </summary>
        Task<List<NodeUserInvite>> GetActiveFor(string email);

        /// <summary>Count of invitations that are currently redeemable.</summary>
        Task<int> CountActive();

        /// <summary>Persist a new invitation. The caller supplies an already-hashed token.</summary>
        Task<NodeUserInvite> Create(NodeUserInvite input);

        /// <summary>
        /// ATOMIC single-use consume: marks the invitation consumed ONLY if it is still
        /// unconsumed, unrevoked and unexpired, and reports whether this call was the one that
        /// won. Returns false when another request got there first.
        ///
        /// <para>
        /// This is the whole reason the method exists instead of a read-then-write in the logic
        /// layer. Two simultaneous redemptions of one link would both read "unconsumed", both
        /// create an account, and the second would fail on the duplicate email after the first had
        /// already committed. The check and the write have to be one statement.
        /// </para>
        /// </summary>
        Task<bool> MarkConsumed(Guid uuid, Guid consumedByUserId, DateTime consumedAtUtc);

        /// <summary>Mark an outstanding invitation revoked. Returns false when it was already
        /// consumed or revoked, so a double-click cannot overwrite who revoked it or when.</summary>
        Task<bool> MarkRevoked(Guid uuid, string revokedByEmail, DateTime revokedAtUtc);
    }

    /// <summary>
    /// DI-only implementation over the tenant's own <see cref="DataDbContext"/>: greenfield node
    /// code, so deliberately NO legacy self-newing constructor. Swept into DI by the
    /// <c>AddFebrisUserNodeDataAccess</c> naming convention.
    /// </summary>
    public class NodeUserInviteQueries : INodeUserInviteQueries
    {
        private readonly DataDbContext _dataDbContext;

        /// <summary>DI constructor (the only one).</summary>
        public NodeUserInviteQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }

        /// <inheritdoc />
        public async Task<List<NodeUserInvite>> Get()
        {
            try
            {
                return await _dataDbContext.NodeUserInvite
                    .AsNoTracking()
                    .OrderByDescending(i => i.Id)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<NodeUserInvite> GetByTokenHash(string tokenHash)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tokenHash))
                {
                    // An empty hash must never match a row. Guarded here rather than trusted to the
                    // caller, because "" is what a missing query parameter hashes down to if
                    // anybody ever forgets a null check upstream.
                    return null;
                }

                return await _dataDbContext.NodeUserInvite
                    .AsNoTracking()
                    .OrderBy(i => i.Id)
                    .FirstOrDefaultAsync(i => i.TokenHash == tokenHash);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<NodeUserInvite> GetByUuid(Guid uuid)
        {
            try
            {
                return await _dataDbContext.NodeUserInvite
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.UUID == uuid);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<NodeUserInvite>> GetActiveFor(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return new List<NodeUserInvite>();
                }

                string trimmed = email.Trim();
                DateTime now = DateTime.UtcNow;
                return await _dataDbContext.NodeUserInvite
                    .AsNoTracking()
                    .Where(i => i.ConsumedAt == null
                             && i.RevokedAt == null
                             && i.ExpiresAt > now
                             && i.Email.ToLower() == trimmed.ToLower())
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> CountActive()
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                return await _dataDbContext.NodeUserInvite
                    .AsNoTracking()
                    .CountAsync(i => i.ConsumedAt == null && i.RevokedAt == null && i.ExpiresAt > now);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<NodeUserInvite> Create(NodeUserInvite input)
        {
            try
            {
                if (input == null)
                {
                    throw new ArgumentNullException(nameof(input));
                }

                // Explicit UUID (rather than the column default) so the row is complete on
                // provider-neutral stores (InMemory has no uuid_generate_v4()), and because the
                // UUID is the admin-side handle for revoke and must exist before SaveChanges.
                if (input.UUID == Guid.Empty)
                {
                    input.UUID = Guid.NewGuid();
                }

                _dataDbContext.NodeUserInvite.Add(input);
                await _dataDbContext.SaveChangesAsync();
                return input;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> MarkConsumed(Guid uuid, Guid consumedByUserId, DateTime consumedAtUtc)
        {
            try
            {
                // TRACKED read (no AsNoTracking) because this one writes, and the where clause
                // repeats every redeemability condition rather than trusting the caller's earlier
                // read -- that earlier read is exactly the window a second request slips through.
                NodeUserInvite invite = await _dataDbContext.NodeUserInvite
                    .FirstOrDefaultAsync(i => i.UUID == uuid
                                           && i.ConsumedAt == null
                                           && i.RevokedAt == null
                                           && i.ExpiresAt > consumedAtUtc);
                if (invite == null)
                {
                    return false;
                }

                invite.ConsumedAt = consumedAtUtc;
                invite.ConsumedByUserId = consumedByUserId;
                await _dataDbContext.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Another request consumed the row between the read and the write. Losing that race
                // is a legitimate outcome, not a fault: report "you did not win" so the caller
                // rolls back rather than proceeding with an account it did not earn.
                Febris.SharedServices.FebrisLog.Warn(
                    "[invitation] concurrent consume lost the race for " + uuid + ": " + ex.GetType().Name);
                return false;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> MarkRevoked(Guid uuid, string revokedByEmail, DateTime revokedAtUtc)
        {
            try
            {
                NodeUserInvite invite = await _dataDbContext.NodeUserInvite
                    .FirstOrDefaultAsync(i => i.UUID == uuid
                                           && i.ConsumedAt == null
                                           && i.RevokedAt == null);
                if (invite == null)
                {
                    return false;
                }

                invite.RevokedAt = revokedAtUtc;
                invite.RevokedByEmail = string.IsNullOrWhiteSpace(revokedByEmail) ? null : revokedByEmail.Trim();
                await _dataDbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
    }
}
