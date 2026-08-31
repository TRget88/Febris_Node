// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    public interface ICurriculumLogic
    {
        Task<List<Curriculum>> Get();

        /// <summary>
        /// Live curricula, or every curriculum including obsoleted ones. Without the second mode
        /// an obsoleted curriculum is invisible to the UI and therefore impossible to restore,
        /// which makes soft delete behave like a hard delete from the operator's side.
        /// </summary>
        Task<List<Curriculum>> Get(bool includeObsolete);

        Task<Curriculum> Get(long? id);

        /// <summary>Classification lookup for the authoring form.</summary>
        Task<List<CurriculumClassification>> GetClassifications();

        /// <summary>Create or update a node-authored curriculum. Returns the persisted row.</summary>
        Task<Curriculum> Save(Curriculum input);

        /// <summary>Soft-delete. Link rows reference curricula, so this never hard-deletes.</summary>
        Task<bool> SetObsolete(long id, bool obsolete);

        /// <summary>The modules currently in this curriculum, Module graph included.</summary>
        Task<List<ModuleLinkedCurriculum>> GetLinkedModules(long curriculumId);

        /// <summary>
        /// Add or remove one module from a curriculum, returning the resulting membership state
        /// (true = now linked). Idempotent in both directions.
        /// </summary>
        Task<bool> ToggleModuleLink(Guid curriculumUuid, Guid moduleUuid);
    }
    public class CurriculumLogic: ICurriculumLogic
    {
        private readonly ICurriculumQueries _dataContext;
        private readonly IModuleLinkedCurriculumQueries _linkContext;
        private readonly IModuleQueries _moduleContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;

        // DI refactor
        public CurriculumLogic(
            IHttpContextAccessor httpContextAccessor,
            ICurriculumQueries dataContext,
            IModuleLinkedCurriculumQueries linkContext,
            IModuleQueries moduleContext)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _dataContext = dataContext;
            _linkContext = linkContext;
            _moduleContext = moduleContext;
        }

        public CurriculumLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;
            _dataContext = new CurriculumQueries();
            _linkContext = new ModuleLinkedCurriculumQueries();
            _moduleContext = new ModuleQueries();
        }

        #region Get              

        public async Task<List<Curriculum>> Get()
        {
            //bool output = true;
            List<Curriculum> output = new List<Curriculum>();
            try
            {
                output = await _dataContext.Get();
                //output.AddRange(preoutput);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        //public async Task<Curriculum> Get(Guid? input)
        //{
        //    //bool output = true;
        //    Curriculum output = new Curriculum();
        //    try
        //    {
        //        //use input to find subscription
        //        output = await _dataContext.Get(input);
        //        //output = subscription;
        //    }
        //    catch { }
        //    return output;
        //}
        public async Task<Curriculum> Get(long? input)
        {
            //bool output = true;
            Curriculum output = new Curriculum();
            try
            {
                //use input to find subscription
                output = await _dataContext.Get(input);
                //output = subscription;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<Curriculum>> Get(bool includeObsolete)
        {
            List<Curriculum> output = new List<Curriculum>();
            try
            {
                output = includeObsolete
                    ? await _dataContext.GetIncludingObsolete()
                    : await _dataContext.Get();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<CurriculumClassification>> GetClassifications()
        {
            List<CurriculumClassification> output = new List<CurriculumClassification>();
            try
            {
                output = await _dataContext.GetClassifications();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<ModuleLinkedCurriculum>> GetLinkedModules(long curriculumId)
        {
            List<ModuleLinkedCurriculum> output = new List<ModuleLinkedCurriculum>();
            try
            {
                Curriculum curriculum = await _dataContext.Get(curriculumId);
                if (curriculum == null)
                {
                    return output;
                }
                output = await _linkContext.Get(curriculum);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        #endregion

        #region Write

        public async Task<Curriculum> Save(Curriculum input)
        {
            try
            {
                if (input == null || string.IsNullOrWhiteSpace(input.Name))
                {
                    // A nameless curriculum is unusable in every list and picker that renders it.
                    return null;
                }

                return await _dataContext.Upsert(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<bool> SetObsolete(long id, bool obsolete)
        {
            try
            {
                return await _dataContext.SetObsolete(id, obsolete);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<bool> ToggleModuleLink(Guid curriculumUuid, Guid moduleUuid)
        {
            try
            {
                Curriculum curriculum = await _dataContext.Get(curriculumUuid);
                Module module = await _moduleContext.Get(moduleUuid);
                if (curriculum == null || module == null)
                {
                    // Resolve BOTH endpoints before writing: a link row whose module or curriculum
                    // does not exist renders as a blank line in the curriculum's module list.
                    return false;
                }

                List<ModuleLinkedCurriculum> existing = await _linkContext.Get(curriculum);
                bool alreadyLinked = existing.Any(i => i.ModuleUUID == moduleUuid);

                if (alreadyLinked)
                {
                    await _linkContext.Remove(moduleUuid, curriculumUuid);
                    return false;
                }

                await _linkContext.Upsert(new ModuleLinkedCurriculum()
                {
                    UUID = Guid.NewGuid(),
                    Curriculum = curriculum,
                    CurriculumUUID = curriculum.UUID,
                    Module = module,
                    ModuleUUID = module.UUID
                });
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
