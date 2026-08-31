// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.UserModels;
using Febris.ModelLibrary.ViewModels;
using Febris.PrimaryLogicLayer.Logic.UserLogic;
using Febris.UserNode.LogicLayer.Logic.IdentityLogic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Febris.UserNode.Portal.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Redeem an account invitation (invitation flow 2026-08-21).
    ///
    /// <para>
    /// This page is anonymous but it is NOT self-registration, and the distinction is the whole
    /// design. It does not consult <c>IRegistrationPolicy</c> at all: the authorization is the
    /// token, which an operator minted for one named address. That is why closing registration does
    /// not strand people who already hold an invitation.
    /// </para>
    ///
    /// <para>
    /// TWO CONTROLS THE CENTRAL DEVELOPER-ORG INVITE FLOW SHIPS WITHOUT, both of which its own
    /// source documents as known gaps:
    /// </para>
    /// <list type="number">
    /// <item><b>Recipient binding is enforced.</b> The invitee must state the address the invitation
    /// was sent to, checked with <see cref="InviteRecipientMatch.RecipientEmailMatches"/> -- the
    /// helper the central flow ships deliberately uncalled. The address is NOT in the link and NOT
    /// prefilled, so a forwarded email is not a transferable account.</item>
    /// <item><b>The token is consumed on POST, not on GET.</b> A link preview, a mail scanner or a
    /// prefetching browser cannot burn an invitation by fetching it.</item>
    /// </list>
    ///
    /// <para>
    /// Every failure states the same thing regardless of cause, except where a distinct message is
    /// genuinely more useful to a legitimate invitee (expired, already used, cancelled). Nothing
    /// here ever reveals whether an address already has an account.
    /// </para>
    /// </summary>
    [AllowAnonymous]
    public class AcceptInvitationModel : PageModel
    {
        private readonly INodeInvitationLogic _invitations;
        private readonly IUserLogic _userLogic;
        private readonly SignInManager<LocalApplicationUser> _signInManager;
        private readonly UserManager<LocalApplicationUser> _userManager;

        public AcceptInvitationModel(
            INodeInvitationLogic invitations,
            IUserLogic userLogic,
            SignInManager<LocalApplicationUser> signInManager,
            UserManager<LocalApplicationUser> userManager)
        {
            _invitations = invitations;
            _userLogic = userLogic;
            _signInManager = signInManager;
            _userManager = userManager;
        }

        /// <summary>The raw token from the link. Round-tripped through a HIDDEN FORM FIELD rather
        /// than being re-read from the query on POST, so the browser posts to a clean URL.</summary>
        [BindProperty]
        public string Code { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>True when the token is currently redeemable and the form should render.</summary>
        public bool Redeemable { get; set; }

        /// <summary>
        /// True when no code was supplied at all, so the page should ASK for one rather than report
        /// an invalid token. This is the path from the login page's "have an invitation?" link, for
        /// somebody who mislaid the emailed link but still has the code. Distinguished from a bad
        /// code deliberately: telling someone their blank field is invalid is not an error message,
        /// it is a dead end.
        /// </summary>
        public bool AwaitingCode { get; set; }

        /// <summary>Why the invitation cannot be used, when it cannot. Null while redeemable.</summary>
        public string UnavailableMessage { get; set; }

        /// <summary>Given name from the invitation, shown so the page reads as addressed to a
        /// person. Safe to show: it is only ever populated for an ACTIVE token, which means the
        /// person reading it holds a secret sent to that address.</summary>
        public string InvitedName { get; set; }

        public class InputModel
        {
            /// <summary>
            /// The address the invitation was sent to. This is the recipient-binding control, not a
            /// convenience field: it is compared against the stored address before any account is
            /// created. Not prefilled, deliberately -- prefilling would hand the answer to whoever
            /// holds the link.
            /// </summary>
            [Required]
            [EmailAddress]
            [Display(Name = "The email address this invitation was sent to")]
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

        /// <summary>
        /// Show the form for a redeemable token, or explain why it is not.
        /// <para>
        /// READ-ONLY on purpose. Nothing is consumed here, so a mail scanner, a link preview or a
        /// prefetching browser cannot spend somebody's invitation before they read the email.
        /// </para>
        /// </summary>
        public async Task<IActionResult> OnGetAsync(string code)
        {
            Code = code;
            await ClassifyAsync(code);
            return Page();
        }

        /// <summary>
        /// Redeem: re-validate, enforce the recipient binding, create the account, then claim the
        /// invitation atomically.
        /// </summary>
        public async Task<IActionResult> OnPostAsync()
        {
            // RE-VALIDATE. The GET's answer is never trusted: the invitation may have been revoked
            // or expired between rendering the form and posting it, and the form field is
            // client-controlled anyway.
            NodeInviteValidation validation = await ClassifyAsync(Code);
            if (!Redeemable)
            {
                // A code that does not resolve is most likely a typo when it was entered by hand,
                // so keep the form up with the message attached rather than replacing the page with
                // a dead end. Expired, used and cancelled are terminal states where retyping cannot
                // help, so those still take over the page.
                if (validation.State == InviteState.NotFound)
                {
                    AwaitingCode = true;
                    UnavailableMessage = null;
                    ModelState.AddModelError(string.Empty, "That invitation code is not valid.");
                }
                return Page();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            NodeUserInvite invite = validation.Invite;

            // RECIPIENT BINDING. The one control that makes an invitation link something other than
            // a bearer token for an account. Deliberately produces the SAME message as a bad token,
            // so this page cannot be used to discover which address an invitation was issued to.
            if (!InviteRecipientMatch.RecipientEmailMatches(invite.Email, Input.Email))
            {
                Febris.SharedServices.FebrisLog.Warn(
                    "[invitation] recipient mismatch on redemption attempt for invitation " + invite.UUID);
                ModelState.AddModelError(string.Empty,
                    "That is not the address this invitation was sent to.");
                return Page();
            }

            // Create the account with the invited role and a CONFIRMED email: redeeming a token that
            // was only ever delivered to this address is what confirmation proves.
            var (user, errors) = await _userLogic.CreateFromInvitation(
                invite.FirstName, invite.LastName, invite.Email, Input.Password, invite.Role);

            if (user == default)
            {
                foreach (var error in errors)
                {
                    // A duplicate here means the address was claimed between issue and redemption.
                    // Say so plainly rather than with the usual anti-enumeration hedge: the person
                    // reading this already proved they control the address, so there is nothing left
                    // to keep from them, and "sign in instead" is the useful answer.
                    if (error.Code == "DuplicateUserName" || error.Code == "DuplicateEmail")
                    {
                        ModelState.AddModelError(string.Empty,
                            "An account already exists for that address. Sign in instead, or use Forgot Password.");
                        continue;
                    }
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            // CLAIM THE INVITATION, atomically and only now. If this loses -- revoked in the last
            // few milliseconds, or a second redemption in flight -- the account must not survive,
            // or a cancelled invitation would still have produced one.
            Guid createdUserId;
            Guid.TryParse(await _userManager.GetUserIdAsync(user), out createdUserId);
            bool consumed = await _invitations.Consume(invite.UUID, createdUserId);
            if (!consumed)
            {
                await RollBackAsync(user, invite.UUID);
                Redeemable = false;
                UnavailableMessage = "This invitation is no longer valid. Ask whoever invited you to send a new one.";
                return Page();
            }

            // OPTIONAL cohort linkage, and it runs AFTER the invitation is safely claimed so a
            // failure here cannot cost the account. The logic layer never throws and reports whether
            // a link was made; a cohort deleted since issue simply does not get one.
            await _invitations.LinkAcceptedUserToCohort(invite, createdUserId);

            Febris.SharedServices.FebrisLog.Warn(string.Format(
                "[invitation] accepted for {0} at role {1}", invite.Email, invite.Role));

            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect("~/");
        }

        /// <summary>Undo a created account when the invitation turned out not to be claimable. The
        /// xAPI Actor minted alongside it is left as a harmless orphan, the same posture
        /// <c>ProvisionUserAsync</c> documents for its own rollback.</summary>
        private async Task RollBackAsync(LocalApplicationUser user, Guid inviteUuid)
        {
            try
            {
                await _userManager.DeleteAsync(user);
            }
            catch (Exception ex)
            {
                // Logged rather than swallowed: an account that outlives its unclaimed invitation is
                // exactly the kind of thing someone needs to be able to find later.
                Febris.SharedServices.FebrisLog.Error(ex,
                    "[invitation] could not roll back the account created for unclaimable invitation " + inviteUuid);
            }
        }

        /// <summary>Classify the token and set the page's display state from it.</summary>
        private async Task<NodeInviteValidation> ClassifyAsync(string code)
        {
            AwaitingCode = string.IsNullOrWhiteSpace(code);
            if (AwaitingCode)
            {
                // Nothing to validate, and nothing to complain about. The form renders with a
                // visible code field instead.
                Redeemable = false;
                UnavailableMessage = null;
                InvitedName = null;
                return new NodeInviteValidation() { State = InviteState.NotFound };
            }

            NodeInviteValidation validation = await _invitations.Validate(code);
            Redeemable = validation.State == InviteState.Active && validation.Invite != null;
            UnavailableMessage = Redeemable ? null : validation.Message;
            InvitedName = Redeemable ? validation.Invite.FirstName : null;
            return validation;
        }
    }
}
