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

    public interface ILocalAnalyticsQueries
    {
        Task<List<LocalAnalytics>> Get();
        Task<bool> Create(LocalAnalytics input);
        Task<bool> Create(List<LocalAnalytics> input);
        Task<LocalAnalytics> Get(long? id);
        Task<List<LocalAnalytics>> Get(DateTime startDate, DateTime endDate);

        /// <summary>
        /// One page of rows, newest first, with the optional search applied IN THE DATABASE.
        /// Returns the page and the total matching count.
        ///
        /// <para>
        /// T11. The only reader called <see cref="Get()"/>, which materialised the ENTIRE table
        /// ordered by an unindexed column, filtered it in memory across six columns, and then took
        /// 25 rows. Every view of the analytics screen paid for the whole table. This keeps the
        /// work in SQL so the cost is the page, not the history.
        /// </para>
        /// </summary>
        Task<(List<LocalAnalytics> Page, int TotalCount)> GetPage(string searchString, int pageNumber, int pageSize);
        //Task<List<LocalAnalytics>> GetNoGeoList();
        //Task Update(List<LocalAnalytics> listToUpdate);
        //Task<List<GeoIPData>> GetGeoIPData();

        /// <summary>
        /// Deletes at most <paramref name="batchSize"/> rows older than <paramref name="cutoffUtc"/>
        /// and returns how many went. Batched on purpose: a single DELETE across a table that has
        /// been collecting a row per HTTP request is a long transaction holding locks, which is
        /// exactly the surprise a retention job must not spring on a running node.
        /// </summary>
        Task<int> DeleteOlderThan(DateTime cutoffUtc, int batchSize);

    }
    public class LocalAnalyticsQueries: ILocalAnalyticsQueries
    {
        private readonly AnalyticsDbContext _context;
        public LocalAnalyticsQueries()
        {
            _context = new AnalyticsDbContext(AnalyticsDbContext.ops.DbOptions);
        }
        public async Task<bool> Create(LocalAnalytics input)
        {
            bool output = false;
            try
            {
                await _context.LocalAnalytics.AddAsync(input);
                //_context.LocalAnalytics.Update(input);
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
        /// <inheritdoc />
        public async Task<(List<LocalAnalytics> Page, int TotalCount)> GetPage(string searchString, int pageNumber, int pageSize)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 25;

                IQueryable<LocalAnalytics> query = _context.LocalAnalytics.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    // ILIKE via EF.Functions so the match runs in PostgreSQL. ToLower().Contains()
                    // on the client is what forced the whole table into memory in the first place.
                    string pattern = "%" + searchString.Trim() + "%";
                    query = query.Where(a =>
                        EF.Functions.ILike(a.IPAddress ?? string.Empty, pattern)
                        || EF.Functions.ILike(a.Query ?? string.Empty, pattern)
                        || EF.Functions.ILike(a.Path ?? string.Empty, pattern)
                        || EF.Functions.ILike(a.Referer ?? string.Empty, pattern)
                        || EF.Functions.ILike(a.UserAgent ?? string.Empty, pattern));
                }

                int total = await query.CountAsync();

                List<LocalAnalytics> page = await query
                    .OrderByDescending(a => a.TimeStamp)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return (page, total);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// UNBOUNDED. Loads every analytics row ever recorded. Retained for the chart builders that
        /// aggregate over the whole history; do NOT use it to render a list. See
        /// <see cref="GetPage"/>.
        /// </summary>
        public async Task<List<LocalAnalytics>> Get()
        {
            List<LocalAnalytics> output = new List<LocalAnalytics>();
            try
            {
                output = await _context.LocalAnalytics.AsNoTracking().OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<bool> Create(List<LocalAnalytics> input)
        {
            bool output = false;
            try
            {
                await _context.LocalAnalytics.AddRangeAsync(input);
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
        public async Task<LocalAnalytics> Get(long? id)
        {
            LocalAnalytics output = new LocalAnalytics();
            try
            {
                output = await _context.LocalAnalytics.FindAsync(id);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<LocalAnalytics>> Get(DateTime startDate, DateTime endDate)
        {
            List<LocalAnalytics> output = new List<LocalAnalytics>();
            try
            {
                output = await _context.LocalAnalytics
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

        //public async Task<List<LocalAnalytics>> GetNoGeoList()
        //{
        //    List<LocalAnalytics> output = new List<LocalAnalytics>();
        //    try
        //    {
        //        output = await _context.LocalAnalytics
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

        //public async Task Update(List<LocalAnalytics> listToUpdate)
        //{
        //    try
        //    {
        //        _context.LocalAnalytics.UpdateRange(listToUpdate);
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
        //        List<LocalAnalytics> temp = await Get(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

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


        /// <inheritdoc />
        public async Task<int> DeleteOlderThan(DateTime cutoffUtc, int batchSize)
        {
            try
            {
                if (batchSize <= 0) return 0;

                // Select ids first, then delete by id. Take() combined directly with ExecuteDelete
                // does not translate, and this keeps each statement small and predictable instead of
                // deleting an unbounded set in one transaction.
                List<long> ids = await _context.LocalAnalytics
                    .AsNoTracking()
                    .Where(a => a.TimeStamp < cutoffUtc)
                    .OrderBy(a => a.Id)
                    .Take(batchSize)
                    .Select(a => a.Id)
                    .ToListAsync();

                if (ids.Count == 0) return 0;

                return await _context.LocalAnalytics
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
