#!/usr/bin/env python3
"""Mutation harness for the node's architecture and contract guards.

A guard that has never been seen to fail is not a guard. Every mutation below breaks exactly one
thing a guard claims to protect, and the NAMED test must turn red.

It has earned its keep. Runs of these sets found three holes in the AJAX guards, and later six in the
self-recursive-getter guard -- including two shapes that guard was specifically written to catch, and
the fact that its "first version could not see the defect" claim is reproducible at all is down to
the `getter-guard` mutation that restores the original regex.

WHY THIS LIVES IN THE REPO. It used to live in a session scratch directory, which made every claim
resting on it unreproducible, and one run's log was overwritten by the next so its evidence stopped
existing. Tooling that backs a claim in a pull request has to be runnable by whoever reads the claim.

    python tests/mutation/run-mutations.py --check        # anchors only, ~1 second, read-only
    python tests/mutation/run-mutations.py                # every set
    python tests/mutation/run-mutations.py getter-guard   # one set

Run `--check` first. It answers "are these mutations still testing anything?" in a second, which is
the only question that makes the twenty-minute run worth starting.

Exit code is 0 only if every mutation in every selected set was caught. Expect tens of minutes: each
mutation is a full `dotnet test` invocation.

THIS SCRIPT EDITS TRACKED FILES IN PLACE and restores them. Four safeguards, each of which exists
because an earlier version got it wrong:

  1. It REFUSES to run when tracked files are modified. Restoration is from memory, so a crash
     mid-run would otherwise be indistinguishable from your own uncommitted edits.
  2. Reads and writes are BYTE-EXACT for the byte-order mark and line endings, while MATCHING is done
     on newline-normalised text. One earlier version read and wrote `utf-8-sig` at both ends, which
     silently ADDED a BOM to every BOM-less file it restored -- it corrupted ten files across two
     runs before anyone noticed. The next attempt fixed that by going fully byte-exact and broke
     every multi-line anchor instead, because this tree is checked out CRLF and the anchors use \n.
  3. After each mutation it verifies with `git diff` that the tree came back CLEAN, and aborts the
     run if it did not. A harness that leaves debris behind is worse than no harness.
  4. A stale anchor is a FAILURE, not a skip. A mutation whose anchor no longer matches has silently
     stopped testing anything, which looks exactly like a pass. This rule immediately caught seven
     anchors that a bad refactor of this very file had corrupted.
  5. Every test a set names is run ONCE on the clean tree first and must be green. A guard that is
     red without any mutation "catches" all of them and proves nothing -- and one shipped exactly
     that way, with a 5/5 run in its commit message, before this check existed.
"""
import functools
import io
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))

# A full run is 38 `dotnet test` invocations over tens of minutes. Python buffers stdout when it is
# redirected, so without this the run looks hung until the moment it finishes, and a tool nobody can
# watch is a tool people kill and stop trusting.
print = functools.partial(print, flush=True)  # noqa: A001

# ---------------------------------------------------------------------------------------------
# The mutation sets.
#
# Each block below is spliced verbatim from the working harness that first proved it, at column 0
# and with its original constant names. The names are REBOUND between blocks rather than scoped in
# functions, on purpose: nesting them meant re-indenting, and re-indenting silently rewrote the
# insides of every multi-line anchor.
# ---------------------------------------------------------------------------------------------

# ---- ajax-feedback: ROADMAP 20, the browser reports the outcome of every mutating AJAX call ----
P = os.path.join(REPO, 'enduser', 'FebrisEndUserPortal')
T = 'tests/FebrisArchitectureTests/Febris.ArchitectureTests.csproj'

MUTATIONS = [
    ('layout include removed',
     P + r'\Views\Shared\_Layout.cshtml',
     '<script src="~/JSScriptLib/Feedback/StatusMessage.js"></script>',
     '',
     'The_layout_loads_the_status_message_helper_before_any_other_custom_script'),

    ('layout include moved after jQuery is fine but before it is not',
     P + r'\Views\Shared\_Layout.cshtml',
     '<script src="~/gentelella-master/vendors/jquery/dist/jquery.min.js"></script>',
     '<script src="~/JSScriptLib/ButtonOperation/Generic/ButtonActions.js"></script>\n    <script src="~/gentelella-master/vendors/jquery/dist/jquery.min.js"></script>',
     'The_layout_loads_the_status_message_helper_before_any_other_custom_script'),

    ('a mutating call site stops reporting success',
     P + r'\Views\Cohort\ManageMemberIndex.cshtml',
     'window.StatusMessage.ok("Member added.");',
     '',
     'Every_mutating_ajax_call_site_reports_success'),

    ('an error handler announces the failure the net already announces',
     P + r'\Views\Cohort\IndexPartial.cshtml',
     'error: function () {\n                checkbox.checked = !desired;',
     'error: function (xhr) {\n                window.StatusMessage.failed("nope", xhr);\n                checkbox.checked = !desired;',
     'No_call_site_announces_a_transport_failure_itself'),

    ('confirm() comes back in a success handler',
     P + r'\Views\Cohort\ManageMemberIndex.cshtml',
     'window.StatusMessage.ok("Member removed.");',
     'confirm("Item removed.");',
     'No_success_handler_uses_a_blocking_dialog'),

    ('alert() comes back in a success handler',
     P + r'\wwwroot\JSScriptLib\TableScripts\BulkUserProcessing.js',
     'window.StatusMessage.warn("Bulk create finished -- check the counts.", result);',
     'alert(result);',
     'No_success_handler_uses_a_blocking_dialog'),

    ('SubmitFollowing is reintroduced',
     P + r'\Views\User\DetailsModal.cshtml',
     'onchange="UserLockoutToggle(this, \'@Model.ApplicationUser.Id\')"',
     'onchange="SubmitFollowing(\'/ArchiveToggle\',\'@Model.ApplicationUser.Id\')"',
     'The_submit_following_helper_stays_gone'),

    ('the lock toggle points back at the route that does not exist',
     P + r'\Views\User\DetailsModal.cshtml',
     'url: "/User/LockoutToggle",',
     'url: "/ArchiveToggle",',
     'The_user_lock_toggle_posts_to_the_action_that_actually_exists'),

    ('a read-only lock indicator is given an onchange',
     P + r'\Views\User\IndexPartial.cshtml',
     '<input type="checkbox" checked="@item.IsLockedOut" disabled />',
     '<input type="checkbox" checked="@item.IsLockedOut" onchange="Toggle(this)" />',
     'The_read_only_lock_indicators_stay_read_only'),

    ('the abort filter is dropped from the global net',
     P + r'\wwwroot\JSScriptLib\Feedback\StatusMessage.js',
     'if (jqStatusText === "abort" || thrownError === "abort") {',
     'if (false) {',
     'The_status_message_helper_installs_a_global_failure_net'),

    ('the visibility gate comes back (suppresses failures in a background tab)',
     P + r'\wwwroot\JSScriptLib\Feedback\StatusMessage.js',
     'if (unloading) {',
     'if (document.visibilityState === "hidden") {',
     'The_status_message_helper_installs_a_global_failure_net'),

    ('the unload listener is dropped',
     P + r'\wwwroot\JSScriptLib\Feedback\StatusMessage.js',
     '$(window).on("beforeunload pagehide", function () {',
     '$(window).on("beforeunload", function () {',
     'The_status_message_helper_installs_a_global_failure_net'),

    ('the net stops distinguishing a request that never arrived',
     P + r'\wwwroot\JSScriptLib\Feedback\StatusMessage.js',
     'case 0:',
     'case 999:',
     'The_status_message_helper_installs_a_global_failure_net'),
]

