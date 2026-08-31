// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.PrimaryLogicLayer.Logic.DataLogic
{
    public interface ILocationLogic
    {
        Task<List<Location>> Get();
        Task<Location> Get(long? id);
        Task<Location> Create(Location location);
        Task<Location> Update(Location location);
        Task<bool> Delete(long id);
    }

    public class LocationLogic : ILocationLogic
    {        
        private readonly ILocationQueries _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;

        public LocationLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;
            _context = new LocationQueries();
        }

        // DI refactor
        public LocationLogic(IHttpContextAccessor httpContextAccessor, ILocationQueries context)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _context = context;
        }


        #region Get
        public async Task<Location> Get(long? input)
        {
            Location output = new Location();
            try
            {
                //use input to find subscription
                output = await _context.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "LocationLogic.Get(long?): suppressed exception"); }
            return output;
        }
        public async Task<Location> Get(Guid? input)
        {
            //bool output = true;
            Location output = new Location();
            try
            {
                //use input to find subscription
                output = await _context.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "LocationLogic.Get(Guid?): suppressed exception"); }
            return output;
        }
        public async Task<List<Location>> Get()
        {
            List<Location> output = new List<Location>();
            try
            {
                //use input to find subscription
                output = await _context.Get();
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "LocationLogic.Get(): suppressed exception"); }
            return output;
        }
        #endregion

        #region Create
        public async Task<Location> Create(Location input)
        {
            //if (await IsLockedOut())
            //{
            //    return null;
            //}
            Location output = new Location();            
            double longitude = 0;
            double latitude = 0;
            try
            {
                if (!User.IsLocalAdmin()||!User.IsLocalFebrisAdmin())
                {
                    return null;
                }
               
                (latitude, longitude) = Geocoder.GetGeoCodes(input.Address,
                                                               input.City,
                                                               input.ZipCode,
                                                               input.State,
                                                               input.Country);
                input.Latitude = latitude;
                input.Longitude = longitude;
                output = await _context.Create(input);                
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }
        #endregion

        #region Update
        public async Task<Location> Update(Location input)
        {
            double longitude = 0;
            double latitude = 0;
            Location output = new Location();
            try
            {
                if (!User.IsLocalAdmin() || !User.IsLocalFebrisAdmin())
                {
                    return null;
                }

                Location original = await _context.Get(input.Id);
                                
                if(input.Longitude==0 && input.Latitude == 0)
                {
                    (latitude, longitude) = Geocoder.GetGeoCodes(input.Address,
                                                               input.City,
                                                               input.ZipCode,
                                                               input.State,
                                                               input.Country);
                    input.Latitude = latitude;
                    input.Longitude = longitude;
                }

                //use input to find subscription
                output = await _context.Update(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "LocationLogic.Update: suppressed exception"); }
            return output;
        }
        #endregion

        #region Delete
        public async Task<bool> Delete(long input)
        {
            bool output = false;
            try
            {
                //use input to find subscription
                output = await _context.Delete(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "LocationLogic.Delete: suppressed exception"); }
            return output;
        }
        #endregion 
    }
        
}
