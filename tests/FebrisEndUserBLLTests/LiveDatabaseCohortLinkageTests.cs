// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
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
    /// The invitation cohort-linkage WRITE, against real PostgreSQL.
    ///
    /// <para>
    /// WHY THIS EXISTS. The identity-chain work shipped with a recorded carry: the linkage had a
    /// live-database test for the COLUMN'S EXISTENCE only, never for the write itself. Everything
    /// else about it ran on EF InMemory, which is not a database -- it enforces no foreign keys,
    /// translates no LINQ to SQL, and treats column types and defaults as metadata. The specific
    /// thing InMemory cannot see here is the shape of the row: <c>CohortMember</c> carries BOTH a
    /// plain <c>CohortUUID</c> Guid column AND a NULLABLE shadow foreign key <c>CohortId</c>, and
    /// only the second is a real relational link. A write that set the UUID and left the FK null
    /// would look perfectly linked to every InMemory assertion in the suite while being, to
    /// Postgres, a membership attached to no cohort at all. That is the assertion this file adds.
    /// </para>
    ///
    /// <para>
    /// SAFETY. Creates its own scratch databases named with <see cref="ScratchPrefix"/> plus a
    /// random suffix, and every create or drop calls <see cref="AssertIsScratchDatabase"/> first,
    /// which throws on any name without the prefix. No pre-existing database is opened, read,
    /// altered or dropped. Opt-in and skipped by default, so CI and a fresh clone are unaffected.
    /// Its own prefix rather than a shared one, matching the precedent of the two live-database
    /// suites that already exist.
    /// </para>
    /// </summary>
    public class LiveDatabaseCohortLinkageTests
    {
        private const string ScratchPrefix = "febris_cohortlink_";

        private readonly ITestOutputHelper _output;

        public LiveDatabaseCohortLinkageTests(ITestOutputHelper output)
        {
            _output = output;

            // The hosts set this in Program.cs before any Npgsql work, and a test process is not a
            // host. Without it a DateTime with Kind=Utc is rejected on the way into a "timestamp
            // without time zone" column, which is what BaseModel's stamps are.
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
            using (DataDbContext ctx = Context(ConnectionTo(db)))
            {
                await ctx.Database.MigrateAsync();
            }
            _output.WriteLine("provisioned scratch database " + db);
            return db;
        }

        /// <summary>Seed one cohort and return its surrogate key and UUID.</summary>
        private static async Task<(long Id, Guid Uuid)> SeedCohortAsync(string cs, bool archived = false)
        {
            using DataDbContext ctx = Context(cs);
            Cohort cohort = new Cohort
            {
                UUID = Guid.NewGuid(),
                Name = archived ? "Retired Term" : "Spring Term",
                Archive = archived,
                TimeStamp = DateTime.Now,
                LastUpdateTimeStamp = DateTime.Now
            };
            ctx.Cohort.Add(cohort);
            await ctx.SaveChangesAsync();
            return (cohort.Id, cohort.UUID);
        }

        // ---- The write itself --------------------------------------------------------------------

        [SkippableFact]
        public async Task TheLinkage_PopulatesTheRealForeignKey_NotOnlyTheUuid()
        {
            Skip.IfNot(Enabled, SkipReason);
            string db = await ProvisionAsync("fk");
            try
            {
                string cs = ConnectionTo(db);
                (long cohortId, Guid cohortUuid) = await SeedCohortAsync(cs);
                Guid acceptedUser = Guid.NewGuid();

                using (DataDbContext ctx = Context(cs))
                {
                    CohortMember member = await new CohortMemberQueries(ctx).CreateForCohort(acceptedUser, cohortUuid);
                    member.Should().NotBeNull("the cohort exists, so the invitation's linkage must be made");
                }

                // Read the row back through RAW SQL rather than EF, so the assertion is about what
                // Postgres actually stored and not about what the tracked entity happens to hold.
                string storedFk = await ScalarAsync(cs,
                    "SELECT \"CohortId\" FROM \"CohortMember\" WHERE \"UserId\" = '" + acceptedUser + "'");
                string storedUuid = await ScalarAsync(cs,
                    "SELECT \"CohortUUID\" FROM \"CohortMember\" WHERE \"UserId\" = '" + acceptedUser + "'");

                // THE POINT OF THIS FILE. CohortId is the real, nullable foreign key; CohortUUID is
                // a plain Guid column with no constraint behind it. A write that set only the UUID
                // would satisfy every InMemory test in the suite and leave a membership that joins
                // to nothing.
                storedFk.Should().NotBeNull(
                    "CohortMember.CohortId is the actual foreign key and it is NULLABLE, so a linkage that set only CohortUUID would insert happily and join to nothing");
                storedFk.Should().Be(cohortId.ToString());
                storedUuid.Should().Be(cohortUuid.ToString());
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        [SkippableFact]
        public async Task TheMembership_JoinsBackToItsCohort_ThroughRealSql()
        {
            // The linkage is only worth anything if a JOIN finds it. InMemory never translates one,
            // so this is the first time the relationship is exercised as SQL.
            Skip.IfNot(Enabled, SkipReason);
            string db = await ProvisionAsync("join");
            try
            {
                string cs = ConnectionTo(db);
                (long _, Guid cohortUuid) = await SeedCohortAsync(cs);
                Guid acceptedUser = Guid.NewGuid();

                using (DataDbContext ctx = Context(cs))
                {
                    await new CohortMemberQueries(ctx).CreateForCohort(acceptedUser, cohortUuid);
                }

                string joinedName = await ScalarAsync(cs,
                    "SELECT c.\"Name\" FROM \"CohortMember\" m " +
                    "JOIN \"Cohort\" c ON c.\"Id\" = m.\"CohortId\" " +
                    "WHERE m.\"UserId\" = '" + acceptedUser + "'");

                joinedName.Should().Be("Spring Term",
                    "the membership must resolve to its cohort through the foreign key, which is what makes it a linkage rather than two loose columns");
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        [SkippableFact]
        public async Task TheLinkage_Succeeds_WhenTheContextAlreadyTracksTheCohort()
        {
            // The reason CreateForCohort resolves the cohort INSIDE the DAL rather than accepting
            // one read through ICohortQueries: an AsNoTracking copy attached to a context that
            // already tracks the same key throws "another instance with the same key value is
            // already being tracked", the catch swallows it, and the operator sees a linkage that
            // silently did not happen. This drives the collision deliberately.
            Skip.IfNot(Enabled, SkipReason);
            string db = await ProvisionAsync("tracked");
            try
            {
                string cs = ConnectionTo(db);
                (long cohortId, Guid cohortUuid) = await SeedCohortAsync(cs);
                Guid acceptedUser = Guid.NewGuid();

                using (DataDbContext ctx = Context(cs))
                {
                    // Make the context track the cohort FIRST, which is the state a real request
                    // reaches after any earlier read of the same row.
                    Cohort alreadyTracked = await ctx.Cohort.FirstOrDefaultAsync(c => c.UUID == cohortUuid);
                    alreadyTracked.Should().NotBeNull();

                    CohortMember member = await new CohortMemberQueries(ctx).CreateForCohort(acceptedUser, cohortUuid);
                    member.Should().NotBeNull("resolving inside the DAL is what keeps this from throwing");
                }

                string storedFk = await ScalarAsync(cs,
                    "SELECT \"CohortId\" FROM \"CohortMember\" WHERE \"UserId\" = '" + acceptedUser + "'");
                storedFk.Should().Be(cohortId.ToString(), "and the row must still carry the real foreign key");
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        [SkippableFact]
        public async Task TheLinkage_IsSkipped_AndNoRowIsWritten_WhenTheCohortWasDeletedSinceIssue()
        {
            // Days pass between issuing an invitation and accepting it. The cohort may be gone by
            // then, which is why the invite stores a plain uuid rather than a foreign key: the
            // account is still created and only the linkage is skipped.
            Skip.IfNot(Enabled, SkipReason);
            string db = await ProvisionAsync("gone");
            try
            {
                string cs = ConnectionTo(db);
                Guid neverExisted = Guid.NewGuid();
                Guid acceptedUser = Guid.NewGuid();

                using (DataDbContext ctx = Context(cs))
                {
                    CohortMember member = await new CohortMemberQueries(ctx).CreateForCohort(acceptedUser, neverExisted);
                    member.Should().BeNull("a cohort that no longer exists yields no membership");
                }

                string rows = await ScalarAsync(cs,
                    "SELECT COUNT(*) FROM \"CohortMember\" WHERE \"UserId\" = '" + acceptedUser + "'");
                rows.Should().Be("0", "and must leave no orphan row behind");
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        [SkippableFact]
        public async Task TheLinkage_IncludesArchivedCohorts_BecauseTheIssuerAlreadyChose()
        {
            // Documented behaviour rather than an oversight: this resolves a selection an operator
            // made when the invitation was issued, so an archive in the meantime does not silently
            // discard it. Pinned so it stays a decision.
            Skip.IfNot(Enabled, SkipReason);
            string db = await ProvisionAsync("archived");
            try
            {
                string cs = ConnectionTo(db);
                (long cohortId, Guid cohortUuid) = await SeedCohortAsync(cs, archived: true);
                Guid acceptedUser = Guid.NewGuid();

                using (DataDbContext ctx = Context(cs))
                {
                    CohortMember member = await new CohortMemberQueries(ctx).CreateForCohort(acceptedUser, cohortUuid);
                    member.Should().NotBeNull("an archived cohort still accepts the membership the issuer selected");
                }

                string storedFk = await ScalarAsync(cs,
                    "SELECT \"CohortId\" FROM \"CohortMember\" WHERE \"UserId\" = '" + acceptedUser + "'");
                storedFk.Should().Be(cohortId.ToString());
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }

        [SkippableFact]
        public async Task TheForeignKeyIsRealAndNullable_WhichIsWhyTheWriteHasToBeChecked()
        {
            // Establishes the premise the headline test rests on, straight from the catalog rather
            // than from the model snapshot: there IS a foreign key from CohortMember to Cohort, it
            // is on CohortId, and that column is nullable. Nullable is the load-bearing part -- if
            // it were NOT NULL, Postgres itself would refuse a half-made linkage and the headline
            // test would be redundant. It is not, so it is not.
            Skip.IfNot(Enabled, SkipReason);
            string db = await ProvisionAsync("catalog");
            try
            {
                string cs = ConnectionTo(db);

                string fkTarget = await ScalarAsync(cs,
                    "SELECT ccu.table_name FROM information_schema.table_constraints tc " +
                    "JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name " +
                    "JOIN information_schema.constraint_column_usage ccu ON tc.constraint_name = ccu.constraint_name " +
                    "WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_name = 'CohortMember' " +
                    "AND kcu.column_name = 'CohortId'");
                fkTarget.Should().Be("Cohort", "the membership's real relational link is CohortId, not CohortUUID");

                string nullable = await ScalarAsync(cs,
                    "SELECT is_nullable FROM information_schema.columns " +
                    "WHERE table_name = 'CohortMember' AND column_name = 'CohortId'");
                nullable.Should().Be("YES",
                    "because it is nullable, a write that forgets the navigation inserts successfully and links nothing, which no InMemory test can detect");

                string uuidConstraints = await ScalarAsync(cs,
                    "SELECT COUNT(*) FROM information_schema.table_constraints tc " +
                    "JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name " +
                    "WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_name = 'CohortMember' " +
                    "AND kcu.column_name = 'CohortUUID'");
                uuidConstraints.Should().Be("0",
                    "CohortUUID carries no constraint, so it can name a cohort that does not exist and Postgres will not object");
            }
            finally
            {
                await DropScratchAsync(db);
            }
        }
    }
}
