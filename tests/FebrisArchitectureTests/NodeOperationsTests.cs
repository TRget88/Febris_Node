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
    /// T11, operations and retention. Build-time guards for the operational wiring that has no
    /// runtime test because it only manifests on a deployed node.
    ///
    /// <para>
    /// Every guard here corresponds to a defect that was live at HEAD and that NOTHING would have
    /// caught: a host whose logger was never wired, a log path that evaporated on the documented
    /// upgrade, and a readiness endpoint that reported green while the database had no tables.
    /// </para>
    /// </summary>
    public class NodeOperationsTests
    {
        private static IEnumerable<string> UncommentedLines(string text, string commentMarker)
        {
            return text.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith(commentMarker, StringComparison.Ordinal));
        }

        private static string ReadRepoFile(string repoRoot, params string[] parts)
        {
            string[] all = new[] { repoRoot }.Concat(parts).ToArray();
            string path = Path.Combine(all);
            Assert.True(File.Exists(path), "expected file not found: " + path);
            return File.ReadAllText(path);
        }

        // ------------------------------------------------------------------
        // Logging
        // ------------------------------------------------------------------

        [Fact]
        public void Both_node_hosts_wire_serilog_into_the_host_builder()
        {
            // The API host did not. Without UseSerilog the generic host keeps its default console
            // and debug providers, so every ILogger<T> on that host bypassed the configured file
            // sink and existed only in stdout, which the documented upgrade also destroys. The
            // Portal had always called it, so the two hosts silently disagreed about what "logging
            // is configured" meant.
            string repoRoot = ProjectGraph.FindRepoRoot();

            foreach (string host in new[] { "FebrisEndUserApi", "FebrisEndUserPortal" })
            {
                string program = ReadRepoFile(repoRoot, "enduser", host, "Program.cs");

                Assert.True(
                    UncommentedLines(program, "//").Any(l => l.Contains(".UseSerilog()")),
                    host + "/Program.cs must call .UseSerilog(), or its ILogger<T> output never reaches the configured sinks");
            }
        }

        [Fact]
        public void Container_logs_are_written_to_a_persisted_volume_not_the_container_layer()
        {
            // The sink shipped with a RELATIVE path, which resolves under the image WORKDIR /app --
            // the container's writable layer. The documented upgrade rebuilds the image and
            // recreates the containers, so the entire on-disk log history died with every upgrade,
            // along with `docker compose logs`, which is the only log-reading procedure the docs
            // give. Surviving history was zero, not "console only".
            string repoRoot = ProjectGraph.FindRepoRoot();
            string compose = ReadRepoFile(repoRoot, "docker-compose.yml");

            List<string> paths = UncommentedLines(compose, "#")
                .Where(l => l.Contains("Serilog__WriteTo__1__Args__path"))
                .ToList();

            Assert.True(paths.Count == 2,
                "both node hosts must pin their log path to a persisted location; found " + paths.Count);

            foreach (string line in paths)
            {
                Assert.True(line.Contains("/data/storage/"),
                    "the log path must live on the persisted storage volume, not the container layer: " + line);
            }

            // One directory per host, or they interleave into one file.
            Assert.Contains(paths, l => l.Contains("/logs/api/"));
            Assert.Contains(paths, l => l.Contains("/logs/portal/"));
        }

        [Fact]
        public void The_log_sink_has_an_explicit_size_bound()
        {
            // Serilog.Sinks.File defaults fileSizeLimitBytes to 1 GB with rollOnFileSizeLimit
            // FALSE, so an unbounded-looking config silently stops accepting events partway through
            // a busy day. A blind spot with no error is worse during an incident than a full disk,
            // because nothing indicates that logging stopped.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string compose = ReadRepoFile(repoRoot, "docker-compose.yml");

            List<string> live = UncommentedLines(compose, "#").ToList();

            Assert.Contains(live, l => l.Contains("Serilog__WriteTo__1__Args__fileSizeLimitBytes"));
            Assert.Contains(live, l => l.Contains("Serilog__WriteTo__1__Args__rollOnFileSizeLimit"));
        }

        // ------------------------------------------------------------------
        // Forwarded headers and the client-IP trust boundary
        // ------------------------------------------------------------------

        [Fact]
        public void The_framework_forwarded_headers_flag_is_not_set()
        {
            // ASPNETCORE_FORWARDEDHEADERS_ENABLED does NOT merely enable the middleware. In
            // WebHost.ConfigureWebDefaults the ForwardedHeaders_Enabled branch CLEARS KnownNetworks
            // and KnownProxies, and the framework's own source comment says why: "Only loopback
            // proxies are allowed by default. Clear that restriction because forwarders are being
            // enabled by explicit configuration."
            //
            // It therefore registers a SECOND forwarded-headers middleware, through a startup filter
            // that runs ahead of everything in Configure, trusting ANY peer. It would set
            // RemoteIpAddress from the header before the app's own restricted UseForwardedHeaders
            // ever ran, defeating the deliberate "KnownNetworks, NOT TrustAllProxies" decision and
            // re-opening H-15 through X-Forwarded-For for anyone able to reach a host directly.
            // node-api publishes 8081, loopback-only by default but explicitly widenable.
            //
            // The app applies XForwardedFor and XForwardedProto itself, with KnownNetworks, so this
            // flag buys nothing. It was previously set with a comment asserting the opposite of the
            // framework's behaviour, which is exactly how it would come back.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string compose = ReadRepoFile(repoRoot, "docker-compose.yml");

            Assert.False(
                UncommentedLines(compose, "#").Any(l => l.Contains("ASPNETCORE_FORWARDEDHEADERS_ENABLED")),
                "ASPNETCORE_FORWARDEDHEADERS_ENABLED clears KnownNetworks and KnownProxies, granting trust-any-peer ahead of the app's own restricted middleware. Declare the proxy with ForwardedHeaders__KnownNetworks instead.");
        }

        [Fact]
        public void The_reverse_proxy_is_declared_rather_than_blindly_trusted()
        {
            // The other half of the same trust boundary. Without a declared proxy, ASP.NET Core
            // discards Caddy's forwarded headers, every caller resolves to Caddy's own address, and
            // the rate limiter collapses into ONE bucket for the whole node. With TrustAllProxies
            // instead, a direct caller on the published API port could forge the chain. The correct
            // posture is naming the network, and the compose subnet is pinned so that name is
            // deterministic rather than whatever Docker assigns.
            string repoRoot = ProjectGraph.FindRepoRoot();
            List<string> live = UncommentedLines(ReadRepoFile(repoRoot, "docker-compose.yml"), "#").ToList();

            Assert.True(
                live.Any(l => l.Contains("ForwardedHeaders__KnownNetworks")),
                "the reverse proxy must be declared, or every caller collapses into one rate-limit bucket");

            Assert.True(
                live.Any(l => l.Contains("subnet:")),
                "the compose subnet must stay pinned, or the declared KnownNetworks CIDR stops matching the network Docker actually creates");

            Assert.False(
                live.Any(l => l.Contains("TrustAllProxies") && l.Contains("true")),
                "TrustAllProxies would let a direct caller on the published API port forge the forwarded chain");
        }

        // ------------------------------------------------------------------
        // Dormant features must not look like working ones
        // ------------------------------------------------------------------

                // REMOVED 2026-08-18 with the microcredential feature. This guard asserted that the
        // Awarded Badges screen carried a notice saying awarding was inactive. The screen is
        // gone, and deleting the feature serves the guard's purpose more completely than a
        // notice on a permanently empty page ever did.

        // ------------------------------------------------------------------
        // Analytics volume
        // ------------------------------------------------------------------

        [Fact]
        public void The_analytics_middleware_consults_the_request_filter()
        {
            // AnalyticsVolumeTests exercises the filter in ISOLATION, so every one of those cases
            // stays green if somebody deletes the guard from InvokeAsync and the middleware goes
            // back to writing a row per request. A mutation run proved exactly that gap. This is the
            // guard for the WIRING, which is the half unit tests structurally cannot see.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string middleware = ReadRepoFile(repoRoot,
                "enduser", "FebrisEndUserBLL", "Logic", "AnalyticsLogic", "AnalyticsLogic.cs");

            List<string> live = UncommentedLines(middleware, "//").ToList();

            Assert.True(
                live.Any(l => l.Contains("AnalyticsRequestFilter.ShouldRecord")),
                "LocalAnalyticsMiddleware must consult AnalyticsRequestFilter, or every health probe and static asset writes a database row again");
        }

        [Fact]
        public void Nothing_deletes_an_xapi_actor_row()
        {
            // THE guard. FK_LocalStatement_Actor_ActorId is ON DELETE CASCADE over a NOT NULL
            // column, verified against the live schema, so deleting ONE Actor row deletes EVERY
            // statement that learner ever produced. That is the learning record this node exists to
            // keep.
            //
            // IActorQueries.Delete exists, has no caller today, and suppresses its own exception, so
            // a future "tidy up orphaned actors" job would look reasonable, compile, silently
            // destroy learner history and report nothing. Account deletion PSEUDONYMISES instead:
            // it clears Name, Mbox and OpenId and keeps Mbox_sha1sum, which is a legal xAPI Inverse
            // Functional Identifier on its own, so the Agent stays valid and the statements stay
            // attributable.
            string repoRoot = ProjectGraph.FindRepoRoot();
            List<string> callers = new List<string>();
            int scanned = 0;

            // Scoped to the NODE. shared/FebrisSharedLogicLayer carries its own ActorLogic.Delete
            // twin serving the central and developer tiers, which are outside this audit. It has no
            // caller either and is recorded in docs/BUGS.md rather than removed from under them.
            foreach (string dir in new[] { "enduser" })
            {
                string root = Path.Combine(repoRoot, dir);
                if (!Directory.Exists(root)) continue;

                foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (ProjectGraph.IsInBuildOutput(file)) continue;
                    // The declaration and implementation live in ActorQueries itself.
                    if (Path.GetFileName(file).Equals("ActorQueries.cs", StringComparison.OrdinalIgnoreCase)) continue;

                    scanned++;
                    string text = File.ReadAllText(file);

                    // Resolve the VARIABLE NAMES that hold an IActorQueries in this file, rather
                    // than guessing them. The first version of this guard hardcoded names like
                    // "actorContext" and a mutation run walked straight past it: ActorLogic holds
                    // its queries in a field called _dataContext, so the brittle version passed
                    // while the code deleted actors.
                    HashSet<string> actorHandles = new HashSet<string>(StringComparer.Ordinal);
                    foreach (Match decl in Regex.Matches(text, @"IActorQueries\s+(\w+)"))
                    {
                        actorHandles.Add(decl.Groups[1].Value);
                    }
                    // Constructor parameters assign into fields; follow "_x = y;" so the field name
                    // is covered too.
                    foreach (Match asn in Regex.Matches(text, @"(\w+)\s*=\s*(\w+)\s*;"))
                    {
                        if (actorHandles.Contains(asn.Groups[2].Value))
                        {
                            actorHandles.Add(asn.Groups[1].Value);
                        }
                    }

                    if (actorHandles.Count == 0) continue;

                    string[] lines = text.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (line.StartsWith("//", StringComparison.Ordinal)) continue;

                        foreach (string handle in actorHandles)
                        {
                            if (line.Contains(handle + ".Delete("))
                            {
                                callers.Add(ProjectGraph.Rel(repoRoot, file) + ":" + (i + 1) + "  " + line);
                                break;
                            }
                        }
                    }
                }
            }

            Assert.True(scanned > 0, "the scan must actually read source, otherwise this guard passes vacuously");

            Assert.True(callers.Count == 0,
                "something deletes an xAPI Actor. That CASCADES to every statement the learner produced. Pseudonymise instead (see ActorLogic.Pseudonymise): "
                    + string.Join(" | ", callers));
        }

        [Fact]
        public void Every_analytics_query_string_write_goes_through_the_redactor()
        {
            // H-26 was closed on 2026-08-09 and was only HALF closed. The redaction landed on the
            // two analytics MIDDLEWARES and never reached ModuleUsageAnalyticsLogic or
            // ModuleDownloadAnalyticsLogic, which kept storing the query string verbatim into tables
            // rendered to Org Admins. ASP.NET Identity puts password-reset and email-confirmation
            // tokens in the query of the emailed link, which is why H-26 was rated account-takeover
            // material retained forever.
            //
            // Counting write SITES rather than checking known files: the defect was that somebody
            // fixed the sites they knew about, so a guard naming those same sites would have caught
            // nothing. A NEW analytics logger added tomorrow fails this.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string analyticsDir = Path.Combine(repoRoot, "enduser", "FebrisEndUserBLL", "Logic", "AnalyticsLogic");

            Assert.True(Directory.Exists(analyticsDir), "analytics logic directory not found at " + analyticsDir);

            List<string> unredacted = new List<string>();
            int writeSites = 0;

            foreach (string file in Directory.EnumerateFiles(analyticsDir, "*.cs", SearchOption.AllDirectories))
            {
                if (ProjectGraph.IsInBuildOutput(file))
                {
                    continue;
                }

                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();

                    // A live assignment of the Query column, ignoring commented-out scaffolding.
                    if (line.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (!line.StartsWith("Query = ", StringComparison.Ordinal)) continue;
                    if (!line.Contains("QueryString")) continue;

                    writeSites++;
                    if (!line.Contains("SensitiveQueryRedactor.Redact"))
                    {
                        unredacted.Add(ProjectGraph.Rel(repoRoot, file) + ":" + (i + 1) + "  " + line);
                    }
                }
            }

            Assert.True(writeSites > 0, "the scan must find analytics query writes, otherwise this guard passes vacuously");

            Assert.True(unredacted.Count == 0,
                "analytics rows storing a RAW query string. Identity puts reset tokens there and these tables are shown to Org Admins:\n  "
                    + string.Join("\n  ", unredacted));
        }

        [Fact]
        public void The_analytics_list_screen_does_not_load_the_whole_table()
        {
            // The controller called the unbounded Get(), which materialises every analytics row ever
            // recorded, sorts it in memory by an unindexed column, and then takes 25. The table
            // grows one row per HTTP request, so the cost of viewing page 1 grew forever.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string controller = ReadRepoFile(repoRoot,
                "enduser", "FebrisEndUserPortal", "Controllers", "Analytics", "LocalAnalyticsController.cs");

            List<string> live = UncommentedLines(controller, "//").ToList();

            Assert.True(
                live.Any(l => l.Contains("_context.GetPage(")),
                "the analytics list must read one page from the database");

            Assert.False(
                live.Any(l => l.Contains("_context.Get()")),
                "the analytics list must not call the unbounded Get(), which loads the entire history to render 25 rows");
        }

        [Fact]
        public void The_analytics_timestamp_column_is_indexed()
        {
            // TimeStamp is the only column any reader orders by and the table had exactly one index,
            // the primary key. Declared on the model rather than in a migration because the
            // provisioner gives this context EnsureCreated(), so a migration would never run.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string context = ReadRepoFile(repoRoot,
                "enduser", "FebrisEndUserDAL", "DataContext", "AnalyticsDbContext.cs");

            List<string> live = UncommentedLines(context, "//").ToList();

            Assert.True(
                live.Any(l => l.Contains("HasIndex") && l.Contains("TimeStamp")),
                "LocalAnalytics.TimeStamp must be indexed on the model, or EnsureCreated builds the table without it");
        }

        // ------------------------------------------------------------------
        // Readiness
        // ------------------------------------------------------------------

        [Fact]
        public void Readiness_proves_the_schema_is_usable_not_merely_reachable()
        {
            // THE defect this guard exists for. DbContextHealthCheck probes CanConnectAsync, which
            // answers "reachable" for a connectable database with ZERO tables. Combined with a
            // provisioner that swallows every migration failure, /health/ready reported green on a
            // node whose schema was never created, the compose healthcheck passed, and
            // depends_on: service_healthy released the reverse proxy onto it.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string registration = ReadRepoFile(repoRoot,
                "enduser", "FebrisEndUserBLL", "Logic", "HealthLogic", "NodeHealthRegistration.cs");

            List<string> live = UncommentedLines(registration, "//").ToList();

            foreach (string context in new[] { "ApplicationDbContext", "DataDbContext", "XApiDbContext" })
            {
                Assert.True(
                    live.Any(l => l.Contains("AddSchemaCheck<" + context + ">")),
                    context + " must have a schema readiness check, or a failed migration reports ready");
            }
        }

        [Fact]
        public void The_migration_less_analytics_context_is_excluded_from_the_schema_check()
        {
            // AnalyticsDbContext is provisioned with EnsureCreated(), which writes no
            // __EFMigrationsHistory, yet it OWNS a migration chain
            // (enduser/FebrisEndUserDAL/Migrations/AnalyticsDbContextModelSnapshot.cs). Asking it
            // for pending migrations would report the whole chain forever and pin readiness red on
            // a perfectly healthy node. That mismatch is a real defect recorded in docs/BUGS.md;
            // this guard just stops it being "fixed" by adding the check here.
            string repoRoot = ProjectGraph.FindRepoRoot();
            string registration = ReadRepoFile(repoRoot,
                "enduser", "FebrisEndUserBLL", "Logic", "HealthLogic", "NodeHealthRegistration.cs");

            Assert.DoesNotContain("AddSchemaCheck<AnalyticsDbContext>",
                string.Join("\n", UncommentedLines(registration, "//")));
        }
    }
}
