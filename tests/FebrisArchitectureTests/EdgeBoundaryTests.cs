// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Febris.ArchitectureTests
{
    /// <summary>
    /// Build-time guard for the core/edge trust boundary documented in
    /// the core API boundary.
    ///
    /// An "edge" deployment (anything under enduser/, pc/, mobile/) ships to an
    /// environment the owner does NOT control. It may reference ONLY the four
    /// sanctioned shared libraries (Febris.EnumLibrary, Febris.ModelLibrary,
    /// Febris.SharedServices, Febris.XApi.Models) plus its own edge projects. It must NEVER reference
    /// the core data-access layer (Febris.SharedDataAccessLayer), the core
    /// business logic layer (Febris.SharedLogicLayer), or any project under
    /// central/, developer/, or marketing/. All core data flows over the core API
    /// (the Remote query pattern), never an in-process DAL or BLL link.
    ///
    /// This test fails the instant a forbidden reference is introduced, which is
    /// the durable guard that judgment and code review did not provide.
    /// </summary>
    public class EdgeBoundaryTests
    {
        // The ONLY shared/central projects an edge deployment may reference.
        private static readonly HashSet<string> AllowedSharedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Febris.EnumLibrary",
            "Febris.ModelLibrary",
            "Febris.SharedServices",
            // The netstandard2.0 xAPI contract keystone that Febris.ModelLibrary sits on
            // (the POCO/EF split). It is a dependency-free contract leaf carrying no core
            // data access, so it is the same sanctioned category as the three above; an edge
            // (e.g. mobile) references it directly because it cannot pull the heavy net8
            // Febris.ModelLibrary. Test #2 below polices that this keystone stays clean.
            "Febris.XApi.Models",
        };

        // Top-level directories whose projects are themselves "edge". The edge
        // may freely reference its own projects.
        private static readonly string[] EdgeRoots = { "enduser", "pc", "mobile" };

        // Top-level directories that are core-only. An edge project referencing
        // anything here is a violation.
        private static readonly string[] CoreRoots = { "central", "developer", "marketing" };

        // Shared-folder projects that are core-only despite living under shared/.
        private static readonly HashSet<string> CoreOnlySharedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Febris.SharedDataAccessLayer",
            "Febris.SharedLogicLayer",
        };

        // GRANDFATHERED, TRACKED violations. Currently EMPTY: the FebrisEndUserApi
        // -> SharedDataAccessLayer/SharedLogicLayer violations were remediated
        // 2026-06-25. Do NOT add entries here without an explicit, tracked
        // remediation plan -- the boundary is the default, not the exception.
        // Format: "<edge project name>|<forbidden referenced project name>".
        private static readonly HashSet<string> KnownTrackedViolations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
        };

        [Fact]
        public void EdgeDeployments_reference_only_allowlisted_shared_projects()
        {
            var repoRoot = ProjectGraph.FindRepoRoot();
            var violations = new List<string>();

            foreach (var edgeCsproj in ProjectGraph.EnumerateProjectFilesUnder(repoRoot, EdgeRoots))
            {
                var edgeName = ProjectGraph.ProjectName(edgeCsproj);
                foreach (var referenced in ProjectGraph.DirectProjectReferences(edgeCsproj))
                {
                    if (IsAllowedForEdge(referenced, repoRoot))
                    {
                        continue;
                    }
                    var key = edgeName + "|" + ProjectGraph.ProjectName(referenced);
                    if (KnownTrackedViolations.Contains(key))
                    {
                        continue; // grandfathered, tracked for removal
                    }
                    violations.Add($"{edgeName} -> {ProjectGraph.ProjectName(referenced)}  ({ProjectGraph.Rel(repoRoot, referenced)})");
                }
            }

            Assert.True(
                violations.Count == 0,
                "Edge deployments may reference ONLY {Febris.EnumLibrary, Febris.ModelLibrary, Febris.SharedServices, Febris.XApi.Models} " +
                "plus their own enduser/pc/mobile projects. Core data must flow through the core API, never an " +
                "in-process DAL/BLL link. Forbidden references found:\n  " +
                string.Join("\n  ", violations.Distinct().OrderBy(v => v, StringComparer.Ordinal)));
        }

        [Fact]
        public void Allowlisted_shared_libs_stay_clean_of_core_data_access()
        {
            // The four crossing libraries must not themselves pull the core
            // DAL/BLL or any central project across the boundary, or the whole
            // allowlist is meaningless.
            var repoRoot = ProjectGraph.FindRepoRoot();
            var violations = new List<string>();

            foreach (var lib in AllowedSharedProjects)
            {
                var csproj = ProjectGraph.FindProjectFile(repoRoot, lib);
                if (csproj == null)
                {
                    continue;
                }
                foreach (var referenced in ProjectGraph.TransitiveProjectReferences(csproj))
                {
                    if (IsForbiddenCoreProject(referenced, repoRoot))
                    {
                        violations.Add($"{lib} -> {ProjectGraph.ProjectName(referenced)}  ({ProjectGraph.Rel(repoRoot, referenced)})");
                    }
                }
            }

            Assert.True(
                violations.Count == 0,
                "A boundary-crossing shared lib must not reference the core DAL/BLL or any central project, or it " +
                "would smuggle core data access into every edge deployment. Found:\n  " +
                string.Join("\n  ", violations.Distinct().OrderBy(v => v, StringComparer.Ordinal)));
        }

        [Fact]
        public void Grandfathered_violations_list_has_no_stale_entries()
        {
            // Ratchet: every grandfathered entry must still correspond to a real
            // current reference, so the exception list can only shrink, never rot.
            var repoRoot = ProjectGraph.FindRepoRoot();
            var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var edgeCsproj in ProjectGraph.EnumerateProjectFilesUnder(repoRoot, EdgeRoots))
            {
                var edgeName = ProjectGraph.ProjectName(edgeCsproj);
                foreach (var referenced in ProjectGraph.DirectProjectReferences(edgeCsproj))
                {
                    current.Add(edgeName + "|" + ProjectGraph.ProjectName(referenced));
                }
            }

            var stale = KnownTrackedViolations.Where(k => !current.Contains(k)).ToList();

            Assert.True(
                stale.Count == 0,
                "These grandfathered entries no longer match a real reference. The boundary violation was fixed, so " +
                "delete the entry from KnownTrackedViolations:\n  " +
                string.Join("\n  ", stale.OrderBy(v => v, StringComparer.Ordinal)));
        }

        // --- edge-specific classification (generic graph helpers live in ProjectGraph) ---

        private static bool IsAllowedForEdge(string csprojPath, string repoRoot)
        {
            if (AllowedSharedProjects.Contains(ProjectGraph.ProjectName(csprojPath)))
            {
                return true;
            }
            // The edge may reference its own projects (under enduser/pc/mobile).
            return ProjectGraph.IsUnderAnyRoot(csprojPath, repoRoot, EdgeRoots);
        }

        private static bool IsForbiddenCoreProject(string csprojPath, string repoRoot)
        {
            if (CoreOnlySharedProjects.Contains(ProjectGraph.ProjectName(csprojPath)))
            {
                return true;
            }
            return ProjectGraph.IsUnderAnyRoot(csprojPath, repoRoot, CoreRoots);
        }
    }
}
