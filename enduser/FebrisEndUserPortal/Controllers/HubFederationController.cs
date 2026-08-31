// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System.Threading.Tasks;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.LogicLayer.Logic.FederationLogic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Febris.UserNode.Portal.Controllers
{
    /// <summary>
    /// The operator's Hub Federation page (owner-ratified 2026-07-17: the OPERATOR
    /// owns federation -- opting in happens here, on the node portal, and the license key is a
    /// marketplace membership credential, never an operate requirement). Same auth shape as the
    /// other node-admin surfaces (<c>NodeStatusController</c>):
    /// signed-in Portal cookie Identity, org-admin roles; anti-forgery on every state-changing
    /// post per this portal's convention.
    ///
    /// <para>
    /// The license key field is WRITE-ONLY: the page shows only the masked form (last four
    /// characters) and never round-trips the stored key. Test Connection runs the same
    /// gate-aware reachability probe as the readiness endpoint, against the SAVED settings.
    /// "Sync now" (rendered gate-open only) runs one PULL-ONLY hub enrichment pass and shows
    /// its per-domain summary inline.
    /// </para>
    /// </summary>
    [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
    public class HubFederationController : Controller
    {
        private readonly IHubFederationSettingsLogic _settingsContext;
        private readonly IHubSyncLogic _syncContext;

        /// <summary>DI constructor (the only one).</summary>
        public HubFederationController(IHubFederationSettingsLogic settingsContext, IHubSyncLogic syncContext)
        {
            _settingsContext = settingsContext;
            _syncContext = syncContext;
        }

        // GET: /HubFederation
        /// <summary>Render the stored settings (masked key) + effective gate state.</summary>
        public async Task<IActionResult> Index()
        {
            HubFederationSettingsViewModel model = await _settingsContext.GetSettings();
            return View(model);
        }

        // POST: /HubFederation/Save
        /// <summary>Persist the operator's settings (write-only key semantics) and re-render.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(HubFederationSettingsInputModel input)
        {
            HubFederationSettingsViewModel model = await _settingsContext.SaveSettings(input);
            ViewBag.SaveMessage = "Settings saved.";
            return View("Index", model);
        }

        // POST: /HubFederation/TestConnection
        /// <summary>Probe the hub against the SAVED settings; show the result inline.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestConnection()
        {
            HubProbeResultViewModel probe = await _settingsContext.TestConnection();
            HubFederationSettingsViewModel model = await _settingsContext.GetSettings();
            ViewBag.ProbeResult = probe;
            return View("Index", model);
        }

        // POST: /HubFederation/SyncNow
        /// <summary>Run one hub-pull sync pass (a quiet no-op summary when the gate is closed)
        /// and show the per-domain added/updated/failed counts inline.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncNow()
        {
            HubSyncSummaryViewModel summary = await _syncContext.SyncNow();
            HubFederationSettingsViewModel model = await _settingsContext.GetSettings();
            ViewBag.SyncSummary = summary;
            return View("Index", model);
        }
    }
}
