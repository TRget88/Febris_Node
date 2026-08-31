// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.SharedServices;
using Febris.UserNode.LogicLayer.Logic.HealthLogic;
using Febris.UserNode.LogicLayer.Logic.IdentityLogic;
using Febris.UserNode.Portal.IdentityPolicy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Febris.UserNode.Portal.Controllers
{
    /// <summary>
    /// Account invitations (invitation flow 2026-08-21). An operator names someone, the node emails
    /// them a one-time link, and the account is created when they accept -- so an invitation nobody
    /// takes up leaves nothing behind.
    ///
    /// <para>
    /// Gated to <c>EducatorAndOrgAdmins</c> rather than <c>OrgAdmins</c>, because inviting a class
    /// is an educator's job and <c>UserLogic.Create</c> already admits educators to account
    /// creation. What an educator may GRANT is a separate question, answered by
    /// <c>RoleRankPolicy</c> in the logic layer: the role list this page renders is filtered by the
    /// same policy that the issue path enforces, so the form cannot offer a choice the POST would
    /// refuse.
    /// </para>
    ///
    /// <para>
    /// Invitations work in EVERY registration mode. The mode governs whether unauthenticated
    /// strangers may sign themselves up; an invited person was named by someone entitled to create
    /// their account outright. Tying invitations to Invite mode would also mean flipping the mode
    /// back silently strands every outstanding link, which is exactly the kind of quiet breakage
    /// this feature exists to remove.
    /// </para>
    /// </summary>
    [Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]
    public class InvitationsController : Controller
    {
        /// <summary>TempData key carrying the one-time accept link across the post-redirect-get.
        /// TempData rather than a field because the page redirects after issuing, and rather than a
        /// session value because it must survive exactly one render and no more.</summary>
        private const string IssuedUrlKey = "InvitationIssuedUrl";
        private const string IssuedEmailKey = "InvitationIssuedEmail";
        private const string IssuedMailFailedKey = "InvitationIssuedMailFailed";

        private readonly INodeInvitationLogic _invitations;
        private readonly IRegistrationPolicy _registrationPolicy;
        private readonly IEmailSender _emailSender;

        /// <summary>DI constructor (the only one).</summary>
        public InvitationsController(
            INodeInvitationLogic invitations,
            IRegistrationPolicy registrationPolicy,
            IEmailSender emailSender)
        {
            _invitations = invitations;
            _registrationPolicy = registrationPolicy;
            _emailSender = emailSender;
        }

        // GET: /Invitations
        /// <summary>The invitation list plus the issue form.</summary>
        public async Task<IActionResult> Index()
        {
            return View(await BuildModel());
        }

        // POST: /Invitations/Issue
        /// <summary>Create an invitation, email the link, and show it once in case the mail did not
        /// go out.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Issue(InvitationIssueInputModel input)
        {
            NodeInviteIssueResult result = await _invitations.Issue(input, ActorUserId(), ActorEmail());
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "The invitation could not be created.");
                return View("Index", await BuildModel());
            }

            string acceptUrl = AcceptUrlFor(result.RawToken);

            // MAIL FAILURE MUST NOT LOSE THE INVITATION. EmailService rethrows on any send failure,
            // and the row is already committed by this point, so an uncaught throw here would 500
            // the operator after creating an invitation they never learn about. Several existing
            // node call sites have exactly that shape; this one does not.
            bool mailed = true;
            try
            {
                // The IEmailSender contract in this codebase takes an EmailType NAME where a subject
                // belongs and the URL where a body belongs (see Register.SendConfirmationEmailAsync).
                await _emailSender.SendEmailAsync(
                    result.Invite.Email, EmailType.NodeUserInvite.ToString(), acceptUrl);
            }
            catch (Exception ex)
            {
                mailed = false;
                FebrisLog.Error(ex, "[invitation] created but the email could not be sent");
            }

            TempData[IssuedUrlKey] = acceptUrl;
            TempData[IssuedEmailKey] = result.Invite.Email;
            TempData[IssuedMailFailedKey] = !mailed;
            TempData["StatusMessage"] = mailed
                ? "Invitation sent to " + result.Invite.Email + "."
                : "Invitation created for " + result.Invite.Email + ", but the email could not be sent. Send them the link below.";

            return RedirectToAction(nameof(Index));
        }

        // POST: /Invitations/Revoke
        /// <summary>Cancel an outstanding invitation.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(Guid uuid)
        {
            bool revoked = await _invitations.Revoke(uuid, ActorEmail());
            TempData["StatusMessage"] = revoked
                ? "Invitation cancelled."
                : "That invitation was already used or cancelled.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Absolute accept URL for a token.
        ///
        /// <para>
        /// TWO THINGS HERE ARE LOAD-BEARING AND NEITHER IS OBVIOUS.
        /// </para>
        /// <para>
        /// The URL must be ABSOLUTE. The invitation email template only renders its button when the
        /// link parses as an absolute http/https URI (the SCBA-B4 guard), so a relative
        /// <c>Url.Page</c> result would send a mail with no button and no error anywhere.
        /// </para>
        /// <para>
        /// The token parameter must be named <c>code</c>. The analytics middleware records
        /// <c>Request.QueryString</c> on every request into a table rendered to org admins, and
        /// <c>SensitiveQueryRedactor</c> blanks the values of a fixed key list that includes
        /// <c>code</c> -- the ASP.NET Identity convention. Naming it anything else would put live
        /// invitation tokens in the analytics table for any admin to read, which is precisely
        /// finding H-26 reopened. <c>NodeInvitationAcceptUrlTests</c> pins both properties.
        /// </para>
        /// <para>
        /// AND THE EMAIL IS DELIBERATELY NOT IN THE LINK, for two reasons. It would hand the
        /// recipient-binding answer to whoever holds the link, turning a forwarded email back into
        /// account transfer -- the exact defect the central invite flow documents and this one
        /// fixes. And <c>SensitiveQueryRedactor</c> explicitly does NOT redact <c>email</c> (it
        /// reasons that treating identifiers as secrets is a PII-retention decision made elsewhere),
        /// so an address in this query would be retained verbatim in the analytics table. The
        /// invitee does not need it prefilled: the invitation arrived at that address.
        /// </para>
        /// </summary>
        public string AcceptUrlFor(string rawToken)
        {
            return Url.Page(
                "/Account/AcceptInvitation",
                pageHandler: null,
                values: new { area = "Identity", code = rawToken },
                protocol: Request.Scheme);
        }

        /// <summary>The roles this operator may grant, filtered by the same rank policy the issue
        /// path enforces so the form cannot offer a choice the POST would refuse.</summary>
        private List<string> AssignableRoles()
        {
            IList<string> actorRoles = ActorRoles();
            return NodeIdentityRoles.Required
                .Where(role => RoleRankPolicy.CanAssign(actorRoles, new string[0], role))
                .ToList();
        }

        private IList<string> ActorRoles()
        {
            return User?.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList() ?? new List<string>();
        }

        /// <summary>The signed-in operator's Identity id, for the invitation's audit stamp.
        /// <see cref="Guid.Empty"/> when the claim is absent or unparseable -- an unrecorded issuer
        /// is honest, an invented one is not.</summary>
        private Guid ActorUserId()
        {
            string raw = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid parsed;
            return Guid.TryParse(raw, out parsed) ? parsed : Guid.Empty;
        }

        private string ActorEmail()
        {
            string email = User?.FindFirst(ClaimTypes.Email)?.Value;
            if (!string.IsNullOrWhiteSpace(email))
            {
                return email;
            }
            return string.IsNullOrWhiteSpace(User?.Identity?.Name) ? null : User.Identity.Name;
        }

        /// <summary>Compose the page model, flattening each invitation's lifecycle into a single
        /// state so the view holds no date arithmetic.</summary>
        private async Task<InvitationsPageViewModel> BuildModel()
        {
            List<NodeUserInvite> invites = await _invitations.Get() ?? new List<NodeUserInvite>();
            DateTime now = DateTime.UtcNow;

            // Names resolved in ONE read rather than per row. Archived cohorts are included here on
            // purpose: a row whose cohort was retired since issue should still say which one it was.
            Dictionary<Guid, string> cohortNames = await _invitations.CohortNames();

            return new InvitationsPageViewModel()
            {
                Invitations = invites.Select(i => new InvitationRowViewModel()
                {
                    Uuid = i.UUID,
                    Email = i.Email,
                    Name = string.Join(" ", new[] { i.FirstName, i.LastName }
                        .Where(part => !string.IsNullOrWhiteSpace(part))),
                    Role = i.Role,
                    CohortName = i.CohortUUID.HasValue && cohortNames.ContainsKey(i.CohortUUID.Value)
                        ? cohortNames[i.CohortUUID.Value]
                        : null,
                    IssuedByEmail = i.IssuedByEmail,
                    IssuedAtUtc = i.TimeStamp,
                    ExpiresAtUtc = i.ExpiresAt,
                    State = StateOf(i, now),
                    ClosedAtUtc = i.ConsumedAt ?? i.RevokedAt,
                    RevokedByEmail = i.RevokedByEmail
                }).ToList(),
                AssignableRoles = AssignableRoles(),
                AvailableCohorts = await _invitations.CohortOptions(),
                EffectiveRegistrationMode = _registrationPolicy.Mode.ToString(),
                IssuedAcceptUrl = TempData[IssuedUrlKey] as string,
                IssuedForEmail = TempData[IssuedEmailKey] as string,
                IssuedEmailFailed = TempData[IssuedMailFailedKey] as bool? ?? false
            };
        }

        /// <summary>
        /// One invitation's lifecycle state. Same precedence as the logic layer's validation:
        /// revoked beats consumed beats expired. Duplicated here rather than shared because the list
        /// classifies rows it already holds while validation classifies a token it has to look up,
        /// and pinning both against the same table of cases is cheaper than a shared abstraction
        /// over two different inputs. <c>NodeInvitationLogicTests</c> asserts they agree.
        /// </summary>
        public static InviteState StateOf(NodeUserInvite invite, DateTime nowUtc)
        {
            if (invite == null)
            {
                return InviteState.NotFound;
            }
            if (invite.RevokedAt.HasValue)
            {
                return InviteState.Revoked;
            }
            if (invite.ConsumedAt.HasValue)
            {
                return InviteState.AlreadyConsumed;
            }
            if (invite.ExpiresAt <= nowUtc)
            {
                return InviteState.Expired;
            }
            return InviteState.Active;
        }
    }
}
