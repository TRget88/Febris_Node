// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the node-local Module-to-Curriculum join (owner ruling 2026-08-01: modules belong to
    /// curricula). This link previously existed ONLY hub-side behind Remote/ModuleLinkedCurriculum
    /// Queries, gated closed, so every curriculum on a self-hosted node reported zero modules.
    /// With this store the whole chain is local:
    /// Cohort -&gt; CohortLinkedCurriculum -&gt; Curriculum -&gt; ModuleLinkedCurriculum -&gt; Module.
    /// Uses the EF InMemory provider, matching LocalModuleCatalogTests.
    /// </summary>
    public class LocalModuleCurriculumLinkTests
    {
        private static DataDbContext BuildContext(string dbName)
        {
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new DataDbContext(options);
        }

        private static Module NewModule(string name)
        {
            return new Module()
            {
                UUID = Guid.NewGuid(),
                Name = name,
                Version = "1.0",
                Description = name + " description",
                ModuleClassification = new ModuleClassification() { UUID = Guid.NewGuid(), Name = "Training" }
            };
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

        private static ModuleLinkedCurriculum Link(Module module, Curriculum curriculum)
        {
            return new ModuleLinkedCurriculum()
            {
                UUID = Guid.NewGuid(),
                Module = module,
                ModuleUUID = module.UUID,
                Curriculum = curriculum,
                CurriculumUUID = curriculum.UUID
            };
        }

        [Fact]
        public async Task Get_ReturnsOnlyThatCurriculumsModules_WithTheModuleGraph()
        {
            using DataDbContext context = BuildContext(nameof(Get_ReturnsOnlyThatCurriculumsModules_WithTheModuleGraph));

            Curriculum welding = NewCurriculum("Welding");
            Curriculum unrelated = NewCurriculum("Unrelated");
            Module cutting = NewModule("Cutting");
            Module grinding = NewModule("Grinding");
            Module elsewhere = NewModule("Elsewhere");

            context.ModuleLinkedCurriculum.AddRange(
                Link(cutting, welding), Link(grinding, welding), Link(elsewhere, unrelated));
            await context.SaveChangesAsync();

            IModuleLinkedCurriculumQueries queries = new ModuleLinkedCurriculumQueries(context);
            List<ModuleLinkedCurriculum> result = await queries.Get(welding);

            result.Should().HaveCount(2);
            result.Select(i => i.Module.Name).Should().BeEquivalentTo(new[] { "Cutting", "Grinding" });
            // The classification comes through ThenInclude; a null graph would render blank rows.
            result.Should().OnlyContain(i => i.Module.ModuleClassification != null);
            result.Should().NotContain(i => i.Module.Name == "Elsewhere");
        }

        [Fact]
        public async Task GetByModule_ReturnsEveryCurriculumTheModuleBelongsTo()
        {
            using DataDbContext context = BuildContext(nameof(GetByModule_ReturnsEveryCurriculumTheModuleBelongsTo));

            Module shared = NewModule("Shared Safety");
            Curriculum welding = NewCurriculum("Welding");
            Curriculum electrical = NewCurriculum("Electrical");

            context.ModuleLinkedCurriculum.AddRange(Link(shared, welding), Link(shared, electrical));
            await context.SaveChangesAsync();

            IModuleLinkedCurriculumQueries queries = new ModuleLinkedCurriculumQueries(context);
            List<ModuleLinkedCurriculum> result = await queries.GetByModule(shared.UUID);

            // A module belonging to several curricula is the whole reason this is a join table.
            result.Should().HaveCount(2);
            result.Select(i => i.Curriculum.Name).Should().BeEquivalentTo(new[] { "Welding", "Electrical" });
        }

        [Fact]
        public async Task Upsert_MatchesOnThePair_SoItNeverDuplicatesOrRepointsAnUnrelatedRow()
        {
            using DataDbContext context = BuildContext(nameof(Upsert_MatchesOnThePair_SoItNeverDuplicatesOrRepointsAnUnrelatedRow));

            Module shared = NewModule("Shared Safety");
            Curriculum welding = NewCurriculum("Welding");
            Curriculum electrical = NewCurriculum("Electrical");
            context.Module.Add(shared);
            context.AddRange(welding, electrical);
            await context.SaveChangesAsync();

            IModuleLinkedCurriculumQueries queries = new ModuleLinkedCurriculumQueries(context);

            await queries.Upsert(Link(shared, welding));
            await queries.Upsert(Link(shared, electrical));
            // Re-ingesting the SAME pair must update, not duplicate.
            await queries.Upsert(Link(shared, welding));

            List<ModuleLinkedCurriculum> all = await context.ModuleLinkedCurriculum.AsNoTracking().ToListAsync();
            all.Should().HaveCount(2);
            // Keying on the module alone would have collapsed these two into one.
            all.Select(i => i.CurriculumUUID).Should().BeEquivalentTo(new[] { welding.UUID, electrical.UUID });
        }

        [Fact]
        public async Task Remove_DropsOnlyTheNamedPair_AndIsANoOpWhenAbsent()
        {
            using DataDbContext context = BuildContext(nameof(Remove_DropsOnlyTheNamedPair_AndIsANoOpWhenAbsent));

            Module shared = NewModule("Shared Safety");
            Curriculum welding = NewCurriculum("Welding");
            Curriculum electrical = NewCurriculum("Electrical");
            context.ModuleLinkedCurriculum.AddRange(Link(shared, welding), Link(shared, electrical));
            await context.SaveChangesAsync();

            IModuleLinkedCurriculumQueries queries = new ModuleLinkedCurriculumQueries(context);

            await queries.Remove(shared.UUID, welding.UUID);
            List<ModuleLinkedCurriculum> after = await context.ModuleLinkedCurriculum.AsNoTracking().ToListAsync();
            after.Should().ContainSingle();
            after[0].CurriculumUUID.Should().Be(electrical.UUID);

            // Absent pair: must not throw and must not delete anything else.
            await queries.Remove(shared.UUID, Guid.NewGuid());
            (await context.ModuleLinkedCurriculum.AsNoTracking().ToListAsync()).Should().ContainSingle();
        }

        [Fact]
        public async Task Upsert_LinksDETACHEDEndpoints_WithoutReinsertingThem()
        {
            string db = nameof(Upsert_LinksDETACHEDEndpoints_WithoutReinsertingThem);

            Guid moduleUuid;
            Guid curriculumUuid;
            using (DataDbContext seed = BuildContext(db))
            {
                Module m = NewModule("Detached Module");
                Curriculum c = NewCurriculum("Detached Curriculum");
                seed.Module.Add(m);
                seed.Curriculum.Add(c);
                await seed.SaveChangesAsync();
                moduleUuid = m.UUID;
                curriculumUuid = c.UUID;
            }

            // Read them back through a SEPARATE context with AsNoTracking -- exactly what the BLL
            // does. These instances are DETACHED. Linking them used to make EF treat both endpoints
            // as new and re-INSERT them, failing live with
            // "23505: duplicate key value violates unique constraint PK_Curriculum".
            // The earlier tests missed it because they added entities to the same context, so those
            // instances were already tracked.
            using DataDbContext context = BuildContext(db);
            Module detachedModule = await context.Module.AsNoTracking().FirstAsync(i => i.UUID == moduleUuid);
            Curriculum detachedCurriculum = await context.Curriculum.AsNoTracking().FirstAsync(i => i.UUID == curriculumUuid);

            IModuleLinkedCurriculumQueries queries = new ModuleLinkedCurriculumQueries(context);
            await queries.Upsert(Link(detachedModule, detachedCurriculum));

            (await context.ModuleLinkedCurriculum.AsNoTracking().CountAsync()).Should().Be(1);
            // The endpoints must NOT have been duplicated.
            (await context.Curriculum.AsNoTracking().CountAsync()).Should().Be(1);
            (await context.Module.AsNoTracking().CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task Get_ReturnsEmptyNotNull_ForNullOrUnlinkedCurriculum()
        {
            using DataDbContext context = BuildContext(nameof(Get_ReturnsEmptyNotNull_ForNullOrUnlinkedCurriculum));

            IModuleLinkedCurriculumQueries queries = new ModuleLinkedCurriculumQueries(context);

            // Callers foreach straight over this; null would NRE rather than render an empty list.
            (await queries.Get(null)).Should().NotBeNull().And.BeEmpty();
            (await queries.Get(NewCurriculum("Nothing Linked"))).Should().NotBeNull().And.BeEmpty();
            (await queries.GetByModule(null)).Should().NotBeNull().And.BeEmpty();
        }
    }
}
