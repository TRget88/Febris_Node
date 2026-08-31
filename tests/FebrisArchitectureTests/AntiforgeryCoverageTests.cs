// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Febris.ArchitectureTests
{
    /// <summary>
    /// Build-time guard that keeps every state-changing POST on the user node Portal covered by
    /// antiforgery validation.
    ///
    /// <para>
    /// WHY THIS EXISTS. The Portal shipped with the global filter commented out in
    /// <c>Startup.cs</c>, under a note calling it "a little redicouls". That left exactly three
    /// POST actions unprotected, and they were the worst three on the node: bulk user CREATE, bulk
    /// CSV import, and bulk user REMOVAL. Mass account creation and mass account deletion, both
    /// reachable by cross-site request.
    /// </para>
    ///
    /// <para>
    /// It was not mitigated by the browser either. When Redis/Valkey is configured -- the
    /// recommended HA production path -- the auth cookie is set to <c>SameSite=None</c>
    /// (<c>Startup.cs</c>, the <c>UsesRedisSessionStore</c> branch), so browsers DO attach it to
    /// cross-site POSTs. The relaxed no-Redis path uses <c>SameSite=Lax</c>, which does mitigate it.
    /// The exposure was therefore worst in exactly the configuration a real deployment uses.
    /// </para>
    ///
    /// <para>
    /// The whole protection chain had been disassembled piece by piece: the token-holder
    /// <c>&lt;form&gt;</c> commented out in <c>BulkCreatePartial.cshtml</c>, the
    /// <c>setRequestHeader</c> calls commented out at both JS call sites, the attributes commented
    /// out on the actions, and -- the piece that made the JS pointless -- the token HOLDER form in
    /// <c>BulkCreatePartial.cshtml</c>, so <c>$("[name='__RequestVerificationToken']").val()</c> had
    /// nothing to read and would have sent an empty header.
    /// </para>
    ///
    /// <para>
    /// CORRECTED. An earlier version of this note blamed a missing <c>AddAntiforgery(HeaderName)</c>
    /// call, claiming a JSON body left the token nowhere to go. That was wrong.
    /// <c>AntiforgeryOptions.HeaderName</c> already DEFAULTS to <c>RequestVerificationToken</c> on
    /// .NET 8 (verified by constructing the options, not assumed), so the header would always have
    /// been read. The <c>AddAntiforgery</c> call in Startup is an explicit pin of that existing
    /// default, kept only because the JS hard-codes the same header name.
    /// </para>
    ///
    /// <para>
    /// This guard parses source rather than referencing the Portal, matching the rest of this
    /// project (see the note in the csproj: intentionally no ProjectReference items).
    /// </para>
    /// </summary>
    public class AntiforgeryCoverageTests
    {
        private const string PortalDir = "enduser/FebrisEndUserPortal";

        /// <summary>
        /// GRANDFATHERED exemptions, deliberately EMPTY. Every <c>[HttpPost]</c> action on the
        /// Portal is covered. Do NOT add entries: an endpoint that genuinely cannot carry a token
        /// does not belong on a browser-only host. Format: "<c>FileName.cs:MethodName</c>".
        /// </summary>
        private static readonly HashSet<string> KnownUnprotected =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
            };

        /// <summary>
        /// Best-effort method name out of a signature line, used only to build the exemption key.
        /// </summary>
        private static string MethodNameOf(string signature)
        {
            int paren = signature.IndexOf('(');
            if (paren <= 0)
            {
                return signature.Trim();
            }

            string beforeParen = signature.Substring(0, paren);
            string[] parts = beforeParen.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? beforeParen.Trim() : parts[parts.Length - 1];
        }

        private static IEnumerable<string> PortalSourceFiles(string repoRoot)
        {
            string root = Path.Combine(repoRoot, PortalDir.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !ProjectGraph.IsInBuildOutput(f));
        }

        [Fact]
        public void Every_portal_post_action_validates_the_antiforgery_token()
        {
            string repoRoot = ProjectGraph.FindRepoRoot();
            List<string> violations = new List<string>();
            int covered = 0;

            foreach (string file in PortalSourceFiles(repoRoot))
            {
                string[] lines = File.ReadAllLines(file);

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (!line.Contains("[HttpPost") || line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Walk forward over the remaining attributes to the method signature, skipping
                    // comments and blank lines. Commented-out attributes must NOT count as coverage:
                    // a commented-out attribute is exactly how this defect shipped.
                    List<string> block = new List<string> { line };
                    string signature = line;

                    for (int j = i + 1; j < lines.Length && j < i + 12; j++)
                    {
                        string candidate = lines[j].Trim();
                        if (candidate.Length == 0 || candidate.StartsWith("//", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        block.Add(lines[j]);
                        if (candidate.Contains("public") && candidate.Contains("("))
                        {
                            signature = candidate;
                            break;
                        }
                    }

                    string blockText = string.Join("\n", block);
                    bool validated = blockText.Contains("ValidateAntiForgeryToken")
                        || blockText.Contains("AutoValidateAntiforgeryToken");

                    if (validated)
                    {
                        covered++;
                        continue;
                    }

                    string key = Path.GetFileName(file) + ":" + MethodNameOf(signature);
                    if (KnownUnprotected.Contains(key))
                    {
                        continue;
                    }

                    violations.Add(ProjectGraph.Rel(repoRoot, file) + ":" + (i + 1) + "  " + signature);
                }
            }

            covered.Should_BeGreaterThanZero("the parser must actually be finding POST actions, otherwise this guard passes vacuously");

            Assert.True(
                violations.Count == 0,
                "POST actions on the Portal with no antiforgery validation:\n  " + string.Join("\n  ", violations));
        }

        [Fact]
        public void Startup_registers_the_global_antiforgery_filter_and_the_header_read_side()
        {
            string repoRoot = ProjectGraph.FindRepoRoot();
            string startup = Path.Combine(repoRoot, PortalDir.Replace('/', Path.DirectorySeparatorChar), "Startup.cs");

            Assert.True(File.Exists(startup), "Portal Startup.cs not found at " + startup);

            string[] lines = File.ReadAllLines(startup);

            bool globalFilter = lines.Any(l =>
                !l.TrimStart().StartsWith("//", StringComparison.Ordinal) &&
                l.Contains("Filters.Add(new AutoValidateAntiforgeryTokenAttribute())"));

            // Scanned line by line, skipping comments, so a commented-out line can never satisfy it.
            bool headerName = lines.Any(l =>
                !l.TrimStart().StartsWith("//", StringComparison.Ordinal) &&
                l.Contains("AddAntiforgery") &&
                l.Contains("HeaderName"));

            Assert.True(globalFilter,
                "the global AutoValidateAntiforgeryTokenAttribute filter must stay registered -- it is the default-deny that covers every FUTURE action without anyone remembering an attribute");

            Assert.True(headerName,
                "AddAntiforgery(options => options.HeaderName = ...) must stay pinned -- BulkUserProcessing.js hard-codes that header name, and leaving the coupling to a silent framework default is how it gets changed by accident");
        }

        [Fact]
        public void Bulk_user_scripts_send_the_antiforgery_header()
        {
            // The client half of the same contract. Protecting the server without sending the token
            // would simply break bulk import, so both halves are pinned together.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string script = Path.Combine(
                repoRoot,
                PortalDir.Replace('/', Path.DirectorySeparatorChar),
                "wwwroot", "JSScriptLib", "TableScripts", "BulkUserProcessing.js");

            Assert.True(File.Exists(script), "BulkUserProcessing.js not found at " + script);

            int sends = File.ReadAllLines(script).Count(l =>
                !l.TrimStart().StartsWith("//", StringComparison.Ordinal) &&
                l.Contains("setRequestHeader") &&
                l.Contains("RequestVerificationToken"));

            Assert.True(sends >= 2,
                "both bulk call sites (BulkCreatePost and BulkRemovalPost) must send the RequestVerificationToken header; found " + sends + " uncommented sends");
        }
    }

    internal static class CountAssertions
    {
        public static void Should_BeGreaterThanZero(this int value, string because)
        {
            Assert.True(value > 0, because);
        }
    }
}
