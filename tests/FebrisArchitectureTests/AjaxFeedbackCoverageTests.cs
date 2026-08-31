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
    /// ROADMAP 20, owner-raised 2026-08-05: every AJAX call that changes state must tell the user
    /// whether it worked. Silence is not success.
    ///
    /// <para>
    /// WHY THIS EXISTS AS A GUARD RATHER THAN A ONE-OFF FIX. Silent-success is the defect family
    /// this whole audit kept finding -- C-02's inverted acknowledgement, a bulk import that reported
    /// success while dropping rows, a controller refusal that redirected as if it had saved -- and
    /// the browser layer is where it is cheapest to reintroduce. A new <c>$.post</c> with no
    /// <c>.fail</c> looks exactly like a correct one in review, and the resulting bug is invisible
    /// by definition: nothing appears on screen either way. The worst instance found here,
    /// <c>SubmitFollowing</c>, had an EMPTY success handler and no failure handler, and had
    /// therefore been requesting an address no controller serves for as long as it had existed --
    /// nobody noticed, because there was nothing to notice.
    /// </para>
    ///
    /// <para>
    /// THE CONTRACT these guards encode, which is what makes it one mechanism rather than twelve:
    /// FAILURE is reported by the global <c>ajaxError</c> net in <c>StatusMessage.js</c>, so no call
    /// site announces its own transport failure (two announcements per failure is its own defect).
    /// SUCCESS is reported by the call site, because only the call site knows what succeeded. A
    /// refusal that arrives as HTTP 200 is invisible to the net and must be announced by the call
    /// site through <c>StatusMessage.refused</c>.
    /// </para>
    ///
    /// <para>
    /// Source-scanning, matching the rest of this project. There is no JS test harness in this repo,
    /// and adding one to assert "a toast appeared" would test jQuery rather than the wiring.
    /// </para>
    /// </summary>
    public class AjaxFeedbackCoverageTests
    {
        private const string PortalDir = "enduser/FebrisEndUserPortal";

        /// <summary>How far past a marker to look. Generous enough for a full $.ajax options object.</summary>
        private const int CallBlockLines = 40;

        private static string PortalPath(string repoRoot, params string[] parts)
        {
            string[] all = new[] { repoRoot, PortalDir.Replace('/', Path.DirectorySeparatorChar) }
                .Concat(parts).ToArray();
            return Path.Combine(all);
        }

        private static string ReadPortalFile(string repoRoot, params string[] parts)
        {
            string path = PortalPath(repoRoot, parts);
            Assert.True(File.Exists(path), "expected file not found: " + path);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Razor and HTML block comments removed, so a commented-out call site does not count as
        /// live. Several of the files scanned here carry large commented-out blocks of the very
        /// patterns being searched for -- the three Manage*Index pages each keep two dead
        /// <c>$.get</c> calls in comments -- so skipping this step would produce failures for code
        /// that cannot run.
        /// </summary>
        private static string StripBlockComments(string source)
        {
            source = Regex.Replace(source, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
            source = Regex.Replace(source, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);
            return source;
        }

        /// <summary>
        /// Lines with <c>//</c>-commented ones blanked rather than removed, so line offsets survive
        /// and a "within N lines" window still means what it says.
        /// </summary>
        private static string[] LiveLines(string source)
        {
            return StripBlockComments(source)
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(l => l.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : l)
                .ToArray();
        }

        /// <summary>Every .cshtml and .js file under the Portal that is not build output.</summary>
        private static List<string> ScriptBearingFiles(string repoRoot)
        {
            string portalRoot = PortalPath(repoRoot);
            return Directory
                .EnumerateFiles(portalRoot, "*.*", SearchOption.AllDirectories)
                .Where(f => !ProjectGraph.IsInBuildOutput(f))
                .Where(f => f.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                // Vendored theme assets are not ours to police.
                .Where(f => f.IndexOf("gentelella-master", StringComparison.OrdinalIgnoreCase) < 0
                         && f.IndexOf(Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar,
                                      StringComparison.OrdinalIgnoreCase) < 0)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
        }

        private static string Window(string[] lines, int startIndex, int count)
        {
            int take = Math.Min(count, lines.Length - startIndex);
            return string.Join("\n", lines.Skip(startIndex).Take(take));
        }

        /// <summary>
        /// JavaScript comments removed, both forms. Needed wherever a guard asserts that a token is
        /// ABSENT: the comments in StatusMessage.js deliberately name the mistakes that were made,
        /// so a check against the raw text sees the explanation and reports the bug it describes.
        /// </summary>
        private static string StripJsComments(string source)
        {
            source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            source = Regex.Replace(source, @"//[^\n]*", string.Empty);
            return source;
        }

        /// <summary>Any line that opens a new jQuery request.</summary>
        private static readonly Regex CallOpener =
            new Regex(@"\$\.(ajax|post|get)\s*\(", RegexOptions.Compiled);

        /// <summary>
        /// The text of the single request block containing <paramref name="markerIndex"/>, bounded
        /// at the NEXT request rather than at a fixed line count.
        ///
        /// <para>
        /// The bound is the whole point. A fixed forward window let a NEIGHBOURING call's success
        /// message satisfy the check for a call that had none: the three Manage*Index pages put
        /// their add and remove calls ten lines apart (eleven before this change), so deleting the
        /// add message left the remove message sitting inside the add call's window and the guard
        /// stayed green.
        /// Mutation testing found that -- the first version of this file reported all clear on a
        /// deliberately broken call site.
        /// </para>
        /// </summary>
        private static string CallBlock(string[] lines, int markerIndex)
        {
            // `type: "POST"` sits below the `$.ajax({` that opens its block, so walk back to the
            // opener -- but only a few lines, and never past a different request.
            int start = markerIndex;
            for (int back = markerIndex; back >= Math.Max(0, markerIndex - 8); back--)
            {
                if (CallOpener.IsMatch(lines[back]))
                {
                    start = back;
                    break;
                }
            }

            int end = Math.Min(lines.Length, start + CallBlockLines);
            for (int fwd = start + 1; fwd < end; fwd++)
            {
                if (CallOpener.IsMatch(lines[fwd]))
                {
                    end = fwd;
                    break;
                }
            }

            return Window(lines, start, end - start);
        }

        // ------------------------------------------------------------------
        // The mechanism is present and armed before anything can use it
        // ------------------------------------------------------------------

        [Fact]
        public void The_layout_loads_the_status_message_helper_before_any_other_custom_script()
        {
            // LOAD ORDER IS LOAD-BEARING. StatusMessage.js installs the global ajaxError net, and a
            // page's @section scripts block runs before the bottom-of-body script tags. Loading the
            // helper down there with the other custom scripts would leave the first request of
            // several pages unreported -- the exact silence this item removes, reintroduced by
            // tidying an include into the group where it looks like it belongs.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string layout = StripBlockComments(ReadPortalFile(repoRoot, "Views", "Shared", "_Layout.cshtml"));

            int jquery = layout.IndexOf("vendors/jquery/dist/jquery.min.js", StringComparison.Ordinal);
            int helper = layout.IndexOf("JSScriptLib/Feedback/StatusMessage.js", StringComparison.Ordinal);
            int firstCustom = layout.IndexOf("JSScriptLib/ButtonOperation/", StringComparison.Ordinal);
            int css = layout.IndexOf("css/StatusMessage.css", StringComparison.Ordinal);

            Assert.True(jquery >= 0, "the layout must load jQuery");
            Assert.True(helper >= 0,
                "the layout must load JSScriptLib/Feedback/StatusMessage.js, or NOTHING reports an AJAX failure anywhere in the Portal");
            Assert.True(css >= 0,
                "the layout must link css/StatusMessage.css, or the toasts render as unstyled text at the top of the document");

            Assert.True(jquery < helper,
                "StatusMessage.js must load AFTER jQuery -- it installs its net on $(document) and logs an error and gives up if jQuery is absent");
            Assert.True(firstCustom < 0 || helper < firstCustom,
                "StatusMessage.js must load BEFORE the other custom scripts, so the net is armed before any page script can fire a request");
        }

        [Fact]
        public void The_status_message_helper_installs_a_global_failure_net()
        {
            string repoRoot = ProjectGraph.FindRepoRoot();
            string helper = ReadPortalFile(repoRoot, "wwwroot", "JSScriptLib", "Feedback", "StatusMessage.js");

            Assert.Contains("ajaxError", helper);

            // The three states the owner asked to be able to tell apart. status 0 is the one that
            // did not exist before: a request that never reached the server looked identical to a
            // refusal, because both produced nothing at all.
            Assert.Contains("case 0:", helper);
            Assert.Contains("case 403:", helper);
            Assert.Contains("never reached the server", helper);

            // Without this, every navigation away from a page with an in-flight request puts a
            // sticky red toast on the screen, the guard gets suppressed as noise, and the silence
            // comes back.
            //
            // Asserted against the COMPARISON in comment-stripped source, not the bare word. The
            // first version of this looked for "abort" anywhere in the raw file, which the
            // surrounding comment satisfies -- mutation testing replaced the whole condition with
            // `if (false)` and the guard stayed green. Matching `=== "abort"` was enough to fix it,
            // but it is stripped as well so that both halves of this pair are checked the same way
            // and neither can be re-satisfied by prose.
            Assert.Contains("=== \"abort\"", StripJsComments(helper));

            // THE SUPPRESSION MUST BE AN UNLOAD CHECK, NEVER A VISIBILITY CHECK. The first version
            // of the net skipped reporting when document.visibilityState was "hidden", reasoning
            // that a request dying with status 0 on a hidden page meant the user had navigated
            // away. A BACKGROUND TAB IS ALSO HIDDEN. An operator who switched tabs while a save was
            // in flight got nothing at all -- the "the request never arrived" case, the one this
            // whole item was raised for, suppressed by the mechanism meant to surface it. No unit
            // test could have caught it; it took driving a real browser, where the pane happened to
            // be a background tab and the toast never appeared.
            //
            // Checked against CODE, not the raw file. The comment in StatusMessage.js that records
            // this mistake necessarily contains the word, and asserting on the bare word therefore
            // failed against the corrected file -- the same trap as the "abort" assertion two lines
            // up, hit twice in one sitting.
            Assert.False(StripJsComments(helper).Contains("visibilityState"),
                "the global net must not gate reporting on document.visibilityState -- a background tab is 'hidden', so this silently suppresses failures for any user who switched tabs. Use the unload flag instead.");
            Assert.Contains("pagehide", StripJsComments(helper));

            foreach (string member in new[] { "ok:", "warn:", "refused:", "failed:", "describe" })
            {
                Assert.True(helper.Contains(member),
                    "StatusMessage must expose " + member + " -- call sites here depend on it");
            }

            // Response bodies reach the toast, so the text goes in as text.
            Assert.Contains("textContent", helper);
            // The excluded \s is what makes this work. Without it, \s* backtracks and the character
            // class happily matches the space in `innerHTML = "&times;"`, so a correct assignment
            // reads as a violation -- which is precisely what it did on the first run.
            Assert.False(Regex.IsMatch(helper, @"\.innerHTML\s*=\s*[^\s""']"),
                "StatusMessage must not assign non-literal content through innerHTML: some of what it renders comes from a server response body");
        }

        // ------------------------------------------------------------------
        // Every mutating call site reports its outcome
        // ------------------------------------------------------------------

        [Fact]
        public void Every_mutating_ajax_call_site_reports_success()
        {
            // The rule, applied mechanically. A POST changes something, so somebody has to be told
            // it happened. Failure is not checked here because it is not the call site's job -- see
            // the contract in this class's summary.
            string repoRoot = ProjectGraph.FindRepoRoot();

            // The marker for "this call mutates". $.post is one by definition; $.ajax needs its
            // type read, and it is always on its own line in this codebase.
            Regex post = new Regex(@"\$\.post\s*\(|type:\s*[""']POST[""']", RegexOptions.Compiled);

            List<string> offenders = new List<string>();
            int checkedCount = 0;

            foreach (string file in ScriptBearingFiles(repoRoot))
            {
                string[] lines = LiveLines(File.ReadAllText(file));

                for (int i = 0; i < lines.Length; i++)
                {
                    if (!post.IsMatch(lines[i]))
                    {
                        continue;
                    }

                    checkedCount++;

                    string block = CallBlock(lines, i);

                    if (block.IndexOf("StatusMessage.", StringComparison.Ordinal) < 0)
                    {
                        offenders.Add(ProjectGraph.Rel(repoRoot, file) + ":" + (i + 1) + "  " + lines[i].Trim());
                    }
                }
            }

            Assert.True(checkedCount > 0,
                "the scan must find mutating AJAX call sites, otherwise this guard passes vacuously");

            Assert.True(offenders.Count == 0,
                "mutating AJAX calls that never tell the user whether they worked (ROADMAP 20). Report the outcome through StatusMessage -- ok() for success, refused() for a refusal that arrives as HTTP 200. Transport failures are handled globally and must NOT be announced here:\n  " +
                string.Join("\n  ", offenders));
        }

        [Fact]
        public void No_call_site_announces_a_transport_failure_itself()
        {
            // The other half of the contract. jQuery's ajaxError fires whether or not a call has its
            // own error handler, so a call site that also announces produces TWO messages for one
            // failure. An error handler is still allowed -- reverting a checkbox to the state the
            // database actually holds is necessary -- it just may not talk.
            string repoRoot = ProjectGraph.FindRepoRoot();

            Regex errorHandler = new Regex(@"error:\s*function|\.fail\s*\(\s*function", RegexOptions.Compiled);

            List<string> offenders = new List<string>();

            foreach (string file in ScriptBearingFiles(repoRoot))
            {
                // The helper itself is where the announcing lives.
                if (file.EndsWith("StatusMessage.js", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] lines = LiveLines(File.ReadAllText(file));

                for (int i = 0; i < lines.Length; i++)
                {
                    if (!errorHandler.IsMatch(lines[i]))
                    {
                        continue;
                    }

                    string block = Window(lines, i, 12);
                    if (block.IndexOf("StatusMessage.failed", StringComparison.Ordinal) >= 0
                        || block.IndexOf("StatusMessage.refused", StringComparison.Ordinal) >= 0
                        || block.IndexOf("StatusMessage.ok", StringComparison.Ordinal) >= 0)
                    {
                        offenders.Add(ProjectGraph.Rel(repoRoot, file) + ":" + (i + 1));
                    }
                }
            }

            Assert.True(offenders.Count == 0,
                "these error handlers announce a failure the global net in StatusMessage.js already announces, so the user gets two messages for one failure. Repair local state here and say nothing:\n  " +
                string.Join("\n  ", offenders));
        }

        [Fact]
        public void No_success_handler_uses_a_blocking_dialog()
        {
            // confirm() is the wrong primitive and alert() is the wrong presentation. confirm() on a
            // success callback asks a yes/no question about something that has ALREADY happened,
            // which is not a decision anyone can make -- and in the three Manage*Index pages,
            // answering Cancel skipped the list refresh, so declining a pointless prompt silently
            // left the screen showing membership the database no longer had.
            //
            // A confirm() BEFORE the request is untouched by this and is correct: voiding a
            // statement and regenerating a device credential both ask first, on purpose.
            string repoRoot = ProjectGraph.FindRepoRoot();

            // $.post(url, data, callback) passes its success handler POSITIONALLY, with no
            // `success:` key and no `.done(` anywhere -- which is the shape the three Manage*Index
            // pages use, and therefore the shape every confirm() this item removed was written in.
            // Matching only the two named forms left that entire family unchecked, and mutation
            // testing caught it: confirm() was put back into a live call site and this guard stayed
            // green. A $.post block is a success context by construction.
            Regex successMarker = new Regex(
                @"success:\s*function|\.done\s*\(\s*function|\$\.post\s*\(", RegexOptions.Compiled);
            Regex blocking = new Regex(@"\b(alert|confirm)\s*\(", RegexOptions.Compiled);

            List<string> offenders = new List<string>();
            int checkedCount = 0;

            foreach (string file in ScriptBearingFiles(repoRoot))
            {
                string[] lines = LiveLines(File.ReadAllText(file));

                for (int i = 0; i < lines.Length; i++)
                {
                    if (!successMarker.IsMatch(lines[i]))
                    {
                        continue;
                    }

                    checkedCount++;

                    // Bounded at the next request, and forward-only from the opener, so a confirm()
                    // asked BEFORE the call -- which is the correct place for one -- is not caught.
                    Match hit = blocking.Match(CallBlock(lines, i));
                    if (hit.Success)
                    {
                        offenders.Add(ProjectGraph.Rel(repoRoot, file) + ":" + (i + 1) + "  uses " + hit.Value);
                    }
                }
            }

            Assert.True(checkedCount > 0,
                "the scan must find success handlers, otherwise this guard passes vacuously");

            Assert.True(offenders.Count == 0,
                "blocking alert()/confirm() inside a success handler (ROADMAP 20). Use StatusMessage.ok or StatusMessage.refused -- a toast does not block the page, and confirm() asks a question after the action already happened:\n  " +
                string.Join("\n  ", offenders));
        }

        // ------------------------------------------------------------------
        // The helper that could not report, and the call site it broke
        // ------------------------------------------------------------------

        [Fact]
        public void The_submit_following_helper_stays_gone()
        {
            // It was `$.get(route + id).done(function () { })`: an explicitly empty success handler
            // and no failure handler, so it could not report either outcome by construction. That is
            // also why nobody noticed it had never worked -- its single caller passed a route no
            // controller serves, and the 404 went into an empty function.
            //
            // Not replaced with a fixed generic version. A shared toggle helper has to know the
            // route, the token holder and how to repair the control it belongs to, and guessing
            // those for unrelated callers is what produced the bug.
            string repoRoot = ProjectGraph.FindRepoRoot();

            List<string> offenders = ScriptBearingFiles(repoRoot)
                .Where(f => LiveLines(File.ReadAllText(f))
                    .Any(l => l.Contains("SubmitFollowing(")))
                .Select(f => ProjectGraph.Rel(repoRoot, f))
                .ToList();

            Assert.True(offenders.Count == 0,
                "SubmitFollowing is gone and must stay gone -- it silently swallowed a 404 on every call for as long as it existed:\n  " +
                string.Join("\n  ", offenders));
        }

        [Fact]
        public void The_user_lock_toggle_posts_to_the_action_that_actually_exists()
        {
            // The concrete bug behind the rule. This checkbox called SubmitFollowing('/ArchiveToggle',
            // id), which GET-requested "/ArchiveToggle<guid>". There is no ArchiveToggle on
            // UserController and the real action, LockoutToggle, is [HttpPost] +
            // [ValidateAntiForgeryToken], so a GET could never have reached it either.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string modal = ReadPortalFile(repoRoot, "Views", "User", "DetailsModal.cshtml");
            string[] live = LiveLines(modal);
            string joined = string.Join("\n", live);

            Assert.False(joined.Contains("/ArchiveToggle"),
                "UserController has no ArchiveToggle action -- this route 404d on every click");

            Assert.Contains("/User/LockoutToggle", joined);
            Assert.Contains("\"POST\"", joined);
            Assert.Contains("__RequestVerificationToken", joined);

            // The token has to be INSIDE this partial: it is loaded into #modalContent over AJAX, so
            // a token rendered by the host page is not reachable from here.
            Assert.Contains("Html.AntiForgeryToken()", joined);

            // The action answers with the state it stored. Showing the click instead of the answer
            // is how a refused lock ends up looking applied.
            Assert.Contains("result.lockedOut", joined);
        }

        [Fact]
        public void The_read_only_lock_indicators_stay_read_only()
        {
            // The four user index partials render this checkbox `disabled` and Hardware does the
            // same, for the reason written at Views/Hardware/IndexPartial.cshtml under audit C-09:
            // there is no one-click POST action behind them. Wiring an onchange here without adding
            // one would recreate exactly the bug this file exists to prevent, and now that failures
            // are visible it would do it loudly on every click.
            string repoRoot = ProjectGraph.FindRepoRoot();

            string[] indexPartials =
            {
                "IndexPartial.cshtml",
                "StudentIndexPartial.cshtml",
                "EducatorIndexPartial.cshtml",
                "AdminIndexPartial.cshtml"
            };

            int checkedCount = 0;
            List<string> offenders = new List<string>();

            foreach (string name in indexPartials)
            {
                string path = PortalPath(repoRoot, "Views", "User", name);
                if (!File.Exists(path))
                {
                    continue;
                }

                checkedCount++;
                string[] live = LiveLines(File.ReadAllText(path));

                for (int i = 0; i < live.Length; i++)
                {
                    if (live[i].Contains("IsLockedOut") && live[i].Contains("onchange"))
                    {
                        offenders.Add(ProjectGraph.Rel(repoRoot, path) + ":" + (i + 1));
                    }
                }
            }

            Assert.True(checkedCount > 0, "the user index partials must be found, otherwise this guard passes vacuously");

            Assert.True(offenders.Count == 0,
                "the lock checkbox on the user index partials is a READ-ONLY indicator. Giving it an onchange needs a POST action, a token inside the AJAX-loaded partial, and a success message -- see Views/User/DetailsModal.cshtml for the shape:\n  " +
                string.Join("\n  ", offenders));
        }
    }
}
