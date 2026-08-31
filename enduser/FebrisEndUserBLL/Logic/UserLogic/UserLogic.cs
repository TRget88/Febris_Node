// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.EmailModels;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.UserModels;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.ViewModels;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.DataAccessLayer.Queries.UserQueries;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
// Non-generic IEmailSender. Microsoft.AspNetCore.Identity (imported above) also declares a
// GENERIC IEmailSender<TUser>, so without this using the bare name binds to that one and fails
// with "requires 1 type arguments". Register.cshtml.cs imports the same namespace for the same
// reason.
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.PrimaryLogicLayer.Logic.UserLogic
{
    public interface IUserLogic
    {
        Task<List<LocalUserViewModel>> Get();
        Task<LocalUserViewModel> Get(Guid? id);
        Task<LocalUserSettingsViewModel> GetEdit(Guid? id);
        Task<LocalApplicationUser> Create(LocalUserCreation input);
        Task<(LocalApplicationUser User, IEnumerable<IdentityError> Errors)> CreateSelfRegistered(string firstName, string lastName, string emailAddress, string password, bool requireApproval);
        Task<(LocalApplicationUser User, IEnumerable<IdentityError> Errors)> CreateExternallyProvisioned(string firstName, string lastName, string emailAddress, UserLoginInfo externalLogin, bool requireApproval);

        /// <summary>
        /// Invitation acceptance (invitation flow 2026-08-21). Same primitive as the other two
        /// self-provisioning paths, so Actor linkage and rollback cannot drift, but differs on two
        /// points that the invitation itself justifies:
        /// <list type="bullet">
        ///   <item>the ROLE comes from the invitation rather than being hardcoded to User, because
        ///   an admin or educator already chose it under the rank policy;</item>
        ///   <item>the email is created CONFIRMED, because redeeming a token that was only ever
        ///   delivered to that address is exactly what email confirmation proves. Sending a
        ///   confirmation mail to an address that just proved itself would be theatre.</item>
        /// </list>
        /// No approval hold: an invited person was named by an operator, which is the approval.
        /// </summary>
        Task<(LocalApplicationUser User, IEnumerable<IdentityError> Errors)> CreateFromInvitation(string firstName, string lastName, string emailAddress, string password, string roleName);

        /// <summary>
        /// First-run claim (2026-08-21): create the node's first ITAdmin from the setup page.
        /// <para>
        /// Same primitive as the other self-provisioning paths, and it deliberately does NOT consult
        /// the role-rank policy. There is no acting operator to outrank anybody: the authorization
        /// is the setup token, which only someone who can read the node's stdout holds. The CALLER
        /// is responsible for having validated that token and for refusing once an ITAdmin exists.
        /// </para>
        /// <para>
        /// Email is created CONFIRMED. Nothing was mailed, so there is nothing to confirm, and
        /// leaving it unconfirmed would lock the operator out of the node they just claimed if
        /// <c>SignIn.RequireConfirmedEmail</c> is on.
        /// </para>
        /// </summary>
        Task<(LocalApplicationUser User, IEnumerable<IdentityError> Errors)> CreateFirstAdmin(string firstName, string lastName, string emailAddress, string password);
        //Task<LocalApplicationUser> Update(LocalUserSettingsViewModel input);
        Task<UserSettingsViewModel> GetSettings(Guid? guid);
        Task<UserSettingsViewModel> Update(IFormFileCollection files, UserSettingsViewModel input);
        Task<LocalApplicationUser> Update(IFormFileCollection files, LocalUserSettingsViewModel input);
        Task<List<LocalUserViewModel>> Get(InstitutionUserAccountType input);
        Task<BulkUserCreationViewModel> BulkCreationPreperation();
        Task<(int UsersAdded, int UsersNotAdded, int cohortLinksMade, int DuplicateEmailAddresses)> Create(BulkUserCreationSubmitListViewModel bulkInput);
        Task<(int UsersAdded, int UsersNotAdded, int cohortLinksMade, int DuplicateEmailAddresses)> Removal(BulkUserCreationSubmitListViewModel bulkInput);

        // Lockout toggle with the B-07 rank gate (the gate lives here so the API path is covered, not
        // just the portal). Returns Allowed=false (no state change) when the acting operator does not
        // outrank the target. On a lock of a plain User, cascades to the linked parent when all of that
        // parent's children are now locked.
        Task<(bool Allowed, bool NowLockedOut)> LockoutToggle(Guid targetUserId);

        /// <summary>
        /// Re-send the account-activation email for an existing account (2026-08-21). Mints a FRESH
        /// password-setup token, so it also serves an account whose original link lapsed.
        /// <para>
        /// Gated by <c>RoleRankPolicy.CanLock</c> -- the same "may this operator act on that
        /// account" rule the lockout toggle uses, reused rather than reinvented. <c>Allowed</c> is
        /// false when the actor does not outrank the target or the account is absent or
        /// soft-deleted; <c>Sent</c> is false when the mail itself could not go out, which is NOT a
        /// failure of the account and is reported separately so the operator is told the truth
        /// rather than a green tick.
        /// </para>
        /// </summary>
        Task<(bool Allowed, bool Sent)> ResendActivation(Guid targetUserId);
    }


    public class UserLogic : IUserLogic
    {
        //private readonly IUserQueries _context;
        private readonly UserManager<LocalApplicationUser> _userManager;
        //private readonly RoleManager<LocalApplicationUser> _roleManager;


        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        private readonly IPasswordGenerator _passwordGenerator;
        private readonly IImageFileHandler _imageHandler;
        private readonly IActorLogic _actorLogic;
        private readonly ICohortQueries _cohortContext;
        private readonly ICohortMemberQueries _memberContext;
        private readonly IUrlHelper Url;
        private readonly IParentLinkLogic _parentLinkLogic;

        /// <summary>
        /// Outbound mail for the account-activation link. INJECTED rather than newed from
        /// <c>StaticDetails.PassedBackConfig</c> like the three older send sites in this file,
        /// because a send that cannot be substituted cannot be tested, and "did the new user
        /// actually get told their account exists" is the entire point of the change that added it.
        /// </summary>
        private readonly IEmailSender _emailSender;

        // DI refactor
        public UserLogic(
            IHttpContextAccessor httpContextAccessor,
            UserManager<LocalApplicationUser> userManager,
            IUrlHelperFactory urlHelperFactory,
            IActionContextAccessor actionContextAccessor,
            IPasswordGenerator passwordGenerator,
            IImageFileHandler imageHandler,
            IActorLogic actorLogic,
            ICohortQueries cohortContext,
            ICohortMemberQueries memberContext,
            IParentLinkLogic parentLinkLogic,
            IEmailSender emailSender
            )
        {
            _emailSender = emailSender;
            _httpContextAccessor = httpContextAccessor;
            //_context = new UserQueries();
            _userManager = userManager;
            User = _httpContextAccessor?.HttpContext?.User;
            _passwordGenerator = passwordGenerator;
            _imageHandler = imageHandler;
            _actorLogic = actorLogic;
            _cohortContext = cohortContext;
            _memberContext = memberContext;
            _parentLinkLogic = parentLinkLogic;
            Url = urlHelperFactory.GetUrlHelper(actionContextAccessor.ActionContext);
            //_roleManager = roleManager;
            //_urlHelper = new UrlHelper(new ActionContext { RouteData = new RouteData() });
        }

        public UserLogic(
            IHttpContextAccessor httpContextAccessor,
            UserManager<LocalApplicationUser> userManager,
            IUrlHelperFactory urlHelperFactory,
            IActionContextAccessor actionContextAccessor
            )
        {
            _httpContextAccessor = httpContextAccessor;
            //_context = new UserQueries();
            _userManager = userManager;
            User = _httpContextAccessor.HttpContext.User;
            _passwordGenerator = new PasswordGenerator();
            _imageHandler = new ImageFileHandler();
            _actorLogic = new ActorLogic(_httpContextAccessor);
            _cohortContext = new CohortQueries();
            _memberContext = new CohortMemberQueries();
            _parentLinkLogic = new ParentLinkLogic(_httpContextAccessor, _userManager);
            // Legacy self-newing ctor: same static-config EmailService the three older send sites in
            // this file build, so this path keeps working unchanged while DI resolution is preferred.
            _emailSender = new EmailService(StaticDetails.PassedBackConfig);
            Url = urlHelperFactory.GetUrlHelper(actionContextAccessor.ActionContext);
            //_roleManager = roleManager;
            //_urlHelper = new UrlHelper(new ActionContext { RouteData = new RouteData() });
        }

        /// <summary>
        /// The signed-in operator's role claims. Audit C-05/C-06: every role-ASSIGNMENT path gates
        /// on these through <see cref="RoleRankPolicy.CanAssign"/>, in the BLL rather than in the
        /// controllers, so the API path is covered too and a view that renders an unfiltered role
        /// list cannot become an escalation door.
        /// </summary>
        private IList<string> ActorRoles()
        {
            return User?.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList() ?? new List<string>();
        }

        /// <summary>
        /// Email an admin-created user the link that lets them set their own password
        /// (2026-08-21). The account already exists with a random password nobody holds, so this
        /// mail is the ONLY thing that makes it reachable.
        ///
        /// <para>
        /// NEVER THROWS. The account is already committed by the time this runs, so an uncaught
        /// send failure would 500 the operator after creating a user -- and this codebase's
        /// EmailService rethrows on every failure, which is exactly the shape several existing node
        /// call sites have. A failed send leaves a usable account that the operator can recover with
        /// Forgot Password, and leaves a log line saying so.
        /// </para>
        ///
        /// <para>
        /// The URL is built ABSOLUTE from the CURRENT REQUEST SCHEME, and both halves matter. The
        /// AccountActivation email template only renders its button for an absolute http/https URI
        /// (the SCBA-B4 anchor guard), so a relative link would send a button-less mail with nothing
        /// in any log to explain it. And the scheme is read from the request rather than hardcoded
        /// "https" the way the bulk path did it, because a node reached over plain http would
        /// otherwise be sent a link to a scheme it does not serve.
        /// </para>
        ///
        /// <para>
        /// The token parameter is named <c>code</c>: <c>SensitiveQueryRedactor</c> blanks that key
        /// before the analytics middleware stores the query string in a table rendered to org
        /// admins. Any other name would put live password-reset tokens there (finding H-26).
        /// </para>
        /// </summary>
        /// <returns>True when the mail was handed to the sender, false when it could not be. The
        /// creation paths ignore this (the account stands either way); the RESEND path reports it,
        /// because "I pressed the button and nothing happened" is the whole reason that button
        /// exists.</returns>
        private async Task<bool> SendAccountActivationAsync(LocalApplicationUser user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return false;
            }

            try
            {
                string code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                string scheme = _httpContextAccessor?.HttpContext?.Request?.Scheme;
                if (string.IsNullOrWhiteSpace(scheme))
                {
                    scheme = "https";
                }

                string callbackUrl = Url.Page(
                    "/Account/ActivateAccount",
                    pageHandler: null,
                    values: new { area = "Identity", code },
                    protocol: scheme);

                await _emailSender.SendEmailAsync(
                    user.Email, EmailType.AccountActivation.ToString(), callbackUrl);
                return true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex,
                    "UserLogic: the account for " + user.Email
                    + " was created but its activation email could not be sent. "
                    + "The account is usable and the person can set a password via Forgot Password.");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<(bool Allowed, bool Sent)> ResendActivation(Guid targetUserId)
        {
            try
            {
                LocalApplicationUser target = await _userManager.FindByIdAsync(targetUserId.ToString());
                if (target == null || target.IsDeleted)
                {
                    // A soft-deleted account is RETAINED, not live. Mailing it a working
                    // password-setup link would quietly undo the deletion, the same reason
                    // ResetPassword refuses one.
                    return (false, false);
                }

                // RANK GATE, and RoleRankPolicy.CanLock is reused rather than a second rule
                // invented. The question is identical -- may this operator act on that account --
                // and two rank rules for the same question is how they drift apart. Its name says
                // "lock" because locking was its first caller, not because the rule is about
                // locking.
                //
                // Note this is STRICTER than the gate on creating the account in the first place:
                // an Admin can create an Admin's account by invitation but cannot resend for a
                // peer. That asymmetry is deliberate and safe in the restrictive direction, and
                // Forgot Password remains the peer's own route.
                IList<string> actorRoles = ActorRoles();
                IList<string> targetRoles = await _userManager.GetRolesAsync(target);
                if (!RoleRankPolicy.CanLock(actorRoles, targetRoles))
                {
                    Febris.SharedServices.FebrisLog.Warn(
                        "UserLogic.ResendActivation: denied -- actor does not outrank target " + targetUserId);
                    return (false, false);
                }

                bool sent = await SendAccountActivationAsync(target);

                // Audit line: this mails a LIVE password-setup token to an account the operator does
                // not own, so who did it and for whom is worth being able to find later. The node
                // has no audit table (see ParentLinkLogic for the same note), so it goes through the
                // existing logging path at Warn.
                Febris.SharedServices.FebrisLog.Warn(string.Format(
                    "[activation] resend for {0} requested by {1}; sent={2}",
                    target.Email,
                    User?.Identity?.Name ?? "(unrecorded)",
                    sent));

                return (true, sent);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        // B-07: the lock decision lives in the BLL so the rank gate also covers the API path, not just
        // the portal controller. The acting operator is the cookie principal (User); the target's roles
        // are read from Identity. RoleRankPolicy decides; on a lock of a plain User we cascade to the parent.
        public async Task<(bool Allowed, bool NowLockedOut)> LockoutToggle(Guid targetUserId)
        {
            try
            {
                LocalApplicationUser target = await _userManager.FindByIdAsync(targetUserId.ToString());
                if (target == null)
                {
                    return (false, false);
                }

                IList<string> actorRoles = User?.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();
                IList<string> targetRoles = await _userManager.GetRolesAsync(target);

                if (!RoleRankPolicy.CanLock(actorRoles, targetRoles))
                {
                    Febris.SharedServices.FebrisLog.Warn("UserLogic.LockoutToggle: denied -- actor does not outrank target " + targetUserId);
                    return (false, false);
                }

                DateTimeOffset? lockoutEnd = await _userManager.GetLockoutEndDateAsync(target);
                bool nowLockedOut;
                if (lockoutEnd == null || lockoutEnd < DateTimeOffset.UtcNow)
                {
                    await _userManager.SetLockoutEndDateAsync(target, DateTimeOffset.MaxValue);
                    nowLockedOut = true;
                }
                else
                {
                    await _userManager.SetLockoutEndDateAsync(target, DateTimeOffset.UtcNow);
                    nowLockedOut = false;
                }
                await _userManager.UpdateAsync(target);

                // Parent cascade: only when we just LOCKED a plain User. Lock that user's linked parent
                // if and only if every one of that parent's children is now locked.
                if (nowLockedOut && targetRoles != null && targetRoles.Contains(InstitutionUserAccountType.User.ToString()))
                {
                    await _parentLinkLogic.CascadeLockParentIfAllChildrenLocked(target);
                }

                return (true, nowLockedOut);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<BulkUserCreationViewModel> BulkCreationPreperation()
        {
            BulkUserCreationViewModel output = new BulkUserCreationViewModel();
            try
            {
                #region Filter
                if (!User.IsLocalFebrisAdmin() && !User.IsLocalAdmin() && !User.IsLocalEducator())
                {
                    return default;
                }
                #endregion

                List<Cohort> cohortList = await _cohortContext.Get();
                SelectList cohortSelectList = new SelectList(cohortList, "UUID", "Name");
                output.CohortSelectList = cohortSelectList;

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<LocalApplicationUser> Create(LocalUserCreation input)
        {
            LocalApplicationUser output = new LocalApplicationUser();
            try
            {
                #region Filter
                if (!User.IsLocalFebrisAdmin()&& !User.IsLocalAdmin() && !User.IsLocalEducator())
                {
                    return default;
                }

                // Audit C-06, the second escalation door: the role filter above admits Educator,
                // and the Create view rendered an UNFILTERED GetEnumSelectList, so an Educator
                // could create an Admin or ITAdmin outright. A new account holds no roles yet, so
                // the target rank is empty and only the granted role is in question.
                if (!RoleRankPolicy.CanAssign(ActorRoles(), new string[0], input.UserAccountType.ToString()))
                {
                    Febris.SharedServices.FebrisLog.Warn(
                        "UserLogic.Create: denied -- actor may not grant " + input.UserAccountType);
                    return default;
                }
                #endregion

                /*LocalApplicationUser newUser*/
                output = new LocalApplicationUser
                {
                    FirstName = input.FirstName,
                    LastName = input.LastName,
                    UserName = input.EmailAddress,/*.FirstName.Replace(" ", "_") + "_" + input.LastName.Replace(" ", "_"),*/
                    Email = input.EmailAddress,
                    PhoneNumber = input.PhoneNumber,
                    // Admin-provisioned accounts are trusted/pre-confirmed, so they can sign in even when
                    // SignIn.RequireConfirmedEmail is on (which then gates only self-registration).
                    EmailConfirmed = true,
                };

                string generatedPassword = _passwordGenerator.PasswordRandomize();
                var result = await _userManager.CreateAsync(output, generatedPassword);

                //add role
                var roleResult = await _userManager.AddToRoleAsync(output, input.UserAccountType.ToString());
                if (!roleResult.Succeeded)
                {
                    return default;
                }


                //if User is of role type User create an actor, if it is a parent than it needs to be attached to a students actor
                if (input.UserAccountType == InstitutionUserAccountType.User)
                {
                    string mbox = Sha1Handler.TextToHash(output.Email);
                    Actor actor = new Actor()
                    {
                        Name = input.FirstName + " " + input.LastName,//output.UserName,
                        //Mbox = new Uri(mbox),
                        Mbox_sha1sum = mbox,
                        ObjectType = "Agent"
                    };
                    actor = await _actorLogic.Create(actor);
                    output.Actor = actor.UUID;

                    await _userManager.UpdateAsync(output);
                }

                // TELL THEM THE ACCOUNT EXISTS. Until 2026-08-21 this method generated a random
                // password, assigned it, DISCARDED it (the local above is never read again) and
                // sent nothing -- so an admin-created user had an account with a password nobody
                // knew and no notification that it existed. Their only route in was guessing that
                // Forgot Password might work on an address they had never registered.
                await SendAccountActivationAsync(output);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        /// <summary>
        /// Self-registration create (IDENTITY_POLICY_GATES: Registration.Mode Open/DomainAllowlist).
        /// Unlike the admin <see cref="Create(LocalUserCreation)"/> there is NO operator-role filter --
        /// the caller is anonymous; the Register page enforces the IRegistrationPolicy gate (mode +
        /// domain allowlist) BEFORE calling this. The user chooses their own password, is NOT
        /// pre-confirmed (SignIn.RequireConfirmedEmail then gates sign-in until the emailed link is
        /// clicked), gets the least-privileged User role, and gets the same xAPI Actor linkage as an
        /// admin-provisioned user -- no orphan accounts (the reason the old scaffolded register path
        /// was removed).
        /// </summary>
        public async Task<(LocalApplicationUser User, IEnumerable<IdentityError> Errors)> CreateSelfRegistered(string firstName, string lastName, string emailAddress, string password, bool requireApproval)
            => await ProvisionUserAsync(firstName, lastName, emailAddress, password, externalLogin: null, requireApproval);

        /// <summary>
        /// SSO first-login (JIT) provisioning. Upholds the SAME invariants as
        /// <see cref="CreateSelfRegistered"/> -- xAPI Actor linkage, least-privileged User role, and the
        /// admin-approval hold, so there are NO orphan accounts -- but the account has no password (the
        /// external IdP authenticates it) and is linked to <paramref name="externalLogin"/>. Both
        /// self-service creation paths flow through <see cref="ProvisionUserAsync"/> so their Actor/role
        /// linkage can never drift apart.
        /// </summary>
        public async Task<(LocalApplicationUser User, IEnumerable<IdentityError> Errors)> CreateExternallyProvisioned(string firstName, string lastName, string emailAddress, UserLoginInfo externalLogin, bool requireApproval)
            => await ProvisionUserAsync(firstName, lastName, emailAddress, password: null, externalLogin, requireApproval);

        /// <inheritdoc />
        public async Task<(LocalApplicationUser User, IEnumerable<IdentityError> Errors)> CreateFromInvitation(string firstName, string lastName, string emailAddress, string password, string roleName)
            => await ProvisionUserAsync(firstName, lastName, emailAddress, password, externalLogin: null,
                requireApproval: false, roleName: roleName, emailConfirmed: true);

        /// <inheritdoc />
        public async Task<(LocalApplicationUser User, IEnumerable<IdentityError> Errors)> CreateFirstAdmin(string firstName, string lastName, string emailAddress, string password)
            => await ProvisionUserAsync(firstName, lastName, emailAddress, password, externalLogin: null,
                requireApproval: false, roleName: InstitutionUserAccountType.ITAdmin.ToString(), emailConfirmed: true);

        /// <summary>
        /// Shared self-provisioning primitive. Actor FIRST (harmless orphan if the account never commits),
        /// then a single CreateAsync (with a password for local registration, without one for SSO), then
        /// the external-login link (SSO only) and the least-privileged role. Every post-create step is
        /// verified with a rollback that logs if the compensating delete also fails, so an incomplete or
        /// role-less row can never silently squat an email address.
        /// </summary>
        /// <param name="roleName">Role to grant. NULL keeps the least-privileged default, which is
        /// what both self-service paths pass -- only invitation acceptance supplies a role, and only
        /// after the rank policy has already authorized the issuer to grant it.</param>
        /// <param name="emailConfirmed">Whether the account starts with a confirmed email. FALSE for
        /// both self-service paths, which must prove the address separately. TRUE only for
        /// invitation acceptance, where redeeming a token delivered solely to that address IS the
        /// proof. Defaulted so neither existing caller changes behavior.</param>
        private async Task<(LocalApplicationUser User, IEnumerable<IdentityError> Errors)> ProvisionUserAsync(string firstName, string lastName, string emailAddress, string password, UserLoginInfo externalLogin, bool requireApproval, string roleName = null, bool emailConfirmed = false)
        {
            try
            {
                // 0. Fail fast on a RESERVED email (an existing OR soft-deleted account) BEFORE minting an
                //    Actor -- FindByEmail sees soft-deleted rows (no query filter), so a duplicate re-register
                //    attempt is rejected here rather than after creating an orphan Actor on every attempt.
                if (await _userManager.FindByEmailAsync(emailAddress) != null)
                {
                    return (default, new[] { new IdentityError { Code = "DuplicateEmail", Description = "That email address is already in use." } });
                }

                // 1. Create the xAPI Actor FIRST. If this fails, NO account row exists yet -- nothing to
                //    roll back and no email squatted (an orphan Actor is harmless: no login, no email key).
                Actor actor;
                try
                {
                    string mbox = Sha1Handler.TextToHash(emailAddress);
                    actor = await _actorLogic.Create(new Actor()
                    {
                        Name = firstName + " " + lastName,
                        Mbox_sha1sum = mbox,
                        ObjectType = "Agent"
                    });
                }
                catch (Exception ex)
                {
                    Febris.SharedServices.FebrisLog.Error(ex);
                    return (default, new[] { new IdentityError { Code = "ActorCreationFailed", Description = "Registration could not be completed. Please try again." } });
                }

                // 2. Build the user with the Actor link AND (if pending approval) the lockout already set,
                //    so a SINGLE CreateAsync commits the final state -- no post-create UpdateAsync, which
                //    removes the concurrency window the rollback could not reliably cover.
                LocalApplicationUser output = new LocalApplicationUser
                {
                    FirstName = firstName,
                    LastName = lastName,
                    UserName = emailAddress,
                    Email = emailAddress,
                    EmailConfirmed = emailConfirmed,
                    Actor = actor.UUID,
                };
                if (requireApproval)
                {
                    // Admin-approval gate (IDENTITY_POLICY_GATES): a pending account is created LOCKED so
                    // it cannot sign in until an admin lifts the lockout (the existing LockoutToggle =
                    // "approve"). LockoutEnabled is load-bearing (IsLockedOutAsync ignores LockoutEnd when
                    // disabled). Without this the RequireAdminApproval knob would be a silent no-op.
                    output.LockoutEnabled = true;
                    output.LockoutEnd = DateTimeOffset.MaxValue;
                }

                // 3. Create the account. Local self-registration supplies a password; an SSO account has
                //    none -- the external IdP authenticates it.
                var createResult = password == null
                    ? await _userManager.CreateAsync(output)
                    : await _userManager.CreateAsync(output, password);
                if (!createResult.Succeeded)
                {
                    // No user row persisted; the Actor from step 1 is a harmless orphan.
                    return (default, createResult.Errors);
                }

                // 4-5. Post-create steps (external-login link for SSO, then the least-privileged role) run
                //       AFTER the row is committed, so ANY failure must roll the row back or it squats the
                //       email. A failed IdentityResult is handled inline; a THROWN exception is caught here,
                //       rolled back, and re-thrown. (UserManager.AddToRoleAsync THROWS, not returns a failed
                //       result, when the "User" role is absent -- e.g. if SeedRolesAsync silently failed at
                //       boot -- so without this catch the outer handler would propagate an orphan row.)
                try
                {
                    // Link the external login (SSO only). Local registration passes externalLogin == null.
                    if (externalLogin != null)
                    {
                        var loginResult = await _userManager.AddLoginAsync(output, externalLogin);
                        if (!loginResult.Succeeded)
                        {
                            await RollBackProvisionedUser(output, emailAddress, "external-login link");
                            return (default, loginResult.Errors);
                        }
                    }

                    // Least-privileged User unless a caller supplied a role. Only invitation
                    // acceptance does, and only for a role the rank policy already cleared its
                    // issuer to grant -- so this stays a single role assignment with the escalation
                    // decision made upstream, not a second place where roles can be chosen.
                    string grantedRole = string.IsNullOrWhiteSpace(roleName)
                        ? InstitutionUserAccountType.User.ToString()
                        : roleName.Trim();
                    var roleResult = await _userManager.AddToRoleAsync(output, grantedRole);
                    if (!roleResult.Succeeded)
                    {
                        await RollBackProvisionedUser(output, emailAddress, "role assignment");
                        return (default, roleResult.Errors);
                    }
                }
                catch (Exception)
                {
                    // A post-create step threw (not merely returned a failed result). Roll the committed row
                    // back so no half-provisioned account squats the email, then let the failure propagate.
                    await RollBackProvisionedUser(output, emailAddress, "post-create provisioning (threw)");
                    throw;
                }

                return (output, Enumerable.Empty<IdentityError>());
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        private async Task RollBackProvisionedUser(LocalApplicationUser user, string emailAddress, string failedStep)
        {
            // Must NEVER throw: this is called from a catch that re-throws the ORIGINAL failure, so a
            // throwing rollback would mask the real cause. Log both delete-failed and delete-threw cases.
            try
            {
                var deleteResult = await _userManager.DeleteAsync(user);
                if (!deleteResult.Succeeded)
                {
                    Febris.SharedServices.FebrisLog.Error(new Exception(
                        "ProvisionUserAsync: " + failedStep + " AND rollback delete both failed for '" + emailAddress +
                        "'. An incomplete account row may squat this email address; manual cleanup required."));
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(new Exception(
                    "ProvisionUserAsync: " + failedStep + " AND rollback delete THREW for '" + emailAddress +
                    "'. An incomplete account row may squat this email address; manual cleanup required.", ex));
            }
        }

        public async Task<(int UsersAdded, int UsersNotAdded, int cohortLinksMade, int DuplicateEmailAddresses)> Create(BulkUserCreationSubmitListViewModel bulkInput)
        {
            int startingList = 0;
            int usersAdded = 0;
            int usersNotAdded = 0;
            int cohortLinksMade = 0;
            int duplicateEmailAddresses = 0;
            List<LocalApplicationUser> userList = new List<LocalApplicationUser>();
            List<CohortMember> memberList = new List<CohortMember>();
            List<Cohort> cohortList = new List<Cohort>();

            try
            {
                #region Filter
                if (!User.IsLocalFebrisAdmin() &&
                    !User.IsLocalAdmin() &&
                    !User.IsLocalEducator() &&
                    !User.IsLocalITAdmin())
                {
                    return default;
                }

                // A null batch is not a crash. Deserializing a JSON body whose property
                // names do not match this view model leaves SubmissionList null, and the
                // row below used to dereference it -- an unhandled 500 on malformed input.
                // Placed AFTER the role check so an unauthorized caller still cannot tell
                // a malformed batch from a refused one, and BEFORE the RoleRankPolicy call,
                // which dereferences bulkInput.AccountType and would NRE on a null input.
                //
                // The return type is a counts tuple with no error channel, so default
                // (all zeros) is the only non-throwing answer, and it matches the two
                // existing refusals in this method. Callers that need to TELL the user
                // guard first and return 400 -- see UserController.BulkCreatePost.
                if (bulkInput == null || bulkInput.SubmissionList == null)
                {
                    Febris.SharedServices.FebrisLog.Warn(
                        "UserLogic.Create: refused -- no submission list on the posted batch.");
                    return default;
                }

                // Audit C-05/C-06, the THIRD door. The audit named Create and Update; bulk import
                // assigns bulkInput.AccountType to every row the same way and admits Educator by
                // the same filter, so gating only the two named paths would have left an Educator
                // able to mint Admins one Excel paste at a time. One AccountType covers the whole
                // batch, so this is decided once here rather than per row.
                if (!RoleRankPolicy.CanAssign(ActorRoles(), new string[0], bulkInput.AccountType.ToString()))
                {
                    Febris.SharedServices.FebrisLog.Warn(
                        "UserLogic.Create(bulk): denied -- actor may not grant " + bulkInput.AccountType);
                    return default;
                }
                #endregion

                startingList = bulkInput.SubmissionList.Count();

                // Pre-fetch existing users + actors in two batch queries so the
                // per-row loop below does dictionary lookups instead of paying
                // FindByEmailAsync + Exists + GetByHashedMbox per submission.
                // CSV imports of 500 rows previously fired ~5*500 = 2500 DB
                // calls just for the lookup phase; this collapses that to 2.
                List<string> emailList = bulkInput.SubmissionList
                    .Where(s => !string.IsNullOrEmpty(s.EmailAddress))
                    .Select(s => s.EmailAddress)
                    .Distinct()
                    .ToList();
                Dictionary<string, LocalApplicationUser> existingUserByEmail = new Dictionary<string, LocalApplicationUser>(StringComparer.OrdinalIgnoreCase);
                if (emailList.Count > 0)
                {
                    List<LocalApplicationUser> existingUsers = await _userManager.Users
                        .Where(u => emailList.Contains(u.Email))
                        .ToListAsync();
                    foreach (var u in existingUsers)
                    {
                        if (!string.IsNullOrEmpty(u.Email) && !existingUserByEmail.ContainsKey(u.Email))
                        {
                            existingUserByEmail.Add(u.Email, u);
                        }
                    }
                }

                Dictionary<string, Actor> existingActorByHash = new Dictionary<string, Actor>();
                if (bulkInput.AccountType == InstitutionUserAccountType.User && emailList.Count > 0)
                {
                    List<string> hashList = emailList
                        .Select(e => Sha1Handler.TextToHash(e))
                        .Distinct()
                        .ToList();
                    List<Actor> existingActors = await _actorLogic.GetByHashedMboxList(hashList);
                    foreach (var a in existingActors)
                    {
                        if (a != null && !string.IsNullOrEmpty(a.Mbox_sha1sum) && !existingActorByHash.ContainsKey(a.Mbox_sha1sum))
                        {
                            existingActorByHash.Add(a.Mbox_sha1sum, a);
                        }
                    }
                }

                //user and actor creation
                foreach (var i in bulkInput.SubmissionList)
                {
                    try
                    {

                        ///Create User -- dict lookup replaces FindByEmailAsync
                        LocalApplicationUser tempUser = null;
                        if (!string.IsNullOrEmpty(i.EmailAddress))
                        {
                            existingUserByEmail.TryGetValue(i.EmailAddress, out tempUser);
                        }
                        IList<string> originalRole = default;
                        if (tempUser != default)
                        {
                            ++duplicateEmailAddresses;
                            originalRole = await _userManager.GetRolesAsync(tempUser);
                        }
                        else
                        {
                            tempUser = new LocalApplicationUser()
                            {
                                IdentificationNumber = i.IdentificationNumber,
                                FirstName = i.FirstName,
                                LastName = i.LastName,
                                Email = i.EmailAddress,
                                PhoneNumber = i.PhoneNumber,
                                UserName = i.EmailAddress,
                                // Admin/bulk-provisioned = trusted/pre-confirmed (see single-create above).
                                EmailConfirmed = true
                            };

                            string generatedPassword = _passwordGenerator.PasswordRandomize();
                            var result = await _userManager.CreateAsync(tempUser, generatedPassword);

                            ///****Role Handling****
                            //remove roles
                            await _userManager.RemoveFromRolesAsync(tempUser, originalRole);
                            //add role
                            var roleResult = await _userManager.AddToRoleAsync(tempUser, bulkInput.AccountType.ToString());
                            if (!roleResult.Succeeded)
                            {
                                // COMPENSATING DELETE, then carry on with the batch.
                                //
                                // This was "return default", which exited the WHOLE method from inside
                                // the per-item loop. Nothing already committed was undone: earlier
                                // users existed in Identity, had Actors, and had already been sent
                                // verification emails. The cohort-linking block below this loop never
                                // ran, so not one of them received their membership. And because the
                                // return type is a value tuple of ints, "default" is (0,0,0,0), so the
                                // admin was told "0 added, 0 not added, 0 cohort links" while dozens
                                // of real, emailed accounts existed. Silent success and silent failure
                                // in the same statement.
                                //
                                // The failing user is DELETED rather than left behind. CreateAsync has
                                // already committed a row, and a roleless account is one that can
                                // authenticate while satisfying no role check anywhere on the node.
                                // Deleting is clean here specifically because the confirmation email
                                // is sent further down, AFTER this point, so nobody has been told
                                // about an account that is about to disappear.
                                FebrisLog.ErrorMessage("UserLogic.Create: role assignment failed for '"
                                    + i.EmailAddress + "': "
                                    + string.Join("; ", roleResult.Errors.Select(e => e.Description))
                                    + ". Removing the part-created account and continuing with the batch.");

                                IdentityResult cleanup = await _userManager.DeleteAsync(tempUser);
                                if (!cleanup.Succeeded)
                                {
                                    FebrisLog.ErrorMessage("UserLogic.Create: could NOT remove the part-created account '"
                                        + i.EmailAddress + "'. It exists in Identity with no role and needs manual removal.");
                                }

                                ++usersNotAdded;
                                continue;
                            }
                            ++usersAdded;
                        }

                        ///Create Actor -- dict lookup replaces Exists + GetByHashedMbox
                        if (bulkInput.AccountType == InstitutionUserAccountType.User)
                        {
                            Actor actor = null;
                            if (tempUser.Actor == null || tempUser.Actor == Guid.Empty || tempUser.Actor == default)
                            {
                                string mbox_sha1sum = Sha1Handler.TextToHash(i.EmailAddress);
                                existingActorByHash.TryGetValue(mbox_sha1sum, out actor);

                                ///create new actor when no existing record matched
                                if (actor == null)
                                {
                                    actor = new Actor()
                                    {
                                        Name = i.FirstName + " " + i.LastName,
                                        Mbox_sha1sum = mbox_sha1sum,
                                        ObjectType = "Agent"
                                    };
                                    actor = await _actorLogic.Create(actor);
                                    // Memo the freshly-created actor so a
                                    // duplicate email later in this same
                                    // import doesn't create a second one.
                                    if (actor != null && !string.IsNullOrEmpty(actor.Mbox_sha1sum))
                                    {
                                        existingActorByHash[actor.Mbox_sha1sum] = actor;
                                    }
                                }
                                //Add Actor to User******************************
                                tempUser.Actor = actor.UUID;
                                await _userManager.UpdateAsync(tempUser);
                            }
                        }

                        //Add to user list
                        userList.Add(tempUser);

                        ///send email
                        if (tempUser != null)
                        {
                            // ACTIVATION, not verification (2026-08-21). This used to send
                            // EmailVerification pointing at /Account/ConfirmEmail, which was the
                            // same defect as the single-create path wearing a disguise: bulk-created
                            // accounts are built with EmailConfirmed = true, so the link confirmed
                            // an already-confirmed address and the recipient STILL had no way to
                            // learn a password. They now get the same set-your-password link an
                            // individually created user gets.
                            //
                            // The #if(!DEBUG) fence is KEPT. It exists because a CSV import can
                            // blast hundreds of emails and nobody wants that from a dev box. The
                            // single-create path deliberately has no such fence, matching Register
                            // and ForgotPassword, so the flow is still testable locally one user at
                            // a time.
#if(!DEBUG)
                            await SendAccountActivationAsync(tempUser);
#endif
                        }


                    }
                    catch (System.Exception ex)
                    {
                        Febris.SharedServices.FebrisLog.Error(ex, "UserLogic.Create: suppressed exception");
                        ++usersNotAdded;
                    }
                    //userList.Add(tempLead);
                }

                //cohort linking
                if (bulkInput.SelectedCohortList.Count() > 0)
                {
                    // P-14 fix (2026-05-20): was N+1 (one _cohortContext.Get(id)
                    // call per selected cohort). _cohortContext.Get() already
                    // returns all cohorts in a single query; filter in memory by
                    // SelectedCohortList. Acceptable for bulk-import (admin-
                    // triggered, infrequent; cohort table is institution-scoped
                    // and small). If cohort tables grow large, add an
                    // ICohortLogic.Get(List<Guid?>) batch method.
                    // ROADMAP 19: resolving an ALREADY-CHOSEN selection, so archived cohorts are
                    // deliberately included. CohortQueries.Get() now excludes them, which is right
                    // for lists and pickers -- but filtering here would silently drop a cohort the
                    // admin explicitly selected, and a bulk import that quietly skips part of its
                    // roster is exactly the silent-success failure this audit exists to remove.
                    List<Cohort> allCohorts = await _cohortContext.GetIncludingArchived();
                    HashSet<Guid?> selectedSet = new HashSet<Guid?>(bulkInput.SelectedCohortList);
                    cohortList.AddRange(allCohorts.Where(c => selectedSet.Contains(c.UUID)));

                    foreach (var i in cohortList)
                    {
                        foreach (var j in userList)
                        {
                            CohortMember tempMember = new CohortMember()
                            {
                                UserId = j.Id,
                                Cohort = i,
                                CohortUUID = i.UUID
                            };
                            memberList.Add(tempMember);
                            ++cohortLinksMade;
                        }
                    }
                    memberList = await _memberContext.Create(memberList);
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return (usersAdded, usersNotAdded, cohortLinksMade, duplicateEmailAddresses);
        }

        public async Task<(int UsersAdded, int UsersNotAdded, int cohortLinksMade, int DuplicateEmailAddresses)> Removal(BulkUserCreationSubmitListViewModel bulkInput)
        {
            int startingList = 0;
            int usersExist = 0;
            int usersDidNotExist = 0;
            int cohortLinksRemoved = 0;
            int duplicateEmailAddresses = 0;
            List<LocalApplicationUser> userList = new List<LocalApplicationUser>();
            List<CohortMember> memberList = new List<CohortMember>();
            List<Cohort> cohortList = new List<Cohort>();

            try
            {
                #region Filter
                if (!User.IsLocalFebrisAdmin() &&
                    !User.IsLocalAdmin() &&
                    !User.IsLocalEducator() &&
                    !User.IsLocalITAdmin())
                {
                    return default;
                }

                // A null batch is not a crash. Deserializing a JSON body whose property
                // names do not match this view model leaves SubmissionList null, and the
                // row below used to dereference it -- an unhandled 500 on malformed input.
                // Placed AFTER the role check so an unauthorized caller still cannot tell
                // a malformed batch from a refused one, and BEFORE the RoleRankPolicy call,
                // which dereferences bulkInput.AccountType and would NRE on a null input.
                //
                // The return type is a counts tuple with no error channel, so default
                // (all zeros) is the only non-throwing answer, and it matches the two
                // existing refusals in this method. Callers that need to TELL the user
                // guard first and return 400 -- see UserController.BulkCreatePost.
                if (bulkInput == null || bulkInput.SubmissionList == null)
                {
                    Febris.SharedServices.FebrisLog.Warn(
                        "UserLogic.Removal: refused -- no submission list on the posted batch.");
                    return default;
                }
                #endregion

                //user and actor creation
                foreach (var i in bulkInput.SubmissionList)
                {
                    try
                    {
                        ///Find User
                        LocalApplicationUser tempUser = await _userManager.FindByEmailAsync(i.EmailAddress);
                        if (tempUser == default)
                        {
                            ++usersDidNotExist;
                        }
                        else
                        {
                            userList.Add(tempUser);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Febris.SharedServices.FebrisLog.Error(ex, "UserLogic.Removal: suppressed exception");
                        ++usersDidNotExist;
                    }
                }
                startingList = bulkInput.SubmissionList.Count();
                //cohort unlinking
                if (bulkInput.SelectedCohortList.Count() > 0)
                {
                    // P-14 fix (2026-05-20): batched cohort lookup; see paired
                    // fix in the linking block above. Same rationale.
                    // ROADMAP 19: resolving an ALREADY-CHOSEN selection, so archived cohorts are
                    // deliberately included. CohortQueries.Get() now excludes them, which is right
                    // for lists and pickers -- but filtering here would silently drop a cohort the
                    // admin explicitly selected, and a bulk import that quietly skips part of its
                    // roster is exactly the silent-success failure this audit exists to remove.
                    List<Cohort> allCohorts = await _cohortContext.GetIncludingArchived();
                    HashSet<Guid?> selectedSet = new HashSet<Guid?>(bulkInput.SelectedCohortList);
                    cohortList.AddRange(allCohorts.Where(c => selectedSet.Contains(c.UUID)));

                    foreach (var i in cohortList)
                    {
                        List<CohortMember> tempMemberList = await _memberContext.Get(i);
                        ///Filter out the needed memebers
                        List<CohortMember>filtedTempMemberList = tempMemberList.Where(j => userList.Any(x => x.Id == j.UserId)).ToList();
                        memberList.AddRange(filtedTempMemberList);
                    }

                    foreach(var i in memberList)
                    {
                        bool temp = await _memberContext.Delete(i);
                        if (temp)
                        {
                            cohortLinksRemoved++;
                        }else
                        {

                        }
                    }                    
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return (usersExist, usersDidNotExist, cohortLinksRemoved, duplicateEmailAddresses);
        }

        public async Task<List<LocalUserViewModel>> Get()
        {
            List<LocalUserViewModel> output = new List<LocalUserViewModel>();
            List<LocalApplicationUser> userList = new List<LocalApplicationUser>();
            //List<LocalApplicationUser> output = new List<LocalApplicationUser>();
            try
            {
                #region Filter
                if (!User.IsLocalFebrisAdmin() && !User.IsLocalAdmin() && !User.IsLocalEducator())
                {
                    return default;
                }
                #endregion
                //output = _userManager.Users.ToList();

                userList = await _userManager.Users.Where(u => !u.IsDeleted).ToListAsync();   // exclude soft-deleted (AccountLifecycle.SoftDeleteOnly)
                //.Where(i=>i.Role==input)
                //.ToList();
                foreach (LocalApplicationUser i in userList.ToList())
                {
                    LocalUserViewModel temp = new LocalUserViewModel();
                    temp.ApplicationUser = i;


                    var role = await _userManager.GetRolesAsync(i);

                    // Hide accounts the viewer does not outrank. This generalises the rule it
                    // replaces ("hide SuperAdmin from anyone who is not SuperAdmin"), which was
                    // written when the node's top account WAS a SuperAdmin. Since the bootstrap
                    // admin moved to ITAdmin, that literal check stopped matching and the node's
                    // sole administrator began rendering in the Educator-visible user index --
                    // where UserLogic.Update rewrites roles with no rank comparison of its own.
                    //
                    // Peers stay visible (strictly greater, not >=), so an Admin can still manage
                    // other Admins exactly as before. Only accounts ABOVE the viewer disappear.
                    //
                    // NOTE: this is a VISIBILITY narrowing, not an authorization fix. The write
                    // path in Update still performs no rank check, so a crafted POST can still
                    // target a higher-ranked account. Closing that needs RoleRankPolicy.CanAssign,
                    // which does not exist yet -- tracked as its own roadmap item.
                    IList<string> viewerRoles = User?.Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value)
                        .ToList();
                    if (RoleRankPolicy.RankOf(role) > RoleRankPolicy.RankOf(viewerRoles))
                    {
                        userList.Remove(i);
                        continue;
                    }

                    foreach (var j in role)
                    {
                        if (string.IsNullOrEmpty(temp.Role))
                        {
                            temp.Role = j;
                        }
                        else
                        {
                            temp.Role = temp.Role + ", " + j;
                        }
                    }

                    if (i.LockoutEnd > DateTime.UtcNow)
                    {
                        temp.IsLockedOut = true;
                    }
                    else
                    {
                        temp.IsLockedOut = false;
                    }

                    temp.UserId = i.Id;
                    output.Add(temp);
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
            //List<LocalUserViewModel> output = new List<LocalUserViewModel>();
            //try
            //{
            //    output = await _context.Get();


            //}
            //catch (Exception ex)
            //{
            //    Febris.SharedServices.FebrisLog.Error(ex);
            //    throw;
            //}
            //return output;
        }

        public async Task<LocalUserViewModel> Get(Guid? input)
        {
            LocalUserViewModel output = new LocalUserViewModel();
            LocalApplicationUser preoutput = new LocalApplicationUser();
            try
            {
                preoutput = await _userManager.FindByIdAsync(input.ToString());
                var role = await _userManager.GetRolesAsync(preoutput);

                output.ApplicationUser = preoutput;

                foreach (var j in role)
                {
                    if (string.IsNullOrEmpty(output.Role))
                    {
                        output.Role = j;
                    }
                    else
                    {
                        output.Role = output.Role + ", " + j;
                    }
                }

                if (preoutput.LockoutEnd > DateTime.UtcNow)
                {
                    output.IsLockedOut = true;
                }
                else
                {
                    output.IsLockedOut = false;
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
            //LocalUserViewModel output = new LocalUserViewModel();
            //try
            //{
            //    output = await _context.Get(id);

            //}
            //catch (Exception ex)
            //{
            //    Febris.SharedServices.FebrisLog.Error(ex);
            //    throw;
            //}
            //return output;            
        }

        public async Task<List<LocalUserViewModel>> Get(InstitutionUserAccountType input)
        {
            //List<LocalUserViewModel> output = new List<LocalUserViewModel>();
            //try
            //{
            //    output = await _
            //    output = await _context.Get(input);               
            //}
            //catch (Exception ex)
            //{
            //    Febris.SharedServices.FebrisLog.Error(ex);
            //    throw;
            //}
            //return output;
            List<LocalUserViewModel> output = new List<LocalUserViewModel>();
            List<LocalApplicationUser> userList = new List<LocalApplicationUser>();
            try
            {
                userList = await _userManager.Users.Where(u => !u.IsDeleted).ToListAsync();   // exclude soft-deleted (AccountLifecycle.SoftDeleteOnly)
                //.Where(i=>i.Role==input)
                //.ToList();
                foreach (LocalApplicationUser i in userList.ToList())
                {
                    LocalUserViewModel temp = new LocalUserViewModel();
                    temp.ApplicationUser = i;


                    var role = await _userManager.GetRolesAsync(i);
                    if (!role.Contains(input.ToString()))
                    {
                        userList.Remove(i);
                        continue;
                    }

                    foreach (var j in role)
                    {
                        if (string.IsNullOrEmpty(temp.Role))
                        {
                            temp.Role = j;
                        }
                        else
                        {
                            temp.Role = temp.Role + ", " + j;
                        }
                    }

                    if (i.LockoutEnd > DateTime.UtcNow)
                    {
                        temp.IsLockedOut = true;
                    }
                    else
                    {
                        temp.IsLockedOut = false;
                    }

                    temp.UserId = i.Id;
                    output.Add(temp);
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<LocalUserSettingsViewModel> GetEdit(Guid? input)
        {
            
            LocalUserSettingsViewModel output = default;
            LocalApplicationUser user = new LocalApplicationUser();
            try
            {
                user = await _userManager.FindByIdAsync(input.ToString());
                //var role = await _userManager.GetRolesAsync(preoutput);

                output = new LocalUserSettingsViewModel()
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IdentificationNumber = user.IdentificationNumber,
                    //UserName = user.UserName,
                    EmailAddress = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    ProfilePicturePath = user.ProfilePicturePath,
                    Id = user.Id
                };

                var role = await _userManager.GetRolesAsync(user);

                try
                {
                    output.UserAccountType = (InstitutionUserAccountType)Enum.Parse(typeof(InstitutionUserAccountType), role.FirstOrDefault());
                }
                catch (System.Exception ex)
                {
                    Febris.SharedServices.FebrisLog.Error(ex, "UserLogic.GetEdit: suppressed exception");
                    //output.UserAccountType = (FebrisUserType)Enum.Parse(typeof(FebrisUserType), role.FirstOrDefault());

                }


                //foreach (var j in role)
                //{
                //    if (string.IsNullOrEmpty(temp..Role))
                //    {
                //        output.UserAccountType = j;
                //    }
                //    else
                //    {
                //        temp.Role = temp.Role + ", " + j;
                //    }
                //}

                //foreach (var j in role)
                //{
                //    if (string.IsNullOrEmpty(output.Role))
                //    {
                //        output.Role = j;
                //    }
                //    else
                //    {
                //        output.Role = output.Role + ", " + j;
                //    }
                //}

                //if (user.LockoutEnd > DateTime.UtcNow)
                //{
                //    output.IsLockedOut = true;
                //}
                //else
                //{
                //    output.IsLockedOut = false;
                //}
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<UserSettingsViewModel> GetSettings(Guid? input)
        {
            UserSettingsViewModel output = default;
            LocalApplicationUser user = new LocalApplicationUser();
            try
            {
                if (!User.IsCurrentUser(input.ToString()))
                {
                    return default;
                }

                user = await _userManager.FindByIdAsync(input.ToString());
                //var role = await _userManager.GetRolesAsync(preoutput);

                output = new UserSettingsViewModel()
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    UserName = user.UserName,
                    EmailAddress = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    ProfilePicturePath = user.ProfilePicturePath,
                    Id = user.Id
                };

                //foreach (var j in role)
                //{
                //    if (string.IsNullOrEmpty(output.Role))
                //    {
                //        output.Role = j;
                //    }
                //    else
                //    {
                //        output.Role = output.Role + ", " + j;
                //    }
                //}

                //if (user.LockoutEnd > DateTime.UtcNow)
                //{
                //    output.IsLockedOut = true;
                //}
                //else
                //{
                //    output.IsLockedOut = false;
                //}
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;

            //try
            //{
            //    LocalApplicationUser user = await Get(id);



            //    output = new UserSettingsViewModel()
            //    {
            //        FirstName = user.FirstName,
            //        LastName = user.LastName,
            //        UserName = user.UserName,
            //        EmailAddress = user.Email,
            //        PhoneNumber = user.PhoneNumber,
            //        ProfilePicturePath = user.ProfilePicturePath,
            //        Id = user.Id
            //    };


            //}
            //catch (Exception ex)
            //{
            //    Febris.SharedServices.FebrisLog.Error(ex);
            //    throw;
            //}
            //return output;
        }

        //public async Task<LocalApplicationUser> Update(LocalUserSettingsViewModel input)
        //{
        //    LocalApplicationUser output = default;
        //    try
        //    {
        //        LocalApplicationUser user = await _context.Get(input.Id);


        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}

        public async Task<UserSettingsViewModel> Update(IFormFileCollection files, UserSettingsViewModel input)
        {
            UserSettingsViewModel output = default;
            try
            {
                //if (User.GetUserId() != input.Id.ToString())
                //{
                //    return null;
                //}
                if (!User.IsCurrentUser(input.Id.ToString()))
                {
                    return default;
                }


                LocalApplicationUser preoutput = await _userManager.FindByIdAsync(User.GetUserId());
                preoutput.FirstName = input.FirstName;
                preoutput.LastName = input.LastName;
                preoutput.UserName = input.EmailAddress;
                preoutput.PhoneNumber = input.PhoneNumber.ToString();
                preoutput.Email = input.EmailAddress;

                if (files.Count != 0)
                {
                    bool uploaded = false;
                    foreach (var file in files)
                    {
                        (uploaded, preoutput) = await _imageHandler.AddImage(file, preoutput);
                        if (uploaded == true)
                        {
                            break;
                        }
                    }
                }
                await _userManager.UpdateAsync(preoutput);
                //preoutput = await _context.Update(preoutput);

                EmailService emailService = new EmailService(StaticDetails.PassedBackConfig)
                {
                    EmailType = EmailType.UserUpdated,
                    EmailModel = new EmailModel()
                    {
                        RecipientName = preoutput.FirstName + " " + preoutput.LastName,
                        RecipientEmailAddress = preoutput.Email,
                        RecipientUUID = preoutput.Id
                    }
                };
                bool sent = await emailService.SendEmail();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<LocalApplicationUser> Update(IFormFileCollection files, LocalUserSettingsViewModel input)
        {
            LocalApplicationUser output = new LocalApplicationUser();
            try
            {
                #region Filter
                if (!User.IsLocalFebrisAdmin() && !User.IsLocalAdmin() && !User.IsLocalEducator())
                {
                    return default;
                }
                #endregion

                output = await _userManager.FindByIdAsync(input.Id.ToString());
                if (output == null)
                {
                    return default;
                }

                // Audit C-05, the primary escalation door: the role filter above admits Educator
                // and this method then rewrote roles with NO rank comparison, so an Educator could
                // open any account INCLUDING ITS OWN, pick ITAdmin, and submit. Gate BEFORE any
                // mutation -- the profile fields below are written before the role block, so a
                // check placed down there would still let a denied request change the account.
                // The visibility narrowing in Get() does not close this: it hides higher-ranked
                // accounts from the list, but a crafted POST never reads the list.
                IList<string> targetRoles = await _userManager.GetRolesAsync(output);

                // No user may change their OWN role here (standing owner ruling: no self-promotion).
                // With the ceiling carve-out in CanAssign an ITAdmin may now re-role a PEER ITAdmin,
                // which is what makes the top role administrable -- but applied to itself that is a
                // route to self-demotion, and a sole ITAdmin demoting itself strands the node with
                // no way back in. Exactly the permanent self-lockout the "Febris User" toggle
                // shipped. Editing your own NAME or email here stays allowed; only a role CHANGE on
                // your own account is refused.
                bool editingSelf = string.Equals(User?.GetUserId(), input.Id.ToString(), StringComparison.OrdinalIgnoreCase);
                bool roleIsChanging = !targetRoles.Contains(input.UserAccountType.ToString(), StringComparer.OrdinalIgnoreCase);
                if (editingSelf && roleIsChanging)
                {
                    Febris.SharedServices.FebrisLog.Warn(
                        "UserLogic.Update: denied -- user " + input.Id + " may not change its own role");
                    return default;
                }

                if (!RoleRankPolicy.CanAssign(
                        ActorRoles(),
                        targetRoles,
                        input.UserAccountType.ToString()))
                {
                    Febris.SharedServices.FebrisLog.Warn(
                        "UserLogic.Update: denied -- actor may not assign " + input.UserAccountType
                        + " to user " + input.Id);
                    return default;
                }

                output.FirstName = input.FirstName;
                output.LastName = input.LastName;
                output.UserName = input.EmailAddress;
                output.PhoneNumber = input.PhoneNumber.ToString();
                output.Email = input.EmailAddress;

                if (files.Count != 0)
                {
                    bool uploaded = false;
                    foreach (var file in files)
                    {
                        (uploaded, input) = await _imageHandler.AddImage(file, input);
                        if (uploaded == true)
                        {
                            break;
                        }
                    }
                }

                var result = await _userManager.UpdateAsync(output);
                if (!result.Succeeded)
                {
                    return null;
                }


                if (input.UserAccountType == InstitutionUserAccountType.User && (output.Actor == Guid.Empty || output.Actor == null))
                {
                    Actor actor = await _actorLogic.Create(output);
                    output.Actor = actor.UUID;
                }


                var currentUserRole = await _userManager.GetRolesAsync(output);
                if (currentUserRole.Count != 1 || currentUserRole[0] != input.UserAccountType.ToString())
                {
                    await _userManager.RemoveFromRolesAsync(output, currentUserRole.ToArray());
                    await _userManager.UpdateAsync(output);
                    await _userManager.AddToRoleAsync(output, input.UserAccountType.ToString());
                    await _userManager.UpdateAsync(output);

                    var roleResult = await _userManager.AddToRoleAsync(output, input.UserAccountType.ToString());
                    if (!roleResult.Succeeded)
                    {
                        return null;
                    }
                }

                EmailService emailService = new EmailService(StaticDetails.PassedBackConfig)
                {
                    EmailType = EmailType.UserUpdated,
                    EmailModel = new EmailModel()
                    {
                        RecipientName = output.FirstName + " " + output.LastName,
                        RecipientEmailAddress = output.Email,
                        RecipientUUID = output.Id
                    }
                };
                bool sent = await emailService.SendEmail();


            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }


    }


}
