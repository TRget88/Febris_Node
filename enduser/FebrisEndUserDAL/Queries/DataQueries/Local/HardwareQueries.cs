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
    public interface IHardwareQueries
    {
        Task<LocalHardware> Get(long? id);
        Task<LocalHardware> GetByKey(string licenseKey);
        Task<LocalHardware> Get(Guid? id);
        Task<List<LocalHardware>> Get();
        Task<LocalHardware> Create(LocalHardware hardware);
        Task<LocalHardware> Update(LocalHardware hardware);
        Task<List<LocalHardware>> Get(DateTime startDate, DateTime endDate);
    }

    public class HardwareQueries: IHardwareQueries
    {
        private readonly DataDbContext _context;

        public HardwareQueries(DataDbContext dataDbContext)
        {
            _context = dataDbContext;
        }
        public HardwareQueries()
        {
            _context = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<LocalHardware> Get(long? input)
        {
            LocalHardware Hardware = new LocalHardware();
            try
            {
                Hardware = await _context.Hardware.FindAsync(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return Hardware;
        }
        public async Task<LocalHardware> Get(Guid? input)
        {
            LocalHardware Hardware = new LocalHardware();
            try
            {
                Hardware = await _context.Hardware
                    .AsNoTracking()
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
        public async Task<List<LocalHardware>> Get()
        {
            List<LocalHardware> HardwareList = new List<LocalHardware>();
            try
            {
                HardwareList = await _context.Hardware.AsNoTracking().OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return HardwareList;
        }
        /// <summary>
        /// Resolve a device by its authentication credential.
        ///
        /// <para>
        /// The stored column holds the HASH, never the credential itself (audit T9), so the incoming
        /// value is hashed and the hashes are compared. The device keeps sending the same string it
        /// always did -- this is a storage change, not a protocol change -- which is also why the
        /// migration that hashed existing rows did not strand already-provisioned devices.
        /// </para>
        ///
        /// <para>
        /// The hash is deterministic on purpose. A per-row salt would force a table scan here and
        /// defeat the unique index on this column.
        /// </para>
        /// </summary>
        public async Task<LocalHardware> GetByKey(string id)
        {
            LocalHardware output = new LocalHardware();
            try
            {
                string hashed = Febris.SharedServices.DeviceCredential.Hash(id);

                output = await _context.Hardware
                    .AsNoTracking()
                    //.Include(i => i.HardwareType)
                    .Where(i => i.PhysicalLicense == hashed)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<LocalHardware>> Get(DateTime startDate, DateTime endDate)
        {
            List<LocalHardware> output = new List<LocalHardware>();
            try
            {
                output = await _context.Hardware
                    .AsNoTracking()
                    //.Include(i => i.HardwareType)
                    .Where(i =>
                   i.TimeStamp.Date >= startDate.Date &&
                   i.TimeStamp.Date <= endDate.Date)
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
        #endregion

        #region Create

        public async Task<LocalHardware> Create(LocalHardware input)
        {
            try
            {
                await _context.Hardware.AddAsync(input);
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
        
        public async Task<LocalHardware> Update(LocalHardware input)
        {
            try
            {
                _context.Hardware.Update(input);
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
       
        public async Task<bool> Delete(LocalHardware input)
        {
            try
            {
                _context.Hardware.Remove(input);
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
