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
    //This means I need to query Module Queries

    public interface IHardwareLinkedModuleQueries
    {
        Task<bool> Delete(LocalHardwareLinkedModule input);
        Task<LocalHardwareLinkedModule> Update(LocalHardwareLinkedModule input);
        Task<LocalHardwareLinkedModule> Create(LocalHardwareLinkedModule input);
        Task<List<LocalHardwareLinkedModule>> Get();
        Task<List<LocalHardwareLinkedModule>> GetByHardware(Guid? input);
        Task<List<LocalHardwareLinkedModule>> GetByHardware(long? input);
        Task<List<LocalHardwareLinkedModule>> GetByModule(long? input);
        Task<List<LocalHardwareLinkedModule>> GetByModule(Guid? input);
        //Task<HardwareLinkedModule> Get(Guid input);
        Task<LocalHardwareLinkedModule> Get(Guid? input);
        Task<LocalHardwareLinkedModule> Get(long? input);        
        Task<bool> Exists(Hardware hardware, Module module);
        Task<List<LocalHardwareLinkedModule>> Get(LocalHardware hardware);
    }

    public class HardwareLinkedModuleQueries: IHardwareLinkedModuleQueries
    {
        private readonly DataDbContext _dataDbContext;

        public HardwareLinkedModuleQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }
        public HardwareLinkedModuleQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        #region Get       
        public async Task<LocalHardwareLinkedModule> Get(long? input)
        {
            LocalHardwareLinkedModule output = new LocalHardwareLinkedModule();
            try
            {
                output = await _dataDbContext.HardwareLinkedModule
                    .AsNoTracking()
                    .Include(i => i.Hardware)
                    //.ThenInclude(i => i.HardwareType)
                    //.Include(i => i.Module)
                    .Where(i => i.Id == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        public async Task<LocalHardwareLinkedModule> Get(Guid? input)
        {
            LocalHardwareLinkedModule output = new LocalHardwareLinkedModule();
            try
            {
                output = await _dataDbContext.HardwareLinkedModule
                   .AsNoTracking()
                   .Include(i => i.Hardware)
                   //.ThenInclude(i => i.HardwareType)
                   //.Include(i => i.Module)
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
        public async Task<List<LocalHardwareLinkedModule>> Get()
        {
            List<LocalHardwareLinkedModule> output = new List<LocalHardwareLinkedModule>();
            try
            {
                //output = await _dataDbContext.HardwareLinkedModule.OrderByDescending(i => i.TimeStamp).ToListAsync();
                output = await _dataDbContext.HardwareLinkedModule
                   .AsNoTracking()
                   .Include(i => i.Hardware)
                   //.ThenInclude(i => i.HardwareType)
                   //.Include(i => i.Module)
                   .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<LocalHardwareLinkedModule>> Get(LocalHardware hardware)
        {
            List<LocalHardwareLinkedModule> output = new List<LocalHardwareLinkedModule>();
            try
            {
                //output = await _dataDbContext.HardwareLinkedModule.OrderByDescending(i => i.TimeStamp).ToListAsync();
                output = await _dataDbContext.HardwareLinkedModule
                   .AsNoTracking()
                   .Include(i => i.Hardware)
                   //.ThenInclude(i => i.HardwareType)
                   //.Include(i => i.Module)
                   .Where(i=>i.Hardware.Id==hardware.Id)
                   .OrderByDescending(i => i.TimeStamp).ToListAsync();
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }            
        }
        public async Task<List<LocalHardwareLinkedModule>> GetByHardware(Guid? input)
        {
            List<LocalHardwareLinkedModule> output = new List<LocalHardwareLinkedModule>();
            try
            {
                output = await _dataDbContext.HardwareLinkedModule
                   .AsNoTracking()
                   .Include(i => i.Hardware)
                   //.ThenInclude(i => i.HardwareType)
                   //.Include(i => i.Module)
                   .Where(i => i.Hardware.UUID == input)
                   .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<LocalHardwareLinkedModule>> GetByHardware(long? input)
        {
            List<LocalHardwareLinkedModule> output = new List<LocalHardwareLinkedModule>();
            try
            {
                output = await _dataDbContext.HardwareLinkedModule
                   .AsNoTracking()
                   .Include(i => i.Hardware)
                   //.ThenInclude(i => i.HardwareType)
                   //.Include(i => i.Module)
                   .Where(i => i.Hardware.Id == input)
                   .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<LocalHardwareLinkedModule>> GetByModule(Guid? input)
        {
            List<LocalHardwareLinkedModule> output = new List<LocalHardwareLinkedModule>();
            try
            {
                output = await _dataDbContext.HardwareLinkedModule
                   .AsNoTracking()
                   .Include(i => i.Hardware)
                   //.ThenInclude(i => i.HardwareType)
                   //.Include(i => i.Module)
                   .Where(i => i.ModuleUUID == input)
                   .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<LocalHardwareLinkedModule>> GetByModule(long? input)
        {
            List<LocalHardwareLinkedModule> output = new List<LocalHardwareLinkedModule>();
            try
            {
                output = await _dataDbContext.HardwareLinkedModule
                   .AsNoTracking()
                   .Include(i => i.Hardware)
                   //.ThenInclude(i => i.HardwareType)
                   //.Include(i => i.Module)
                   .Where(i => i.ModuleId == input)
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
        public async Task<LocalHardwareLinkedModule> Create(LocalHardwareLinkedModule input)
        {
            try
            {
                _dataDbContext.HardwareLinkedModule.Update(input);
                await _dataDbContext.SaveChangesAsync();
                return input;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            
        }

        #endregion

        #region Update

        public async Task<LocalHardwareLinkedModule> Update(LocalHardwareLinkedModule input)
        {
            try
            {
                _dataDbContext.HardwareLinkedModule.Update(input);
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
        public async Task<bool> Delete(LocalHardwareLinkedModule input)
        {
            try
            {
                _dataDbContext.HardwareLinkedModule.Remove(input);
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

        public async Task<bool> Exists(Hardware hardware, Module module)
        {
            bool output = false;
            try
            {
                output = await _dataDbContext.HardwareLinkedModule
                    .Include(i => i.Hardware)
                    //.Include(i => i.ModuleId)
                    .Where(i => i.Hardware.Id == hardware.Id &&
                    i.ModuleId == module.Id)
                    .AnyAsync();

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        //#region Get
        ////public async Task<IHardwareLinkedModule> Get(long input)
        ////{
        ////    HardwareLinkedModule output = new HardwareLinkedModule();
        ////    try
        ////    {
        ////        output = await _dataDbContext.HardwareLinkedModule.FindAsync(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        ////public async Task<IHardwareLinkedModule> Get(Guid input)
        ////{
        ////    HardwareLinkedModule output = new HardwareLinkedModule();
        ////    try
        ////    {
        ////        output = await _dataDbContext.HardwareLinkedModule
        ////             .Where(i => i.UUID == input)
        ////             .FirstOrDefaultAsync();
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        ////public async Task<List<IHardwareLinkedModule>> Get()
        ////{
        ////    List<HardwareLinkedModule> preoutput = new List<HardwareLinkedModule>();
        ////    List<IHardwareLinkedModule> output = new List<IHardwareLinkedModule>();
        ////    try
        ////    {
        ////        preoutput = await _dataDbContext.HardwareLinkedModule.OrderByDescending(i => i.TimeStamp).ToListAsync();
        ////        output.AddRange(preoutput);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        ////public async Task<List<IHardwareLinkedModule>> GetByHardware(Guid input)
        ////{
        ////    List<HardwareLinkedModule> preoutput = new List<HardwareLinkedModule>();
        ////    List<IHardwareLinkedModule> output = new List<IHardwareLinkedModule>();
        ////    try
        ////    {
        ////        preoutput = await _dataDbContext.HardwareLinkedModule
        ////            .Where(i => i.HardwareUUID == input)
        ////            .OrderByDescending(i => i.TimeStamp).ToListAsync();
        ////        output.AddRange(preoutput);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        ////public async Task<List<IHardwareLinkedModule>> GetByHardware(long input)
        ////{
        ////    List<HardwareLinkedModule> preoutput = new List<HardwareLinkedModule>();
        ////    List<IHardwareLinkedModule> output = new List<IHardwareLinkedModule>();
        ////    try
        ////    {
        ////        preoutput = await _dataDbContext.HardwareLinkedModule
        ////            .Include(h => h.Hardware)
        ////            .Where(i => i.Hardware.Id == input)
        ////            .OrderByDescending(i => i.TimeStamp).ToListAsync();
        ////        output.AddRange(preoutput);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        ////public async Task<List<IHardwareLinkedModule>> GetByModule(Guid input)
        ////{
        ////    List<HardwareLinkedModule> preoutput = new List<HardwareLinkedModule>();
        ////    List<IHardwareLinkedModule> output = new List<IHardwareLinkedModule>();
        ////    try
        ////    {
        ////        preoutput = await _dataDbContext.HardwareLinkedModule
        ////            .Where(i => i.ModuleUUID == input)
        ////            .OrderByDescending(i => i.TimeStamp).ToListAsync();
        ////        output.AddRange(preoutput);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        ////public async Task<List<IHardwareLinkedModule>> GetByModule(long input)
        ////{
        ////    List<HardwareLinkedModule> preoutput = new List<HardwareLinkedModule>();
        ////    List<IHardwareLinkedModule> output = new List<IHardwareLinkedModule>();
        ////    try
        ////    {
        ////        preoutput = await _dataDbContext.HardwareLinkedModule
        ////            .Include(h => h.Module)
        ////            .Where(i => i.Module.Id == input)
        ////            .OrderByDescending(i => i.TimeStamp).ToListAsync();
        ////        output.AddRange(preoutput);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}

        //#region Not interfaced
        //public async Task<HardwareLinkedModule> Get(long input)
        //{
        //    HardwareLinkedModule output = new HardwareLinkedModule();
        //    try
        //    {
        //        output = await _dataDbContext.HardwareLinkedModule.FindAsync(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<HardwareLinkedModule> Get(Guid input)
        //{
        //    HardwareLinkedModule output = new HardwareLinkedModule();
        //    try
        //    {
        //        output = await _dataDbContext.HardwareLinkedModule.Where(i => i.HardwareUUID == input).FirstOrDefaultAsync();
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<List<HardwareLinkedModule>> Get()
        //{
        //    List<HardwareLinkedModule> output = new List<HardwareLinkedModule>();
        //    try
        //    {
        //        output = await _dataDbContext.HardwareLinkedModule.OrderByDescending(i => i.TimeStamp).ToListAsync();
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<List<HardwareLinkedModule>> GetByHardware(Guid input)
        //{
        //    List<HardwareLinkedModule> output = new List<HardwareLinkedModule>();
        //    try
        //    {
        //        output = await _dataDbContext.HardwareLinkedModule
        //            .Where(i => i.HardwareUUID == input)
        //            .OrderByDescending(i => i.TimeStamp).ToListAsync();
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}

       
        //public async Task<List<HardwareLinkedModule>> GetByHardware(long input)
        //{
        //    List<HardwareLinkedModule> output = new List<HardwareLinkedModule>();
        //    try
        //    {
        //        output = await _dataDbContext.HardwareLinkedModule
        //            .Include(h => h.Hardware)
        //            .Include(h => h.Module)
        //            .Where(i => i.Hardware.Id == input)
        //            .OrderByDescending(i => i.TimeStamp).ToListAsync();
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<List<HardwareLinkedModule>> GetByModule(Guid input)
        //{
        //    List<HardwareLinkedModule> output = new List<HardwareLinkedModule>();
        //    try
        //    {
        //        output = await _dataDbContext.HardwareLinkedModule
        //            .Where(i => i.ModuleUUID == input)
        //            .OrderByDescending(i => i.TimeStamp).ToListAsync();
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<List<HardwareLinkedModule>> GetByModule(long input)
        //{
        //    List<HardwareLinkedModule> output = new List<HardwareLinkedModule>();
        //    try
        //    {
        //        output = await _dataDbContext.HardwareLinkedModule
        //            .Include(h => h.Module)
        //            .Where(i => i.Module.Id == input)
        //            .OrderByDescending(i => i.TimeStamp).ToListAsync();
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //#endregion 
        //#endregion 

        //#region Create
        ////public async Task<IHardwareLinkedModule> Create(IHardwareLinkedModule input)
        ////{
        ////    try
        ////    {
        ////        await _dataDbContext.HardwareLinkedModule.AddAsync((HardwareLinkedModule)input);
        ////        await _dataDbContext.SaveChangesAsync();
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return input;
        ////}
        //public async Task<HardwareLinkedModule> Create(HardwareLinkedModule input)
        //{
        //    try
        //    {
        //        await _dataDbContext.HardwareLinkedModule.AddAsync((HardwareLinkedModule)input);
        //        await _dataDbContext.SaveChangesAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return input;
        //}

        //#endregion 

        //#region Update
        ////public async Task<IHardwareLinkedModule> Update(IHardwareLinkedModule input)
        ////{
        ////    try
        ////    {
        ////        _dataDbContext.HardwareLinkedModule.Update((HardwareLinkedModule)input);
        ////        await _dataDbContext.SaveChangesAsync();
        ////    }
        ////    catch
        ////    {}
        ////    return input;
        ////}
        //public async Task<HardwareLinkedModule> Update(HardwareLinkedModule input)
        //{
        //    try
        //    {
        //        _dataDbContext.HardwareLinkedModule.Update((HardwareLinkedModule)input);
        //        await _dataDbContext.SaveChangesAsync();
        //    }
        //    catch
        //    { }
        //    return input;
        //}
        //#endregion 

        //#region Delete
        ////public async Task<bool> Delete(IHardwareLinkedModule input)
        ////{
        ////    try
        ////    {
        ////        _dataDbContext.HardwareLinkedModule.Remove((HardwareLinkedModule)input);
        ////        await _dataDbContext.SaveChangesAsync();
        ////        return true;
        ////    }
        ////    catch
        ////    {
        ////        return false;
        ////    }
        ////}
        //public async Task<bool> Delete(HardwareLinkedModule input)
        //{
        //    try
        //    {
        //        _dataDbContext.HardwareLinkedModule.Remove((HardwareLinkedModule)input);
        //        await _dataDbContext.SaveChangesAsync();
        //        return true;
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        //public Task<HardwareLinkedModule> Get(Guid? input)
        //{
        //    throw new NotImplementedException();
        //}

        //public Task<HardwareLinkedModule> Get(long? input)
        //{
        //    throw new NotImplementedException();
        //}
        //#endregion
    }

    
}
