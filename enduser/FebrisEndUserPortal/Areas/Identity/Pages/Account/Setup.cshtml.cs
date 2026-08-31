// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.UserModels;
using Febris.PrimaryLogicLayer.Logic.UserLogic;
using Febris.UserNode.LogicLayer.Logic.IdentityLogic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Febris.UserNode.Portal.Areas.Identity.Pages.Account
{
    /// <summary>
    /// First-run claim (2026-08-21): create the node's first ITAdmin using the one-time token
    /// printed to stdout at startup.
    ///
    /// <para>
    /// This replaces a compiled-in seeded admin, which is a fine shape for unattended automation and
    /// a poor one for an open-source project: it put an admin password in a file on disk, required
    /// editing configuration before first boot, and with nothing configured produced an account at a
    /// reserved address that could never be signed in to. The environment-variable seed is kept as
    /// the unattended door. This is the interactive one.
    /// </para>
    ///
    /// <para>
    /// TWO GATES, AND BOTH MATTER. The page is served only while the node has NO ITAdmin, and a
    /// valid token is required to post. Either alone is insufficient: without the ITAdmin check a
    /// claimed node keeps a claim surface, and without the token the first stranger to reach a fresh
    /// node owns it. Once an ITAdmin exists this returns <b>404, not 403</b>, so it does not confirm
    /// that a setup endpoint was ever here.
    /// </para>
    ///
    /// <para>
    /// The token's trust boundary is the node's stdout, by owner decision. See
    /// <c>SeedData.IssueSetupTokenIfUnclaimedAsync</c> for why it is printed with
    /// <c>Console.WriteLine</c> and never through Serilog.
    /// </para>
    /// </summary>
    [AllowAnonymous]
    public class NodeSetupModel : PageModel
    {
        private readonly INodeSetupLogic _setup;
        private readonly IUserLogic _userLogic;
        private readonly UserManager<LocalApplicationUser> _userManager;
        private readonly SignInManager<LocalApplicationUser> _signInManager;

        public NodeSetupModel(
            INodeSetupLogic setup,
            IUserLogic userLogic,
            UserManager<LocalApplicationUser> userManager,
            SignInManager<LocalApplicationUser> signInManager)
        {
            _setup = setup;
            _userLogic = userLogic;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>True when a token is live and the form should render. False renders the
        /// "restart to get a new token" state instead.</summary>
        public bool TokenAvailable { get; set; }

        /// <summary>How long a freshly printed token lasts, for the page copy. Read from the logic
        /// layer rather than restated, so the page cannot drift from the real lifetime.</summary>
        public int TokenLifetimeMinutes
        {
            get { return (int)NodeSetupLogic.TokenLifetime.TotalMinutes; }
        }

        public class InputModel
        {
            /// <summary>The token from the node's startup output. This is the authorization: only
            /// someone who can read the console has it.</summary>
            [Required]
            [Display(Name = "Setup token from the node's startup output")]
            public string Token { get; set; }

            [Required]
            [Display(Name = "First name")]
            public string FirstName { get; set; }

            [Required]
            [Display(Name = "Last name")]
            public string LastName { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Your email address")]
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

        /// <summary>Render the claim form, or 404 once the node has been claimed.</summary>
        public async Task<IActionResult> OnGetAsync()
        {
            if (await NodeIsClaimedAsync())
            {
                return NotFound();
            }

            TokenAvailable = await _setup.HasLiveToken();
            return Page();
        }

        /// <summary>Claim the node: validate the token, create the first ITAdmin, burn the token.</summary>
        public async Task<IActionResult> OnPostAsync()
        {
            // RE-CHECK, never trusting the GET. A node can be claimed between rendering this form
            // and posting it, and that is precisely the race worth losing safely.
            if (await NodeIsClaimedAsync())
            {
                return NotFound();
            }

            TokenAvailable = await _setup.HasLiveToken();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            NodeSetupTokenState state = await _setup.Validate(Input.Token);
            if (state != NodeSetupTokenState.Claimable)
            {
                ModelState.AddModelError(string.Empty, MessageFor(state));
                return Page();
            }

            Guid? uuid = await _setup.ClaimableUuid(Input.Token);
            if (!uuid.HasValue)
            {
                // Lapsed between the two reads. Same message as any other dead token.
                ModelState.AddModelError(string.Empty, MessageFor(NodeSetupTokenState.Expired));
                return Page();
            }

            var (user, errors) = await _userLogic.CreateFirstAdmin(
                Input.FirstName, Input.LastName, Input.Email, Input.Password);
            if (user == default)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            // BURN THE TOKEN, and only now. If this loses the race the account must not survive, or
            // a node could end up with two first administrators from one token.
            Guid createdUserId;
            Guid.TryParse(await _userManager.GetUserIdAsync(user), out createdUserId);
            bool claimed = await _setup.Consume(uuid.Value, createdUserId, user.Email);
            if (!claimed)
            {
                await RollBackAsync(user);
                ModelState.AddModelError(string.Empty,
                    "This node was claimed by someone else while you were filling this in.");
                TokenAvailable = false;
                return Page();
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect("~/");
        }

        /// <summary>
        /// Whether any LIVE account already holds ITAdmin. The claim surface exists only while this
        /// is false.
        ///
        /// <para>
        /// Soft-deleted accounts do not count (2026-08-25). Deletion sets <c>IsDeleted</c> and locks
        /// the row with <c>LockoutEnd = MaxValue</c>, but retains it for xAPI history and FERPA and
        /// does NOT strip roles. Counting those meant the sole ITAdmin of a node deleting their own
        /// account left this returning true forever: the node believed it was claimed, no setup
        /// token was issued, and /setup answered 404, with no way back short of direct SQL. This
        /// query and the boot-time one in <c>SeedData</c> must agree, or the banner and the page
        /// disagree about whether the node is claimable.
        /// </para>
        /// </summary>
        private async Task<bool> NodeIsClaimedAsync()
        {
            var admins = await _userManager.GetUsersInRoleAsync(
                InstitutionUserAccountType.ITAdmin.ToString());
            return admins != null && admins.Any(admin => !admin.IsDeleted);
        }

        /// <summary>Undo a created account when the token turned out not to be claimable. The xAPI
        /// Actor minted alongside it is left as a harmless orphan, the same posture
        /// <c>ProvisionUserAsync</c> documents for its own rollback.</summary>
        private async Task RollBackAsync(LocalApplicationUser user)
        {
            try
            {
                await _userManager.DeleteAsync(user);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex,
                    "[node-setup] could not roll back the account created for an unclaimable setup token");
            }
        }

        /// <summary>Message for a token that is not claimable. Says what to DO, since the person
        /// reading it has console access and can act on it.</summary>
        private static string MessageFor(NodeSetupTokenState state)
        {
            switch (state)
            {
                case NodeSetupTokenState.Expired:
                    return "That setup token has expired. Restart the node and use the new token it prints.";
                case NodeSetupTokenState.AlreadyClaimed:
                    return "That setup token has already been used.";
                default:
                    return "That setup token is not valid. Check the node's startup output.";
            }
        }
    }
}
