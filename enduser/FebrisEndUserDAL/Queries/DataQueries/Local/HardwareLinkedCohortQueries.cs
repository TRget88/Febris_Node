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
    public interface IHardwareLinkedCohortQueries
    {
        Task<List<HardwareLinkedCohort>> Get();
        Task<HardwareLinkedCohort> Get(long? input);
        Task<HardwareLinkedCohort> Get(Guid? input);
        Task<List<HardwareLinkedCohort>> GetByHardware(long? input);
        Task<List<HardwareLinkedCohort>> GetByHardware(Guid input);
        Task<List<HardwareLinkedCohort>> GetByCohort(Guid input);
        Task<List<HardwareLinkedCohort>> GetByCohort(long? input);
        Task<bool> Delete(HardwareLinkedCohort input);
        Task<HardwareLinkedCohort> Update(HardwareLinkedCohort input);
        Task<HardwareLinkedCohort> Create(HardwareLinkedCohort input);
        Task<List<HardwareLinkedCohort>> Get(LocalHardware hardware);
    }

    public class HardwareLinkedCohortQueries : IHardwareLinkedCohortQueries
    {
        private readonly DataDbContext _context;

        public HardwareLinkedCohortQueries(DataDbContext dataDbContext)
        {
            _context = dataDbContext;
        }
        public HardwareLinkedCohortQueries()
        {
            _context = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<HardwareLinkedCohort> Get(long? input)
        {
            HardwareLinkedCohort Hardware = new HardwareLinkedCohort();
            try
            {
                Hardware = await _context.HardwareLinkedCohort
                    .AsNoTracking()
                    .Include(i => i.Hardware)
                    .Include(i => i.Cohort)
                    .Where(i => i.Id == input)
                    .FirstOrDefaultAsync();

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return Hardware;
        }
        public async Task<HardwareLinkedCohort> Get(Guid? input)
        {
            HardwareLinkedCohort Hardware = new HardwareLinkedCohort();
            try
            {
                Hardware = await _context.HardwareLinkedCohort
                   .AsNoTracking()
                   .Include(i => i.Hardware)
                   .Include(i => i.Cohort)
                   .Where(i => i.UUID == input)
                   .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return Hardware;
        }
        public async Task<List<HardwareLinkedCohort>> Get()
        {
            List<HardwareLinkedCohort> output = new List<HardwareLinkedCohort>();
            try
            {
                output = await _context.HardwareLinkedCohort
                   .AsNoTracking()
                   .Include(i => i.Hardware)
                   .Include(i => i.Cohort)
                   .OrderByDescending(i => i.TimeStamp).ToListAsync();

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<HardwareLinkedCohort>> Get(LocalHardware input)
        {
            List<HardwareLinkedCohort> Hardware = new List<HardwareLinkedCohort>();
            try
            {
                Hardware = await _context.HardwareLinkedCohort
                    .AsNoTracking()
                    .Include(i => i.Hardware)
                    .Include(i => i.Cohort)
                    .Where(i => i.Hardware.Id == input.Id)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
                return Hardware;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }


        }
        public async Task<List<HardwareLinkedCohort>> GetByHardware(long? input)
        {
            List<HardwareLinkedCohort> Hardware = new List<HardwareLinkedCohort>();
            try
            {
                Hardware = await _context.HardwareLinkedCohort
                    .AsNoTracking()
                    .Include(i => i.Hardware)
                    .Include(i => i.Cohort)
                    .Where(i => i.Hardware.Id == input)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return Hardware;
        }
        public async Task<List<HardwareLinkedCohort>> GetByHardware(Guid input)
        {
            List<HardwareLinkedCohort> Hardware = new List<HardwareLinkedCohort>();
            try
            {
                Hardware = await _context.HardwareLinkedCohort
                    .AsNoTracking()
                    .Include(i => i.Hardware)
                    .Include(i => i.Cohort)
                    .Where(i => i.Hardware.UUID == input)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return Hardware;
        }
        public async Task<List<HardwareLinkedCohort>> GetByCohort(Guid input)
        {
            List<HardwareLinkedCohort> Hardware = new List<HardwareLinkedCohort>();
            try
            {
                Hardware = await _context.HardwareLinkedCohort
                    .AsNoTracking()
                    .Include(i => i.Hardware)
                    .Include(i => i.Cohort)
                    .Where(i => i.Cohort.UUID == input)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return Hardware;
        }
        public async Task<List<HardwareLinkedCohort>> GetByCohort(long? input)
        {
            List<HardwareLinkedCohort> Hardware = new List<HardwareLinkedCohort>();
            try
            {
                Hardware = await _context.HardwareLinkedCohort
                    .AsNoTracking()
                    .Include(i => i.Hardware)
                    .Include(i => i.Cohort)
                    .Where(i => i.Cohort.Id == input)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return Hardware;
        }
        #endregion

        #region Create

        public async Task<HardwareLinkedCohort> Create(HardwareLinkedCohort input)
        {
            try
            {
                //await _context.HardwareLinkedCohort.AddAsync(input);
                _context.HardwareLinkedCohort.Update(input);
                await _context.SaveChangesAsync();
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

        public async Task<HardwareLinkedCohort> Update(HardwareLinkedCohort input)
        {
            try
            {
                _context.HardwareLinkedCohort.Update(input);
                await _context.SaveChangesAsync();
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

        public async Task<bool> Delete(HardwareLinkedCohort input)
        {
            try
            {
                _context.HardwareLinkedCohort.Remove(input);
                await _context.SaveChangesAsync();
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
