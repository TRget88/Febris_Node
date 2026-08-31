// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.Models.XApiModels.ExtraModels;
using Febris.ModelLibrary.Models.XApiModels.ModifiedForSharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

//Add-Migration Initial -Context XApiDbContext
//update-database Initial -Context XApiDbContext

namespace Febris.UserNode.DataAccessLayer.DataContext
{
    //Add-Migration Initial -Context XAPIDbContext
    //update-database Update -Context XAPIDbContext
    public class XApiDbContext : DbContext
    {
        public static OptionsBuild ops = new OptionsBuild();
        public class OptionsBuild
        {
            public OptionsBuild()
            {
                Settings = new AppConfiguration();
                OpsBuilder = new DbContextOptionsBuilder<XApiDbContext>();
                OpsBuilder.UseNpgsql(Settings.XApiConnectionString);
                DbOptions = OpsBuilder.Options;
            }

            public DbContextOptionsBuilder<XApiDbContext> OpsBuilder { get; set; }
            public DbContextOptions<XApiDbContext> DbOptions { get; set; }
            private AppConfiguration Settings { get; set; }
        }




        public XApiDbContext(DbContextOptions<XApiDbContext> options) : base(options)
        {
            // FIX (MDM-B4): EnsureCreated and Migrate are mutually exclusive on EF Core 3.1. Schema init moved to startup.
            //if (base.Database.EnsureCreated())
            //{
            //    base.Database.Migrate();
            //}
        }
        //setting up onmodelcreating so UUIDs will be set by db
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // xAPI 1.0.3 typing uplift (Option B): Language Maps and the interaction correct-response
            // array are their true xAPI types (Dictionary<string,string> / List<string>), persisted as
            // jsonb via a Newtonsoft converter -- the SAME serializer the wire path uses, so storage and
            // emission agree and the old "serialize-a-string-that-is-already-JSON" double-encode is gone.
            // A snapshotting ValueComparer lets EF change-track these reference types correctly.
            // NOTE: these are LOCALS, not static fields -- referencing a static field here would trigger
            // this type's static constructor and its config-loading `ops` initializer during model
            // building, which throws in test/design-time contexts (no appsettings on disk).
            var LanguageMapConverter = new ValueConverter<Dictionary<string, string>, string>(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<Dictionary<string, string>>(v));

            var LanguageMapComparer = new ValueComparer<Dictionary<string, string>>(
                (a, b) => JsonConvert.SerializeObject(a) == JsonConvert.SerializeObject(b),
                v => v == null ? 0 : JsonConvert.SerializeObject(v).GetHashCode(),
                v => v == null ? null : JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(v)));

            var StringListConverter = new ValueConverter<List<string>, string>(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<List<string>>(v));

            var StringListComparer = new ValueComparer<List<string>>(
                (a, b) => JsonConvert.SerializeObject(a) == JsonConvert.SerializeObject(b),
                v => v == null ? 0 : JsonConvert.SerializeObject(v).GetHashCode(),
                v => v == null ? null : JsonConvert.DeserializeObject<List<string>>(JsonConvert.SerializeObject(v)));

