// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.EnumLibrary;
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
    /// Pins <c>Cohort.Archive</c>, and the preservation of the vestigial <c>Cohort.LockMembers</c>
    /// column.
    ///
    /// <para>
    /// The defect: the Edit POST binds a fixed property list that omits both flags,
    /// <c>Views/Cohort/Edit.cshtml</c> renders neither, and <c>CohortQueries.Update</c> does a
    /// whole-entity <c>DbSet.Update</c> that writes every scalar. So saving a cohort's NAME
    /// silently cleared both -- and because the toggle actions were unreachable, nothing could set
    /// them back. Cleared by accident, set by nobody.
    /// </para>
    ///
    /// <para>
    /// Scope note, so these tests are not over-read: <c>Archive</c> is not ENFORCED anywhere yet.
    /// No read filters on it, so an archived cohort is still listed (ROADMAP 19 option 1 is to
    /// implement that). These tests pin that the flag can be set and is not clobbered, NOT that
    /// archiving hides anything. <c>MemberLockToggle</c> and its UI were removed entirely by owner
    /// decision for exactly that reason -- it gated nothing -- while <c>Archive</c> was kept because
    /// it is worth implementing. The <c>LockMembers</c> COLUMN survives, so <c>Update</c> must go
    /// on preserving whatever value a row already holds.
    /// </para>
    /// </summary>
    public class CohortRetirementFlagsTests
    {
        private static DataDbContext BuildContext(string dbName)
        {
            return new DataDbContext(new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName).Options);
        }

        /// <summary>An admin principal, so CohortLogic's role filter admits the call.</summary>
        private static IHttpContextAccessor AdminAccessor()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Role, InstitutionUserAccountType.Admin.ToString())
            }, "test");
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(context);
            return accessor.Object;
        }

        private static CohortLogic BuildLogic(DataDbContext context)
        {
            // The greedy DI ctor. Only ICohortQueries is exercised by these paths; the other three
            // are mocked rather than newed so a stray call would fail loudly instead of hitting a
            // self-newed DbContext against the real database.
            return new CohortLogic(
                AdminAccessor(),
                new CohortQueries(context),
                new Mock<ICohortMemberQueries>(MockBehavior.Strict).Object,
                new Mock<ILocationLinkedCohortQueries>(MockBehavior.Strict).Object);
        }

        [Fact]
        public async Task EditingACohortsName_DoesNotSilentlyUnarchiveIt()
        {
            using DataDbContext context = BuildContext(nameof(EditingACohortsName_DoesNotSilentlyUnarchiveIt));
            context.Cohort.Add(new Cohort
            {
                UUID = Guid.NewGuid(),
                Name = "Spring Term",
                Description = "original",
                Archive = true,
                LockMembers = true
            });
            context.SaveChanges();
            long id = context.Cohort.Single().Id;

            // Exactly what the Edit POST hands the BLL: the bound property list omits Archive and
            // LockMembers, so they arrive as the CLR default false.
            CohortLogic logic = BuildLogic(context);
            await logic.Update(new Cohort
            {
                Id = id,
                Name = "Spring Term (renamed)",
                Description = "edited",
                Archive = false,
                LockMembers = false
            });

            Cohort stored = context.Cohort.Single();
            stored.Name.Should().Be("Spring Term (renamed)", "the edit must still apply");
            stored.Description.Should().Be("edited");
            stored.Archive.Should().BeTrue("saving a name must not un-archive the cohort");
            stored.LockMembers.Should().BeTrue("nor unlock its membership");
        }

        [Fact]
        public async Task ArchiveToggle_IsTheOnlyThingThatChangesArchive_AndItRoundTrips()
        {
            using DataDbContext context = BuildContext(nameof(ArchiveToggle_IsTheOnlyThingThatChangesArchive_AndItRoundTrips));
            context.Cohort.Add(new Cohort { UUID = Guid.NewGuid(), Name = "Term", Archive = false });
            context.SaveChanges();
            long id = context.Cohort.Single().Id;

            CohortLogic logic = BuildLogic(context);

            (await logic.ArchiveToggle(id)).Should().BeTrue();
            context.Cohort.Single().Archive.Should().BeTrue("a finished cohort must be retirable");

            (await logic.ArchiveToggle(id)).Should().BeTrue();
            context.Cohort.Single().Archive.Should().BeFalse("and un-retirable -- not a one-way door");
        }

        [Fact]
        public async Task RecordSessionsToggle_IsTheOnlyThingThatChangesTheRecordingPolicy_AndItRoundTrips()
        {
            // ROADMAP 22. Deliberately shaped like the Archive round-trip above, and it matters
            // MORE here: unlike Archive (still unenforced) and LockMembers (removed for gating
            // nothing), this flag has a read side from day one -- LauncherLogic derives every
            // launch's record decision from it, pinned by RecordingPolicyDerivationTests.
            using DataDbContext context = BuildContext(nameof(RecordSessionsToggle_IsTheOnlyThingThatChangesTheRecordingPolicy_AndItRoundTrips));
            context.Cohort.Add(new Cohort { UUID = Guid.NewGuid(), Name = "Term", RecordSessions = false });
            context.SaveChanges();
            long id = context.Cohort.Single().Id;

            CohortLogic logic = BuildLogic(context);

            (await logic.RecordSessionsToggle(id)).Should().BeTrue();
            context.Cohort.Single().RecordSessions.Should().BeTrue("an educator must be able to turn recording on for their class");

            (await logic.RecordSessionsToggle(id)).Should().BeTrue();
            context.Cohort.Single().RecordSessions.Should().BeFalse("and back off again -- not a one-way door");
        }

        [Fact]
        public async Task EditingACohortsName_DoesNotSilentlyDisableRecording()
        {
            // The clobber trap that hit Archive and LockMembers, applied to the new flag: the Edit
            // POST's bind list omits RecordSessions and Edit.cshtml does not render it, so it
            // arrives as the CLR default false on every save. CohortLogic.Update must copy only
            // the editable fields onto the stored row and leave the policy alone.
            //
            // Worse here than it was for the other two: silently clearing this one does not just
            // lose a flag, it silently STOPS RECORDING a class the educator set to record, and
            // nothing in the UI would say so.
            using DataDbContext context = BuildContext(nameof(EditingACohortsName_DoesNotSilentlyDisableRecording));
            context.Cohort.Add(new Cohort
            {
                UUID = Guid.NewGuid(),
                Name = "Spring Term",
                Description = "original",
                RecordSessions = true
            });
            context.SaveChanges();
            long id = context.Cohort.Single().Id;

            CohortLogic logic = BuildLogic(context);
            await logic.Update(new Cohort
            {
                Id = id,
                Name = "Spring Term (renamed)",
                Description = "edited",
                RecordSessions = false
            });

            Cohort stored = context.Cohort.Single();
            stored.Name.Should().Be("Spring Term (renamed)", "the edit must still apply");
            stored.RecordSessions.Should().BeTrue("renaming a cohort must not silently stop recording it");
        }

        // MemberLockToggle_RoundTrips was removed with the action itself (ROADMAP 19, owner
        // decision 2026-08-05): Cohort.LockMembers was enforced nowhere, so the toggle set a flag
        // that gated nothing. The COLUMN survives, and the clobber test above still asserts that
        // Update PRESERVES any stored value rather than writing it to false -- that has to hold
        // for as long as the column exists.

        [Fact]
        public async Task ArchivedCohorts_AreHiddenFromTheDefaultList()
        {
            // ROADMAP 19: the flag now actually does something. Before this, archiving set a
            // boolean that no read consulted, so a finished term stayed in every list and picker.
            using DataDbContext context = BuildContext(nameof(ArchivedCohorts_AreHiddenFromTheDefaultList));
            context.Cohort.Add(new Cohort { UUID = Guid.NewGuid(), Name = "Active term", Archive = false });
            context.Cohort.Add(new Cohort { UUID = Guid.NewGuid(), Name = "Finished term", Archive = true });
            context.SaveChanges();

            CohortLogic logic = BuildLogic(context);

            List<Cohort> active = await logic.Get(false);
            active.Select(c => c.Name).Should().BeEquivalentTo(new[] { "Active term" });
        }

        [Fact]
        public async Task ArchivingIsNotAOneWayDoor_TheIncludeArchivedPathReturnsThemBack()
        {
            // The trap this guards: the un-archive toggle is rendered on the index ROW, so hiding
            // archived cohorts without an include path would archive them beyond reach forever.
            // ROADMAP 11 records exactly that for curricula ("cannot be un-obsoleted from the UI").
            using DataDbContext context = BuildContext(nameof(ArchivingIsNotAOneWayDoor_TheIncludeArchivedPathReturnsThemBack));
            context.Cohort.Add(new Cohort { UUID = Guid.NewGuid(), Name = "Active term", Archive = false });
            context.SaveChanges();
            long id = context.Cohort.Single().Id;

            CohortLogic logic = BuildLogic(context);

            await logic.ArchiveToggle(id);
            (await logic.Get(false)).Should().BeEmpty("archived cohorts leave the default list");

            List<Cohort> withArchived = await logic.Get(true);
            withArchived.Should().HaveCount(1, "and must still be reachable, or archiving is irreversible");

            // Reachable means un-archivable: the round trip has to close.
            await logic.ArchiveToggle(id);
            (await logic.Get(false)).Should().HaveCount(1, "un-archiving must put it back in the default list");
        }

        [Fact]
        public async Task EditThenToggle_ComposeCorrectly()
        {
            // The two paths share a DbContext and CohortQueries.Get uses FindAsync, which TRACKS.
            // The Update fix copies onto that tracked instance rather than passing a second one --
            // passing a second instance with the same key throws. This exercises both in sequence
            // so that constraint stays pinned.
            using DataDbContext context = BuildContext(nameof(EditThenToggle_ComposeCorrectly));
            context.Cohort.Add(new Cohort { UUID = Guid.NewGuid(), Name = "Term", Archive = false });
            context.SaveChanges();
            long id = context.Cohort.Single().Id;

            CohortLogic logic = BuildLogic(context);

            await logic.Update(new Cohort { Id = id, Name = "Renamed", Description = "d" });
            await logic.ArchiveToggle(id);
            await logic.Update(new Cohort { Id = id, Name = "Renamed twice", Description = "d2" });

            Cohort stored = context.Cohort.Single();
            stored.Name.Should().Be("Renamed twice");
            stored.Archive.Should().BeTrue("the archive set between the two edits must survive the second");
        }
    }
}
