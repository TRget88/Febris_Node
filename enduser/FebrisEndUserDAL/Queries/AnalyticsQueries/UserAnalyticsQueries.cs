// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.AnalyticsModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.AnalyticsQueries
{
    public interface IUserAnalyticsQueries
    {
        Task<List<UserAnalytics>> Get();
        Task<bool> Create(UserAnalytics input);
        Task<bool> Create(List<UserAnalytics> input);
        Task<UserAnalytics> Get(long? id);
        Task<List<UserAnalytics>> Get(DateTime startDate, DateTime endDate);
        //Task<List<UserAnalytics>> GetNoGeoList();
        //Task Update(List<UserAnalytics> listToUpdate);
        //Task<List<GeoIPData>> GetGeoIPData();
    }


    public class UserAnalyticsQueries: IUserAnalyticsQueries
    {
        private readonly AnalyticsDbContext _context;
        public UserAnalyticsQueries()
        {
            _context = new AnalyticsDbContext(AnalyticsDbContext.ops.DbOptions);
        }
        public async Task<bool> Create(UserAnalytics input)
        {
            bool output = false;
            try
            {
                await _context.UserAnalytics.AddAsync(input);
                await _context.SaveChangesAsync();
                output = true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<UserAnalytics>> Get()
        {
            List<UserAnalytics> output = new List<UserAnalytics>();
            try
            {
                output = await _context.UserAnalytics.AsNoTracking().OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<bool> Create(List<UserAnalytics> input)
        {
            bool output = false;
            try
            {
                await _context.UserAnalytics.AddRangeAsync(input);
                await _context.SaveChangesAsync();
                output = true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<UserAnalytics> Get(long? id)
        {
            UserAnalytics output = new UserAnalytics();
            try
            {
                output = await _context.UserAnalytics.FindAsync(id);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<UserAnalytics>> Get(DateTime startDate, DateTime endDate)
        {
            List<UserAnalytics> output = new List<UserAnalytics>();
            try
            {
                output = await _context.UserAnalytics
                    .AsNoTracking()
                    .Where(i => i.TimeStamp >= startDate && i.TimeStamp <= endDate.AddDays(1))
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
                    
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        //public async Task<List<UserAnalytics>> GetNoGeoList()
        //{
        //    List<UserAnalytics> output = new List<UserAnalytics>();
        //    try
        //    {
        //        output = await _context.UserAnalytics
        //            .Where(i => i.GeoIPDataId == 0
        //            || i.GeoIPDataId == null)
        //            .ToListAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}

        //public async Task Update(List<UserAnalytics> listToUpdate)
        //{
        //    try
        //    {
        //        _context.UserAnalytics.UpdateRange(listToUpdate);
        //        await _context.SaveChangesAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //}

        //public async Task<List<GeoIPData>> GetGeoIPData()
        //{
        //    List<GeoIPData> output = new List<GeoIPData>();
        //    try
        //    {
        //        List<UserAnalytics> temp = await Get(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        //        List<GeoIPData> geoList = await _context.GeoIPData
        //            .Include(i => i.GeoASN)
        //            .Include(i => i.GeoIPByCity)
        //            .Include(i => i.GeoIPByCountry)
        //            .ToListAsync();

        //        foreach (var k in temp)
        //        {
        //            GeoIPData geoTemp = geoList
        //            .Where(i => i.Id == k.GeoIPDataId)
        //            .FirstOrDefault();
        //            output.Add(geoTemp);
        //        }

        //        //output = await _context.GeoIPData
        //        //    .Include(i => i.GeoASN)
        //        //    .Include(i => i.GeoIPByCity)
        //        //    .Include(i => i.GeoIPByCountry)
        //        //    .ToListAsync();

        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
    }

   
}
