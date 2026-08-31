// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.HealthLogic;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.IdentityLogic
{
    /// <summary>
    /// Account invitations for this node: issue, list, revoke, validate and consume.
    ///
    /// <para>
    /// An invitation is ADMIN-INITIATED account creation with the password step delegated to the
    /// recipient. It is deliberately not gated on the registration MODE. The mode governs whether
    /// unauthenticated strangers may sign themselves up; an invited person is not a stranger, they
    /// were named by someone with the authority to create their account outright. Making invitations
    /// require Invite mode would also mean an operator who flips the mode back strands every
    /// outstanding invitation, which is the sort of quiet breakage this feature exists to avoid.
    /// </para>
    ///
    /// <para>
    /// THREE DEFECTS OF THE CENTRAL DEVELOPER-ORG INVITE FLOW ARE FIXED HERE, all documented in
    /// <see cref="ContentDeveloperUserInvite"/>'s own source: the token is hashed at rest rather
    /// than stored as a readable UUID, the recipient-email binding is actually ENFORCED rather than
    /// shipped-but-uncalled, and an invitation can be revoked.
    /// </para>
    /// </summary>
    public interface INodeInvitationLogic
    {
        /// <summary>Every invitation, newest first, for the admin list.</summary>
        Task<List<NodeUserInvite>> Get();

        /// <summary>How many invitations are currently redeemable. Shown on the registration
        /// settings page so closing a node does not silently strand people.</summary>
        Task<int> CountActive();

        /// <summary>
        /// Issue an invitation. Returns the RAW TOKEN alongside the row -- the only moment it
        /// exists outside the emailed link, because the row stores only its hash.
        /// <para>
        /// The caller must build the accept URL and send the mail: this layer has no
        /// <c>IUrlHelper</c> and an absolute URL cannot be produced without one.
        /// </para>
        /// </summary>
        Task<NodeInviteIssueResult> Issue(InvitationIssueInputModel input, Guid issuerUserId, string issuerEmail);

        /// <summary>Cancel an outstanding invitation. False when it was already used or cancelled.</summary>
        Task<bool> Revoke(Guid uuid, string revokedByEmail);

        /// <summary>Cohorts the issue form can offer, newest first. Empty when the node has none.</summary>
        Task<List<InvitationCohortOption>> CohortOptions();

        /// <summary>Resolve cohort UUIDs to display names for the admin list. A cohort missing from
        /// the result is simply absent rather than an error, so one deleted since issue does not
        /// break the page.</summary>
        Task<Dictionary<Guid, string>> CohortNames();

        /// <summary>
        /// OPTIONAL cohort linkage on acceptance (2026-08-21). A no-op when the invitation carries
        /// no cohort. NEVER THROWS and never blocks the acceptance: the account already exists by
        /// the time this runs, and a cohort archived or deleted in the days since issue must not
        /// make an invitation unredeemable. Returns whether a link was actually made.
        /// </summary>
        Task<bool> LinkAcceptedUserToCohort(NodeUserInvite invite, Guid acceptedUserId);

        /// <summary>
        /// Look an invitation up by raw token and classify it. NEVER reveals whether the address is
        /// registered, and returns the row only when it is <see cref="InviteState.Active"/> so a
        /// caller cannot accidentally act on a dead one.
        /// </summary>
        Task<NodeInviteValidation> Validate(string rawToken);

        /// <summary>
        /// Atomically claim the invitation for <paramref name="consumedByUserId"/>. Returns false
        /// when another request won the race. The caller creates the account only after this
        /// returns true.
        /// </summary>
        Task<bool> Consume(Guid uuid, Guid consumedByUserId);
    }

    /// <summary>
    /// DI-only implementation of <see cref="INodeInvitationLogic"/>. Greenfield node logic,
    /// deliberately NO legacy self-newing constructor. Non-<c>*Queries</c>, so the DAL convention
    /// sweep does not cover it and the host registers it explicitly.
    /// </summary>
    public class NodeInvitationLogic : INodeInvitationLogic
    {
        /// <summary>Default lifetime of an invitation, matching the central flow's stated seven
        /// days and the wording already baked into the invitation email template.</summary>
        public const int DefaultExpiryDays = 7;

        /// <summary>Upper bound on the requested lifetime. An invitation that outlives a school term
        /// is a standing offer of an account, not an invitation.</summary>
        public const int MaxExpiryDays = 30;

        private readonly INodeUserInviteQueries _inviteContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICohortQueries _cohortContext;
        private readonly ICohortMemberQueries _memberContext;

        /// <summary>DI constructor (the only one).</summary>
        public NodeInvitationLogic(
            INodeUserInviteQueries inviteContext,
            IHttpContextAccessor httpContextAccessor,
            ICohortQueries cohortContext,
            ICohortMemberQueries memberContext)
        {
            _inviteContext = inviteContext;
            _httpContextAccessor = httpContextAccessor;
            _cohortContext = cohortContext;
            _memberContext = memberContext;
        }

        /// <inheritdoc />
        public async Task<List<NodeUserInvite>> Get()
        {
            return await _inviteContext.Get();
        }

        /// <inheritdoc />
        public async Task<int> CountActive()
        {
            return await _inviteContext.CountActive();
        }

        /// <inheritdoc />
        public async Task<NodeInviteIssueResult> Issue(
            InvitationIssueInputModel input, Guid issuerUserId, string issuerEmail)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string email = (input.Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email) || email.IndexOf('@') <= 0)
            {
                return NodeInviteIssueResult.Failed("Enter the email address to invite.");
            }

            // ROLE EXISTS. Checked BEFORE the rank gate, and not merely for tidiness: a test proved
            // the rank policy ALLOWS an unrecognized role name. RoleRankPolicy.RankOf returns no
            // rank for a string it does not know, and an unranked role is below every actor, so
            // "NotARole" sails through CanAssign. Storing it would mint an invitation that throws
            // days later, on acceptance, in front of the invitee -- UserManager.AddToRoleAsync
            // THROWS rather than returning a failed result when the role does not exist.
            //
            // NodeIdentityRoles.Required is the same list the boot seed and the readiness probe use,
            // so this cannot drift from the roles the node actually has.
            string requestedRole = (input.Role ?? string.Empty).Trim();
            bool roleExists = NodeIdentityRoles.Required.Any(
                r => string.Equals(r, requestedRole, StringComparison.OrdinalIgnoreCase));
            if (!roleExists)
            {
                FebrisLog.Warn("[invitation] denied -- no such role: " + requestedRole);
                return NodeInviteIssueResult.Failed("That is not a role on this node.");
            }

            // ROLE RANK. The same gate UserLogic.Create applies, for the same reason: an Educator
            // may invite a User but must not be able to mint an Admin. A new account holds no roles,
            // so the target rank is empty and only the granted role is in question. This is why the
            // check lives in the logic layer rather than the controller -- an invitation grants a
            // role, so it is an escalation door and belongs behind the same policy as the other one.
            if (!RoleRankPolicy.CanAssign(ActorRoles(), new string[0], requestedRole))
            {
                FebrisLog.Warn("[invitation] denied -- actor may not grant " + requestedRole);
                return NodeInviteIssueResult.Failed("You cannot invite someone at that role.");
            }

            // One outstanding invitation per address. Stacking them means several live links for one
            // person, only one of which can ever be redeemed, and the rest look broken.
            List<NodeUserInvite> existing = await _inviteContext.GetActiveFor(email);
            if (existing != null && existing.Count > 0)
            {
                return NodeInviteIssueResult.Failed(
                    "There is already an outstanding invitation for that address. Revoke it first to send a new one.");
            }

            // 256 bits from a CSPRNG, hashed for storage. Shared with the device-credential
            // primitive rather than reimplemented: the reasoning transfers exactly (nothing to
            // guess, and the lookup is BY the hash so it must be deterministic).
            string rawToken = DeviceCredential.Generate();
            string tokenHash = DeviceCredential.Hash(rawToken);

            NodeUserInvite created = await _inviteContext.Create(new NodeUserInvite()
            {
                Email = email,
                TokenHash = tokenHash,
                Role = requestedRole,
                FirstName = string.IsNullOrWhiteSpace(input.FirstName) ? null : input.FirstName.Trim(),
                LastName = string.IsNullOrWhiteSpace(input.LastName) ? null : input.LastName.Trim(),
                IssuedByUserId = issuerUserId,
                IssuedByEmail = string.IsNullOrWhiteSpace(issuerEmail) ? null : issuerEmail.Trim(),
                ExpiresAt = DateTime.UtcNow.AddDays(ClampExpiryDays(input.ExpiresInDays)),
                CohortUUID = input.CohortUUID
            });

            // Audit line: the node has no audit table (see ParentLinkLogic for the same note), so
            // this records through the existing logging path. The TOKEN IS NOT LOGGED -- only that
            // an invitation exists, for whom, at what role, and by whom.
            FebrisLog.Warn(string.Format(
                "[invitation] issued to {0} at role {1} by {2}; expires {3}; cohort={4}",
                created.Email, created.Role, created.IssuedByEmail ?? "(unrecorded)",
                created.ExpiresAt.ToString("u"),
                created.CohortUUID.HasValue ? created.CohortUUID.Value.ToString() : "(none)"));

            return NodeInviteIssueResult.Succeeded(created, rawToken);
        }

        /// <inheritdoc />
        public async Task<bool> Revoke(Guid uuid, string revokedByEmail)
        {
            bool revoked = await _inviteContext.MarkRevoked(uuid, revokedByEmail, DateTime.UtcNow);
            if (revoked)
            {
                FebrisLog.Warn(string.Format(
                    "[invitation] revoked {0} by {1}", uuid, revokedByEmail ?? "(unrecorded)"));
            }
            return revoked;
        }

        /// <inheritdoc />
        public async Task<NodeInviteValidation> Validate(string rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
            {
                return new NodeInviteValidation() { State = InviteState.NotFound };
            }

            NodeUserInvite invite = await _inviteContext.GetByTokenHash(DeviceCredential.Hash(rawToken.Trim()));
            if (invite == null)
            {
                return new NodeInviteValidation() { State = InviteState.NotFound };
            }

            // Ordered most-deliberate first. A revoked invitation that has also expired should say
            // revoked, because somebody decided that, whereas expiry merely happened.
            if (invite.RevokedAt.HasValue)
            {
                return new NodeInviteValidation() { State = InviteState.Revoked };
            }
            if (invite.ConsumedAt.HasValue)
            {
                return new NodeInviteValidation() { State = InviteState.AlreadyConsumed };
            }
            if (invite.ExpiresAt <= DateTime.UtcNow)
            {
                return new NodeInviteValidation() { State = InviteState.Expired };
            }

            // The row travels ONLY on the active path. A dead invitation hands back no email, no
            // role and no name, so a caller cannot accidentally act on one, and a probe cannot
            // learn who an expired invitation was for.
            return new NodeInviteValidation() { State = InviteState.Active, Invite = invite };
        }

        /// <inheritdoc />
        public async Task<bool> Consume(Guid uuid, Guid consumedByUserId)
        {
            return await _inviteContext.MarkConsumed(uuid, consumedByUserId, DateTime.UtcNow);
        }

        /// <inheritdoc />
        public async Task<List<InvitationCohortOption>> CohortOptions()
        {
            // Get() EXCLUDES archived cohorts, which is right for a picker: an operator choosing one
            // now should not be offered a retired class. The archived-inclusive read below answers a
            // different question -- resolving a choice already made.
            List<Cohort> cohorts = await _cohortContext.Get();
            if (cohorts == null)
            {
                return new List<InvitationCohortOption>();
            }
            return cohorts
                .Where(c => c != null && c.UUID != Guid.Empty)
                .Select(c => new InvitationCohortOption() { Uuid = c.UUID, Name = c.Name })
                .ToList();
        }

        /// <inheritdoc />
        public async Task<Dictionary<Guid, string>> CohortNames()
        {
            var names = new Dictionary<Guid, string>();
            List<Cohort> cohorts = await _cohortContext.GetIncludingArchived();
            if (cohorts == null)
            {
                return names;
            }
            foreach (Cohort c in cohorts)
            {
                if (c != null && c.UUID != Guid.Empty && !names.ContainsKey(c.UUID))
                {
                    names[c.UUID] = c.Name;
                }
            }
            return names;
        }

        /// <inheritdoc />
        public async Task<bool> LinkAcceptedUserToCohort(NodeUserInvite invite, Guid acceptedUserId)
        {
            if (invite == null || !invite.CohortUUID.HasValue || acceptedUserId == Guid.Empty)
            {
                return false;
            }

            try
            {
                // The DAL resolves the cohort and creates the member in one call, on purpose. Doing
                // it here -- read through ICohortQueries, assign the result as the navigation --
                // hands EF an AsNoTracking copy, and if the context already tracks that cohort the
                // attach throws and the linkage silently does not happen. A test caught that.
                // Archived cohorts are included by CreateForCohort, which is right: this resolves a
                // selection the issuer already made.
                CohortMember member = await _memberContext.CreateForCohort(acceptedUserId, invite.CohortUUID.Value);
                if (member == null)
                {
                    // DELETED since issue. The account stands; only the linkage is skipped.
                    FebrisLog.Warn(string.Format(
                        "[invitation] {0} accepted, but cohort {1} no longer exists so no membership was created",
                        invite.Email, invite.CohortUUID.Value));
                    return false;
                }

                FebrisLog.Warn(string.Format(
                    "[invitation] {0} accepted and added to cohort {1}", invite.Email, invite.CohortUUID.Value));
                return true;
            }
            catch (Exception ex)
            {
                // NEVER blocks the acceptance. The account is created and signed in by the time this
                // runs, so a failure here costs a cohort membership an operator can add by hand, not
                // the person's account.
                FebrisLog.Error(ex, "[invitation] cohort linkage failed after acceptance for " + invite.Email);
                return false;
            }
        }

        /// <summary>Requested lifetime in days, defaulted and clamped. Clamped rather than rejected
        /// so a mistyped number yields a sane invitation instead of a validation wall.</summary>
        public static int ClampExpiryDays(int? requested)
        {
            if (!requested.HasValue || requested.Value <= 0)
            {
                return DefaultExpiryDays;
            }
            return requested.Value > MaxExpiryDays ? MaxExpiryDays : requested.Value;
        }

        /// <summary>The acting operator's roles from the cookie principal. Same shape as
        /// <c>UserLogic.ActorRoles</c>, which is the other caller of the rank policy.</summary>
        private IList<string> ActorRoles()
        {
            return _httpContextAccessor?.HttpContext?.User?.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList() ?? new List<string>();
        }
    }
}
