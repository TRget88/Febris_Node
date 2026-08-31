// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    public interface IHardwareLinkedCohortLogic
    {
        Task<List<HardwareLinkedCohort>> GetByCohort(long? id);
        Task<List<HardwareLinkedCohort>> GetByHardware(long? id);
        Task<HardwareLinkedCohort> Create(long hardwareId, long cohortId);
        Task<bool> Remove(HardwareLinkedCohort link);
        Task<List<HardwareLinkedCohort>> Get(LocalHardware input);
    }


    public class HardwareLinkedCohortLogic: IHardwareLinkedCohortLogic
    {

        private readonly IHardwareLinkedCohortQueries _context;
        private readonly IHardwareQueries _hardwareContext;
        private readonly ICohortQueries _cohortContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        // DI refactor
        public HardwareLinkedCohortLogic(IHttpContextAccessor httpContextAccessor, IHardwareLinkedCohortQueries context, ICohortQueries cohortContext, IHardwareQueries hardwareContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _cohortContext = cohortContext;
            _hardwareContext = hardwareContext;
            User = _httpContextAccessor?.HttpContext?.User;
        }

        public HardwareLinkedCohortLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new HardwareLinkedCohortQueries();
            _cohortContext = new CohortQueries();
            _hardwareContext = new HardwareQueries();
            User = _httpContextAccessor.HttpContext.User;
        }

        [TempData]
        private string StatusMessage { get; set; }


        #region Get
        public async Task<List<HardwareLinkedCohort>> Get()
        {
            List<HardwareLinkedCohort> output = new List<HardwareLinkedCohort>();
            try
            {
                output = await _context.Get();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<HardwareLinkedCohort> Get(long? input)
        {
            HardwareLinkedCohort output = new HardwareLinkedCohort();
            try
            {
                output = await _context.Get(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<HardwareLinkedCohort> Get(Guid input)
        {
            HardwareLinkedCohort output = new HardwareLinkedCohort();
            try
            {
                output = await _context.Get(input);
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
            List<HardwareLinkedCohort> output = new List<HardwareLinkedCohort>();
            try
            {
                LocalHardware hardware = new LocalHardware();
                if (input.Id == 0)
                {
                    hardware = await _hardwareContext.Get(input.UUID);
                }
                else
                {
                    hardware = await _hardwareContext.Get(input.Id);
                }


                output = await _context.Get(hardware);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<HardwareLinkedCohort>> GetByHardware(Guid input)
        {
            List<HardwareLinkedCohort> output = new List<HardwareLinkedCohort>();
            try
            {
                output = await _context.GetByHardware(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<HardwareLinkedCohort>> GetByHardware(long? input)
        {
            List<HardwareLinkedCohort> output = new List<HardwareLinkedCohort>();
            try
            {
                output = await _context.GetByHardware(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<HardwareLinkedCohort>> GetByCohort(Guid input)
        {
            List<HardwareLinkedCohort> output = new List<HardwareLinkedCohort>();
            try
            {
                output = await _context.GetByCohort(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<HardwareLinkedCohort>> GetByCohort(long? input)
        {
            List<HardwareLinkedCohort> output = new List<HardwareLinkedCohort>();
            try
            {
                output = await _context.GetByCohort(input);
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
        public async Task<HardwareLinkedCohort> Create(HardwareLinkedCohort input)
        {
            try
            {
                input = await _context.Create(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return input;
        }

        public async Task<HardwareLinkedCohort> Create(long hardwareId, long cohortId)
        {
            HardwareLinkedCohort output = new HardwareLinkedCohort();
            try
            {
                LocalHardware hardware = await _hardwareContext.Get(hardwareId);
                Cohort cohort = await _cohortContext.Get(cohortId);

                HardwareLinkedCohort input = new HardwareLinkedCohort()
                {
                    Hardware = hardware,
                    HardwareUUID = hardware.UUID,
                    Cohort = cohort,
                    CohortUUID = cohort.UUID
                };

                output = await _context.Create(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        #endregion

        #region Update

        public async Task<HardwareLinkedCohort> Update(HardwareLinkedCohort input)
        {
            try
            {
                input = await _context.Update(input);
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
            bool output = false;
            try
            {
                output = await _context.Delete(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        public async Task<bool> Remove(HardwareLinkedCohort input)
        {
            bool output = false;
            try
            {
                output = await _context.Delete(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }




        #endregion
        

    }

}
