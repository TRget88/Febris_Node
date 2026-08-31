// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.UserModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Febris.UserNode.Portal.IdentityPolicy;
using Febris.UserNode.LogicLayer.Logic.DataLogic;

namespace Febris.UserNode.Portal.Areas.Identity.Pages.Account.Manage
{
    public class DeletePersonalDataModel : PageModel
    {
        private readonly UserManager<LocalApplicationUser> _userManager;
        private readonly SignInManager<LocalApplicationUser> _signInManager;
        private readonly ILogger<DeletePersonalDataModel> _logger;
        private readonly ICohortMemberLogic _cohortMemberLogic;
        private readonly Febris.SharedServices.IImageFileHandler _imageFileHandler;
        private readonly Febris.PrimaryLogicLayer.Logic.XApiLogic.IActorLogic _actorLogic;
        private readonly bool _allowSelfDelete;
        private readonly bool _softDeleteOnly;

        public DeletePersonalDataModel(
            UserManager<LocalApplicationUser> userManager,
            SignInManager<LocalApplicationUser> signInManager,
            ILogger<DeletePersonalDataModel> logger,
            ICohortMemberLogic cohortMemberLogic,
            IOptions<IdentityPolicyOptions> identityPolicy,
            Febris.SharedServices.IImageFileHandler imageFileHandler,
            Febris.PrimaryLogicLayer.Logic.XApiLogic.IActorLogic actorLogic)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _cohortMemberLogic = cohortMemberLogic;
            _allowSelfDelete = identityPolicy?.Value?.AccountLifecycle?.AllowSelfServiceDeletion ?? false;
            _softDeleteOnly = identityPolicy?.Value?.AccountLifecycle?.SoftDeleteOnly ?? true;
            _imageFileHandler = imageFileHandler;
            _actorLogic = actorLogic;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }
        }

        public bool RequirePassword { get; set; }

        public async Task<IActionResult> OnGet()
        {
            // IDENTITY_POLICY_GATES: operators can disable self-service account deletion.
            if (!_allowSelfDelete) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            RequirePassword = await _userManager.HasPasswordAsync(user);
            return Page();
        }

        [EnforcesGate("AccountLifecycle.AllowSelfServiceDeletion")]
        [EnforcesGate("AccountLifecycle.SoftDeleteOnly")]
        public async Task<IActionResult> OnPostAsync()
        {
            if (!_allowSelfDelete) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            RequirePassword = await _userManager.HasPasswordAsync(user);
            if (RequirePassword)
            {
                if (!await _userManager.CheckPasswordAsync(user, Input.Password))
                {
                    ModelState.AddModelError(string.Empty, "Incorrect password.");
                    return Page();
                }
            }

            var userId = await _userManager.GetUserIdAsync(user);
            if (_softDeleteOnly)
            {
                // SoftDeleteOnly: RETAIN the row (xAPI history / FERPA) but LOCK it so it cannot sign in,
                // and stamp the deletion time for PurgeAfterDays. The email/username stays reserved --
                // UserManager.FindByEmail still sees the row, so re-registration is cleanly rejected --
                // until an operator purges it. Locking (not a query filter) is what blocks sign-in while
                // keeping the row visible to the duplicate check.
                user.IsDeleted = true;
                user.DeletedUtc = DateTimeOffset.UtcNow;
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.MaxValue;
                var softResult = await _userManager.UpdateAsync(user);
                if (!softResult.Succeeded)
                {
                    throw new InvalidOperationException($"Unexpected error occurred soft-deleting user with ID '{userId}'.");
                }
            }
            else
            {
                // Hard delete (operator opted out of retention).
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Unexpected error occurred deleting user with ID '{userId}'.");
                }

                // The Identity row is gone, so the learner's name and address must not survive in the
                // xAPI Actor. PSEUDONYMISE, never delete: FK_LocalStatement_Actor_ActorId is ON
                // DELETE CASCADE over a NOT NULL column, so removing the Actor would delete every
                // statement this learner ever produced. Mbox_sha1sum is retained, which keeps the
                // Actor a valid xAPI Agent (it is a legal IFI on its own) and keeps every statement
                // attributable.
                //
                // NOT done on the soft branch. There the row is retained deliberately, and the name
                // is part of what makes that retained history readable.
                try
                {
                    if (user.Actor.HasValue && user.Actor.Value != Guid.Empty)
                    {
                        await _actorLogic.Pseudonymise(user.Actor.Value);
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "DeletePersonalData: failed pseudonymising the xAPI actor for user {UserId}.", userId);
                }
            }

            // A deleted account (soft or hard) cannot remain a cohort member. CohortMember rows are in a
            // separate database (no FK cascade from AspNetUsers), so remove them here. Best-effort AFTER the
            // account delete -- the read-side !IsDeleted filters already exclude a soft-deleted user from every
            // cohort surface, so a cleanup failure must not fail the deletion itself.
            try
            {
                await _cohortMemberLogic.RemoveAllForUser(user.Id);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "DeletePersonalData: failed removing cohort memberships for user {UserId}.", userId);
            }

            // The profile photograph goes in BOTH branches, soft and hard.
            //
            // It is deleted on a SOFT delete too, which is deliberate. The row is retained for xAPI
            // history, and a photograph contributes nothing to that. More practically, this node
            // ships SoftDeleteOnly with PurgeAfterDays unset, so soft-deleted accounts are never
            // purged on a default deployment: deferring the photograph to purge time would mean it
            // was never actually deleted, which is the same theatre as a retention knob nobody sets.
            //
            // Best-effort, after the account delete, exactly like the cohort cleanup above. Failing
            // a deletion because an image could not be removed would be the worse outcome.
            try
            {
                bool photoRemoved = await _imageFileHandler.DeleteProfileImage(user.Id, user.ProfilePicturePath);
                if (photoRemoved)
                {
                    _logger.LogInformation("DeletePersonalData: removed the profile photograph for user {UserId}.", userId);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "DeletePersonalData: failed removing the profile photograph for user {UserId}.", userId);
            }

            await _signInManager.SignOutAsync();

            _logger.LogInformation("User with ID '{UserId}' {Mode} their account.",
                userId, _softDeleteOnly ? "soft-deleted" : "hard-deleted");

            return Redirect("~/");
        }
    }
}
