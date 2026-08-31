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
    public interface IDefinitionLogic
    {
        Task<Definition> Create(Definition definition);
    }
    public class DefinitionLogic : IDefinitionLogic
    {
        private IDefinitionQueries _dataContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        public DefinitionLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _dataContext = new DefinitionQueries();
            User = _httpContextAccessor.HttpContext.User;
        }

        // DI refactor
        public DefinitionLogic(IHttpContextAccessor httpContextAccessor, IDefinitionQueries dataContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _dataContext = dataContext;
            User = _httpContextAccessor?.HttpContext?.User;
        }

        #region Get                         
        public async Task<List<Definition>> Get()
        {
            //bool output = true;
            List<Definition> output = new List<Definition>();
            try
            {
                output = await _dataContext.Get();
                //output.AddRange(preoutput);
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "DefinitionLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<Definition> Get(Guid input)
        {
            //bool output = true;
            Definition output = new Definition();
            try
            {
                //use input to find subscription
                output = await _dataContext.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "DefinitionLogic.Get(Guid): suppressed exception"); }
            return output;
        }
        public async Task<Definition> Get(long input)
        {
            //bool output = true;
            Definition output = new Definition();
            try
            {
                //use input to find subscription
                output = await _dataContext.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "DefinitionLogic.Get(long): suppressed exception"); }
            return output;
        }



        #endregion

        #region Post
        public async Task<Definition> Create(Definition input)
        {
            //Definition output = new Definition();
            try
            {
                input = await _dataContext.Create(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }

            return input;
        }
        //public async Task<IDefinition> Create(IDefinition input)
        //{
        //    IDefinition output = new Definition();
        //    try
        //    {
        //        output = await _definitionQueries.Create(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion

        #region Update
        public async Task<Definition> Update(Definition input)
        {
            Definition output = new Definition();
            try
            {
                output = await _dataContext.Update(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "DefinitionLogic.Update: suppressed exception");
            }

            return output;
        }
        //public async Task<IDefinition> Update(IDefinition input)
        //{
        //    IDefinition output = new Definition();
        //    try
        //    {
        //        output = await _definitionQueries.Update(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion

        #region Delete
        public async Task<bool> Delete(Definition input)
        {
            bool output = false;
            try
            {
                output = await _dataContext.Delete(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "DefinitionLogic.Delete: suppressed exception");
            }

            return output;
        }
        //public async Task<bool> Delete(IDefinition input)
        //{
        //    bool output = false;
        //    try
        //    {
        //        output = await _definitionQueries.Delete(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion
    }

    
}