AJAX_FEEDBACK = (T, MUTATIONS)


# ---- refusal-status: a mutating action must not answer a refusal with a success status ----
C = os.path.join(REPO, 'enduser', 'FebrisEndUserPortal', 'Controllers', 'Data', 'Local')
B = os.path.join(REPO, 'enduser', 'FebrisEndUserBLL', 'Logic', 'DataLogic')
T = 'tests/FebrisEndUserBLLTests'

MUTATIONS = [
    ('AddMember answers a swallowed exception with 200 again',
     C + r'\CohortController.cs',
     '''                response = "No new Item was added";
                return StatusCode(StatusCodes.Status500InternalServerError, response);''',
     '''                response = "No new Item was added";
                return Json(response);''',
     'AddMember_reports_a_failure_as_a_failure'),

    ('RemoveMember answers a missing link with 200 again',
     C + r'\CohortController.cs',
     '''                return threw
                    ? StatusCode(StatusCodes.Status500InternalServerError, response)
                    : NotFound(response);''',
     '''                return Json(response);''',
     'RemoveMember_separates_a_missing_link_from_a_server_error'),

    ('RemoveModule answers a missing link with 200 again',
     C + r'\HardwareController.cs',
     '''                response = "No Item was removed";
                return threw
                    ? StatusCode(StatusCodes.Status500InternalServerError, response)
                    : NotFound(response);''',
     '''                response = "No Item was removed";
                return Json(response);''',
     'RemoveModule_and_RemoveCohort_report_a_missing_link_as_a_404'),

    ('AddModule answers a swallowed exception with 200 again',
     C + r'\HardwareController.cs',
     '''                response = "No new Item was added";
                return StatusCode(StatusCodes.Status500InternalServerError, response);''',
     '''                response = "No new Item was added";
                return Json(response);''',
     'AddModule_and_AddCohort_report_a_failure_as_a_failure'),

    ('Cohort ArchiveToggle discards the RBAC refusal again',
     C + r'\CohortController.cs',
     '''            if (!output)
            {
                return Forbid();
            }''',
     '''            if (!output)
            {
                return Ok();
            }''',
     'Cohort_ArchiveToggle_forbids_rather_than_reporting_success'),

    ('Cohort ArchiveToggle answers an invalid id with Ok again',
     C + r'\CohortController.cs',
     '''                return BadRequest("not a valid choice");''',
     '''                return Ok();''',
     'Cohort_ArchiveToggle_rejects_an_invalid_id_instead_of_returning_ok'),

    ('MessageBoard ToggleArchive discards the answer again',
     C + r'\MessageBoardController.cs',
     '''            if (!output)
            {
                return NotFound();
            }''',
     '''            if (!output)
            {
                return Ok();
            }''',
     'MessageBoard_ToggleArchive_reports_its_outcome'),

    ('MessageBoardLogic goes back to never assigning output',
     B + r'\MessageBoardLogic.cs',
     '''                output = true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;''',
     '''            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;''',
     'MessageBoardLogic_ToggleArchive_reports_success_when_it_succeeds'),
]

REFUSAL_STATUS = (T, MUTATIONS)


# ---- getter-guard: a property getter must never return the property it belongs to ----
C = os.path.join(REPO, 'enduser', 'FebrisEndUserPortal', 'Controllers')
BLL = os.path.join(REPO, 'enduser', 'FebrisEndUserBLL')
G = os.path.join(REPO, 'tests', 'FebrisArchitectureTests', 'SelfRecursivePropertyGuardTests.cs')
T = 'tests/FebrisArchitectureTests/Febris.ArchitectureTests.csproj'

TREE = 'No_property_getter_returns_the_property_it_belongs_to'
SHAPES = 'The_guard_recognises_every_shape_the_defect_can_take'
STRIPPER = 'The_comment_stripper_does_not_eat_urls_or_string_literals'

FIXED_GETTER = '''                // Was `return StatusMessage;` -- the property returning ITSELF. Any read was
                // unbounded recursion and a StackOverflowException, which .NET cannot catch and
                // which takes the whole process down rather than failing one request. It survived
                // only because nothing ever read it. Now reads the store the setter writes.
                return TempData["StatusMessage"] as string;'''

