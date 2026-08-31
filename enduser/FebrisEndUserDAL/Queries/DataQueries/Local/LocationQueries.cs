// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    public interface ILocationQueries
    {
        Task<List<Location>> Get();
        Task<Location> Get(Guid? input);
        Task<Location> Get(long? input);
        Task<Location> Create(Location input);
        Task<bool> Delete(long input);
        Task<Location> Update(Location input);
    }
    public class LocationQueries : ILocationQueries
    {
        private readonly DataDbContext _dataDbContext;

        public LocationQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }
        public LocationQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }
        

        #region Get        
        public async Task<Location> Get(long? input)
        {
            Location output = new Location();
            try
            {
                output = await _dataDbContext.Location.FindAsync(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        public async Task<Location> Get(Guid? input)
        {
            Location output = new Location();
            try
            {
                output = await _dataDbContext.Location.AsNoTracking()
                    .Where(i => i.UUID == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<Location>> Get()
        {
            List<Location> output = new List<Location>();
            try
            {
                output = await _dataDbContext.Location.AsNoTracking().OrderByDescending(i => i.TimeStamp).ToListAsync();
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
        public async Task<Location> Create(Location input)
        {
            try
            {
                //await _dataDbContext.Location.AddAsync(input);
                _dataDbContext.Location.Update(input);
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
        public async Task<Location> Update(Location input)
        {
            try
            {
                _dataDbContext.Location.Update(input);
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
        public async Task<bool> Delete(Location input)
        {
            try
            {
                _dataDbContext.Location.Remove(input);
                await _dataDbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
               
        public async Task<bool> Delete(long input)
        {
            try
            {

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            throw new NotImplementedException();
        }


        #endregion
    }

    
}
