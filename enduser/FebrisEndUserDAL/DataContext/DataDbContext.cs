// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.XApiModels.ExtraModels;
//using Febris.UserNode.DataAccessLayer.Models.FebrisDataModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

//Add-Migration Initial -Context DataDbContext
//update-database Initial -Context DataDbContext
namespace Febris.UserNode.DataAccessLayer.DataContext
{
    public class DataDbContext : DbContext
    {
        public static OptionsBuild ops = new OptionsBuild();
        public class OptionsBuild
        {
            public OptionsBuild()
            {
                Settings = new AppConfiguration();
                OpsBuilder = new DbContextOptionsBuilder<DataDbContext>();
                OpsBuilder.UseNpgsql(Settings.DataConnectionString);
                DbOptions = OpsBuilder.Options;
            }
            public DbContextOptionsBuilder<DataDbContext> OpsBuilder { get; set; }
            public DbContextOptions<DataDbContext> DbOptions { get; set; }
            internal AppConfiguration Settings { get; set; }
        }

        public DataDbContext(DbContextOptions<DataDbContext> options)
            : base(options)
        {
            // ENV-B1 / MDM-B4 (completing the partial fix -- XApi/ApplicationDb were already done):
            // schema init removed from the constructor. EnsureCreated()+Migrate() is a
            // mutually-exclusive pair -- on a fresh tenant DB, EnsureCreated builds the schema with
            // no __EFMigrationsHistory, then Migrate replays the Initial migration and collides
            // (42P07 "relation already exists"). Provisioning now runs ONCE at host startup via
            // EndUserDatabaseProvisioner.ProvisionEndUserDatabases() -- Migrate() for this
            // migration-managed context. Removing ctor DB I/O also unblocks design-time `dotnet ef`.
            //if (base.Database.EnsureCreated())
            //{
            //    base.Database.Migrate();
            //}
        }
        //setting up onmodelcreating so UUIDs will be set by db
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            #region Models
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<MessageBoard>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Location>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Cohort>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<CohortMember>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<LocalHardware>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");