MUTATIONS = [
    # ---- the defect comes back, in each root and each shape --------------------------------------
    ('block form back in the Portal (UserController)',
     C + r'\User\UserController.cs', FIXED_GETTER,
     '                return StatusMessage;', TREE),

    ('block form back in a SECOND root (FebrisEndUserBLL/ObjectLogic)',
     BLL + r'\Logic\XApiLogic\ObjectLogic.cs',
     '        public string StatusMessage { get; set; }',
     '''        private string Recursive
        {
            get
            {
                return Recursive;
            }
        }
        public string StatusMessage { get; set; }''',
     TREE),

    ('this.-qualified form back in the Portal',
     C + r'\WidgetController.cs', FIXED_GETTER,
     '                return this.StatusMessage;', TREE),

    ('expression-bodied ACCESSOR form',
     C + r'\SwitchboardController.cs',
     '            get\n            {\n' + FIXED_GETTER + '\n            }',
     '            get => StatusMessage;', TREE),

    ('expression-bodied PROPERTY form',
     C + r'\xAPI\Local\ActorController.cs',
     '        private string StatusMessage\n        {',
     '        private string Decoy => Decoy;\n        private string StatusMessage\n        {',
     TREE),

    ('the SCAFFOLDING TEMPLATE getter is reverted (root cause, and a .cshtml)',
     REPO + r'\enduser\FebrisEndUserPortal\Templates\ControllerGenerator\MvcControllerWithContext.cshtml',
     '                return TempData["StatusMessage"] as string;',
     '                return StatusMessage;', TREE),

    ('the `as string` cast form, one token from the shipped fix',
     C + r'\Data\Local\LocationController.cs', FIXED_GETTER,
     '                return StatusMessage as string;', TREE),

    # ---- the detector is blinded ------------------------------------------------------------------
    ('the getter filter goes back to same-line only (the guard\'s own original bug)',
     G, r'new Regex(@"\bget\b\s*(?:\{|=>)", RegexOptions.Compiled)',
     r'new Regex(@"\bget\b[ \t]*(?:\{|=>)", RegexOptions.Compiled)', SHAPES),

    ('this. stripping removed from the comparison',
     G,
     '''            if (e.StartsWith("this.", StringComparison.Ordinal))
            {
                e = e.Substring(5).Trim();
            }''',
     '            if (false) { }', SHAPES),

    ('the access modifier is mandatory again',
     G,
     r'(?:(?:public|private|protected|internal|static|virtual|override|sealed|abstract|new|required)[ \t]+)*(?<type>[\w<>?\[\],\.]+)[ \t]+(?:[\w<>\.]+\.)?(?<name>\w+)[ \t]*(?:\r?\n[ \t]*)?\{',
     r'(?:(?:public|private|protected|internal|static|virtual|override|sealed|abstract|new|required)[ \t]+)+(?<type>[\w<>?\[\],\.]+)[ \t]+(?:[\w<>\.]+\.)?(?<name>\w+)[ \t]*(?:\r?\n[ \t]*)?\{',
     SHAPES),

    ('the type-keyword blocklist is emptied so classes count as properties',
     G,
     '''            "namespace", "class", "struct", "interface", "record", "enum", "delegate", "event",
            "using", "else", "try", "finally", "do", "unsafe", "checked", "unchecked", "fixed",
            "lock", "switch", "return", "new", "where"''',
     '', SHAPES),

    ('the recursion check is disabled',
     G,
     '                if (ReturnsItself(body, name))',
     '                if (false)', SHAPES),

    ('the expression-bodied property check is disabled',
     G,
     '                if (Simplify(m.Groups["body"].Value) == m.Groups["name"].Value)',
     '                if (false)', SHAPES),

    ('the `as` cast is no longer unwrapped',
     G,
     '''                Match cast = Regex.Match(e, @"^(?<inner>.+?)\\s+as\\s+[\\w<>?\\[\\],\\.]+$", RegexOptions.Singleline);
                if (cast.Success)
                {
                    e = cast.Groups["inner"].Value.Trim();
                }''',
     '', SHAPES),

    # ---- the scanner goes blind -------------------------------------------------------------------
    ('the comment stripper goes naive and eats URLs',
     G, '''                if (c == '"' || c == '\\'')''', '''                if (false)''', STRIPPER),

    ('the @$"" verbatim spelling stops being recognised',
     G,
     '''                else if ((c == '@' || c == '$')
                         && i + 2 < source.Length
                         && source[i + 1] == (c == '@' ? '$' : '@')
                         && source[i + 2] == '"')
                {
                    verbatimPrefix = 2;
                }''',
     '', STRIPPER),

    ('the scan roots are gutted so it goes blind',
     G,
     '''            "enduser/FebrisEndUserPortal",
            "enduser/FebrisEndUserApi",''',
     '            "enduser/FebrisEndUserApi",', TREE),
]

GETTER_GUARD = (T, MUTATIONS)


# ---- jwt-carveout: the Development signing-secret carve-out is explicit and logged (ROADMAP 18) ----
SHARED = os.path.join(REPO, 'shared', 'FebrisSharedServices')
T = 'tests/FebrisSharedServicesTests'
TA = 'tests/FebrisArchitectureTests/Febris.ArchitectureTests.csproj'

MUTATIONS = [
    ('the silent early return comes back (return null before the checks)',
     os.path.join(SHARED, 'JwtSigningKeyProvider.cs'),
     '''            string reason = ProductionRejectionReason(secret);
            if (reason == null)
            {
                return null;
            }''',
     '''            if (isDevelopment) return null;
            string reason = ProductionRejectionReason(secret);
            if (reason == null)
            {
                return null;
            }''',
     'DevelopmentWaiver_NamesThePlaceholderWhenOneIsAccepted'),

    ('the same silent shape, caught at SOURCE level by the architecture guard',
     os.path.join(SHARED, 'JwtSigningKeyProvider.cs'),
     '''            string reason = ProductionRejectionReason(secret);
            if (reason == null)
            {
                return null;
            }''',
     '''            if (isDevelopment) return null;
            string reason = ProductionRejectionReason(secret);
            if (reason == null)
            {
                return null;
            }''',
     'The_provider_evaluates_production_validation_in_every_environment'),

    ('production validation stops seeing placeholders',
     os.path.join(SHARED, 'JwtSigningKeyProvider.cs'),
     '''            if (IsUnsubstitutedTemplate(secret))
            {
                return "JWT signing secret looks like an unsubstituted template " +''',
     '''            if (false)
            {
                return "JWT signing secret looks like an unsubstituted template " +''',
     'DevelopmentWaiver_NamesThePlaceholderWhenOneIsAccepted'),

    ('the API host stops logging the waiver',
     os.path.join(REPO, 'enduser', 'FebrisEndUserApi', 'Startup.cs'),
     '''            if (jwtKeyProvider.DevelopmentSecretWaiver != null)
            {''',
     '''            if (false)
            {''',
     'The_development_secret_waiver_is_logged_by_the_host'),

    # The 'Portal host stops logging the waiver' mutation retired with ROADMAP 16: the Portal's
    # signing-key registration existed solely for the NodeAdmin token mint and is deleted, so
    # there is no Portal waiver block to mutate. Its replacement guard is mutated below.
    ('the Portal starts signing JWTs again',
     os.path.join(REPO, 'enduser', 'FebrisEndUserPortal', 'Startup.cs'),
     '''            //this is for making and using identity claims (Cookies)''',
     '''            services.AddSingleton<Febris.SharedServices.IJwtSigningKeyProvider>(sp =>
                new Febris.SharedServices.JwtSigningKeyProvider(Configuration, false));
            //this is for making and using identity claims (Cookies)''',
     'The_portal_does_not_sign_jwts'),
]

