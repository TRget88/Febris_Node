// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.FederationLogic
{
    /// <summary>
    /// Hub-pull sync, the last piece of auth severance: the OPT-IN enrichment
    /// contract behind the ONE hub-federation gate. PULL-ONLY: hub-authored vocabulary
    /// (Verb/Object/Version) and license-scoped catalog rows (Module/ModuleLinkedObject) flow
    /// DOWN into the node's local stores; nothing ever flows up.
    /// </summary>
    public interface IHubSyncLogic
    {
        /// <summary>
        /// Run one sync pass. Gate closed = a quiet no-op summary
        /// (<see cref="HubSyncSummaryViewModel.SkippedGateClosed"/>), zero HTTP -- the same
        /// discipline every gated remote path follows. Gate open = one pass over each domain
        /// with per-domain added/updated/failed counts; domains are ISOLATED, so one failing
        /// (hub endpoint down, garbage payload) never aborts the others.
        /// </summary>
        Task<HubSyncSummaryViewModel> SyncNow();
    }

    /// <summary>
    /// DI-only implementation of <see cref="IHubSyncLogic"/>: greenfield
    /// node code, deliberately NO legacy self-newing constructor.
    ///
    /// <para>
    /// CONFLICT SEMANTICS -- additive-and-refresh, never delete: rows are matched by NATURAL key
    /// (Verb/Object by their Id IRI, Version by VersionNumber, Module/link by UUID). A hub row
    /// with no local match is ADDED (adopting the hub's UUID so later hub references resolve,
    /// with a LOCAL surrogate key); a hub row matching an existing local row REFRESHES that
    /// row's hub-authored fields in place ("local wins" would leave stale vocabulary forever);
    /// local-only rows -- the node's own ingested modules, self-registered activities -- are
    /// NEVER touched, and nothing is ever deleted. Unchanged rows count as neither added nor
    /// updated. Object Definitions are NOT pulled (the definition graph carries hub surrogate
    /// keys; local definitions keep accruing through the statement-ingest persist-on-miss path).
    /// </para>
    ///
    /// <para>
    /// DEFERRED (explicitly, per slice review): package-BINARY pull via central
    /// <c>Module/Download</c> into <c>PackageIngestLogic</c> -- the only transport available is
    /// <c>APIRequestFactory.MakeByteArrayRequest</c>, whose own header says "No idea if this is
    /// actually working. Not tested"; wiring multi-hundred-MB module archives through an
    /// untested byte path exceeds this slice. The local catalog row still syncs, so the operator
    /// sees exactly which module payloads remain to ingest manually. Also FUTURE (documented,
    /// not in this slice): a background scheduler -- sync runs only from the portal's "Sync now"
    /// button today.
    /// </para>
    /// </summary>
    public class HubSyncLogic : IHubSyncLogic
    {
        private readonly IHubFederationSettings _federation;
        private readonly IHubSyncQueries _hubContext;
        private readonly IVerbQueries _verbContext;
        private readonly IObjectQueries _objectContext;
        private readonly IVersionQueries _versionContext;
        private readonly IModuleQueries _moduleContext;
        private readonly IModuleLinkedObjectQueries _moduleLinkedObjectContext;

        /// <summary>DI constructor (the only one).</summary>
        public HubSyncLogic(
            IHubFederationSettings federation,
            IHubSyncQueries hubContext,
            IVerbQueries verbContext,
            IObjectQueries objectContext,
            IVersionQueries versionContext,
            IModuleQueries moduleContext,
            IModuleLinkedObjectQueries moduleLinkedObjectContext)
        {
            _federation = federation;
            _hubContext = hubContext;
            _verbContext = verbContext;
            _objectContext = objectContext;
            _versionContext = versionContext;
            _moduleContext = moduleContext;
            _moduleLinkedObjectContext = moduleLinkedObjectContext;
        }

        /// <inheritdoc />
        public async Task<HubSyncSummaryViewModel> SyncNow()
        {
            HubSyncSummaryViewModel summary = new HubSyncSummaryViewModel()
            {
                StartedAtUtc = DateTime.UtcNow
            };

            ///Hub-federation gate: closed -> quiet no-op, zero HTTP, nothing logged.
            if (_federation == null || !_federation.CanReachDataApi)
            {
                summary.SkippedGateClosed = true;
                return summary;
            }

            // Cross-domain context: the hub's activity list (UUID -> IRI) lets the link domain
            // resolve activities against the LOCAL store by natural key even when local UUIDs
            // predate federation; the synced module UUIDs scope the link fetches.
            List<ModelLibrary.Models.XApiModels.Object> hubObjects = null;
            List<Guid> syncedModuleUuids = new List<Guid>();

            summary.Domains.Add(await RunDomain("Verbs", SyncVerbs));
            summary.Domains.Add(await RunDomain("Objects", async result =>
            {
                hubObjects = await SyncObjects(result);
            }));
            summary.Domains.Add(await RunDomain("Versions", SyncVersions));
            summary.Domains.Add(await RunDomain("Modules", async result =>
            {
                syncedModuleUuids = await SyncModules(result);
            }));
            summary.Domains.Add(await RunDomain("Module links", result =>
                SyncModuleLinks(result, syncedModuleUuids, hubObjects)));

            return summary;
        }

        /// <summary>Run one domain with failure ISOLATION: an escaped exception becomes the
        /// domain's Error (type + message) and the pass moves on.</summary>
        private static async Task<HubSyncDomainResultViewModel> RunDomain(
            string domain, Func<HubSyncDomainResultViewModel, Task> body)
        {
            HubSyncDomainResultViewModel result = new HubSyncDomainResultViewModel() { Domain = domain };
            try
            {
                await body(result);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "HubSyncLogic: '" + domain + "' domain failed; other domains continue");
                result.Error = ex.GetType().Name + ": " + ex.Message;
            }
            return result;
        }

        /// <summary>Verbs: natural key = Id IRI; refresh = Display.</summary>
        private async Task SyncVerbs(HubSyncDomainResultViewModel result)
        {
            List<ModelLibrary.Models.XApiModels.Verb> hubVerbs = await _hubContext.GetVerbs() ?? new List<ModelLibrary.Models.XApiModels.Verb>();
            foreach (ModelLibrary.Models.XApiModels.Verb hubVerb in hubVerbs)
            {
                if (hubVerb?.Id == null)
                {
                    result.Failed++;
                    continue;
                }
                ModelLibrary.Models.XApiModels.Verb local = await _verbContext.Get(hubVerb.Id);
                if (local == null)
                {
                    await _verbContext.Create(new ModelLibrary.Models.XApiModels.Verb()
                    {
                        // LOCAL surrogate (Key = 0); hub UUID adopted so hub references resolve.
                        UUID = hubVerb.UUID != Guid.Empty ? hubVerb.UUID : Guid.NewGuid(),
                        Id = hubVerb.Id,
                        Display = hubVerb.Display
                    });
                    result.Added++;
                }
                else if (!string.Equals(JsonConvert.SerializeObject(local.Display), JsonConvert.SerializeObject(hubVerb.Display), StringComparison.Ordinal))
                {
                    local.Display = hubVerb.Display;
                    await _verbContext.Update(local);
                    result.Updated++;
                }
            }
        }

        /// <summary>Objects: natural key = Id IRI; refresh = ObjectType. Definitions stay local
        /// (see class doc). Returns the hub list for the link domain's UUID-to-IRI map.</summary>
        private async Task<List<ModelLibrary.Models.XApiModels.Object>> SyncObjects(HubSyncDomainResultViewModel result)
        {
            List<ModelLibrary.Models.XApiModels.Object> hubObjects = await _hubContext.GetObjects() ?? new List<ModelLibrary.Models.XApiModels.Object>();
            foreach (ModelLibrary.Models.XApiModels.Object hubObject in hubObjects)
            {
                if (hubObject?.Id == null)
                {
                    result.Failed++;
                    continue;
                }
                ModelLibrary.Models.XApiModels.Object local = await _objectContext.Get(hubObject.Id);
                if (local == null)
                {
                    await _objectContext.Create(new ModelLibrary.Models.XApiModels.Object()
                    {
                        UUID = hubObject.UUID != Guid.Empty ? hubObject.UUID : Guid.NewGuid(),
                        Id = hubObject.Id,
                        ObjectType = hubObject.ObjectType
                    });
                    result.Added++;
                }
                else if (!string.Equals(local.ObjectType, hubObject.ObjectType, StringComparison.Ordinal))
                {
                    local.ObjectType = hubObject.ObjectType;
                    await _objectContext.Update(local);
                    result.Updated++;
                }
            }
            return hubObjects;
        }

        /// <summary>Versions: natural key = VersionNumber (existence-only -- the number IS the
        /// payload, so there is nothing to refresh).</summary>
        private async Task SyncVersions(HubSyncDomainResultViewModel result)
        {
            List<ModelLibrary.Models.XApiModels.Version> hubVersions = await _hubContext.GetVersions() ?? new List<ModelLibrary.Models.XApiModels.Version>();
            List<ModelLibrary.Models.XApiModels.Version> localVersions = await _versionContext.Get() ?? new List<ModelLibrary.Models.XApiModels.Version>();
            HashSet<string> known = new HashSet<string>(
                localVersions.Where(v => v?.VersionNumber != null).Select(v => v.VersionNumber),
                StringComparer.Ordinal);
            foreach (ModelLibrary.Models.XApiModels.Version hubVersion in hubVersions)
            {
                if (string.IsNullOrWhiteSpace(hubVersion?.VersionNumber))
                {
                    result.Failed++;
                    continue;
                }
                if (known.Add(hubVersion.VersionNumber))
                {
                    await _versionContext.Create(new ModelLibrary.Models.XApiModels.Version()
                    {
                        UUID = hubVersion.UUID != Guid.Empty ? hubVersion.UUID : Guid.NewGuid(),
                        VersionNumber = hubVersion.VersionNumber
                    });
                    result.Added++;
                }
            }
        }

        /// <summary>Modules: natural key = UUID, license-scoped hub-side (GetByLicense). Scalar
        /// fields only -- no hub surrogate ids, no classification graph (the local
        /// ModuleClassificationUUID is recorded for a later classification sync). Returns the
        /// synced UUIDs so the link domain can scope its fetches.</summary>
        private async Task<List<Guid>> SyncModules(HubSyncDomainResultViewModel result)
        {
            List<Guid> synced = new List<Guid>();
            List<Module> hubModules = await _hubContext.GetModulesByLicense() ?? new List<Module>();
            foreach (Module hubModule in hubModules)
            {
                if (hubModule == null || hubModule.UUID == Guid.Empty)
                {
                    result.Failed++;
                    continue;
                }
                Module existing = await _moduleContext.Get((Guid?)hubModule.UUID);
                await _moduleContext.Upsert(new Module()
                {
                    UUID = hubModule.UUID,
                    Name = hubModule.Name,
                    Version = hubModule.Version,
                    Description = hubModule.Description,
                    Obsolete = hubModule.Obsolete,
                    Language = hubModule.Language,
                    XApiInteractionType = hubModule.XApiInteractionType,
                    MainSectionCount = hubModule.MainSectionCount,
                    TotalSectionCount = hubModule.TotalSectionCount,
                    InteractionComponents = hubModule.InteractionComponents,
                    EstimatedCompletionTime = hubModule.EstimatedCompletionTime,
                    ModuleClassificationUUID = hubModule.ModuleClassificationUUID
                });
                if (existing == null)
                {
                    result.Added++;
                }
                else
                {
                    result.Updated++;
                }
                synced.Add(hubModule.UUID);
            }
            return synced;
        }

        /// <summary>
        /// Module-to-activity links, scoped to the modules THIS pass synced (the hub can only
        /// hold links for modules it knows). The hub link's ObjectId is a HUB surrogate --
        /// useless locally -- so the local activity is re-resolved: by the hub activity's IRI
        /// (via the Objects domain's list) first, by adopted UUID as fallback; a link whose
        /// activity cannot be resolved locally counts Failed (typically because the Objects
        /// domain failed this pass).
        /// </summary>
        private async Task SyncModuleLinks(
            HubSyncDomainResultViewModel result,
            List<Guid> syncedModuleUuids,
            List<ModelLibrary.Models.XApiModels.Object> hubObjects)
        {
            foreach (Guid moduleUuid in syncedModuleUuids ?? new List<Guid>())
            {
                ModuleLinkedObject hubLink = await _hubContext.GetModuleLinkedObject(moduleUuid);
                if (hubLink == null)
                {
                    continue;   // the hub has no link for this module -- nothing to enrich
                }

                ModelLibrary.Models.XApiModels.Object localActivity = await ResolveLocalActivity(hubLink, hubObjects);
                if (localActivity == null)
                {
                    result.Failed++;
                    continue;
                }

                ModuleLinkedObject existing = await _moduleLinkedObjectContext.GetByModule(moduleUuid);
                await _moduleLinkedObjectContext.Upsert(new ModuleLinkedObject()
                {
                    UUID = hubLink.UUID != Guid.Empty ? hubLink.UUID : Guid.NewGuid(),
                    ModuleUUID = moduleUuid,
                    ObjectUUID = localActivity.UUID,
                    ObjectId = localActivity.Key   // LOCAL surrogate, re-resolved above
                });
                if (existing == null)
                {
                    result.Added++;
                }
                else
                {
                    result.Updated++;
                }
            }
        }

        /// <summary>Resolve the hub link's activity against the LOCAL store (see
        /// <see cref="SyncModuleLinks"/> for the order of preference).</summary>
        private async Task<ModelLibrary.Models.XApiModels.Object> ResolveLocalActivity(
            ModuleLinkedObject hubLink,
            List<ModelLibrary.Models.XApiModels.Object> hubObjects)
        {
            Uri hubIri = hubObjects?
                .Where(o => o != null && o.UUID == hubLink.ObjectUUID)
                .Select(o => o.Id)
                .FirstOrDefault();
            if (hubIri != null)
            {
                ModelLibrary.Models.XApiModels.Object byIri = await _objectContext.Get(hubIri);
                if (byIri != null)
                {
                    return byIri;
                }
            }
            if (hubLink.ObjectUUID != Guid.Empty)
            {
                return await _objectContext.Get(hubLink.ObjectUUID);
            }
            return null;
        }
    }
}
