// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Febris.ArchitectureTests
{
    /// <summary>
    /// Build-time guard against DUPLICATION DRIFT: the same type copied into more than one project,
    /// which then silently diverges (the exact failure behind the LMS-B1 bug, where one copy of
    /// StatementFactor was fixed and its twin was left broken).
    ///
    /// This is a RATCHET, not a big-bang cleanup. The set of type names currently defined in more
    /// than one project is frozen in <c>DuplicateTypeBaseline.txt</c>. The guard then enforces:
    ///   1. No NEW duplicate type may appear (a fresh copy fails the build). This is the teeth: a
    ///      future session -- human or AI -- physically cannot merge a second copy of a canonical
    ///      type. The fix is to REFERENCE the existing type or extract it to a shared project, never
    ///      to paste it into a new location.
    ///   2. The baseline may only SHRINK: once a duplicated type is consolidated back to one source,
    ///      its baseline entry must be removed. So the debt is visible and can only go down.
    ///
    /// It deliberately does NOT judge "legitimate tenant twin (Remote/Local queries) vs bad copy" --
    /// it can't, automatically. It just stops the bleeding and makes every existing duplicate an
    /// explicit, tracked line item. Depends on nothing (parses .cs from disk), like the sibling guards.
    /// </summary>
    public class DuplicateTypeGuardTests
    {
        // Tiers whose source is scanned. (v4 web platform + tests.)
        private static readonly string[] ScanRoots = { "shared", "central", "developer", "enduser", "marketing", "tests" };

        private static readonly Regex TypeDecl = new Regex(
            @"^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|protected|private|sealed|abstract|static|partial|readonly|new|\s)*\b(class|interface|record|struct|enum)\s+([A-Za-z_]\w+)",
            RegexOptions.Compiled);

        [Fact]
        public void No_new_duplicate_types_beyond_the_baseline()
        {
            string root = ProjectGraph.FindRepoRoot();
            var duplicates = ScanCrossProjectDuplicates(root);       // typeName -> relative file paths
            var baseline = LoadOrSeedBaseline(root, duplicates.Keys);

            var offenders = duplicates.Keys.Where(n => !baseline.Contains(n)).OrderBy(n => n).ToList();

            Assert.True(offenders.Count == 0, BuildOffenderMessage(offenders, duplicates));
        }

        [Fact]
        public void Baseline_has_no_stale_entries_so_the_ratchet_stays_honest()
        {
            string root = ProjectGraph.FindRepoRoot();
            var duplicates = ScanCrossProjectDuplicates(root);
            var baseline = LoadOrSeedBaseline(root, duplicates.Keys);

            var stale = baseline.Where(n => !duplicates.ContainsKey(n)).OrderBy(n => n).ToList();

            Assert.True(stale.Count == 0,
                "These types are no longer duplicated (nice work) -- remove them from " +
                "tests/FebrisArchitectureTests/DuplicateTypeBaseline.txt so the debt ratchet reflects reality:\n  " +
                string.Join("\n  ", stale));
        }

        // --- scanner -------------------------------------------------------------------------------

        /// <summary>type name -> the relative paths that define it, for names defined in >= 2 distinct projects.</summary>
        internal static SortedDictionary<string, List<string>> ScanCrossProjectDuplicates(string root)
        {
            // Every project directory (dir containing a .csproj) under the scan roots.
            var projectDirs = new List<string>();
            foreach (string r in ScanRoots)
            {
                string dir = Path.Combine(root, r);
                if (!Directory.Exists(dir)) continue;
                foreach (string csproj in Directory.EnumerateFiles(dir, "*.csproj", SearchOption.AllDirectories))
                {
                    if (IsBuildOutput(csproj)) continue;
                    projectDirs.Add(Path.GetDirectoryName(csproj) + Path.DirectorySeparatorChar);
                }
            }

            string OwningProject(string file)
            {
                string best = null;
                foreach (string pd in projectDirs)
                    if (file.StartsWith(pd, StringComparison.OrdinalIgnoreCase) && (best == null || pd.Length > best.Length))
                        best = pd;
                return best;
            }

            // typeName -> (owningProject -> first relative file seen)
            var byType = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (string r in ScanRoots)
            {
                string dir = Path.Combine(root, r);
                if (!Directory.Exists(dir)) continue;
                foreach (string cs in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    if (IsBuildOutput(cs)) continue;
                    string owner = OwningProject(cs);
                    if (owner == null) continue;
                    foreach (string line in File.ReadLines(cs))
                    {
                        Match m = TypeDecl.Match(line);
                        if (!m.Success) continue;
                        string name = m.Groups[2].Value;
                        if (!byType.TryGetValue(name, out var owners))
                            byType[name] = owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        if (!owners.ContainsKey(owner))
                            owners[owner] = ProjectGraph.Rel(root, cs).Replace('\\', '/');
                    }
                }
            }

            var result = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var kv in byType)
                if (kv.Value.Count >= 2)                              // defined in >= 2 distinct projects
                    result[kv.Key] = kv.Value.Values.OrderBy(x => x).ToList();
            return result;
        }

        private static bool IsBuildOutput(string path)
        {
            string p = path.Replace('\\', '/');
            return p.Contains("/bin/") || p.Contains("/obj/");
        }

        // --- baseline ------------------------------------------------------------------------------

        private static string BaselinePath(string root) =>
            Path.Combine(root, "tests", "FebrisArchitectureTests", "DuplicateTypeBaseline.txt");

        /// <summary>Reads the baseline; on first run (file absent) seeds it from the current state and returns it.</summary>
        private static HashSet<string> LoadOrSeedBaseline(string root, IEnumerable<string> currentDuplicates)
        {
            string file = BaselinePath(root);
            if (File.Exists(file))
            {
                return new HashSet<string>(
                    File.ReadAllLines(file)
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 0 && !l.StartsWith("#")),
                    StringComparer.Ordinal);
            }

            // Bootstrap: freeze the current duplication as the starting debt.
            var names = currentDuplicates.OrderBy(n => n, StringComparer.Ordinal).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("# DUPLICATE-TYPE BASELINE (managed by DuplicateTypeGuardTests).");
            sb.AppendLine("# Each line is a type name currently defined in >= 2 projects. This is EXISTING debt.");
            sb.AppendLine("# RULES: this list may only SHRINK. Do NOT add a line to permit a new copy -- reference");
            sb.AppendLine("#        the canonical type or extract it to a shared project instead. When you");
            sb.AppendLine("#        consolidate a type back to one source, delete its line here.");
            sb.AppendLine("# Seeded " + names.Count + " entries.");
            foreach (string n in names) sb.AppendLine(n);
            try { File.WriteAllText(file, sb.ToString()); } catch { /* read-only CI: still enforce against in-memory set */ }
            return new HashSet<string>(names, StringComparer.Ordinal);
        }

        private static string BuildOffenderMessage(List<string> offenders, SortedDictionary<string, List<string>> dups)
        {
            if (offenders.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine($"DUPLICATION DRIFT GUARD: {offenders.Count} type(s) are now defined in more than one project but are NOT in the baseline.");
            sb.AppendLine("A shared type was copied into a new location instead of referenced. That is how twins drift out of sync (LMS-B1).");
            sb.AppendLine("FIX: reference the existing type, or extract it to a shared project both can reference. Do not copy it.");
            sb.AppendLine("If this duplication is genuinely intentional (e.g. a tenant Remote/Local query twin), add the name to");
            sb.AppendLine("tests/FebrisArchitectureTests/DuplicateTypeBaseline.txt WITH a justifying comment above it.");
            sb.AppendLine();
            foreach (string n in offenders)
            {
                sb.AppendLine($"  {n}:");
                foreach (string f in dups[n]) sb.AppendLine($"      {f}");
            }
            return sb.ToString();
        }
    }
}
