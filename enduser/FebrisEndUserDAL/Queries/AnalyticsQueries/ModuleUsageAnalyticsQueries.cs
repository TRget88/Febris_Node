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
    public interface IModuleUsageAnalyticsQueries
    {
        Task<List<ModuleUsageAnalytics>> Get();
        Task<bool> Create(ModuleUsageAnalytics input);
        Task<bool> Create(List<ModuleUsageAnalytics> input);
        Task<ModuleUsageAnalytics> Get(long? id);
        Task<List<ModuleUsageAnalytics>> Get(DateTime startDate, DateTime endDate);
        //Task<List<ModuleUsageAnalytics>> GetNoGeoList();
        Task Update(List<ModuleUsageAnalytics> listToUpdate);
        //Task<List<GeoIPData>> GetGeoIPData();
        //Task<List<GeoIPData>> GetGeoIPData(DateTime startDate, DateTime endDate);
        Task<List<ModuleUsageAnalytics>> SearchGet(string searchString);
        //Task<List<ModuleUsageAnalytics>> Get(List<Module> moduleList, DateTime startDate, DateTime endDate);
        //Task<List<ModuleUsageAnalytics>> Get(List<Module> moduleList);

        /// <summary>
        /// Clears the per-request identifiers on rows older than <paramref name="cutoffUtc"/> while
        /// KEEPING the row, and returns how many were changed.
        ///
        /// <para>
        /// Module launches are never swept, because <c>LauncherLogic</c> does not persist the launch
        /// statement: for a learner who launches and never completes, this row is the only record
        /// the node holds that they engaged with the module at all. Deleting it would destroy a
        /// student record. Clearing IP, user agent, referer and query removes the privacy liability
        /// without touching the fact of the launch, the user, or the module.
        /// </para>
        /// </summary>
        Task<int> AnonymiseOlderThan(DateTime cutoffUtc, int batchSize);

    }
    public class ModuleUsageAnalyticsQueries : IModuleUsageAnalyticsQueries
    {
        private readonly AnalyticsDbContext _context;
        public ModuleUsageAnalyticsQueries()
        {
            _context = new AnalyticsDbContext(AnalyticsDbContext.ops.DbOptions);
        }

        /// <summary>
        /// DI refactor (strangler): the scoped per-tenant
        /// <see cref="AnalyticsDbContext"/> flows in from <c>AddFebrisUserNodeDataAccess</c>,
        /// replacing the static-ops self-newing above wherever the context is registered.
        /// Surfaced by the no-hub boot smoke: the API's launcher graph resolved this class
        /// through the static path, which cannot construct without the developer config.
        /// </summary>
        public ModuleUsageAnalyticsQueries(AnalyticsDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Create(ModuleUsageAnalytics input)
        {
            bool output = false;
            try
            {
                //try
                //{
                //    await _context.ModuleUsageAnalytics.AddAsync(input);
                //    await _context.SaveChangesAsync();
                //}
                //catch (Exception)
                //{
                //    _context.ModuleUsageAnalytics.Update(input);
                //    await _context.SaveChangesAsync();                    
                //}
                await _context.ModuleUsageAnalytics.AddAsync(input);
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
        public async Task<bool> Create(List<ModuleUsageAnalytics> input)
        {
            bool output = false;
            try
            {
                await _context.ModuleUsageAnalytics.AddRangeAsync(input);
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
        public async Task<List<ModuleUsageAnalytics>> SearchGet(string searchString)
        {
            List<ModuleUsageAnalytics> output = new List<ModuleUsageAnalytics>();
            try
            {
                output = await _context.ModuleUsageAnalytics.AsNoTracking().Where(b => (b.IPAddress.Contains(searchString))
                   || (b.Query.Contains(searchString))
                   || (b.Path.Contains(searchString))
                   || (b.Referer.Contains(searchString))
                   || (b.TimeStamp.ToString().Contains(searchString))
                   || (b.UserAgent.Contains(searchString))
                   ).OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<ModuleUsageAnalytics>> Get()
        {
            List<ModuleUsageAnalytics> output = new List<ModuleUsageAnalytics>();
            try
            {
                output = await _context.ModuleUsageAnalytics.AsNoTracking()
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<ModuleUsageAnalytics> Get(long? id)
        {
            ModuleUsageAnalytics output = new ModuleUsageAnalytics();
            try
            {
                output = await _context.ModuleUsageAnalytics.FindAsync(id);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<ModuleUsageAnalytics>> Get(DateTime startDate, DateTime endDate)
        {
            List<ModuleUsageAnalytics> output = new List<ModuleUsageAnalytics>();
            try
            {
                output = await _context.ModuleUsageAnalytics.AsNoTracking()
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
        //public async Task<List<ModuleUsageAnalytics>> Get(List<Module> moduleList, DateTime startDate, DateTime endDate)
        //{
        //    List<ModuleUsageAnalytics> output = new List<ModuleUsageAnalytics>();
        //    try
        //    {
        //        output = await _context.ModuleUsageAnalytics
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
        //public async Task<List<ModuleUsageAnalytics>> Get(List<Module> moduleList)
        //{
        //    List<ModuleUsageAnalytics> output = new List<ModuleUsageAnalytics>();
        //    try
        //    {
        //        if (moduleList == null || moduleList == default || moduleList.Count() == 0)
        //        {
        //            return output;
        //        }

        //        output = await _context.ModuleUsageAnalytics
        //            .Where(i => moduleList.Any(j => j.Id == i.ModuleId))
        //            .OrderByDescending(i => i.TimeStamp)
        //            .ToListAsync();
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
        //        List<ModuleUsageAnalytics> temp = await Get(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
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
        //        List<ModuleUsageAnalytics> temp = await Get(startDate, endDate);

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

        //public async Task<List<ModuleUsageAnalytics>> GetNoGeoList()
        //{
        //    List<ModuleUsageAnalytics> output = new List<ModuleUsageAnalytics>();
        //    try
        //    {
        //        output = await _context.ModuleUsageAnalytics
        //            .Where(i => i.GeoIPDataId == 0
        //            || i.GeoIPDataId == null)
        //            .OrderByDescending(i => i.TimeStamp).ToListAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}


        public async Task Update(List<ModuleUsageAnalytics> listToUpdate)
        {
            try
            {
                _context.ModuleUsageAnalytics.UpdateRange(listToUpdate);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }



        /// <inheritdoc />
        public async Task<int> AnonymiseOlderThan(DateTime cutoffUtc, int batchSize)
        {
            try
            {
                if (batchSize <= 0) return 0;

                List<long> ids = await _context.ModuleUsageAnalytics
                    .AsNoTracking()
                    .Where(a => a.TimeStamp < cutoffUtc
                                && (a.IPAddress != null || a.UserAgent != null || a.Referer != null || a.Query != null))
                    .OrderBy(a => a.Id)
                    .Take(batchSize)
                    .Select(a => a.Id)
                    .ToListAsync();

                if (ids.Count == 0) return 0;

                // The row, its UserId, its ModuleUUID and its timestamp all survive. Only the
                // per-request identifiers go.
                return await _context.ModuleUsageAnalytics
                    .Where(a => ids.Contains(a.Id))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(a => a.IPAddress, (string)null)
                        .SetProperty(a => a.UserAgent, (string)null)
                        .SetProperty(a => a.Referer, (string)null)
                        .SetProperty(a => a.Query, (string)null));
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

    }


}
