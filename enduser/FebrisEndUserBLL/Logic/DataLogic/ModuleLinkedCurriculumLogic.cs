// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    public interface IModuleLinkedCurriculumLogic
    {
        //Task<List<ModuleLinkedCurriculum>> Get();
        Task<List<ModuleLinkedCurriculum>> Get(Curriculum curriculum);
    }

    public class ModuleLinkedCurriculumLogic : IModuleLinkedCurriculumLogic
    {
        private readonly IModuleLinkedCurriculumQueries _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;

        // DI refactor
        public ModuleLinkedCurriculumLogic(IHttpContextAccessor httpContextAccessor, IModuleLinkedCurriculumQueries context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            User = _httpContextAccessor?.HttpContext?.User;
        }

        public ModuleLinkedCurriculumLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new ModuleLinkedCurriculumQueries();
            User = _httpContextAccessor.HttpContext.User;
        }

        public async Task<List<ModuleLinkedCurriculum>> Get(Curriculum curriculum)
        {
            List<ModuleLinkedCurriculum> output = new List<ModuleLinkedCurriculum>();
            try
            {
                output = await _context.Get(curriculum);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        
        #region Get
        //public async Task<List<ModuleLinkedCurriculum>> Get()
        //{
        //    List<ModuleLinkedCurriculum> output = new List<ModuleLinkedCurriculum>();
        //    try
        //    {
        //        output = await _context.Get();
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        //public async Task<ModuleLinkedCurriculum> Get(long? input)
        //{
        //    ModuleLinkedCurriculum output = new ModuleLinkedCurriculum();
        //    try
        //    {
        //        output = await _context.Get(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        //public async Task<ModuleLinkedCurriculum> Get(Guid input)
        //{
        //    ModuleLinkedCurriculum output = new ModuleLinkedCurriculum();
        //    try
        //    {
        //        output = await _context.Get(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        //public async Task<List<ModuleLinkedCurriculum>> GetByHardware(Guid input)
        //{
        //    List<ModuleLinkedCurriculum> output = new List<ModuleLinkedCurriculum>();
        //    try
        //    {
        //        output = await _context.GetByHardware(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        //public async Task<List<ModuleLinkedCurriculum>> GetByHardware(long? input)
        //{
        //    List<ModuleLinkedCurriculum> output = new List<ModuleLinkedCurriculum>();
        //    try
        //    {
        //        output = await _context.GetByHardware(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        //public async Task<List<ModuleLinkedCurriculum>> GetByCohort(Guid input)
        //{
        //    List<ModuleLinkedCurriculum> output = new List<ModuleLinkedCurriculum>();
        //    try
        //    {
        //        output = await _context.GetByCohort(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        //public async Task<List<ModuleLinkedCurriculum>> GetByCohort(long? input)
        //{
        //    List<ModuleLinkedCurriculum> output = new List<ModuleLinkedCurriculum>();
        //    try
        //    {
        //        output = await _context.GetByCohort(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}

        #endregion

        #region Create
        //public async Task<ModuleLinkedCurriculum> Create(ModuleLinkedCurriculum input)
        //{
        //    try
        //    {
        //        input = await _context.Create(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return input;
        //}

        //public async Task<ModuleLinkedCurriculum> Create(long hardwareId, long cohortId)
        //{
        //    ModuleLinkedCurriculum output = new ModuleLinkedCurriculum();
        //    try
        //    {
        //        LocalHardware hardware = await _hardwareContext.Get(hardwareId);
        //        Cohort cohort = await _cohortContext.Get(cohortId);

        //        ModuleLinkedCurriculum input = new ModuleLinkedCurriculum()
        //        {
        //            Hardware = hardware,
        //            HardwareUUID = hardware.UUID,
        //            Cohort = cohort,
        //            CohortUUID = cohort.UUID
        //        };

        //        output = await _context.Create(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}

        #endregion

        #region Update

        //public async Task<ModuleLinkedCurriculum> Update(ModuleLinkedCurriculum input)
        //{
        //    try
        //    {
        //        input = await _context.Update(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }

        //    return input;
        //}
        #endregion

        #region Delete

        //public async Task<bool> Delete(ModuleLinkedCurriculum input)
        //{
        //    bool output = false;
        //    try
        //    {
        //        output = await _context.Delete(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }

        //    return output;
        //}

        //public async Task<bool> Remove(ModuleLinkedCurriculum input)
        //{
        //    bool output = false;
        //    try
        //    {
        //        output = await _context.Delete(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }

        //    return output;
        //}




        #endregion

    }

}
