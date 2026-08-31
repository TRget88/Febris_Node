// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    public interface ICohortLogic
    {
        Task<List<Cohort>> Get();
        Task<List<Cohort>> Get(bool includeArchived);
        Task<Cohort> Get(long? id);
        Task<Cohort> Get(Guid? id);
        Task<Cohort> Update(Cohort cohort);
        Task<Cohort> Create(Cohort cohort);
        Task<bool> ArchiveToggle(long id);

        /// <summary>
        /// ROADMAP 22: flip this cohort's recording policy. The ONLY writer of
        /// <c>Cohort.RecordSessions</c>, and the read side is
        /// <c>LauncherLogic.ShouldRecordSession</c>, which derives every launch's record decision
        /// from it. Same role gate as <see cref="ArchiveToggle"/>: the educator who runs the class
        /// decides whether it is recorded.
        /// </summary>
        /// <returns>false when no cohort with that id exists, or the caller is not permitted.</returns>
        Task<bool> RecordSessionsToggle(long id);

        /// <summary>
        /// HARD delete. Removes the cohort and its link rows only -- users, curricula, locations
        /// and hardware all survive. Distinct from <see cref="ArchiveToggle"/>, which is
        /// reversible and is still the ordinary way to retire a cohort.
        /// </summary>
        Task<bool> Delete(long id);
        Task<CohortAccessListViewModel> GetCohortAccessList(long id);
    }


    public class CohortLogic: ICohortLogic
    {
        private readonly ICohortQueries _context;
        private readonly ICohortMemberQueries _memberContext;
        private readonly ICohortLinkedCurriculumQueries _curriculumDataContext;
        private readonly ILocationLinkedCohortQueries _locationDataContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;

        public CohortLogic(
            IHttpContextAccessor httpContextAccessor
            )
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;
            _context = new CohortQueries();
            _memberContext = new CohortMemberQueries();
            _curriculumDataContext = new CohortLinkedCurriculumQueries();
            _locationDataContext = new LocationLinkedCohortQueries();
        }
        // DI refactor
        public CohortLogic(
            IHttpContextAccessor httpContextAccessor,
            ICohortQueries context,
            ICohortMemberQueries memberContext,
            ICohortLinkedCurriculumQueries curriculumDataContext,
            ILocationLinkedCohortQueries locationDataContext
            )
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _context = context;
            _memberContext = memberContext;
            _curriculumDataContext = curriculumDataContext;
            _locationDataContext = locationDataContext;
        }

        #region Get                      
        public async Task<List<Cohort>> Get()
        {
            #region Filter
            //if (!User.IsLocalFebrisAdmin() && !User.IsLocalAdmin() && !User.IsLocalEducator())
            //{
            //    return default;
            //}
            #endregion
            //bool output = true;
            List<Cohort> output = new List<Cohort>();
            try
            {
                output = await _context.Get();
                //output.AddRange(preoutput);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        /// <summary>
        /// ROADMAP 19. Active cohorts by default; archived included when the operator asks.
        /// Without the include path, archiving is a one-way door -- the un-archive toggle sits on
        /// the index row that archiving removes. Mirrors CurriculumLogic.Get(bool includeObsolete).
        /// </summary>
        public async Task<List<Cohort>> Get(bool includeArchived)
        {
            List<Cohort> output = new List<Cohort>();
            try
            {
                output = includeArchived
                    ? await _context.GetIncludingArchived()
                    : await _context.Get();
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
            #region Filter
            //if (!User.IsLocalFebrisAdmin() && !User.IsLocalAdmin() && !User.IsLocalEducator())
            //{
            //    return default;
            //}
            #endregion
            //bool output = true;
            Cohort output = new Cohort();
            try
            {
                //use input to find subscription
                output = await _context.Get(input);
                //output = subscription;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<Cohort> Get(long? input)
        {
            #region Filter
            //if (!User.IsLocalFebrisAdmin() && !User.IsLocalAdmin() && !User.IsLocalEducator())
            //{
            //    return default;
            //}
            #endregion
            //bool output = true;
            Cohort output = new Cohort();
            try
            {
                //use input to find subscription
                output = await _context.Get(input);
                //output = subscription;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        
        #endregion

        #region Post
        //public Task<string> Create(CohortViewModel input)
        //{
        //    throw new NotImplementedException();
        //}
        public async Task<Cohort> Create(Cohort input)
        {
            #region Filter
            if (!User.IsLocalFebrisAdmin() && !User.IsLocalAdmin() && !User.IsLocalEducator())
            {
                return default;
            }
            #endregion
            Cohort output = new Cohort();
            try
            {
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
        public async Task<Cohort> Update(Cohort input)
        {
            #region Filter
            if (!User.IsLocalFebrisAdmin() && !User.IsLocalAdmin() && !User.IsLocalEducator())
            {
                return default;
            }
            #endregion
            Cohort output = new Cohort();
            try
            {
                // The Edit POST binds a fixed property list that does NOT include Archive or
                // LockMembers, and Edit.cshtml renders neither, so they arrive as false on every
                // save. CohortQueries.Update then does a whole-entity DbSet.Update which writes
                // every scalar -- so saving a cohort's NAME silently un-archived it and unlocked
                // its members. Nothing else can set either flag back to true, so this was one-way
                // data loss on a fully reachable, pure-clickthrough path.
                //
                // Fixed by loading the stored row and copying only the EDITABLE fields onto it,
                // mirroring ModuleLogic.Save. Archive and LockMembers are simply never touched, so
                // the edit form has no way to assert a value for them -- they are owned by their
                // own toggle actions. This is preferable to round-tripping them as hidden fields,
                // which would hand the form an overposting surface for exactly the two flags it
                // must not control.
                //
                // Note the tracking constraint: CohortQueries.Get uses FindAsync, which TRACKS.
                // Copying onto that same instance is required -- passing a second instance with
                // the same key to DbSet.Update throws "another instance with the same key value is
                // already being tracked".
                Cohort stored = await _context.Get(input.Id);
                if (stored == null)
                {
                    return default;
                }

                stored.Name = input.Name;
                stored.Description = input.Description;
                stored.InstructorId = input.InstructorId;
                stored.LastUpdateTimeStamp = DateTime.Now;

                output = await _context.Update(stored);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }



        #endregion

        #region Delete

        /// <summary>
        /// HARD delete of a cohort. Owner ruling 2026-08-09: delete the cohort and its link rows,
        /// and nothing on the other side of those links.
        ///
        /// <para>
        /// The DAL does the row work in one transaction; see <c>CohortQueries.Delete</c> for exactly
        /// what is removed and why the three RESTRICT links MUST go in the same statement.
        /// </para>
        ///
        /// <para>
        /// ROLE FILTER, and this one IS a considered policy, not just the matched shape. Owner ruling
        /// 2026-08-09: Educator KEEPS hard delete, because educators manage users as well. The
        /// question was asked explicitly -- an irreversible delete is a different risk from a
        /// reversible archive -- and answered. Do not re-litigate it.
        ///
        /// <para>
        /// The gate resolves to Admin, ITAdmin and Educator. ITAdmin arrives through the OR branch
        /// inside IsLocalAdmin() rather than a clause of its own, and IsLocalFebrisAdmin() is dead on
        /// a node (it tests SuperAdmin, which the 2026-08-01 ruling stopped seeding). All three
        /// permitted roles are pinned individually in CohortHardDeleteTests, so none of that depends
        /// on reading the shared helpers correctly.
        /// </para>
        /// </para>
        /// </summary>
        /// <returns>false when no cohort with that id exists, or the caller is not permitted.</returns>
        public async Task<bool> Delete(long id)
        {
            #region Filter
            if (!User.IsLocalFebrisAdmin() && !User.IsLocalAdmin() && !User.IsLocalEducator())
            {
                return default;
            }
            #endregion

            bool output = false;
            try
            {
                output = await _context.Delete(id);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        #endregion

        public async Task<bool> ArchiveToggle(long id)
        {
            #region Filter
            if (!User.IsLocalFebrisAdmin() && !User.IsLocalAdmin() && !User.IsLocalEducator())
            {
                return default;
            }
            #endregion
            bool output = false;
            Cohort preoutput = new Cohort();
            try
            {
                preoutput = await _context.Get(id);
                preoutput.Archive = !preoutput.Archive;
                preoutput = await _context.Update(preoutput);
                output = true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        /// <summary>
        /// ROADMAP 22. Deliberately shaped exactly like <see cref="ArchiveToggle"/> above,
        /// including reading the cohort back through <c>_context.Get</c> and writing the whole
        /// entity: <c>CohortQueries.Get</c> returns a TRACKED instance and <c>Update</c> copies
        /// onto it, so flipping one flag this way cannot clobber another (the failure the cohort
        /// retirement-flag tests exist to catch).
        /// </summary>
        public async Task<bool> RecordSessionsToggle(long id)
        {
            #region Filter
            if (!User.IsLocalFebrisAdmin() && !User.IsLocalAdmin() && !User.IsLocalEducator())
            {
                return default;
            }
            #endregion
            bool output = false;
            Cohort preoutput = new Cohort();
            try
            {
                preoutput = await _context.Get(id);
                preoutput.RecordSessions = !preoutput.RecordSessions;
                preoutput = await _context.Update(preoutput);
                output = true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        // MemberLockToggle removed 2026-08-05 (ROADMAP 19). It was the only writer of
        // Cohort.LockMembers, which nothing in the product read as a gate -- see the note in
        // CohortController where the action lived. The column stays in the database.

        public async Task<CohortAccessListViewModel> GetCohortAccessList(long id)
        {
            try
            {
                // Access is curriculum-derived and entirely node-local. The cohort is linked to
                // curricula (CohortLinkedCurriculum) and every member inherits that access, so
                // Seats is the member count rather than a per-user purchase tally. The old
                // implementation asked the hub what the cohort's members had BOUGHT, which
                // returned nothing on a self-hosted node.
                Cohort cohort = await _context.Get(id);
                List<CohortMember> memberList = await _memberContext.Get(cohort);
                int seats = memberList != null ? memberList.Count : 0;

                List<CohortLinkedCurriculum> linkedCurriculumList =
                    await _curriculumDataContext.GetListByCohort(cohort);

                CohortAccessListViewModel output = new CohortAccessListViewModel()
                {
                    AccessList = new List<CohortAccessEntryViewModel>()
                };

                foreach (CohortLinkedCurriculum item in linkedCurriculumList)
                {
                    // A cohort can carry the same curriculum through more than one link row;
                    // collapse to one entry so the list reflects distinct access, not link rows.
                    if (item.Curriculum == null
                        || output.AccessList.Any(i => i.Curriculum.Id == item.Curriculum.Id))
                    {
                        continue;
                    }

                    output.AccessList.Add(new CohortAccessEntryViewModel()
                    {
                        Seats = seats,
                        Curriculum = item.Curriculum
                    });
                }

                output.AccessList = output.AccessList
                    .OrderBy(i => i.Curriculum.Name)
                    .ToList();

                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }            
        }
    }


}
