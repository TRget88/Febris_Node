// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Febris.ArchitectureTests
{
    /// <summary>
    /// The node must not redirect to controllers that only exist in the CENTRAL tier.
    ///
    /// <para>
    /// WHY THIS EXISTS, and it is the plan's own warning made executable.
    /// <c>docs/NODE_REMOTE_TEARDOWN_PLAN.md:18</c> records that
    /// <c>RedirectToAction(..., nameof(Purchase), ...)</c> is NOT a compile break, because
    /// <c>nameof</c> binds to the ENTITY type via the <c>Models.DataModels</c> using at the top of
    /// those controllers. It compiles, ships, and 404s at runtime. The plan called it "the easiest
    /// defect to ship by accident" and it was then shipped by accident: two such actions survived
    /// the teardown, and one of them fed a widget slot on <c>CohortMember/DetailsModal</c>, a page
    /// operators actually open, so it spun forever.
    /// </para>
    ///
    /// <para>
    /// A compiler cannot catch this and neither can a unit test of the controller, because the
    /// action is never invoked by anything under test. Only a source rule can.
    /// </para>
    /// </summary>
    public class NoRedirectsToRemovedControllersTests
    {
        /// <summary>
        /// Controllers that belong to the central tier and were removed from the node with the
        /// commerce surface. A redirect naming any of these is a runtime 404.
        /// </summary>
        private static readonly string[] RemovedFromNode =
        {
            "Purchase",
            "PurchaseDispute",
            "Invoice",
            "Disbursement",
            "MarketplaceListing",
            "PrivateMarketplaceListingWhiteList",
        };

        private static string StripComments(string source)
        {
            source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            source = Regex.Replace(source, @"^[ \t]*//.*$", string.Empty, RegexOptions.Multiline);
            return source;
        }

        [Fact]
        public void No_node_controller_redirects_to_a_controller_the_node_does_not_have()
        {
            string repoRoot = ProjectGraph.FindRepoRoot();
            string portal = Path.Combine(repoRoot,
                "enduser".Replace('/', Path.DirectorySeparatorChar),
                "FebrisEndUserPortal", "Controllers");

            Assert.True(Directory.Exists(portal), "expected the node Portal controllers at " + portal);

            List<string> files = Directory.EnumerateFiles(portal, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                .ToList();

            Assert.True(files.Count > 10,
                "expected to scan the node's controllers, found " + files.Count + " -- this guard would pass vacuously");

            List<string> offenders = new List<string>();

            foreach (string file in files)
            {
                string source = StripComments(File.ReadAllText(file));

                foreach (string removed in RemovedFromNode)
                {
                    // Both spellings a redirect can take: nameof(X) and the quoted "X".
                    string byNameof = "nameof(" + removed + ")";
                    bool hasNameof = source.Contains(byNameof, StringComparison.Ordinal)
                        && source.Contains("RedirectToAction", StringComparison.Ordinal);

                    bool hasQuoted = Regex.IsMatch(
                        source,
                        @"RedirectToAction\s*\([^)]*""" + Regex.Escape(removed) + @"""",
                        RegexOptions.Singleline);

                    if (hasNameof || hasQuoted)
                    {
                        offenders.Add(ProjectGraph.Rel(repoRoot, file) + " -> " + removed);
                    }
                }
            }

            Assert.True(offenders.Count == 0,
                "the node redirects to controllers it does not have. These COMPILE (nameof binds to " +
                "the entity type) and 404 at runtime, which is why the teardown plan calls them the " +
                "easiest defect to ship by accident:" + Environment.NewLine +
                string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void The_removed_controllers_really_are_absent_from_the_node()
        {
            // Guards the guard. If someone reinstates one of these controllers node-side, the rule
            // above becomes wrong rather than merely unnecessary, and should be revisited
            // deliberately instead of silently passing.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string portal = Path.Combine(repoRoot, "enduser", "FebrisEndUserPortal", "Controllers");

            foreach (string removed in RemovedFromNode)
            {
                string[] found = Directory
                    .EnumerateFiles(portal, removed + "Controller.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                    .ToArray();

                Assert.True(found.Length == 0,
                    removed + "Controller now exists in the node. The redirect rule above assumes it " +
                    "does not, so revisit both together: " + string.Join(", ", found));
            }
        }

        [Fact]
        public void The_TestUser_facade_is_reachable_end_to_end()
        {
            // TestUser is a facade for generating and testing xAPI records, NOT a real user and
            // never attached to ApplicationUser. Its logic is a documented KEEPER
            // (NODE_REMOTE_TEARDOWN_PLAN.md:22, :101) and LauncherLogic sets IsTestUser in live
            // code, yet its whole UI sat commented out -- which is how an audit sweep came to read
            // it as dead scaffolding and nearly deleted it.
            //
            // Nothing in the unit suites can notice a controller being commented out again, because
            // no test constructs it. Hence a source rule over the whole chain.
            string repoRoot = ProjectGraph.FindRepoRoot();

            string controller = File.ReadAllText(Path.Combine(repoRoot,
                "enduser", "FebrisEndUserPortal", "Controllers", "Data", "Local", "TestUserController.cs"));

            // Live, not commented. The class line must not be preceded by a comment marker.
            Assert.Matches(@"(?m)^\s*public class TestUserController\s*:\s*Controller", controller);
            foreach (string action in new[] { "Index", "IndexPartial", "DetailsModal" })
            {
                Assert.Contains("public async Task<IActionResult> " + action + "(", controller);
            }

            // Registered, and deliberately OUTSIDE the marketplace region that the teardown deletes.
            string startup = File.ReadAllText(Path.Combine(repoRoot,
                "enduser", "FebrisEndUserPortal", "Startup.cs"));
            Assert.Matches(@"(?m)^\s*services\.AddScoped<ITestUserLogic, TestUserLogic>\(\);", startup);

            // Reachable: a live navbar link, with Razor comments stripped so a commented-out entry
            // does not count. That distinction is the entire reason this was mis-read.
            string layout = File.ReadAllText(Path.Combine(repoRoot,
                "enduser", "FebrisEndUserPortal", "Views", "Shared", "_Layout.cshtml"));
            string liveLayout = Regex.Replace(layout, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
            Assert.Contains("asp-controller=\"TestUser\"", liveLayout);

            // And the button route the views actually use resolves in the GENERIC dispatcher,
            // which is why no TestUser-specific script is needed or wanted.
            string generic = File.ReadAllText(Path.Combine(repoRoot,
                "enduser", "FebrisEndUserPortal", "wwwroot", "JSScriptLib", "ButtonOperation",
                "Generic", "GenericButtonOperation.js"));
            Assert.Contains("TestUserDetails", generic);
        }
    }
}
