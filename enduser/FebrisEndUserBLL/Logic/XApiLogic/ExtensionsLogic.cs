// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.XApiModels;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Febris.PrimaryLogicLayer.Logic.XApiLogic
{
    public interface IExtensionsLogic
    {
    }
    public class ExtensionsLogic : IExtensionsLogic
    {
        //private IExtensionsQueries _extensionsQueries = new SharedDataAccessLayer.Queries.XApiQueries.ExtensionsQueries();
        private readonly IExtensionsQueries _dataContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        public ExtensionsLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _dataContext = new ExtensionsQueries();
            User = _httpContextAccessor.HttpContext.User;
        }

        // DI refactor
        public ExtensionsLogic(IHttpContextAccessor httpContextAccessor, IExtensionsQueries dataContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _dataContext = dataContext;
            User = _httpContextAccessor?.HttpContext?.User;
        }

        #region global
        #region Get        
        public async Task<Extensions> Get(long Id)
        {
            Extensions output = await _dataContext.Get(Id);
            return output;
        }

        public async Task<Extensions> Get(Guid Id)
        {
            Extensions output = await _dataContext.Get(Id);
            return output;
        }

        public async Task<List<Extensions>> Get()
        {
            List<Extensions> output = await _dataContext.Get();


            return output;
        }
        #endregion

        #region Post
        public async Task<Extensions> Create(Extensions input)
        {
            Extensions output = new Extensions();
            try
            {
                output = await _dataContext.Create(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ExtensionsLogic.Create: suppressed exception");
            }

            return output;
        }

        #endregion

        #region Update
        public async Task<Extensions> Update(Extensions input)
        {
            Extensions output = new Extensions();
            try
            {
                output = await _dataContext.Update(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ExtensionsLogic.Update: suppressed exception");
            }

            return output;
        }

        #endregion

        #region Delete
        public async Task<bool> Delete(Extensions input)
        {
            bool output = false;
            try
            {
                output = await _dataContext.Delete(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ExtensionsLogic.Delete: suppressed exception");
            }
            return output;
        }

        #endregion
        #endregion

    }

}
