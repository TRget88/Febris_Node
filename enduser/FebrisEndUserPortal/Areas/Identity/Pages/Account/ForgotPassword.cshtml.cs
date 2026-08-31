// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Febris.ModelLibrary.Models.UserModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Febris.SharedServices;
using Microsoft.Extensions.Options;
using Febris.UserNode.Portal.IdentityPolicy;

namespace Febris.UserNode.Portal.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<LocalApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly bool _allowReset;

        public ForgotPasswordModel(
            UserManager<LocalApplicationUser> userManager,
            IEmailSender emailSender,
            IOptions<IdentityPolicyOptions> identityPolicy)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _allowReset = identityPolicy?.Value?.Login?.AllowSelfServiceReset ?? false;
        }

        public IActionResult OnGet()
        {
            // IDENTITY_POLICY_GATES: operators can disable self-service password reset.
            if (!_allowReset) return RedirectToPage("./Login");
            return Page();
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        [EnforcesGate("Login.AllowSelfServiceReset")]
        public async Task<IActionResult> OnPostAsync()
        {
            if (!_allowReset) return RedirectToPage("./Login");
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null || user.IsDeleted || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }

                // For more information on how to enable account confirmation and password reset please 
                // visit https://go.microsoft.com/fwlink/?LinkID=532713
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code },
                    protocol: Request.Scheme);


                // A mail-send failure must NOT change the response. IEmailSender rethrows, so an
                // uncaught send would 500 for a KNOWN confirmed address while an unknown one 302s
                // (SMTP unset is the shipped default) -- an enumeration oracle that defeats the
                // anti-enumeration return above. Log and fall through to the same redirect.
                try
                {
                    await _emailSender.SendEmailAsync(
                        Input.Email,
                        EmailType.ForgotPassword.ToString(),
                        callbackUrl);
                        //$"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
                }
                catch (Exception ex)
                {
                    Febris.SharedServices.FebrisLog.Error(ex);
                }

                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            return Page();
        }
    }
}
