// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.SharedServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Deliberate .NET 8 migration decision:
            // preserve EF Core 3.1-era Npgsql timestamp semantics. Npgsql 6+ otherwise requires
            // DateTime.Kind=Utc for 'timestamp with time zone' and rejects Kind=Utc writes to the
            // 'timestamp without time zone' columns this schema uses everywhere. Must run before
            // ANY DbContext/Npgsql use. Design-time factories set the same switch.
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            // The environment overlay is OPTIONAL. It is gitignored, so a fresh clone has none and
            // a required load threw FileNotFoundException before Serilog or any startup validator
            // existed to say anything useful. This host in particular had none on the dev machine,
            // so it could not start under DEBUG at all.
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

            //initalize folders (host-scoped: EndUser deployments never create central/adminportal dirs)
            var files = new FileServerHandler();
            files.FileInitalizer(configData, FileServerHostRole.EndUser);


            StaticDetails.PassedBackConfig = configData;


            try
            {
                //Need to look over this and figure out the iwebhost vs ihost
                Log.Information("Starting up");
                // ENV-B1 / MDM-B4: provision this tenant's DBs (User/Data/XAPI/Analytics) once,
                // before serving traffic. Guarded + non-crashing; skips any DB whose connection
                // string is absent. Replaces the removed per-context ctor EnsureCreated()+Migrate().
                EndUserDatabaseProvisioner.ProvisionEndUserDatabases(configData);
                //CreateWebHostBuilder(args).Build().Run();
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application startup Failed");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        // T11. Without UseSerilog this host keeps Host.CreateDefaultBuilder's console and debug
        // providers, so every ILogger<T> on it bypassed the configured Serilog file sink entirely.
        // The API's durable log therefore held only the static Serilog.Log.* calls, most of which are
        // startup breadcrumbs, while the ILogger<T> call sites across its controllers and the
        // VideoRetentionService went to stdout alone and died with the container. The Portal has
        // always called this (its Program.cs), and the API never did, which is why the two hosts'
        // log files looked so different for the same class of event.
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .UseEnvironment(Environment)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });

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
