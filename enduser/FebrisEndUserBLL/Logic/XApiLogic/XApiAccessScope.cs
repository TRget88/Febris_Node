// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.SharedServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Febris.PrimaryLogicLayer.Logic.XApiLogic
{
    /// <summary>
    /// The set of xApi Actor UUIDs a request is permitted to read.
    /// <see cref="Unrestricted"/> means tenant-wide (staff). Otherwise access is
    /// limited to <see cref="AllowedActorUuids"/>, which is empty for a denied
    /// caller (default-deny).
    /// </summary>
    public class ActorAccessScope
    {
        public bool Unrestricted { get; set; }
        public HashSet<Guid> AllowedActorUuids { get; set; } = new HashSet<Guid>();
    }

    /// <summary>
    /// FERPA access scoping for xApi reads. Resolves a <see cref="ClaimsPrincipal"/>
    /// to the actors it may read:
    /// admin / IT admin / educator / Febris super admin are Unrestricted (all actors);
    /// a learner (InstitutionUserAccountType.User) gets their own actor only;
    /// a parent/guardian (UserParent) gets exactly the actors of the students linked
    /// to them in ParentLinkedStudent and nothing else; anyone else is denied.
    /// ActorLogic and StatementLogic both consult this so the parent and learner read
    /// paths share one default-deny implementation.
    /// </summary>
    public static class XApiAccessScope
    {
        public static async Task<ActorAccessScope> ResolveAsync(ClaimsPrincipal user, IParentLinkedStudentQueries links = null)
        {
            ActorAccessScope scope = new ActorAccessScope();
            if (user == null)
            {
                return scope; // no principal -> deny
            }

            if (user.IsLocalFebrisAdmin() || user.IsLocalAdmin() || user.IsLocalEducator())
            {
                scope.Unrestricted = true;
                return scope;
            }

            if (user.IsLocalParent())
            {
                // A parent has no learner actor of their own. Their access is exactly
                // the actors of the students linked to them, read live so a newly
                // added link takes effect without the parent re-authenticating.
                if (Guid.TryParse(user.GetUserId(), out Guid parentUserId))
                {
                    links = links ?? new ParentLinkedStudentQueries();
                    List<Guid> actorIds = await links.GetStudentActorIdsForParent(parentUserId);
                    scope.AllowedActorUuids = new HashSet<Guid>(actorIds);
                }
                return scope;
            }

            if (user.IsLocalUser())
            {
                if (user.HasActor() && Guid.TryParse(user.GetActor(), out Guid ownActor))
                {
                    scope.AllowedActorUuids.Add(ownActor);
                }
                return scope;
            }

            return scope; // default deny
        }
    }
}
