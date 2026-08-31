// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    public interface IModuleLinkedCurriculumQueries
    {
        /// <summary>The modules belonging to a curriculum, Module graph included. Never null.</summary>
        Task<List<ModuleLinkedCurriculum>> Get(Curriculum curriculum);

        /// <summary>The curricula a module belongs to, Curriculum included. Never null.</summary>
        Task<List<ModuleLinkedCurriculum>> GetByModule(Guid? moduleUuid);

        /// <summary>
        /// Create-or-update by the (ModuleUUID, CurriculumUUID) pair, so re-authoring or
        /// re-ingesting the same pairing re-points the existing row instead of duplicating it.
        /// Returns the persisted row.
        /// </summary>
        Task<ModuleLinkedCurriculum> Upsert(ModuleLinkedCurriculum input);

        /// <summary>Remove one module-to-curriculum pairing. No-op when the pairing is absent.</summary>
        Task Remove(Guid moduleUuid, Guid curriculumUuid);
    }

    /// <summary>
    /// Node-local Module-to-Curriculum link store (delivery-path severance, owner ruling
    /// 2026-08-01: modules belong to curricula).
    ///
    /// <para>
    /// This class was previously a Remote-HTTP client resolving the join from the hub's
    /// FebrisSharedAPI, gated closed, so on a self-hosted node every curriculum reported zero
    /// modules. The link rows now live in the node's own DataDbContext beside the local Module
    /// catalog and the long-standing Curriculum / CohortLinkedCurriculum tables, completing the
    /// chain Cohort -&gt; CohortLinkedCurriculum -&gt; Curriculum -&gt; ModuleLinkedCurriculum -&gt; Module
    /// entirely locally. Local-EF bodies mirror the proven ModuleLinkedObjectQueries twin; the
    /// pre-existing <c>Get(Curriculum)</c> shape is unchanged so no BLL call site moves.
    /// </para>
    /// </summary>
    public class ModuleLinkedCurriculumQueries : IModuleLinkedCurriculumQueries
    {
        private readonly DataDbContext _dataDbContext;

        // DI refactor
        public ModuleLinkedCurriculumQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }

        public ModuleLinkedCurriculumQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<List<ModuleLinkedCurriculum>> Get(Curriculum curriculum)
        {
            List<ModuleLinkedCurriculum> output = new List<ModuleLinkedCurriculum>();
            try
            {
                if (curriculum == null)
                {
                    return output;
                }

                output = await _dataDbContext.ModuleLinkedCurriculum
                    .AsNoTracking()
                    .Include(i => i.Module).ThenInclude(i => i.ModuleClassification)
                    .Include(i => i.Curriculum)
                    .Where(i => i.Curriculum.Id == curriculum.Id)
                    .OrderBy(i => i.Module.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<ModuleLinkedCurriculum>> GetByModule(Guid? moduleUuid)
        {
            List<ModuleLinkedCurriculum> output = new List<ModuleLinkedCurriculum>();
            try
            {
                if (moduleUuid == null)
                {
                    return output;
                }

                output = await _dataDbContext.ModuleLinkedCurriculum
                    .AsNoTracking()
                    .Include(i => i.Curriculum)
                    .Include(i => i.Module)
                    .Where(i => i.Module.UUID == moduleUuid)
                    .OrderBy(i => i.Curriculum.Name)
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

        #region Write
        public async Task<ModuleLinkedCurriculum> Upsert(ModuleLinkedCurriculum input)
        {
            try
            {
                if (input == null)
                {
                    return null;
                }

                // Match on the PAIR, not on either side alone: a module legitimately belongs to
                // several curricula and a curriculum holds many modules, so keying on one of them
                // would silently repoint an unrelated pairing.
                ModuleLinkedCurriculum existing = await _dataDbContext.ModuleLinkedCurriculum
                    .Where(i => i.ModuleUUID == input.ModuleUUID
                             && i.CurriculumUUID == input.CurriculumUUID)
                    .FirstOrDefaultAsync();

                if (existing == null)
                {
                    // Both endpoints normally arrive as DETACHED entities -- callers read them
                    // with AsNoTracking. Adding a link row that carries detached navigations makes
                    // EF treat those endpoints as NEW and re-INSERT them, which fails on
                    // "duplicate key value violates unique constraint PK_Curriculum". Attaching
                    // marks them Unchanged so only the link row is written.
                    AttachExisting(input.Curriculum);
                    AttachExisting(input.Module);

                    _dataDbContext.ModuleLinkedCurriculum.Add(input);
                    await _dataDbContext.SaveChangesAsync();
                    return input;
                }

                // The pair IS the row: both endpoints are already what the caller asked for, and
                // ModuleId / CurriculumId are EF shadow FK properties, not CLR members. So there is
                // nothing to repoint -- just mark it touched so re-ingest is visible.
                existing.LastUpdateTimeStamp = DateTime.Now;
                await _dataDbContext.SaveChangesAsync();
                return existing;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// Mark an already-persisted endpoint as Unchanged so EF writes only the link row.
        /// Id == 0 means the caller really did hand us a new entity, so leave it alone rather than
        /// masking that as an update.
        /// </summary>
        private void AttachExisting(object entity)
        {
            if (entity == null)
            {
                return;
            }

            if (entity is Febris.ModelLibrary.Models.BaseModel model && model.Id == 0)
            {
                return;
            }

            if (_dataDbContext.Entry(entity).State == EntityState.Detached)
            {
                _dataDbContext.Attach(entity);
            }
        }

        public async Task Remove(Guid moduleUuid, Guid curriculumUuid)
        {
            try
            {
                ModuleLinkedCurriculum existing = await _dataDbContext.ModuleLinkedCurriculum
                    .Where(i => i.ModuleUUID == moduleUuid && i.CurriculumUUID == curriculumUuid)
                    .FirstOrDefaultAsync();

                if (existing == null)
                {
                    return;
                }

                _dataDbContext.ModuleLinkedCurriculum.Remove(existing);
                await _dataDbContext.SaveChangesAsync();
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