# Two test projects are involved, so the set carries the project per mutation via a lookup
# rather than one T. Architecture-guard mutations run against TA, provider mutations against T.
_JWT_PROJECT = {
    'The_provider_evaluates_production_validation_in_every_environment': TA,
    'The_development_secret_waiver_is_logged_by_the_host': TA,
    'The_portal_does_not_sign_jwts': TA,
}
JWT_CARVEOUT = ('jwt-multi', [
    (label, path, old, new, test, _JWT_PROJECT.get(test, T))
    for (label, path, old, new, test) in MUTATIONS
])


# ---- config-surface: the node templates carry no residue, and VideoLimits is really read (ROADMAP 18) ----
API = os.path.join(REPO, 'enduser', 'FebrisEndUserApi')
PORTAL = os.path.join(REPO, 'enduser', 'FebrisEndUserPortal')
G = os.path.join(REPO, 'tests', 'FebrisArchitectureTests', 'ConfigurationSurfaceGuardTests.cs')
T = 'tests/FebrisArchitectureTests/Febris.ArchitectureTests.csproj'
TB = 'tests/FebrisEndUserBLLTests'

MUTATIONS = [
    ('a section with no reader comes back on the API (UsingRevProxy)',
     os.path.join(API, 'appsettings.json'),
     '  "AllowedHosts": "*",',
     '  "UsingRevProxy": true,\n  "AllowedHosts": "*",',
     'Every_template_section_has_a_reader_in_the_node_graph'),

    ('a section with no reader comes back on the Portal (KeyPersistence)',
     os.path.join(PORTAL, 'appsettings.json'),
     '  "AppKeys": {\n    "KeyRingPath": "keys"\n  },',
     '  "AppKeys": {\n    "KeyRingPath": "keys"\n  },\n  "KeyPersistence": "{Path}",',
     'Every_template_section_has_a_reader_in_the_node_graph'),

    ('a removed section that DOES have a literal reader comes back (LicenseKey, Portal)',
     os.path.join(PORTAL, 'appsettings.json'),
     '  "AppKeys": {\n    "KeyRingPath": "keys"\n  },',
     '  "AppKeys": {\n    "KeyRingPath": "keys"\n  },\n  "LicenseKey": "{Path}",',
     'Known_residue_does_not_come_back'),

    ('the key scanner goes blind',
     G,
     'if (_lastString != null && depth == 1 && _lastDepth == 1',
     'if (_lastString != null && depth == 99 && _lastDepth == 99',
     'Every_template_section_has_a_reader_in_the_node_graph'),

    ('the IVideoFileHandler registration is removed from the API host',
     os.path.join(API, 'Startup.cs'),
     '            services.AddSingleton<Febris.SharedServices.IVideoFileHandler, Febris.SharedServices.VideoFileHandler>();\n',
     '',
     'The_api_registers_the_video_file_handler_so_the_greedy_constructor_is_resolvable'),

    ('a template section loses its heading in the configuration reference',
     os.path.join(REPO, 'docs', 'CONFIGURATION_REFERENCE.md'),
     '### `Identity`',
     '### Identity (heading broken)',
     'Every_template_section_is_documented_in_the_configuration_reference'),

    ('the greedy constructor stops reading VideoLimits:MaxPartBytes',
     os.path.join(REPO, 'enduser', 'FebrisEndUserBLL', 'Logic', 'LauncherLogic', 'VideoUploadLogic.cs'),
     '            MaxPartBytes = config?.GetValue<long?>("VideoLimits:MaxPartBytes") ?? DefaultMaxPartBytes;',
     '            MaxPartBytes = DefaultMaxPartBytes;',
     'The_resolved_logic_honours_a_configured_limit_above_the_compiled_default'),
]

_CFG_PROJECT = {
    'The_resolved_logic_honours_a_configured_limit_above_the_compiled_default': TB,
}
CONFIG_SURFACE = ('cfg-multi', [
    (label, path, old, new, test, _CFG_PROJECT.get(test, T))
    for (label, path, old, new, test) in MUTATIONS
])


# ---- reachability: ROADMAP 17, every Portal action and view is reachable by a human ----
PORTAL = os.path.join(REPO, 'enduser', 'FebrisEndUserPortal')
G = os.path.join(REPO, 'tests', 'FebrisArchitectureTests', 'PortalReachabilityGuardTests.cs')
T = 'tests/FebrisArchitectureTests/Febris.ArchitectureTests.csproj'

