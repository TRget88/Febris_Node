// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    public class LocationLinkedUserQueries//: ILocationLinkedUserQueries
    {
        private readonly DataDbContext _dataDbContext;

        public LocationLinkedUserQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }
        public LocationLinkedUserQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<LocationLinkedUser> Get(long input)
        {
            LocationLinkedUser LocationLinkedUser = new LocationLinkedUser();
            try
            {
                LocationLinkedUser = await _dataDbContext.LocationLinkedUser.FindAsync(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return LocationLinkedUser;
        }
        public async Task<List<LocationLinkedUser>> Get()
        {
            List<LocationLinkedUser> LocationLinkedUserList = new List<LocationLinkedUser>();
            try
            {
                LocationLinkedUserList = await _dataDbContext.LocationLinkedUser.AsNoTracking().OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return LocationLinkedUserList;
        }

        public Task<LocationLinkedUser> Get(Guid input)
        {
            throw new NotImplementedException();
        }

        public async Task<List<LocationLinkedUser>> GetByUser(Guid input)
        {
            List<LocationLinkedUser> output = new List<LocationLinkedUser>();
            try
            {
                output = await _dataDbContext.LocationLinkedUser
                    .AsNoTracking()
                    .Where(i => i.UserId == input)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        public async Task<List<LocationLinkedUser>> GetByLocation(Guid input)
        {
            List<LocationLinkedUser> output = new List<LocationLinkedUser>();
            try
            {
                output = await _dataDbContext.LocationLinkedUser
                    .AsNoTracking()
                    .Where(i => i.LocationUUID == input)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        public async Task<List<LocationLinkedUser>> GetByLocation(long input)
        {
            List<LocationLinkedUser> output = new List<LocationLinkedUser>();
            try
            {
                output = await _dataDbContext.LocationLinkedUser
                    .AsNoTracking()
                    .Include(h => h.Location)
                    .Where(i => i.Location.Id == input)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        #endregion 

        #region Create
        public async Task<LocationLinkedUser> Create(LocationLinkedUser input)
        {
            try
            {
                await _dataDbContext.LocationLinkedUser.AddAsync(input);
                await _dataDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return input;
        }

        #endregion 

        #region Update
        public async Task<LocationLinkedUser> Update(LocationLinkedUser input)
        {
            try
            {
                _dataDbContext.LocationLinkedUser.Update(input);
                await _dataDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return input;
        }
        #endregion 

        #region Delete
        public async Task<bool> Delete(LocationLinkedUser input)
        {
            try
            {
                _dataDbContext.LocationLinkedUser.Remove(input);
                await _dataDbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
        #endregion 
    }
}
