// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;

namespace Febris.UserNode.LogicLayer.Logic.HealthLogic
{
    /// <summary>
    /// The Identity roles a delivery node requires to provision and authorize accounts -- the SINGLE
    /// source of truth for both the boot seed (<c>SeedData.SeedRolesAsync</c>, which now fails startup
    /// if any is absent) and the runtime readiness probe (<see cref="IdentityRolesHealthCheck"/>), so the
    /// two can never drift. Without these roles, <c>UserManager.AddToRoleAsync</c> THROWS and every
    /// account creation 500s.
    /// </summary>
    public static class NodeIdentityRoles
    {
        /// <summary>Roles that MUST exist for the node to provision and authorize accounts.</summary>
        public static readonly string[] Required =
        {
            InstitutionUserAccountType.User.ToString(),
            InstitutionUserAccountType.Educator.ToString(),
            InstitutionUserAccountType.Admin.ToString(),
            InstitutionUserAccountType.ITAdmin.ToString(),
            InstitutionUserAccountType.UserParent.ToString()
            // SuperAdmin removed (owner ruling 2026-08-01). It is a FebrisUserType -- a VENDOR
            // staff role added when Febris offered hosted support. Support is not offered, so a
            // self-hosted node has no reason to mint it. ITAdmin is the node's top local role and
            // satisfies every policy the bootstrap admin needs: IsLocalAdmin() returns true for
            // Admin OR ITAdmin, and EndUserAll / OrgAdmins / EducatorAndOrgAdmins all list ITAdmin.
            // The vendor-staff policies (FebrisStaff, FebrisEmployeeAndSystemAdmins,
            // FebrisSystemAdminsSpaced, OrgStaffLegacy) are commented out at every node call site,
            // so nothing loses reachability. RoleConstants itself is NOT edited: it lives in the
            // shared EnumLibrary that the hub also compiles, and a role string no node user holds
            // is simply never matched.
        };
    }
}