REACHABILITY = (T, [
    ('the testuser dispatcher case disappears again',
     os.path.join(PORTAL, 'Controllers', 'WidgetController.cs'),
     'case "testuser":',
     'case "testuserprobe":',
     'Every_modal_slot_has_a_live_dispatcher_case'),

    ('the nav grows a link to an action that does not exist (the SortedIndex bug reborn)',
     os.path.join(PORTAL, 'Views', 'Shared', '_Layout.cshtml'),
     '<a asp-area="" asp-controller="LocalAnalytics" asp-action="Index">Raw Index</a>',
     '<a asp-area="" asp-controller="LocalAnalytics" asp-action="SortedIndex">Raw Index</a>',
     'Every_tag_helper_link_names_an_action_that_exists'),

    ('a live modal slot names an action that was never written (the ModuleIndex bug reborn)',
     os.path.join(PORTAL, 'Views', 'Curriculum', 'DetailsModal.cshtml'),
     '<input type="hidden" id="Element0" value="ModuleIndex" />',
     '<input type="hidden" id="Element0" value="ModuleIndexProbe" />',
     'Every_modal_slot_names_a_load_action_that_exists'),

    ('the slot-backing action disappears from the controller side',
     os.path.join(PORTAL, 'Controllers', 'Data', 'Remote', 'CurriculumController.cs'),
     'public async Task<IActionResult> LoadModuleIndex(Curriculum input)',
     'public async Task<IActionResult> LoadModuleIndexProbe(Curriculum input)',
     'Every_modal_slot_names_a_load_action_that_exists'),

    ('a render reaches into a deleted GenericButtons folder (the leaf-name blind spot reborn)',
     os.path.join(PORTAL, 'Views', 'Cohort', 'IndexPartial.cshtml'),
     '"Buttons/GenericButtons/Cohort/_IndexButtonGroupPartial"',
     '"Buttons/GenericButtons/Verb/_IndexButtonGroupPartial"',
     'Every_partial_a_view_renders_actually_exists'),

    ('an action nothing references appears',
     os.path.join(PORTAL, 'Controllers', 'SwitchboardController.cs'),
     '        #region used Charts',
     '        public IActionResult LoadOrphanProbe() { return PartialView(); }\n        #region used Charts',
     'Every_portal_action_is_reachable_or_pending_with_a_reason'),

    ('a view loses its only render',
     os.path.join(PORTAL, 'Views', 'Shared', '_Layout.cshtml'),
     '<partial name="_LoginPartial" />',
     '',
     'Every_portal_view_is_rendered_or_pending_with_a_reason'),

    ('a Pending entry goes stale',
     G,
     '["action:Location/Delete"] = "OWNER 2026-08-24: KEEP, multi-location filtering. Repair pending, scope not yet set",',
     '["action:Location/DeleteConfirmed"] = "OWNER 2026-08-24: KEEP, multi-location filtering. Repair pending, scope not yet set",',
     'Pending_only_shrinks'),
])


# ---- roadmap16: the admin writes live on the Portal, and the NodeAdmin token stays dead ----
PORTAL = os.path.join(REPO, 'enduser', 'FebrisEndUserPortal')
API = os.path.join(REPO, 'enduser', 'FebrisEndUserApi')
BLL = os.path.join(REPO, 'enduser', 'FebrisEndUserBLL')
T = 'tests/FebrisArchitectureTests/Febris.ArchitectureTests.csproj'

ROADMAP16 = (T, [
    ('the package Upload POST loses its OrgAdmins gate',
     os.path.join(PORTAL, 'Controllers', 'Data', 'Remote', 'LocalSoftwarePackageController.cs'),
     '''        [RequestSizeLimit(1_073_741_824)]
        [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
        public async Task<IActionResult> Upload(SoftwarePackageUploadViewModel input)''',
     '''        [RequestSizeLimit(1_073_741_824)]
        public async Task<IActionResult> Upload(SoftwarePackageUploadViewModel input)''',
     'The_moved_write_actions_carry_the_full_gate_stack'),

    ('the FeedSync POST loses its antiforgery attribute',
     os.path.join(PORTAL, 'Controllers', 'Data', 'Remote', 'LocalSoftwarePackageController.cs'),
     '''        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
        public async Task<IActionResult> FeedSync(PackageFeedSyncRequestViewModel input)''',
     '''        [HttpPost]
        [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
        public async Task<IActionResult> FeedSync(PackageFeedSyncRequestViewModel input)''',
     'Every_portal_post_action_validates_the_antiforgery_token'),

    ('the API grows a write action back',
     os.path.join(API, 'Controllers', 'ModuleController.cs'),
     '        private Dictionary<string, string> GetMimeTypes()',
     '''        [HttpPost("Upload")]
        public IActionResult Upload() { return Ok(); }

        private Dictionary<string, string> GetMimeTypes()''',
     'The_api_write_surface_stays_deleted'),

    ('the composed-scheme flag returns to the filter',
     os.path.join(BLL, 'Attributes', 'AttributeClasses.cs'),
     '''    public class AuthorizeAttribute : Attribute, IAuthorizationFilter
    {''',
     '''    public class AuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public bool AllowNodeAdmin { get; set; } = false;''',
     'The_node_admin_credential_stays_deleted'),

    ('the middleware attaches a second scheme again',
     os.path.join(BLL, 'Logic', 'AuthorizationLogic', 'HardwareKeyAuthorization.cs'),
     '                    context.Items["Hardware"] = attached;',
     '''                    context.Items["Hardware"] = attached;
                    context.Items["NodeAdmin"] = attached;''',
     'The_node_admin_credential_stays_deleted'),

    ('the module form loses its upsert UUID field',
     os.path.join(PORTAL, 'Views', 'Module', 'Create.cshtml'),
     '<input asp-for="UUID" class="form-control" placeholder="Leave empty for a new module" />',
     '',
     'The_upsert_uuid_fields_stay_on_both_forms'),
])


# ---- roadmap22: the node derives the record decision, and the client does not get a vote ----
BLL = os.path.join(REPO, 'enduser', 'FebrisEndUserBLL')
TB = 'tests/FebrisEndUserBLLTests'

