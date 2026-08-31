// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.UserModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    public interface ICohortMemberLogic
    {
        Task<List<CohortMember>> Get();
        Task<CohortMemberViewModel> Get(long? id);
        Task<CohortMember> Get(Guid? input);
        Task<List<CohortMember>> Get(Cohort input);
        Task<List<CohortMember>> GetCohortsByMember(Guid? input);
        Task<bool> AddMember(Guid CohortId, Guid UserId);
        Task<bool> RemoveMember(Guid CohortMemberId);
        Task<List<CohortMemberViewModel>> GetByCohort(long? id);
        Task<bool> Remove(CohortMember link);
        /// <summary>Remove every cohort membership for a user (called on account deletion -- a deleted user
        /// cannot remain a cohort member). Returns the number removed.</summary>
        Task<int> RemoveAllForUser(Guid userId);

        Task<CohortMember> Create(long cohortId, Guid userId);
    }


    public class CohortMemberLogic : ICohortMemberLogic
    {
        private readonly ICohortMemberQueries _context;
        private readonly ICohortQueries _cohortContext;
        //private readonly ICohortMemberQueries _userDataContext;
        // Dead fields (DI refactor Stage 3 cleanup): never assigned in either constructor
        // (both assignments below are commented out) and never read in this class. Commented
        // out rather than deleted, matching _userDataContext above. The live equivalents in
        // CohortLogic are a separate class.
        //private readonly ICohortLinkedCurriculumQueries _curriculumDataContext;
        //private readonly ILocationLinkedCohortQueries _locationDataContext;

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        private readonly UserManager<LocalApplicationUser> _userManagerContext;

        public CohortMemberLogic(
            IHttpContextAccessor httpContextAccessor,
            UserManager<LocalApplicationUser> userManager
            )
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;
            _context = new CohortMemberQueries();
            _cohortContext = new CohortQueries();
            _userManagerContext = userManager;
            //_userDataContext = new CohortLinkedUserQueries();
            //_curriculumDataContext = new CohortLinkedCurriculumQueries();
            //_locationDataContext = new LocationLinkedCohortQueries();
        }
        // DI refactor
        public CohortMemberLogic(
            IHttpContextAccessor httpContextAccessor,
            UserManager<LocalApplicationUser> userManager,
            ICohortMemberQueries context,
            ICohortQueries cohortContext
            )
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _context = context;
            _cohortContext = cohortContext;
            _userManagerContext = userManager;
            //_userDataContext = new CohortLinkedUserQueries();
            //_curriculumDataContext = new CohortLinkedCurriculumQueries();
            //_locationDataContext = new LocationLinkedCohortQueries();
        }

        #region Get                      
        public async Task<List<CohortMember>> Get()
        {
            //bool output = true;
            List<CohortMember> output = new List<CohortMember>();
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
        public async Task<CohortMember> Get(Guid? input)
        {
            //bool output = true;
            CohortMember output = new CohortMember();
            try
            {
                //use input to find subscription
                output = await _context.Get(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<CohortMemberViewModel> Get(long? input)
        {
            CohortMemberViewModel output = new CohortMemberViewModel();
            try
            {
                CohortMember member = await _context.Get(input);
                //use input to find subscription

                LocalUserViewModel tempUserVM = new LocalUserViewModel();
                LocalApplicationUser tempUser = await _userManagerContext.FindByIdAsync(member.UserId.ToString());
                tempUserVM.ApplicationUser = tempUser;
                var role = await _userManagerContext.GetRolesAsync(tempUser);
                foreach (var j in role)
                {
                    if (string.IsNullOrEmpty(tempUserVM.Role))
                    {
                        tempUserVM.Role = j;
                    }
                    else
                    {
                        tempUserVM.Role = tempUserVM.Role + ", " + j;
                    }
                }

                output = new CohortMemberViewModel()
                {
                    CohortMember = member,
                    UserData = tempUserVM
                };
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<CohortMember>> Get(Cohort input)
        {
            //bool output = true;
            List<CohortMember> output = new List<CohortMember>();
            try
            {
                //use input to find subscription
                output = await _context.Get(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<CohortMemberViewModel>> GetByCohort(long? input)
        {
            //bool output = true;
            List<CohortMemberViewModel> output = new List<CohortMemberViewModel>();
            try
            {
                //use input to find subscription
                List<CohortMember> preoutput = await _context.GetByCohort(input);

                foreach (var i in preoutput)
                {
                    LocalUserViewModel tempUserVM = new LocalUserViewModel();
                    LocalApplicationUser tempUser = await _userManagerContext.FindByIdAsync(i.UserId.ToString());
                    if (tempUser == null || tempUser.IsDeleted)
                    {
                        // Soft-deleted (AccountLifecycle.SoftDeleteOnly) or vanished member -- excluded from
                        // the active cohort roster (also avoids a NRE on a stale CohortMember row).
                        continue;
                    }
                    tempUserVM.ApplicationUser = tempUser;

                    var role = await _userManagerContext.GetRolesAsync(tempUser);

                    foreach (var j in role)
                    {
                        if (string.IsNullOrEmpty(tempUserVM.Role))
                        {
                            tempUserVM.Role = j;
                        }
                        else
                        {
                            tempUserVM.Role = tempUserVM.Role + ", " + j;
                        }
                    }

                    CohortMemberViewModel temp = new CohortMemberViewModel()
                    {
                        CohortMember = i,
                        UserData = tempUserVM
                    };
                    output.Add(temp);

                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<CohortMember>> GetCohortsByMember(Guid? input)
        {
            //bool output = true;
            List<CohortMember> output = new List<CohortMember>();
            try
            {
                //use input to find subscription
                output = await _context.GetCohortsByMember(input);

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
        public async Task<CohortMember> Create(CohortMember input)
        {
            CohortMember output = new CohortMember();
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

        #region Delete
        public async Task<bool> Delete(CohortMember input)
        {
            bool output = false;
            try
            {
                //CohortMember preoutput = await _context.Get(input.Id);
                output = await _context.Delete(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<bool> Delete(Guid input)
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

        public async Task<int> RemoveAllForUser(Guid userId)
        {
            // A deleted account cannot remain a cohort member. CohortMember rows live in a separate database
            // (no FK cascade from AspNetUsers), so remove them explicitly on account deletion.
            int removed = 0;
            List<CohortMember> memberships = await _context.GetCohortsByMember(userId);
            if (memberships != null)
            {
                foreach (CohortMember membership in memberships)
                {
                    if (await _context.Delete(membership))
                    {
                        removed++;
                    }
                }
            }
            return removed;
        }
        #endregion

        public async Task<bool> AddMember(Guid CohortId, Guid UserId)
        {
            bool output = false;
            CohortMember preoutput = new CohortMember();
            try
            {
                Cohort cohort = await _cohortContext.Get(CohortId);

                preoutput = new CohortMember()
                {
                    Cohort = cohort,
                    CohortUUID = cohort.UUID,
                    UserId = UserId
                };

                preoutput = await Create(preoutput);
                output = true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        public async Task<bool> RemoveMember(Guid CohortMemberId)
        {
            bool output = false;
            CohortMember preoutput = new CohortMember();
            try
            {
                output = await Delete(CohortMemberId);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        public async Task<bool> Remove(CohortMember link)
        {
            bool output = false;
            try
            {
                output = await Delete(link);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }


        public async Task<CohortMember> Create(long cohortId, Guid userId)
        {
            CohortMember output = new CohortMember();
            try
            {
                Cohort cohort = await _cohortContext.Get(cohortId);

                output = new CohortMember()
                {
                    Cohort = cohort,
                    CohortUUID = cohort.UUID,
                    UserId = userId
                };

                output = await _context.Create(output);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
    }


}
