// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.AnalyticsModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Febris.UserNode.DataAccessLayer.DataContext
{
    //Add-Migration Initial -Context AnalyticsDbContext
    //update-database Initial -Context AnalyticsDbContext
    public class AnalyticsDbContext : DbContext
    {
        #region Factory and settings
        public static OptionsBuild ops = new OptionsBuild();
        public class OptionsBuild
        {
            public OptionsBuild()
            {
                Settings = new AppConfiguration();
                OpsBuilder = new DbContextOptionsBuilder<AnalyticsDbContext>();
                OpsBuilder.UseNpgsql(Settings.AnalyticsConnectionString);
                DbOptions = OpsBuilder.Options;
            }
            public DbContextOptionsBuilder<AnalyticsDbContext> OpsBuilder { get; set; }
            public DbContextOptions<AnalyticsDbContext> DbOptions { get; set; }
            internal AppConfiguration Settings { get; set; }
        }

        public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options)
            : base(options)
        {
            // ENV-B1 / MDM-B4: schema init removed from the constructor (see DataDbContext for the
            // 42P07 collision this caused). This context has NO migrations of its own, so it is
            // provisioned at startup via EnsureCreated() -- never Migrate() -- by
            // EndUserDatabaseProvisioner.ProvisionEndUserDatabases().
            //if (base.Database.EnsureCreated())
            //{
            //    base.Database.Migrate();
            //}
        }

        #endregion

        //setting up onmodelcreating so UUIDs will be set by db
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region Models                   
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<UserAnalytics>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<LocalAnalytics>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");

                // T11. This table had exactly ONE index, the primary key, while TimeStamp is the
                // only column any reader orders by, and the table grows one row per HTTP request.
                // Every view of the analytics screen was a full scan and a sort of the whole history.
                //
                // Declared on the MODEL rather than in a migration on purpose: the provisioner gives
                // this context EnsureCreated(), never Migrate(), so a migration would simply never be
                // applied. EnsureCreated builds indexes from the model, so a NEW deployment gets it.
                //
                // An EXISTING deployment does not, and deliberately is not given it automatically.
                // Building an index on a table that has been collecting a row per request holds a
                // write lock for the duration, and doing that silently during a boot is exactly the
                // kind of operational surprise this audit exists to remove. SELF_HOSTING.md carries
                // the CREATE INDEX CONCURRENTLY line for operators to run when they choose.
                x.HasIndex(b => b.TimeStamp);
            });

            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<ModuleUsageAnalytics>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });
            modelBuilder.HasPostgresExtension("uuid-ossp").Entity<ModuleDownloadAnalytics>(x =>
            {
                x.Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
                x.Property(b => b.LastUpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
                x.Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            });

            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<GeoIPData>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.Entity<GeoIPData>().Property(b => b.TimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            //modelBuilder.Entity<GeoIPData>().Property(b => b.UpdateTimeStamp).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
            #endregion

        }

        #region Models
        public DbSet<LocalAnalytics> LocalAnalytics { get; set; }        
        public DbSet<UserAnalytics> UserAnalytics { get; set; }

        //public DbSet<GeoIPData> GeoIPData { get; set; }
        //public DbSet<GeoIPByCity> GeoIPByCity { get; set; }
        //public DbSet<GeoIPByCityData> GeoIPByCityData { get; set; }
        //public DbSet<GeoIPByCountry> GeoIPByCountry { get; set; }
        //public DbSet<GeoIPByCountryData> GeoIPByCountryData { get; set; }
        //public DbSet<GeoASN> GeoASN { get; set; }
        #endregion
        public DbSet<ModuleUsageAnalytics> ModuleUsageAnalytics { get; set; }
        public DbSet<ModuleDownloadAnalytics> ModuleDownloadAnalytics { get; set; }

    }
}
