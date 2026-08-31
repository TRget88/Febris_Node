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
    /// Pins the node-local module catalog (delivery-path severance): the
    /// node OWNS Module / ModuleClassification / ModuleLinkedObject in its own DataDbContext --
    /// rows created by the package-ingest path, resolved with zero HTTP -- instead of re-fetching
    /// them from central on every launcher initialize / statement initialization. Uses the EF
    /// InMemory provider: relational/Npgsql annotations (uuid defaults) are metadata-only there,
    /// so entities set their own UUIDs where the database would normally do it.
    /// </summary>
    public class LocalModuleCatalogTests
    {
        private static DataDbContext BuildContext(string dbName)
        {
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new DataDbContext(options);
        }

        private static Module NewModule(string name, bool obsolete = false)
        {
            return new Module()
            {
                UUID = Guid.NewGuid(),
                Name = name,
                Version = "1.0",
                Description = name + " description",
                Obsolete = obsolete,
                ModuleClassification = new ModuleClassification() { UUID = Guid.NewGuid(), Name = "Training" }
            };
        }

        [Fact]
        public async Task ModuleQueries_ResolveLocally_ByUuidAndBatch_IncludingClassification()
        {
            using DataDbContext context = BuildContext(nameof(ModuleQueries_ResolveLocally_ByUuidAndBatch_IncludingClassification));
            Module welding = NewModule("Welding 101");
            Module lockout = NewModule("Lockout Tagout");
            context.Module.AddRange(welding, lockout);
            context.SaveChanges();
            ModuleQueries queries = new ModuleQueries(context);

            // The launcher initialize path resolves the hardware's linked modules by UUID batch.
            List<Module> batch = await queries.Get(new List<Guid>() { welding.UUID, lockout.UUID });
            batch.Should().HaveCount(2, "linked modules must resolve from the local catalog with zero HTTP");

            // Single-module resolution (download path) must include the classification graph the
            // remote path used to return serialized.
            Module byUuid = await queries.Get((Guid?)welding.UUID);
            byUuid.Should().NotBeNull();
            byUuid.ModuleClassification.Should().NotBeNull("the remote path returned the full graph; the local twin must Include it");
        }

        [Fact]
        public async Task ModuleQueries_GetByLicense_ReturnsTheLocalNonObsoleteCatalog()
        {
            using DataDbContext context = BuildContext(nameof(ModuleQueries_GetByLicense_ReturnsTheLocalNonObsoleteCatalog));
            context.Module.AddRange(NewModule("Current"), NewModule("Retired", obsolete: true));
            context.SaveChanges();
            ModuleQueries queries = new ModuleQueries(context);

            // ModuleLogic.GetAccessableModules asks "what may this deployment deliver": previously
            // a central license expansion, now the node's own non-obsolete catalog.
            List<Module> accessible = await queries.GetByLicense();
            accessible.Should().ContainSingle(m => m.Name == "Current");
        }

        [Fact]
        public async Task ModuleQueries_Get_ReturnsNullOnMiss()
        {
            using DataDbContext context = BuildContext(nameof(ModuleQueries_Get_ReturnsNullOnMiss));
            ModuleQueries queries = new ModuleQueries(context);

            (await queries.Get((Guid?)Guid.NewGuid())).Should().BeNull();
            (await queries.Get((long?)42)).Should().BeNull();
        }

        [Fact]
        public async Task ModuleQueries_Upsert_CreatesThenUpdatesByUuid_WithoutDuplicating()
        {
            using DataDbContext context = BuildContext(nameof(ModuleQueries_Upsert_CreatesThenUpdatesByUuid_WithoutDuplicating));
            ModuleQueries queries = new ModuleQueries(context);
            Module first = NewModule("Confined Spaces");

            await queries.Upsert(first);
            context.Module.Count().Should().Be(1);

            // Re-ingesting the same package UUID with new metadata must update in place.
            Module reingest = new Module() { UUID = first.UUID, Name = "Confined Spaces", Version = "1.1" };
            Module updated = await queries.Upsert(reingest);

            context.Module.Count().Should().Be(1, "same catalog UUID must not duplicate");
            updated.Version.Should().Be("1.1");
        }

        [Fact]
        public async Task ModuleLinkedObjectQueries_GetByModule_ResolvesTheLinkLocally_WithTheModuleGraph()
        {
            using DataDbContext context = BuildContext(nameof(ModuleLinkedObjectQueries_GetByModule_ResolvesTheLinkLocally_WithTheModuleGraph));
            Module module = NewModule("Forklift Basics");
            context.Module.Add(module);
            context.ModuleLinkedObject.Add(new ModuleLinkedObject()
            {
                UUID = Guid.NewGuid(),
                Module = module,
                ModuleUUID = module.UUID,
                ObjectId = 7,
                ObjectUUID = Guid.NewGuid()
            });
            context.SaveChanges();
            ModuleLinkedObjectQueries queries = new ModuleLinkedObjectQueries(context);

            // LauncherLogic.InitalizeStatement resolves the module's xAPI activity through this
            // link, then dereferences link.Module.ModuleClassification -- the Include chain must hold.
            ModuleLinkedObject link = await queries.GetByModule(module.UUID);
            link.Should().NotBeNull("statement initialization must resolve the link with zero HTTP");
            link.ObjectId.Should().Be(7);
            link.Module.Should().NotBeNull();
            link.Module.ModuleClassification.Should().NotBeNull();

            (await queries.GetByModule(Guid.NewGuid())).Should().BeNull();
        }

        [Fact]
        public async Task ModuleLinkedObjectQueries_Upsert_RepointsTheExistingLinkOnReingest()
        {
            using DataDbContext context = BuildContext(nameof(ModuleLinkedObjectQueries_Upsert_RepointsTheExistingLinkOnReingest));
            ModuleLinkedObjectQueries queries = new ModuleLinkedObjectQueries(context);
            Guid moduleUuid = Guid.NewGuid();

            await queries.Upsert(new ModuleLinkedObject() { UUID = Guid.NewGuid(), ModuleUUID = moduleUuid, ObjectId = 1, ObjectUUID = Guid.NewGuid() });
            Guid newObjectUuid = Guid.NewGuid();
            await queries.Upsert(new ModuleLinkedObject() { UUID = Guid.NewGuid(), ModuleUUID = moduleUuid, ObjectId = 2, ObjectUUID = newObjectUuid });

            context.ModuleLinkedObject.Count().Should().Be(1, "one module carries exactly one activity link");
            ModuleLinkedObject link = context.ModuleLinkedObject.Single();
            link.ObjectId.Should().Be(2);
            link.ObjectUUID.Should().Be(newObjectUuid);
        }
    }
}
