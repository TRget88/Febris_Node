// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Febris.PrimaryLogicLayer.Logic.XApiLogic
{
    public interface IVerbLogic
    {
        //Task<Verb> Create(Verb verb);
        //Task<Verb> Get(long? id);
        //Task<Verb> Update(Verb verb);
        //Task<List<Verb>> Get();
        //Task<Verb> Create(VerbCreationViewModel input);
    }
    public class VerbLogic : IVerbLogic
    {
        //private IVerbQueries _verbQueries = new SharedDataAccessLayer.Queries.XApiQueries.VerbQueries();
        private readonly IVerbQueries _verbQueries;// = new VerbQueries();
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IJsonStringDictionaryBuilder _stringBuilder;
        private readonly ClaimsPrincipal User;
        public VerbLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _verbQueries = new VerbQueries();
            User = _httpContextAccessor.HttpContext.User;
            _stringBuilder = new JsonStringDictionaryBuilder();
        }

        // DI refactor
        public VerbLogic(IHttpContextAccessor httpContextAccessor, IVerbQueries verbQueries, IJsonStringDictionaryBuilder stringBuilder)
        {
            _httpContextAccessor = httpContextAccessor;
            _verbQueries = verbQueries;
            User = _httpContextAccessor?.HttpContext?.User;
            _stringBuilder = stringBuilder;
        }

        #region Get      
        public async Task<Verb> Get(long? input)
        {
            Verb output = new Verb();
            try
            {
                output = await _verbQueries.Get(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        

        public async Task<Verb> Get(Guid input)
        {
            Verb output = await _verbQueries.Get(input);
            return output;
        }

        public async Task<List<Verb>> Get()
        {
            List<Verb> output = await _verbQueries.Get();


            return output;
        }
        public async Task<Verb> Get(Module input)
        {
            Verb output = new Verb();
            try
            {
                Uri verbUri = new Uri(string.Empty);
                //if (input.IsTest == true)
                //{
                    verbUri = new Uri(VerbIRIResolver.ResolveVerbIRI(VerbEnums.Initialized));
               // }
                //else
                //{
                //    verbUri = new Uri(VerbIRIResolver.ResolveVerbIRI(VerbEnums.Attempted));
                //}
                output = await _verbQueries.Get(verbUri);
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "VerbLogic.Get: suppressed exception"); }
            return output;
        }
        #endregion

        #region Post
        public async Task<Verb> Create(Verb input)
        {
            Verb output = new Verb();
            try
            {
                output = await _verbQueries.Create(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "VerbLogic.Create: suppressed exception");
            }

            return output;
        }
        //public async Task<Verb> Create(VerbCreationViewModel input)
        //{
        //    Verb output = new Verb();
        //    try
        //    {                
        //        string display = _stringBuilder.NewJsonDictionaryStringBuilder(input.LanguageMap.ToString().Replace('_', '-'), input.Description);
        //        string idUri = input.Description.Replace(" ", "_");
        //        output = new Verb()
        //        {
        //            Display = display,
        //            Id = new Uri("https://febr.is/Verb/Details/" + idUri)
        //        };
        //        output = await _verbQueries.Create(output);
        //    }
        //    catch
        //    {
        //    }
        //    return output;
        //}        
        #endregion

        #region Update
        public async Task<Verb> Update(Verb input)
        {
            Verb output = new Verb();
            try
            {
                output = await _verbQueries.Update(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "VerbLogic.Update: suppressed exception");
            }

            return output;
        }
        //public async Task<IVerb> Update(IVerb input)
        //{
        //    IVerb output = new Verb();
        //    try
        //    {
        //        output = await _verbQueries.Update(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion

        //#region Delete
        //public async Task<bool> Delete(Verb input)
        //{
        //    bool output = false;
        //    try
        //    {
        //        output = await _verbQueries.Delete(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}

       
        ////public async Task<bool> Delete(IVerb input)
        ////{
        ////    bool output = false;
        ////    try
        ////    {
        ////        output = await _verbQueries.Delete(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        //#endregion
    }

    
}