ROADMAP22 = (TB, [
    ('the derive reverts to trusting the client flag',
     os.path.join(BLL, 'Logic', 'LauncherLogic', 'LauncherLogic.cs'),
     '''                // that used to gate it was never populated by either of them.
                if (await ShouldRecordSession(actor))''',
     '''                // that used to gate it was never populated by either of them.
                if (input.RecordSession)''',
     'ClientRefusingToRecord_CannotSuppress_ThePolicy'),

    ('the device arm of the union stops counting',
     os.path.join(BLL, 'Logic', 'LauncherLogic', 'LauncherLogic.cs'),
     '                if (RecordsAnySession(deviceLinks?.Select(i => i.Cohort)))',
     '                if (false)',
     'DeviceLinkedCohortWithPolicy_Records'),

    ('the learner arm of the union stops counting',
     os.path.join(BLL, 'Logic', 'LauncherLogic', 'LauncherLogic.cs'),
     '                    if (RecordsAnySession(memberships?.Select(i => i.Cohort)))',
     '                    if (false)',
     'LearnersCohortWithPolicy_Records_EvenWhenTheDeviceHasNoCohort'),

    ('a retired cohort gets to keep recording',
     os.path.join(BLL, 'Logic', 'LauncherLogic', 'LauncherLogic.cs'),
     'return cohorts != null && cohorts.Any(c => c != null && c.RecordSessions && !c.Archive);',
     'return cohorts != null && cohorts.Any(c => c != null && c.RecordSessions);',
     'ArchivedCohortWithPolicy_DoesNotRecord'),

    ('the policy toggle becomes a one-way door',
     os.path.join(BLL, 'Logic', 'DataLogic', 'CohortLogic.cs'),
     '                preoutput.RecordSessions = !preoutput.RecordSessions;',
     '                preoutput.RecordSessions = true;',
     'RecordSessionsToggle_IsTheOnlyThingThatChangesTheRecordingPolicy_AndItRoundTrips'),

    ('the cohort edit starts clobbering the recording policy',
     os.path.join(BLL, 'Logic', 'DataLogic', 'CohortLogic.cs'),
     '''                stored.InstructorId = input.InstructorId;
                stored.LastUpdateTimeStamp = DateTime.Now;''',
     '''                stored.InstructorId = input.InstructorId;
                stored.RecordSessions = input.RecordSessions;
                stored.LastUpdateTimeStamp = DateTime.Now;''',
     'EditingACohortsName_DoesNotSilentlyDisableRecording'),
])


# ---- roadmap13: a deleted sole admin must not brick the node's claim path ----
# Both gates read the same question and must agree. One mutation per gate, because reverting
# either one alone reintroduces the brick, and a guard that only watched one would have let the
# other rot.
PORTAL13 = os.path.join(REPO, 'enduser', 'FebrisEndUserPortal')
ROADMAP13 = (TB, [
    ('the boot claim gate counts soft-deleted admins again',
     os.path.join(PORTAL13, 'Data', 'SeedData.cs'),
     '                if (admins != null && admins.Any(admin => !admin.IsDeleted))',
     '                if (admins != null && admins.Count > 0)',
     'BothClaimGates_IgnoreSoftDeletedAdmins_SoADeletedSoleAdminCannotBrickTheNode'),

    ('the /setup gate counts soft-deleted admins again, disagreeing with the boot gate',
     os.path.join(PORTAL13, 'Areas', 'Identity', 'Pages', 'Account', 'Setup.cshtml.cs'),
     '            return admins != null && admins.Any(admin => !admin.IsDeleted);',
     '            return admins != null && admins.Any();',
     'BothClaimGates_IgnoreSoftDeletedAdmins_SoADeletedSoleAdminCannotBrickTheNode'),
])


# ---- roadmap11: the curriculum edit clobber, third appearance of the C-07 shape ----
# The mutation restores the line exactly as it shipped. Cohort.Archive and Cohort.LockMembers were
# the first two, and both were found by hand, so the point of pinning this one is that the NEXT
# copy of this shape fails a test instead of waiting for somebody to notice a resurrected row.
DAL = os.path.join(REPO, 'enduser', 'FebrisEndUserDAL')
ROADMAP11 = (TB, [
    ('the curriculum edit writes Obsolete back from the form again',
     os.path.join(DAL, 'Queries', 'DataQueries', 'CurriculumQueries.cs'),
     '''                existing.Name = input.Name;
                existing.Description = input.Description;
                existing.Version = input.Version;
                existing.MicroCredentialAvailable = input.MicroCredentialAvailable;
                existing.CurriculumClassificationUUID = input.CurriculumClassificationUUID;''',
     '''                existing.Name = input.Name;
                existing.Description = input.Description;
                existing.Version = input.Version;
                existing.MicroCredentialAvailable = input.MicroCredentialAvailable;
                existing.Obsolete = input.Obsolete;
                existing.CurriculumClassificationUUID = input.CurriculumClassificationUUID;''',
     'EditingACurriculum_DoesNotSilentlyUnObsoleteIt'),

    ('obsoleting becomes a one-way door, the trap the flag was added to avoid',
     os.path.join(DAL, 'Queries', 'DataQueries', 'CurriculumQueries.cs'),
     '                existing.Obsolete = obsolete;',
     '                existing.Obsolete = true;',
     'SetObsolete_IsStillTheWriter_InBothDirections_AfterAnEdit'),

    ('the classification link goes back to writing only the unconstrained UUID column',
     os.path.join(DAL, 'Queries', 'DataQueries', 'CurriculumQueries.cs'),
     '''                existing.CurriculumClassificationUUID = input.CurriculumClassificationUUID;
                await AssignClassification(existing, input.CurriculumClassificationUUID);''',
     '                existing.CurriculumClassificationUUID = input.CurriculumClassificationUUID;',
     'Upsert_ChangesTheClassificationLink_WhenTheOperatorRe_Classifies'),

    ('picking [None] stops clearing the link, so the picker is a one-way door',
     os.path.join(DAL, 'Queries', 'DataQueries', 'CurriculumQueries.cs'),
     '''                target.CurriculumClassification = null;
                return;''',
     '                return;',
     'Upsert_ClearsTheClassificationLink_WhenTheOperatorPicksNone'),

    ('a dangling classification UUID is assigned anyway instead of being ignored',
     os.path.join(DAL, 'Queries', 'DataQueries', 'CurriculumQueries.cs'),
     '''            if (classification != null)
            {
                target.CurriculumClassification = classification;
            }''',
     '            target.CurriculumClassification = classification;',
     'Upsert_LeavesTheLinkAlone_WhenTheUuidNamesNoClassification'),
])


