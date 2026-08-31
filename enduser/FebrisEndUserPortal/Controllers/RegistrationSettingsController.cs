// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.LogicLayer.Logic.IdentityLogic;
using Febris.UserNode.Portal.IdentityPolicy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Febris.UserNode.Portal.Controllers
{
    /// <summary>
    /// The operator's Registration page (node initialization design 2026-08-18). Same auth shape as
    /// the other node-admin surfaces (<c>HubFederationController</c> / <c>NodeStatusController</c>):
    /// signed-in portal cookie identity, org-admin roles, anti-forgery on every state-changing post.
    ///
    /// <para>
    /// The page exists because initialization was never the missing piece -- the seeded bootstrap
    /// admin already covers it -- but the registration MODE could only be changed by editing
    /// appsettings.json inside a container and restarting. This is the turn on that policy.
    /// </para>
    ///
    /// <para>
    /// Mode VALIDATION lives here rather than in the logic layer, because the registration-mode
    /// enum is a portal type: the logic layer deals in names and would have to duplicate the enum
    /// to check one. An unrecognized post is rejected and nothing is written.
    /// </para>
    /// </summary>
    [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
    public class RegistrationSettingsController : Controller
    {
        private readonly INodeRegistrationSettingsLogic _settingsContext;
        private readonly IRegistrationPolicy _policy;
        private readonly IRegistrationPolicyCache _policyCache;
        private readonly IOptions<IdentityPolicyOptions> _identityPolicy;

        /// <summary>DI constructor (the only one).</summary>
        public RegistrationSettingsController(
            INodeRegistrationSettingsLogic settingsContext,
            IRegistrationPolicy policy,
            IRegistrationPolicyCache policyCache,
            IOptions<IdentityPolicyOptions> identityPolicy)
        {
            _settingsContext = settingsContext;
            _policy = policy;
            _policyCache = policyCache;
            _identityPolicy = identityPolicy;
        }

        // GET: /RegistrationSettings
        /// <summary>Render the stored policy, the configured fallback, and the EFFECTIVE posture.</summary>
        public async Task<IActionResult> Index()
        {
            return View(await BuildModel());
        }

        // POST: /RegistrationSettings/Save
        /// <summary>Validate the mode, persist the policy, drop this host's cached snapshot so the
        /// change applies immediately, and re-render.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(RegistrationSettingsInputModel input)
        {
            RegistrationMode mode;
            if (input == null || !NodeRegistrationPolicyResolver.TryParseModeName(input.Mode, out mode))
            {
                // Name-only parsing, the same helper the resolver uses, so a value the page accepts
                // can never be one the resolver would later refuse to read. It also rejects a posted
                // ordinal, which Enum.TryParse plus Enum.IsDefined would have accepted.
                ModelState.AddModelError(string.Empty, "Select a valid registration mode.");
                return View("Index", await BuildModel());
            }

            await _settingsContext.Save(input, mode.ToString(), ActorEmail());

            // The stored row now governs; drop the snapshot so the very next consultation on this
            // host sees it rather than waiting out the resolver's TTL.
            _policyCache?.Invalidate();

            ViewBag.SaveMessage = "Registration policy saved.";
            return View("Index", await BuildModel());
        }

        // POST: /RegistrationSettings/ResetToConfigured
        /// <summary>
        /// Clear the stored policy so the configured <c>Identity:Registration</c> section governs
        /// again. Implemented as a SAVE of the configured mode rather than a row delete: it leaves
        /// the audit trail (who reset it, and when) intact, and a delete would make the page's
        /// "stored versus configured" indicator lie about a node that had in fact been changed.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetToConfigured()
        {
            RegistrationOptions configured = Configured();
            await _settingsContext.Save(
                new RegistrationSettingsInputModel()
                {
                    Mode = configured.Mode.ToString(),
                    AllowedEmailDomains = configured.AllowedEmailDomains == null
                        ? null
                        : string.Join(",", configured.AllowedEmailDomains),
                    RequireAdminApproval = configured.RequireAdminApproval,
                    AutoProvisionJit = configured.AutoProvisionJit,
                    OpenForHours = null
                },
                configured.Mode.ToString(),
                ActorEmail());

            _policyCache?.Invalidate();

            ViewBag.SaveMessage = "Registration policy reset to the deployment configuration.";
            return View("Index", await BuildModel());
        }

        /// <summary>Every selectable mode, paired with the plain-language description the page
        /// shows next to it. Ordered least-open first so the safe choice reads first.</summary>
        public static IReadOnlyList<KeyValuePair<RegistrationMode, string>> SelectableModes()
        {
            return new List<KeyValuePair<RegistrationMode, string>>
            {
                new KeyValuePair<RegistrationMode, string>(RegistrationMode.AdminOnly,
                    "Only an administrator can create accounts. There is no sign-up form."),
                new KeyValuePair<RegistrationMode, string>(RegistrationMode.Invite,
                    "No self sign-up, and the sign-in page offers a way to redeem an invitation code. Invitations themselves work in every mode -- see Users, Invitations -- so this setting is about how people arrive, not about whether invitations are accepted."),
                new KeyValuePair<RegistrationMode, string>(RegistrationMode.DomainAllowlist,
                    "Anyone with an email address on the allowed-domain list can sign themselves up."),
                new KeyValuePair<RegistrationMode, string>(RegistrationMode.Open,
                    "Anyone who can reach this node can sign themselves up. Pair it with an auto-close window.")
            };
        }

        /// <summary>The configured registration section, i.e. what governs with nothing stored.</summary>
        private RegistrationOptions Configured()
        {
            return _identityPolicy?.Value?.Registration ?? new RegistrationOptions();
        }

        /// <summary>The signed-in admin's email, for the row's audit stamp. Null rather than a
        /// placeholder when no email claim is present -- an unrecorded actor is honest, an invented
        /// one is not.</summary>
        private string ActorEmail()
        {
            string email = User?.FindFirst(ClaimTypes.Email)?.Value;
            if (!string.IsNullOrWhiteSpace(email))
            {
                return email;
            }
            return string.IsNullOrWhiteSpace(User?.Identity?.Name) ? null : User.Identity.Name;
        }

        /// <summary>
        /// Compose the page model from the three sources it has to reconcile: what is STORED, what
        /// is CONFIGURED, and what the resolver is EFFECTIVELY serving right now. Showing all three
        /// is deliberate -- the effective mode is the only one that governs, and it can differ from
        /// the stored one when an open window has elapsed or a stored value failed to parse.
        /// </summary>
        private async Task<RegistrationSettingsViewModel> BuildModel()
        {
            RegistrationOptions configured = Configured();

            StoredRegistrationPolicy stored;
            try
            {
                stored = await _settingsContext.GetStored();
            }
            catch (Exception ex)
            {
                // The resolver has already failed this closed; the page must SAY so rather than
                // render a blank form that looks like a node with no policy saved.
                Febris.SharedServices.FebrisLog.Error(ex,
                    "[registration-policy] admin page could not read the stored policy");
                ModelState.AddModelError(string.Empty,
                    "The stored registration policy could not be read, so registration is closed (admin only) until it can. The values below are the deployment configuration.");
                stored = new StoredRegistrationPolicy() { HasStoredSettings = false };
            }

            bool expired = stored.HasStoredSettings
                && stored.OpenUntilUtc.HasValue
                && stored.OpenUntilUtc.Value <= DateTime.UtcNow;

            return new RegistrationSettingsViewModel()
            {
                HasStoredSettings = stored.HasStoredSettings,
                Mode = stored.HasStoredSettings ? stored.Mode : configured.Mode.ToString(),
                AllowedEmailDomains = stored.HasStoredSettings
                    ? stored.AllowedEmailDomains
                    : (configured.AllowedEmailDomains == null ? null : string.Join(",", configured.AllowedEmailDomains)),
                RequireAdminApproval = stored.HasStoredSettings
                    ? stored.RequireAdminApproval
                    : configured.RequireAdminApproval,
                AutoProvisionJit = stored.HasStoredSettings
                    ? stored.AutoProvisionJit
                    : configured.AutoProvisionJit,
                OpenUntilUtc = stored.OpenUntilUtc,
                OpenWindowExpired = expired,
                EffectiveMode = _policy.Mode.ToString(),
                EffectiveSelfRegistrationEnabled = _policy.SelfRegistrationEnabled,
                ConfiguredMode = configured.Mode.ToString(),
                RequireConfirmedEmailConfigured = configured.RequireConfirmedEmail,
                UpdatedAtUtc = stored.UpdatedAtUtc,
                UpdatedByEmail = stored.UpdatedByEmail
            };
        }
    }
}
