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
    public interface IXRHardwareModelLogic
    {
        Task<List<XRHardwareModel>> Get();
    }

    public class XRHardwareModelLogic: IXRHardwareModelLogic
    {
        private readonly IXRHardwareModelQueries _dataContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;

        public XRHardwareModelLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;            
            _dataContext = new XRHardwareModelQueries();
        }

        // DI refactor
        public XRHardwareModelLogic(IHttpContextAccessor httpContextAccessor,
            IXRHardwareModelQueries dataContext)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _dataContext = dataContext;
        }
        #region Get                      
        public async Task<List<XRHardwareModel>> Get()
        {
            //bool output = true;
            List<XRHardwareModel> output = new List<XRHardwareModel>();
            try
            {
                output = await _dataContext.Get();
                //output.AddRange(preoutput);
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "XRHardwareModelLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<XRHardwareModel> Get(Guid? input)
        {
            //bool output = true;
            XRHardwareModel output = new XRHardwareModel();
            try
            {
                //use input to find subscription
                output = await _dataContext.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "XRHardwareModelLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<XRHardwareModel> Get(long? input)
        {
            //bool output = true;
            XRHardwareModel output = new XRHardwareModel();
            try
            {
                //use input to find subscription
                output = await _dataContext.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "XRHardwareModelLogic.Get: suppressed exception"); }
            return output;
        }

        #endregion
        //#region Get              
        //#region not interfaced                
        //public async Task<List<XRHardwareModel>> Get()
        //{
        //    //bool output = true;
        //    List<XRHardwareModel> output = new List<XRHardwareModel>();
        //    try
        //    {
        //        output = await _dataContext.Get();
        //        //output.AddRange(preoutput);
        //    }
        //    catch { }
        //    return output;
        //}
        //public async Task<XRHardwareModel> Get(Guid input)
        //{
        //    //bool output = true;
        //    XRHardwareModel output = new XRHardwareModel();
        //    try
        //    {
        //        //use input to find subscription
        //        output = await _dataContext.Get(input);
        //        //output = subscription;
        //    }
        //    catch { }
        //    return output;
        //}
        //public async Task<XRHardwareModel> Get(long input)
        //{
        //    //bool output = true;
        //    XRHardwareModel output = new XRHardwareModel();
        //    try
        //    {
        //        //use input to find subscription
        //        output = await _dataContext.Get(input);
        //        //output = subscription;
        //    }
        //    catch { }
        //    return output;
        //}

        //#endregion
        ////public async Task<List<IXRHardwareModel>> Get()
        ////{
        ////    //bool output = true;
        ////    List<IXRHardwareModel> output = new List<IXRHardwareModel>();
        ////    try
        ////    {
        ////        output = await _XRHardwareModelQueries.Get();
        ////        //output.AddRange(preoutput);
        ////    }
        ////    catch { }
        ////    return output;
        ////}
        ////public async Task<IXRHardwareModel> Get(Guid input)
        ////{
        ////    //bool output = true;
        ////    IXRHardwareModel output = new XRHardwareModel();
        ////    try
        ////    {
        ////        //use input to find subscription
        ////        output = await _XRHardwareModelQueries.Get(input);
        ////        //output = subscription;
        ////    }
        ////    catch { }
        ////    return output;
        ////}
        ////public async Task<IXRHardwareModel> Get(long input)
        ////{
        ////    //bool output = true;
        ////    IXRHardwareModel output = new XRHardwareModel();
        ////    try
        ////    {
        ////        //use input to find subscription
        ////        output = await _XRHardwareModelQueries.Get(input);
        ////        //output = subscription;
        ////    }
        ////    catch { }
        ////    return output;
        ////}

        //#endregion

        //#region Post
        //public async Task<XRHardwareModel> Create(XRHardwareModel input)
        //{
        //    XRHardwareModel output = new XRHardwareModel();
        //    try
        //    {
        //        output = await _dataContext.Create(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        ////public async Task<IXRHardwareModel> Create(IXRHardwareModel input)
        ////{
        ////    IXRHardwareModel output = new XRHardwareModel();
        ////    try
        ////    {
        ////        output = await _XRHardwareModelQueries.Create(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        //#endregion

        //#region Update
        //public async Task<XRHardwareModel> Update(XRHardwareModel input)
        //{
        //    XRHardwareModel output = new XRHardwareModel();
        //    try
        //    {
        //        output = await _dataContext.Update(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        ////public async Task<IXRHardwareModel> Update(IXRHardwareModel input)
        ////{
        ////    IXRHardwareModel output = new XRHardwareModel();
        ////    try
        ////    {
        ////        output = await _XRHardwareModelQueries.Update(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        //#endregion

        //#region Delete
        //public async Task<bool> Delete(XRHardwareModel input)
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
        ////public async Task<bool> Delete(IXRHardwareModel input)
        ////{
        ////    bool output = false;
        ////    try
        ////    {
        ////        output = await _XRHardwareModelQueries.Delete(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        //#endregion
    }

}
