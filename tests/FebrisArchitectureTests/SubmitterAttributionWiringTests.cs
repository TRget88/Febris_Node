// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Febris.ArchitectureTests
{
    /// <summary>
    /// The wiring that makes <c>LocalStatement.SubmittedByHardwareUUID</c> READABLE from the product.
    ///
    /// <para>
    /// WHY THIS EXISTS. The column shipped with two writers and no reader anywhere in the
    /// application. A repo-wide grep returned the two assignments, the property declaration and the
    /// migration, and nothing else. It had been justified on the grounds that a forged learning
    /// record would become "investigable instead of indistinguishable", and as shipped that
    /// investigation required direct database access. Unit tests of the query cannot notice that,
    /// which is exactly how a write-side-only field passes review: every test of it is green because
    /// every test constructs its own reader.
    /// </para>
    ///
    /// <para>
    /// So these guards check the CHAIN, not the pieces: the DAL exposes a read, the BLL exposes it,
    /// the controller calls it, and the view renders it. Break any link and the column is decorative
    /// again. Source-parsed rather than referencing the Portal, matching
    /// <c>StatementVoidingWiringTests</c>.
    /// </para>
    ///
    /// <para>
    /// EACH GUARD ASSERTS ITS INPUT WAS NON-EMPTY FIRST. A source-text guard that silently reads
    /// nothing passes forever, and this project has already been bitten by a guard that matched a
    /// hardcoded name and let a real mutation through.
    /// </para>
    /// </summary>
    public class SubmitterAttributionWiringTests
    {
        private const string ColumnName = "SubmittedByHardwareUUID";

        private static string Read(string repoRelative)
        {
            string path = Path.Combine(
                ProjectGraph.FindRepoRoot(),
                repoRelative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), "expected file not found: " + path);

            string text = File.ReadAllText(path);
            Assert.False(string.IsNullOrWhiteSpace(text), "file was empty, so this guard would pass vacuously: " + path);
            return text;
        }

        /// <summary>Razor lines with the block-comment markers stripped, so commented-out wiring does not count.</summary>
        private static string WithoutRazorComments(string text)
        {
            // Razor block comments are @* ... *@ and may span lines.
            int guard = 0;
            while (guard++ < 500)
            {
                int open = text.IndexOf("@*", StringComparison.Ordinal);
                if (open < 0) break;
                int close = text.IndexOf("*@", open + 2, StringComparison.Ordinal);
                if (close < 0) break;
                text = text.Remove(open, close - open + 2);
            }
            return text;
        }

        [Fact]
        public void The_column_has_a_DAL_read_not_only_writes()
        {
            string dal = Read("enduser/FebrisEndUserDAL/Queries/XAPIQueries/StatementQueries.cs");

            Assert.Contains("GetBySubmittingHardware", dal);
            Assert.Contains("CountBySubmittingHardware", dal);
            Assert.Contains(ColumnName, dal);
        }

        [Fact]
        public void The_BLL_exposes_the_read_to_callers()
        {
            string bll = Read("enduser/FebrisEndUserBLL/Logic/XApiLogic/StatementLogic.cs");

            Assert.Contains("GetSubmissionsByDevice", bll);
            Assert.Contains("GetBySubmittingHardware", bll);
        }

        [Fact]
        public void The_device_screen_actually_asks_for_the_submissions()
        {
            // The controller is the link that unit tests of the query would never exercise.
            string controller = Read("enduser/FebrisEndUserPortal/Controllers/Data/Local/HardwareController.cs");

            Assert.Contains("GetSubmissionsByDevice", controller);
            Assert.Contains("IStatementLogic", controller);
        }

        [Fact]
        public void The_device_screen_renders_them()
        {
            // The last link. Everything above can be present and correct while the view shows none
            // of it, which is precisely the state the column shipped in.
            string view = WithoutRazorComments(
                Read("enduser/FebrisEndUserPortal/Views/Hardware/DetailsModal.cshtml"));

            Assert.Contains("LocalHardwareDetailsViewModel", view);

            // OUTPUT expressions, not merely references. The first draft of this guard asserted
            // Contains("Model.Submissions.TotalCount"), which a mutation replacing the rendered
            // total with a literal walked straight past: the string still occurred in the
            // `else if (Model.Submissions.TotalCount == 0)` branch a few lines above. Asserting on
            // the leading @ is what distinguishes rendering the value from testing it.
            Assert.Contains("@Model.Submissions.TotalCount", view);

            // And the rows themselves must be enumerated. A screen showing only a count answers
            // "how many" but not "which", and the whole point is naming the statements.
            Assert.Matches(@"foreach\s*\(\s*var\s+\w+\s+in\s+Model\.Submissions\.Statements\s*\)", view);
        }

        [Fact]
        public void The_screen_states_the_list_may_be_partial()
        {
            // An investigation screen that shows a capped list as if it were the whole history is
            // worse than no screen, because it produces confident wrong conclusions.
            string view = WithoutRazorComments(
                Read("enduser/FebrisEndUserPortal/Views/Hardware/DetailsModal.cshtml"));

            Assert.Contains("IsTruncated", view);
        }

        [Fact]
        public void Regenerating_a_credential_revokes_the_session_it_replaced()
        {
            // Blocker 3, guarded in the same place because it is the same screen and the same
            // incident. The durable half must be written, and the immediate half published.
            string logic = Read("enduser/FebrisEndUserBLL/Logic/DataLogic/HardwareLogic.cs");

            int regenAt = logic.IndexOf("public async Task<string> RegenerateCredential", StringComparison.Ordinal);
            Assert.True(regenAt > 0, "RegenerateCredential not found, so this guard would pass vacuously");

            // Bound the search to the method body rather than the whole file, so the lock path's own
            // RevokeAsync call at the bottom of Update cannot satisfy this on its own.
            int nextMethod = logic.IndexOf("public async Task<LocalHardware> Update", regenAt, StringComparison.Ordinal);
            Assert.True(nextMethod > regenAt, "could not bound RegenerateCredential's body");

            string body = logic.Substring(regenAt, nextMethod - regenAt);

            Assert.Contains("CredentialRegeneratedAt", body);
            Assert.Contains("RevokeAsync", body);
        }

        [Fact]
        public void The_recording_attribution_chain_reaches_the_screen()
        {
            // Ownership ruling of 2026-08-18. Recording.HardwareUUID was never write-only --
            // MayAcceptPart reads it to gate uploads -- but nothing surfaced it to a person, so
            // "what else did this device mint" had no answer outside SQL. Same chain check as the
            // statement one above: DAL, BLL, controller, view.
            string dal = Read("enduser/FebrisEndUserDAL/Queries/DataQueries/Local/RecordingQueries.cs");
            Assert.Contains("GetByHardware", dal);
            Assert.Contains("CountByHardware", dal);

            string bll = Read("enduser/FebrisEndUserBLL/Logic/DataLogic/RecordingLogic.cs");
            Assert.Contains("GetRecordingsByDevice", bll);

            string controller = Read("enduser/FebrisEndUserPortal/Controllers/Data/Local/HardwareController.cs");
            Assert.Contains("GetRecordingsByDevice", controller);
            Assert.Contains("IRecordingLogic", controller);

            string view = WithoutRazorComments(
                Read("enduser/FebrisEndUserPortal/Views/Hardware/DetailsModal.cshtml"));
            Assert.Contains("@Model.Recordings.TotalCount", view);
            Assert.Matches(@"foreach\s*\(\s*var\s+\w+\s+in\s+Model\.Recordings\.Recordings\s*\)", view);
        }

        [Fact]
        public void The_ownership_ruling_stays_attribution_and_not_enforcement()
        {
            // The ruling deliberately does NOT bind the actor to the device, because the only
            // membership state to bind through is HardwareLinkedCohort and the owner ruling of
            // 2026-08-10 rejected binding writes through mutable membership. If someone later adds
            // a refusal here it must be a deliberate decision with its own evidence, not a quiet
            // tightening of a read. This pins the launcher's registration as unconditional.
            string launcher = Read("enduser/FebrisEndUserBLL/Logic/LauncherLogic/LauncherLogic.cs");

            int handlerAt = launcher.IndexOf("internal async Task<Attachment> VideoAttachmentHandler", StringComparison.Ordinal);
            Assert.True(handlerAt > 0, "VideoAttachmentHandler not found, so this guard would pass vacuously");

            string body = launcher.Substring(handlerAt, Math.Min(2000, launcher.Length - handlerAt));
            Assert.Contains("_recordingContext.Register(", body);
        }

        [Fact]
        public void The_refresh_path_honours_the_regeneration_stamp()
        {
            string auth = Read("enduser/FebrisEndUserBLL/Logic/AuthorizationLogic/HardwareKeyAuthorization.cs");

            int refreshAt = auth.IndexOf("public async Task<HardwareAuthenticationResponse> RefreshHardwareToken", StringComparison.Ordinal);
            Assert.True(refreshAt > 0, "RefreshHardwareToken not found, so this guard would pass vacuously");

            string body = auth.Substring(refreshAt);

            Assert.Contains("CredentialRegeneratedAt", body);
        }
    }
}
