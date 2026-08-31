// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    public interface IModuleLinkedObjectLogic
    {
    }

    public class ModuleLinkedObjectLogic: IModuleLinkedObjectLogic
    {
        //private ModuleLinkedObjectQueries _context = new SharedDataAccessLayer.Queries.DataQueries.ModuleLinkedObjectQueries();
        //private IModuleLinkedObjectQueries _moduleLinkedObjectQueries = new SharedDataAccessLayer.Queries.DataQueries.ModuleLinkedObjectQueries();
        //private IObjectQueries _objectQueries = new SharedDataAccessLayer.Queries.XApiQueries.ObjectQueries();
        private readonly IModuleLinkedObjectQueries _dataContext;
        private readonly IObjectQueries _objectQueries;
        private readonly IModuleQueries _moduleQueries;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;

        // DI refactor
        public ModuleLinkedObjectLogic(IHttpContextAccessor httpContextAccessor, IObjectQueries objectQueries, IModuleQueries moduleQueries, IModuleLinkedObjectQueries dataContext)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _objectQueries = objectQueries;
            _moduleQueries = moduleQueries;
            _dataContext = dataContext;
        }

        public ModuleLinkedObjectLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;
            _objectQueries = new ObjectQueries();
            _moduleQueries = new ModuleQueries();
            _dataContext = new ModuleLinkedObjectQueries();
        }

        #region Get
        //public async Task<ModuleLinkedObject> Get(long input)
        //{
        //    ModuleLinkedObject ouput = new ModuleLinkedObject();
        //    try
        //    {
        //        ouput = await _dataContext.Get(input);
        //    }
        //    catch
        //    {

        //    }

        //    return ouput;
        //}
        //public async Task<ModuleLinkedObject> Get(Guid input)
        //{
        //    ModuleLinkedObject output = new ModuleLinkedObject();
        //    try
        //    {
        //        output = await _dataContext.Get(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<List<ModuleLinkedObject>> Get()
        //{
        //    List<ModuleLinkedObject> output = new List<ModuleLinkedObject>();
        //    try
        //    {
        //        //List<ModuleBaseLinkedObject> preoutput = await _moduleBaseLinkedObjectQueries.Get();
        //        //output.AddRange(preoutput);
        //        output = await _dataContext.Get();
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<List<ModuleLinkedObject>> GetListByObject(Guid input)
        //{
        //    List<ModuleLinkedObject> output = new List<ModuleLinkedObject>();
        //    try
        //    {
        //        //List<ModuleBaseLinkedObject> preoutput = await _moduleBaseLinkedObjectQueries.GetByObject(input);
        //        //output.AddRange(preoutput);
        //        output = await _dataContext.GetListByObject(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<List<ModuleLinkedObject>> GetListByObject(long input)
        //{
        //    List<ModuleLinkedObject> output = new List<ModuleLinkedObject>();
        //    try
        //    {
        //        //List<ModuleBaseLinkedObject> preoutput = await _moduleBaseLinkedObjectQueries.GetByObject(input);
        //        //output.AddRange(preoutput);
        //        output = await _dataContext.GetListByObject(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<List<ModuleLinkedObject>> GetListByModule(Guid input)
        //{
        //    List<ModuleLinkedObject> output = new List<ModuleLinkedObject>();
        //    try
        //    {
        //        //List<ModuleBaseLinkedObject> preoutput = await _moduleBaseLinkedObjectQueries.GetByModule(input);
        //        //output.AddRange(preoutput);
        //        output = await _dataContext.GetListByModule(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<List<ModuleLinkedObject>> GetListByModule(long input)
        //{
        //    List<ModuleLinkedObject> output = new List<ModuleLinkedObject>();
        //    try
        //    {
        //        //List<ModuleBaseLinkedObject> preoutput = await _moduleBaseLinkedObjectQueries.GetByModule(input);
        //        //output.AddRange(preoutput);
        //        output = await _dataContext.GetListByModule(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<ModuleLinkedObject> GetByObject(Guid input)
        //{
        //    ModuleLinkedObject output = new ModuleLinkedObject();
        //    try
        //    {
        //        //List<ModuleBaseLinkedObject> preoutput = await _moduleBaseLinkedObjectQueries.GetByObject(input);
        //        //output.AddRange(preoutput);
        //        output = await _moduleQueries.GetByObject(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<ModuleLinkedObject> GetByObject(long input)
        //{
        //    ModuleLinkedObject output = new ModuleLinkedObject();
        //    try
        //    {
        //        //List<ModuleBaseLinkedObject> preoutput = await _moduleBaseLinkedObjectQueries.GetByObject(input);
        //        //output.AddRange(preoutput);
        //        output = await _moduleQueries.GetByObject(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<ModuleLinkedObject> GetByModule(Guid input)
        //{
        //    ModuleLinkedObject output = new ModuleLinkedObject();
        //    try
        //    {
        //        //List<ModuleBaseLinkedObject> preoutput = await _moduleBaseLinkedObjectQueries.GetByModule(input);
        //        //output.AddRange(preoutput);
        //        output = await _moduleQueries.GetByModule(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<ModuleLinkedObject> GetByModule(long input)
        //{
        //    ModuleLinkedObject output = new ModuleLinkedObject();
        //    try
        //    {
        //        //List<ModuleBaseLinkedObject> preoutput = await _moduleBaseLinkedObjectQueries.GetByModule(input);
        //        //output.AddRange(preoutput);
        //        output = await _moduleQueries.GetByModule(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<ModuleLinkedObject> Get(Module input)
        //{
        //    ModuleLinkedObject output = new ModuleLinkedObject();
        //    try
        //    {
        //        output = await _moduleQueries.GetByModule(input.Id);
        //        if (input.UUID != output.ModuleUUID)
        //        {
        //            output = await _moduleQueries.GetByModule(input.UUID);
        //        }
        //    }
        //    catch
        //    {

        //    }
        //    return output;
        //}
        
        #endregion
                
    }

    
}
