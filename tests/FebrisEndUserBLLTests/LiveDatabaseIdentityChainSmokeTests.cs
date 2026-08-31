// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// End-to-end smoke test of the identity chain built 2026-08-21 (registration policy,
    /// invitations, activation) against REAL PostgreSQL.
    ///
    /// <para>
    /// WHY THIS EXISTS, and it is not belt-and-braces. Every other test for that work runs on the EF
    /// InMemory provider, which is not a database: it ignores unique indexes entirely, never
    /// translates a LINQ expression to SQL, and treats column types and defaults as metadata. So
    /// three of the load-bearing claims made elsewhere were, until this file, unproven:
    /// </para>
    /// <list type="bullet">
    /// <item>that <c>NodeUserInvite.TokenHash</c> is actually UNIQUE, which is what stops one link
    /// redeeming an arbitrary invitation;</item>
    /// <item>that the case-insensitive duplicate check in <c>GetActiveFor</c> TRANSLATES, rather
    /// than silently falling back to client evaluation that a real provider would refuse;</item>
    /// <item>that a <c>DateTime.UtcNow</c> value round-trips through a
    /// <c>timestamp without time zone</c> column at all.</item>
    /// </list>
    ///
    /// <para>
    /// OPT-IN, exactly like <c>LiveDatabaseMigrationVerificationTests</c>: skipped unless
    /// <c>FEBRIS_LIVE_DB_TEST=1</c> and <c>FEBRIS_LIVE_DB_ADMIN</c> are set, so a normal run, a
    /// fresh clone and CI are unaffected.
    /// </para>
    ///
    /// <para>
    /// SAFETY. Creates its own scratch databases named with <see cref="ScratchPrefix"/> plus a
    /// per-run GUID and drops ONLY those. Every create and drop passes
    /// <see cref="AssertIsScratchDatabase"/> first, which throws on any name without the prefix. No
    /// pre-existing database is opened, read, altered or dropped.
    /// </para>
    /// </summary>
    public class LiveDatabaseIdentityChainSmokeTests
    {
        /// <summary>Every database this test creates or drops must start with this.</summary>
        private const string ScratchPrefix = "febris_idsmoke_";

        private readonly ITestOutputHelper _output;

        public LiveDatabaseIdentityChainSmokeTests(ITestOutputHelper output)
        {
            _output = output;

            // The hosts set this in Program.cs before any Npgsql work. A test process is not a host,
            // so it has to do the same or a DateTime with Kind=Utc is rejected on the way into a
            // "timestamp without time zone" column. Stated rather than assumed, because it means
            // these round-trips depend on a switch that lives in host startup code, and a host that
            // ever drops it would break writes that this suite would still call green.
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        private static bool Enabled =>
            Environment.GetEnvironmentVariable("FEBRIS_LIVE_DB_TEST") == "1" &&
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FEBRIS_LIVE_DB_ADMIN"));

        private const string SkipReason =
            "Set FEBRIS_LIVE_DB_TEST=1 and FEBRIS_LIVE_DB_ADMIN to run the live-database checks.";

        private static string AdminConnectionString =>
            Environment.GetEnvironmentVariable("FEBRIS_LIVE_DB_ADMIN");

        /// <summary>Hard guard: a non-scratch name throws instead of touching the server.</summary>
        private static void AssertIsScratchDatabase(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !name.StartsWith(ScratchPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "REFUSING to touch database '" + name + "': only names prefixed '" + ScratchPrefix + "' may be created or dropped.");
            }
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    throw new InvalidOperationException("REFUSING: scratch database name has an unexpected character: " + name);
                }
            }
        }

        private static string ScratchName(string suffix)
        {
            return ScratchPrefix + suffix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static string ConnectionTo(string database)
        {
            return new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = database }.ConnectionString;
        }

        private static async Task CreateScratchAsync(string name)
        {
            AssertIsScratchDatabase(name);
            using NpgsqlConnection admin = new NpgsqlConnection(AdminConnectionString);
            await admin.OpenAsync();
            using NpgsqlCommand cmd = admin.CreateCommand();
            cmd.CommandText = "CREATE DATABASE \"" + name + "\"";
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task DropScratchAsync(string name)
        {
            AssertIsScratchDatabase(name);
            using NpgsqlConnection admin = new NpgsqlConnection(AdminConnectionString);
            await admin.OpenAsync();
            using NpgsqlCommand cmd = admin.CreateCommand();
            cmd.CommandText = "DROP DATABASE IF EXISTS \"" + name + "\" WITH (FORCE)";
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<string> ScalarAsync(string connectionString, string sql)
        {
            using NpgsqlConnection conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            using NpgsqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            object value = await cmd.ExecuteScalarAsync();
            return value == null || value == DBNull.Value ? null : value.ToString();
        }

        private static DataDbContext Context(string connectionString)
        {
            return new DataDbContext(
                new DbContextOptionsBuilder<DataDbContext>().UseNpgsql(connectionString).Options);
        }

        /// <summary>Provision a scratch DataDb from the migration chain alone.</summary>
        private async Task<string> ProvisionAsync(string suffix)
        {
            string db = ScratchName(suffix);
            await CreateScratchAsync(db);
            string cs = ConnectionTo(db);

            // Applying the CHAIN to an empty database is itself an assertion: a malformed migration
            // or hand-edited Designer throws here rather than at somebody's deploy.
            using (DataDbContext ctx = Context(cs))
            {
                await ctx.Database.MigrateAsync();
            }
            _output.WriteLine("provisioned scratch database " + db);
            return db;
        }

        // ---- 1. The schema the two new migrations describe actually exists -----------------------

        [SkippableFact]
        public async Task BothNewTables_AreCreatedByTheMigrationChain_WithTheirDefaults()
        {
            Skip.IfNot(Enabled, SkipReason);

            string db = await ProvisionAsync("schema");
            try
            {
                string cs = ConnectionTo(db);

                foreach (string table in new[] { "NodeRegistrationConfig", "NodeUserInvite" })
                {
                    // ::text is required, not tidiness: to_regclass returns the regclass OID type and
                    // Npgsql refuses to read that as object ("Reading as 'System.Object' is not
                    // supported for fields having DataTypeName 'regclass'").
                    string exists = await ScalarAsync(cs,
                        "select to_regclass('public.\"" + table + "\"')::text");
                    exists.Should().NotBeNullOrEmpty(
                        table + " must exist -- ParentLinkedStudentTable is the precedent for a migration "
                        + "that was believed to create a table and did not");

                    string uuidDefault = await ScalarAsync(cs,
                        "select column_default from information_schema.columns where table_name='" + table + "' and column_name='UUID'");
                    uuidDefault.Should().NotBeNullOrEmpty();
                    uuidDefault.ToLowerInvariant().Should().Contain("uuid_generate_v4");

                    string stampDefault = await ScalarAsync(cs,
                        "select column_default from information_schema.columns where table_name='" + table + "' and column_name='TimeStamp'");
                    stampDefault.Should().NotBeNullOrEmpty();
                    stampDefault.ToUpperInvariant().Should().Contain("CURRENT_TIMESTAMP");
                }

                // The columns the resolver and the invite flow actually read. A rename that the
                // model and the migration agreed on but the code did not would surface here.
                foreach (string column in new[] { "Mode", "AllowedEmailDomains", "RequireAdminApproval", "AutoProvisionJit", "OpenUntilUtc", "UpdatedAt", "UpdatedByEmail" })
                {
                    (await ScalarAsync(cs,
                        "select column_name from information_schema.columns where table_name='NodeRegistrationConfig' and column_name='" + column + "'"))
                        .Should().Be(column);
                }
                // CohortUUID is the optional cohort linkage (2026-08-21). Nullable, and NOT a foreign
                // key: a cohort deleted between issue and acceptance must not make an invitation
                // unredeemable, so the linkage is skipped rather than the account refused.
                foreach (string column in new[] { "Email", "TokenHash", "Role", "FirstName", "LastName", "IssuedByUserId", "IssuedByEmail", "ExpiresAt", "ConsumedAt", "ConsumedByUserId", "RevokedAt", "RevokedByEmail", "CohortUUID" })
                {
                    (await ScalarAsync(cs,
                        "select column_name from information_schema.columns where table_name='NodeUserInvite' and column_name='" + column + "'"))
                        .Should().Be(column);
                }
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        [SkippableFact]
        public async Task TheTokenHashIndex_IsUnique_AndPostgresEnforcesIt()
        {
            Skip.IfNot(Enabled, SkipReason);

            // THE CLAIM INMEMORY CANNOT TEST. It ignores unique indexes completely, so every
            // assertion elsewhere that "the token hash is unique" was a statement about the model,
            // not about the database. Redemption looks an invitation up BY that hash, so a duplicate
            // would make which invitation a link redeems depend on row order.
            string db = await ProvisionAsync("uniq");
            try
            {
                string cs = ConnectionTo(db);

                string indexIsUnique = await ScalarAsync(cs,
                    "select indisunique::text from pg_index i "
                    + "join pg_class c on c.oid = i.indexrelid "
                    + "where c.relname = 'IX_NodeUserInvite_TokenHash'");
                // Postgres renders a boolean as lowercase "true", not .NET's "True".
                indexIsUnique.Should().NotBeNull("the index IX_NodeUserInvite_TokenHash must exist");
                indexIsUnique.ToLowerInvariant().Should().Be("true", "and it must be UNIQUE");

                using (DataDbContext ctx = Context(cs))
                {
                    INodeUserInviteQueries queries = new NodeUserInviteQueries(ctx);
                    await queries.Create(NewInvite("first@school.example", "COLLIDING-HASH"));
                }

                // A second row with the same hash must be refused by the DATABASE, not merely by
                // application code that could be bypassed or rewritten.
                using (DataDbContext ctx = Context(cs))
                {
                    INodeUserInviteQueries queries = new NodeUserInviteQueries(ctx);
                    Func<Task> second = async () =>
                        await queries.Create(NewInvite("second@school.example", "COLLIDING-HASH"));
                    await second.Should().ThrowAsync<DbUpdateException>(
                        "Postgres must reject the duplicate");
                }

                (await ScalarAsync(cs, "select count(*)::text from \"NodeUserInvite\"")).Should().Be("1");
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        // ---- 2. The registration policy round-trips through a real database ----------------------

        [SkippableFact]
        public async Task RegistrationPolicy_SavesAndReadsBack_ThroughRealPostgres()
        {
            Skip.IfNot(Enabled, SkipReason);

            string db = await ProvisionAsync("regcfg");
            try
            {
                string cs = ConnectionTo(db);
                DateTime openUntil = DateTime.UtcNow.AddHours(4);

                using (DataDbContext ctx = Context(cs))
                {
                    await new NodeRegistrationConfigQueries(ctx).Save(new NodeRegistrationConfig()
                    {
                        Mode = "DomainAllowlist",
                        AllowedEmailDomains = "acme.com,beta.org",
                        RequireAdminApproval = true,
                        AutoProvisionJit = true,
                        OpenUntilUtc = openUntil,
                        UpdatedByEmail = "admin@example.com"
                    });
                }

                using (DataDbContext ctx = Context(cs))
                {
                    NodeRegistrationConfig row = await new NodeRegistrationConfigQueries(ctx).Get();
                    row.Should().NotBeNull();
                    row.Mode.Should().Be("DomainAllowlist");
                    row.AllowedEmailDomains.Should().Be("acme.com,beta.org");
                    row.RequireAdminApproval.Should().BeTrue();
                    row.AutoProvisionJit.Should().BeTrue();
                    row.UpdatedByEmail.Should().Be("admin@example.com");

                    // The round-trip that InMemory cannot vouch for: a UTC DateTime into a
                    // "timestamp without time zone" column and back, to the second.
                    row.OpenUntilUtc.Should().NotBeNull();
                    row.OpenUntilUtc.Value.Should().BeCloseTo(openUntil, TimeSpan.FromSeconds(1));
                    row.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
                }

                // Single-row semantics against a real store: a second save UPDATES rather than
                // appends, which is what makes "the stored row governs" a well-defined statement.
                using (DataDbContext ctx = Context(cs))
                {
                    await new NodeRegistrationConfigQueries(ctx).Save(new NodeRegistrationConfig()
                    {
                        Mode = "AdminOnly",
                        UpdatedByEmail = "other@example.com"
                    });
                }
                (await ScalarAsync(cs, "select count(*)::text from \"NodeRegistrationConfig\"")).Should().Be("1");
                (await ScalarAsync(cs, "select \"Mode\" from \"NodeRegistrationConfig\"")).Should().Be("AdminOnly");
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        // ---- 3. The invitation lifecycle, end to end, on real SQL --------------------------------

        [SkippableFact]
        public async Task InvitationLifecycle_IssueValidateConsume_OnRealPostgres()
        {
            Skip.IfNot(Enabled, SkipReason);

            string db = await ProvisionAsync("invite");
            try
            {
                string cs = ConnectionTo(db);
                string rawToken = DeviceCredential.Generate();
                string tokenHash = DeviceCredential.Hash(rawToken);
                Guid inviteUuid;

                using (DataDbContext ctx = Context(cs))
                {
                    NodeUserInvite created = await new NodeUserInviteQueries(ctx)
                        .Create(NewInvite("learner@school.example", tokenHash));
                    inviteUuid = created.UUID;
                    inviteUuid.Should().NotBeEmpty();
                }

                // The token is NOT in the database in any readable form. Asserted against the raw
                // table rather than the mapped entity, so a column added later that happened to
                // carry it would still be caught.
                string rowDump = await ScalarAsync(cs,
                    "select coalesce(\"Email\",'') || '|' || coalesce(\"TokenHash\",'') || '|' || coalesce(\"Role\",'') "
                    + "|| '|' || coalesce(\"FirstName\",'') || '|' || coalesce(\"LastName\",'') "
                    + "|| '|' || coalesce(\"IssuedByEmail\",'') || '|' || coalesce(\"RevokedByEmail\",'') "
                    + "|| '|' || \"UUID\"::text from \"NodeUserInvite\"");
                rowDump.Should().NotContain(rawToken,
                    "the raw token must appear in NO column -- the central invite flow's defect is that its UUID IS the token");
                rowDump.Should().Contain(tokenHash);

                using (DataDbContext ctx = Context(cs))
                {
                    INodeUserInviteQueries queries = new NodeUserInviteQueries(ctx);

                    (await queries.GetByTokenHash(tokenHash)).Should().NotBeNull();
                    (await queries.GetByTokenHash(DeviceCredential.Hash("some other token"))).Should().BeNull();
                    (await queries.CountActive()).Should().Be(1);

                    // The duplicate-address check has to TRANSLATE to SQL. InMemory would evaluate
                    // this client-side and pass whatever happened; a real provider either translates
                    // it or throws.
                    (await queries.GetActiveFor("learner@school.example")).Should().HaveCount(1);
                    (await queries.GetActiveFor("LEARNER@School.Example")).Should().HaveCount(1,
                        "addresses differing only in case are the same person");
                    (await queries.GetActiveFor("someone.else@school.example")).Should().BeEmpty();
                }

                // Single use, enforced by the store rather than by a read-then-write above it.
                using (DataDbContext ctx = Context(cs))
                {
                    INodeUserInviteQueries queries = new NodeUserInviteQueries(ctx);
                    (await queries.MarkConsumed(inviteUuid, Guid.NewGuid(), DateTime.UtcNow)).Should().BeTrue();
                }
                using (DataDbContext ctx = Context(cs))
                {
                    INodeUserInviteQueries queries = new NodeUserInviteQueries(ctx);
                    (await queries.MarkConsumed(inviteUuid, Guid.NewGuid(), DateTime.UtcNow)).Should()
                        .BeFalse("a second redemption of one link must lose");
                    (await queries.MarkRevoked(inviteUuid, "admin@example.com", DateTime.UtcNow)).Should()
                        .BeFalse("cancelling a used invitation would imply the account can be taken back");
                    (await queries.CountActive()).Should().Be(0);
                }

                (await ScalarAsync(cs, "select count(*)::text from \"NodeUserInvite\" where \"ConsumedAt\" is not null"))
                    .Should().Be("1");
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        [SkippableFact]
        public async Task ExpiredInvitation_IsRefusedByTheStore_UsingRealTimestampComparison()
        {
            Skip.IfNot(Enabled, SkipReason);

            // The expiry guard compares two timestamp columns in SQL. On InMemory that comparison is
            // ordinary C#; here it is Postgres, which is where it will run.
            string db = await ProvisionAsync("expiry");
            try
            {
                string cs = ConnectionTo(db);
                Guid uuid;

                using (DataDbContext ctx = Context(cs))
                {
                    NodeUserInvite expired = NewInvite("late@school.example", DeviceCredential.Hash("late-token"));
                    expired.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
                    uuid = (await new NodeUserInviteQueries(ctx).Create(expired)).UUID;
                }

                using (DataDbContext ctx = Context(cs))
                {
                    INodeUserInviteQueries queries = new NodeUserInviteQueries(ctx);
                    (await queries.MarkConsumed(uuid, Guid.NewGuid(), DateTime.UtcNow)).Should().BeFalse();
                    (await queries.CountActive()).Should().Be(0);
                    (await queries.GetActiveFor("late@school.example")).Should().BeEmpty(
                        "an expired invitation must not block a fresh one for the same person");

                    // Still findable by token, so the accept page can say EXPIRED rather than a flat
                    // "not valid" -- the difference between a useful message and a dead end.
                    (await queries.GetByTokenHash(DeviceCredential.Hash("late-token"))).Should().NotBeNull();
                }
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        [SkippableFact]
        public async Task RevokedInvitation_FreesTheAddress_ForAFreshInvitation()
        {
            Skip.IfNot(Enabled, SkipReason);

            // The documented way to re-send: revoke, then issue again. If revocation did not free
            // the address the duplicate guard would make that impossible, which is a deadlock a
            // real-SQL run is worth proving against.
            string db = await ProvisionAsync("revoke");
            try
            {
                string cs = ConnectionTo(db);
                Guid uuid;

                using (DataDbContext ctx = Context(cs))
                {
                    uuid = (await new NodeUserInviteQueries(ctx)
                        .Create(NewInvite("learner@school.example", DeviceCredential.Hash("first")))).UUID;
                }
                using (DataDbContext ctx = Context(cs))
                {
                    (await new NodeUserInviteQueries(ctx).MarkRevoked(uuid, "admin@example.com", DateTime.UtcNow))
                        .Should().BeTrue();
                }
                using (DataDbContext ctx = Context(cs))
                {
                    INodeUserInviteQueries queries = new NodeUserInviteQueries(ctx);
                    (await queries.GetActiveFor("learner@school.example")).Should().BeEmpty();
                    await queries.Create(NewInvite("learner@school.example", DeviceCredential.Hash("second")));
                    (await queries.GetActiveFor("learner@school.example")).Should().HaveCount(1);
                    (await queries.CountActive()).Should().Be(1);
                }

                (await ScalarAsync(cs, "select \"RevokedByEmail\" from \"NodeUserInvite\" where \"UUID\" = '" + uuid + "'"))
                    .Should().Be("admin@example.com");
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        // ---- 4. The resolver reads a REAL database -----------------------------------------------

        [SkippableFact]
        public async Task TheRegistrationResolver_ReadsAStoredRow_FromRealPostgres()
        {
            Skip.IfNot(Enabled, SkipReason);

            // The last link in the chain that InMemory could not vouch for. Everything else about
            // the resolver is pinned with a fake settings logic, which proves the PRECEDENCE rules
            // but says nothing about whether the row can be read out of an actual database through
            // the actual query class and DbContext. This wires the real ones.
            string db = await ProvisionAsync("resolver");
            try
            {
                string cs = ConnectionTo(db);

                // Configuration says AdminOnly, which is the shipped default.
                IdentityPolicyOptionsForTest configured = new IdentityPolicyOptionsForTest();

                // With NOTHING stored, configuration governs -- the back-compat guarantee.
                Febris.UserNode.Portal.IdentityPolicy.NodeRegistrationPolicyResolver closed =
                    ResolverOver(cs, configured.AdminOnly);
                closed.Mode.Should().Be(Febris.UserNode.Portal.IdentityPolicy.RegistrationMode.AdminOnly);
                closed.SelfRegistrationEnabled.Should().BeFalse();

                // Now store Open, exactly as the admin page's Save does.
                using (DataDbContext ctx = Context(cs))
                {
                    await new NodeRegistrationConfigQueries(ctx).Save(new NodeRegistrationConfig()
                    {
                        Mode = "Open",
                        UpdatedByEmail = "admin@example.com"
                    });
                }

                // A NEW resolver, because the old one holds a cached snapshot. This is the whole
                // feature in one assertion: a row written to the database changes what the register
                // page is allowed to do, with no restart and no config edit.
                Febris.UserNode.Portal.IdentityPolicy.NodeRegistrationPolicyResolver opened =
                    ResolverOver(cs, configured.AdminOnly);
                opened.Mode.Should().Be(Febris.UserNode.Portal.IdentityPolicy.RegistrationMode.Open);
                opened.SelfRegistrationEnabled.Should().BeTrue();
                opened.IsEmailAllowed("anyone@anywhere.example").Should().BeTrue();

                // And the configured value is still visible as the reset target.
                opened.ConfiguredRegistration.Mode.Should()
                    .Be(Febris.UserNode.Portal.IdentityPolicy.RegistrationMode.AdminOnly);
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        /// <summary>Small holder so the configured-options shape reads clearly at the call site.</summary>
        private sealed class IdentityPolicyOptionsForTest
        {
            public Febris.UserNode.Portal.IdentityPolicy.IdentityPolicyOptions AdminOnly =>
                new Febris.UserNode.Portal.IdentityPolicy.IdentityPolicyOptions
                {
                    Registration = new Febris.UserNode.Portal.IdentityPolicy.RegistrationOptions
                    {
                        Mode = Febris.UserNode.Portal.IdentityPolicy.RegistrationMode.AdminOnly
                    }
                };
        }

        /// <summary>The real resolver over the real logic over the real query class over real
        /// Postgres. Only the configuration is supplied by the test.</summary>
        private static Febris.UserNode.Portal.IdentityPolicy.NodeRegistrationPolicyResolver ResolverOver(
            string connectionString,
            Febris.UserNode.Portal.IdentityPolicy.IdentityPolicyOptions configured)
        {
            Microsoft.Extensions.DependencyInjection.ServiceCollection services =
                new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped(
                services, _ => Context(connectionString));
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<
                INodeUserInviteQueries, NodeUserInviteQueries>(services);
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<
                INodeRegistrationConfigQueries, NodeRegistrationConfigQueries>(services);
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<
                Febris.UserNode.LogicLayer.Logic.IdentityLogic.INodeRegistrationSettingsLogic,
                Febris.UserNode.LogicLayer.Logic.IdentityLogic.NodeRegistrationSettingsLogic>(services);

            System.IServiceProvider provider =
                Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions
                    .BuildServiceProvider(services);

            return new Febris.UserNode.Portal.IdentityPolicy.NodeRegistrationPolicyResolver(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(provider),
                Microsoft.Extensions.Options.Options.Create(configured));
        }

        // ---- 5. This suite cleans up after itself ------------------------------------------------

        [SkippableFact]
        public async Task NoScratchDatabases_AreLeftBehindOnTheServer()
        {
            Skip.IfNot(Enabled, SkipReason);

            // Written because the first attempt to check this from the shell produced a VACUOUS
            // "none": PowerShell 5.1 could not load the net8.0 Npgsql assembly, the result list was
            // never populated, and the empty list read as a clean bill of health. A check that
            // cannot fail is not a check.
            //
            // Safe to assert inside the suite that creates them: xunit does not parallelize tests
            // within a class, and every other test here drops its database in a finally before
            // returning. Sibling classes use a different prefix, so their scratch databases are not
            // counted here.
            string count = await ScalarAsync(AdminConnectionString,
                "select count(*)::text from pg_database where datname like '" + ScratchPrefix + "%'");

            _output.WriteLine("scratch databases matching " + ScratchPrefix + "%: " + count);
            count.Should().Be("0",
                "every scratch database is dropped in a finally -- a leftover means a run was killed "
                + "mid-test, and it is somebody's disk");
        }

        private static NodeUserInvite NewInvite(string email, string tokenHash)
        {
            return new NodeUserInvite()
            {
                Email = email,
                TokenHash = tokenHash,
                Role = "User",
                FirstName = "Ada",
                LastName = "Lovelace",
                IssuedByUserId = Guid.NewGuid(),
                IssuedByEmail = "admin@example.com",
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };
        }
    }
}
