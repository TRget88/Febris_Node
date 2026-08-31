// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
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
    public interface ILocationLinkedHardwareQueries
    {
    }


    public class LocationLinkedHardwareQueries: ILocationLinkedHardwareQueries
    {
        private readonly DataDbContext _dataDbContext;

        public LocationLinkedHardwareQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }
        public LocationLinkedHardwareQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }


        #region Get
        public async Task<LocalLocationLinkedHardware> Get(long input)
        {
            LocalLocationLinkedHardware LocalLocationLinkedHardware = new LocalLocationLinkedHardware();
            try
            {
                LocalLocationLinkedHardware = await _dataDbContext.LocationLinkedHardware.FindAsync(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return LocalLocationLinkedHardware;
        }
        public async Task<List<LocalLocationLinkedHardware>> Get()
        {
            List<LocalLocationLinkedHardware> LocationLinkedHardwareList = new List<LocalLocationLinkedHardware>();
            try
            {
                LocationLinkedHardwareList = await _dataDbContext.LocationLinkedHardware.AsNoTracking().OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return LocationLinkedHardwareList;
        }

        public Task<LocalLocationLinkedHardware> Get(Guid input)
        {
            throw new NotImplementedException();
        }

        public async Task<List<LocalLocationLinkedHardware>> GetByHardware(Guid input)
        {
            List<LocalLocationLinkedHardware> LocalLocationLinkedHardware = new List<LocalLocationLinkedHardware>();
            try
            {
                LocalLocationLinkedHardware = await _dataDbContext.LocationLinkedHardware
                    .AsNoTracking()
                    .Where(i => i.HardwareUUID == input)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return LocalLocationLinkedHardware;
        }
        public async Task<List<LocalLocationLinkedHardware>> GetByHardware(long input)
        {
            List<LocalLocationLinkedHardware> LocalLocationLinkedHardware = new List<LocalLocationLinkedHardware>();
            try
            {
                LocalLocationLinkedHardware = await _dataDbContext.LocationLinkedHardware
                    .AsNoTracking()
                    .Include(h => h.Hardware)
                    .Include(h => h.Location)
                    .Where(i => i.Hardware.Id == input)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return LocalLocationLinkedHardware;
        }
        public async Task<List<LocalLocationLinkedHardware>> GetByLocation(Guid input)
        {
            List<LocalLocationLinkedHardware> LocalLocationLinkedHardware = new List<LocalLocationLinkedHardware>();
            try
            {
                LocalLocationLinkedHardware = await _dataDbContext.LocationLinkedHardware
                    .AsNoTracking()
                    .Where(i => i.LocationUUID == input)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return LocalLocationLinkedHardware;
        }
        public async Task<List<LocalLocationLinkedHardware>> GetByLocation(long input)
        {
            List<LocalLocationLinkedHardware> LocalLocationLinkedHardware = new List<LocalLocationLinkedHardware>();
            try
            {
                LocalLocationLinkedHardware = await _dataDbContext.LocationLinkedHardware
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

            return LocalLocationLinkedHardware;
        }
        #endregion 

        #region Create
        public async Task<LocalLocationLinkedHardware> Create(LocalLocationLinkedHardware input)
        {
            try
            {
                _dataDbContext.LocationLinkedHardware.Update(input);//.AddAsync(input);
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
        public async Task<LocalLocationLinkedHardware> Update(LocalLocationLinkedHardware input)
        {
            try
            {
                _dataDbContext.LocationLinkedHardware.Update(input);
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
        public async Task<bool> Delete(LocalLocationLinkedHardware input)
        {
            try
            {
                _dataDbContext.LocationLinkedHardware.Remove(input);
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
