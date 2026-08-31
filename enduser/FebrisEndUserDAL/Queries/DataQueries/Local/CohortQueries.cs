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
    public interface ICohortQueries
    {
        Task<List<Cohort>> Get();
        //Task<Cohort> Get(Guid input);
        Task<Cohort> Get(long? input);
        Task<Cohort> Create(Cohort input);
        Task<Cohort> Update(Cohort input);
        Task<Cohort> Get(Guid? input);
        Task<List<Cohort>> GetIncludingArchived();

        /// <summary>
        /// HARD delete of a cohort and ITS LINK ROWS ONLY. See the implementation for what is and
        /// is not removed. Declared here deliberately: the previous Delete existed on the concrete
        /// class but not on this interface, and every consumer holds the interface, so it had zero
        /// reachable callers.
        /// </summary>
        Task<bool> Delete(long id);
    }

    public class CohortQueries: ICohortQueries
    {
        private readonly DataDbContext _dataDbContext;

        public CohortQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }
        public CohortQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        #region Normal queries

        #region Get
        public async Task<Cohort> Get(long? input)
        {
            Cohort output = new Cohort();
            try
            {
                output = await _dataDbContext.Cohort.FindAsync(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        public async Task<Cohort> Get(Guid? input)
        {
            Cohort output = new Cohort();
            try
            {
                output = await _dataDbContext.Cohort.AsNoTracking()
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
        /// <summary>
        /// Active cohorts only. Archived ones are excluded so a finished term stops cluttering the
        /// lists and pickers -- the effect ROADMAP 19 records the Archive flag as never having had.
        /// Mirrors the CurriculumQueries.Get / GetIncludingObsolete pair exactly.
        /// </summary>
        public async Task<List<Cohort>> Get()
        {
            List<Cohort> output = new List<Cohort>();
            try
            {
                output = await _dataDbContext.Cohort
                    .AsNoTracking()
                    .Where(i => !i.Archive)
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

        /// <summary>
        /// Every cohort, archived included. Two kinds of caller need this: the index when the
        /// operator has asked to see archived cohorts (without it, archiving is a ONE-WAY DOOR --
        /// the un-archive toggle lives on the row that just disappeared, which is the trap
        /// ROADMAP 11 records for curricula), and any path RESOLVING an already-chosen cohort,
        /// where filtering would silently drop something the admin explicitly selected.
        /// </summary>
        public async Task<List<Cohort>> GetIncludingArchived()
        {
            List<Cohort> output = new List<Cohort>();
            try
            {
                output = await _dataDbContext.Cohort
                    .AsNoTracking()
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
        //public async Task<Cohort> GetByContentDeveloper(long input)
        //{
        //    Cohort output = new Cohort();
        //    try
        //    {
        //        output = await _dataDbContext.Cohort
        //           .Include(i => i.ContentDeveloper)
        //           .Where(i => i.ContentDeveloper.Id == input)
        //           .FirstOrDefaultAsync();
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<Cohort> GetByContentDeveloper(Guid input)
        //{
        //    Cohort output = new Cohort();
        //    try
        //    {
        //        output = await _dataDbContext.Cohort
        //            .Include(i => i.ContentDeveloper)
        //            .Where(i => i.ContentDeveloperUUID == input)
        //            .FirstOrDefaultAsync();
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<Cohort> GetByContentDeveloper(ContentDeveloper input)
        //{
        //    Cohort output = new Cohort();
        //    try
        //    {
        //        output = await _dataDbContext.Cohort
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

        public async Task<Cohort> Create(Cohort input)
        {
            try
            {
                await _dataDbContext.Cohort.AddAsync((Cohort)input);
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

        #region Update

        public async Task<Cohort> Update(Cohort input)
        {
            try
            {
                _dataDbContext.Cohort.Update((Cohort)input);
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

        /// <summary>
        /// HARD delete of a cohort and ITS LINK ROWS ONLY.
        ///
        /// <para>
        /// WHAT GOES: the Cohort row, plus the junction rows that point at it -- CohortMember,
        /// CohortLinkedCurriculum, CohortLinkedLocation and HardwareLinkedCohort.
        /// </para>
        /// <para>
        /// WHAT SURVIVES: every entity on the other side of those links. Users, Curricula,
        /// Locations and Hardware are never touched. A junction row is not an entity, and a link to
        /// a deleted cohort is meaningless, which is why it goes with it.
        /// </para>
        ///
        /// <para>
        /// The three Cohort* links are ON DELETE RESTRICT in the database, so Postgres REFUSES to
        /// delete the cohort while any of them exist. They are not optional cleanup: without them
        /// the delete raises 23503 and rolls back. HardwareLinkedCohort is ON DELETE CASCADE (set in
        /// 20220726211838_updates) and would go on its own, but it is removed explicitly so the
        /// behaviour is identical on any provider and is visible here rather than only in a 2022
        /// migration.
        /// </para>
        ///
        /// <para>
        /// ONE SaveChangesAsync, which EF wraps in a single transaction, matching the rest of this
        /// DAL (no explicit transactions anywhere). If any part fails, nothing is deleted -- the
        /// cohort never survives with its links already gone.
        /// </para>
        ///
        /// <para>
        /// CohortId is a SHADOW property on all four link types (they expose only the navigation),
        /// so the filters use EF.Property rather than a CLR member.
        /// </para>
        ///
        /// <para>
        /// Replaces an earlier Delete(Cohort) that did a bare Remove. That one was unreachable (not
        /// on ICohortQueries, zero callers) and would have thrown 23503 on any populated cohort, so
        /// it is removed rather than left as a footgun that looks usable.
        /// </para>
        /// </summary>
        /// <returns>false when no cohort with that id exists; true when one was deleted.</returns>
        public async Task<bool> Delete(long id)
        {
            try
            {
                Cohort cohort = await _dataDbContext.Cohort.FirstOrDefaultAsync(c => c.Id == id);
                if (cohort == null)
                {
                    return false;
                }

                _dataDbContext.CohortMember.RemoveRange(
                    _dataDbContext.CohortMember.Where(x => EF.Property<long?>(x, "CohortId") == id));

                _dataDbContext.CohortLinkedCurriculum.RemoveRange(
                    _dataDbContext.CohortLinkedCurriculum.Where(x => EF.Property<long?>(x, "CohortId") == id));

                _dataDbContext.CohortLinkedLocation.RemoveRange(
                    _dataDbContext.CohortLinkedLocation.Where(x => EF.Property<long?>(x, "CohortId") == id));

                _dataDbContext.HardwareLinkedCohort.RemoveRange(
                    _dataDbContext.HardwareLinkedCohort.Where(x => EF.Property<long?>(x, "CohortId") == id));

                _dataDbContext.Cohort.Remove(cohort);

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

        #endregion
    }       
}
