// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins node-local curriculum authoring. Curriculum and CurriculumClassification have existed
    /// in the node's DataDb since the Initial migration but had no DbSet and no local query
    /// surface -- reachable only through other entities' navigations and impossible to create,
    /// while ICurriculumQueries was a hub HTTP client behind a closed gate. A standalone node MUST
    /// author its own content: the hub-side content developer portal is hub-private and never
    /// ships. Uses the EF InMemory provider, matching LocalModuleCatalogTests.
    /// </summary>
    public class LocalCurriculumAuthoringTests
    {
        private static DataDbContext BuildContext(string dbName)
        {
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new DataDbContext(options);
        }

        private static Curriculum NewCurriculum(string name)
        {
            return new Curriculum()
            {
                UUID = Guid.NewGuid(),
                Name = name,
                Description = name + " description",
                Version = "1.0"
            };
        }

        [Fact]
        public async Task Upsert_CreatesThenUpdatesByUuid_WithoutDuplicating()
        {
            using DataDbContext context = BuildContext(nameof(Upsert_CreatesThenUpdatesByUuid_WithoutDuplicating));
            ICurriculumQueries queries = new CurriculumQueries(context);

            Curriculum created = await queries.Upsert(NewCurriculum("Welding"));
            created.Should().NotBeNull();

            created.Name = "Welding II";
            created.Version = "2.0";
            await queries.Upsert(created);

            List<Curriculum> all = await context.Curriculum.AsNoTracking().ToListAsync();
            all.Should().ContainSingle();
            all[0].Name.Should().Be("Welding II");
            all[0].Version.Should().Be("2.0");
        }

        [Fact]
        public async Task Upsert_DoesNotInsertADuplicateClassification_WhenTheNavigationIsPopulated()
        {
            using DataDbContext context = BuildContext(nameof(Upsert_DoesNotInsertADuplicateClassification_WhenTheNavigationIsPopulated));

            CurriculumClassification safety = new CurriculumClassification()
            {
                UUID = Guid.NewGuid(),
                Name = "Safety"
            };
            context.CurriculumClassification.Add(safety);
            await context.SaveChangesAsync();

            ICurriculumQueries queries = new CurriculumQueries(context);

            // A model-bound form can arrive with a STUB navigation populated. EF cascades inserts
            // through navigations, so without the guard in Upsert this creates a second
            // classification row on every authored curriculum.
            Curriculum input = NewCurriculum("Confined Space");
            input.CurriculumClassificationUUID = safety.UUID;
            input.CurriculumClassification = new CurriculumClassification()
            {
                UUID = Guid.NewGuid(),
                Name = "Safety"
            };

            await queries.Upsert(input);

            List<CurriculumClassification> classifications =
                await context.CurriculumClassification.AsNoTracking().ToListAsync();
            classifications.Should().ContainSingle("the FK carries the relationship, not the navigation");
            classifications[0].UUID.Should().Be(safety.UUID);
        }

        [Fact]
        public async Task Get_HidesObsolete_ButGetIncludingObsoleteDoesNot()
        {
            using DataDbContext context = BuildContext(nameof(Get_HidesObsolete_ButGetIncludingObsoleteDoesNot));
            ICurriculumQueries queries = new CurriculumQueries(context);

            Curriculum live = await queries.Upsert(NewCurriculum("Live"));
            Curriculum retired = await queries.Upsert(NewCurriculum("Retired"));
            await queries.SetObsolete(retired.Id, true);

            List<Curriculum> visible = await queries.Get();
            visible.Should().ContainSingle();
            visible[0].Name.Should().Be("Live");

            // Without this second surface an obsoleted curriculum is invisible and therefore
            // impossible to restore -- soft delete behaving like a hard delete.
            List<Curriculum> all = await queries.GetIncludingObsolete();
            all.Should().HaveCount(2);
            all.Select(i => i.Name).Should().BeEquivalentTo(new[] { "Live", "Retired" });
        }

        [Fact]
        public async Task BllGet_SwitchesBetweenLiveAndArchived_SoRetiredCurriculaAreRecoverable()
        {
            using DataDbContext context = BuildContext(nameof(BllGet_SwitchesBetweenLiveAndArchived_SoRetiredCurriculaAreRecoverable));
            ICurriculumQueries queries = new CurriculumQueries(context);

            await queries.Upsert(NewCurriculum("Live"));
            Curriculum retired = await queries.Upsert(NewCurriculum("Retired"));
            await queries.SetObsolete(retired.Id, true);

            // This is the pair the archived view depends on. If Get(false) were the only surface,
            // an archived curriculum could never be shown again and so could never be restored --
            // soft delete would be indistinguishable from a hard one for the operator.
            (await queries.Get()).Select(i => i.Name).Should().BeEquivalentTo(new[] { "Live" });
            (await queries.GetIncludingObsolete()).Select(i => i.Name)
                .Should().BeEquivalentTo(new[] { "Live", "Retired" });

            // And restoring puts it back in the default list.
            await queries.SetObsolete(retired.Id, false);
            (await queries.Get()).Select(i => i.Name).Should().BeEquivalentTo(new[] { "Live", "Retired" });
        }

        [Fact]
        public async Task SetObsolete_RoundTrips_AndReportsMissingRows()
        {
            using DataDbContext context = BuildContext(nameof(SetObsolete_RoundTrips_AndReportsMissingRows));
            ICurriculumQueries queries = new CurriculumQueries(context);

            Curriculum c = await queries.Upsert(NewCurriculum("Reversible"));

            (await queries.SetObsolete(c.Id, true)).Should().BeTrue();
            (await queries.Get(c.Id)).Obsolete.Should().BeTrue();

            // Restorable, which is the whole point of soft delete.
            (await queries.SetObsolete(c.Id, false)).Should().BeTrue();
            (await queries.Get(c.Id)).Obsolete.Should().BeFalse();

            (await queries.SetObsolete(999999, true)).Should().BeFalse("a missing row is reported, not thrown");
        }

        [Fact]
        public async Task Get_ByIdAndUuid_ResolveTheSameRow_AndReturnNullOnMiss()
        {
            using DataDbContext context = BuildContext(nameof(Get_ByIdAndUuid_ResolveTheSameRow_AndReturnNullOnMiss));
            ICurriculumQueries queries = new CurriculumQueries(context);

            Curriculum c = await queries.Upsert(NewCurriculum("Resolvable"));

            (await queries.Get(c.Id)).UUID.Should().Be(c.UUID);
            (await queries.Get(c.UUID)).Id.Should().Be(c.Id);

            (await queries.Get((long?)null)).Should().BeNull();
            (await queries.Get((Guid?)null)).Should().BeNull();
            (await queries.Get(Guid.NewGuid())).Should().BeNull();
        }

        /// <summary>
        /// ROADMAP 11. The third appearance of the C-07 clobber, after Cohort.Archive and
        /// Cohort.LockMembers.
        ///
        /// <para>
        /// Views/Curriculum/Edit.cshtml renders the obsolete switch <c>disabled</c>, deliberately,
        /// because obsoleting is a distinct role-gated action. The disabled CHECKBOX posts nothing,
        /// but the tag helper emits a COMPANION HIDDEN FIELD for a bool property which is NOT
        /// disabled, so the browser posts <c>Curriculum.Obsolete=false</c> explicitly. The update
        /// branch wrote that over the stored value, and a retired curriculum came back to life
        /// because somebody corrected its description, with nothing saying so.
        /// </para>
        /// <para>
        /// This test drives the exact binder outcome rather than a hand-set flag: the posted object
        /// carries <c>false</c>, which is precisely what the form sends.
        /// </para>
        /// </summary>
        [Fact]
        public async Task EditingACurriculum_DoesNotSilentlyUnObsoleteIt()
        {
            using DataDbContext context = BuildContext(nameof(EditingACurriculum_DoesNotSilentlyUnObsoleteIt));
            ICurriculumQueries queries = new CurriculumQueries(context);

            Curriculum created = await queries.Upsert(NewCurriculum("Retired Course"));
            (await queries.SetObsolete(created.Id, true)).Should().BeTrue();

            // What the Edit POST actually hands the DAL: every editable field, and Obsolete left at
            // default(bool) because the disabled checkbox posted nothing.
            Curriculum posted = new Curriculum()
            {
                Id = created.Id,
                UUID = created.UUID,
                Name = "Retired Course, renamed",
                Description = created.Description,
                Version = "1.1",
                Obsolete = false
            };
            await queries.Upsert(posted);

            Curriculum stored = await context.Curriculum.AsNoTracking()
                .FirstAsync(i => i.UUID == created.UUID);
            stored.Name.Should().Be("Retired Course, renamed", "the editable fields still save");
            stored.Version.Should().Be("1.1");
            stored.Obsolete.Should().BeTrue("editing a name must not resurrect a retired curriculum");
        }

        /// <summary>
        /// The other direction, so the fix is not simply "Obsolete can never change". SetObsolete
        /// remains the sole writer for an existing row, and it still works in both directions.
        /// A flag that could only be set would be a one-way door, which is the trap ROADMAP 11
        /// recorded for curricula in the first place.
        /// </summary>
        [Fact]
        public async Task SetObsolete_IsStillTheWriter_InBothDirections_AfterAnEdit()
        {
            using DataDbContext context = BuildContext(nameof(SetObsolete_IsStillTheWriter_InBothDirections_AfterAnEdit));
            ICurriculumQueries queries = new CurriculumQueries(context);

            Curriculum created = await queries.Upsert(NewCurriculum("Round Trip"));
            await queries.SetObsolete(created.Id, true);

            // A DETACHED object, because that is what model binding produces. Reusing the instance
            // Upsert returned would mutate the row EF is already tracking, so SaveChanges would
            // persist the mutation whatever the update branch copies -- the test would fail against
            // correct code. Worth stating: Upsert's field-copying contract only holds for a
            // detached input, which every real caller supplies.
            await queries.Upsert(new Curriculum()
            {
                Id = created.Id,
                UUID = created.UUID,
                Name = "Round Trip, edited",
                Description = created.Description,
                Version = created.Version,
                Obsolete = false          // the binder default again
            });
            (await queries.Get(created.Id)).Obsolete.Should().BeTrue();

            (await queries.SetObsolete(created.Id, false)).Should().BeTrue();
            (await queries.Get(created.Id)).Obsolete.Should().BeFalse("un-obsoleting through the real action still works");
        }

        /// <summary>
        /// A create still honours the flag it is given. The fix removes the write from the UPDATE
        /// branch only, and a guard that also froze creation would be too broad.
        /// </summary>
        [Fact]
        public async Task Upsert_StillHonoursObsolete_WhenCreatingARow()
        {
            using DataDbContext context = BuildContext(nameof(Upsert_StillHonoursObsolete_WhenCreatingARow));
            ICurriculumQueries queries = new CurriculumQueries(context);

            Curriculum seeded = NewCurriculum("Born Retired");
            seeded.Obsolete = true;

            Curriculum created = await queries.Upsert(seeded);
            created.Obsolete.Should().BeTrue();

            // Get(long?) deliberately does NOT filter obsolete -- a retired row has to be fetchable
            // in order to be un-retired. It is the LIST read that hides them.
            (await queries.Get(created.Id)).Obsolete.Should().BeTrue();
            (await queries.Get()).Should().NotContain(i => i.UUID == seeded.UUID, "the list read hides obsolete rows");
            (await queries.GetIncludingObsolete()).Should().ContainSingle(i => i.UUID == seeded.UUID);
        }

        /// <summary>
        /// ROADMAP 11, the classification link. The headline: the UUID column is NOT the
        /// relationship.
        ///
        /// <para>
        /// EF built a SHADOW foreign key, <c>CurriculumClassificationId</c>, and that column plus
        /// its index and real database FK shipped in the Initial migration.
        /// <c>CurriculumClassificationUUID</c> is an unconstrained Guid that joins to nothing, so
        /// the write path recorded the operator's choice somewhere no read could follow it and the
        /// navigation came back null every time. Same shape as <c>CohortMember.CohortUUID</c>
        /// versus its shadow <c>CohortId</c>.
        /// </para>
        /// <para>
        /// Asserting on the NAVIGATION rather than the UUID column is the whole point. A test that
        /// checked the UUID round-tripped would have passed against the broken code, which is
        /// exactly how this survived.
        /// </para>
        /// </summary>
        [Fact]
        public async Task Upsert_StoresTheRealClassificationLink_NotJustTheUnconstrainedUuid()
        {
            using DataDbContext context = BuildContext(nameof(Upsert_StoresTheRealClassificationLink_NotJustTheUnconstrainedUuid));
            ICurriculumQueries queries = new CurriculumQueries(context);

            CurriculumClassification classification = new CurriculumClassification()
            {
                UUID = Guid.NewGuid(),
                Name = "Safety",
                Description = "Safety courses"
            };
            context.CurriculumClassification.Add(classification);
            await context.SaveChangesAsync();

            Curriculum toCreate = NewCurriculum("Confined Space");
            toCreate.CurriculumClassificationUUID = classification.UUID;
            Curriculum created = await queries.Upsert(toCreate);

            // The read path Includes the navigation. Before the fix this was null.
            Curriculum read = await queries.Get(created.Id);
            read.CurriculumClassification.Should().NotBeNull("the shadow foreign key must actually be populated");
            read.CurriculumClassification.Name.Should().Be("Safety");
            read.CurriculumClassificationUUID.Should().Be(classification.UUID, "the UUID column is still written too");

            // And the principal was not duplicated, which is what the Add branch's null-out guard
            // has always been defending against.
            (await context.CurriculumClassification.AsNoTracking().ToListAsync())
                .Should().ContainSingle("assigning the navigation must not insert a second classification");
        }

        /// <summary>
        /// "[None]" has to be able to REMOVE a link, or the picker is a one-way door -- the same
        /// trap ROADMAP 11 originally recorded for obsolete curricula.
        /// </summary>
        [Fact]
        public async Task Upsert_ClearsTheClassificationLink_WhenTheOperatorPicksNone()
        {
            using DataDbContext context = BuildContext(nameof(Upsert_ClearsTheClassificationLink_WhenTheOperatorPicksNone));
            ICurriculumQueries queries = new CurriculumQueries(context);

            CurriculumClassification classification = new CurriculumClassification()
            {
                UUID = Guid.NewGuid(),
                Name = "Safety"
            };
            context.CurriculumClassification.Add(classification);
            await context.SaveChangesAsync();

            Curriculum toCreate = NewCurriculum("Reclassified");
            toCreate.CurriculumClassificationUUID = classification.UUID;
            Curriculum created = await queries.Upsert(toCreate);
            (await queries.Get(created.Id)).CurriculumClassification.Should().NotBeNull();

            await queries.Upsert(new Curriculum()
            {
                Id = created.Id,
                UUID = created.UUID,
                Name = created.Name,
                Description = created.Description,
                Version = created.Version,
                CurriculumClassificationUUID = null
            });

            Curriculum read = await queries.Get(created.Id);
            read.CurriculumClassification.Should().BeNull("picking [None] must clear the link");
            read.CurriculumClassificationUUID.Should().BeNull();
        }

        /// <summary>
        /// The UPDATE path, which is where the operator actually re-classifies something.
        ///
        /// <para>
        /// Added because the mutation run caught a hole in my own first draft: every classification
        /// test above drives the CREATE branch, so a mutation that stripped the link assignment from
        /// the UPDATE branch stayed green. Editing a curriculum's classification was unpinned.
        /// </para>
        /// </summary>
        [Fact]
        public async Task Upsert_ChangesTheClassificationLink_WhenTheOperatorRe_Classifies()
        {
            using DataDbContext context = BuildContext(nameof(Upsert_ChangesTheClassificationLink_WhenTheOperatorRe_Classifies));
            ICurriculumQueries queries = new CurriculumQueries(context);

            CurriculumClassification safety = new CurriculumClassification() { UUID = Guid.NewGuid(), Name = "Safety" };
            CurriculumClassification clinical = new CurriculumClassification() { UUID = Guid.NewGuid(), Name = "Clinical" };
            context.CurriculumClassification.AddRange(safety, clinical);
            await context.SaveChangesAsync();

            Curriculum toCreate = NewCurriculum("Reclassify Me");
            toCreate.CurriculumClassificationUUID = safety.UUID;
            Curriculum created = await queries.Upsert(toCreate);
            (await queries.Get(created.Id)).CurriculumClassification.Name.Should().Be("Safety");

            await queries.Upsert(new Curriculum()
            {
                Id = created.Id,
                UUID = created.UUID,
                Name = created.Name,
                Description = created.Description,
                Version = created.Version,
                CurriculumClassificationUUID = clinical.UUID
            });

            Curriculum read = await queries.Get(created.Id);
            read.CurriculumClassification.Should().NotBeNull();
            read.CurriculumClassification.Name.Should().Be("Clinical", "re-classifying on an edit must move the real link, not just the UUID column");
            read.CurriculumClassificationUUID.Should().Be(clinical.UUID);
        }

        /// <summary>
        /// The UUID column is unconstrained, so a stale or hand-edited value is possible. It must
        /// not throw and must not lose the operator's other edits.
        ///
        /// <para>
        /// The UPDATE half was added after the mutation run: on a CREATE there is no link yet, so
        /// "leave it alone" and "set it to null" are indistinguishable, and a mutation that dropped
        /// the null check stayed green. Against an EXISTING link they differ, and that difference is
        /// the whole point of the check -- a stale UUID must not silently strip a good
        /// classification.
        /// </para>
        /// </summary>
        [Fact]
        public async Task Upsert_LeavesTheLinkAlone_WhenTheUuidNamesNoClassification()
        {
            using DataDbContext context = BuildContext(nameof(Upsert_LeavesTheLinkAlone_WhenTheUuidNamesNoClassification));
            ICurriculumQueries queries = new CurriculumQueries(context);

            // Create path: a dangling UUID must not fail the save.
            Curriculum toCreate = NewCurriculum("Orphan Link");
            toCreate.CurriculumClassificationUUID = Guid.NewGuid();   // names no row

            Curriculum created = await queries.Upsert(toCreate);
            created.Should().NotBeNull("a dangling classification UUID must not fail the whole save");

            Curriculum read = await queries.Get(created.Id);
            read.Name.Should().Be("Orphan Link");
            read.CurriculumClassification.Should().BeNull();

            // Update path: an existing GOOD link must survive a dangling UUID.
            CurriculumClassification safety = new CurriculumClassification() { UUID = Guid.NewGuid(), Name = "Safety" };
            context.CurriculumClassification.Add(safety);
            await context.SaveChangesAsync();

            Curriculum linked = await queries.Upsert(new Curriculum()
            {
                Id = created.Id,
                UUID = created.UUID,
                Name = created.Name,
                Description = created.Description,
                Version = created.Version,
                CurriculumClassificationUUID = safety.UUID
            });
            (await queries.Get(linked.Id)).CurriculumClassification.Should().NotBeNull();

            await queries.Upsert(new Curriculum()
            {
                Id = created.Id,
                UUID = created.UUID,
                Name = "Edited With A Stale Uuid",
                Description = created.Description,
                Version = created.Version,
                CurriculumClassificationUUID = Guid.NewGuid()   // names no row again
            });

            Curriculum afterStale = await queries.Get(created.Id);
            afterStale.Name.Should().Be("Edited With A Stale Uuid", "the other edits still save");
            afterStale.CurriculumClassification.Should().NotBeNull("a stale UUID must not strip a good classification");
            afterStale.CurriculumClassification.Name.Should().Be("Safety");
        }
    }
}
