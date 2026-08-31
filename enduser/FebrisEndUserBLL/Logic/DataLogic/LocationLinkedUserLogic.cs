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
    public interface ILocationLinkedUserLogic
    {
    }

    public class LocationLinkedUserLogic: ILocationLinkedUserLogic
    {
        private readonly LocationLinkedUserQueries _dataContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;

        public LocationLinkedUserLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;
            _dataContext = new LocationLinkedUserQueries();
        }


        #region Get              
        #region not interfaced                
        public async Task<List<LocationLinkedUser>> Get()
        {
            //bool output = true;
            List<LocationLinkedUser> output = new List<LocationLinkedUser>();
            try
            {
                output = await _dataContext.Get();
                //output.AddRange(preoutput);
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "LocationLinkedUserLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<LocationLinkedUser> Get(Guid input)
        {
            //bool output = true;
            LocationLinkedUser output = new LocationLinkedUser();
            try
            {
                //use input to find subscription
                output = await _dataContext.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "LocationLinkedUserLogic.Get(Guid): suppressed exception"); }
            return output;
        }
        public async Task<LocationLinkedUser> Get(long input)
        {
            //bool output = true;
            LocationLinkedUser output = new LocationLinkedUser();
            try
            {
                //use input to find subscription
                output = await _dataContext.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "LocationLinkedUserLogic.Get(long): suppressed exception"); }
            return output;
        }

        #endregion
        //public async Task<List<ILocationLinkedUser>> Get()
        //{
        //    //bool output = true;
        //    List<ILocationLinkedUser> output = new List<ILocationLinkedUser>();
        //    try
        //    {
        //        output = await _LocationLinkedUserQueries.Get();
        //        //output.AddRange(preoutput);
        //    }
        //    catch { }
        //    return output;
        //}
        //public async Task<ILocationLinkedUser> Get(Guid input)
        //{
        //    //bool output = true;
        //    ILocationLinkedUser output = new LocationLinkedUser();
        //    try
        //    {
        //        //use input to find subscription
        //        output = await _LocationLinkedUserQueries.Get(input);
        //        //output = subscription;
        //    }
        //    catch { }
        //    return output;
        //}
        //public async Task<ILocationLinkedUser> Get(long input)
        //{
        //    //bool output = true;
        //    ILocationLinkedUser output = new LocationLinkedUser();
        //    try
        //    {
        //        //use input to find subscription
        //        output = await _LocationLinkedUserQueries.Get(input);
        //        //output = subscription;
        //    }
        //    catch { }
        //    return output;
        //}

        #endregion

        #region Post
        public async Task<LocationLinkedUser> Create(LocationLinkedUser input)
        {
            LocationLinkedUser output = new LocationLinkedUser();
            try
            {
                output = await _dataContext.Create(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "LocationLinkedUserLogic.Create: suppressed exception");
            }

            return output;
        }
        //public async Task<ILocationLinkedUser> Create(ILocationLinkedUser input)
        //{
        //    ILocationLinkedUser output = new LocationLinkedUser();
        //    try
        //    {
        //        output = await _LocationLinkedUserQueries.Create(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion

        #region Update
        public async Task<LocationLinkedUser> Update(LocationLinkedUser input)
        {
            LocationLinkedUser output = new LocationLinkedUser();
            try
            {
                output = await _dataContext.Update(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "LocationLinkedUserLogic.Update: suppressed exception");
            }

            return output;
        }
        //public async Task<ILocationLinkedUser> Update(ILocationLinkedUser input)
        //{
        //    ILocationLinkedUser output = new LocationLinkedUser();
        //    try
        //    {
        //        output = await _LocationLinkedUserQueries.Update(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion

        #region Delete
        public async Task<bool> Delete(LocationLinkedUser input)
        {
            bool output = false;
            try
            {
                output = await _dataContext.Delete(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "LocationLinkedUserLogic.Delete: suppressed exception");
            }

            return output;
        }
        //public async Task<bool> Delete(ILocationLinkedUser input)
        //{
        //    bool output = false;
        //    try
        //    {
        //        output = await _LocationLinkedUserQueries.Delete(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion


    }    
}
