// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    /// <summary>
    /// Read access to the node's LOCAL single-tenant identity (auth
    /// severance). One row per deployment, seeded by <see cref="NodeIdentitySeeder"/>; this is
    /// the no-hub answer for every read that historically derived the institution identity from
    /// the scheme-B License claim.
    /// </summary>
    public interface INodeIdentityQueries
    {
        /// <summary>The node's identity row, or null on an unprovisioned store.</summary>
        Task<NodeIdentity> Get();
    }

    /// <summary>
    /// DI-only implementation over the tenant's own <see cref="DataDbContext"/>.
    /// Greenfield node code: deliberately NO legacy self-newing constructor -- resolved
    /// through <c>AddFebrisUserNodeDataAccess</c>'s convention sweep.
    /// </summary>
    public class NodeIdentityQueries : INodeIdentityQueries
    {
        private readonly DataDbContext _context;

        public NodeIdentityQueries(DataDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<NodeIdentity> Get()
        {
            // Deterministic pick (lowest Id) in the never-expected case of multiple rows, so the
            // node's identity cannot flap between reads.
            return await _context.NodeIdentity.OrderBy(i => i.Id).FirstOrDefaultAsync();
        }
    }
}
