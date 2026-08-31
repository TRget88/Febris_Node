// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.UserModels;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Febris.UserNode.DataAccessLayer.DataContext
{
    //Add-Migration Initial -Context ApplicationDbContext
    //update-database Initial -Context ApplicationDbContext
    public class ApplicationDbContext : IdentityDbContext<LocalApplicationUser, ApplicationRole, Guid>
    {
        #region build options
        public static OptionsBuild ops = new OptionsBuild();
        public class OptionsBuild
        {
            public OptionsBuild()
            {
                Settings = new AppConfiguration();
                OpsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                OpsBuilder.UseNpgsql(Settings.UserDataConnectionString);                          
                DbOptions = OpsBuilder.Options;
            }

            public DbContextOptionsBuilder<ApplicationDbContext> OpsBuilder { get; set; }
            public DbContextOptions<ApplicationDbContext> DbOptions { get; set; }
            internal AppConfiguration Settings { get; set; }
        }
        #endregion

        public ApplicationDbContext()
        {
        }
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
            //base.Database.Migrate();
            //if (base.Database.EnsureCreated()) 
            //{
            //    base.Database.Migrate();
            //}
        }
        //setting up onmodelcreating so UUIDs will be set by db
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<UserSecurityAndTracking>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
            //modelBuilder.HasPostgresExtension("uuid-ossp").Entity<DailyActiveUsers>().Property(b => b.UUID).HasDefaultValueSql("uuid_generate_v4()");
                                                                                                                                                                                              
            //decryption
            #region decrypt properties
            //modelBuilder.Entity<ApplicationUser>().Property(i => i.UserName).HasConversion(
            //    i => i,
            //    i => this.Decrypt(i)
            //    );
            //modelBuilder.Entity<ApplicationUser>().Property(i => i.Email).HasConversion(
            //    i => i,
            //    i => this.Decrypt(i)
            //    );
            //modelBuilder.Entity<ApplicationUser>().Property(i => i.NormalizedEmail).HasConversion(
            //     i => i,
            //     i => this.Decrypt(i)
            //     );
            //modelBuilder.Entity<ApplicationUser>().Property(i => i.NormalizedUserName).HasConversion(
            //    i => i,
            //    i => this.Decrypt(i)
            //    );
            //modelBuilder.Entity<LocalApplicationUser>().Property(i => i.PhoneNumber).HasConversion(
            //    i => i,
            //    i => this.Decrypt(i)
            //    );
            #endregion
        }

        #region decrypt request
        internal string Decrypt(string cipher)
        {
            string decrypted = string.Empty;
            try
            {
                //var connectionString = this.appSettings.ConnectionStrings.DB;
                var connectionString = ops.Settings.UserDataConnectionString;//_config.GetConnectionString("DataDBConnection");
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseNpgsql(connectionString);
                try
                {
                    using (var dbContext = new ApplicationDbContext(optionsBuilder.Options))
                    using (var command = dbContext.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandText = "public.decrypt_on_select_text";
                        command.Parameters.Add(
                            new Npgsql.NpgsqlParameter("column_field", NpgsqlTypes.NpgsqlDbType.Text) { Value = cipher });
                        if (command.Connection.State == ConnectionState.Closed)
                        {
                            command.Connection.Open();
                        }
                        decrypted = (string)command.ExecuteScalar();
                    }
                }
                catch
                {
                    using (var dbContext = new ApplicationDbContext(optionsBuilder.Options))
                    using (var command = dbContext.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandText = "public.decrypt_on_select";
                        command.Parameters.Add(
                            new Npgsql.NpgsqlParameter("column_field", NpgsqlTypes.NpgsqlDbType.Text) { Value = cipher });
                        if (command.Connection.State == ConnectionState.Closed)
                        {
                            command.Connection.Open();
                        }
                        decrypted = (string)command.ExecuteScalar();
                    }
                }
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "ApplicationDbContext.Decrypt: suppressed exception"); }
            return decrypted;
        }
        #endregion
        
        
        //public DbSet<UserSecurityAndTracking> UserSecurityAndTracking { get; set; }


        //public DbSet<DailyActiveUsers> DailyActiveUsers { get; set; }
    }
}