            // Local-first vocabulary: the node owns its xAPI vocabulary
            // (Verb / Object / Version) in its OWN store instead of re-fetching it from central
            // over HTTP on every ingest/read. Blocks mirror the shared XApiDbContext exactly.
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Verb>(x =>
            {
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
                x.Property(b => b.Display).HasColumnType("jsonb").HasConversion(LanguageMapConverter, LanguageMapComparer);
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<ModelLibrary.Models.XApiModels.Object>(x =>
            {
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<ModelLibrary.Models.XApiModels.Version>(x =>
            {
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<LocalStatement>(x =>
            {
                x.Property(b => b.Stored).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.Timestamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");

                // T5 VOIDING: voided statements are excluded from EVERY query on this context.
                //
                // A GLOBAL filter rather than a predicate added to each read, deliberately. There
                // are 28 read paths across StatementQueries and StatementLogic and not one of them
                // filtered on anything void-related. Adding an exclusion to each would work until
                // the 29th read was written without it, and a voided statement that still counts
                // somewhere is exactly the defect the 2021 implementation shipped: it overwrote the
                // target's verb and never excluded it from a single query.
                //
                // The cost is that this is INVISIBLE at the call site. Anyone debugging "my row is
                // missing" will not see a WHERE they did not write. That is the trade accepted for
                // an exclusion that cannot be forgotten, and it is why the marker is a nullable
                // timestamp whose meaning is legible in the data itself.
                //
                // Reads that MUST still see voided rows call IgnoreQueryFilters(). Today that is the
                // dedupe lookup: it matches an inbound statement against its producer UUID, and if
                // it could not see voided rows the same statement id could be inserted twice.
                x.HasQueryFilter(b => b.VoidedAt == null);
                x.HasIndex(b => b.VoidedAt);

                // SDKV-19/20 (idempotent ingest): StatementLogic dedupes inbound
                // statements by their producer-assigned UUID before inserting, so
                // the lookup needs an index. Deliberately NON-unique: deployments
                // that ran the pre-fix double-commit bug may already hold duplicate
                // UUIDs -- a unique index would fail to apply there. The dedupe is
                // enforced by the pre-insert lookup, not by a constraint.
                x.HasIndex(b => b.UUID);
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Actor>(x =>
            {
                //x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                //x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Member>(x =>
            {
                //x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                //x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Account>(x =>
            {
                //x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                //x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Extensions>(x =>
            {
                //x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                //x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Attachment>(x =>
            {
                //x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                //x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
                x.Property(b => b.Display).HasColumnType("jsonb").HasConversion(LanguageMapConverter, LanguageMapComparer);
                x.Property(b => b.Description).HasColumnType("jsonb").HasConversion(LanguageMapConverter, LanguageMapComparer);
            });
            // Definition has no UUID default block above, but its Language-Map (Name/Description) and
            // interaction correct-response array need the jsonb/typed mapping (xAPI 1.0.3 uplift).
            modelBuilder.Entity<Definition>(x =>
            {
                x.Property(b => b.Name).HasColumnType("jsonb").HasConversion(LanguageMapConverter, LanguageMapComparer);
                x.Property(b => b.Description).HasColumnType("jsonb").HasConversion(LanguageMapConverter, LanguageMapComparer);
                x.Property(b => b.CorrectResponsesPattern).HasColumnType("jsonb").HasConversion(StringListConverter, StringListComparer);
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Authority>(x =>
            {
                //x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                //x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Context>(x =>
            {
                //x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                //x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<ContextActivities>(x =>
            {
                //x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                //x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Result>(x =>
            {
                //x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                //x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Score>(x =>
            {
                //x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                //x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<StatementReference>(x =>
            {
                //x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                //x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<XApiResultExtras>(x =>
            {
                //x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                //x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Statement>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<LocalStatement>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            ////this needs to be tested. It may not work. 
            //modelBuilder.Entity<Statement>().Property(b => b.Stored).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<LocalStatement>(x =>
            //{
            //    x.Property(b => b.Stored).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();                
            //    x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //});
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Actor>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Member>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Account>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            ////modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Verb>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            ////modelBuilder.HasPostgresExtension("uuid-ossp").Entity<ModelLibrary.Models.XApiModels.Object>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Definition>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Extensions>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Attachment>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Authority>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Context>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<ContextActivities>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Result>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<Score>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<StatementReference>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            ////probably need to add more from the different models in here. 
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<XApiResultExtras>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
        }

        //probably need to add more from the different models in here. 

        public DbSet<LocalStatement> LocalStatement { get; set; }
        public DbSet<Actor> Actor { get; set; }
        public DbSet<Member> Member { get; set; }
        public DbSet<Account> Account { get; set; }

        // Previously deliberately ABSENT (vocabulary was central-owned and
        // fetched over HTTP). The node now owns Verb/Object/Version locally; seeded at startup
        // by XApiVocabularySeeder, optionally refreshed from a hub when one is configured.
        public DbSet<Verb> Verb { get; set; }

        public DbSet<ModelLibrary.Models.XApiModels.Object> Object { get; set; }
        public DbSet<Definition> Definition { get; set; }
        public DbSet<Extensions> Extensions { get; set; }

        public DbSet<Attachment> Attachments { get; set; }

        public DbSet<Authority> Authority { get; set; }

        public DbSet<Context> Context { get; set; }
        public DbSet<ContextActivities> ContextActivities { get; set; }
        public DbSet<StatementReference> StatementReference { get; set; }

        public DbSet<Result> Result { get; set; }
        public DbSet<Score> Score { get; set; }
        public DbSet<ModelLibrary.Models.XApiModels.Version> Version { get; set; }

        public DbSet<XApiResultExtras> XApiResultExtras { get; set; }
    }
}
