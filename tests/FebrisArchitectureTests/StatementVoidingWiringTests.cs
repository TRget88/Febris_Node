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
    /// T5, second half: the wiring that makes voiding reachable from the Portal.
    ///
    /// <para>
    /// WHY THIS EXISTS. The voiding engine shipped first, fully tested and completely unreachable.
    /// Everything the audit pointed at as evidence that voiding was "partly built" turned out to be
    /// debris that could never have run: an orphaned button partial referenced by zero views, a JS
    /// route (<c>/XAPI/VoidStatement</c>) with no controller behind it, and a storage path commented
    /// out in three places. Unit tests of the engine cannot notice any of that, which is exactly how
    /// the audit came to believe the Portal "ships the button".
    /// </para>
    ///
    /// <para>
    /// These guards parse source rather than referencing the Portal, matching the rest of this
    /// project. The controller's own contract is pinned by reflection in
    /// <c>StatementVoidingControllerTests</c>.
    /// </para>
    /// </summary>
    public class StatementVoidingWiringTests
    {
        private const string PortalDir = "enduser/FebrisEndUserPortal";

        private static string PortalPath(string repoRoot, params string[] parts)
        {
            string[] all = new[] { repoRoot, PortalDir.Replace('/', Path.DirectorySeparatorChar) }
                .Concat(parts).ToArray();
            return Path.Combine(all);
        }

        /// <summary>
        /// Every file tracked under the Portal's wwwroot, EXACTLY as git spells it, relative to
        /// wwwroot and with forward slashes.
        ///
        /// <para>
        /// Deliberately git rather than <c>Directory.EnumerateFiles</c>. The check this backs is a
        /// case-sensitivity check, and the developer filesystem is case-INsensitive while the
        /// Debian runtime is not, so the filesystem is the one source that cannot answer the
        /// question being asked.
        /// </para>
        /// </summary>
        private static HashSet<string> TrackedWwwrootFiles(string repoRoot)
        {
            if (_trackedWwwroot != null)
            {
                return _trackedWwwroot;
            }

            const string prefix = "enduser/FebrisEndUserPortal/wwwroot/";

            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-files " + prefix,
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);
            using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
            {
                string line;
                while ((line = p.StandardOutput.ReadLine()) != null)
                {
                    string t = line.Trim();
                    if (t.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        found.Add(t.Substring(prefix.Length));
                    }
                }
                p.WaitForExit();
            }

            _trackedWwwroot = found;
            return found;
        }

        private static HashSet<string> _trackedWwwroot;

        /// <summary>
        /// Razor and HTML comments removed, so a commented-out reference does not count as live.
        /// This audit has now made that mistake twice, once in a report and once in this very guard.
        /// </summary>
        private static string StripComments(string source)
        {
            source = Regex.Replace(source, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
            source = Regex.Replace(source, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);
            return source;
        }

        private static string ReadPortalFile(string repoRoot, params string[] parts)
        {
            string path = PortalPath(repoRoot, parts);
            Assert.True(File.Exists(path), "expected file not found: " + path);
            return File.ReadAllText(path);
        }

        /// <summary>Lines with the leading Razor/JS comment markers stripped out.</summary>
        private static IEnumerable<string> UncommentedLines(string text)
        {
            return text.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal));
        }

        // ------------------------------------------------------------------
        // The engine is registered
        // ------------------------------------------------------------------

        [Fact]
        public void Startup_registers_the_voiding_logic()
        {
            // Without this the controller cannot be constructed and every statement details modal
            // 500s -- not just voiding. MS.DI throws loudly for controllers, so this is a build-time
            // reminder rather than a silent-failure guard.
            string startup = ReadPortalFile(ProjectGraph.FindRepoRoot(), "Startup.cs");

            List<string> live = UncommentedLines(startup).ToList();

            Assert.True(
                live.Any(l => l.Contains("IStatementVoidingLogic") && l.Contains("StatementVoidingLogic>")),
                "Startup must register IStatementVoidingLogic -> StatementVoidingLogic, or StatementController cannot be resolved");

            Assert.True(
                live.Any(l => l.Contains("IStatementDownloadLogic") && l.Contains("StatementDownloadLogic>")),
                "Startup must register IStatementDownloadLogic -> StatementDownloadLogic, or StatementController cannot be resolved");

            // StatementDownloadLogic depends on it and, unlike StatementLogic, has no legacy
            // self-newing constructor for MS.DI to silently fall back to.
            Assert.True(
                live.Any(l => l.Contains("IStatementFileHandler") && l.Contains("StatementFileHandler>")),
                "Startup must register IStatementFileHandler, or the download logic cannot be constructed");
        }

        [Fact(Skip = "subject moved to febris-shared, see docs/decisions/TRIAD_OWNERSHIP.md")]
        public void The_statement_file_handler_has_a_read_side()
        {
            // The download could not be restored by wiring alone because IStatementFileHandler was
            // WRITE-ONLY: it declared UploadPackage and nothing else, while two read methods sat
            // below it entirely commented out.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string path = Path.Combine(repoRoot, "shared", "FebrisSharedServices", "FileServerHandler.cs");
            Assert.True(File.Exists(path), "FileServerHandler.cs not found at " + path);

            string source = File.ReadAllText(path);
            int ifaceAt = source.IndexOf("interface IStatementFileHandler", StringComparison.Ordinal);
            Assert.True(ifaceAt >= 0, "IStatementFileHandler not found");

            int close = source.IndexOf("}", source.IndexOf("{", ifaceAt), StringComparison.Ordinal);
            string body = source.Substring(ifaceAt, close - ifaceAt);

            Assert.Contains("DownloadPackage", body);
        }

        // ------------------------------------------------------------------
        // The button is rendered, and carries the right identifier
        // ------------------------------------------------------------------

        [Fact]
        public void The_statement_details_modal_renders_the_action_buttons()
        {
            string repoRoot = ProjectGraph.FindRepoRoot();
            string modal = ReadPortalFile(repoRoot, "Views", "Statement", "DetailsModal.cshtml");

            List<string> live = UncommentedLines(modal).ToList();

            Assert.True(
                live.Any(l => l.Contains("_StatementActionsButtonGroupPartial")),
                "the details modal must render the statement action button group -- the previous block here was commented out AND pointed at a partial that does not exist");

            // The modal must NOT gate the group on admin. Doing so would hide the DOWNLOAD from
            // every educator and learner, which is the opposite of how the 2021 Portal had it and
            // the opposite of what the read scoping already allows. The partial decides per button.
            Assert.False(
                live.Any(l => l.Contains("@if") && l.Contains("IsLocalAdmin")),
                "the modal must not admin-gate the whole group, or the download disappears for everyone below admin");
        }

        [Fact]
        public void The_action_group_gates_void_on_admin_but_never_the_download()
        {
            // The security-relevant split, and the easiest thing to get wrong by "tidying" the
            // partial: one @if that swallows both buttons either hides the download from educators
            // and learners, or exposes void to them.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string group = ReadPortalFile(repoRoot,
                "Views", "Shared", "Buttons", "GenericButtons", "Statement", "_StatementActionsButtonGroupPartial.cshtml");

            int downloadAt = group.IndexOf("\"StatementDownload\"", StringComparison.Ordinal);
            int voidAt = group.IndexOf("\"VoidStatement\"", StringComparison.Ordinal);
            int gateAt = group.IndexOf("User.IsLocalAdmin()", StringComparison.Ordinal);

            Assert.True(downloadAt >= 0, "the download button must be in the group");
            Assert.True(voidAt >= 0, "the void button must be in the group");
            Assert.True(gateAt >= 0, "the group must gate something on IsLocalAdmin");

            Assert.True(downloadAt < gateAt,
                "the download button must appear BEFORE the first admin gate, so it renders for anyone who may view the statement");
            Assert.True(voidAt > gateAt,
                "the void button must appear AFTER the admin gate");

            Assert.Contains("statementAntiForgeryHolder", group);
        }

        [Fact]
        public void The_void_button_is_passed_the_statement_uuid_not_the_primary_key()
        {
            // THE defect this file exists to prevent. The orphaned partial passed Model.Id, a long.
            // The controller and BLL take the xAPI UUID. Handing the key to a Guid parameter binds
            // Guid.Empty and refuses every void, silently -- the same identifier mismatch that
            // shipped a broken video entitlement gate earlier in this audit.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string modal = ReadPortalFile(repoRoot, "Views", "Statement", "DetailsModal.cshtml");

            string render = UncommentedLines(modal)
                .SkipWhile(l => !l.Contains("_StatementActionsButtonGroupPartial"))
                .Take(4)
                .Aggregate(string.Empty, (acc, l) => acc + " " + l);

            Assert.Contains("Model.UUID", render);
            Assert.DoesNotContain("Model.Id", render);
        }

        [Fact]
        public void The_action_partials_exist_and_chain_together()
        {
            string repoRoot = ProjectGraph.FindRepoRoot();

            string group = ReadPortalFile(repoRoot,
                "Views", "Shared", "Buttons", "GenericButtons", "Statement", "_StatementActionsButtonGroupPartial.cshtml");

            // Html.PartialAsync resolves its path as a runtime string, so a typo here is invisible
            // to the compiler and only shows up as an InvalidOperationException when an admin opens
            // the modal.
            Assert.Contains("_StatementIndividualUUIDButtonPartial", group);
            Assert.Contains("IndividualUUIDButtonPartial", group);

            string button = ReadPortalFile(repoRoot,
                "Views", "Shared", "Buttons", "_StatementIndividualUUIDButtonPartial.cshtml");

            Assert.Contains("StatementButtonAction(this)", button);
            Assert.Contains("IndividualUUIDButtonPartial", button);
        }

        [Fact]
        public void The_dead_void_route_debris_is_gone()
        {
            // The orphaned _FebrisStatementDetailButtonPartial and the /XAPI/VoidStatement route it
            // pointed at are what convinced the audit the Portal already shipped a void button.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string root = PortalPath(repoRoot);

            List<string> offenders = Directory
                .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => !ProjectGraph.IsInBuildOutput(f))
                .Where(f => f.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                .Where(f =>
                {
                    string text = File.ReadAllText(f);
                    return text.Contains("/XAPI/VoidStatement")
                        || text.Contains("/XAPI/StatementDownloader")
                        || text.Contains("LoadStatementAction")
                        || text.Contains("_FebrisStatementDetailButtonPartial");
                })
                .Select(f => ProjectGraph.Rel(repoRoot, f))
                .ToList();

            Assert.True(offenders.Count == 0,
                "the dead /XAPI/* statement routes, the never-defined LoadStatementAction helper, and the orphaned partial must stay gone:\n  " +
                string.Join("\n  ", offenders));
        }

        [Fact]
        public void No_statement_button_partial_is_orphaned()
        {
            // Orphaned button markup is not harmless litter here. Four such partials survived the
            // port while the features they described did not, and the audit read them as evidence
            // that the Portal already "shipped the button" for voiding and for the JSON download.
            // Both were actually missing. Debris that describes a feature nobody can reach is worse
            // than no debris, because it reads as proof the feature exists.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string portalRoot = PortalPath(repoRoot);
            string buttonDir = Path.Combine(portalRoot, "Views", "Shared", "Buttons",
                "GenericButtons", "Statement");

            Assert.True(Directory.Exists(buttonDir), "statement button directory not found at " + buttonDir);

            // DELIBERATELY EMPTY, and it must stay that way. The last exemption here was
            // _DetailsButtonGroupPartial, which rendered an "Edit" button for a commented-out
            // StatementController.Edit action. Editing a statement is an xAPI spec violation,
            // because statements are immutable and that immutability is the entire reason voiding
            // exists, so the owner ruled it deleted rather than restored on 2026-08-15. An orphaned
            // partial is either a lost feature to restore or scaffolding to remove. It is never
            // something to park here.
            HashSet<string> pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
            };

            List<string> allViews = Directory
                .EnumerateFiles(Path.Combine(portalRoot, "Views"), "*.cshtml", SearchOption.AllDirectories)
                .Where(f => !ProjectGraph.IsInBuildOutput(f))
                .ToList();

            List<string> orphans = new List<string>();
            int checkedCount = 0;

            foreach (string partial in Directory.EnumerateFiles(buttonDir, "*.cshtml"))
            {
                string name = Path.GetFileName(partial);
                if (pending.Contains(name))
                {
                    continue;
                }

                checkedCount++;
                string bare = Path.GetFileNameWithoutExtension(name);

                bool rendered = allViews
                    .Where(v => !string.Equals(v, partial, StringComparison.OrdinalIgnoreCase))
                    .Any(v => File.ReadAllText(v).Contains(bare));

                if (!rendered)
                {
                    orphans.Add(ProjectGraph.Rel(repoRoot, partial));
                }
            }

            Assert.True(checkedCount > 0, "the scan must find statement button partials, otherwise this guard passes vacuously");

            Assert.True(orphans.Count == 0,
                "statement button partials rendered by no view. Either wire them or delete them, but do not leave markup describing a feature nobody can reach:\n  " +
                string.Join("\n  ", orphans));
        }

        [Fact]
        public void The_download_script_gets_the_real_route_and_carries_no_token()
        {
            string repoRoot = ProjectGraph.FindRepoRoot();
            string script = ReadPortalFile(repoRoot,
                "wwwroot", "JSScriptLib", "ButtonOperation", "Specific", "StatementButtonOperation.js");

            List<string> live = UncommentedLines(script).ToList();

            Assert.True(live.Any(l => l.Contains("/Statement/StatementDownload?statementId=")),
                "the download must hit the route the controller actually serves, not the dead /XAPI/ one");

            // Every function this file dispatches to must be DEFINED here. The original download
            // helper was called from three portals and defined in none, which is why the button
            // threw a ReferenceError instead of downloading anything.
            foreach (string fn in new[] { "DownloadStatement", "VoidStatement" })
            {
                Assert.True(live.Any(l => l.Contains("function " + fn + "(")),
                    "StatementButtonOperation.js dispatches to " + fn + " so it must define it");
            }
        }

        // ------------------------------------------------------------------
        // The client half
        // ------------------------------------------------------------------

        [Fact]
        public void The_void_script_posts_to_the_real_route_with_a_token()
        {
            string repoRoot = ProjectGraph.FindRepoRoot();
            string script = ReadPortalFile(repoRoot,
                "wwwroot", "JSScriptLib", "ButtonOperation", "Specific", "StatementButtonOperation.js");

            List<string> live = UncommentedLines(script).ToList();

            Assert.True(live.Any(l => l.Contains("$.post(\"/Statement/VoidStatement\"")),
                "the script must POST to the route the controller actually serves");

            Assert.True(live.Any(l => l.Contains("__RequestVerificationToken")),
                "the POST must carry the antiforgery token, or the global AutoValidateAntiforgeryToken filter rejects it");

            Assert.True(live.Any(l => l.Contains("confirm(")),
                "voiding is irreversible by owner ruling and by the spec, so the confirm is part of the contract");

            Assert.False(live.Any(l => l.Contains("$.get") && l.Contains("VoidStatement")),
                "voiding must never be GET-reachable (audit C-07)");
        }

        [Fact]
        public void Every_portal_view_script_include_points_at_a_file_that_exists()
        {
            // FOUND while wiring T5. Views/Statement/Index.cshtml included
            // LocalStatementButtonOperation.js, which does not exist anywhere in the repo, so the
            // tag 404d and StatementButtonAction was undefined on the page. Nothing depended on it
            // yet, which is why it went unnoticed -- the void button does, so it had to be fixed.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string portalRoot = PortalPath(repoRoot);
            string viewsRoot = Path.Combine(portalRoot, "Views");

            // KNOWN BROKEN, pre-existing and out of scope for T5. Each of these leaves the buttons
            // on its page inert. Recorded in docs/BUGS.md for a follow-up rather than silently
            // tolerated -- do NOT add entries to make a new failure go away.
            // KNOWN BROKEN, still. Each entry's page is dead surface -- see the four dead-asset
            // decisions in docs/BUGS.md (2026-08-18) for the reachability evidence on each.
            HashSet<string> knownMissing = new HashSet<string>(StringComparer.Ordinal)
            {
                "JSScriptLib/ButtonOperation/Specific/CohortMemberButtonOperation.js",
                // TestUserButtonOperation.js is GONE from this list on purpose (2026-08-18). The
                // tag was removed rather than the file created: this portal drives TestUser buttons
                // through the generic ButtonAction path, and GenericButtonOperation.js already
                // routes "TestUserDetails". The specific file was a copy-paste from the central and
                // developer portals and never existed here or in the retired SVN EndUser tree.
                //
                // lib/leaflet/leaflet.js is GONE from this list too (2026-08-23, ROADMAP 18): the two
                // Widget partials that included it were deleted with the rest of the map surface,
                // per the owner's "remove the map surface, do not vendor the library" ruling.
            };

            // WIDENED 2026-08-18, twice, because this guard had two blind spots that let real
            // breakage through while reporting green.
            //
            // 1. It only matched JSScriptLib/, so `~/lib/leaflet/leaflet.js` was never checked at
            //    all. Leaflet is referenced by two Widget partials and has never been vendored.
            // 2. The comparison was OrdinalIgnoreCase and the existence test is File.Exists, which
            //    on a case-insensitive Windows filesystem cannot see a case-only mismatch. The
            //    runtime is Debian, which can. That is exactly how
            //    MessageBoardButtonOperation.js (file: Messageboard...) stayed hidden: the guard
            //    ran green on Windows while the Edit button in the Messageboard details modal was
            //    dead in production.
            //
            // The case check below is done against git's own file list rather than the filesystem,
            // because git is authoritative about casing and the filesystem here is not.
            Regex include = new Regex("script src=\"~/(?<path>[^\"]+\\.js)\"", RegexOptions.Compiled);
            List<string> missing = new List<string>();
            int checkedCount = 0;

            foreach (string view in Directory.EnumerateFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories))
            {
                // COMMENTS STRIPPED FIRST. A script tag inside @* *@ or <!-- --> loads nothing, so
                // counting it produces false failures -- five commented-out gentelella vendor tags
                // in _Layout alone. Widening the regex without this turned a guard with blind spots
                // into a guard that cries wolf, which gets it suppressed and is worse.
                string viewSource = StripComments(File.ReadAllText(view));

                foreach (Match m in include.Matches(viewSource))
                {
                    string rel = m.Groups["path"].Value;
                    checkedCount++;

                    if (knownMissing.Contains(rel))
                    {
                        continue;
                    }

                    // CASE-SENSITIVE, against git rather than the filesystem. File.Exists on
                    // Windows answers yes for MessageBoardButtonOperation.js when the tracked file
                    // is Messageboard..., and the Debian runtime answers no. Git knows the real
                    // name, so ask it.
                    if (!TrackedWwwrootFiles(repoRoot).Contains(rel))
                    {
                        missing.Add(ProjectGraph.Rel(repoRoot, view) + " -> " + rel);
                    }
                }
            }

            Assert.True(checkedCount > 0, "the parser must actually be finding script includes, otherwise this guard passes vacuously");

            Assert.True(missing.Count == 0,
                "views including script files that do not exist (the tag 404s and every handler in it is undefined):\n  " +
                string.Join("\n  ", missing));
        }
    }
}
