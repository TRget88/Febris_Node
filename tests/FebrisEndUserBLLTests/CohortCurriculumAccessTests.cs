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
    /// Pins the curriculum-derived cohort access list. A cohort's access used to be computed from
    /// what its members had BOUGHT -- IPurchaseLogic.GetByUsers, a hub call that returns nothing on
    /// a self-hosted node, so the table rendered empty forever. Access is now what curricula the
    /// cohort is LINKED to (CohortLinkedCurriculum), which is node-local schema that already
    /// existed. Uses the EF InMemory provider, matching LocalModuleCatalogTests.
    /// </summary>
    public class CohortCurriculumAccessTests
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
        public async Task GetListByCohort_ReturnsTheCohortsCurricula_WithTheCurriculumGraph()
        {
            using DataDbContext context = BuildContext(nameof(GetListByCohort_ReturnsTheCohortsCurricula_WithTheCurriculumGraph));

            Cohort cohort = new Cohort() { UUID = Guid.NewGuid(), Name = "Fall Intake" };
            Cohort other = new Cohort() { UUID = Guid.NewGuid(), Name = "Unrelated" };
            Curriculum welding = NewCurriculum("Welding");
            Curriculum safety = NewCurriculum("Safety");
            Curriculum hidden = NewCurriculum("Not This Cohort");

            context.Cohort.AddRange(cohort, other);
            context.CohortLinkedCurriculum.AddRange(
                new CohortLinkedCurriculum() { UUID = Guid.NewGuid(), Cohort = cohort, Curriculum = welding },
                new CohortLinkedCurriculum() { UUID = Guid.NewGuid(), Cohort = cohort, Curriculum = safety },
                new CohortLinkedCurriculum() { UUID = Guid.NewGuid(), Cohort = other, Curriculum = hidden });
            await context.SaveChangesAsync();

            CohortLinkedCurriculumQueries queries = new CohortLinkedCurriculumQueries(context);
            List<CohortLinkedCurriculum> result = await queries.GetListByCohort(cohort);

            result.Should().HaveCount(2);
            // The Curriculum navigation must be populated -- the access list reads Name/Description
            // off it, and a null navigation would render blank rows rather than fail loudly.
            result.Should().OnlyContain(i => i.Curriculum != null);
            result.Select(i => i.Curriculum.Name).Should().BeEquivalentTo(new[] { "Welding", "Safety" });
            result.Should().NotContain(i => i.Curriculum.Name == "Not This Cohort");
        }

        [Fact]
        public async Task GetListByCohort_IsExposedOnTheInterface_SoDiCallersCanReachIt()
        {
            using DataDbContext context = BuildContext(nameof(GetListByCohort_IsExposedOnTheInterface_SoDiCallersCanReachIt));

            Cohort cohort = new Cohort() { UUID = Guid.NewGuid(), Name = "Interface Check" };
            context.Cohort.Add(cohort);
            context.CohortLinkedCurriculum.Add(
                new CohortLinkedCurriculum() { UUID = Guid.NewGuid(), Cohort = cohort, Curriculum = NewCurriculum("Basics") });
            await context.SaveChangesAsync();

            // Deliberately through the INTERFACE: GetListByCohort existed on the concrete class
            // while ICohortLinkedCurriculumQueries was empty, so no DI consumer could call it.
            ICohortLinkedCurriculumQueries queries = new CohortLinkedCurriculumQueries(context);
            List<CohortLinkedCurriculum> result = await queries.GetListByCohort(cohort);

            result.Should().ContainSingle();
            result[0].Curriculum.Name.Should().Be("Basics");
        }

        [Fact]
        public async Task GetListByCohort_ReturnsEmpty_WhenTheCohortHasNoCurriculum()
        {
            using DataDbContext context = BuildContext(nameof(GetListByCohort_ReturnsEmpty_WhenTheCohortHasNoCurriculum));

            Cohort cohort = new Cohort() { UUID = Guid.NewGuid(), Name = "Empty" };
            context.Cohort.Add(cohort);
            await context.SaveChangesAsync();

            ICohortLinkedCurriculumQueries queries = new CohortLinkedCurriculumQueries(context);
            List<CohortLinkedCurriculum> result = await queries.GetListByCohort(cohort);

            // Empty, never null: the access list foreach would NRE on null.
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void CohortAccessEntry_CarriesCurriculum_NotAMarketplaceListing()
        {
            // Guards the severance itself. If a MarketplaceListing-shaped property ever comes back
            // on this view model, the node has been re-coupled to hub commerce.
            Type entry = typeof(Febris.ModelLibrary.ViewModels.CohortAccessEntryViewModel);

            entry.GetProperty("Curriculum").Should().NotBeNull();
            entry.GetProperty("Curriculum").PropertyType.Should().Be<Curriculum>();
            entry.GetProperty("MarketplaceListing").Should().BeNull();
        }
    }
}
