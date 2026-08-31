// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.UserModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace Febris.UserNode.Portal.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Initial-password landing page for ADMIN-CREATED accounts (2026-08-21).
    ///
    /// <para>
    /// Closes a real defect rather than adding a feature. <c>UserLogic.Create</c> generated a random
    /// password, assigned it, DISCARDED it, and sent nothing -- so a person an admin created had an
    /// account with a password nobody knew and no notification that it existed. Their only route in
    /// was guessing they should try Forgot Password. Now they are emailed a link that lands here.
    /// </para>
    ///
    /// <para>
    /// Mechanically this is a password reset: the link carries an ASP.NET Identity password-reset
    /// token, and <c>ResetPasswordAsync</c> validates it, enforces the configured password policy
    /// and rolls the security stamp. It is a SEPARATE page from <c>ResetPassword</c> only because
    /// telling somebody to "reset" a password they have never had is confusing, and the activation
    /// email's button says "Set Your Password".
    /// </para>
    ///
    /// <para>
    /// DELIBERATELY DIFFERENT FROM THE CENTRAL TIER'S EQUIVALENT, which takes <c>userId</c> in the
    /// URL, looks the account up on GET and renders its email address read-only BEFORE validating
    /// the token -- so any well-formed base64 string plus a real user id discloses that user's email
    /// address. Here the link carries only <c>code</c>, the invitee states their own address, and
    /// there is nothing to disclose. Requiring the address costs one field and matches
    /// <c>ResetPassword</c>, which is the page node users already know.
    /// </para>
    ///
    /// <para>
    /// The token parameter is named <c>code</c> for the same reason as everywhere else in this
    /// codebase: <c>SensitiveQueryRedactor</c> blanks that key before the analytics middleware
    /// stores the query string in a table rendered to org admins. Any other name would put live
    /// password-reset tokens there, which is finding H-26.
    /// </para>
    /// </summary>
    [AllowAnonymous]
    public class NodeAccountActivationModel : PageModel
    {
        private readonly UserManager<LocalApplicationUser> _userManager;
        private readonly SignInManager<LocalApplicationUser> _signInManager;

        public NodeAccountActivationModel(
            UserManager<LocalApplicationUser> userManager,
            SignInManager<LocalApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>True when a code was supplied and the form should render. False renders the
        /// "this link is not usable" state instead.</summary>
        public bool LinkUsable { get; set; }

        public class InputModel
        {
            /// <summary>
            /// The address the account was created for. A lookup key, not a security control: the
            /// reset token is already bound to one user, so a wrong address simply fails. It is
            /// asked for rather than shown because showing it would mean resolving an account from
            /// the URL before the token is verified, which is how the central equivalent became an
            /// email-disclosure oracle.
            /// </summary>
            [Required]
            [EmailAddress]
            [Display(Name = "Your email address")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Choose a password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            /// <summary>The reset token, carried from the link and posted back in a hidden field so
            /// the browser submits to a clean URL.</summary>
            public string Code { get; set; }
        }

        /// <summary>
        /// Render the form. Deliberately does NO account lookup and NO token verification: there is
        /// nothing here that a link-holder could learn, and nothing is consumed by fetching the
        /// page, so a mail scanner or link preview cannot spend the token.
        /// </summary>
        public IActionResult OnGet(string code = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                LinkUsable = false;
                return Page();
            }

            string decoded;
            if (!TryDecode(code, out decoded))
            {
                LinkUsable = false;
                return Page();
            }

            Input = new InputModel() { Code = decoded };
            LinkUsable = true;
            return Page();
        }

        /// <summary>Set the password, then sign in.</summary>
        public async Task<IActionResult> OnPostAsync()
        {
            LinkUsable = true;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            LocalApplicationUser user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null || user.IsDeleted)
            {
                // ONE message for every failure below, so this page cannot be used to learn which
                // addresses have accounts. A legitimate holder of a live link never sees it.
                ModelState.AddModelError(string.Empty, LinkFailureMessage);
                return Page();
            }

            IdentityResult result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);
            if (!result.Succeeded)
            {
                foreach (IdentityError error in result.Errors)
                {
                    // A password that fails the configured policy is the user's own input and is
                    // worth naming; an invalid or expired token is not, and must not be
                    // distinguishable from an unknown address.
                    if (string.Equals(error.Code, "InvalidToken", StringComparison.Ordinal))
                    {
                        ModelState.AddModelError(string.Empty, LinkFailureMessage);
                        continue;
                    }
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            // Clicking a link delivered to that inbox proves the address, so a separate verification
            // round trip would be theatre. Admin-created accounts are already created confirmed;
            // this keeps the flag honest for any path that is not.
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                try
                {
                    await _userManager.UpdateAsync(user);
                }
                catch (Exception ex)
                {
                    // Not fatal: the password is already set and the account is usable. Logged
                    // rather than surfaced, because failing the activation here would strand
                    // somebody whose password change already committed.
                    Febris.SharedServices.FebrisLog.Error(ex,
                        "[activation] password set but EmailConfirmed could not be updated");
                }
            }

            Febris.SharedServices.FebrisLog.Warn("[activation] account activated for " + user.Email);

            // Signed in rather than bounced to the login form: they have just proved the mailbox and
            // chosen the password, so asking for it again immediately is friction with no gain. Any
            // enforced two-factor enrolment is applied afterwards by UseTwoFactorEnrollmentGate.
            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect("~/");
        }

        /// <summary>The single message every link or account failure produces.</summary>
        public const string LinkFailureMessage =
            "This link is no longer usable. Ask an administrator to send a new one, or use Forgot Password.";

        /// <summary>Base64url-decode the token from the link. A malformed value is a dead link, not
        /// an exception.</summary>
        private static bool TryDecode(string code, out string decoded)
        {
            decoded = null;
            try
            {
                decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
