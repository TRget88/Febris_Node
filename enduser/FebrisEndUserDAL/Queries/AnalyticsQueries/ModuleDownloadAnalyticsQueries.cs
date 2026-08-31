// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.AnalyticsModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.AnalyticsQueries
{
    public interface IModuleDownloadAnalyticsQueries
    {
        Task<List<ModuleDownloadAnalytics>> Get();
        Task<bool> Create(ModuleDownloadAnalytics input);
        Task<bool> Create(List<ModuleDownloadAnalytics> input);
        Task<ModuleDownloadAnalytics> Get(long? id);
        Task<List<ModuleDownloadAnalytics>> Get(DateTime startDate, DateTime endDate);
        //Task<List<ModuleDownloadAnalytics>> GetNoGeoList();
        Task Update(List<ModuleDownloadAnalytics> listToUpdate);
        //Task<List<GeoIPData>> GetGeoIPData();
        //Task<List<GeoIPData>> GetGeoIPData(DateTime startDate, DateTime endDate);
        Task<List<ModuleDownloadAnalytics>> SearchGet(string searchString);
        //void LogRequest(ClaimsPrincipal user, Hardware hardware, Module module);
        //Task<List<ModuleDownloadAnalytics>> Get(List<Module> moduleList, DateTime startDate, DateTime endDate);
        //Task<List<ModuleDownloadAnalytics>> Get(List<Module> moduleList);

        /// <summary>
        /// Deletes at most <paramref name="batchSize"/> rows older than <paramref name="cutoffUtc"/>
        /// and returns how many went. Batched on purpose: a single DELETE across a table that has
        /// been collecting a row per HTTP request is a long transaction holding locks, which is
        /// exactly the surprise a retention job must not spring on a running node.
        /// </summary>
        Task<int> DeleteOlderThan(DateTime cutoffUtc, int batchSize);

    }
    public class ModuleDownloadAnalyticsQueries : IModuleDownloadAnalyticsQueries
    {
        private readonly AnalyticsDbContext _context;
        public ModuleDownloadAnalyticsQueries()
        {
            _context = new AnalyticsDbContext(AnalyticsDbContext.ops.DbOptions);
        }

        /// <summary>
        /// DI refactor (strangler): the scoped per-tenant
        /// <see cref="AnalyticsDbContext"/> flows in from <c>AddFebrisUserNodeDataAccess</c>,
        /// replacing the static-ops self-newing above wherever the context is registered.
        /// Surfaced by the no-hub boot smoke alongside its ModuleUsage sibling.
        /// </summary>
        public ModuleDownloadAnalyticsQueries(AnalyticsDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Create(ModuleDownloadAnalytics input)
        {
            bool output = false;
            try
            {
                //try
                //{
                //    await _context.ModuleDownloadAnalytics.AddAsync(input);
                //    await _context.SaveChangesAsync();
                //}
                //catch (Exception)
                //{
                //    _context.ModuleDownloadAnalytics.Update(input);
                //    await _context.SaveChangesAsync();                    
                //}
                await _context.ModuleDownloadAnalytics.AddAsync(input);
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
        public async Task<bool> Create(List<ModuleDownloadAnalytics> input)
        {
            bool output = false;
            try
            {
                await _context.ModuleDownloadAnalytics.AddRangeAsync(input);
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
        public async Task<List<ModuleDownloadAnalytics>> SearchGet(string searchString)
        {
            List<ModuleDownloadAnalytics> output = new List<ModuleDownloadAnalytics>();
            try
            {
                output = await _context.ModuleDownloadAnalytics.AsNoTracking().Where(b => (b.IPAddress.Contains(searchString))
                   || (b.Query.Contains(searchString))
                   || (b.Path.Contains(searchString))
                   || (b.Referer.Contains(searchString))
                   || (b.TimeStamp.ToString().Contains(searchString))
                   || (b.UserAgent.Contains(searchString))
                   ).OrderByDescending(i => i.TimeStamp)
                   .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<ModuleDownloadAnalytics>> Get()
        {
            List<ModuleDownloadAnalytics> output = new List<ModuleDownloadAnalytics>();
            try
            {
                output = await _context.ModuleDownloadAnalytics
                    .AsNoTracking()
                    .OrderByDescending(i => i.TimeStamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<ModuleDownloadAnalytics> Get(long? id)
        {
            ModuleDownloadAnalytics output = new ModuleDownloadAnalytics();
            try
            {
                output = await _context.ModuleDownloadAnalytics.FindAsync(id);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<ModuleDownloadAnalytics>> Get(DateTime startDate, DateTime endDate)
        {
            List<ModuleDownloadAnalytics> output = new List<ModuleDownloadAnalytics>();
            try
            {
                output = await _context.ModuleDownloadAnalytics
                    .AsNoTracking()
                    .Where(i => i.TimeStamp.Date >= startDate.Date
                    && i.TimeStamp.Date <= endDate.AddDays(1).Date)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        //public async Task<List<ModuleDownloadAnalytics>> Get(List<Module> moduleList, DateTime startDate, DateTime endDate)
        //{
        //    List<ModuleDownloadAnalytics> output = new List<ModuleDownloadAnalytics>();
        //    try
        //    {
        //        output = await _context.ModuleDownloadAnalytics
        //            .Where(i =>
        //            i.TimeStamp >= startDate &&
        //            i.TimeStamp <= endDate.AddDays(1) &&
        //            moduleList.Any(j => j.Id == i.ModuleId)
        //            )
        //            .OrderByDescending(i => i.TimeStamp).ToListAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        //public async Task<List<ModuleDownloadAnalytics>> Get(List<Module> moduleList)
        //{
        //    List<ModuleDownloadAnalytics> output = new List<ModuleDownloadAnalytics>();
        //    try
        //    {
        //        output = await _context.ModuleDownloadAnalytics
        //            .Where(i =>
        //            moduleList.Any(j => j.Id == i.ModuleId)
        //            )
        //            .OrderByDescending(i => i.TimeStamp).ToListAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        //public async Task<List<GeoIPData>> GetGeoIPData()
        //{
        //    List<GeoIPData> output = new List<GeoIPData>();
        //    try
        //    {
        //        List<ModuleDownloadAnalytics> temp = await Get(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        //        List<GeoIPData> geoList = await _context.GeoIPData
        //            .Include(i => i.GeoASN)
        //            .Include(i => i.GeoIPByCity)
        //            .Include(i => i.GeoIPByCountry)
        //            .OrderByDescending(i => i.TimeStamp).ToListAsync();

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
        //        //    .OrderByDescending(i => i.TimeStamp).ToListAsync();

        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        //public async Task<List<GeoIPData>> GetGeoIPData(DateTime startDate, DateTime endDate)
        //{
        //    List<GeoIPData> output = new List<GeoIPData>();
        //    try
        //    {
        //        List<ModuleDownloadAnalytics> temp = await Get(startDate, endDate);

        //        List<GeoIPData> geoList = await _context.GeoIPData
        //            .Include(i => i.GeoASN)
        //            .Include(i => i.GeoIPByCity)
        //            .Include(i => i.GeoIPByCountry)
        //            .Where(i => i.TimeStamp >= startDate
        //            && i.TimeStamp <= endDate)
        //            .OrderByDescending(i => i.TimeStamp).ToListAsync();

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
        //        //    .OrderByDescending(i => i.TimeStamp).ToListAsync();

        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        //public async Task<List<ModuleDownloadAnalytics>> GetNoGeoList()
        //{
        //    List<ModuleDownloadAnalytics> output = new List<ModuleDownloadAnalytics>();
        //    try
        //    {
        //        output = await _context.ModuleDownloadAnalytics
        //            .Where(i => i.GeoIPDataId == 0
        //            || i.GeoIPDataId == default)
        //            .OrderByDescending(i => i.TimeStamp).ToListAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        public async Task Update(List<ModuleDownloadAnalytics> listToUpdate)
        {
            try
            {
                _context.ModuleDownloadAnalytics.UpdateRange(listToUpdate);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }



        /// <inheritdoc />
        public async Task<int> DeleteOlderThan(DateTime cutoffUtc, int batchSize)
        {
            try
            {
                if (batchSize <= 0) return 0;

                // Select ids first, then delete by id. Take() combined directly with ExecuteDelete
                // does not translate, and this keeps each statement small and predictable instead of
                // deleting an unbounded set in one transaction.
                List<long> ids = await _context.ModuleDownloadAnalytics
                    .AsNoTracking()
                    .Where(a => a.TimeStamp < cutoffUtc)
                    .OrderBy(a => a.Id)
                    .Take(batchSize)
                    .Select(a => a.Id)
                    .ToListAsync();

                if (ids.Count == 0) return 0;

                return await _context.ModuleDownloadAnalytics
                    .Where(a => ids.Contains(a.Id))
                    .ExecuteDeleteAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

    }
}
