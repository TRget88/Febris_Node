// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Febris.PrimaryLogicLayer.Logic.XApiLogic
{
    public interface IVersionLogic
    {
    }
    public class VersionLogic : IVersionLogic
    {
        private readonly IVersionQueries _dataContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        public VersionLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _dataContext = new VersionQueries();
            User = _httpContextAccessor.HttpContext.User;
        }

        // DI refactor
        public VersionLogic(IHttpContextAccessor httpContextAccessor, IVersionQueries dataContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _dataContext = dataContext;
            User = _httpContextAccessor?.HttpContext?.User;
        }


        #region Get
        public async Task<ModelLibrary.Models.XApiModels.Version> Get(long Id)
        {
            ModelLibrary.Models.XApiModels.Version output = await _dataContext.Get(Id);
            return output;
        }

        public async Task<ModelLibrary.Models.XApiModels.Version> Get(Guid Id)
        {
            ModelLibrary.Models.XApiModels.Version output = await _dataContext.Get(Id);
            return output;
        }

        public async Task<List<ModelLibrary.Models.XApiModels.Version>> Get()
        {
            List<ModelLibrary.Models.XApiModels.Version> output = await _dataContext.Get();


            return output;
        }
        //public async Task<Version> GetLatest()
        //{
        //    ModelLibrary.Models.XApiModels.Version output = await _versionQueries.GetLatest();
        //    return output;
        //}

        #region Get - interfaced
        //public async Task<IVersion> Get(long Id)
        //{
        //    IVersion output = await _versionQueries.Get(Id);
        //    return output;
        //}

        //public async Task<IVersion> Get(Guid Id)
        //{
        //    IVersion output = await _versionQueries.Get(Id);
        //    return output;
        //}

        //public async Task<List<IVersion>> Get()
        //{
        //    List<IVersion> output = await _versionQueries.Get();


        //    return output;
        //}
        //public async Task<IVersion> GetLatest()
        //{
        //    IVersion output = await _versionQueries.GetLatest();
        //    return output;
        //}
        #endregion 
        #endregion 

        //#region Post
        //public async Task<Febris.ModelLibrary.Models.XApiModels.Version> Create(Febris.ModelLibrary.Models.XApiModels.Version input)
        //{
        //    Febris.ModelLibrary.Models.XApiModels.Version output = new Febris.ModelLibrary.Models.XApiModels.Version();
        //    try
        //    {
        //        output = await _dataContext.Create(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        ////public async Task<IVersion> Create(IVersion input)
        ////{
        ////    IVersion output = new Febris.ModelLibrary.Models.XApiModels.Version();
        ////    try
        ////    {
        ////        output = await _versionQueries.Create(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        //#endregion

        //#region Update
        //public async Task<Febris.ModelLibrary.Models.XApiModels.Version> Update(Febris.ModelLibrary.Models.XApiModels.Version input)
        //{
        //    Febris.ModelLibrary.Models.XApiModels.Version output = new Febris.ModelLibrary.Models.XApiModels.Version();
        //    try
        //    {
        //        output = await _dataContext.Update(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        ////public async Task<IVersion> Update(IVersion input)
        ////{
        ////    IVersion output = new Febris.ModelLibrary.Models.XApiModels.Version();
        ////    try
        ////    {
        ////        output = await _versionQueries.Update(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        //#endregion

        //#region Delete
        //public async Task<bool> Delete(Febris.ModelLibrary.Models.XApiModels.Version input)
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

        //#endregion
    }
        
}