# ---- roadmap15: package ingest validates the BYTES, not the filename ----
# Each mutation restores a real prior state of this code, not a synthetic break. The extension-only
# check is what actually shipped, and the missing rewind is the defect the naive fix would have
# introduced (a package that ingests "successfully" and downloads as garbage).
ROADMAP15 = (TB, [
    ('the archive check is dropped, so the extension alone decides again',
     os.path.join(BLL, 'Logic', 'DataLogic', 'PackageIngestLogic.cs'),
     'if (content == null || metadata == null || !IsZip(sourceFileName) || !IsReadableArchive(content))\n                {\n                    return null;\n                }\n\n                Guid uuid = metadata.UUID ?? Guid.NewGuid();\n                string storageKey = StorageKeys.Module(uuid.ToString() + ".zip");',
     'if (content == null || metadata == null || !IsZip(sourceFileName))\n                {\n                    return null;\n                }\n\n                Guid uuid = metadata.UUID ?? Guid.NewGuid();\n                string storageKey = StorageKeys.Module(uuid.ToString() + ".zip");',
     'Ingest_RejectsAFileMerelyRENAMEDToZip_RatherThanAcceptingItOnTheExtension'),

    ('the software-package path loses the archive check while the module path keeps it',
     os.path.join(BLL, 'Logic', 'DataLogic', 'PackageIngestLogic.cs'),
     'if (content == null || metadata == null || !IsZip(sourceFileName) || !IsReadableArchive(content))\n                {\n                    return null;\n                }\n\n                Guid uuid = metadata.UUID ?? Guid.NewGuid();\n                string storageKey = StorageKeys.SoftwarePackage(uuid.ToString() + ".zip");',
     'if (content == null || metadata == null || !IsZip(sourceFileName))\n                {\n                    return null;\n                }\n\n                Guid uuid = metadata.UUID ?? Guid.NewGuid();\n                string storageKey = StorageKeys.SoftwarePackage(uuid.ToString() + ".zip");',
     'Ingest_RejectsAFileMerelyRENAMEDToZip_RatherThanAcceptingItOnTheExtension'),

    ('an empty archive is accepted, so a zip with nothing in it becomes a module',
     os.path.join(BLL, 'Logic', 'DataLogic', 'PackageIngestLogic.cs'),
     '                    return archive.Entries.Count > 0;',
     '                    return true;',
     'Ingest_RejectsAnEmptyArchive'),

    ('the stream is not rewound, so the stored bytes and the checksum describe nothing',
     os.path.join(BLL, 'Logic', 'DataLogic', 'PackageIngestLogic.cs'),
     '''            finally
            {
                content.Position = origin;
            }''',
     '''            finally
            {
            }''',
     'Ingest_StoresTheWholePayload_AfterReadingItToValidateTheArchive'),
])


# ---- transport: both node hosts apply the same operator-configured transport security (ROADMAP 5) ----
API = os.path.join(REPO, 'enduser', 'FebrisEndUserApi')
TA = 'tests/FebrisArchitectureTests/Febris.ArchitectureTests.csproj'
TB = 'tests/FebrisEndUserBLLTests'

MUTATIONS = [
    ('the API hardcodes the HSTS max-age instead of reading Transport:Hsts',
     os.path.join(API, 'Startup.cs'),
     '''            services.AddHsts(o =>
            {
                o.MaxAge = TimeSpan.FromDays(transportOptions.Hsts.MaxAgeDays);''',
     '''            services.AddHsts(o =>
            {
                o.MaxAge = TimeSpan.FromDays(30);''',
     'WithNoTransportSection_TheApiUsesTheSafeSharedDefaults_NotTheFrameworkDefaults'),

    ('the operator includeSubDomains choice stops reaching the HSTS policy',
     os.path.join(API, 'Startup.cs'),
     '                o.IncludeSubDomains = transportOptions.Hsts.IncludeSubdomains;',
     '                o.IncludeSubDomains = true;',
     'TheOperatorsTransportValues_ReachTheHstsPolicy'),

    ('the API emits HSTS unconditionally again',
     os.path.join(API, 'Startup.cs'),
     '''                if (transport.Hsts.Enabled)
                {
                    app.UseHsts();
                }''',
     '''                app.UseHsts();''',
     'Neither_host_emits_hsts_unconditionally'),

    ('HTTPS redirection goes back to being a commented-out line',
     os.path.join(API, 'Startup.cs'),
     '''            if (transport.HttpsRedirection)
            {
                app.UseHttpsRedirection();
            }''',
     '''            //app.UseHttpsRedirection();''',
     'Both_hosts_gate_https_redirection_on_the_operator_setting'),

    ('the X-Frame-Options fail-safe is simplified away',
     os.path.join(API, 'Startup.cs'),
     '''                    ctx.Response.Headers["X-Frame-Options"] =
                        string.Equals(transport.SecurityHeaders.XFrameOptions, "Deny", StringComparison.OrdinalIgnoreCase)
                            ? "DENY"
                            : "SAMEORIGIN";''',
     '''                    ctx.Response.Headers["X-Frame-Options"] = "DENY";''',
     'Both_hosts_apply_the_x_frame_options_policy_and_fail_safe'),

    ('the API template goes back to shipping only Cors',
     os.path.join(API, 'appsettings.json'),
     '''    "Hsts": {
      "Enabled": true,''',
     '''    "HstsRemoved": {
      "Enabled": true,''',
     'The_api_template_ships_the_whole_transport_section'),
]

_TRANSPORT_PROJECT = {
    'WithNoTransportSection_TheApiUsesTheSafeSharedDefaults_NotTheFrameworkDefaults': TB,
    'TheOperatorsTransportValues_ReachTheHstsPolicy': TB,
}
TRANSPORT = ('transport-multi', [
    (label, path, old, new, test, _TRANSPORT_PROJECT.get(test, TA))
    for (label, path, old, new, test) in MUTATIONS
])


SETS = {
    'ajax-feedback': AJAX_FEEDBACK,
    'refusal-status': REFUSAL_STATUS,
    'getter-guard': GETTER_GUARD,
    'jwt-carveout': JWT_CARVEOUT,
    'config-surface': CONFIG_SURFACE,
    'reachability': REACHABILITY,
    'roadmap16': ROADMAP16,
    'roadmap11': ROADMAP11,
    'roadmap13': ROADMAP13,
    'roadmap15': ROADMAP15,
    'roadmap22': ROADMAP22,
    'transport': TRANSPORT,
}


def tree_is_clean():
    """Clean means no TRACKED modification, staged or unstaged.

    Untracked files are ignored deliberately. This harness only rewrites files it has already read
    and never creates one, so an untracked path cannot be its debris. Blocking on untracked files
    also made the first version refuse to run in the very commit that added it.
    """
    out = subprocess.run(['git', 'status', '--porcelain'],
                         cwd=REPO, capture_output=True, text=True).stdout
    tracked = [ln for ln in out.splitlines() if ln[:2] != '??']
    return not tracked, '\n'.join(tracked)


