// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Febris.UserNode.DataAccessLayer.DataContext;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// The check the InMemory suite structurally CANNOT do: provision a database from the migration
    /// chain alone and look at the real schema.
    ///
    /// <para>
    /// Audit C-01 and C-04 were both defects where the EF model, the model snapshot and the actual
    /// database disagreed. Every existing test runs on the EF InMemory provider, which treats
    /// relational defaults and DDL as metadata only -- so it cannot see a missing column default or
    /// a table that no migration creates, which is precisely why neither defect was caught. Their
    /// fixes are asserted elsewhere against the migrations' own operations; this asserts the
    /// migrations actually APPLY and produce the schema those operations describe.
    /// </para>
    ///
    /// <para>
    /// OPT-IN. Skipped unless <c>FEBRIS_LIVE_DB_TEST=1</c> and <c>FEBRIS_LIVE_DB_ADMIN</c> (a
    /// connection string to the maintenance database) are set, so a normal <c>dotnet test</c> run,
    /// a fresh clone and CI are all unaffected. Run it deliberately, against a dev server.
    /// </para>
    ///
    /// <para>
    /// SAFETY. This creates its own scratch databases named with <see cref="ScratchPrefix"/> and a
    /// per-run GUID, and drops ONLY those. <see cref="AssertIsScratchDatabase"/> gates every drop
    /// and every create; a name that does not carry the prefix throws rather than executes. It
    /// never opens, reads, alters or drops any pre-existing database -- the four live
    /// <c>Febris.EndUser.*</c> databases on the dev server are untouched by construction.
    /// </para>
    /// </summary>
    public class LiveDatabaseMigrationVerificationTests
    {
        /// <summary>Every database this test creates or drops must start with this. Nothing else is touchable.</summary>
        private const string ScratchPrefix = "febris_migcheck_";

        private readonly ITestOutputHelper _output;

        public LiveDatabaseMigrationVerificationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static bool Enabled =>
            Environment.GetEnvironmentVariable("FEBRIS_LIVE_DB_TEST") == "1" &&
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FEBRIS_LIVE_DB_ADMIN"));

        private static string AdminConnectionString =>
            Environment.GetEnvironmentVariable("FEBRIS_LIVE_DB_ADMIN");

        /// <summary>
        /// Hard guard. Any create/drop path calls this first; a non-scratch name throws instead of
        /// touching the server. Deliberately paranoid: the cost of being wrong here is someone
        /// else's data.
        /// </summary>
        private static void AssertIsScratchDatabase(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !name.StartsWith(ScratchPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "REFUSING to touch database '" + name + "': only names prefixed '" + ScratchPrefix + "' may be created or dropped.");
            }
            // A quoted identifier would let a crafted name break out of the DDL statement below.
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
            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder(AdminConnectionString)
            {
                Database = database
            };
            return builder.ConnectionString;
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

        private static async Task<List<string>> ListAsync(string connectionString, string sql)
        {
            List<string> rows = new List<string>();
            using NpgsqlConnection conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            using NpgsqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(reader.IsDBNull(0) ? null : reader.GetString(0));
            }
            return rows;
        }

        [SkippableFact]
        public async Task XApiDb_ProvisionedFromMigrationsOnly_GivesLocalStatementItsColumnDefaults()
        {
            Skip.IfNot(Enabled,
                "Set FEBRIS_LIVE_DB_TEST=1 and FEBRIS_LIVE_DB_ADMIN to run the live-database checks.");

            string db = ScratchName("xapi");
            await CreateScratchAsync(db);
            try
            {
                string cs = ConnectionTo(db);
                DbContextOptions<XApiDbContext> options = new DbContextOptionsBuilder<XApiDbContext>()
                    .UseNpgsql(cs).Options;

                // The whole point: apply the migration CHAIN to an empty database. If either new
                // hand-written migration or its hand-generated Designer is malformed, this throws.
                using (XApiDbContext ctx = new XApiDbContext(options))
                {
                    await ctx.Database.MigrateAsync();
                }

                // C-01: both columns must now carry a default. Before the fix they were NOT NULL
                // with an empty column_default, while the model declared them store-generated --
                // so EF omitted them from the INSERT and the insert violated NOT NULL.
                string stored = await ScalarAsync(cs,
                    "select column_default from information_schema.columns where table_name='LocalStatement' and column_name='Stored'");
                string timestamp = await ScalarAsync(cs,
                    "select column_default from information_schema.columns where table_name='LocalStatement' and column_name='Timestamp'");

                _output.WriteLine("LocalStatement.Stored    column_default = " + (stored ?? "<null>"));
                _output.WriteLine("LocalStatement.Timestamp column_default = " + (timestamp ?? "<null>"));

                stored.Should().NotBeNullOrEmpty("audit C-01: Stored is never assigned in the BLL, so without a default every statement insert violates NOT NULL");
                timestamp.Should().NotBeNullOrEmpty();
                stored.ToUpperInvariant().Should().Contain("CURRENT_TIMESTAMP");
                timestamp.ToUpperInvariant().Should().Contain("CURRENT_TIMESTAMP");

                // And the sibling default that was already correct, as a control -- proving the
                // query and the provisioning are sound rather than trivially passing.
                string uuid = await ScalarAsync(cs,
                    "select column_default from information_schema.columns where table_name='LocalStatement' and column_name='UUID'");
                uuid.Should().NotBeNullOrEmpty("this one was always right; if it is null the provisioning itself failed");
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        [SkippableFact]
        public async Task XApiDb_StatementInsert_WithNoStoredOrTimestamp_Succeeds()
        {
            Skip.IfNot(Enabled,
                "Set FEBRIS_LIVE_DB_TEST=1 and FEBRIS_LIVE_DB_ADMIN to run the live-database checks.");

            string db = ScratchName("xapiins");
            await CreateScratchAsync(db);
            try
            {
                string cs = ConnectionTo(db);
                using (XApiDbContext ctx = new XApiDbContext(new DbContextOptionsBuilder<XApiDbContext>().UseNpgsql(cs).Options))
                {
                    await ctx.Database.MigrateAsync();
                }

                // LocalStatement.ActorId carries a real FK, so the statement needs an Actor to
                // point at. (A first attempt inserted ActorId 0 and died on the FK before it ever
                // reached the column under test -- a test defect, not a product one.)
                string actorId = await ScalarAsync(cs, "insert into \"Actor\" default values returning \"Id\"");
                actorId.Should().NotBeNullOrEmpty();

                // The audit's stated C-01 verification, reduced to the part that does not need the
                // whole BLL graph: write the row the way EF writes it when Stored is unset -- that
                // is, omitting the column entirely, which is what ValueGeneratedOnAdd makes EF do.
                // Before the fix this failed with a not-null violation on a migrations-only
                // database, and no learning record could be stored on a fresh node at all.
                string inserted = await ScalarAsync(cs,
                    "insert into \"LocalStatement\" (\"ActorId\",\"VerbId\",\"VerbUUID\",\"ObjectId\",\"ObjectUUID\",\"VersionId\",\"VersionUUID\") " +
                    "values (" + actorId + ",0,'00000000-0000-0000-0000-000000000000',0,'00000000-0000-0000-0000-000000000000',0,'00000000-0000-0000-0000-000000000000') " +
                    "returning \"Stored\"");

                _output.WriteLine("inserted row Stored = " + (inserted ?? "<null>"));
                inserted.Should().NotBeNullOrEmpty("the database must supply Stored, because nothing in the BLL ever assigns it");

                // Timestamp is assigned by StatementLogic now, but the column default is the
                // backstop for a row that genuinely carries none -- assert it landed too.
                string ts = await ScalarAsync(cs, "select \"Timestamp\" from \"LocalStatement\" limit 1");
                _output.WriteLine("inserted row Timestamp = " + (ts ?? "<null>"));
                ts.Should().NotBeNullOrEmpty();
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        [SkippableFact]
        public async Task DataDb_ProvisionedFromMigrationsOnly_CreatesParentLinkedStudent()
        {
            Skip.IfNot(Enabled,
                "Set FEBRIS_LIVE_DB_TEST=1 and FEBRIS_LIVE_DB_ADMIN to run the live-database checks.");

            string db = ScratchName("data");
            await CreateScratchAsync(db);
            try
            {
                string cs = ConnectionTo(db);
                using (DataDbContext ctx = new DataDbContext(new DbContextOptionsBuilder<DataDbContext>().UseNpgsql(cs).Options))
                {
                    await ctx.Database.MigrateAsync();
                }

                // C-04: the table every layer above it assumed existed.
                List<string> columns = await ListAsync(cs,
                    "select column_name from information_schema.columns where table_name='ParentLinkedStudent' order by column_name");
                _output.WriteLine("ParentLinkedStudent columns: " + string.Join(", ", columns));

                columns.Should().BeEquivalentTo(new[]
                {
                    "Id", "LastUpdateTimeStamp", "ParentUserId", "StudentActorId", "StudentUserId", "TimeStamp", "UUID"
                }, "the table must exist and match the entity");

                List<string> indexes = await ListAsync(cs,
                    "select indexname from pg_indexes where tablename='ParentLinkedStudent' order by indexname");
                _output.WriteLine("ParentLinkedStudent indexes: " + string.Join(", ", indexes));
                indexes.Should().Contain("IX_ParentLinkedStudent_ParentUserId");
                indexes.Should().Contain("IX_ParentLinkedStudent_StudentActorId");
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        [SkippableFact]
        public async Task NoScratchDatabasesAreLeftBehind_AndTheLiveOnesAreUntouched()
        {
            Skip.IfNot(Enabled,
                "Set FEBRIS_LIVE_DB_TEST=1 and FEBRIS_LIVE_DB_ADMIN to run the live-database checks.");

            // Hygiene on a SHARED dev server. Every scratch database is dropped in a finally, but a
            // crashed run could leak one, and a leaked database is somebody else's problem later.
            // Run this last (or on its own) to confirm the server is as it was found.
            List<string> all = await ListAsync(AdminConnectionString,
                "select datname from pg_database where not datistemplate order by datname");
            _output.WriteLine("databases on server: " + string.Join(", ", all));

            all.Should().NotContain(n => n != null && n.StartsWith(ScratchPrefix, StringComparison.Ordinal),
                "every scratch database this fixture creates must be dropped again");

            // The four the node actually uses must still be present -- this fixture never opens,
            // alters or drops them, and this is the assertion that says so out loud.
            all.Should().Contain("Febris.EndUser.DataDB");
            all.Should().Contain("Febris.EndUser.xApiDB");
            all.Should().Contain("Febris.EndUser.UserDB");
            all.Should().Contain("Febris.EndUser.Analytics");
        }

        [SkippableFact]
        public async Task DataDb_ProvisionedFromMigrationsOnly_GivesHardwareItsKindColumn()
        {
            Skip.IfNot(Enabled,
                "Set FEBRIS_LIVE_DB_TEST=1 and FEBRIS_LIVE_DB_ADMIN to run the live-database checks.");

            // The InMemory provider cannot see a column type, a NOT NULL, or a default, which is
            // exactly the blind spot that let C-01 ship. This asserts against real DDL.
            string db = ScratchName("hwkind");
            await CreateScratchAsync(db);
            try
            {
                string cs = ConnectionTo(db);
                using (DataDbContext ctx = new DataDbContext(new DbContextOptionsBuilder<DataDbContext>().UseNpgsql(cs).Options))
                {
                    await ctx.Database.MigrateAsync();
                }

                string dataType = await ScalarAsync(cs,
                    "SELECT data_type FROM information_schema.columns " +
                    "WHERE table_schema='public' AND table_name='Hardware' AND column_name='HardwareKind'");
                dataType.Should().Be("integer", "HardwareKind is persisted as the enum's underlying int");

                string nullable = await ScalarAsync(cs,
                    "SELECT is_nullable FROM information_schema.columns " +
                    "WHERE table_schema='public' AND table_name='Hardware' AND column_name='HardwareKind'");
                nullable.Should().Be("NO");

                string columnDefault = await ScalarAsync(cs,
                    "SELECT column_default FROM information_schema.columns " +
                    "WHERE table_schema='public' AND table_name='Hardware' AND column_name='HardwareKind'");
                columnDefault.Should().StartWith("0",
                    "an existing device row must land on HardwareKind.Unknown, never on a real kind");

                // RETAINED by owner instruction, and now genuinely inert: nothing writes them and
                // nothing reads them. Kept in case the hub returns. If a later change drops them,
                // this fails and the reason gets re-read rather than rediscovered.
                string carrierId = await ScalarAsync(cs,
                    "SELECT COUNT(*) FROM information_schema.columns " +
                    "WHERE table_schema='public' AND table_name='Hardware' AND column_name='HardwareTypeId'");
                carrierId.Should().Be("1", "HardwareTypeId is retained but inert");
                string carrierUuid = await ScalarAsync(cs,
                    "SELECT COUNT(*) FROM information_schema.columns " +
                    "WHERE table_schema='public' AND table_name='Hardware' AND column_name='HardwareTypeUUID'");
                carrierUuid.Should().Be("1", "HardwareTypeUUID is retained but inert");
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        [SkippableFact]
        public async Task DataDb_ProvisionedFromMigrationsOnly_HasNoHardware1_AndLinksTheRealDeviceTable()
        {
            Skip.IfNot(Enabled,
                "Set FEBRIS_LIVE_DB_TEST=1 and FEBRIS_LIVE_DB_ADMIN to run the live-database checks.");

            // Hardware1 was an EF Core 3.1 auto-disambiguation artifact that no migration ever
            // converged, and the two link foreign keys pointed at it. It is empty and has no
            // writer, so linking a device to a curriculum or a location failed with 23503 on every
            // node. This asserts against real DDL because that is the only place the defect was
            // visible: the InMemory provider has no foreign keys and the solution built 0 errors
            // throughout.
            string db = ScratchName("hw1gone");
            await CreateScratchAsync(db);
            try
            {
                string cs = ConnectionTo(db);
                using (DataDbContext ctx = new DataDbContext(new DbContextOptionsBuilder<DataDbContext>().UseNpgsql(cs).Options))
                {
                    await ctx.Database.MigrateAsync();
                }

                string hardware1 = await ScalarAsync(cs,
                    "SELECT COUNT(*) FROM information_schema.tables " +
                    "WHERE table_schema='public' AND table_name='Hardware1'");
                hardware1.Should().Be("0", "Hardware1 must not survive the migration chain");

                List<string> fks = await ListAsync(cs,
                    "SELECT src.relname || '.' || att.attname || ' -> ' || tgt.relname " +
                    "FROM pg_constraint con " +
                    "JOIN pg_class src ON src.oid = con.conrelid " +
                    "JOIN pg_class tgt ON tgt.oid = con.confrelid " +
                    "JOIN pg_attribute att ON att.attrelid = con.conrelid AND att.attnum = con.conkey[1] " +
                    "WHERE con.contype='f' AND att.attname='HardwareId' ORDER BY 1");

                fks.Should().Contain("HardwareLinkedCurriculum.HardwareId -> Hardware",
                    "linking a device to a curriculum must reference the table that holds devices");
                fks.Should().Contain("LocationLinkedHardware.HardwareId -> Hardware",
                    "linking a device to a location must reference the table that holds devices");
                fks.Should().NotContain(f => f.EndsWith("-> Hardware1"),
                    "no foreign key may point at the orphan table");

                // The node has NO HardwareType store at all. That vocabulary belongs to the hub
                // (owner ruling 2026-08-07) and the node's copy was seeded locally, displayed, and
                // never read to decide anything. Its removal is the point, so it is asserted rather
                // than assumed.
                string typeTable = await ScalarAsync(cs,
                    "SELECT COUNT(*) FROM information_schema.tables " +
                    "WHERE table_schema='public' AND table_name='HardwareType'");
                typeTable.Should().Be("0",
                    "the hardware type vocabulary lives on the hub, not in the node's database");
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        [SkippableFact]
        public async Task BothContexts_HaveNoPendingModelChanges_AfterMigrating()
        {
            Skip.IfNot(Enabled,
                "Set FEBRIS_LIVE_DB_TEST=1 and FEBRIS_LIVE_DB_ADMIN to run the live-database checks.");

            // Catches the defect class BEHIND C-01 and C-04 rather than the two instances: a model
            // that disagrees with its migration chain. Both new migrations were hand-written and
            // their Designers hand-generated, and the DataDb snapshot was hand-edited, so this is
            // the assertion that those edits were complete.
            string xapi = ScratchName("pendingx");
            string data = ScratchName("pendingd");
            string appdb = ScratchName("pendinga");
            await CreateScratchAsync(xapi);
            await CreateScratchAsync(data);
            await CreateScratchAsync(appdb);
            try
            {
                // GetPendingMigrationsAsync AFTER MigrateAsync is empty BY DEFINITION, so the
                // original form of this test could not fail for the reason its name gives. It
                // proved only that the chain applies without throwing, which is worth something
                // but is not the drift check the name and the class doc promise.
                //
                // HasPendingModelChanges is the EF Core 8 API that compares the MODEL against the
                // last migration's snapshot, which is the thing that actually goes wrong: someone
                // edits an entity and does not scaffold, and the next migration silently carries
                // their change plus everyone else's. That is exactly how the Analytics TimeStamp
                // index drifted.
                using (XApiDbContext ctx = new XApiDbContext(new DbContextOptionsBuilder<XApiDbContext>().UseNpgsql(ConnectionTo(xapi)).Options))
                {
                    await ctx.Database.MigrateAsync();
                    IEnumerable<string> pending = await ctx.Database.GetPendingMigrationsAsync();
                    pending.Should().BeEmpty("every XApiDb migration must apply");
                    ctx.Database.HasPendingModelChanges().Should().BeFalse(
                        "the XApiDb model has edits that were never scaffolded into a migration");
                }
                using (DataDbContext ctx = new DataDbContext(new DbContextOptionsBuilder<DataDbContext>().UseNpgsql(ConnectionTo(data)).Options))
                {
                    await ctx.Database.MigrateAsync();
                    IEnumerable<string> pending = await ctx.Database.GetPendingMigrationsAsync();
                    pending.Should().BeEmpty("every DataDb migration must apply");
                    ctx.Database.HasPendingModelChanges().Should().BeFalse(
                        "the DataDb model has edits that were never scaffolded into a migration");
                }

                // ApplicationDb was absent from this check entirely, so its model could drift
                // without anything noticing. It is migration-managed like the two above.
                using (ApplicationDbContext ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(ConnectionTo(appdb)).Options))
                {
                    await ctx.Database.MigrateAsync();
                    IEnumerable<string> pending = await ctx.Database.GetPendingMigrationsAsync();
                    pending.Should().BeEmpty("every ApplicationDb migration must apply");
                    ctx.Database.HasPendingModelChanges().Should().BeFalse(
                        "the ApplicationDb model has edits that were never scaffolded into a migration");
                }
            }
            finally
            {
                await DropScratchAsync(xapi);
                await DropScratchAsync(data);
                await DropScratchAsync(appdb);
            }
        }
    }
}
