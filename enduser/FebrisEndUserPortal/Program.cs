// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
//using Febris.UserNode.Portal.LocalUtility;
using Febris.EnumLibrary;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.SharedServices;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.Portal
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // NET8 migration, deliberate and owner-ratified: preserve EF Core 3.1-era Npgsql
            // timestamp semantics. Npgsql 6+ otherwise requires DateTime.Kind=Utc for
            // 'timestamp with time zone' and rejects Kind=Utc writes to the
            // 'timestamp without time zone' columns this schema uses everywhere. Must run before
            // ANY DbContext/Npgsql use. Design-time factories set the same switch.
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            // The environment overlay is OPTIONAL. It is gitignored, so a fresh clone has none and
            // a required load threw FileNotFoundException before Serilog or any startup validator
            // existed to say anything useful.
            //
            // To supply local settings: copy the committed appsettings.json to
            // appsettings.Development.json and substitute the {Token} placeholders it already
            // carries for every key. That file IS the template -- there is deliberately no second
            // template file to drift out of sync with it. Only the keys you actually need to
            // override have to stay in the copy; the rest fall through to appsettings.json.
#if (DEBUG)
            var configData = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json")
                            .AddJsonFile("appsettings.Development.json", optional: true)
                            .AddEnvironmentVariables()
                            .Build();
#elif (STAGING)
            var configData = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json")
                            .AddJsonFile("appsettings.Staging.json", optional: true)
                            .AddEnvironmentVariables()
                            .Build();
#else
            var configData = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json")            
                            .AddEnvironmentVariables()
                            .Build();
#endif
            // LOG-B1: build through the validator so a sink that fails to bind (missing package ->
            // host logs nowhere) is shouted to stderr instead of being silently dropped by Serilog.
            Log.Logger = SerilogStartupValidator.CreateAndValidate(
                () => new LoggerConfiguration()
                    .ReadFrom.Configuration(configData)
                    .CreateLogger(),
                configData);

            // STARTUP PRECONDITION. ConfigurationPlaceholderValidator already exists and this
            // host already calls it, but from Startup.Configure, which the generic host does not
            // invoke until host.Run(). SeedAllDataAsync runs BEFORE host.Run(), so the seed
            // reaches Npgsql first and the guard never executes.
            //
            // Measured on 2026-09-02. A Production boot with no injected environment emitted four
            // provisioning failures and then died inside Identity role seeding, every message
            // reading "Format of the initialization string does not conform to specification
            // starting at index 0", which names neither the offending key nor the file it came
            // from. One misconfiguration produced roughly ten messages across two streams and not
            // one of them was actionable.
            //
            // The gate is on the KEY, not the environment. UserDBConnection is what SeedRolesAsync
            // opens, and an unsubstituted value there is fatal in every environment including
            // Development. Every other unresolved placeholder stays a warning everywhere, so a
            // node that boots today without SMTP keeps booting.
            List<string> unresolvedKeys = ConfigurationPlaceholderValidator.FindUnresolvedPlaceholders(configData);
            if (unresolvedKeys.Count > 0)
            {
                Log.Warning(
                    "Configuration keys still hold the literal placeholder that appsettings.json "
                    + "ships, so no deploy-time value was injected for them: {Placeholders}. Supply "
                    + "them through the environment, or copy appsettings.json to "
                    + "appsettings.Development.json and substitute them for local work.",
                    string.Join(", ", unresolvedKeys));

                if (unresolvedKeys.Contains("ConnectionStrings:UserDBConnection"))
                {
                    Log.Fatal(
                        "ConnectionStrings:UserDBConnection is an unsubstituted placeholder. This "
                        + "host cannot seed Identity roles or serve a page without it, so startup "
                        + "is aborted here rather than failing deep inside the database driver.");
                    Log.CloseAndFlush();
                    System.Environment.ExitCode = 1;
                    return;
                }
            }

            StaticDetails.PassedBackConfig = configData;

            //initalize folders (host-scoped: EndUser deployments never create central/adminportal dirs)
            var files = new FileServerHandler();
            files.FileInitalizer(configData, FileServerHostRole.EndUser);

            

            try
            {
                Log.Information("Starting up");
                // ENV-B1 / MDM-B4: provision the tenant DB(s) this host owns (UserDB / Identity)
                // BEFORE the seed runs -- SeedData needs the Identity schema to exist. Guarded +
                // non-crashing; skips any DB whose connection string this host doesn't carry.
                EndUserDatabaseProvisioner.ProvisionEndUserDatabases(configData);
                var host = CreateWebHostBuilder(args).Build();
                // One-time data seed (roles + the bootstrap ITAdmin). Runs awaited, once, BEFORE
                // the host serves traffic, so it completes and any failure surfaces here.
                // Idempotent -- the admin is only created when absent. See SeedData.SeedAllDataAsync.
                // (Corrected 2026-08-25: this said "bootstrap SuperAdmin". SuperAdmin is a VENDOR
                // role and was dropped from the node seed by owner ruling on 2026-08-01.)
                Data.SeedData.SeedAllDataAsync(host.Services).GetAwaiter().GetResult();

                // Sample content for local work, so the node can be exercised without hand-entering
                // curricula, cohorts and devices first. The class body is fenced #if DEBUG, so this
                // call compiles to a no-op in Release rather than relying on a config switch.
                // Idempotent and marked -- see DevSampleData.
                Data.DevSampleData.SeedAsync(host.Services).GetAwaiter().GetResult();

                host.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Applicaiton startup Failed");
                // Signal a non-zero exit so an orchestrator (Docker/K8s) restarts the node -- and retries
                // the idempotent seed -- instead of reading a mis-seeded/failed startup as a clean exit.
                System.Environment.ExitCode = 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }


            //CreateHostBuilder(args).Build().Run();
        }

        // NET8 Wave 4: converted from the pre-3.0 WebHost.CreateDefaultBuilder pattern to the
        // Generic Host -- Serilog.AspNetCore 8 only ships the IHostBuilder UseSerilog overload.
        // Same Startup/environment/Kestrel config; callers still do .Build().Run().
        public static IHostBuilder CreateWebHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .UseEnvironment(Environment)
            .ConfigureWebHostDefaults(webBuilder => webBuilder
                .UseStartup<Startup>()
                .UseKestrel(options =>
                {
                    options.AddServerHeader = false;
                }));

        public static string Environment
        {
            get
            {
                string environmentName = "production";
#if DEBUG
                environmentName = "development";
#elif STAGING
                environmentName = "staging";
#elif STAGINGDEV
                 environmentName = "stagingDev";
#elif RELEASE
                 environmentName = "production";
#endif
                return environmentName;
            }
        }

        //public static IHostBuilder CreateHostBuilder(string[] args) =>
        //    Host.CreateDefaultBuilder(args)
        //        .ConfigureWebHostDefaults(webBuilder =>
        //        {
        //            webBuilder.UseStartup<Startup>();
        //        });
    }
}
