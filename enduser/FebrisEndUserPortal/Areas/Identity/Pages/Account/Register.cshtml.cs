// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Febris.ModelLibrary.Models.UserModels;
using Febris.EnumLibrary;
using Febris.PrimaryLogicLayer.Logic.UserLogic;
using Febris.UserNode.Portal.IdentityPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Febris.UserNode.Portal.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Registration is GATE-DRIVEN (docs IDENTITY_POLICY_GATES, "Identity:Registration"):
    /// <list type="bullet">
    ///   <item><b>AdminOnly / Invite</b> (safe default): the page keeps its closed behavior --
    ///         an informational prompt plus the configured external providers; any POST bounces
    ///         to Login.</item>
    ///   <item><b>Open / DomainAllowlist</b>: a real self-registration form. The server-side gate
    ///         is re-checked on POST (never trust the rendered form), the email must pass
    ///         <see cref="IRegistrationPolicy.IsEmailAllowed"/>, and the account is created through
    ///         <see cref="IUserLogic.CreateSelfRegistered"/> -- least-privileged User role + the same
    ///         xAPI Actor linkage as admin-provisioned users (no orphan accounts), NOT pre-confirmed,
    ///         so SignIn.RequireConfirmedEmail gates sign-in until the emailed link is clicked.</item>
    /// </list>
    /// </summary>
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<LocalApplicationUser> _signInManager;
        private readonly UserManager<LocalApplicationUser> _userManager;
        private readonly IUserLogic _userLogic;
        private readonly IRegistrationPolicy _registrationPolicy;
        private readonly IEmailSender _emailSender;
        private readonly IdentityPolicyOptions _identityPolicy;

        public RegisterModel(
            SignInManager<LocalApplicationUser> signInManager,
            UserManager<LocalApplicationUser> userManager,
            IUserLogic userLogic,
            IRegistrationPolicy registrationPolicy,
            IEmailSender emailSender,
            IOptions<IdentityPolicyOptions> identityPolicy)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _userLogic = userLogic;
            _registrationPolicy = registrationPolicy;
            _emailSender = emailSender;
            _identityPolicy = identityPolicy?.Value ?? new IdentityPolicyOptions();
        }

        public string ReturnUrl { get; set; }

        /// <summary>Whether the operator has enabled self-registration (Open / DomainAllowlist).</summary>
        public bool SelfRegistrationEnabled => _registrationPolicy.SelfRegistrationEnabled;

        /// <summary>External providers (OIDC / OAuth) registered in Startup. Empty list -> the
        /// federated-sign-in section is hidden by the view.</summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "First name")]
            public string FirstName { get; set; }

            [Required]
            [Display(Name = "Last name")]
            public string LastName { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        [EnforcesGate("Registration.Mode")]
        [EnforcesGate("Registration.RequireAdminApproval")]
        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            // Server-side gate: when the operator keeps registration closed, a POST is a misrouted
            // request and bounces to Login -- identical to the pre-gate behavior.
            if (!_registrationPolicy.SelfRegistrationEnabled)
            {
                return RedirectToPage("./Login", new { returnUrl });
            }

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Domain gate (DomainAllowlist mode; Open admits any well-formed address).
            if (!_registrationPolicy.IsEmailAllowed(Input.Email))
            {
                ModelState.AddModelError(string.Empty,
                    "Registration is not available for this email domain. Please use your organization email address or contact your administrator.");
                return Page();
            }

            bool requireApproval = _registrationPolicy.RequiresAdminApproval;
            var (user, errors) = await _userLogic.CreateSelfRegistered(
                Input.FirstName, Input.LastName, Input.Email, Input.Password, requireApproval);

            if (user == default)
            {
                // Anti-enumeration: never reveal that an email is already registered. A duplicate returns
                // the SAME neutral confirmation outcome as a fresh registration; only non-duplicate errors
                // (e.g. a weak password the user themselves submitted) are surfaced.
                var surfaced = errors
                    .Where(e => e.Code != "DuplicateUserName" && e.Code != "DuplicateEmail")
                    .ToList();
                if (surfaced.Count == 0)
                {
                    return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl });
                }
                foreach (var error in surfaced)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            // Pending admin approval (account created LOCKED) OR email confirmation required: do NOT
            // sign in; land on the neutral confirmation page.
            if (requireApproval || _identityPolicy.Registration.RequireConfirmedEmail)
            {
                if (_identityPolicy.Registration.RequireConfirmedEmail)
                {
                    await SendConfirmationEmailAsync(user);
                }
                return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl });
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl);
        }

        private async Task SendConfirmationEmailAsync(LocalApplicationUser user)
        {
            try
            {
                string code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                string callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code },
                    protocol: Request.Scheme);

                // This codebase's IEmailSender templates by an EmailType passed as the "subject" (see
                // ForgotPassword); EmailVerification is the matching template and the URL is the body
                // hyperlink. Passing a human subject would throw Enum.Parse and 500 after user creation.
                await _emailSender.SendEmailAsync(user.Email, EmailType.EmailVerification.ToString(), callbackUrl);
            }
            catch (System.Exception ex)
            {
                // A mail-send failure must NOT 500 or orphan the (already-created) account. Log and
                // continue; the account exists and confirmation can be re-driven (resend / admin).
                Febris.SharedServices.FebrisLog.Error(ex);
            }
        }
    }
}
