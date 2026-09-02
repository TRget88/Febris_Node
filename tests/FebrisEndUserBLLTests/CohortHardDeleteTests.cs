// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Cohort HARD delete. Owner ruling 2026-08-09: delete the cohort and its link rows, and
    /// nothing on the other side of those links.
    ///
    /// <para>
    /// These tests exist because the boundary is easy to get wrong in either direction, and both
    /// directions are damaging. Delete too little and Postgres refuses the whole operation, because
    /// <c>CohortMember</c>, <c>CohortLinkedCurriculum</c> and <c>CohortLinkedLocation</c> are
    /// <c>ON DELETE RESTRICT</c> in the shipped schema. Delete too much and a cohort deletion starts
    /// destroying user accounts, curricula, locations or devices, which is exactly what the ruling
    /// forbids.
    /// </para>
    ///
    /// <para>
    /// A note on <c>HardwareLinkedCohort</c>, because the word "cascade" caused real alarm: it is
    /// <c>ON DELETE CASCADE</c> on BOTH of its foreign keys, and it is a pure junction row holding
    /// nothing but the two sides. Deleting a cohort removes the ASSIGNMENT, never the device;
    /// deleting a device removes the assignment, never the cohort. That symmetry is the loose
    /// coupling the schema was designed with, and it predates this work by four years
    /// (<c>20220726211838_updates</c>).
    /// </para>
    ///
    /// <para>
    /// The DAL removes all four link types EXPLICITLY rather than leaning on the database cascade.
    /// That makes the behaviour identical on any provider, which is also what makes these tests
    /// meaningful: the InMemory provider enforces neither foreign keys nor cascades, so a test that
    /// relied on the database to clean up would pass while proving nothing.
    /// </para>
    /// </summary>
    public class CohortHardDeleteTests
    {
        private static DataDbContext BuildContext(string dbName)
        {
            return new DataDbContext(new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName).Options);
        }

        private static IHttpContextAccessor Accessor(params string[] roles)
        {
            ClaimsIdentity identity = new ClaimsIdentity(
                roles.Select(r => new Claim(ClaimTypes.Role, r)), "test");
            DefaultHttpContext context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(context);
            return accessor.Object;
        }

        private static IHttpContextAccessor AdminAccessor()
        {
            return Accessor(InstitutionUserAccountType.Admin.ToString());
        }

        private static CohortLogic BuildLogic(DataDbContext context, IHttpContextAccessor accessor = null)
        {
            return new CohortLogic(
                accessor ?? AdminAccessor(),
                new CohortQueries(context),
                new Mock<ICohortMemberQueries>(MockBehavior.Strict).Object,
                new Mock<ILocationLinkedCohortQueries>(MockBehavior.Strict).Object);
        }

        /// <summary>
        /// Two cohorts, each fully wired to the SAME four entities. The bystander is the control:
        /// every assertion about what survives is worthless if the seed only ever had rows for the
        /// cohort being deleted.
        /// </summary>
        private static (long TargetId, long BystanderId) Seed(DataDbContext context)
        {
            Cohort target = new Cohort { Name = "Target", UUID = Guid.NewGuid() };
            Cohort bystander = new Cohort { Name = "Bystander", UUID = Guid.NewGuid() };
            Curriculum curriculum = new Curriculum { Name = "Shared curriculum", UUID = Guid.NewGuid() };
            Location location = new Location { UUID = Guid.NewGuid() };
            LocalHardware hardware = new LocalHardware { DescriptiveName = "Shared headset", UUID = Guid.NewGuid() };

            context.Cohort.AddRange(target, bystander);
            context.Curriculum.Add(curriculum);
            context.Location.Add(location);
            context.Hardware.Add(hardware);
            context.SaveChanges();

            foreach (Cohort c in new[] { target, bystander })
            {
                context.CohortMember.Add(new CohortMember
                {
                    Cohort = c,
                    CohortUUID = c.UUID,
                    UserId = Guid.NewGuid()
                });
                context.CohortLinkedCurriculum.Add(new CohortLinkedCurriculum
                {
                    Cohort = c,
                    CohortUUID = c.UUID,
                    Curriculum = curriculum,
                    CurriculumUUID = curriculum.UUID
                });
                context.CohortLinkedLocation.Add(new CohortLinkedLocation
                {
                    Cohort = c,
                    CohortUUID = c.UUID,
                    Location = location,
                    LocationUUID = location.UUID
                });
                context.HardwareLinkedCohort.Add(new HardwareLinkedCohort
                {
                    Cohort = c,
                    CohortUUID = c.UUID,
                    Hardware = hardware,
                    HardwareUUID = hardware.UUID
                });
            }

            context.SaveChanges();
            return (target.Id, bystander.Id);
        }

        /// <summary>
        /// TOTAL rows in each link table, deliberately NOT filtered by CohortId.
        ///
        /// <para>
        /// The first version of these tests counted rows whose CohortId still matched the deleted
        /// cohort, and it was WORTHLESS: removing all four RemoveRange calls from the DAL left every
        /// test green. EF's ClientSetNull convention applies to TRACKED optional dependents, so the
        /// link rows survived with CohortId = NULL -- orphaned, not deleted -- and a FK-filtered
        /// count cannot tell "deleted" from "silently orphaned". That silent orphan is one of the
        /// two outcomes this feature exists to avoid, so the test must count rows that EXIST.
        /// </para>
        /// </summary>
        private static (int Members, int Curricula, int Locations, int Hardware) LinkRowTotals(DataDbContext context)
        {
            return (context.CohortMember.Count(),
                    context.CohortLinkedCurriculum.Count(),
                    context.CohortLinkedLocation.Count(),
                    context.HardwareLinkedCohort.Count());
        }

        /// <summary>Link rows left pointing at nothing. Must always be zero.</summary>
        private static int OrphanedLinkRows(DataDbContext context)
        {
            return context.CohortMember.Count(x => EF.Property<long?>(x, "CohortId") == null)
                + context.CohortLinkedCurriculum.Count(x => EF.Property<long?>(x, "CohortId") == null)
                + context.CohortLinkedLocation.Count(x => EF.Property<long?>(x, "CohortId") == null)
                + context.HardwareLinkedCohort.Count(x => EF.Property<long?>(x, "CohortId") == null);
        }

        private static int LinksFor(DataDbContext context, long cohortId)
        {
            return context.CohortMember.Count(x => EF.Property<long?>(x, "CohortId") == cohortId)
                + context.CohortLinkedCurriculum.Count(x => EF.Property<long?>(x, "CohortId") == cohortId)
                + context.CohortLinkedLocation.Count(x => EF.Property<long?>(x, "CohortId") == cohortId)
                + context.HardwareLinkedCohort.Count(x => EF.Property<long?>(x, "CohortId") == cohortId);
        }

        [Fact]
        public async Task Delete_RemovesTheCohortAndEveryLinkRowThatPointsAtIt()
        {
            using DataDbContext context = BuildContext(nameof(Delete_RemovesTheCohortAndEveryLinkRowThatPointsAtIt));
            (long targetId, _) = Seed(context);

            LinkRowTotals(context).Should().Be((2, 2, 2, 2), "the seed must wire all four link types on BOTH cohorts, or this test proves nothing");

            bool deleted = await BuildLogic(context).Delete(targetId);

            deleted.Should().BeTrue();
            context.Cohort.Any(c => c.Id == targetId).Should().BeFalse("the cohort row itself must be gone");

            // Counting ROWS, not matching FKs. See LinkRowTotals for why the FK-filtered version of
            // this assertion passed even with the DAL's cleanup deleted entirely.
            LinkRowTotals(context).Should().Be((1, 1, 1, 1), "each link table must lose exactly the deleted cohort's row and keep the other cohort's");
            OrphanedLinkRows(context).Should().Be(0, "a link row left with a null CohortId is orphaned, not deleted -- that is the silent-corruption outcome");
        }

        [Fact]
        public async Task Delete_LeavesTheUsersCurriculaLocationsAndDevicesAlone()
        {
            // The owner ruling, stated as a test. A junction row is not an entity.
            using DataDbContext context = BuildContext(nameof(Delete_LeavesTheUsersCurriculaLocationsAndDevicesAlone));
            (long targetId, _) = Seed(context);

            await BuildLogic(context).Delete(targetId);

            context.Curriculum.Should().HaveCount(1, "deleting a cohort must never delete a curriculum");
            context.Location.Should().HaveCount(1, "deleting a cohort must never delete a location");
            context.Hardware.Should().HaveCount(1, "deleting a cohort must never delete a device");
        }

        [Fact]
        public async Task Delete_DoesNotTouchAnyOtherCohortOrItsLinks()
        {
            using DataDbContext context = BuildContext(nameof(Delete_DoesNotTouchAnyOtherCohortOrItsLinks));
            (long targetId, long bystanderId) = Seed(context);

            await BuildLogic(context).Delete(targetId);

            context.Cohort.Any(c => c.Id == bystanderId).Should().BeTrue("only the requested cohort may be deleted");
            LinksFor(context, bystanderId).Should().Be(4, "the other cohort's links share the same tables and must be untouched");
            LinkRowTotals(context).Should().Be((1, 1, 1, 1), "and no stray rows may survive in those tables");
        }

        [Fact]
        public async Task Delete_ReturnsFalseForACohortThatDoesNotExist()
        {
            using DataDbContext context = BuildContext(nameof(Delete_ReturnsFalseForACohortThatDoesNotExist));
            Seed(context);

            bool deleted = await BuildLogic(context).Delete(999999);

            deleted.Should().BeFalse("a missing cohort is not an error, and must not report success");
        }

        /// <summary>
        /// WHO MAY DELETE. Owner ruling 2026-08-09: Educator keeps it, because educators manage
        /// users as well. So this is a considered policy, not just the matched filter shape, and it
        /// is pinned per-role rather than left to the shared gate.
        ///
        /// <para>
        /// ITAdmin is included because it is the node's TOP LOCAL role -- it is what the bootstrap
        /// admin is seeded as -- and it reaches the gate through the OR branch inside
        /// <c>IsLocalAdmin()</c> rather than through a clause of its own. Nothing pinned that branch
        /// before, so a change to <c>IsLocalAdmin</c> could have locked the node's only
        /// administrator out of this operation with every test still green.
        /// </para>
        /// </summary>
        [Theory]
        [InlineData("Admin")]
        [InlineData("ITAdmin")]
        [InlineData("Educator")]
        public async Task Delete_IsPermittedFor(string role)
        {
            using DataDbContext context = BuildContext(nameof(Delete_IsPermittedFor) + role);
            (long targetId, _) = Seed(context);

            bool deleted = await BuildLogic(context, Accessor(role)).Delete(targetId);

            deleted.Should().BeTrue(role + " must be able to hard-delete a cohort");
            context.Cohort.Any(c => c.Id == targetId).Should().BeFalse();
            LinkRowTotals(context).Should().Be((1, 1, 1, 1));
        }

        /// <summary>
        /// The other half. A refused delete must write NOTHING -- not the cohort, not a link row,
        /// not a null-ed FK.
        /// </summary>
        [Theory]
        [InlineData("User")]
        [InlineData("UserParent")]
        [InlineData("")]
        public async Task Delete_IsRefusedFor_AndDeletesNothing(string role)
        {
            using DataDbContext context = BuildContext(nameof(Delete_IsRefusedFor_AndDeletesNothing) + role);
            (long targetId, _) = Seed(context);

            IHttpContextAccessor accessor = role.Length == 0 ? Accessor() : Accessor(role);
            bool deleted = await BuildLogic(context, accessor).Delete(targetId);

            deleted.Should().BeFalse();
            context.Cohort.Any(c => c.Id == targetId).Should().BeTrue("a refused delete must leave the cohort in place");
            LinksFor(context, targetId).Should().Be(4, "a refused delete must not remove links either");
            LinkRowTotals(context).Should().Be((2, 2, 2, 2), "nothing at all may be written by a refused delete");
        }
    }
}
