// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Febris.UserNode.DataAccessLayer.DataContext
{
    /// <summary>
    /// ENV-B1 / MDM-B4: explicit per-tenant database provisioning, run ONCE at host startup.
    ///
    /// <para>
    /// The four per-tenant contexts used to self-provision in their constructors via
    /// <c>if (Database.EnsureCreated()) Database.Migrate();</c>. That pair is mutually exclusive: on
    /// a brand-new tenant DB, <c>EnsureCreated()</c> builds the schema straight from the model with no
    /// <c>__EFMigrationsHistory</c>, then <c>Migrate()</c> replays the Initial migration and collides
    /// (<c>42P07 relation already exists</c>), so a fresh primary-tenant deployment could not boot. It
    /// also ran on every instantiation and blocked design-time <c>dotnet ef</c>. MDM-B4 removed the
    /// block from XApi/ApplicationDb but left DataDb/Analytics live and never added the startup
    /// replacement -- this type is that replacement, applied to all four.
    /// </para>
    ///
    /// <para>
    /// <b>Provisioning rule:</b> migration-managed contexts (<see cref="ApplicationDbContext"/>,
    /// <see cref="DataDbContext"/>, <see cref="XApiDbContext"/>) get <c>Migrate()</c> -- which creates
    /// the DB if absent, applies every migration, and records history (keeping the tenant upgradeable).
    /// The migration-less <see cref="AnalyticsDbContext"/> gets <c>EnsureCreated()</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Per-host safety:</b> each DB is provisioned only if this host's configuration actually
    /// carries its connection string (the API owns Data/XAPI/Analytics/User; the Portal owns only
    /// User) -- a missing key is skipped. Every step is wrapped so a single unreachable DB logs and is
    /// skipped rather than crashing host startup (the pre-fix behavior on an unreachable DB was an
    /// error on first use, which this does not regress).
    /// </para>
    /// </summary>
    public static class EndUserDatabaseProvisioner
    {
        /// <summary>
        /// Provision every per-tenant database whose connection string is present in
        /// <paramref name="config"/>. Call once from Program.Main after the host is built (so
        /// configuration is available) and after the Npgsql legacy-timestamp switch is set.
        /// </summary>
        public static void ProvisionEndUserDatabases(IConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            // Defensive/idempotent: the hosts set this as the first line of Program.Main (NET8 Wave 3).
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            // Migration-managed -> Migrate().
            Run<ApplicationDbContext>(config, "UserDBConnection", o => new ApplicationDbContext(o), c => c.Database.Migrate());
            // DataDb additionally seeds the node's LOCAL single-tenant identity (auth
            // severance): one NodeIdentity row, InstitutionUUID generated at provision
            // time and persisted, idempotent (provision-once -- a non-empty table is never
            // touched). Runs inside the same per-database try/catch as the migration.
            Run<DataDbContext>(config, "DataDBConnection", o => new DataDbContext(o), c =>
            {
                c.Database.Migrate();
                NodeIdentitySeeder.Seed(c, config);
                // Local-first HardwareType lookup: the standard types the
                // node's hardware-registration pages need, seeded only when the table is empty.
            });
            // XApi additionally seeds the node-local vocabulary: standard
            // verbs + default Version, idempotent, so a fresh node resolves statements with no
            // central configured. Runs inside the same per-database try/catch as the migration.
            Run<XApiDbContext>(config, "XAPIDBConnection", o => new XApiDbContext(o), c =>
            {
                c.Database.Migrate();
                XApiVocabularySeeder.Seed(c);
            });
            // Migration-less -> EnsureCreated().
            Run<AnalyticsDbContext>(config, "AnalyticsDBConnection", o => new AnalyticsDbContext(o), c => c.Database.EnsureCreated());
        }

        private static void Run<TContext>(
            IConfiguration config,
            string connectionKey,
            Func<DbContextOptions<TContext>, TContext> factory,
            Action<TContext> provision)
            where TContext : DbContext
        {
            string connectionString = config.GetConnectionString(connectionKey);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // This host does not own this database (e.g., the Portal has only UserDBConnection).
                return;
            }

            try
            {
                DbContextOptions<TContext> options =
                    new DbContextOptionsBuilder<TContext>().UseNpgsql(connectionString).Options;
                using TContext context = factory(options);
                provision(context);
            }
            catch (Exception ex)
            {
                // Never let provisioning crash host startup. Surface it and continue; the failing DB
                // will error on first real use, which is the pre-fix behavior (no regression).
                Console.Error.WriteLine(
                    "[EndUserDatabaseProvisioner] provisioning '" + connectionKey + "' failed: " + ex.Message);
            }
        }
    }
}
