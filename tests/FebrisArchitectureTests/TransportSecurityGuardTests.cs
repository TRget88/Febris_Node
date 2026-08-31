// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Febris.ArchitectureTests
{
    /// <summary>
    /// ROADMAP 5: both node hosts apply the SAME operator-configured transport security, and
    /// neither falls back to framework defaults behind the operator's back.
    ///
    /// <para>
    /// WHY. `Transport` was read in full by the Portal and only for CORS by the API, so an
    /// operator who hardened HSTS got it on one of their two hosts and no warning about the other.
    /// The API's `app.UseHsts()` was a bare call on framework defaults (30 days, no
    /// includeSubDomains, no preload) while the shared options object documents 365 days with
    /// subdomains, its HTTPS redirection was a commented-out line, and it emitted no
    /// X-Frame-Options at all. A configuration surface that silently applies to half the
    /// deployment is worse than one that does not exist, because the operator believes it worked.
    /// </para>
    ///
    /// <para>
    /// WHY A SOURCE GUARD. The configuration-surface ratchets next door check that every template
    /// SECTION has a reader, and `Transport` had one on both hosts all along -- for CORS. Neither
    /// ratchet inspects sub-keys, so every regression this file exists to catch (wiring the code
    /// and not the template, adding the key and not the reader, reverting one host to a bare call)
    /// passes them green. The parity is only checkable by reading the pipelines.
    /// </para>
    /// </summary>
    public class TransportSecurityGuardTests
    {
        private static string HostStartup(string host)
        {
            string path = Path.Combine(
                ProjectGraph.FindRepoRoot(), "enduser",
                host == "Api" ? "FebrisEndUserApi" : "FebrisEndUserPortal", "Startup.cs");
            Assert.True(File.Exists(path), "Startup.cs not found at " + path);
            return SelfRecursivePropertyGuardTests.StripCommentsPreservingLayout(File.ReadAllText(path));
        }

        [Theory]
        [InlineData("Api")]
        [InlineData("Portal")]
        public void Both_hosts_configure_hsts_from_the_transport_section(string host)
        {
            // AddHsts must exist AND be fed from Transport, not left to framework defaults. The
            // API had no AddHsts at all, which is exactly the state this pins shut.
            string live = HostStartup(host);

            int add = live.IndexOf("AddHsts(", StringComparison.Ordinal);
            Assert.True(add >= 0,
                host + " must call AddHsts, or app.UseHsts() runs on the framework's 30-day, no-subdomains default");

            string block = live.Substring(add, Math.Min(600, live.Length - add));
            Assert.True(Regex.IsMatch(block, @"MaxAge\s*=\s*TimeSpan\.FromDays\(\s*\w+\.Hsts\.MaxAgeDays"),
                host + " must take the HSTS max-age from Transport:Hsts:MaxAgeDays");
            Assert.True(Regex.IsMatch(block, @"IncludeSubDomains\s*=\s*\w+\.Hsts\.IncludeSubdomains"),
                host + " must take includeSubDomains from Transport:Hsts:IncludeSubdomains");
            Assert.True(Regex.IsMatch(block, @"Preload\s*=\s*\w+\.Hsts\.Preload"),
                host + " must take preload from Transport:Hsts:Preload");
        }

        [Theory]
        [InlineData("Api")]
        [InlineData("Portal")]
        public void Neither_host_emits_hsts_unconditionally(string host)
        {
            // Every UseHsts() call must sit inside an `if` testing the operator's Enabled flag.
            // A bare call cannot be turned off, which matters because a node whose TLS terminates
            // at a proxy that owns the header needs to stop emitting its own.
            string live = HostStartup(host);

            foreach (Match call in Regex.Matches(live, @"app\.UseHsts\(\)"))
            {
                string before = live.Substring(Math.Max(0, call.Index - 300), Math.Min(300, call.Index));
                Assert.True(Regex.IsMatch(before, @"if\s*\(\s*\w+\.Hsts\.Enabled\s*\)\s*\{[^}]*$"),
                    host + " calls app.UseHsts() without an enclosing `if (<transport>.Hsts.Enabled)` -- " +
                    "an operator cannot turn it off, and on one host but not the other is worse than neither");
            }
        }

        [Theory]
        [InlineData("Api")]
        [InlineData("Portal")]
        public void Both_hosts_gate_https_redirection_on_the_operator_setting(string host)
        {
            // Off by default because self-host nodes terminate TLS at a proxy where an app-level
            // redirect loops. The API expressed "off" as a COMMENTED-OUT line, which is not the
            // same thing: it cannot be turned on.
            string live = HostStartup(host);

            int call = live.IndexOf("app.UseHttpsRedirection()", StringComparison.Ordinal);
            Assert.True(call >= 0,
                host + " has no live UseHttpsRedirection call -- commenting it out makes Transport:HttpsRedirection a lie");

            string before = live.Substring(Math.Max(0, call - 300), Math.Min(300, call));
            Assert.True(Regex.IsMatch(before, @"if\s*\(\s*\w+\.HttpsRedirection\s*\)\s*\{[^}]*$"),
                host + " must gate UseHttpsRedirection on Transport:HttpsRedirection");
        }

        [Theory]
        [InlineData("Api")]
        [InlineData("Portal")]
        public void Both_hosts_apply_the_x_frame_options_policy_and_fail_safe(string host)
        {
            // The three-way policy: "Off" omits, "Deny" denies, ANYTHING ELSE (including a typo)
            // must land on SameOrigin. A guard that only checked the header is emitted would miss
            // the failure that actually matters, which is a misspelling silently disabling it.
            string live = HostStartup(host);

            Assert.True(Regex.IsMatch(live, @"SecurityHeaders\.XFrameOptions"),
                host + " must read Transport:SecurityHeaders:XFrameOptions");
            Assert.True(Regex.IsMatch(live, @"""Off""", RegexOptions.IgnoreCase),
                host + " must honour the \"Off\" value that omits the header entirely");
            Assert.True(Regex.IsMatch(live, @"""Deny""", RegexOptions.IgnoreCase),
                host + " must honour the \"Deny\" value");
            Assert.True(Regex.IsMatch(live, @"SameOrigin", RegexOptions.IgnoreCase),
                host + " must fall back to SameOrigin, so an unrecognised value never drops the protection");
        }

        [Fact]
        public void The_api_emits_its_security_headers_before_routing()
        {
            // Response-header middleware that runs after UseRouting can be short-circuited by an
            // endpoint, so the headers would be missing from exactly the responses that matter.
            string live = HostStartup("Api");

            int headers = live.IndexOf("X-Frame-Options", StringComparison.Ordinal);
            int routing = live.IndexOf("app.UseRouting()", StringComparison.Ordinal);

            Assert.True(headers >= 0, "the Api must emit X-Frame-Options");
            Assert.True(routing >= 0, "the Api must call UseRouting");
            Assert.True(headers < routing,
                "the Api's security headers must be written BEFORE UseRouting, or an endpoint can return without them");
        }

        [Fact]
        public void The_api_template_ships_the_whole_transport_section()
        {
            // The config-surface ratchets check SECTIONS, not sub-keys, so they cannot see a
            // Transport block that ships only Cors -- which is what the Api shipped while three
            // quarters of the section applied to it invisibly.
            string template = File.ReadAllText(Path.Combine(
                ProjectGraph.FindRepoRoot(), "enduser", "FebrisEndUserApi", "appsettings.json"));

            foreach (string key in new[] { "\"Hsts\"", "\"HttpsRedirection\"", "\"Cors\"", "\"SecurityHeaders\"" })
            {
                Assert.True(template.Contains(key, StringComparison.Ordinal),
                    "the Api template's Transport section must ship " + key +
                    " so the operator can see every knob that applies to this host");
            }
        }
    }
}
