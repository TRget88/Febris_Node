// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.Models.XApiModels.ModifiedForSharing;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.UserNode.DataAccessLayer.Queries.XAPIQueries;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Febris.PrimaryLogicLayer.Logic.XApiLogic
{
    public interface IStatementVoidingLogic
    {
        /// <summary>
        /// Voids a statement. Returns false for anything it cannot positively justify: not an
        /// admin, no such statement, or already voided.
        /// </summary>
        Task<bool> Void(Guid statementUuid);
    }

    /// <summary>
    /// xAPI voiding (T5). A statement that turns out to be wrong is RETRACTED, never edited and
    /// never deleted: a new statement is issued with the verb
    /// <c>http://adlnet.gov/expapi/verbs/voided</c> referencing the target, and the target stops
    /// counting.
    ///
    /// <para>
    /// This restores a feature that existed in the 2021 Portal
    /// (<c>xAPIController.VoidStatementConfirmed</c>, retired SVN) and did not survive the port into
    /// this repo -- only the debris did: the seeded verb, an orphaned button partial no view renders,
    /// a dead JS route, and the storage path commented out in three places.
    /// </para>
    ///
    /// <para>
    /// Three things the 2021 version did are NOT reproduced. It overwrote the target's verb with
    /// <c>voided</c>, which destroyed the record of what the learner actually did and made its own
    /// Unvoid unable to restore anything. It wrote the voiding statement to a JSON file instead of
    /// the table, so nothing could query or export it. And it never excluded voided statements from
    /// a single query, so voiding changed a verb and nothing else.
    /// </para>
    ///
    /// <para>
    /// <b>One-way, by the owner's ruling and the spec.</b> There is no unvoid. A mistaken void is
    /// corrected by issuing a new statement, not by reversing the old one, which is why
    /// <c>VoidedAt</c> is only ever set and never cleared.
    /// </para>
    ///
    /// <para>
    /// <b>Admin and up.</b> The 2021 gate was vendor-only (<c>FebrisEmployee</c>, <c>SuperAdmin</c>,
    /// <c>SystemAdmin</c>) and cannot be copied: a node never mints those, and SuperAdmin was
    /// removed from the node's seeded roles by owner ruling on 2026-08-01.
    /// <c>IsLocalAdmin()</c> covers Admin and ITAdmin, which is the node's "admin and up".
    /// </para>
    /// </summary>
    public class StatementVoidingLogic : IStatementVoidingLogic
    {
        /// <summary>The spec verb. Seeded already -- this resolves it rather than minting one.</summary>
        public const string VoidedVerbId = "http://adlnet.gov/expapi/verbs/voided";

        private readonly IStatementQueries _context;
        private readonly IVerbQueries _verbContext;
        private readonly IObjectQueries _objectContext;
        private readonly IActorQueries _actorContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;

        public StatementVoidingLogic(
            IHttpContextAccessor httpContextAccessor,
            IStatementQueries context,
            IVerbQueries verbContext,
            IObjectQueries objectContext,
            IActorQueries actorContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _verbContext = verbContext;
            _objectContext = objectContext;
            _actorContext = actorContext;
            User = _httpContextAccessor?.HttpContext?.User;
        }

        public async Task<bool> Void(Guid statementUuid)
        {
            try
            {
                if (User == null || !User.IsLocalAdmin())
                {
                    // Default deny, matching every other scoped operation on this node.
                    return false;
                }
                if (statementUuid == Guid.Empty)
                {
                    return false;
                }

                // MUST see voided rows: an already-voided statement is invisible to every ordinary
                // read, and without this a second void would look like "no such statement" rather
                // than the no-op it is.
                LocalStatement target = await _context.GetIncludingVoided(statementUuid);
                if (target == null)
                {
                    FebrisLog.Warn("Void refused: no statement '" + statementUuid + "'.");
                    return false;
                }
                if (target.VoidedAt.HasValue)
                {
                    // Idempotent, and deliberately not an error: voiding is one-way, so a repeat is
                    // a no-op rather than a failure.
                    return false;
                }

                await WriteVoidingStatement(target);

                // The target itself is NEVER altered beyond these two fields. Its verb, result,
                // context and attachments stay exactly as the producer sent them, which is the whole
                // difference between this and the 2021 implementation.
                target.VoidedAt = DateTime.UtcNow;
                target.VoidedByUserId = ResolveOperatorUserId();
                await _context.MarkVoided(target.Id, target.VoidedAt.Value, target.VoidedByUserId);

                FebrisLog.Info("Statement " + statementUuid + " voided by user " +
                    (target.VoidedByUserId?.ToString() ?? "unknown") + ".");
                return true;
            }
            catch (Exception ex)
            {
                FebrisLog.Error(ex, "StatementVoidingLogic.Void");
                return false;
            }
        }

        private Guid? ResolveOperatorUserId()
        {
            return Guid.TryParse(User.GetUserId(), out Guid userId) ? userId : (Guid?)null;
        }

        /// <summary>
        /// Records the xAPI artifact: a real statement, in the table, with the voided verb and an
        /// object referencing the target.
        ///
        /// <para>
        /// The object is stored as an Activity row carrying <c>ObjectType = "StatementRef"</c> and
        /// an <c>urn:uuid:</c> id, because <c>LocalStatement.ObjectId</c> is a foreign key to
        /// <c>Object</c> and there is no way in this model for the object POSITION to hold a
        /// statement reference. <c>StatementReference</c> exists but hangs off <c>Context</c>. This
        /// keeps the spec's shape within the schema that exists.
        /// </para>
        ///
        /// <para>
        /// Best-effort by design: a failure here is logged and the void still proceeds. The MARK is
        /// what stops the statement counting, and refusing to retract a wrong learner record because
        /// its audit artifact could not be written would be the worse outcome.
        /// </para>
        /// </summary>
        private async Task WriteVoidingStatement(LocalStatement target)
        {
            try
            {
                Verb voidedVerb = await _verbContext.Get(new Uri(VoidedVerbId));
                if (voidedVerb == null)
                {
                    FebrisLog.Warn("Voided verb is not seeded, so no voiding statement was recorded for " +
                        target.UUID + ". The statement is still voided.");
                    return;
                }

                // The voiding actor is the ADMIN performing it, when they have an xAPI actor. Many
                // admins do not -- the Actor claim is only issued to accounts with one -- and
                // attributing the void to the TARGET's actor would assert the learner retracted
                // their own record, which is false. Better to record no voiding statement than a
                // misleading one; VoidedByUserId keeps the operator attributable either way.
                Actor voidingActor = null;
                if (User.HasActor() && Guid.TryParse(User.GetActor(), out Guid actorUuid))
                {
                    voidingActor = await _actorContext.Get(actorUuid);
                }
                if (voidingActor == null)
                {
                    FebrisLog.Warn("Voiding admin has no xAPI actor, so no voiding statement was recorded for " +
                        target.UUID + ". The statement is still voided and the operator is on VoidedByUserId.");
                    return;
                }

                ModelLibrary.Models.XApiModels.Object statementRef = await _objectContext.Create(
                    new ModelLibrary.Models.XApiModels.Object
                    {
                        ObjectType = "StatementRef",
                        Id = new Uri("urn:uuid:" + target.UUID)
                    });

                await _context.Create(new LocalStatement
                {
                    Actor = voidingActor,
                    VerbId = voidedVerb.Key,
                    VerbUUID = voidedVerb.UUID,
                    ObjectId = statementRef.Key,
                    ObjectUUID = statementRef.UUID,
                    Timestamp = DateTime.UtcNow,
                    Stored = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                FebrisLog.Error(ex, "StatementVoidingLogic.WriteVoidingStatement: the statement is still voided");
            }
        }
    }
}