                // PhysicalLicense IS the device authentication credential. HardwareQueries
                // .GetByKey resolves an incoming device to a row by matching it, and until now
                // the column had NO index and NO uniqueness -- verified against a live node
                // database, where the ONLY index on Hardware was the primary key. Two
                // consequences, one slow and one dangerous:
                //
                //   1. every device authentication was a sequential scan of the device table.
                //   2. nothing stopped two rows carrying the SAME credential, and GetByKey picks
                //      with an UNORDERED FirstOrDefaultAsync, so WHICH row authenticated was
                //      arbitrary. Audit C-09 hit exactly this: its duplicate-INSERT bug created a
                //      second row with the same credential, and locking the device out reported
                //      success while the device kept authenticating as the other row. C-09 fixed
                //      the insert. It could not fix the schema that allowed it.
                //
                // FILTERED so unregistered devices do not collide with each other. Postgres
                // already treats NULLs as distinct in a unique index, but an empty string is not
                // NULL and would, so both are excluded explicitly.
                x.HasIndex(b => b.PhysicalLicense)
                    .IsUnique()
                    .HasFilter("\"PhysicalLicense\" IS NOT NULL AND \"PhysicalLicense\" <> ''");
            });

            // ------------------------------------------------------------------------------
            // CENTRAL ENTITIES DO NOT BELONG IN A NODE DATABASE.
            //
            // The shared MessageBoard model navigates to three CENTRAL aggregates -- Institution,
            // ContentDeveloper and AccreditationBody. MessageBoard is a node DbSet, and EF Core
            // maps every entity REACHABLE from a DbSet, so those three plus everything they in
            // turn navigate to were pulled into this model and given tables in every node
            // database:
            //
            //   Institution        -> InstitutionSettings, InstitutionType, DeploymentType
            //   ContentDeveloper   -> ContentDeveloperSettings, ContentDeveloperType
            //   AccreditationBody  -> AccreditationBodySettings
            //
            // Nine tables. Verified against a live node database: every one of them EMPTY, no
            // controller for any of them, and the only node logic that referenced them
            // (ContentDeveloperLogic, InstitutionLogic) has no injector -- CurriculumController
            // records that they were deliberately removed from it because ContentDeveloper is a
            // hub concept.
            //
            // This is the Hardware1 defect again: a shared model dragging a central aggregate
            // into the node context by convention. It is not merely untidy. Because the node's
            // model CONTAINED those entities, every time the central tier evolved one the node's
            // model diverged from the node's own schema -- ContentDeveloper gained
            // SubscriptionRate and PendingSelfSignUp centrally and the node's table has neither.
            // That left DataDbContext permanently reporting pending model changes, which
            // silently bundled four unrelated operations into any new migration.
            //
            // Ignoring the three NAVIGATIONS is enough: the other six are reachable only through
            // them. The MessageBoard columns themselves are unaffected -- it keeps its
            // InstitutionUUID / ContentDeveloperUUID / AccreditationBodyUUID scalars, which is
            // how a node refers to a central thing anyway.
            modelBuilder.Entity<MessageBoard>(x =>
            {
                x.Ignore(b => b.Institution);
                x.Ignore(b => b.ContentDeveloper);
                x.Ignore(b => b.AccreditationBody);

                // The matching UUID scalars go too (owner ruling 2026-08-09). They were how the
                // central tier filtered a message board, and the node never used them:
                //   - nothing WRITES them. The Create and Edit bind lists on
                //     MessageBoardController are Subject/Message/TimeStamp/... and never include
                //     a UUID, so on a node all three are permanently Guid.Empty.
                //   - nothing READS them. The only two filters that ever did --
                //     CohortQueries:153 and HardwareLinkedCurriculumQueries:121, both
                //     .Where(i => i.ContentDeveloperUUID == input) -- are commented out.
                //   - the only display was Views/MessageBoard/Delete.cshtml, whose controller
                //     action is itself commented out, so the view is unreachable. Deleted.
                //
                // IGNORED, not deleted from the model class: MessageBoard is a SHARED model with
                // 284 references in the central tier, which still uses these. This is a node
                // mapping decision and must not reach across the boundary.
                //
                // NOT to be confused with NodeIdentity.InstitutionUUID, which is a DIFFERENT
                // entity and is the node's own stable identity -- generated once by
                // NodeIdentitySeeder and read by NodeStatusLogic. That one stays.
                x.Ignore(b => b.InstitutionUUID);
                x.Ignore(b => b.ContentDeveloperUUID);
                x.Ignore(b => b.AccreditationBodyUUID);
            });

            // DECLARE the two indexes that 20260805130000_ParentLinkedStudentTable created.
            // The model only had a bare DbSet, so EF did not know about them and wanted to DROP
            // them from every node database. They are deliberate and they match how the table is
            // read -- a parent has many students, a student has several guardians -- so the model
            // is corrected to match the schema rather than the schema losing the indexes.
            modelBuilder.Entity<ParentLinkedStudent>(x =>
            {
                x.HasIndex(b => b.ParentUserId);
                x.HasIndex(b => b.StudentActorId);
            });
            // Video ownership. Name is the value that arrives on the video loader's query string,
            // so it is the lookup key and must be UNIQUE: two rows for one filename would make
            // "who owns this recording" ambiguous, and an entitlement check that resolves a row
            // arbitrarily is not a check. ActorUUID is indexed the same way and for the same reason
            // ParentLinkedStudent indexes StudentActorId above -- it is how the set is read.
            modelBuilder.Entity<Recording>(x =>
            {
                x.HasIndex(b => b.Name).IsUnique();
                x.HasIndex(b => b.ActorUUID);
                // Indexed because the upload gate resolves a recording and compares this on every
                // part, so it is on the hot path for ingest, not just for reads.
                x.HasIndex(b => b.HardwareUUID);
            });
            // The central `Hardware` entity is GONE from this context as of the Hardware1
            // teardown, so the NET8 Wave 2 PK-to-PK table-split carve-out that used to sit here is
            // deleted rather than migrated.
            //
            // What it was working around: the node registered the shared, centrally-typed link
            // entities (HardwareLinkedCurriculum, LocationLinkedHardware), whose `public Hardware
            // Hardware` navigations pulled the central aggregate into this model by convention.
            // DbSet<LocalHardware> had already claimed the table name "Hardware", so EF Core 3.1
            // silently gave the central entity a second table, Hardware1.
            //
            // Two things that carve-out asserted were wrong, which is why it is not kept:
            //   1. "the enduser DBs are provisioned by EnsureCreated from this model" -- they are
            //      not. EndUserDatabaseProvisioner calls Database.Migrate() for DataDb, and has
            //      since the v4 initial commit, so the model-only fix never reached any database
            //      the shipping code builds.
            //   2. "same single table, same columns, zero schema change" -- EF's own model differ
            //      wanted SEVEN extra suffixed columns on Hardware (HardwareTypeId1,
            //      Hardware_PhysicalLicense and friends), because the two entities disagree on
            //      HardwareTypeId nullability and so cannot share those columns.
            //
            // The node now uses its own LocalHardwareLinkedCurriculum and
            // LocalLocationLinkedHardware twins, so nothing pulls the central type in and the
            // collision cannot recur.
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<TestUser>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            #region - simplified
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<MessageBoard>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<MessageBoard>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<MessageBoard>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();

            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Location>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<Location>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<Location>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();

            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Cohort>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<Cohort>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<Cohort>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();

            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<CohortMember>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<CohortMember>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<CohortMember>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();

            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<LocalHardware>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<LocalHardware>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<LocalHardware>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();

            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<TestUser>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<TestUser>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<TestUser>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();  


            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<DailyUse>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<DailyUse>().Property(b => b.Date).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<DailyUse>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<DailyUse>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
            #endregion
            #endregion

            #region lookup Models     
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<CohortLinkedCurriculum>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<CohortLinkedLocation>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            // Modules > Curricula, node-locally. The node already owned Module and (through the
            // link entities' navigations) Curriculum, plus CohortLinkedCurriculum for the cohort
            // end -- but the Module-to-Curriculum join existed ONLY hub-side, so a node could not
            // say which modules a curriculum contains without asking the hub.
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<ModuleLinkedCurriculum>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<LocalHardwareLinkedCurriculum>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<HardwareLinkedCohort>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<LocalHardwareLinkedModule>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<HardwareLinkedModule>(x =>
            //{
            //    x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //    x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
            //    x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //});
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<LocalLocationLinkedHardware>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<LocationLinkedUser>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            #region - simlified
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<CohortLinkedCurriculum>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<CohortLinkedCurriculum>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<CohortLinkedCurriculum>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();

            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<CohortLinkedLocation>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<CohortLinkedLocation>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<CohortLinkedLocation>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();

            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<HardwareLinkedCurriculum>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<HardwareLinkedCurriculum>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<HardwareLinkedCurriculum>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();

            ////modelBuilder.HasPostgresExtension("uuid-ossp").Entity<HardwareLinkedCurriculum>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            ////modelBuilder.Entity<HardwareLinkedCurriculum>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            ////modelBuilder.Entity<HardwareLinkedCurriculum>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();

            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<HardwareLinkedCohort>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<HardwareLinkedCohort>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<HardwareLinkedCohort>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();

            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<LocationLinkedHardware>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<LocationLinkedHardware>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<LocationLinkedHardware>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();

            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<LocationLinkedUser>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<LocationLinkedUser>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<LocationLinkedUser>().Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
            #endregion
            #endregion

            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<HardwareLinkedCurriculum>(x =>
            //{
            //    x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //    x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
            //    x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //});
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<HardwareLinkedCohort>(x =>
            //{
            //    x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //    x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
            //    x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //});

            #region Local module catalog
            // (delivery-path severance): the node owns its module CATALOG
            // in its own DataDb instead of re-fetching it from central over HTTP on every launcher
            // initialize / download. The tenant schema originally created Module/ModuleClassification
            // in the 2022 Initial migration, then DROPPED them in 20220726211838_updates when the
            // catalog moved central-side; the LocalModuleCatalog migration re-creates them (plus the
            // previously central-only ModuleLinkedObject) as the node-local source of truth. Blocks
            // mirror the shared DataDbContext exactly.
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Module>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<ModuleClassification>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<ModuleLinkedObject>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            #endregion

            #region Node identity
            // (auth severance): the node's LOCAL single-tenant identity --
            // exactly one row, seeded idempotently at provision time by NodeIdentitySeeder.
            // NODE-ONLY (never mapped centrally); replaces the License-claim-derived institution
            // identity when no hub license is present.
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<NodeIdentity>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            #endregion

            #region Hub federation settings
            // (hub-pull sync; owner-ratified 2026-07-17 "the operator owns federation"):
            // the node-side persistence for the ONE hub-federation gate. Exactly one row, written
            // by the portal admin surface (no seeder -- absence means the operator never opted
            // in); when present it GOVERNS the gate over the legacy configuration resolution.
            // LicenseKey stores the IDataProtection payload, never plaintext (see
            // HubFederationConfigQueries). NODE-ONLY (never mapped centrally).
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<HubFederationConfig>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            #endregion

            #region Node registration policy
            // (node initialization design 2026-08-18: the gap was the TOGGLE, not the bootstrap):
            // the node-side persistence for the operator's registration policy. Exactly one row,
            // written by the portal's Registration admin page (no seeder -- absence means the
            // operator never touched the page, and the configured Identity:Registration section
            // keeps governing unchanged). When present it GOVERNS, with one asymmetry that is the
            // point of the feature: a FAILED read resolves AdminOnly rather than falling back to
            // configuration, because a node configured Open must not re-open on a database blip.
            // NODE-ONLY (never mapped centrally).
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<NodeRegistrationConfig>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            #endregion

            #region Node account invitations
            // (invitation flow 2026-08-21): admin-issued invitations to create an account on this
            // node. NODE-ONLY, and NOT the same thing as ContentDeveloperUserInvite, which is the
            // central tier's invite into a developer org and is mapped only on the shared context.
            // TokenHash is UNIQUE: redemption looks the row up BY that hash, so a duplicate would
            // make which invitation a link redeems depend on row order.
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<NodeUserInvite>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
                x.HasIndex(b => b.TokenHash).IsUnique();
            });
            #endregion

            #region Node first-run setup token
            // (first-run claim 2026-08-21): the one-time token that lets whoever can read the
            // node's STDOUT claim it by creating the first ITAdmin. Replaces a compiled-in seeded
            // admin, which is a poor deployment shape for an open-source project. At most one live
            // row at a time; consumed rows are kept as the audit record of the claim. TokenHash is
            // UNIQUE for the same reason as the invitation store: the claim finds the row BY that
            // hash. NODE-ONLY (never mapped centrally).
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<NodeSetupToken>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
                x.HasIndex(b => b.TokenHash).IsUnique();
            });
            #endregion

            #region Node artifact store
            // (client-software distribution + module ingest): the node's own
            // software-package catalog (previously proxied from central over HTTP) and the artifact
            // bookkeeping rows for everything ingested through IStorageProvider. PackageArtifact is
            // NODE-ONLY (never mapped centrally); StorageKey is unique -- re-ingesting a key
            // overwrites the stored object and updates its row.
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<LocalSoftwarePackage>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<PackageArtifact>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
                x.HasIndex(b => b.StorageKey).IsUnique();
            });
            #endregion

        }


        /// <summary>
        /// Models in Alphabetical order
        /// </summary>
        /// 
        #region Models
        //Communication
        public DbSet<MessageBoard> MessageBoard { get; set; }
        //Filter helpers
        public DbSet<Location> Location { get; set; }
        //Student models
        public DbSet<Cohort> Cohort { get; set; }
        public DbSet<CohortMember> CohortMember { get; set; }
        public DbSet<ParentLinkedStudent> ParentLinkedStudent { get; set; }
        // Session-video ownership. Deliberately carries NO navigation property: a nav to the
        // central Hardware type is what produced the Hardware1 second table, so this entity stores
        // only the filename and the owning actor UUID.
        public DbSet<Recording> Recording { get; set; }
        //Hardware
        public DbSet<LocalHardware> Hardware { get; set; }
        // (local-first flip): the hardware-type lookup the node's own
        // registration pages need. NOT a new table -- it has been in the shipped tenant schema
        // since the 2022 Initial migration (created for the central Hardware entity's FK, pulled
        // into this model by convention through that navigation) but sat empty while the Remote
        // HardwareTypeQueries fetched it over HTTP. Seeded at provision time by HardwareTypeSeeder.
        //May only need these for test users
        public DbSet<TestUser> TestUser { get; set; }
        #endregion


        /// <summary>
        /// lookup Models in Alphabetical order
        /// </summary>        
        #region lookup Models       

        public DbSet<CohortLinkedLocation> CohortLinkedLocation { get; set; }
        public DbSet<CohortLinkedCurriculum> CohortLinkedCurriculum { get; set; }
        public DbSet<ModuleLinkedCurriculum> ModuleLinkedCurriculum { get; set; }

        public DbSet<LocalHardwareLinkedCurriculum> HardwareLinkedCurriculum { get; set; }
        public DbSet<HardwareLinkedCohort> HardwareLinkedCohort { get; set; }
        public DbSet<LocalHardwareLinkedModule> HardwareLinkedModule { get; set; }

        public DbSet<LocalLocationLinkedHardware> LocationLinkedHardware { get; set; }
        public DbSet<LocationLinkedUser> LocationLinkedUser { get; set; }

        #endregion


        #region Local module catalog
        // previously deliberately ABSENT (the catalog was central-owned and
        // fetched over HTTP by the Remote ModuleQueries / ModuleLinkedObjectQueries). The node now
        // owns Module / ModuleClassification / ModuleLinkedObject locally; rows are created by the
        // node's package-ingest path, optionally refreshed from a hub when one is configured.
        public DbSet<Module> Module { get; set; }
        public DbSet<ModuleClassification> ModuleClassification { get; set; }
        // Node-owned content authoring. Both tables have existed since the Initial migration but
        // had no DbSet, so they were reachable only through other entities' navigations and there
        // was no way to create one. A standalone node MUST be able to author its own curricula --
        // the hub-side content developer portal is hub-private and never ships. Schema-neutral:
        // these map the existing tables, no migration required.
        public DbSet<Curriculum> Curriculum { get; set; }
        public DbSet<CurriculumClassification> CurriculumClassification { get; set; }
        public DbSet<ModuleLinkedObject> ModuleLinkedObject { get; set; }
        #endregion

        #region Node artifact store
        // the node's own client-software catalog (mobile Server APK, Companion
        // APK, PC launcher installer, integration SDKs) plus the ingest bookkeeping for every
        // artifact stored through IStorageProvider (module .zips AND software packages).
        public DbSet<LocalSoftwarePackage> LocalSoftwarePackage { get; set; }
        public DbSet<PackageArtifact> PackageArtifact { get; set; }
        #endregion

        #region Node identity
        // (auth severance): the node's local single-tenant identity row.
        public DbSet<NodeIdentity> NodeIdentity { get; set; }
        #endregion

        #region Hub federation settings
        // the operator-owned federation settings row (single-row; portal-written).
        public DbSet<HubFederationConfig> HubFederationConfig { get; set; }
        #endregion

        #region Node registration policy
        // the operator-owned registration policy row (single-row; portal-written). Absence means
        // the configured Identity:Registration section governs.
        public DbSet<NodeRegistrationConfig> NodeRegistrationConfig { get; set; }
        #endregion

        #region Node account invitations
        // admin-issued invitations to create an account on this node. The token is stored HASHED;
        // the row never holds a redeemable secret.
        public DbSet<NodeUserInvite> NodeUserInvite { get; set; }
        #endregion

        #region Node first-run setup token
        // the one-time first-run claim token. Hashed at rest; the token itself only ever reaches
        // the node's stdout.
        public DbSet<NodeSetupToken> NodeSetupToken { get; set; }
        #endregion
    }
}
