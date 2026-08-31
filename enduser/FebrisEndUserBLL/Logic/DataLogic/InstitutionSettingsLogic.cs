// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    public interface IInstitutionSettingsLogic
    {
        /// <summary>The settings governing THIS deployment. Auth severance:
        /// the license-claim-derived read (the shared tier's <c>GetSettings</c> resolves the id
        /// from <c>License.Institution.InstitutionSettings</c>) falls back to the node's LOCAL
        /// single-tenant identity when no license is present -- which on a hub-less node is
        /// always.</summary>
        Task<InstitutionSettings> GetSettings();
    }


    /// <summary>
    /// The tenant's InstitutionSettings surface. Auth severance: the Remote
    /// reads to central go behind the ONE hub-federation gate; with the gate closed the node
    /// answers from its LOCAL single-tenant identity (<see cref="NodeIdentity"/>) with
    /// default-valued settings -- exactly the values the BLL already surfaced when the hub was
    /// unreachable, now deliberate instead of accidental.
    /// </summary>
    public class InstitutionSettingsLogic: IInstitutionSettingsLogic
    {
        private readonly InstitutionSettingsQueries _dataContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        private readonly INodeIdentityQueries _nodeIdentityContext;
        private readonly Febris.ModelLibrary.ViewModels.IHubFederationSettings _federation;

        public InstitutionSettingsLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;
            _dataContext = new InstitutionSettingsQueries();
            // Legacy self-newing path: gate resolved from the passed-back config (null-safe);
            // no local identity store on this path -- the local answer degrades to defaults.
            _federation = Febris.ModelLibrary.ViewModels.HubFederationSettings.Resolve(Febris.SharedServices.StaticDetails.PassedBackConfig);
        }

        /// <summary>
        /// Greedy DI ctor: the host-registered federation gate and the
        /// node's local identity store flow in; the Remote query stays legacy-newed until its
        /// own strangler conversion (it is fully gated internally).
        /// </summary>
        public InstitutionSettingsLogic(
            IHttpContextAccessor httpContextAccessor,
            INodeIdentityQueries nodeIdentityContext,
            Febris.ModelLibrary.ViewModels.IHubFederationSettings federation)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _dataContext = new InstitutionSettingsQueries();
            _nodeIdentityContext = nodeIdentityContext;
            _federation = federation ?? Febris.ModelLibrary.ViewModels.HubFederationSettings.Disabled();
        }

        #region Get

        /// <summary>
        /// This deployment's settings, local-first. No license claim is attached on the node (the
        /// scheme-B middleware never runs tenant-side), so with the gate closed the answer is the
        /// node's own single-tenant settings; with a hub attached the remote read still serves
        /// hub-scoped deployments.
        /// </summary>
        public async Task<InstitutionSettings> GetSettings()
        {
            InstitutionSettings output = new InstitutionSettings();
            try
            {
                ///Hub-federation gate: no hub -> the node's local single-tenant answer.
                if (!_federation.CanReachDataApi)
                {
                    return await GetLocalSettings();
                }
                output = await _dataContext.Get() is List<InstitutionSettings> list && list.Count > 0
                    ? list[0]
                    : output;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "InstitutionSettingsLogic.GetSettings: suppressed exception"); }
            return output;
        }

        /// <summary>
        /// The node's LOCAL settings answer: default-valued toggles scoped to
        /// the provisioned <see cref="NodeIdentity"/> (single-tenant, so the institution identity
        /// IS the settings scope -- the UUID carries the node's InstitutionUUID). Unprovisioned
        /// store or legacy construction -> plain defaults.
        /// </summary>
        public async Task<InstitutionSettings> GetLocalSettings()
        {
            NodeIdentity node = _nodeIdentityContext == null ? null : await _nodeIdentityContext.Get();
            InstitutionSettings output = new InstitutionSettings();
            if (node != null)
            {
                output.UUID = node.InstitutionUUID;
                output.TimeStamp = node.TimeStamp;
                output.LastUpdateTimeStamp = node.LastUpdateTimeStamp;
            }
            return output;
        }

        public async Task<List<InstitutionSettings>> Get()
        {
            //bool output = true;
            List<InstitutionSettings> output = new List<InstitutionSettings>();
            try
            {
                ///Hub-federation gate: no hub -> a single-tenant list of one (the node itself).
                if (!_federation.CanReachDataApi)
                {
                    output.Add(await GetLocalSettings());
                    return output;
                }
                output = await _dataContext.Get();
                //output.AddRange(preoutput);
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "InstitutionSettingsLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<InstitutionSettings> Get(Guid input)
        {
            //bool output = true;
            InstitutionSettings output = new InstitutionSettings();
            try
            {
                ///Hub-federation gate: no hub -> the node's local single-tenant answer.
                if (!_federation.CanReachDataApi)
                {
                    return await GetLocalSettings();
                }
                //use input to find subscription
                output = await _dataContext.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "InstitutionSettingsLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<InstitutionSettings> Get(long input)
        {
            //bool output = true;
            InstitutionSettings output = new InstitutionSettings();
            try
            {
                ///Hub-federation gate: no hub -> the node's local single-tenant answer.
                if (!_federation.CanReachDataApi)
                {
                    return await GetLocalSettings();
                }
                //use input to find subscription
                output = await _dataContext.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "InstitutionSettingsLogic.Get: suppressed exception"); }
            return output;
        }

        #endregion
       

       
        #region Post
        //public async Task<InstitutionSettings> Create(InstitutionSettings input)
        //{
        //    InstitutionSettings output = new InstitutionSettings();
        //    try
        //    {
        //        output = await _dataContext.Create(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<IInstitutionSettings> Create(IInstitutionSettings input)
        //{
        //    IInstitutionSettings output = new InstitutionSettings();
        //    try
        //    {
        //        output = await _InstitutionSettingsQueries.Create(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion

        #region Update
        public async Task<InstitutionSettings> Update(InstitutionSettings input)
        {
            InstitutionSettings output = new InstitutionSettings();
            try
            {
                output = await _dataContext.Update(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "InstitutionSettingsLogic.Update: suppressed exception");
            }

            return output;
        }
        //public async Task<IInstitutionSettings> Update(IInstitutionSettings input)
        //{
        //    IInstitutionSettings output = new InstitutionSettings();
        //    try
        //    {
        //        output = await _InstitutionSettingsQueries.Update(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion

        #region Delete
        //public async Task<bool> Delete(InstitutionSettings input)
        //{
        //    bool output = false;
        //    try
        //    {
        //        output = await _dataContext.Delete(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<bool> Delete(IInstitutionSettings input)
        //{
        //    bool output = false;
        //    try
        //    {
        //        output = await _InstitutionSettingsQueries.Delete(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion
    }

    
}
