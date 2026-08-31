// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.SharedServices;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    /// <summary>
    /// Hub-pull sync transport: the read-only hub fetches HubSyncLogic enriches the
    /// local stores from. One method per pulled shape; every one consults the ONE hub-federation
    /// gate first and is PULL-ONLY -- nothing is ever written hub-ward.
    /// </summary>
    public interface IHubSyncQueries
    {
        /// <summary>The hub's full Verb vocabulary (central <c>GET Verb/</c>). Null when the
        /// gate is closed; throws when the hub answered non-OK (so sync can report the failure).</summary>
        Task<List<ModelLibrary.Models.XApiModels.Verb>> GetVerbs();

        /// <summary>The hub's full Object (activity) vocabulary (central <c>GET Object/</c>).</summary>
        Task<List<ModelLibrary.Models.XApiModels.Object>> GetObjects();

        /// <summary>The hub's xAPI Version list (central <c>GET Version/</c>).</summary>
        Task<List<ModelLibrary.Models.XApiModels.Version>> GetVersions();

        /// <summary>The modules this node's license entitles it to (central
        /// <c>GET Module/GetByLicense</c> -- the hub expands the license server-side).</summary>
        Task<List<Module>> GetModulesByLicense();

        /// <summary>The module-to-activity link for one module (central
        /// <c>GET ModuleLinkedObject/GetByModuleUUID/{uuid}</c>). Null when the hub has none.</summary>
        Task<ModuleLinkedObject> GetModuleLinkedObject(Guid moduleUuid);
    }

    /// <summary>
    /// DI-only implementation of <see cref="IHubSyncQueries"/>: greenfield
    /// node code, deliberately NO legacy self-newing constructor and NO static config read.
    /// Swept into DI by the <c>AddFebrisUserNodeDataAccess</c> naming convention.
    ///
    /// <para>
    /// Transport is the SAME <see cref="APIRequestFactory"/> + gate-check + license-Bearer +
    /// RenewToken-retry recipe every existing gated Remote query class uses (the [Historical]
    /// remote bodies preserved in the local Verb/Object/Version/Module query classes document
    /// these exact endpoints -- this class is their planned pull-sync replacement; no new raw
    /// HTTP stack). One deliberate DIFFERENCE from the legacy classes: a non-OK answer THROWS
    /// instead of degrading to an empty string, because sync must report "hub said no" per
    /// domain rather than mis-counting it as an empty catalog.
    /// </para>
    /// </summary>
    public class HubSyncQueries : IHubSyncQueries
    {
        private readonly IHubFederationSettings _federation;
        private readonly ITokenQueries _tokenContext;

        /// <summary>DI constructor (the only one): the host-registered (DB-first) gate plus the
        /// scheme-B token bootstrap for the license-Bearer + renewal retry.</summary>
        public HubSyncQueries(IHubFederationSettings federation, ITokenQueries tokenContext)
        {
            _federation = federation ?? HubFederationSettings.Disabled();
            _tokenContext = tokenContext;
        }

        /// <inheritdoc />
        public async Task<List<ModelLibrary.Models.XApiModels.Verb>> GetVerbs()
        {
            return await FetchList<ModelLibrary.Models.XApiModels.Verb>("Verb/");
        }

        /// <inheritdoc />
        public async Task<List<ModelLibrary.Models.XApiModels.Object>> GetObjects()
        {
            return await FetchList<ModelLibrary.Models.XApiModels.Object>("Object/");
        }

        /// <inheritdoc />
        public async Task<List<ModelLibrary.Models.XApiModels.Version>> GetVersions()
        {
            return await FetchList<ModelLibrary.Models.XApiModels.Version>("Version/");
        }

        /// <inheritdoc />
        public async Task<List<Module>> GetModulesByLicense()
        {
            return await FetchList<Module>("Module/GetByLicense");
        }

        /// <inheritdoc />
        public async Task<ModuleLinkedObject> GetModuleLinkedObject(Guid moduleUuid)
        {
            string result = await MakeGetRequest("ModuleLinkedObject/GetByModuleUUID/" + moduleUuid.ToString());
            if (string.IsNullOrWhiteSpace(result))
            {
                return null;
            }
            return JsonConvert.DeserializeObject<ModuleLinkedObject>(result);
        }

        /// <summary>Fetch + deserialize one list-shaped endpoint. Null when the gate is closed
        /// (quiet local-only); an empty hub answer is an empty list, not null.</summary>
        private async Task<List<T>> FetchList<T>(string method)
        {
            string result = await MakeGetRequest(method);
            if (result == null)
            {
                return null;
            }
            if (string.IsNullOrWhiteSpace(result))
            {
                return new List<T>();
            }
            return JsonConvert.DeserializeObject<List<T>>(result) ?? new List<T>();
        }

        /// <summary>
        /// The gated GET recipe of the existing Remote query classes (gate check first; license
        /// Bearer; one RenewToken + retry on non-OK), except that a final non-OK THROWS -- see
        /// the class doc. Null return = gate closed.
        /// </summary>
        private async Task<string> MakeGetRequest(string method)
        {
            ///Hub-federation gate: no hub configured -> local-only.
            ///No HTTP attempt, nothing logged.
            if (!_federation.CanReachDataApi)
            {
                return null;
            }

            IAPIRequestFactory request = new APIRequestFactory()
            {
                endPoint = _federation.DataApi + method,
                httpMethod = httpVerb.GET,
                authTech = AuthenticaitonTechnique.Token,
                authType = Authenticationtype.BearerToken,
                token = StaticDetails.LicenseAuthenticateResponse?.JwtToken ?? string.Empty,
                postJSON = string.Empty,
            };
            (string response, HttpStatusCode status) = await request.MakeStringRequest();
            if (status != HttpStatusCode.OK)
            {
                bool renewed = _tokenContext != null && await _tokenContext.RenewToken();
                if (renewed)
                {
                    request.token = StaticDetails.LicenseAuthenticateResponse.JwtToken;
                    (response, status) = await request.MakeStringRequest();
                }
            }
            if (status != HttpStatusCode.OK)
            {
                throw new InvalidOperationException("hub sync fetch failed (HTTP " + (int)status + " on " + method + ")");
            }
            return response;
        }
    }
}
