// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.UserModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Febris.UserNode.Portal.IdentityPolicy;

namespace Febris.UserNode.Portal.Areas.Identity.Pages.Account.Manage
{
    public class DownloadPersonalDataModel : PageModel
    {
        private readonly UserManager<LocalApplicationUser> _userManager;
        private readonly ILogger<DownloadPersonalDataModel> _logger;
        private readonly bool _allowExport;

        public DownloadPersonalDataModel(
            UserManager<LocalApplicationUser> userManager,
            ILogger<DownloadPersonalDataModel> logger,
            IOptions<IdentityPolicyOptions> identityPolicy)
        {
            _userManager = userManager;
            _logger = logger;
            _allowExport = identityPolicy?.Value?.AccountLifecycle?.AllowPersonalDataExport ?? false;
        }

        [EnforcesGate("AccountLifecycle.AllowPersonalDataExport")]
        public async Task<IActionResult> OnPostAsync()
        {
            // IDENTITY_POLICY_GATES: operators can disable personal-data export.
            if (!_allowExport) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            _logger.LogInformation("User with ID '{UserId}' asked for their personal data.", _userManager.GetUserId(User));

            // Only include personal data for download
            var personalData = new Dictionary<string, string>();
            var personalDataProps = typeof(LocalApplicationUser).GetProperties().Where(
                            prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
            foreach (var p in personalDataProps)
            {
                personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
            }

            var logins = await _userManager.GetLoginsAsync(user);
            foreach (var l in logins)
            {
                personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
            }

            Response.Headers.Add("Content-Disposition", "attachment; filename=PersonalData.json");
            return new FileContentResult(JsonSerializer.SerializeToUtf8Bytes(personalData), "application/json");
        }
    }
}
