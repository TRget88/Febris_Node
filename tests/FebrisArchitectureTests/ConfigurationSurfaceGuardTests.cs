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
    /// ROADMAP 18: the configuration surface stays honest.
    ///
    /// <para>
    /// WHY. The node's config surface had grown three jobs in one file (committed default, deploy
    /// template, local-setup documentation), carried keys nothing read, and hid a Development
    /// carve-out that let a host sign every token with the literal string <c>{JwtTokenSecret}</c>
    /// without saying so. Each of those produced wasted effort or a wrong conclusion in an audit.
    /// These guards pin the two properties that are cheapest to lose again: the carve-out is
    /// LOGGED, and the templates carry no residue.
    /// </para>
    /// </summary>
    public class ConfigurationSurfaceGuardTests
    {
        private static string PortalPath(string repoRoot, params string[] parts)
        {
            return Path.Combine(new[] { repoRoot, "enduser", "FebrisEndUserPortal" }.Concat(parts).ToArray());
        }

        private static string ApiPath(string repoRoot, params string[] parts)
        {
            return Path.Combine(new[] { repoRoot, "enduser", "FebrisEndUserApi" }.Concat(parts).ToArray());
        }

        /// <summary>C# with <c>//</c> and <c>/* */</c> comments blanked, string literals respected.</summary>
        private static string LiveSource(string path)
        {
            return SelfRecursivePropertyGuardTests.StripCommentsPreservingLayout(File.ReadAllText(path));
        }

        /// <summary>
        /// Index of the <c>}</c> closing the block whose <c>{</c> sits just before
        /// <paramref name="afterOpen"/>, skipping braces inside string literals.
        ///
        /// <para>
        /// The first version of the host guard used <c>IndexOf('}')</c>. The warning it was looking
        /// for is <c>Log.Warning("... Development: {Reason} ...", x.DevelopmentSecretWaiver)</c>, so
        /// the first <c>}</c> after the gate was the one inside the message template, the block was
        /// truncated before the property argument, and the guard failed ON CORRECT CODE -- which the
        /// commit that shipped it did not notice, because its mutation run reported every mutation
        /// "caught" against a guard that was red either way. A guard red on the clean tree catches
        /// everything and proves nothing.
        /// </para>
        /// </summary>
        private static int MatchingBrace(string source, int afterOpen)
        {
            int depth = 1;
            bool inString = false;
            for (int i = afterOpen; i < source.Length; i++)
            {
                char c = source[i];
                if (inString)
                {
                    if (c == '\\')
                    {
                        i++;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
                else if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        // ------------------------------------------------------------------
        // The Development carve-out is observable on both node hosts
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("Api")]
        // The Portal case was retired with ROADMAP 16: its JwtSigningKeyProvider registration
        // existed solely for the NodeAdmin token mint, which is deleted, so the Portal no longer
        // constructs the provider at all. The_portal_does_not_sign_jwts below pins that instead.
        public void The_development_secret_waiver_is_logged_by_the_host(string host)
        {
            // JwtSigningKeyProvider reports what Development waived through
            // DevelopmentSecretWaiver. That is only worth anything if the host LOGS it: the whole
            // defect was a carve-out nobody could see. A refactor that constructs the provider and
            // forgets the warning reintroduces the silence with the property still present, which
            // is why this pins the log call and not the property.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string startup = host == "Api"
                ? ApiPath(repoRoot, "Startup.cs")
                : PortalPath(repoRoot, "Startup.cs");

            Assert.True(File.Exists(startup), "Startup.cs not found at " + startup);
            string live = LiveSource(startup);

            int constructed = live.IndexOf("new JwtSigningKeyProvider(", StringComparison.Ordinal);
            if (constructed < 0)
            {
                constructed = live.IndexOf("new Febris.SharedServices.JwtSigningKeyProvider(", StringComparison.Ordinal);
            }
            Assert.True(constructed >= 0, host + " Startup must construct the JwtSigningKeyProvider");

            // The warning must sit right after construction, in the same registration block. A
            // generous window, but bounded: a log line four hundred lines away is not "at boot".
            string window = live.Substring(constructed, Math.Min(1200, live.Length - constructed));

            // The warning must be CONDITIONED ON the waiver, not merely mention it. The first
            // version of this guard checked that "DevelopmentSecretWaiver" and "Log.Warning(" both
            // appeared in the window, and mutation testing replaced the guarding `if` with
            // `if (false)` -- leaving a dead Log.Warning block whose argument still named the
            // property. Both strings present, nothing ever logged, guard green. So the pattern
            // pins the actual shape: an `if` testing the property against null, and the warning
            // inside the block it opens.
            Match gate = Regex.Match(window, @"if\s*\(\s*\w+\.DevelopmentSecretWaiver\s*!=\s*null\s*\)\s*\{");
            Assert.True(gate.Success,
                host + " Startup must gate on `<provider>.DevelopmentSecretWaiver != null` right after construction, or the Development carve-out is silent again");

            int blockStart = gate.Index + gate.Length;
            int blockEnd = MatchingBrace(window, blockStart);
            Assert.True(blockEnd > blockStart, host + " Startup: the waiver gate has no block body");
            string block = window.Substring(blockStart, blockEnd - blockStart);

            Assert.True(Regex.IsMatch(block, @"Log\.Warning\("),
                host + " Startup must LOG the waiver as a warning INSIDE the gate, not merely read it");
            Assert.True(block.Contains("DevelopmentSecretWaiver"),
                host + " Startup's warning must carry the waived reason, or the operator learns that something was waived but not what");
        }

        [Fact]
        public void The_portal_does_not_sign_jwts()
        {
            // ROADMAP 16: the Portal's only JWT concern was minting the NodeAdmin token, and the
            // token is deleted -- the admin writes it reached moved into the Portal behind cookie
            // auth. A Portal that constructs the signing-key provider again has either regrown a
            // token mint or copied key material somewhere it no longer belongs, and either way it
            // would sit OUTSIDE the waiver-logging pin above, silently. Fail here first.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string live = LiveSource(PortalPath(repoRoot, "Startup.cs"));

            Assert.True(live.IndexOf("new JwtSigningKeyProvider(", StringComparison.Ordinal) < 0
                     && live.IndexOf("new Febris.SharedServices.JwtSigningKeyProvider(", StringComparison.Ordinal) < 0,
                "the Portal Startup constructs JwtSigningKeyProvider again -- the Portal stopped signing JWTs with ROADMAP 16, and a new signer must carry the waiver logging the Api host pins");
        }

        // ------------------------------------------------------------------
        // The templates carry no residue
        // ------------------------------------------------------------------

        /// <summary>
        /// Sections a node host consumes through the framework rather than through a string literal
        /// in our own code, with where the read actually happens. Anything NOT listed here must
        /// have a literal reader in the node graph, or it is residue.
        /// </summary>
        private static readonly Dictionary<string, string> FrameworkReadSections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AllowedHosts"] = "HostFiltering, wired by WebHost.CreateDefaultBuilder",
            ["ConnectionStrings"] = "IConfiguration.GetConnectionString(name) prefixes the section itself",
            ["Transport"] = "NodeTransportOptions, in the Febris.SharedServices package",
            ["FileSystem"] = "FileServerHandler, in the Febris.SharedServices package",
            ["EmailSender"] = "EmailSender and the model library, in the Febris.SharedServices package",
            ["GeoDataUrls"] = "Geocoder and ChartViewModels, in the SharedServices and ModelLibrary packages",
            ["Serilog"] = "Serilog's ReadFrom.Configuration(...) in both Program.cs files",
        };

        /// <summary>The .cs trees a node host can reach, per the csproj ProjectReference graph.</summary>
        private static IEnumerable<string> NodeGraphSources(string repoRoot, string host)
        {
            string[] roots =
            {
                Path.Combine(repoRoot, "enduser", host),
                Path.Combine(repoRoot, "enduser", "FebrisEndUserBLL"),
                Path.Combine(repoRoot, "enduser", "FebrisEndUserDAL"),
            };
            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }
                foreach (string f in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (!ProjectGraph.IsInBuildOutput(f))
                    {
                        yield return f;
                    }
                }
            }
        }

        private static IEnumerable<string> TopLevelSections(string appsettingsPath)
        {
            // JSONC: strip // comments (string-aware enough for these files, which have no '//' in
            // a top-level key) and read the top-level keys positionally. A full parser is not
            // needed to list keys at depth one.
            string text = File.ReadAllText(appsettingsPath);
            text = Regex.Replace(text, @"^\s*//.*$", string.Empty, RegexOptions.Multiline);
            int depth = 0;
            bool inString = false;
            StringBuilderKeyScanner scanner = new StringBuilderKeyScanner();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') { inString = false; scanner.EndString(depth); }
                    else { scanner.Append(c); }
                    continue;
                }
                if (c == '"') { inString = true; scanner.BeginString(); continue; }
                if (c == '{' || c == '[') { depth++; continue; }
                if (c == '}' || c == ']') { depth--; continue; }
                if (c == ':') { scanner.SawColon(depth); }
            }
            return scanner.Keys;
        }

        /// <summary>Collects strings that are immediately followed by ':' at depth 1.</summary>
        private sealed class StringBuilderKeyScanner
        {
            private readonly System.Text.StringBuilder _current = new System.Text.StringBuilder();
            private string _lastString;
            private int _lastDepth = -1;
            public List<string> Keys { get; } = new List<string>();
            public void BeginString() { _current.Clear(); }
            public void Append(char c) { _current.Append(c); }
            public void EndString(int depth) { _lastString = _current.ToString(); _lastDepth = depth; }
            public void SawColon(int depth)
            {
                if (_lastString != null && depth == 1 && _lastDepth == 1 && !_lastString.StartsWith("_", StringComparison.Ordinal))
                {
                    Keys.Add(_lastString);
                }
                _lastString = null;
            }
        }

        [Theory]
        [InlineData("FebrisEndUserApi")]
        [InlineData("FebrisEndUserPortal")]
        public void Every_template_section_has_a_reader_in_the_node_graph(string host)
        {
            // THE RATCHET. Before ROADMAP 18 both templates carried sections nothing read:
            // CertificationSettings (bound, never injected), KeyPersistence (read only by the
            // Developer Portal), UsingRevProxy (no reader anywhere), AppKeys:RedisCache, the whole
            // EmailSender block on the API, a GeoDataUrls tile URL feeding a widget with no map
            // library. Each one cost somebody an hour of "what must I set here". A key that
            // nothing reads is a question with no answer, and this test makes adding one fail.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string template = Path.Combine(repoRoot, "enduser", host, "appsettings.json");
            Assert.True(File.Exists(template), "template not found at " + template);

            List<string> sections = TopLevelSections(template).ToList();
            Assert.True(sections.Count >= 8,
                "the key scan found only " + sections.Count + " top-level sections in " + host + ", which means it has gone blind rather than clean");

            // One pass over the graph, then a lookup per section.
            string graph = string.Join("\n", NodeGraphSources(repoRoot, host).Select(LiveSource));

            List<string> residue = new List<string>();
            foreach (string section in sections)
            {
                if (FrameworkReadSections.ContainsKey(section))
                {
                    continue;
                }
                // A reader names the section as a string literal: GetSection("X"), ["X:Key"],
                // GetValue<T>("X:Key"), or a const holding "X:...".
                bool read = graph.Contains("\"" + section + "\"") || graph.Contains("\"" + section + ":");
                if (!read)
                {
                    residue.Add(section);
                }
            }

            Assert.True(residue.Count == 0,
                host + " appsettings.json carries sections that no code reachable from the host reads (ROADMAP 18 residue). Delete them, or if the framework reads them add them to FrameworkReadSections with the read site:\n  " +
                string.Join("\n  ", residue));
        }

        [Theory]
        [InlineData("FebrisEndUserApi", "CertificationSettings")]
        [InlineData("FebrisEndUserApi", "KeyPersistence")]
        [InlineData("FebrisEndUserApi", "UsingRevProxy")]
        [InlineData("FebrisEndUserApi", "EmailSender")]
        [InlineData("FebrisEndUserApi", "GeoDataUrls")]
        [InlineData("FebrisEndUserApi", "LicenseKey")]
        [InlineData("FebrisEndUserPortal", "CertificationSettings")]
        [InlineData("FebrisEndUserPortal", "KeyPersistence")]
        [InlineData("FebrisEndUserPortal", "UsingRevProxy")]
        [InlineData("FebrisEndUserPortal", "ExternalAuthProviders")]
        [InlineData("FebrisEndUserPortal", "LicenseKey")]
        public void Known_residue_does_not_come_back(string host, string section)
        {
            // The literal-reader check above cannot see residue INSIDE a section that is read as a
            // whole, and two of the sections removed in ROADMAP 18 do still have a literal reader
            // somewhere in the graph (LicenseKey via the legacy hub-federation fallback that stays
            // for existing deployments; ExternalAuthProviders via a registration whose every
            // provider call is commented out). So the sections that were deliberately removed are
            // named here as well, with the reason each one left recorded in docs/ROADMAP.md item 18.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string template = Path.Combine(repoRoot, "enduser", host, "appsettings.json");

            Assert.DoesNotContain(section, TopLevelSections(template));
        }

        [Theory]
        [InlineData("FebrisEndUserApi")]
        [InlineData("FebrisEndUserPortal")]
        public void Every_template_section_is_documented_in_the_configuration_reference(string host)
        {
            // The inverse ratchet. The templates must not carry what the code does not read, and
            // the reference must not omit what the templates carry. docs/CONFIGURATION_REFERENCE.md
            // is THE one artefact for "what do I set" (ROADMAP 18), and it is only that while it
            // is complete. A section added to a template without a paragraph here is a question
            // with no answer again.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string reference = Path.Combine(repoRoot, "docs", "CONFIGURATION_REFERENCE.md");
            Assert.True(File.Exists(reference), "docs/CONFIGURATION_REFERENCE.md is missing -- it is the configuration documentation of record");
            string doc = File.ReadAllText(reference);

            string template = Path.Combine(repoRoot, "enduser", host, "appsettings.json");
            List<string> undocumented = TopLevelSections(template)
                .Where(section => !doc.Contains("### `" + section + "`"))
                .ToList();

            Assert.True(undocumented.Count == 0,
                host + " appsettings.json has sections with no '### `Section`' entry in docs/CONFIGURATION_REFERENCE.md:\n  " +
                string.Join("\n  ", undocumented));
        }

        [Fact]
        public void The_api_registers_the_video_file_handler_so_the_greedy_constructor_is_resolvable()
        {
            // VideoUploadLogic's greedy constructor is the only one that reads VideoLimits:*. It
            // needs IVideoFileHandler; without that registration MS.DI silently picks the legacy
            // constructor and the configured limits are ignored. This line is what makes
            // VideoUploadLogicResolutionTests mean anything, and it looks redundant enough to be
            // tidied away by someone who never read that test.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string live = LiveSource(ApiPath(repoRoot, "Startup.cs"));

            Assert.True(Regex.IsMatch(live, @"AddSingleton<\s*(Febris\.SharedServices\.)?IVideoFileHandler\s*,"),
                "the API Startup must register IVideoFileHandler, or VideoUploadLogic resolves through its legacy constructor and VideoLimits:* is read by nothing that runs");
        }

        // The JwtSigningKeyProvider source guard MOVED to the febris-shared repository on
        // 2026-08-29 (tests/FebrisSharedServicesTests/JwtSigningKeyProviderSourceGuardTests.cs).
        // It read a file that ships there, not here. Everything else in this file stays: it
        // targets enduser/ config, and its shared/ paths are scan ROOTS for the node dependency
        // graph rather than assertions about shared code.
    }
}