def run_test(project, test_name):
    r = subprocess.run(
        ['dotnet', 'test', project, '--filter', 'FullyQualifiedName~' + test_name,
         '--nologo', '-v', 'q'],
        cwd=REPO, capture_output=True, text=True)
    # SAFEGUARD 6: a filter that matches ZERO tests exits 0, which reads as green. It
    # happened: a jwt-carveout mutation named an architecture test while the set's default
    # project was the BLL tests, the filter matched nothing in that project, the baseline
    # passed vacuously and the mutation was reported as a MISS against a test that never
    # ran. A test that did not run is not a test that passed.
    out = (r.stdout or '') + (r.stderr or '')
    if 'No test matches' in out or ('Passed!' not in out and 'Failed!' not in out):
        return 255
    return r.returncode


def baseline(name, project, mutations):
    """Run every test the set names ONCE on the unmutated tree. Each must be GREEN.

    A guard that is red without any mutation "catches" every mutation and proves nothing. This
    happened: a guard shipped red on the clean tree, its mutation run reported 5/5 caught, and
    the commit landed. The harness cannot know a test is meaningful, but it can know when it is
    meaningless, and that is this check.
    """
    seen = {}
    for mutation in mutations:
        test_name = mutation[4]
        target = mutation[5] if len(mutation) > 5 else project
        if test_name in seen:
            continue
        seen[test_name] = run_test(target, test_name)
    red = [t for t, code in seen.items() if code != 0]
    for t in red:
        print('  RED-ON-CLEAN-TREE  %s' % t)
    return red


def apply_set(name):
    project, mutations = SETS[name]
    print('== %s (%d mutations) ==' % (name, len(mutations)))

    red = baseline(name, project, mutations)
    if red:
        print('  ABORT  %d test(s) this set names are already failing without any mutation,' % len(red))
        print('         so every "caught" below would be vacuous. Fix the guard first.')
        return ['%s: baseline red: %s' % (name, t) for t in red]

    failures = []
    for mutation in mutations:
        # A set normally shares one test project. A mutation may carry its own as a sixth
        # element when a set spans two projects (jwt-carveout: provider tests + architecture guards).
        label, path, old, new, test_name = mutation[:5]
        target = mutation[5] if len(mutation) > 5 else project
        raw = io.open(path, 'rb').read()
        bom = raw.startswith(b'\xef\xbb\xbf')
        text = raw.decode('utf-8-sig')
        crlf = '\r\n' in text
        original = text.replace('\r\n', '\n')

        def write(body, _p=path, _bom=bom, _crlf=crlf):
            out = body.replace('\n', '\r\n') if _crlf else body
            data = out.encode('utf-8')
            io.open(_p, 'wb').write((b'\xef\xbb\xbf' + data) if _bom else data)

        if old not in original:
            failures.append('%s: ANCHOR MISSING in %s' % (label, os.path.relpath(path, REPO)))
            print('  STALE  %-58s anchor no longer matches' % label[:58])
            continue

        write(original.replace(old, new, 1))
        try:
            code = run_test(target, test_name)
        finally:
            write(original)

        clean, dirt = tree_is_clean()
        if not clean:
            print('  ABORT  the tree did not come back clean after %r:' % label)
            print(dirt)
            print('')
            print('  Two causes, and the paths listed above tell you which:')
            print('   - a path this mutation touched  -> the restore path is broken, fix it before rerunning')
            print('   - any OTHER path               -> something edited the tree while this was running')
            print('')
            print('  The second is not hypothetical: a run was aborted here because a doc was edited in')
            print('  another window mid-run. This check cannot tell that from its own debris, which is')
            print('  exactly why the harness demands a clean tree to start. Do not edit tracked files')
            print('  while it runs.')
            raise SystemExit('aborted with the tree dirty')

        if code == 0:
            failures.append('%s: %s stayed green' % (label, test_name))
            print('  MISS   %-58s %s stayed green' % (label[:58], test_name))
        else:
            print('  CAUGHT %-58s %s' % (label[:58], test_name))

    return failures


def check_anchors(wanted):
    """Verify every anchor still matches, WITHOUT running a single test.

    A full run is tens of minutes, and until it finishes you do not know whether the anchors are
    even valid. This takes about a second and answers the only question that makes the rest
    worthwhile. It exists because a refactor of this file corrupted seven anchors, and the twenty
    minutes spent discovering that could have been one.
    """
    stale = []
    total = 0
    for name in wanted:
        for mutation in SETS[name][1]:
            label, path, old = mutation[0], mutation[1], mutation[2]
            total += 1
            text = io.open(path, 'rb').read().decode('utf-8-sig').replace('\r\n', '\n')
            if old not in text:
                stale.append('%s / %s -> %s' % (name, label, os.path.relpath(path, REPO)))

    for s in stale:
        print('STALE  ' + s)
    print('%d anchors checked, %d stale' % (total, len(stale)))
    return 1 if stale else 0


def main(argv):
    args = argv[1:]
    check_only = '--check' in args
    args = [a for a in args if a != '--check']

    wanted = args or list(SETS)
    unknown = [w for w in wanted if w not in SETS]
    if unknown:
        raise SystemExit('unknown set(s): %s (known: %s)' % (', '.join(unknown), ', '.join(SETS)))

    if check_only:
        # Read-only, so it does not need a clean tree.
        return check_anchors(wanted)

    clean, dirt = tree_is_clean()
    if not clean:
        print('REFUSING to run: tracked files have uncommitted changes.')
        print('This harness edits tracked files and restores them from memory, so a crash mid-run')
        print('would be indistinguishable from your own edits. Commit or stash first.\n')
        print(dirt)
        return 2

    failures = []
    total = 0
    for name in wanted:
        failures.extend(apply_set(name))
        total += len(SETS[name][1])
        print()

    if failures:
        print('%d of %d MUTATION(S) NOT CAUGHT:' % (len(failures), total))
        for f in failures:
            print('  ' + f)
        return 1

    print('all %d mutations caught across %d set(s)' % (total, len(wanted)))
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
