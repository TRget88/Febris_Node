// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Text;

namespace Febris.UserNode.DataAccessLayer.DataContext
{
    //public class DatabaseContextFactory
    //{
    //    class FebrisPortalDatabaseContextFactory : IDesignTimeDbContextFactory<DataDbContext>
    //    {
    //        //this seem to all need to be the same. The way it is set up it should not work. 
    //        public DataDbContext CreateDbContext(string[] args)
    //        {
    //            AppConfiguration appConfiguration = new AppConfiguration();
    //            var opsBuilder = new DbContextOptionsBuilder<DataDbContext>();
    //            opsBuilder.UseNpgsql(appConfiguration.DataConnectionString);
    //            return new DataDbContext(opsBuilder.Options);
    //        }
    //    }
    //    class UserDatabaseContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    //    {
    //        public ApplicationDbContext CreateDbContext(string[] args)
    //        {
    //            AppConfiguration appConfiguration = new AppConfiguration();
    //            var opsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
    //            opsBuilder.UseNpgsql(appConfiguration.UserDataConnectionString);
    //            return new ApplicationDbContext(opsBuilder.Options);
    //        }
    //    }
    //    class XApiDatabaseContextFactory : IDesignTimeDbContextFactory<XApiDbContext>
    //    {
    //        public XApiDbContext CreateDbContext(string[] args)
    //        {
    //            AppConfiguration appConfiguration = new AppConfiguration();
    //            var opsBuilder = new DbContextOptionsBuilder<XApiDbContext>();
    //            opsBuilder.UseNpgsql(appConfiguration.XApiConnectionString);
    //            return new XApiDbContext(opsBuilder.Options);
    //        }
    //    }
    //}

    /// <summary>
    /// The Npgsql legacy-timestamp switch, applied at DESIGN TIME so `dotnet ef migrations add`
    /// doesn't scaffold spurious timestamptz AlterColumns for every temporal column.
    /// Hosts set the same switch in Program.Main.
    /// </summary>
    internal static class DesignTimeNpgsqlSwitches
    {
        internal static void Apply()
            => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    /// <summary>
    /// Design-time connection-string resolution that works in a sanitized checkout.
    /// <para>
    /// <c>AppConfiguration</c> (DEBUG) hard-requires the portal's private
    /// <c>appsettings.Development.json</c>, which this repo deliberately does not ship
    /// (secrets are scrubbed to templates). Commands like <c>dotnet ef migrations add</c>
    /// only need the Npgsql PROVIDER configured -- they never open a connection -- so when
    /// the developer config is absent we fall back to an obviously-fake placeholder
    /// instead of failing scaffolding. Commands that DO connect (<c>database update</c>)
    /// will fail loudly on the placeholder, which is correct: point them at a real
    /// deployment config instead.
    /// </para>
    /// </summary>
    internal static class DesignTimeConnectionStrings
    {
        internal const string Placeholder =
            "Host=localhost;Database=febris_design_time_only;Username=design;Password=design";

        internal static string Resolve(Func<AppConfiguration, string> selector)
        {
            try
            {
                string configured = selector(new AppConfiguration());
                return string.IsNullOrWhiteSpace(configured) ? Placeholder : configured;
            }
            catch (Exception)
            {
                // No developer config in this checkout (sanitized repo) -- scaffold-only fallback.
                return Placeholder;
            }
        }
    }

    public class FebrisPortalDatabaseContextFactory : IDesignTimeDbContextFactory<DataDbContext>
    {
        //this seem to all need to be the same. The way it is set up it should not work.
        public DataDbContext CreateDbContext(string[] args)
        {
            DesignTimeNpgsqlSwitches.Apply();
            var opsBuilder = new DbContextOptionsBuilder<DataDbContext>();
            opsBuilder.UseNpgsql(DesignTimeConnectionStrings.Resolve(c => c.DataConnectionString));
            return new DataDbContext(opsBuilder.Options);
        }
    }
    public class UserDatabaseContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            DesignTimeNpgsqlSwitches.Apply();
            var opsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            opsBuilder.UseNpgsql(DesignTimeConnectionStrings.Resolve(c => c.UserDataConnectionString));
            return new ApplicationDbContext(opsBuilder.Options);
        }
    }
    public class XApiDatabaseContextFactory : IDesignTimeDbContextFactory<XApiDbContext>
    {
        public XApiDbContext CreateDbContext(string[] args)
        {
            DesignTimeNpgsqlSwitches.Apply();
            var opsBuilder = new DbContextOptionsBuilder<XApiDbContext>();
            opsBuilder.UseNpgsql(DesignTimeConnectionStrings.Resolve(c => c.XApiConnectionString));
            return new XApiDbContext(opsBuilder.Options);
        }
    }
    public class AnalyticsDatabaseContextFactory : IDesignTimeDbContextFactory<AnalyticsDbContext>
    {
        public AnalyticsDbContext CreateDbContext(string[] args)
        {
            DesignTimeNpgsqlSwitches.Apply();
            var opsBuilder = new DbContextOptionsBuilder<AnalyticsDbContext>();
            opsBuilder.UseNpgsql(DesignTimeConnectionStrings.Resolve(c => c.AnalyticsConnectionString));
            return new AnalyticsDbContext(opsBuilder.Options);
        }
    }
}
