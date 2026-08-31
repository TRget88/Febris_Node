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
    /// Every partial a view renders must exist, matched CASE-SENSITIVELY against git.
    ///
    /// <para>
    /// WHY THIS EXISTS. Rendering a partial that is not there throws
    /// <c>InvalidOperationException</c> at REQUEST time. It is not a build error, and no unit test
    /// catches it because nothing constructs the view. The node remote teardown deleted a great many
    /// partials across seven phases, and the plan's own note for step 1.12 warned that missing one
    /// such re-point is "a runtime InvalidOperationException" -- so the hazard is real and recurring.
    /// </para>
    ///
    /// <para>
    /// NO ALLOWLIST, DELIBERATELY. An earlier attempt at this guard was abandoned because 14
    /// references were unresolved and every one sat inside a partial that nothing rendered. A guard
    /// that fails on 14 inert things gets suppressed, and a suppressed guard is worse than none. The
    /// orphans were removed first (the AccreditationBodyUser family with the contentmgmt sweep, then
    /// the 11-file Provider family), which took the count to ZERO and let this ship strict. Keep it
    /// that way: if this test fails, fix the reference or delete the dead view. Do NOT add an
    /// exemption list, because that is how the count crept to 14 the first time.
    /// </para>
    ///
    /// <para>
    /// CASE-SENSITIVE AGAINST GIT, not the filesystem. Development is on Windows, which is
    /// case-insensitive; production is Debian, which is not. A `File.Exists` check cannot see a
    /// case-only mismatch, which is exactly how a Messageboard script include stayed hidden while the
    /// button was dead in production. Git knows the real spelling.
    /// </para>
    /// </summary>
    public class EveryRenderedPartialExistsTests
    {
        private static HashSet<string> _viewPaths;
        private static HashSet<string> _viewNames;

        private const string ViewsPrefix = "enduser/FebrisEndUserPortal/Views/";

        /// <summary>
        /// Tracked view paths and leaf names, exactly as git spells them. Git rather than the
        /// filesystem, because this is a case-sensitivity check and the dev filesystem cannot answer
        /// case questions.
        /// </summary>
        private static void LoadViews(string repoRoot)
        {
            if (_viewPaths != null)
            {
                return;
            }

            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-files " + ViewsPrefix,
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
            {
                string line;
                while ((line = p.StandardOutput.ReadLine()) != null)
                {
                    string t = line.Trim();
                    if (!t.StartsWith(ViewsPrefix, StringComparison.Ordinal) ||
                        !t.EndsWith(".cshtml", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string rel = t.Substring(ViewsPrefix.Length);
                    rel = rel.Substring(0, rel.Length - ".cshtml".Length);
                    paths.Add(rel);
                    names.Add(rel.Contains('/') ? rel.Substring(rel.LastIndexOf('/') + 1) : rel);
                }
                p.WaitForExit();
            }

            _viewPaths = paths;
            _viewNames = names;
        }

        /// <summary>Razor comments removed, so a commented-out render does not count as live.</summary>
        private static string StripRazorComments(string source)
        {
            return Regex.Replace(source, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
        }

        /// <summary>
        /// Razor resolves a partial by relative path under Views and Views/Shared, or -- for a
        /// PATHLESS name only -- by searching the view locations. The leaf-name fallback used to
        /// apply to folder-qualified references too, which was the single biggest blind spot the
        /// ROADMAP 17 critic found: _IndexButtonGroupPartial exists in 30+ GenericButtons entity
        /// folders, so a reference into a dead folder passed because a LIVE folder still had a
        /// file of the same leaf name. A reference with a path must now resolve as written.
        /// </summary>
        private static bool Resolves(string reference)
        {
            string r = reference.TrimStart('~', '/');
            // "../Widget/_X" written from a controller's PartialView climbs out of the action's
            // view folder into a sibling folder under Views/.
            while (r.StartsWith("../", StringComparison.Ordinal))
            {
                r = r.Substring(3);
            }
            if (_viewPaths.Contains(r) || _viewPaths.Contains("Shared/" + r))
            {
                return true;
            }

            return !r.Contains('/') && _viewNames.Contains(r);
        }

        [Fact]
        public void Every_partial_a_view_renders_actually_exists()
        {
            string repoRoot = ProjectGraph.FindRepoRoot();
            LoadViews(repoRoot);

            Assert.True(_viewPaths.Count > 100,
                "expected to enumerate the Portal's views, found " + _viewPaths.Count +
                " -- this guard would pass vacuously");

            string viewsDir = Path.Combine(repoRoot,
                ViewsPrefix.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));

            Regex render = new Regex("PartialAsync\\(\\s*\"(?<ref>[^\"]+)\"", RegexOptions.Compiled);

            List<string> broken = new List<string>();
            int checkedCount = 0;

            foreach (string view in Directory.EnumerateFiles(viewsDir, "*.cshtml", SearchOption.AllDirectories))
            {
                string source = StripRazorComments(File.ReadAllText(view));

                foreach (Match m in render.Matches(source))
                {
                    checkedCount++;
                    string reference = m.Groups["ref"].Value;

                    if (!Resolves(reference))
                    {
                        broken.Add(ProjectGraph.Rel(repoRoot, view) + "  ->  " + reference);
                    }
                }
            }

            Assert.True(checkedCount > 50,
                "expected to find partial renders, found " + checkedCount + " -- the parser is broken");

            Assert.True(broken.Count == 0,
                "views render partials that do not exist. Each is an InvalidOperationException at " +
                "REQUEST time, invisible to the build and to every unit test. Fix the reference or " +
                "delete the dead view -- do NOT add an exemption list:" + Environment.NewLine + "  " +
                string.Join(Environment.NewLine + "  ", broken));
        }

        [Fact]
        public void The_guard_is_case_sensitive_so_a_windows_build_sees_what_debian_would()
        {
            // Pins the property that makes this guard worth having over File.Exists. The dev box is
            // case-insensitive and production is not, so a reference differing only in case must be
            // treated as broken here even though Windows would happily open the file.
            string repoRoot = ProjectGraph.FindRepoRoot();
            LoadViews(repoRoot);

            string sample = _viewPaths.FirstOrDefault(p => p.Any(char.IsUpper));
            Assert.False(string.IsNullOrEmpty(sample), "expected at least one view path with an uppercase letter");

            Assert.True(Resolves(sample), "the unmodified path must resolve");
            Assert.False(Resolves(sample.ToLowerInvariant()),
                "a case-only variant must NOT resolve, otherwise this guard cannot see the class of " +
                "bug that only appears on Debian");
        }
    }
}
