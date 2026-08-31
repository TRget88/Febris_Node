// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System.Threading.Tasks;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.LogicLayer.Logic.HealthLogic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Febris.UserNode.Portal.Controllers
{
    /// <summary>
    /// Node health site (sub-slice 2): the operator's status page. Renders the
    /// <see cref="INodeStatusLogic"/> snapshot -- overall + per-component health, node identity,
    /// node + installed client-software package versions, artifact-store disk usage, and the
    /// hub-federation gate state. Same auth shape as the other node-admin surfaces
    /// (<c>HubFederationController</c>): signed-in Portal cookie Identity, org-admin roles.
    /// Machine probes do NOT use this page -- they hit the anonymous <c>/health/live</c> +
    /// <c>/health/ready</c> endpoints; this page is the human, secrets-off view of the same
    /// checks.
    /// </summary>
    [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
    public class NodeStatusController : Controller
    {
        private readonly INodeStatusLogic _nodeStatusContext;

        /// <summary>DI constructor (the only one).</summary>
        public NodeStatusController(INodeStatusLogic nodeStatusContext)
        {
            _nodeStatusContext = nodeStatusContext;
        }

        // GET: /NodeStatus
        /// <summary>Take a live snapshot (runs every registered health check) and render it.</summary>
        public async Task<IActionResult> Index()
        {
            NodeStatusViewModel model = await _nodeStatusContext.GetStatus();
            return View(model);
        }
    }
}
