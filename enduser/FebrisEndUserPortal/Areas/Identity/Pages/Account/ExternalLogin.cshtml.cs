// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Febris.ModelLibrary.Models.UserModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Febris.EnumLibrary;
using Febris.UserNode.Portal.IdentityPolicy;
using Febris.PrimaryLogicLayer.Logic.UserLogic;

namespace Febris.UserNode.Portal.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<LocalApplicationUser> _signInManager;
        private readonly UserManager<LocalApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ExternalLoginModel> _logger;
        private readonly IRegistrationPolicy _registrationPolicy;
        private readonly IdentityPolicyOptions _identityPolicy;
        private readonly IUserLogic _userLogic;

        public ExternalLoginModel(
            SignInManager<LocalApplicationUser> signInManager,
            UserManager<LocalApplicationUser> userManager,
            ILogger<ExternalLoginModel> logger,
            IEmailSender emailSender,
            IRegistrationPolicy registrationPolicy,
            IOptions<IdentityPolicyOptions> identityPolicy,
            IUserLogic userLogic)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _emailSender = emailSender;
            _registrationPolicy = registrationPolicy;
            _identityPolicy = identityPolicy?.Value ?? new IdentityPolicyOptions();
            _userLogic = userLogic;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ProviderDisplayName { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public IActionResult OnGetAsync()
        {
            return RedirectToPage("./Login");
        }

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new {ReturnUrl = returnUrl });
            }
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor : true);
            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }
            else
            {
                // Closed SSO (Registration.AutoProvisionJit=false): a first-login user with no existing account
                // is turned away rather than offered the create-account form -- they must be pre-provisioned.
                if (!_registrationPolicy.AutoProvisionJitEnabled)
                {
                    ErrorMessage = "No account is associated with your sign-in. Please contact your administrator.";
                    return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
                }

                // If the user does not have an account, then ask the user to create an account.
                ReturnUrl = returnUrl;
                ProviderDisplayName = info.ProviderDisplayName;
                if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
                {
                    Input = new InputModel
                    {
                        Email = info.Principal.FindFirstValue(ClaimTypes.Email)
                    };
                }
                return Page();
            }
        }

        // SSO first-login self-provisioning (JIT account creation). This is a SELF-SERVICE sink, so it
        // honors the SAME registration policy as the local Register page: mode/domain admission
        // (IsEmailAllowed), the admin-approval hold, and email confirmation. Admin/bulk provisioning is a
        // DIFFERENT sink and is intentionally NOT constrained by the self-registration allowlist.
        [EnforcesGate("Registration.AllowedEmailDomains")]
        [EnforcesGate("Registration.AutoProvisionJit")]
        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            // Get the information about the user from the external login provider
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information during confirmation.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // Closed SSO (Registration.AutoProvisionJit=false): never create an account from an SSO first-login.
            // Defense in depth -- the callback already turns these users away before the form is shown.
            if (!_registrationPolicy.AutoProvisionJitEnabled)
            {
                ErrorMessage = "No account is associated with your sign-in. Please contact your administrator.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            if (ModelState.IsValid)
            {
                // Gate on the IdP-VERIFIED email claim, NOT the user-editable form field: an attacker who
                // signs in via a public/multi-tenant IdP as attacker@x.com could otherwise retype an
                // allowlisted address and defeat DomainAllowlist. Fall back to the typed value only when the
                // IdP supplies no email claim -- and in that case force confirmation below so the unverified
                // address must be proven before the account can sign in.
                string verifiedEmail = info.Principal.FindFirstValue(ClaimTypes.Email);
                string email = verifiedEmail ?? Input.Email;
                bool idpVerifiedEmail = verifiedEmail != null;

                // Domain/mode gate: Open admits any well-formed address, DomainAllowlist restricts to the
                // configured domains, AdminOnly/Invite block JIT creation (admins add these users instead).
                if (!_registrationPolicy.IsEmailAllowed(email))
                {
                    ModelState.AddModelError(string.Empty,
                        "Registration is not available for this email domain. Please use your organization email address or contact your administrator.");
                    ProviderDisplayName = info.ProviderDisplayName;
                    ReturnUrl = returnUrl;
                    return Page();
                }

                // Provision through the SAME primitive as local self-registration: xAPI Actor + least-
                // privileged role + approval hold, so an SSO account is never a role-less/Actor-less orphan.
                string firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty;
                string lastName = info.Principal.FindFirstValue(ClaimTypes.Surname) ?? string.Empty;
                bool requireApproval = _registrationPolicy.RequiresAdminApproval;

                var (user, errors) = await _userLogic.CreateExternallyProvisioned(
                    firstName, lastName, email, info, requireApproval);

                if (user == default)
                {
                    // Anti-enumeration (mirrors Register): a duplicate email returns the SAME neutral
                    // confirmation outcome as a fresh account; only non-duplicate errors are surfaced.
                    var surfaced = errors
                        .Where(e => e.Code != "DuplicateUserName" && e.Code != "DuplicateEmail")
                        .ToList();
                    if (surfaced.Count == 0)
                    {
                        return RedirectToPage("./RegisterConfirmation", new { Email = email });
                    }
                    foreach (var error in surfaced)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    ProviderDisplayName = info.ProviderDisplayName;
                    ReturnUrl = returnUrl;
                    return Page();
                }

                _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

                // Confirm the email when the policy requires it OR the IdP did not verify it. Do NOT sign in
                // while held for approval or awaiting confirmation; land on the neutral confirmation page.
                bool mustConfirm = _identityPolicy.Registration.RequireConfirmedEmail || !idpVerifiedEmail;
                if (requireApproval || mustConfirm)
                {
                    if (mustConfirm)
                    {
                        await SendConfirmationEmailAsync(user);
                    }
                    return RedirectToPage("./RegisterConfirmation", new { Email = email });
                }

                await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }

            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;
            return Page();
        }

        private async Task SendConfirmationEmailAsync(LocalApplicationUser user)
        {
            try
            {
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code },
                    protocol: Request.Scheme);

                // This codebase's IEmailSender templates by an EmailType passed as the "subject" (see
                // ForgotPassword/Register); a human subject would throw Enum.Parse and 500 AFTER the
                // account was created. EmailVerification is the matching template; the URL is the body.
                await _emailSender.SendEmailAsync(user.Email, EmailType.EmailVerification.ToString(), callbackUrl);
            }
            catch (System.Exception ex)
            {
                // A mail-send failure must NOT 500 or orphan the just-created account.
                Febris.SharedServices.FebrisLog.Error(ex);
            }
        }
    }
}
