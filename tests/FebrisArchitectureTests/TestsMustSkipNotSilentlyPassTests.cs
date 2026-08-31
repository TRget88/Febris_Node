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
    /// A test that cannot run must SKIP, never quietly pass.
    ///
    /// <para>
    /// WHY THIS EXISTS. <c>LiveDatabaseMigrationVerificationTests</c> had seven <c>[Fact]</c>s that
    /// each opened with <c>if (!Enabled) { _output.WriteLine("SKIPPED: ..."); return; }</c>. An
    /// early return is a PASS to xunit, so those seven counted toward a green gate while asserting
    /// nothing, and the word SKIPPED in the log made it look deliberate and handled. They were the
    /// ONLY tests in the repository that apply the migration chain to real PostgreSQL, so the claim
    /// a deploy rested on was the one claim the gate did not actually make.
    /// </para>
    ///
    /// <para>
    /// The project already had the right tool: <c>Xunit.SkippableFact</c>, used correctly in the
    /// shared-services suites. This guard exists because the failure is INVISIBLE by construction.
    /// Nothing goes red, no count changes, and the only symptom is a number that is larger than the
    /// work it represents.
    /// </para>
    /// </summary>
    public class TestsMustSkipNotSilentlyPassTests
    {
        /// <summary>
        /// An early return in a method whose guard names an opt-in environment variable. The
        /// deliberate narrowness matters: plenty of legitimate tests return early, so this looks for
        /// the specific shape of "this test cannot run here, so I will pass instead".
        /// </summary>
        private static readonly Regex OptInGuard = new Regex(
            @"if\s*\(\s*!\s*(?<flag>Enabled|IsEnabled|Configured|IsConfigured)\s*\)\s*\{[^}]*?\breturn\s*;[^}]*?\}",
            RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Comments removed before matching. The first version of this guard failed on ITSELF,
        /// because the doc comment above quotes the very pattern it hunts for. A guard that cannot
        /// tell code from prose about code will either cry wolf or, worse, be silenced with an
        /// exclusion that also hides real instances.
        /// </summary>
        private static string WithoutComments(string source)
        {
            source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            source = Regex.Replace(source, @"^[ \t]*///.*$", string.Empty, RegexOptions.Multiline);
            source = Regex.Replace(source, @"(?<![:""])//(?![""]).*$", string.Empty, RegexOptions.Multiline);
            return source;
        }

        private static IEnumerable<string> TestFiles(string repoRoot)
        {
            string tests = Path.Combine(repoRoot, "tests");
            return Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));
        }

        [Fact]
        public void No_test_returns_early_instead_of_skipping()
        {
            string repoRoot = ProjectGraph.FindRepoRoot();

            List<string> scanned = TestFiles(repoRoot).ToList();
            Assert.True(scanned.Count > 50,
                "expected to scan the test tree, found " + scanned.Count + " files -- this guard would pass vacuously");

            List<string> offenders = new List<string>();
            foreach (string file in scanned)
            {
                string source = WithoutComments(File.ReadAllText(file));
                if (!source.Contains("[Fact]") && !source.Contains("[Theory]"))
                {
                    continue;
                }

                foreach (Match m in OptInGuard.Matches(source))
                {
                    // Only a problem inside a test method. A helper may legitimately bail out.
                    int factBefore = source.LastIndexOf("[Fact]", m.Index, StringComparison.Ordinal);
                    int theoryBefore = source.LastIndexOf("[Theory]", m.Index, StringComparison.Ordinal);
                    int attrBefore = Math.Max(factBefore, theoryBefore);
                    if (attrBefore < 0)
                    {
                        continue;
                    }

                    // If another method body starts between the attribute and the guard, the guard
                    // is not in the attributed method.
                    string between = source.Substring(attrBefore, m.Index - attrBefore);
                    if (between.Contains("[SkippableFact]") || between.Contains("Skip.If"))
                    {
                        continue;
                    }

                    offenders.Add(
                        Path.GetFileName(file) + " near offset " + m.Index +
                        " -- an opt-in guard returns instead of skipping, so the test PASSES when it did not run");
                }
            }

            Assert.True(offenders.Count == 0,
                "Tests that cannot run must use [SkippableFact] + Skip.IfNot, not an early return:" +
                Environment.NewLine + string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void The_live_database_suite_uses_skippable_facts()
        {
            // The specific regression, pinned by name. The guard above is the general rule; this is
            // the instance that was actually wrong, so a rewrite of that suite cannot quietly drop
            // back to plain Facts.
            string path = Path.Combine(
                ProjectGraph.FindRepoRoot(),
                "tests", "FebrisEndUserBLLTests", "LiveDatabaseMigrationVerificationTests.cs");
            Assert.True(File.Exists(path), "expected file not found: " + path);

            string source = File.ReadAllText(path);

            int skippable = Regex.Matches(source, @"\[SkippableFact\]").Count;
            Assert.True(skippable >= 7,
                "expected the live-database checks to be SkippableFacts, found " + skippable);

            Assert.DoesNotContain("[Fact]", source);
            Assert.Contains("Skip.IfNot(", source);
        }

        [Fact]
        public void The_model_drift_check_asserts_drift_not_a_tautology()
        {
            // GetPendingMigrationsAsync AFTER MigrateAsync is empty by definition, so the original
            // assertion could not fail for the reason its name gave. HasPendingModelChanges is the
            // API that compares the model to the last snapshot, which is the thing that drifts.
            string path = Path.Combine(
                ProjectGraph.FindRepoRoot(),
                "tests", "FebrisEndUserBLLTests", "LiveDatabaseMigrationVerificationTests.cs");
            string source = File.ReadAllText(path);

            Assert.Contains("HasPendingModelChanges", source);

            // All three migration-managed contexts, not two. ApplicationDb was absent entirely.
            foreach (string ctx in new[] { "XApiDbContext", "DataDbContext", "ApplicationDbContext" })
            {
                Assert.Contains(ctx, source);
            }
        }
    }
}
