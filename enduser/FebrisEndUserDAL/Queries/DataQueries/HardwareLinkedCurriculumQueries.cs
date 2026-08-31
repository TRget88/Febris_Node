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
    public interface IHardwareLinkedCurriculumQueries
    {
    }
    public class HardwareLinkedCurriculumQueries: IHardwareLinkedCurriculumQueries
    {
        private readonly DataDbContext _dataDbContext;

        public HardwareLinkedCurriculumQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }
        public HardwareLinkedCurriculumQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        #region Normal queries

        #region Get
        public async Task<LocalHardwareLinkedCurriculum> Get(long input)
        {
            LocalHardwareLinkedCurriculum output = new LocalHardwareLinkedCurriculum();
            try
            {
                output = await _dataDbContext.HardwareLinkedCurriculum.FindAsync(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "HardwareLinkedCurriculumQueries.Get(long): suppressed exception");
            }

            return output;
        }
        public async Task<LocalHardwareLinkedCurriculum> Get(Guid input)
        {
            LocalHardwareLinkedCurriculum output = new LocalHardwareLinkedCurriculum();
            try
            {
                output = await _dataDbContext.HardwareLinkedCurriculum
                    .AsNoTracking()
                    .Where(i => i.UUID == input)
                    .FirstOrDefaultAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "HardwareLinkedCurriculumQueries.Get(Guid): suppressed exception");
            }

            return output;
        }
        public async Task<List<LocalHardwareLinkedCurriculum>> Get()
        {
            List<LocalHardwareLinkedCurriculum> output = new List<LocalHardwareLinkedCurriculum>();
            try
            {
                output = await _dataDbContext.HardwareLinkedCurriculum.AsNoTracking().OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "HardwareLinkedCurriculumQueries.Get: suppressed exception");
            }

            return output;
        }

        public async Task<List<LocalHardwareLinkedCurriculum>> GetByHardware(long id)
        {
            List<LocalHardwareLinkedCurriculum> output = new List<LocalHardwareLinkedCurriculum>();
            try
            {
                output = await _dataDbContext.HardwareLinkedCurriculum
                    .AsNoTracking()
                    .Include(i=>i.Hardware)
                    .Include(i => i.Curriculum)
                    .Where(i=>i.Hardware.Id==id)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "HardwareLinkedCurriculumQueries.GetByHardware: suppressed exception");
            }

            return output;
        }
        //public async Task<LocalHardwareLinkedCurriculum> GetByContentDeveloper(long input)
        //{
        //    LocalHardwareLinkedCurriculum output = new LocalHardwareLinkedCurriculum();
        //    try
        //    {
        //        output = await _dataDbContext.HardwareLinkedCurriculum
        //           .Include(i => i.ContentDeveloper)
        //           .Where(i => i.ContentDeveloper.Id == input)
        //           .FirstOrDefaultAsync();
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<LocalHardwareLinkedCurriculum> GetByContentDeveloper(Guid input)
        //{
        //    LocalHardwareLinkedCurriculum output = new LocalHardwareLinkedCurriculum();
        //    try
        //    {
        //        output = await _dataDbContext.HardwareLinkedCurriculum
        //            .Include(i => i.ContentDeveloper)
        //            .Where(i => i.ContentDeveloperUUID == input)
        //            .FirstOrDefaultAsync();
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<LocalHardwareLinkedCurriculum> GetByContentDeveloper(ContentDeveloper input)
        //{
        //    LocalHardwareLinkedCurriculum output = new LocalHardwareLinkedCurriculum();
        //    try
        //    {
        //        output = await _dataDbContext.HardwareLinkedCurriculum
        //            .Include(i => i.ContentDeveloper)
        //            .Where(i => i.ContentDeveloper.Id == input.Id)
        //            .FirstOrDefaultAsync();
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}


        #endregion

        #region Create

        public async Task<LocalHardwareLinkedCurriculum> Create(LocalHardwareLinkedCurriculum input)
        {
            try
            {
                await _dataDbContext.HardwareLinkedCurriculum.AddAsync((LocalHardwareLinkedCurriculum)input);
                await _dataDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "HardwareLinkedCurriculumQueries.Create: suppressed exception");
            }

            return input;
        }

        #endregion

        #region Update

        public async Task<LocalHardwareLinkedCurriculum> Update(LocalHardwareLinkedCurriculum input)
        {
            try
            {
                _dataDbContext.Entry(input).Property(e => e.LastUpdateTimeStamp).IsModified = true;
                _dataDbContext.HardwareLinkedCurriculum.Update((LocalHardwareLinkedCurriculum)input);
                await _dataDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "HardwareLinkedCurriculumQueries.Update: suppressed exception");
            }

            return input;
        }
        #endregion

        #region Delete

        public async Task<bool> Delete(LocalHardwareLinkedCurriculum input)
        {
            try
            {
                _dataDbContext.HardwareLinkedCurriculum.Remove((LocalHardwareLinkedCurriculum)input);
                await _dataDbContext.SaveChangesAsync();
                return true;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "HardwareLinkedCurriculumQueries.Delete: suppressed exception");
                return false;
            }
        }

       
        #endregion

        #endregion

    }

   
}
