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
    /// ROADMAP 17: no Portal action or view is unreachable by a human without a recorded reason.
    ///
    /// <para>
    /// WHY. "Capabilities that are built, registered and tested, and that no human can reach" was
    /// the recurring defect class of the whole node audit. Audits kept reporting them as shipped
    /// because the code genuinely is. The first sweep (2026-08-23) found a 50-action chart scaffold
    /// on ten controllers, a whole commerce button layer the teardown left behind, two content
    /// Edit buttons hidden behind a claim the node never issues, a CSV import with no file input,
    /// and a modal slot that 404s because its dispatcher case is commented out. None of those was
    /// visible to any existing test, because every existing test reaches the code directly.
    /// </para>
    ///
    /// <para>
    /// HOW. Every public action in every Portal controller, and every view, must have a reference
    /// somewhere a human can follow: a tag helper, a route literal, a redirect, a button dispatch,
    /// a rendered partial -- or one of the two mechanisms that never spell the action name at all
    /// (see <see cref="References"/>). Anything unreferenced must be in <see cref="Pending"/> with
    /// the owner's open question. Pending may only shrink.
    /// </para>
    ///
    /// <para>
    /// WHAT IT CANNOT SEE, stated so nobody over-trusts it: a route composed from a data attribute
    /// this scan does not model, an action reached only by an external client (the PC launcher,
    /// curl), or a link that resolves but lands on a page that does not work. The first sweep's
    /// adjudication found every one of those by hand; this guard stops the class from regrowing,
    /// it does not replace the reading.
    /// </para>
    /// </summary>
    public class PortalReachabilityGuardTests
    {
        private const string PortalRel = "enduser/FebrisEndUserPortal";

        /// <summary>
        /// Unreachable by design or awaiting an owner ruling, each with the question. DO NOT add an
        /// entry to make a failure go away: add the link, or delete the code, or bring the owner
        /// the question and record it here with a date.
        /// </summary>
        private static readonly Dictionary<string, string> Pending = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // The NodeAdminToken/Mint entry that led this list is gone the right way: ROADMAP 16
            // moved package upload, feed sync and module upload into the Portal behind cookie
            // auth and deleted the token with them, as the owner's ruling required.

            // ANSWERED 2026-08-24, and the answers are in the tree rather than here:
            //  - Widget/ImageLoader: FIX. The three commented entity-image renders (Actor, Module,
            //    TestUser modals) are live again, with the null-handling their commented versions
            //    lacked. The action is reachable, so it needs no entry.
            //  - Actor Test Index: DELETE, superseded by Test User. Views, the commented actions,
            //    the nav link, IActorLogic.GetTestActors and the IActorQueries batch overload that
            //    existed only for it all went.
            //  - CohortMember standalone listing: DELETE. Membership management is
            //    Cohort/ManageMemberIndex, which this was never part of. DetailsModal and
            //    LoadCohortList stay.
            //  - TestUser editing: STAYS DELETED. A test user must not look editable, or somebody
            //    mistakes it for a real one.
            //  - The marketing-area button family: DELETED, no longer needed on a user node.

            // OWNER RULING 2026-08-24: Location is KEPT. It is the surface for a company running one
            // node across multiple locations, meant as a way to filter. It does not work today --
            // create and edit return null on a node (the LocationLogic guard wants SuperAdmin),
            // nothing links the surface, its button groups route to a hub controller that does not
            // exist here, and its details modal is a Map slot plus seven SOME_VARIABLE
            // placeholders. So this is a REPAIR still to be scoped (what it filters is the open
            // question), not an open keep-or-delete question, and the entries stay until it lands.
            ["action:Location/DetailsModal"] = "OWNER 2026-08-24: KEEP, multi-location filtering. Repair pending, scope not yet set",
            // GET Delete and the POST DeleteConfirmed method share this route key, because
            // [ActionName("Delete")] renames the POST and this guard keys by ROUTE name.
            ["action:Location/Delete"] = "OWNER 2026-08-24: KEEP, multi-location filtering. Repair pending, scope not yet set",

            // OWNER RULING 2026-08-24: RemoteImageLoader is RETAINED DELIBERATELY. It is a
            // federation-gated byte proxy for hub-hosted images and no node screen renders one
            // today, but if the hub is used again it is needed again. Unreachable by design rather
            // than by accident, which is what this list is for.
            ["action:Widget/RemoteImageLoader"] = "OWNER 2026-08-24: retained for a future hub, dormant behind the federation gate",
        };

        /// <summary>
        /// Same contract as <see cref="Pending"/> but for whole families, matched by key prefix.
        /// Currently empty, and it should stay that way: a prefix exempts everything beneath it, so
        /// it is the widest tool here and the easiest one to hide behind.
        /// </summary>
        private static readonly Dictionary<string, string> PendingPrefixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // EMPTY, and it earned that. The marketing-area button family (Campaign, CampaignMember,
            // CampaignNote, EmailCampaignMessage, Lead and its satellites, MarketingAnalytics,
            // TeamMemberAssignedToCampaign) was the only occupant: adjudicated DEAD by the
            // 2026-08-23 sweep but held because NODE_REMOTE_TEARDOWN_PLAN.md Phase 6 said not to
            // touch the marketing area. The owner ruled 2026-08-24 that a user node does not need
            // it, and all 61 files went (35 partials across the twelve folders, 14 individual
            // button partials, 12 ButtonOperation.js). Keep this list empty the same way Pending shrinks: by
            // answering the question, not by widening the exemption.
        };

        private static bool IsPending(string key)
        {
            return Pending.ContainsKey(key)
                || PendingPrefixes.Keys.Any(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Modal views whose Element slots are part of an OPEN owner question, exempt from the two
        /// slot guards below. Same discipline as <see cref="Pending"/>: dated, never added just to
        /// silence a failure, and a stale entry fails <see cref="Pending_only_shrinks"/>.
        /// </summary>
        private static readonly Dictionary<string, string> HeldSlotViews = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Views/Location/DetailsModal.cshtml"] = "OWNER 2026-08-23: is Location a node feature at all? Element0=Map targets the deleted map surface, Element1-7 are literal SOME_VARIABLE placeholders, and LoadPartialDetail has no location case",
        };

        // ------------------------------------------------------------------
        // Source model
        // ------------------------------------------------------------------

        private sealed class Action
        {
            public string Controller;
            public string Name;        // the ROUTE name: [ActionName] wins over the method name
            public string File;
            public int Line;
            public string Key => "action:" + Controller + "/" + Name;
        }

        private sealed class View
        {
            public string Path;        // relative to the Portal, forward slashes
            public string Folder;      // Views/<Folder>/...
            public string Name;        // file name without .cshtml
            public bool IsPartial => Name.StartsWith("_", StringComparison.Ordinal);
            public string Key => "view:" + Path;
        }

        private sealed class Source
        {
            public string Path;
            public string Live;        // comments stripped
            public string Folder;      // Views/<Folder> or null
            public string Kind;        // view | js | cs
        }

        private static string StripRazor(string s)
        {
            s = Regex.Replace(s, @"@\*.*?\*@", " ", RegexOptions.Singleline);
            s = Regex.Replace(s, @"<!--.*?-->", " ", RegexOptions.Singleline);
            // inline <script> bodies are JavaScript and carry their own comments
            s = Regex.Replace(s, @"<script\b[^>]*>(.*?)</script>",
                m => "<script>" + SelfRecursivePropertyGuardTests.StripCommentsPreservingLayout(m.Groups[1].Value) + "</script>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            return s;
        }

        private static readonly Regex ActionDecl = new Regex(
            @"^[ \t]*public\s+(?:async\s+)?(?:Task<)?(?:IActionResult|ActionResult|JsonResult|PartialViewResult|ViewResult|FileResult|RedirectToActionResult)>?\s+(?<name>\w+)\s*\(",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static List<Action> Actions(string portal)
        {
            List<Action> list = new List<Action>();
            foreach (string file in Directory.EnumerateFiles(Path.Combine(portal, "Controllers"), "*Controller.cs", SearchOption.AllDirectories))
            {
                string live = SelfRecursivePropertyGuardTests.StripCommentsPreservingLayout(File.ReadAllText(file));
                string controller = Path.GetFileName(file).Replace("Controller.cs", string.Empty);
                foreach (Match m in ActionDecl.Matches(live))
                {
                    // Attributes sit in the lines immediately above the declaration. The comment
                    // stripper preserves layout, so a stripped comment line is whitespace rather than
                    // empty, and a search for "\n\n" walks straight past it into the previous method.
                    // Split on whitespace-only lines instead.
                    string[] prior = Regex.Split(live.Substring(0, m.Index), @"\n[ \t]*\n");
                    string above = prior[prior.Length - 1];
                    if (above.Contains("[NonAction]"))
                    {
                        continue;
                    }
                    // `[HttpPost, ActionName("Delete")]` shares its brackets with the verb, so the
                    // attribute is matched by name, not by an opening bracket of its own.
                    Match alias = Regex.Match(above, @"\bActionName\(\s*""(?<n>\w+)""\s*\)");
                    list.Add(new Action
                    {
                        Controller = controller,
                        Name = alias.Success ? alias.Groups["n"].Value : m.Groups["name"].Value,
                        File = Rel(portal, file),
                        Line = live.Substring(0, m.Index).Count(c => c == '\n') + 1,
                    });
                }
            }
            return list;
        }

        private static List<View> Views(string portal)
        {
            List<View> list = new List<View>();
            foreach (string file in Directory.EnumerateFiles(Path.Combine(portal, "Views"), "*.cshtml", SearchOption.AllDirectories))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name == "_ViewStart" || name == "_ViewImports" || name == "_Layout")
                {
                    continue; // the pipeline renders these, not a reference
                }
                string rel = Rel(portal, file);
                string[] parts = rel.Split('/');
                list.Add(new View { Path = rel, Folder = parts.Length > 2 ? parts[1] : string.Empty, Name = name });
            }
            return list;
        }

        private static List<Source> Corpus(string portal)
        {
            List<Source> list = new List<Source>();
            void Add(string root, string pattern, string kind, Func<string, string> strip)
            {
                string dir = Path.Combine(portal, root);
                if (!Directory.Exists(dir)) return;
                foreach (string f in Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories))
                {
                    if (ProjectGraph.IsInBuildOutput(f)) continue;
                    string rel = Rel(portal, f);
                    string[] parts = rel.Split('/');
                    list.Add(new Source
                    {
                        Path = rel,
                        Live = strip(File.ReadAllText(f)),
                        Folder = parts[0] == "Views" && parts.Length > 2 ? parts[1] : null,
                        Kind = kind,
                    });
                }
            }
            Add("Views", "*.cshtml", "view", StripRazor);
            Add("Areas", "*.cshtml", "view", StripRazor);
            Add("Pages", "*.cshtml", "view", StripRazor);
            Add(Path.Combine("wwwroot", "JSScriptLib"), "*.js", "js", SelfRecursivePropertyGuardTests.StripCommentsPreservingLayout);
            Add("Controllers", "*.cs", "cs", SelfRecursivePropertyGuardTests.StripCommentsPreservingLayout);
            foreach (string f in new[] { "Startup.cs", "Program.cs" })
            {
                string p = Path.Combine(portal, f);
                if (File.Exists(p))
                {
                    list.Add(new Source { Path = f, Live = SelfRecursivePropertyGuardTests.StripCommentsPreservingLayout(File.ReadAllText(p)), Kind = "cs" });
                }
            }
            return list;
        }

        private static string Rel(string root, string path)
        {
            return Path.GetRelativePath(root, path).Replace('\\', '/');
        }

        // ------------------------------------------------------------------
        // The reference rules
        // ------------------------------------------------------------------

        /// <summary>
        /// True when something a human can follow names this action.
        ///
        /// <para>
        /// Two mechanisms here never spell the action name, and the first sweep's enumerator
        /// missed ~25 reachable actions because of them. (1) A modal's
        /// <c>&lt;input id="ElementN" value="X"&gt;</c> is read by <c>LoadViewDetails.js</c>, which calls
        /// <c>Widget/LoadPartialDetail?modelName=&lt;controller from the page URL&gt;&amp;variableName=X</c>,
        /// and <c>WidgetController.LoadPartialDetail</c> does <c>RedirectToAction("Load" + X)</c>. So
        /// <c>value="MemberList"</c> in <c>Views/Cohort/...</c> is a reference to <c>Cohort/LoadMemberList</c>.
        /// (2) <c>[ActionName("Delete")]</c> on a method called <c>DeleteConfirmed</c> makes the
        /// route <c>Delete</c>, which <see cref="Actions"/> already honours.
        /// </para>
        /// </summary>
        private static bool Referenced(Action a, List<Source> corpus)
        {
            string C = Regex.Escape(a.Controller);
            string A = Regex.Escape(a.Name);
            RegexOptions ci = RegexOptions.IgnoreCase;

            foreach (Source s in corpus)
            {
                string t = s.Live;
                if (Regex.IsMatch(t, @"asp-controller=""" + C + @"""[^>]*asp-action=""" + A + @"""", ci)) return true;
                if (Regex.IsMatch(t, @"asp-action=""" + A + @"""[^>]*asp-controller=""" + C + @"""", ci)) return true;
                if (Regex.IsMatch(t, @"Url\.Action\(\s*""" + A + @"""\s*,\s*""" + C + @"""", ci)) return true;
                if (Regex.IsMatch(t, @"ActionLink\([^)]*""" + A + @"""\s*,\s*""" + C + @"""", ci)) return true;
                if (Regex.IsMatch(t, @"RedirectToAction\(\s*""" + A + @"""\s*,\s*""" + C + @"""", ci)) return true;
                // route literals, case-insensitive like MVC routing: "/C/A", "../C/A", "C/A?", "/C/A"
                if (Regex.IsMatch(t, @"[""'](?:\.\./|/)?" + C + "/" + A + @"(?:[""'?/\\ #]|$)", ci | RegexOptions.Multiline)) return true;
                // "/" + controller + "/A" with the controller taken from the page
                if (Regex.IsMatch(t, @"""/""\s*\+\s*controller\s*\+\s*""/" + A + @"[""?]", ci)) return true;

                bool sameController = string.Equals(s.Folder, a.Controller, StringComparison.OrdinalIgnoreCase)
                    || (s.Kind == "cs" && s.Path.EndsWith("/" + a.Controller + "Controller.cs", StringComparison.OrdinalIgnoreCase));
                if (sameController)
                {
                    if (Regex.IsMatch(t, @"asp-action=""" + A + @"""", ci)) return true;
                    if (Regex.IsMatch(t, @"Url\.Action\(\s*""" + A + @"""\s*[,)]", ci)) return true;
                    if (Regex.IsMatch(t, @"RedirectToAction\(\s*(?:""" + A + @"""|nameof\(" + A + @"\))\s*[,)]", ci)) return true;
                    // the Element<N> hidden-input dispatcher: value="X" means Load<X>
                    if (a.Name.StartsWith("Load", StringComparison.Ordinal) && a.Name.Length > 4)
                    {
                        string X = Regex.Escape(a.Name.Substring(4));
                        if (Regex.IsMatch(t, @"id=""Element\d+""[^>]*value=""" + X + @"""", ci)) return true;
                        if (Regex.IsMatch(t, @"value=""" + X + @"""[^>]*id=""Element\d+""", ci)) return true;
                    }
                }

                // a per-entity ButtonOperation script dispatching on the action name
                if (s.Kind == "js" && s.Path.EndsWith("/" + a.Controller + "ButtonOperation.js", StringComparison.OrdinalIgnoreCase)
                    && Regex.IsMatch(t, @"case\s+""" + A + @"""", ci)) return true;
            }
            return false;
        }

        private static bool Rendered(View v, List<Source> corpus)
        {
            string N = Regex.Escape(v.Name);
            string F = Regex.Escape(v.Folder);
            // The single biggest blind spot of the first sweep's enumerator: _IndexButtonGroupPartial
            // and _DetailsButtonGroupPartial exist in 30+ GenericButtons entity folders, so a
            // leaf-name match let ONE live folder's render vouch for every dead folder's copy
            // (~80 files passed as referenced). For any partial that shares its leaf name with
            // another view, only a parent-folder-qualified render counts.
            string parent = v.Path.Contains('/') ? v.Path.Substring(0, v.Path.LastIndexOf('/')) : string.Empty;
            string parentLeaf = parent.Contains('/') ? parent.Substring(parent.LastIndexOf('/') + 1) : parent;
            // Ambiguity is judged within Views/ only: the Identity AREA carries its own copies of
            // _LoginPartial and _ValidationScriptsPartial, but Razor's search order resolves a
            // pathless name from an area page to the area copy and from a main view to the
            // Views/Shared copy, so the pair is not actually ambiguous.
            bool leafIsAmbiguous = corpus.Count(s => s.Kind == "view"
                && s.Path.StartsWith("Views/", StringComparison.Ordinal)
                && s.Path.EndsWith("/" + v.Name + ".cshtml", StringComparison.OrdinalIgnoreCase)) > 1;
            foreach (Source s in corpus)
            {
                string t = s.Live;
                if (v.IsPartial || v.Folder == "Shared" || v.Folder == "Widget")
                {
                    // A pathless render resolves within the renderer's OWN tree first, so a render
                    // in Pages/ or Areas/ whose tree carries its own copy of this leaf name says
                    // nothing about the Views/ copy. Pages/Shared/_Layout rendering its own
                    // _LoginPartial must not vouch for Views/Shared/_LoginPartial (a mutation
                    // proved it did).
                    if (s.Kind == "view" && !s.Path.StartsWith("Views/", StringComparison.Ordinal))
                    {
                        string tree = s.Path.Split('/')[0];
                        bool ownCopy = corpus.Any(c => c.Kind == "view"
                            && c.Path.StartsWith(tree + "/", StringComparison.Ordinal)
                            && c.Path.EndsWith("/" + v.Name + ".cshtml", StringComparison.OrdinalIgnoreCase));
                        if (ownCopy)
                        {
                            continue;
                        }
                    }
                    string q = leafIsAmbiguous ? Regex.Escape(parentLeaf) + "/" : "[^\"]*\\b";
                    if (Regex.IsMatch(t, @"(?:Partial(?:Async)?|RenderPartial(?:Async)?|PartialView)\(\s*""[^""]*" + q + N + @"""")) return true;
                    if (Regex.IsMatch(t, @"<partial\s+name=""[^""]*" + q + N + @"""")) return true;
                }
                if (!v.IsPartial)
                {
                    bool ownController = s.Kind == "cs" && s.Path.EndsWith("/" + v.Folder + "Controller.cs", StringComparison.OrdinalIgnoreCase);
                    if (ownController)
                    {
                        if (Regex.IsMatch(t, @"\bView\(\s*""" + N + @"""")) return true;
                        if (Regex.IsMatch(t, @"PartialView\(\s*""" + N + @"""")) return true;
                        // implicit View() from the action of the same name
                        Match decl = Regex.Match(t, @"public\s+(?:async\s+)?(?:Task<)?\w+>?\s+" + N + @"\s*\([^)]*\)\s*\{");
                        if (decl.Success)
                        {
                            int end = t.IndexOf("\n        }", decl.Index, StringComparison.Ordinal);
                            string body = end > 0 ? t.Substring(decl.Index, end - decl.Index) : t.Substring(decl.Index);
                            // View() / View(model) / PartialView() / PartialView(model): the view name
                            // is implied by the action name. Only a STRING first argument names a
                            // different view, and that case is handled above.
                            if (Regex.IsMatch(body, @"\b(?:Partial)?View\(\s*(?:\)|[^""\s)])")) return true;
                        }
                    }
                    // a shared page view is rendered implicitly by ANY controller's action of that name
                    if (v.Folder == "Shared" && s.Kind == "cs" && Regex.IsMatch(t, @"public\s+(?:async\s+)?(?:Task<)?\w+>?\s+" + N + @"\s*\(")) return true;
                    if (Regex.IsMatch(t, @"PartialView\(\s*""\.\./" + F + "/" + N + @"""")) return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // The guards
        // ------------------------------------------------------------------

        [Fact]
        public void Every_portal_action_is_reachable_or_pending_with_a_reason()
        {
            string portal = Path.Combine(ProjectGraph.FindRepoRoot(), PortalRel.Replace('/', Path.DirectorySeparatorChar));
            List<Action> actions = Actions(portal);
            List<Source> corpus = Corpus(portal);

            Assert.True(actions.Count >= 100,
                "the action scan found only " + actions.Count + ", which means it has gone blind rather than clean");

            List<string> orphans = actions
                .Where(a => !IsPending(a.Key) && !Referenced(a, corpus))
                .Select(a => a.Key + "  (" + a.File + ":" + a.Line + ")")
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();

            Assert.True(orphans.Count == 0,
                "Portal actions nothing a human can follow refers to (ROADMAP 17). Wire them, delete them, or bring the owner the question and record it in Pending:\n  " +
                string.Join("\n  ", orphans));
        }

        [Fact]
        public void Every_portal_view_is_rendered_or_pending_with_a_reason()
        {
            string portal = Path.Combine(ProjectGraph.FindRepoRoot(), PortalRel.Replace('/', Path.DirectorySeparatorChar));
            List<View> views = Views(portal);
            List<Source> corpus = Corpus(portal);

            Assert.True(views.Count >= 100,
                "the view scan found only " + views.Count + ", which means it has gone blind rather than clean");

            List<string> orphans = views
                .Where(v => !IsPending(v.Key) && !Rendered(v, corpus))
                .Select(v => v.Key)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();

            Assert.True(orphans.Count == 0,
                "Portal views nothing renders (ROADMAP 17). Delete them, render them, or record the owner's question in Pending:\n  " +
                string.Join("\n  ", orphans));
        }

        [Fact]
        public void Pending_only_shrinks()
        {
            // Every Pending entry must still exist, or it is a stale allowlist line that would
            // silently re-admit a new orphan of the same name. And nothing may be added without a
            // dated owner question in its reason.
            string portal = Path.Combine(ProjectGraph.FindRepoRoot(), PortalRel.Replace('/', Path.DirectorySeparatorChar));
            HashSet<string> actionKeys = new HashSet<string>(Actions(portal).Select(a => a.Key), StringComparer.OrdinalIgnoreCase);
            HashSet<string> viewKeys = new HashSet<string>(Views(portal).Select(v => v.Key), StringComparer.OrdinalIgnoreCase);

            List<string> stale = Pending.Keys.Where(k => !actionKeys.Contains(k) && !viewKeys.Contains(k)).ToList();
            Assert.True(stale.Count == 0,
                "Pending names things that no longer exist -- remove the entries, the question is answered:\n  " + string.Join("\n  ", stale));

            List<string> stalePrefixes = PendingPrefixes.Keys
                .Where(p => !viewKeys.Any(k => k.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                         && !actionKeys.Any(k => k.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            Assert.True(stalePrefixes.Count == 0,
                "PendingPrefixes cover nothing any more -- remove them, the family is gone:\n  " + string.Join("\n  ", stalePrefixes));

            List<string> staleHeld = HeldSlotViews.Keys
                .Where(k => !File.Exists(Path.Combine(portal, k.Replace('/', Path.DirectorySeparatorChar))))
                .ToList();
            Assert.True(staleHeld.Count == 0,
                "HeldSlotViews name views that no longer exist -- remove the entries:\n  " + string.Join("\n  ", staleHeld));

            List<string> undated = Pending.Concat(PendingPrefixes).Concat(HeldSlotViews)
                .Where(kv => !Regex.IsMatch(kv.Value, @"\b20\d\d-\d\d-\d\d\b"))
                .Select(kv => kv.Key)
                .ToList();
            Assert.True(undated.Count == 0,
                "Pending entries must carry a dated owner question:\n  " + string.Join("\n  ", undated));
        }

        [Fact]
        public void Every_modal_slot_has_a_live_dispatcher_case()
        {
            // The mechanism behind the Element<N> inputs has its own failure mode: the slot, the
            // action, the logic and the partial all exist, and the modal still spins forever,
            // because WidgetController.LoadPartialDetail has no live `case "<controller>"` to route
            // the request. TestUser's case was commented out and its Actor Info slot 404'd for as
            // long as the TestUser restoration had existed.
            string portal = Path.Combine(ProjectGraph.FindRepoRoot(), PortalRel.Replace('/', Path.DirectorySeparatorChar));
            string widget = SelfRecursivePropertyGuardTests.StripCommentsPreservingLayout(
                File.ReadAllText(Path.Combine(portal, "Controllers", "WidgetController.cs")));

            int dispatcher = widget.IndexOf("LoadPartialDetail(", StringComparison.Ordinal);
            Assert.True(dispatcher >= 0, "WidgetController.LoadPartialDetail not found");
            HashSet<string> cases = new HashSet<string>(
                Regex.Matches(widget.Substring(dispatcher), @"case\s+""(?<c>\w+)""").Cast<Match>().Select(m => m.Groups["c"].Value),
                StringComparer.OrdinalIgnoreCase);

            List<string> broken = new List<string>();
            foreach (Source s in Corpus(portal).Where(s => s.Folder != null && s.Folder != "Shared" && s.Folder != "Widget"))
            {
                if (HeldSlotViews.ContainsKey(s.Path))
                {
                    continue;
                }
                if (Regex.IsMatch(s.Live, @"id=""Element\d+""") && !cases.Contains(s.Folder))
                {
                    broken.Add(s.Path + " has Element slots but LoadPartialDetail has no live case \"" + s.Folder.ToLowerInvariant() + "\"");
                }
            }

            Assert.True(cases.Count >= 5, "the dispatcher scan found only " + cases.Count + " cases, which means it has gone blind");
            Assert.True(broken.Count == 0,
                "modal slots that can never load -- the view asks the dispatcher for a controller it does not route:\n  " + string.Join("\n  ", broken));
        }

        [Fact]
        public void Every_modal_slot_names_a_load_action_that_exists()
        {
            // The inverse failure of the reference scan, and the one the first sweep's critic had
            // to find by hand: a LIVE slot naming Load<X> that no controller defines. Three shipped
            // that way: Curriculum's ModuleIndex slot (LoadModuleIndex was never written, so every
            // Curriculum details modal spun forever on a reachable page), Module's Classification
            // slot, and Location's whole slot grid. The slot dispatches to the controller of the
            // page the modal is opened FROM; for every current modal that is the modal's own
            // folder, which is what this asserts.
            string portal = Path.Combine(ProjectGraph.FindRepoRoot(), PortalRel.Replace('/', Path.DirectorySeparatorChar));
            ILookup<string, string> actionsByController = Actions(portal)
                .ToLookup(a => a.Controller, a => a.Name, StringComparer.OrdinalIgnoreCase);

            List<string> broken = new List<string>();
            int slots = 0;
            foreach (Source s in Corpus(portal).Where(s => s.Kind == "view" && s.Folder != null && s.Folder != "Shared" && s.Folder != "Widget"))
            {
                if (HeldSlotViews.ContainsKey(s.Path))
                {
                    continue;
                }
                foreach (Match m in Regex.Matches(s.Live, @"id=""Element\d+""[^>]*value=""(?<x>\w+)"""))
                {
                    slots++;
                    string load = "Load" + m.Groups["x"].Value;
                    if (!actionsByController[s.Folder].Contains(load, StringComparer.OrdinalIgnoreCase))
                    {
                        broken.Add(s.Path + " slot \"" + m.Groups["x"].Value + "\" -> " + s.Folder + "/" + load + " which does not exist");
                    }
                }
            }

            Assert.True(slots >= 10, "the slot scan found only " + slots + " live slots, which means it has gone blind");
            Assert.True(broken.Count == 0,
                "live modal slots naming actions that were never written -- each is a permanent spinner on a reachable page:\n  " + string.Join("\n  ", broken));
        }

        [Fact]
        public void Every_tag_helper_link_names_an_action_that_exists()
        {
            // The other inverse failure: a live link to an action that does not exist. The nav had
            // one for as long as the node has existed -- _Layout's Analytics menu linked
            // LocalAnalytics/SortedIndex, an action never written, so the entry 404'd for every
            // admin who clicked it. The reference scan cannot see this because it walks actions
            // looking for references, never references looking for actions.
            string portal = Path.Combine(ProjectGraph.FindRepoRoot(), PortalRel.Replace('/', Path.DirectorySeparatorChar));
            ILookup<string, string> actionsByController = Actions(portal)
                .ToLookup(a => a.Controller, a => a.Name, StringComparer.OrdinalIgnoreCase);
            HashSet<string> controllers = new HashSet<string>(actionsByController.Select(g => g.Key), StringComparer.OrdinalIgnoreCase);

            List<string> broken = new List<string>();
            int links = 0;
            foreach (Source s in Corpus(portal).Where(s => s.Kind == "view"))
            {
                foreach (Match m in Regex.Matches(s.Live, @"<(?:a|form|img)\b[^>]*asp-controller=""(?<c>\w+)""[^>]*>"))
                {
                    string tag = m.Value;
                    if (Regex.IsMatch(tag, @"asp-area=""\w"))
                    {
                        continue; // areas route to Razor Pages, not these controllers
                    }
                    Match act = Regex.Match(tag, @"asp-action=""(?<a>\w+)""");
                    if (!act.Success)
                    {
                        continue;
                    }
                    links++;
                    string c = m.Groups["c"].Value;
                    string a = act.Groups["a"].Value;
                    if (!controllers.Contains(c))
                    {
                        broken.Add(s.Path + " links " + c + "/" + a + " but there is no " + c + "Controller");
                    }
                    else if (!actionsByController[c].Contains(a, StringComparer.OrdinalIgnoreCase))
                    {
                        broken.Add(s.Path + " links " + c + "/" + a + " which does not exist");
                    }
                }
            }

            Assert.True(links >= 30, "the link scan found only " + links + " tag-helper links, which means it has gone blind");
            Assert.True(broken.Count == 0,
                "live links to actions that do not exist -- each 404s for whoever clicks it:\n  " + string.Join("\n  ", broken));
        }
    }
}
