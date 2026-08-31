// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Febris.ModelLibrary.Interfaces.DataModelInterfaces;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    /// <summary>
    /// The tenant's view of "which institution am I". Auth severance: the
    /// Remote read to central goes behind the ONE hub-federation gate; with the gate closed, the
    /// node's LOCAL single-tenant identity (<see cref="NodeIdentity"/>, seeded at provision) is
    /// the answer -- no license claim, no HTTP.
    /// </summary>
    public class InstitutionLogic: IInstitutionLogic
    {
        //private IInstitutionQueries _institutionQueries = new SharedDataAccessLayer.Queries.DataQueries.InstitutionQueries();
        //private InstitutionQueries _context = new SharedDataAccessLayer.Queries.DataQueries.InstitutionQueries();
        private readonly InstitutionQueries _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        private readonly INodeIdentityQueries _nodeIdentityContext;
        private readonly Febris.ModelLibrary.ViewModels.IHubFederationSettings _federation;

        public InstitutionLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;
            _context = new InstitutionQueries();
            // Legacy self-newing path: gate resolved from the passed-back config (null-safe);
            // no local identity store on this path -- the local answer degrades to empty.
            _federation = Febris.ModelLibrary.ViewModels.HubFederationSettings.Resolve(Febris.SharedServices.StaticDetails.PassedBackConfig);
        }

        /// <summary>
        /// Greedy DI ctor: the host-registered federation gate and the
        /// node's local identity store flow in; the Remote query stays legacy-newed until its
        /// own strangler conversion (it is fully gated internally).
        /// </summary>
        public InstitutionLogic(
            IHttpContextAccessor httpContextAccessor,
            INodeIdentityQueries nodeIdentityContext,
            Febris.ModelLibrary.ViewModels.IHubFederationSettings federation)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _context = new InstitutionQueries();
            _nodeIdentityContext = nodeIdentityContext;
            _federation = federation ?? Febris.ModelLibrary.ViewModels.HubFederationSettings.Disabled();
        }


        #region Get
        public async Task<Institution> Get(long Id)
        {
            ///Hub-federation gate: no hub -> the node's own identity is the institution.
            if (!_federation.CanReachDataApi)
            {
                return await GetLocalInstitution();
            }
            Institution output = (Institution)await _context.Get(Id);
            return output;
        }
        public async Task<Institution> Get(Guid Id)
        {
            ///Hub-federation gate: no hub -> the node's own identity is the institution.
            if (!_federation.CanReachDataApi)
            {
                return await GetLocalInstitution();
            }
            Institution output = (Institution)await _context.Get(Id);
            return output;
        }
        public async Task<List<Institution>> Get()
        {
            ///Hub-federation gate: no hub -> a single-tenant list of one (the node itself).
            if (!_federation.CanReachDataApi)
            {
                return new List<Institution>() { await GetLocalInstitution() };
            }
            List<Institution> output = await _context.Get();
            return output;
        }

        /// <summary>
        /// The node's LOCAL single-tenant identity projected as an <see cref="Institution"/>
        /// -- local identity is the no-hub answer. Unprovisioned store or
        /// legacy construction (no identity queries) -> quiet empty Institution, the same value
        /// an unreachable hub already produced.
        /// </summary>
        public async Task<Institution> GetLocalInstitution()
        {
            NodeIdentity node = _nodeIdentityContext == null ? null : await _nodeIdentityContext.Get();
            if (node == null)
            {
                return new Institution();
            }
            return new Institution()
            {
                UUID = node.InstitutionUUID,
                Name = node.Name,
                TimeStamp = node.TimeStamp,
                LastUpdateTimeStamp = node.LastUpdateTimeStamp
            };
        }
        #region Get
        //public async Task<IInstitution> Get(long Id)
        //{
        //    IInstitution output = (IInstitution)await _institutionQueries.Get(Id);
        //    return output;
        //}
        //public async Task<IInstitution> Get(Guid Id)
        //{
        //    IInstitution output = (IInstitution)await _institutionQueries.Get(Id);
        //    return output;
        //}
        //public async Task<List<IInstitution>> Get()
        //{
        //    List<IInstitution> output = await _institutionQueries.Get();
        //    return output;
        //}
        #endregion
        #endregion

        //#region Create
        //public async Task<Institution> Create(Institution input)
        //{
        //    try
        //    {
        //        input = await _context.Create(input);                
        //    }
        //    catch
        //    {

        //    }
        //    return input;
        //}
        ////public async Task<IInstitution> Create(IInstitution input)
        ////{
        ////    try
        ////    {
        ////        input = await _institutionQueries.Create(input);
        ////    }
        ////    catch
        ////    {

        ////    }
        ////    return (Institution)input;
        ////}
        //#endregion

        //#region Update
        ////public async Task<IInstitution> Update(IInstitution input)
        ////{
        ////    try
        ////    {
        ////        input = await _institutionQueries.Update(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return (Institution)input;
        ////}
        //public async Task<Institution> Update(Institution input)
        //{
        //    try
        //    {
        //        input = await _context.Update(input);
        //    }
        //    catch
        //    {

        //    }

        //    return input;
        //}
        //#endregion

        //#region Delete

        ////public async Task<bool> Delete(IInstitution input)
        ////{
        ////    bool output = false;
        ////    try
        ////    {
        ////        output = await _institutionQueries.Delete(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        //public async Task<bool> Delete(Institution input)
        //{
        //    bool output = false;
        //    try
        //    {
        //        output = await _context.Delete(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //#endregion
    }

    public interface IInstitutionLogic
    {
        Task<Institution> Get(long Id);
        Task<Institution> Get(Guid Id);
        Task<List<Institution>> Get();

        /// <summary>The node's local single-tenant identity as an Institution -- the no-hub
        /// answer under auth severance.</summary>
        Task<Institution> GetLocalInstitution();
    }
}
