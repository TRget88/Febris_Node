// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.UserNode.LogicLayer.Logic.FederationLogic;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using XApiObject = Febris.ModelLibrary.Models.XApiModels.Object;
using XApiVerb = Febris.ModelLibrary.Models.XApiModels.Verb;
using XApiVersion = Febris.ModelLibrary.Models.XApiModels.Version;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the hub-pull sync (sub-slice 2): PULL-ONLY enrichment of the node's local
    /// vocabulary/catalog stores behind the ONE hub-federation gate.
    /// <list type="bullet">
    /// <item>gate closed = quiet no-op -- zero hub calls, zero store writes;</item>
    /// <item>additive-and-refresh by natural key -- hub-authored updates refresh matching local
    /// rows, local-only rows are NEVER deleted;</item>
    /// <item>per-domain added/updated/failed counts;</item>
    /// <item>failure isolation -- one domain failing never aborts the others;</item>
    /// <item>hub surrogate keys never leak into the local stores (the module link's ObjectId is
    /// re-resolved against the LOCAL activity row).</item>
    /// </list>
    /// Local stores are the REAL query classes over EF InMemory; the hub transport is mocked at
    /// the IHubSyncQueries seam.
    /// </summary>
    public class HubSyncLogicTests
    {
        private static readonly HubFederationSettings OpenGate = new HubFederationSettings()
        {
            Enabled = true,
            DataApi = "https://hub.example/api/"
        };

        private sealed class Stores : IDisposable
        {
            public DataDbContext DataDb { get; }
            public XApiDbContext XApiDb { get; }
            public VerbQueries Verbs { get; }
            public ObjectQueries Objects { get; }
            public VersionQueries Versions { get; }
            public ModuleQueries Modules { get; }
            public ModuleLinkedObjectQueries ModuleLinks { get; }

            public Stores(string dbName)
            {
                DataDb = new DataDbContext(new DbContextOptionsBuilder<DataDbContext>()
                    .UseInMemoryDatabase(dbName + ".data").Options);
                XApiDb = new XApiDbContext(new DbContextOptionsBuilder<XApiDbContext>()
                    .UseInMemoryDatabase(dbName + ".xapi").Options);
                Verbs = new VerbQueries(XApiDb);
                Objects = new ObjectQueries(XApiDb);
                Versions = new VersionQueries(XApiDb);
                Modules = new ModuleQueries(DataDb);
                ModuleLinks = new ModuleLinkedObjectQueries(DataDb);
            }

            public HubSyncLogic Logic(IHubSyncQueries hub, IHubFederationSettings gate = null)
            {
                return new HubSyncLogic(gate ?? OpenGate, hub, Verbs, Objects, Versions, Modules, ModuleLinks);
            }

            public void Dispose()
            {
                DataDb.Dispose();
                XApiDb.Dispose();
            }
        }

        /// <summary>A hub mock whose unspecified fetches return EMPTY (so single-domain tests
        /// need only set up their own domain).</summary>
        private static Mock<IHubSyncQueries> QuietHub()
        {
            var hub = new Mock<IHubSyncQueries>();
            hub.Setup(h => h.GetVerbs()).ReturnsAsync(new List<XApiVerb>());
            hub.Setup(h => h.GetObjects()).ReturnsAsync(new List<XApiObject>());
            hub.Setup(h => h.GetVersions()).ReturnsAsync(new List<XApiVersion>());
            hub.Setup(h => h.GetModulesByLicense()).ReturnsAsync(new List<Module>());
            hub.Setup(h => h.GetModuleLinkedObject(It.IsAny<Guid>())).ReturnsAsync((ModuleLinkedObject)null);
            return hub;
        }

        [Fact]
        public async Task SyncNow_GateClosed_IsAQuietNoOp_ZeroHubCalls()
        {
            using var stores = new Stores(nameof(SyncNow_GateClosed_IsAQuietNoOp_ZeroHubCalls));
            var hub = new Mock<IHubSyncQueries>(MockBehavior.Strict);   // ANY call throws

            HubSyncSummaryViewModel summary = await stores.Logic(hub.Object, HubFederationSettings.Disabled()).SyncNow();

            summary.SkippedGateClosed.Should().BeTrue("a closed gate must short-circuit before any HTTP");
            summary.Domains.Should().BeEmpty();
            hub.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SyncVerbs_AdditiveAndRefresh_LocalOnlyRowsPreserved()
        {
            using var stores = new Stores(nameof(SyncVerbs_AdditiveAndRefresh_LocalOnlyRowsPreserved));
            Uri sharedIri = new Uri("https://hub.example/verbs/attempted");
            Uri localOnlyIri = new Uri("https://this.node/verbs/homebrew");
            stores.XApiDb.Verb.AddRange(
                new XApiVerb() { UUID = Guid.NewGuid(), Id = sharedIri, Display = new Dictionary<string, string> { ["en"] = "stale display" } },
                new XApiVerb() { UUID = Guid.NewGuid(), Id = localOnlyIri, Display = new Dictionary<string, string> { ["en"] = "local-only" } });
            stores.XApiDb.SaveChanges();

            var hub = QuietHub();
            hub.Setup(h => h.GetVerbs()).ReturnsAsync(new List<XApiVerb>()
            {
                // Hub surrogate keys (Key=999...) must NOT leak into the local store.
                new XApiVerb() { Key = 999, UUID = Guid.NewGuid(), Id = sharedIri, Display = new Dictionary<string, string> { ["en"] = "hub display" } },
                new XApiVerb() { Key = 998, UUID = Guid.NewGuid(), Id = new Uri("https://hub.example/verbs/completed"), Display = new Dictionary<string, string> { ["en"] = "completed" } },
            });

            HubSyncSummaryViewModel summary = await stores.Logic(hub.Object).SyncNow();

            HubSyncDomainResultViewModel verbs = summary.Domains.Single(d => d.Domain == "Verbs");
            verbs.Added.Should().Be(1);
            verbs.Updated.Should().Be(1, "hub-authored updates SHOULD refresh matching rows -- local-wins would pin stale vocabulary");
            verbs.Failed.Should().Be(0);
            verbs.Error.Should().BeNull();

            List<XApiVerb> local = stores.XApiDb.Verb.AsNoTracking().ToList();
            local.Should().HaveCount(3, "additive-and-refresh NEVER deletes local-only rows");
            local.Single(v => v.Id == sharedIri).Display["en"].Should().Be("hub display");
            local.Single(v => v.Id == localOnlyIri).Display["en"].Should().Be("local-only", "the node's own vocabulary is untouched");
            local.Select(v => v.Key).Should().NotContain(new long[] { 998, 999 }, "hub surrogates must not leak");
        }

        [Fact]
        public async Task SyncVerbs_UnchangedRow_CountsNeitherAddedNorUpdated()
        {
            using var stores = new Stores(nameof(SyncVerbs_UnchangedRow_CountsNeitherAddedNorUpdated));
            Uri iri = new Uri("https://hub.example/verbs/attempted");
            stores.XApiDb.Verb.Add(new XApiVerb() { UUID = Guid.NewGuid(), Id = iri, Display = new Dictionary<string, string> { ["en"] = "same" } });
            stores.XApiDb.SaveChanges();

            var hub = QuietHub();
            hub.Setup(h => h.GetVerbs()).ReturnsAsync(new List<XApiVerb>()
            {
                new XApiVerb() { UUID = Guid.NewGuid(), Id = iri, Display = new Dictionary<string, string> { ["en"] = "same" } }
            });

            HubSyncSummaryViewModel summary = await stores.Logic(hub.Object).SyncNow();

            HubSyncDomainResultViewModel verbs = summary.Domains.Single(d => d.Domain == "Verbs");
            (verbs.Added + verbs.Updated + verbs.Failed).Should().Be(0, "an identical row is a no-op, not an update");
        }

        [Fact]
        public async Task SyncVersions_AddsMissingByVersionNumber_ExistingUntouched()
        {
            using var stores = new Stores(nameof(SyncVersions_AddsMissingByVersionNumber_ExistingUntouched));
            stores.XApiDb.Version.Add(new XApiVersion() { UUID = Guid.NewGuid(), VersionNumber = "1.0.3" });
            stores.XApiDb.SaveChanges();

            var hub = QuietHub();
            hub.Setup(h => h.GetVersions()).ReturnsAsync(new List<XApiVersion>()
            {
                new XApiVersion() { UUID = Guid.NewGuid(), VersionNumber = "1.0.3" },
                new XApiVersion() { UUID = Guid.NewGuid(), VersionNumber = "2.0.0" },
            });

            HubSyncSummaryViewModel summary = await stores.Logic(hub.Object).SyncNow();

            HubSyncDomainResultViewModel versions = summary.Domains.Single(d => d.Domain == "Versions");
            versions.Added.Should().Be(1);
            versions.Updated.Should().Be(0, "VersionNumber IS the payload; existence is the whole sync");
            stores.XApiDb.Version.AsNoTracking().Should().HaveCount(2);
        }

        [Fact]
        public async Task SyncModules_LicenseScopedCatalog_UpsertsByUuid_LocalOnlyPreserved()
        {
            using var stores = new Stores(nameof(SyncModules_LicenseScopedCatalog_UpsertsByUuid_LocalOnlyPreserved));
            Guid sharedUuid = Guid.NewGuid();
            stores.DataDb.Module.AddRange(
                new Module() { UUID = sharedUuid, Name = "Welding 101", Version = "1.0" },
                new Module() { UUID = Guid.NewGuid(), Name = "Node-only ingest", Version = "0.1" });
            stores.DataDb.SaveChanges();

            var hub = QuietHub();
            hub.Setup(h => h.GetModulesByLicense()).ReturnsAsync(new List<Module>()
            {
                new Module() { Id = 4242, UUID = sharedUuid, Name = "Welding 101", Version = "2.0", Description = "refreshed" },
                new Module() { Id = 4243, UUID = Guid.NewGuid(), Name = "Lockout Tagout", Version = "1.0" },
            });

            HubSyncSummaryViewModel summary = await stores.Logic(hub.Object).SyncNow();

            HubSyncDomainResultViewModel modules = summary.Domains.Single(d => d.Domain == "Modules");
            modules.Added.Should().Be(1);
            modules.Updated.Should().Be(1);
            modules.Failed.Should().Be(0);

            List<Module> local = stores.DataDb.Module.AsNoTracking().ToList();
            local.Should().HaveCount(3, "the node's own ingested module survives every sync");
            local.Single(m => m.UUID == sharedUuid).Version.Should().Be("2.0", "hub-authored updates refresh the catalog row");
            local.Select(m => m.Id).Should().NotContain(new long[] { 4242, 4243 }, "hub surrogates must not leak");
        }

        [Fact]
        public async Task SyncModuleLinks_RemapsHubSurrogateToTheLocalActivityRow()
        {
            using var stores = new Stores(nameof(SyncModuleLinks_RemapsHubSurrogateToTheLocalActivityRow));

            // A PRE-federation local activity: same IRI as the hub's, but its own UUID + Key.
            Uri activityIri = new Uri("https://hub.example/activities/welding-101");
            var localActivity = new XApiObject() { UUID = Guid.NewGuid(), Id = activityIri, ObjectType = "Activity" };
            stores.XApiDb.Object.Add(localActivity);
            stores.XApiDb.SaveChanges();
            long localKey = stores.XApiDb.Object.AsNoTracking().Single().Key;

            Guid moduleUuid = Guid.NewGuid();
            Guid hubObjectUuid = Guid.NewGuid();   // the HUB's UUID for that same activity
            var hub = QuietHub();
            hub.Setup(h => h.GetModulesByLicense()).ReturnsAsync(new List<Module>()
            {
                new Module() { UUID = moduleUuid, Name = "Welding 101", Version = "1.0" }
            });
            hub.Setup(h => h.GetObjects()).ReturnsAsync(new List<XApiObject>()
            {
                new XApiObject() { Key = 777, UUID = hubObjectUuid, Id = activityIri, ObjectType = "Activity" }
            });
            hub.Setup(h => h.GetModuleLinkedObject(moduleUuid)).ReturnsAsync(new ModuleLinkedObject()
            {
                UUID = Guid.NewGuid(),
                ModuleUUID = moduleUuid,
                ObjectUUID = hubObjectUuid,
                ObjectId = 777   // HUB surrogate -- meaningless on this node
            });

            HubSyncSummaryViewModel summary = await stores.Logic(hub.Object).SyncNow();

            HubSyncDomainResultViewModel links = summary.Domains.Single(d => d.Domain == "Module links");
            links.Added.Should().Be(1);
            links.Failed.Should().Be(0);

            ModuleLinkedObject link = stores.DataDb.ModuleLinkedObject.AsNoTracking().Single();
            link.ModuleUUID.Should().Be(moduleUuid);
            link.ObjectId.Should().Be(localKey, "the link must point at the LOCAL activity row, not the hub surrogate");
            link.ObjectUUID.Should().Be(localActivity.UUID, "the IRI match wins over the hub UUID");

            // And the activity domain refreshed nothing (same IRI, same ObjectType) rather than
            // duplicating the row under the hub's UUID.
            stores.XApiDb.Object.AsNoTracking().Should().HaveCount(1, "natural-key match must not duplicate the activity");
        }

        [Fact]
        public async Task SyncNow_OneDomainFailing_DoesNotAbortTheOthers()
        {
            using var stores = new Stores(nameof(SyncNow_OneDomainFailing_DoesNotAbortTheOthers));
            var hub = QuietHub();
            hub.Setup(h => h.GetVerbs()).ThrowsAsync(new InvalidOperationException("hub sync fetch failed (HTTP 500 on Verb/)"));
            hub.Setup(h => h.GetVersions()).ReturnsAsync(new List<XApiVersion>()
            {
                new XApiVersion() { UUID = Guid.NewGuid(), VersionNumber = "1.0.3" }
            });
            hub.Setup(h => h.GetModulesByLicense()).ReturnsAsync(new List<Module>()
            {
                new Module() { UUID = Guid.NewGuid(), Name = "Lockout Tagout", Version = "1.0" }
            });

            HubSyncSummaryViewModel summary = await stores.Logic(hub.Object).SyncNow();

            summary.SkippedGateClosed.Should().BeFalse();
            summary.Domains.Should().HaveCount(5, "every domain reports, failed or not");

            HubSyncDomainResultViewModel verbs = summary.Domains.Single(d => d.Domain == "Verbs");
            verbs.Error.Should().Contain("InvalidOperationException").And.Contain("HTTP 500");
            verbs.Added.Should().Be(0);

            summary.Domains.Single(d => d.Domain == "Versions").Added.Should().Be(1, "a failing sibling domain must not abort this one");
            summary.Domains.Single(d => d.Domain == "Modules").Added.Should().Be(1);
            stores.DataDb.Module.AsNoTracking().Should().HaveCount(1);
            stores.XApiDb.Version.AsNoTracking().Should().HaveCount(1);
        }

        [Fact]
        public async Task SyncNow_GarbageHubRows_CountFailed_WithoutAbortingTheDomain()
        {
            using var stores = new Stores(nameof(SyncNow_GarbageHubRows_CountFailed_WithoutAbortingTheDomain));
            var hub = QuietHub();
            hub.Setup(h => h.GetVerbs()).ReturnsAsync(new List<XApiVerb>()
            {
                new XApiVerb() { UUID = Guid.NewGuid(), Id = null, Display = new Dictionary<string, string> { ["en"] = "no IRI" } },   // unusable
                new XApiVerb() { UUID = Guid.NewGuid(), Id = new Uri("https://hub.example/verbs/ok"), Display = new Dictionary<string, string> { ["en"] = "ok" } },
            });

            HubSyncSummaryViewModel summary = await stores.Logic(hub.Object).SyncNow();

            HubSyncDomainResultViewModel verbs = summary.Domains.Single(d => d.Domain == "Verbs");
            verbs.Failed.Should().Be(1, "a row without its natural key cannot be applied");
            verbs.Added.Should().Be(1, "the rest of the domain still lands");
        }
    }
}
