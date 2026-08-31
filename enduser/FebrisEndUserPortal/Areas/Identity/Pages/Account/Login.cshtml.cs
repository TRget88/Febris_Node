// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Febris.ModelLibrary.Models.UserModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Febris.EnumLibrary;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Febris.UserNode.Portal.IdentityPolicy;

namespace Febris.UserNode.Portal.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly UserManager<LocalApplicationUser> _userManager;
        private readonly SignInManager<LocalApplicationUser> _signInManager;
        //private readonly RoleManager<ApplicationUser> _roleManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly bool _allowLocalPassword;

        public LoginModel(
            SignInManager<LocalApplicationUser> signInManager,
            ILogger<LoginModel> logger,
            UserManager<LocalApplicationUser> userManager,
            IOptions<IdentityPolicyOptions> identityPolicy
            //RoleManager<ApplicationUser> roleManager
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            // Default true: on total options-binding failure keep password sign-in working rather than
            // locking every user out. An SSO-only deployment sets this false deliberately.
            _allowLocalPassword = identityPolicy?.Value?.Login?.AllowLocalPassword ?? true;
            //_roleManager = roleManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl = returnUrl ?? Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        [EnforcesGate("Login.AllowLocalPassword")]
        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            // IDENTITY_POLICY_GATES: operators can disable local username/password sign-in (SSO-only).
            // The password form is hidden on GET; this blocks a direct POST as defense in depth.
            if (!_allowLocalPassword) return NotFound();

            returnUrl = returnUrl ?? Url.Content("~/");

            if (!ModelState.IsValid)
            {
                ReturnUrl = returnUrl;
                return Page();
            }

            // Resolve the user via email lookup. A missing user and a wrong
            // password both surface the same "Invalid login attempt" message
            // so unauthenticated traffic can't enumerate valid email
            // addresses.
            LocalApplicationUser user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null || user.IsDeleted)
            {
                // A soft-deleted (retained) account is treated as non-existent: same generic message as an
                // unknown email, so sign-in is blocked regardless of lockout state and the reserved email is
                // not distinguishable from a fresh one (anti-enumeration).
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                ReturnUrl = returnUrl;
                return Page();
            }

            // Audit S-04 fix: lockoutOnFailure is now true so failed
            // attempts increment the Identity counter and eventually trip
            // the configured lockout. Previously this was false, which made
            // brute force free. The earlier in-flight Thread.Sleep(3000)
            // pinned Kestrel workers without actually stopping the attack
            // and has been removed.
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in.");
                return Redirect(returnUrl);
            }
            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
            }
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                return RedirectToPage("./Lockout");
            }
            if (result.IsNotAllowed)
            {
                // Surface email-not-confirmed explicitly so the user knows
                // to check their inbox rather than retrying the password.
                ModelState.AddModelError(string.Empty,
                    "Please verify your email address before signing in.");
                ReturnUrl = returnUrl;
                return Page();
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            ReturnUrl = returnUrl;
            return Page();
        }

        private async Task<ClaimsPrincipal> AuthTokenBuilder()
        {
            try
            {
                List<Claim> claims = new List<Claim>
                {
                  new Claim(ClaimTypes.Name, Guid.NewGuid().ToString())
                };
                ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                AuthenticationProperties authProperties = new AuthenticationProperties();
                ClaimsPrincipal output = new ClaimsPrincipal(claimsIdentity);
                //await HttpContext.SignInAsync(
                //  CookieAuthenticationDefaults.AuthenticationScheme,
                //  new ClaimsPrincipal(claimsIdentity),
                //  authProperties);

                return output;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "LoginModel.AuthTokenBuilder: suppressed exception");
            }
            return null;
        }
    }
}
