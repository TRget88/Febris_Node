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
    /// Store surface for the node's single <see cref="NodeRegistrationConfig"/> row -- the
    /// operator's stored registration policy (node initialization design 2026-08-18).
    /// Single-row semantics like <see cref="INodeIdentityQueries"/> and
    /// <see cref="IHubFederationConfigQueries"/>: first row wins, absence means "never saved".
    /// <para>
    /// Nothing here is encrypted, unlike the federation store: a registration mode is policy, not
    /// a credential, and the admin screen displays every value it holds.
    /// </para>
    /// </summary>
    public interface INodeRegistrationConfigQueries
    {
        /// <summary>The single stored row, or null when the operator never saved one (in which
        /// case the configured <c>Identity:Registration</c> section still governs).</summary>
        Task<NodeRegistrationConfig> Get();

        /// <summary>
        /// Create-or-update the single row (first row wins). Stamps <c>UpdatedAt</c> (UTC).
        /// Returns the persisted row.
        /// </summary>
        Task<NodeRegistrationConfig> Save(NodeRegistrationConfig input);
    }

    /// <summary>
    /// DI-only implementation of <see cref="INodeRegistrationConfigQueries"/> over the tenant's own
    /// <see cref="DataDbContext"/>: greenfield node code, so deliberately NO legacy self-newing
    /// constructor. Swept into DI by the <c>AddFebrisUserNodeDataAccess</c> naming convention
    /// (<c>IXxxQueries</c> -> <c>XxxQueries</c>).
    /// </summary>
    public class NodeRegistrationConfigQueries : INodeRegistrationConfigQueries
    {
        private readonly DataDbContext _dataDbContext;

        /// <summary>DI constructor (the only one).</summary>
        public NodeRegistrationConfigQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }

        /// <inheritdoc />
        public async Task<NodeRegistrationConfig> Get()
        {
            try
            {
                // Deterministic pick (lowest Id) in the never-expected case of multiple rows, so
                // the node's registration posture cannot flap between reads.
                return await _dataDbContext.NodeRegistrationConfig
                    .AsNoTracking()
                    .OrderBy(r => r.Id)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                // Rethrow after logging: the CALLER decides the failure posture, and for this store
                // the caller's answer is to fail CLOSED. Swallowing here would hand it a null that
                // is indistinguishable from "never configured", which resolves to the CONFIGURED
                // mode -- and on a node configured Open that would be a fail-open on a DB blip.
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<NodeRegistrationConfig> Save(NodeRegistrationConfig input)
        {
            try
            {
                if (input == null)
                {
                    throw new ArgumentNullException(nameof(input));
                }

                NodeRegistrationConfig existing = await _dataDbContext.NodeRegistrationConfig
                    .OrderBy(r => r.Id)
                    .FirstOrDefaultAsync();
                if (existing == null)
                {
                    existing = new NodeRegistrationConfig()
                    {
                        // Explicit UUID (rather than the column default) so the row is complete on
                        // provider-neutral stores (InMemory has no uuid_generate_v4()).
                        UUID = Guid.NewGuid()
                    };
                    _dataDbContext.NodeRegistrationConfig.Add(existing);
                }

                existing.Mode = input.Mode;
                existing.AllowedEmailDomains = input.AllowedEmailDomains;
                existing.RequireAdminApproval = input.RequireAdminApproval;
                existing.AutoProvisionJit = input.AutoProvisionJit;
                existing.OpenUntilUtc = input.OpenUntilUtc;
                existing.UpdatedByEmail = input.UpdatedByEmail;
                existing.UpdatedAt = DateTime.UtcNow;
                await _dataDbContext.SaveChangesAsync();

                return existing;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
    }
}
