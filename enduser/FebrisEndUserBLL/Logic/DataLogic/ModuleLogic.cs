// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Interfaces.DataModelInterfaces;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.UserModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    public interface IModuleLogic
    {
        Task<List<Module>> Get();
        Task<Module> Get(long? id);
        Task<List<Module>> GetAccessableModules(LocalHardware hardware);
        Task<Module> Get(Guid? input);

        /// <summary>
        /// Update a module's CATALOG METADATA in place. Deliberately metadata-only: the package
        /// bytes are owned by the ingest path (PackageIngestLogic -> IStorageProvider), so editing
        /// a name here must never imply the stored .zip changed. Returns the persisted row, or
        /// null when the module is unnamed or missing.
        /// </summary>
        Task<Module> Save(Module input);
    }

    public class ModuleLogic : IModuleLogic
    {
        //private IModuleQueries _moduleBaseQueries = new SharedDataAccessLayer.Queries.DataQueries.ModuleQueries();
        private readonly IModuleQueries _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        private readonly IHardwareLinkedCohortQueries _hardwareLinkedCohortContext;
        //private readonly UserManager<LocalApplicationUser> _userManagerContext;
        private readonly ICohortMemberQueries _cohortMemberContext;

        // DI refactor
        public ModuleLogic(
            IHttpContextAccessor httpContextAccessor,
            //UserManager<LocalApplicationUser> userManagerContext
            IModuleQueries context,
            IHardwareLinkedCohortQueries hardwareLinkedCohortContext,
            ICohortMemberQueries cohortMemberContext
            )
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _context = context;
            _hardwareLinkedCohortContext = hardwareLinkedCohortContext;
            //_userManagerContext = userManagerContext;
            _cohortMemberContext = cohortMemberContext;
        }

        public ModuleLogic(
            IHttpContextAccessor httpContextAccessor//, 
                                                    //UserManager<LocalApplicationUser> userManagerContext
            )
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;
            _context = new ModuleQueries();
            _hardwareLinkedCohortContext = new HardwareLinkedCohortQueries();
            //_userManagerContext = userManagerContext;
            _cohortMemberContext = new CohortMemberQueries();
        }

        public async Task<List<Module>> Get()
        {
            List<Module> output = default;
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

        public async Task<Module> Get(long? id)
        {
            Module output = default;
            try
            {
                output = await _context.Get(id);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<Module> Get(Guid? input)
        {
            Module output = default;
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

        /// <summary>
        /// If we use the License it will make the module list easier to find 
        /// If we use users it will be more accurate
        /// </summary>
        /// <param name="hardware"></param>
        /// <returns></returns>
        /// <remarks>
        /// The <paramref name="hardware"/> argument is currently UNREAD. Its only use is the
        /// commented cohort lookup at the top of the body. Retyped from the central Hardware
        /// aggregate to <see cref="LocalHardware"/> so the node stops constructing a type it does
        /// not own purely to pass an id, but the parameter is left in place because the commented
        /// block shows what it was for. Delete it deliberately, not as a side effect of this.
        /// </remarks>
        public async Task<List<Module>> GetAccessableModules(LocalHardware hardware)
        {
            List<Module> output = new List<Module>();
            try
            {
                #region using users
                ///More narrowly focused modules that are only ones focused on users linked to the Cohorts

                ////get cohorts linked to hardware
                //List<HardwareLinkedCohort> cohortLinkList = await _hardwareLinkedCohortContext.GetByHardware(hardware.Id);
                //List<Cohort> cohortList = cohortLinkList.Select(i => i.Cohort).ToList();

                ////use cohorts to find users
                //List<CohortMember> memberList = new List<CohortMember>();
                //foreach (var i in cohortList)
                //{
                //    List<CohortMember> tempList = new List<CohortMember>();
                //    tempList = await _cohortMemberContext.GetByCohort(i.Id);
                //    memberList.AddRange(tempList);
                //}

                ////Filter out user Id list
                //List<Guid> userIdList = default;
                //userIdList = memberList.Select(i => i.UserId).Distinct().ToList();

                ////send user Ids to server to see purchases (Purchases>marketplacelisting>curriculum>Module)
                //output = await _context.GetByUser(userIdList);
                #endregion

                #region using License
                ///This will get all accessable moduels
                List<Module> preoutput = await _context.GetByLicense();
                #endregion

                #region Distinct
                //.Distinct().ToList();
                output = preoutput.GroupBy(i => i.Id).Select(group => group.First()).ToList();


                #endregion
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                //throw;
            }
            return output;
        }


        //#region Get
        //#region Interfaced
        ////public async Task<IModule> Get(long? input)
        ////{
        ////    IModule output = new Module();
        ////    try
        ////    {
        ////        output = await _moduleBaseQueries.Get(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        ////public async Task<IModule> Get(Guid input)
        ////{
        ////    IModule output = new Module();
        ////    try
        ////    {
        ////        output = await _moduleBaseQueries.Get(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        ////public async Task<List<IModule>> Get()
        ////{
        ////    List<IModule> output = new List<IModule>();
        ////    try
        ////    {
        ////        //List<ModuleBase> preoutput = await _moduleBaseQueries.Get();
        ////        //output.AddRange(preoutput);
        ////        output = await _moduleBaseQueries.Get();

        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        //#endregion
        //public async Task<Module> Get(long input)
        //{
        //    Module output = new Module();
        //    try
        //    {
        //        output = await _context.Get(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<Module> Get(Guid input)
        //{
        //    Module output = new Module();
        //    try
        //    {
        //        output = await _context.Get(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<List<Module>> Get()
        //{
        //    List<Module> output = new List<Module>();
        //    try
        //    {
        //        //List<ModuleBase> preoutput = await _moduleBaseQueries.Get();
        //        //output.AddRange(preoutput);
        //        output = await _context.Get();

        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //#endregion

        //#region Post
        //public async Task<Module> Create(Module input)
        //{
        //    Module output = new Module();
        //    try
        //    {
        //        output = await _context.Create(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        ////public async Task<IModule> Create(IModule input)
        ////{
        ////    IModule output = new Module();
        ////    try
        ////    {
        ////        output = await _moduleBaseQueries.Create(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        //#endregion

        //#region Update
        //public async Task<Module> Update(Module input)
        //{
        //    Module output = new Module();
        //    try
        //    {
        //        output = await _context.Update(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        ////public async Task<IModule> Update(IModule input)
        ////{
        ////    IModule output = new Module();
        ////    try
        ////    {
        ////        output = await _moduleBaseQueries.Update(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        //#endregion

        //#region Delete
        //public async Task<bool> Delete(Module input)
        //{
        //    bool output = false;
        //    try
        //    {
        //        output = await _context.Delete(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        ////public async Task<bool> Delete(IModule input)
        ////{
        ////    bool output = false;
        ////    try
        ////    {
        ////        output = await _moduleBaseQueries.Delete(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}
        //#endregion

        #region Write

        public async Task<Module> Save(Module input)
        {
            try
            {
                if (input == null || string.IsNullOrWhiteSpace(input.Name))
                {
                    // An unnamed module is unusable in the catalog, the linking screen and every
                    // picker that renders it.
                    return null;
                }

                Module existing = await _context.Get(input.UUID);
                if (existing == null)
                {
                    // Metadata-only by design: a module row without stored package bytes would
                    // appear in the catalog and then fail on download. Creating a module is the
                    // ingest path's job, because that is what writes the .zip to storage.
                    return null;
                }

                existing.Name = input.Name;
                existing.Version = input.Version;
                existing.Description = input.Description;
                existing.Language = input.Language;
                existing.XApiInteractionType = input.XApiInteractionType;
                existing.MainSectionCount = input.MainSectionCount;
                existing.TotalSectionCount = input.TotalSectionCount;
                existing.InteractionComponents = input.InteractionComponents;
                existing.EstimatedCompletionTime = input.EstimatedCompletionTime;
                existing.Obsolete = input.Obsolete;
                existing.LastUpdateTimeStamp = DateTime.Now;

                return await _context.Upsert(existing);
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
