// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Febris.UserNode.Portal.Data
{
    /// <summary>
    /// Sample content so the node can be exercised without hand-entering everything first.
    ///
    /// <para>
    /// DEBUG ONLY, by compilation, not by configuration. The whole class body is fenced, so a
    /// Release build cannot call it even by mistake and it cannot be switched on by an appsettings
    /// key an operator might copy from a dev machine. A node that ships with invented curricula is
    /// worse than one that ships empty.
    /// </para>
    ///
    /// <para>
    /// IDEMPOTENT, keyed on a marker. Every row it creates carries <see cref="SampleMarker"/> in its
    /// description, and the seeder returns early if any already exist, so restarting a host does not
    /// accumulate duplicates. That matters here specifically: the development database is SHARED on
    /// the LAN, so a seeder that appended on every boot would grow without bound in someone else's
    /// workspace.
    /// </para>
    ///
    /// <para>
    /// It deliberately writes <see cref="CohortLinkedCurriculum"/> rows DIRECTLY. There is no writer
    /// for that table anywhere in the application, so a curriculum cannot be attached to a cohort
    /// through any screen. Seeding the link is the only way to exercise the read side (the cohort's
    /// curriculum access listing), and the absence of a writer is recorded as a real gap rather than
    /// papered over by this file.
    /// </para>
    /// </summary>
    public static class DevSampleData
    {
        /// <summary>Stamped into every seeded row so the set is identifiable and removable.</summary>
        public const string SampleMarker = "[sample data]";

        public static async Task SeedAsync(IServiceProvider services)
        {
#if DEBUG
            try
            {
                using IServiceScope scope = services.CreateScope();
                DataDbContext db = scope.ServiceProvider.GetRequiredService<DataDbContext>();

                bool already = await db.Curriculum.AnyAsync(c => c.Description.Contains(SampleMarker));
                if (already)
                {
                    Log.Information("DevSampleData: sample rows already present, skipping.");
                    return;
                }

                DateTime now = DateTime.UtcNow;

                // ---- Curricula -------------------------------------------------------------
                List<Curriculum> curricula = new List<Curriculum>
                {
                    NewCurriculum("Fire Safety Fundamentals", "1.0", "Evacuation, extinguisher classes and alarm response.", now),
                    NewCurriculum("Confined Space Entry",     "2.1", "Atmospheric testing, permits and rescue planning.",   now),
                    NewCurriculum("Working at Height",        "1.3", "Harness inspection, anchor points and ladder use.",   now),
                };
                db.Curriculum.AddRange(curricula);

                // ---- Modules ---------------------------------------------------------------
                List<Module> modules = new List<Module>
                {
                    NewModule("Extinguisher Selection", "1.0", "Match the extinguisher class to the fire class.", now),
                    NewModule("Evacuation Drill",       "1.2", "Locate the nearest route and muster point.",      now),
                    NewModule("Gas Meter Calibration",  "2.0", "Bump test and calibrate before entry.",           now),
                    NewModule("Harness Inspection",     "1.1", "Pre-use inspection of a full-body harness.",      now),
                };
                db.Module.AddRange(modules);

                // ---- Cohorts ---------------------------------------------------------------
                List<Cohort> cohorts = new List<Cohort>
                {
                    NewCohort("Autumn Intake A", "First-year induction group.", now),
                    NewCohort("Maintenance Crew", "Rolling refresher group for site maintenance staff.", now),
                };
                db.Cohort.AddRange(cohorts);

                // ---- Devices ---------------------------------------------------------------
                // No PhysicalLicense: the credential is MINTED by HardwareLogic and stored only as a
                // hash. Seeding one would either be a plaintext credential in source or an
                // unusable string, and both are worse than a device an operator must regenerate.
                db.Hardware.AddRange(new List<LocalHardware>
                {
                    NewHardware("Bay 1 Headset", "Training bay 1.", now),
                    NewHardware("Bay 2 Headset", "Training bay 2.", now),
                    NewHardware("Mobile Cart",   "Roaming cart for off-site sessions.", now),
                });

                await db.SaveChangesAsync();

                // ---- Cohort to curriculum links -------------------------------------------
                // Written directly because nothing in the application can write them. See the class
                // note. Done after SaveChanges so the generated UUIDs are populated.
                db.CohortLinkedCurriculum.AddRange(new List<CohortLinkedCurriculum>
                {
                    NewLink(cohorts[0], curricula[0], now),
                    NewLink(cohorts[0], curricula[2], now),
                    NewLink(cohorts[1], curricula[1], now),
                });

                await db.SaveChangesAsync();

                Log.Information(
                    "DevSampleData: seeded {Curricula} curricula, {Modules} modules, {Cohorts} cohorts, " +
                    "3 devices and 3 cohort-curriculum links. All marked {Marker}.",
                    curricula.Count, modules.Count, cohorts.Count, SampleMarker);
            }
            catch (Exception ex)
            {
                // Never block a boot for sample data. A developer without it is inconvenienced; a
                // host that will not start is broken.
                Log.Error(ex, "DevSampleData: seeding failed, continuing without sample data");
            }
#else
            await Task.CompletedTask;
#endif
        }

#if DEBUG
        private static Curriculum NewCurriculum(string name, string version, string description, DateTime now)
        {
            return new Curriculum
            {
                UUID = Guid.NewGuid(),
                Name = name,
                Version = version,
                Description = description + " " + SampleMarker,
                Obsolete = false,
                TimeStamp = now,
                LastUpdateTimeStamp = now,
            };
        }

        private static Module NewModule(string name, string version, string description, DateTime now)
        {
            return new Module
            {
                UUID = Guid.NewGuid(),
                Name = name,
                Version = version,
                Description = description + " " + SampleMarker,
                Obsolete = false,
                TimeStamp = now,
                LastUpdateTimeStamp = now,
            };
        }

        private static Cohort NewCohort(string name, string description, DateTime now)
        {
            return new Cohort
            {
                UUID = Guid.NewGuid(),
                Name = name,
                Description = description + " " + SampleMarker,
                Archive = false,
                LockMembers = false,
                TimeStamp = now,
                LastUpdateTimeStamp = now,
            };
        }

        private static LocalHardware NewHardware(string name, string description, DateTime now)
        {
            return new LocalHardware
            {
                UUID = Guid.NewGuid(),
                DescriptiveName = name,
                Description = description + " " + SampleMarker,
                HardwareKind = HardwareKind.MobileServer,
                HardwareCondition = HardwareCondition.Active,
                IsLockedOut = false,
                TimeStamp = now,
                LastUpdateTimeStamp = now,
            };
        }

        private static CohortLinkedCurriculum NewLink(Cohort cohort, Curriculum curriculum, DateTime now)
        {
            return new CohortLinkedCurriculum
            {
                UUID = Guid.NewGuid(),
                Cohort = cohort,
                CohortUUID = cohort.UUID,
                Curriculum = curriculum,
                CurriculumUUID = curriculum.UUID,
                TimeStamp = now,
                LastUpdateTimeStamp = now,
            };
        }
#endif
    }
}
