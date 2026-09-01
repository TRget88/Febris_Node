// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Febris.ArchitectureTests
{
    /// <summary>
    /// ROADMAP 16: the node's admin writes live on the PORTAL behind cookie auth, and the
    /// NodeAdmin token is gone.
    ///
    /// <para>
    /// WHY. The API carried three admin-only writes (package upload, feed sync, module upload)
    /// reachable only with a Portal-minted 60-minute NON-REVOCABLE bearer token, minted from a
    /// route with no view by a documented curl sequence. The owner's ruling: move the writes to
    /// the Portal behind the cookie Identity and role gates that every other operator action
    /// uses, and the token deletes itself -- it existed solely to let a human reach API-side
    /// writes. These guards pin both halves so neither quietly regresses: the Portal surface
    /// keeps its full gate stack, and the API stays write-free and single-scheme.
    /// </para>
    /// </summary>
    public class PortalWriteSurfaceGuardTests
    {
        private static string Portal(string repoRoot, params string[] parts)
        {
            return Path.Combine(new[] { repoRoot, "enduser", "FebrisEndUserPortal" }.Concat(parts).ToArray());
        }

        private static string Live(string path)
        {
            Assert.True(File.Exists(path), path + " not found");
            return SelfRecursivePropertyGuardTests.StripCommentsPreservingLayout(File.ReadAllText(path));
        }

        /// <summary>The attribute block above one action: from the end of the previous member to
        /// the declaration line. Split on whitespace-only lines, same reasoning as the
        /// reachability guard's attribute scan.</summary>
        private static string AttributeBlockAbove(string live, string declarationPattern)
        {
            Match decl = Regex.Match(live, declarationPattern);
            Assert.True(decl.Success, "declaration not found: " + declarationPattern);
            string[] prior = Regex.Split(live.Substring(0, decl.Index), @"\n[ \t]*\n");
            return prior[prior.Length - 1];
        }

        [Fact]
        public void The_moved_write_actions_carry_the_full_gate_stack()
        {
            // Class-level [Authorize(EducatorAndOrgAdmins)] alone is NOT the ruling's gate: the
            // writes are node-administration, so each action must carry its own OrgAdmins
            // attribute (attributes AND together, narrowing the class gate). Antiforgery is
            // belt-and-braces on top of the global auto-validate filter, matching every other
            // Portal POST. The upload additionally needs a size limit or Kestrel's ~28.6 MB
            // default kills any real package.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string live = Live(Portal(repoRoot, "Controllers", "Data", "Remote", "LocalSoftwarePackageController.cs"));

            // 2026-08-31, owner ruling: the package Upload action and its view were REMOVED.
            // Distribution routes operators to the public download page, and packages reach a
            // node only through the verified feed, never a hand upload. The two Upload gate
            // assertions that stood here went with it. FeedSync keeps its full stack below and
            // matters MORE now, because it is the only remaining ingest path.

            string feedSyncPost = AttributeBlockAbove(live,
                @"public\s+async\s+Task<IActionResult>\s+FeedSync\s*\(PackageFeedSyncRequestViewModel");
            Assert.Contains("[HttpPost]", feedSyncPost);
            Assert.Contains("[ValidateAntiForgeryToken]", feedSyncPost);
            Assert.True(Regex.IsMatch(feedSyncPost, @"\[Authorize\(Roles\s*=\s*Febris\.Constants\.RoleConstants\.OrgAdmins\)\]"),
                "the FeedSync POST writes to the catalog and the artifact store -- OrgAdmins only");

            string feedSyncGet = AttributeBlockAbove(live,
                @"public\s+IActionResult\s+FeedSync\s*\(\)");
            Assert.True(Regex.IsMatch(feedSyncGet, @"\[Authorize\(Roles\s*=\s*Febris\.Constants\.RoleConstants\.OrgAdmins\)\]"),
                "the FeedSync form must carry the same OrgAdmins gate as its POST");

            string moduleLive = Live(Portal(repoRoot, "Controllers", "Data", "Remote", "ModuleController.cs"));
            string moduleCreate = AttributeBlockAbove(moduleLive,
                @"public\s+async\s+Task<IActionResult>\s+Create\s*\(ModulePackageUploadViewModel");
            Assert.Contains("[ValidateAntiForgeryToken]", moduleCreate);
            Assert.Contains("[RequestSizeLimit(", moduleCreate);
        }

        [Fact]
        public void The_api_write_surface_stays_deleted()
        {
            // The API had exactly three admin writes. All three moved to the Portal, and the
            // SoftwarePackageController went entirely (its reads had zero in-repo callers --
            // devices fetch through CompanionApp, humans through the Portal). An [HttpPost]
            // growing back on the API's distribution or module surface means either a write
            // returned to the wrong tier or a new credential is about to be invented for it.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string controllers = Path.Combine(repoRoot, "enduser", "FebrisEndUserApi", "Controllers");

            Assert.False(File.Exists(Path.Combine(controllers, "SoftwarePackageController.cs")),
                "the API SoftwarePackageController came back -- ROADMAP 16 consolidated distribution to the Portal (humans) and CompanionApp (devices)");

            foreach (string file in new[] { "ModuleController.cs", "CompanionAppController.cs" })
            {
                string live = Live(Path.Combine(controllers, file));
                Assert.False(Regex.IsMatch(live, @"\[HttpPost"),
                    "enduser/FebrisEndUserApi/Controllers/" + file + " has grown a POST action -- the node's admin writes belong on the Portal behind cookie auth (ROADMAP 16)");
            }
        }

        [Fact]
        public void The_node_admin_credential_stays_deleted()
        {
            // The 60-minute non-revocable bearer and both halves of its machinery: the mint
            // (NodeAdminAuthorization + NodeAdminTokenController), the middleware attach
            // (Items["NodeAdmin"]), and the composed filter flags (AllowNodeAdmin /
            // RequireNodeAdmin). Comment-stripped scan over the whole enduser tier and the
            // shared model library, so a historical comment can explain the deletion without
            // tripping the guard while any LIVE reintroduction trips it immediately.
            string repoRoot = ProjectGraph.FindRepoRoot();
            var offenders = new System.Collections.Generic.List<string>();
            foreach (string root in new[]
            {
                Path.Combine(repoRoot, "enduser"),
            })
            {
                foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (ProjectGraph.IsInBuildOutput(file))
                    {
                        continue;
                    }
                    string live = SelfRecursivePropertyGuardTests.StripCommentsPreservingLayout(File.ReadAllText(file));
                    if (Regex.IsMatch(live, @"\bAllowNodeAdmin\b|\bRequireNodeAdmin\b|\bNodeAdminAuthorization\b|Items\[""NodeAdmin""\]|\bNodeAdminTokenController\b|\bINodeAdminAuthorization\b"))
                    {
                        offenders.Add(ProjectGraph.Rel(repoRoot, file));
                    }
                }
            }

            Assert.True(offenders.Count == 0,
                "the NodeAdmin credential machinery is back in live code (ROADMAP 16 deleted it -- the admin writes moved to the Portal precisely so no API bearer credential exists):\n  " +
                string.Join("\n  ", offenders));
        }

        [Fact]
        public void The_upsert_uuid_fields_stay_on_both_forms()
        {
            // ROADMAP 16 row 3: without a UUID input, every submit posts null and the ingest
            // mints a fresh catalog row, a fresh stored .zip and (for modules) a fresh xAPI
            // activity, orphaning the old set. The upsert-by-UUID path has existed in the shared
            // ingest logic all along -- these two inputs are what expose it to the operator.
            string repoRoot = ProjectGraph.FindRepoRoot();
            foreach (string view in new[]
            {
                Portal(repoRoot, "Views", "Module", "Create.cshtml"),
            })
            {
                string live = Regex.Replace(File.ReadAllText(view), @"@\*.*?\*@", " ", RegexOptions.Singleline);
                // The INPUT specifically: a first version of this guard accepted any
                // asp-for="UUID", and the mutation run proved the surviving <label> satisfied it
                // with the input gone. A label without an input posts nothing.
                Assert.True(Regex.IsMatch(live, @"<input\s[^>]*asp-for=""UUID"""),
                    ProjectGraph.Rel(repoRoot, view) + " lost its UUID input -- re-uploads orphan instead of replacing (ROADMAP 16 row 3)");
            }
        }
    }
}
