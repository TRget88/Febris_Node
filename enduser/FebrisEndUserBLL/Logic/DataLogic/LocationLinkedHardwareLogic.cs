// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;

using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.PrimaryLogicLayer.Logic.DataLogic
{
    public interface ILocationLinkedHardwareLogic
    {
    }

    public class LocationLinkedHardwareLogic: ILocationLinkedHardwareLogic
    {
        private readonly LocationLinkedHardwareQueries _dataContext;// = new LocationLinkedHardwareQueries();
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;

        public LocationLinkedHardwareLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;
            _dataContext = new LocationLinkedHardwareQueries();
        }

        #region Get              
        #region not interfaced                
        public async Task<List<LocalLocationLinkedHardware>> Get()
        {
            //bool output = true;
            List<LocalLocationLinkedHardware> output = new List<LocalLocationLinkedHardware>();
            try
            {
                output = await _dataContext.Get();
                //output.AddRange(preoutput);
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "LocationLinkedHardwareLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<LocalLocationLinkedHardware> Get(Guid input)
        {
            //bool output = true;
            LocalLocationLinkedHardware output = new LocalLocationLinkedHardware();
            try
            {
                //use input to find subscription
                output = await _dataContext.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "LocationLinkedHardwareLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<LocalLocationLinkedHardware> Get(long input)
        {
            //bool output = true;
            LocalLocationLinkedHardware output = new LocalLocationLinkedHardware();
            try
            {
                //use input to find subscription
                output = await _dataContext.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "LocationLinkedHardwareLogic.Get: suppressed exception"); }
            return output;
        }

        #endregion
        //public async Task<List<ILocationLinkedHardware>> Get()
        //{
        //    //bool output = true;
        //    List<ILocationLinkedHardware> output = new List<ILocationLinkedHardware>();
        //    try
        //    {
        //        output = await _LocationLinkedHardwareQueries.Get();
        //        //output.AddRange(preoutput);
        //    }
        //    catch { }
        //    return output;
        //}
        //public async Task<ILocationLinkedHardware> Get(Guid input)
        //{
        //    //bool output = true;
        //    ILocationLinkedHardware output = new LocalLocationLinkedHardware();
        //    try
        //    {
        //        //use input to find subscription
        //        output = await _LocationLinkedHardwareQueries.Get(input);
        //        //output = subscription;
        //    }
        //    catch { }
        //    return output;
        //}
        //public async Task<ILocationLinkedHardware> Get(long input)
        //{
        //    //bool output = true;
        //    ILocationLinkedHardware output = new LocalLocationLinkedHardware();
        //    try
        //    {
        //        //use input to find subscription
        //        output = await _LocationLinkedHardwareQueries.Get(input);
        //        //output = subscription;
        //    }
        //    catch { }
        //    return output;
        //}

        #endregion

        #region Post
        public async Task<LocalLocationLinkedHardware> Create(LocalLocationLinkedHardware input)
        {
            LocalLocationLinkedHardware output = new LocalLocationLinkedHardware();
            try
            {
                output = await _dataContext.Create(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "LocationLinkedHardwareLogic.Create: suppressed exception");
            }

            return output;
        }
        //public async Task<ILocationLinkedHardware> Create(ILocationLinkedHardware input)
        //{
        //    ILocationLinkedHardware output = new LocalLocationLinkedHardware();
        //    try
        //    {
        //        output = await _LocationLinkedHardwareQueries.Create(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion

        #region Update
        public async Task<LocalLocationLinkedHardware> Update(LocalLocationLinkedHardware input)
        {
            LocalLocationLinkedHardware output = new LocalLocationLinkedHardware();
            try
            {
                output = await _dataContext.Update(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "LocationLinkedHardwareLogic.Update: suppressed exception");
            }

            return output;
        }
        //public async Task<ILocationLinkedHardware> Update(ILocationLinkedHardware input)
        //{
        //    ILocationLinkedHardware output = new LocalLocationLinkedHardware();
        //    try
        //    {
        //        output = await _LocationLinkedHardwareQueries.Update(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion

        #region Delete
        public async Task<bool> Delete(LocalLocationLinkedHardware input)
        {
            bool output = false;
            try
            {
                output = await _dataContext.Delete(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "LocationLinkedHardwareLogic.Delete: suppressed exception");
            }

            return output;
        }
        //public async Task<bool> Delete(ILocationLinkedHardware input)
        //{
        //    bool output = false;
        //    try
        //    {
        //        output = await _LocationLinkedHardwareQueries.Delete(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion

    }
        
}
