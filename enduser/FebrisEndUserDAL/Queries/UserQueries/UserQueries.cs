// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.UserModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.UserQueries
{
    public interface IUserQueries
    {
        Task<List<LocalUserViewModel>> Get(InstitutionUserAccountType input);
        Task<LocalUserViewModel> Get(Guid? id);
        Task<List<LocalApplicationUser>> Get(List<Guid> id);
        Task<LocalApplicationUser> GetByActor(Guid actorUuid);
        Task<LocalApplicationUser> Update(LocalApplicationUser preoutput);
        Task<LocalApplicationUser> GetUser(Guid? id);
    }

    /// <summary>
    /// Node-local user directory reads over the Identity <see cref="ApplicationDbContext"/>
    /// (DI-only swappability contract).
    ///
    /// <para>
    /// The only cross-host-reachable member is <see cref="Get(List{Guid})"/>, resolved by the API
    /// host's <c>LauncherLogic.HardwareInialization</c> to turn a cohort's member ids into the
    /// hardware-user list. It reads the Users table through <see cref="_appDbContext"/>. The
    /// strangler DI constructor injects that context from the host container so the read flows
    /// through the DI seam rather than the static <c>ApplicationDbContext.ops.DbOptions</c> path
    /// (which reads app configuration at type load and hard-requires the developer's private
    /// <c>appsettings.Development.json</c> in DEBUG). Both EndUser hosts now register
    /// <see cref="ApplicationDbContext"/>, so the convention DI picks the injected ctor.
    /// </para>
    /// <para>
    /// The remaining <c>_userManager</c>-based members are legacy Portal-admin scaffolding whose
    /// backing field is not wired here (UserManager injection is out of scope for the node-local
    /// read path); they are unreachable from the converted call sites and are left untouched.
    /// </para>
    /// </summary>
    public class UserQueries : IUserQueries
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly UserManager<LocalApplicationUser> _userManager;

        // DI refactor: the host-scoped ApplicationDbContext is injected so the
        // node-local Users read resolves through DI. Registered on BOTH EndUser hosts -- the Portal
        // via AddIdentity's EF stores, the API via a dedicated AddDbContext<ApplicationDbContext>.
        public UserQueries(ApplicationDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public UserQueries()
        {
            // [Historical] static-ops fallback (pre DI seam). Retained so any not-yet-DI call site
            // still constructs, but the reachable API/Portal graphs now resolve the injected ctor
            // above (ApplicationDbContext is registered on both hosts), so this path is dormant.
            _appDbContext = new ApplicationDbContext(ApplicationDbContext.ops.DbOptions);

           // _userManager = new ApplicationDbContext(ApplicationDbContext.ops.DbOptions);
            //var serviceProvider = serviceScope.ServiceProvider;
            //var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            //var userManager = serviceProvider.GetRequiredService<UserManager<LocalApplicationUser>>();
            ////var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            ////var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
            //var applicationUser = serviceProvider.GetRequiredService<ApplicationDbContext>();
        }

        #region Get
        public async Task<LocalApplicationUser> GetUser(Guid? input)
        {            
            LocalApplicationUser output = new LocalApplicationUser();
            try
            {
                //output = await _userManager.FindByIdAsync(input.ToString());               
               
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        //public async Task<LocalApplicationUser> Get(Guid input)
        //{
        //    LocalApplicationUser output = new LocalApplicationUser();
        //    try
        //    {
        //        output = await _userManager.FindByIdAsync(input.ToString());
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }            
        //    return output;
        //}
        // REMOVED 2026-08-09 (dead, and both were traps):
        //
        //   Get()              -- parameterless user list. ZERO callers: UserLogic builds its own
        //                         list from UserManager and its IUserQueries field is commented
        //                         out, and the only live consumer, LauncherLogic:255, calls
        //                         Get(List<Guid>). It excluded users holding the SuperAdmin role,
        //                         which the node stopped minting on the 2026-08-01 owner ruling,
        //                         so the filter matched nothing. Left in place it looked usable on
        //                         the interface, and wiring it into the user index would have
        //                         reintroduced the exact defect UserLogic:842 records as having
        //                         already happened once -- the sole administrator rendering in the
        //                         Educator-visible index. Visibility filtering belongs in the BLL,
        //                         where the rank-based rule that replaced it lives.
        //
        //   GetSuperAdminList() -- never on IUserQueries, zero callers, and the name was false: it
        //                         filtered nothing and returned every user.

        public async Task<LocalUserViewModel> Get(Guid? input)
        {
            LocalUserViewModel output = new LocalUserViewModel();
            LocalApplicationUser preoutput = new LocalApplicationUser();
            try
            {
                preoutput = await _userManager.FindByIdAsync(input.ToString());
                var role = await _userManager.GetRolesAsync(preoutput);
               
                foreach (var j in role)
                {
                    if (string.IsNullOrEmpty(output.Role))
                    {
                        output.Role = j;
                    }
                    else
                    {
                        output.Role = output.Role + ", " + j;
                    }
                }

                if (preoutput.LockoutEnd > DateTime.UtcNow)
                {
                    output.IsLockedOut = true;
                }
                else
                {
                    output.IsLockedOut = false;
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<LocalApplicationUser>> Get(List<Guid> input)
        {
            // Batch fetch: single round-trip with `WHERE Id IN (...)` instead
            // of one query per id (previous foreach version was O(N) DB hits).
            // Caller-facing contract preserved: returns all users whose Id is
            // in `input`. Order is not guaranteed to match `input` because the
            // previous version's order was already incidental (and several
            // callers iterate the result without index assumptions).
            List<LocalApplicationUser> output = new List<LocalApplicationUser>();
            try
            {
                if (input == null || input.Count == 0)
                {
                    return output;
                }

                output = await _appDbContext.Users
                    .AsNoTracking()
                    .Where(j => input.Contains(j.Id) && !j.IsDeleted)   // soft-deleted accounts are not active/entitled members
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
        /// Reverse the stored xAPI Actor link: given an Actor UUID, the node-local account it
        /// belongs to, or null when no account claims it.
        ///
        /// <para>
        /// ROADMAP 22 needed this and it did not exist. <c>LocalApplicationUser.Actor</c> has been
        /// WRITTEN on every provisioning path since the identity chain landed
        /// (<c>UserLogic.ProvisionUserAsync</c>) and READ by the guardian-link flow, but nothing
        /// walked it backwards, so a launch that knows only an ActorId could not reach the
        /// learner's cohort memberships. It is a node-local directory read on the same
        /// <see cref="ApplicationDbContext"/> as <see cref="Get(List{Guid})"/> -- the EndUser tier
        /// is its own auth island and this reads nothing central.
        /// </para>
        ///
        /// <para>
        /// Soft-deleted accounts are excluded, matching <see cref="Get(List{Guid})"/>: a retained
        /// but locked-out account is not an active member, so its cohorts must not drive a live
        /// recording decision. <c>Guid.Empty</c> is refused rather than matched, because an
        /// unlinked account stores the Actor link as null and a caller passing an empty Guid is
        /// asking a question with no answer, not asking for "the unlinked ones".
        /// </para>
        /// </summary>
        public async Task<LocalApplicationUser> GetByActor(Guid actorUuid)
        {
            try
            {
                if (actorUuid == Guid.Empty)
                {
                    return null;
                }

                return await _appDbContext.Users
                    .AsNoTracking()
                    .Where(j => j.Actor == actorUuid && !j.IsDeleted)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<List<LocalUserViewModel>> Get(InstitutionUserAccountType input)
        {
            List<LocalUserViewModel> output = new List<LocalUserViewModel>();
            List<LocalApplicationUser> userList = new List<LocalApplicationUser>();
            try
            {
                userList = await _userManager.Users.Where(u => !u.IsDeleted).ToListAsync();   // exclude soft-deleted (AccountLifecycle.SoftDeleteOnly)
                //.Where(i=>i.Role==input)
                //.ToList();
                foreach (LocalApplicationUser i in userList.ToList())
                {
                    LocalUserViewModel temp = new LocalUserViewModel();
                    temp.ApplicationUser = i;


                    var role = await _userManager.GetRolesAsync(i);
                    if (!role.Contains(input.ToString()))
                    {
                        userList.Remove(i);
                        continue;
                    }

                    foreach (var j in role)
                    {
                        if (string.IsNullOrEmpty(temp.Role))
                        {
                            temp.Role = j;
                        }
                        else
                        {
                            temp.Role = temp.Role + ", " + j;
                        }
                    }

                    if (i.LockoutEnd > DateTime.UtcNow)
                    {
                        temp.IsLockedOut = true;
                    }
                    else
                    {
                        temp.IsLockedOut = false;
                    }

                    temp.UserId = i.Id;
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

        #endregion 

        #region Create
        public async Task<LocalApplicationUser> Create(LocalApplicationUser input, string secret)
        {
            await _userManager.CreateAsync(input, secret);
            return input;
        }
        public async void AddRole(LocalApplicationUser input, AccountType type)
        {
            await _userManager.AddToRoleAsync(input, type.ToString());
        }
        #endregion

        #region Update
        //public async Task<ApplicationUser> Update(AdminUserViewModel input)
        //{
        //    //Get user
        //    ApplicationUser user = await Get(input.Id);

        //    //update information
        //    user.UserName = input.EmailAddress;
        //    user.PhoneNumber = input.PhoneNumber.ToString();
        //    user.Email = input.EmailAddress;
        //    await _userManager.UpdateAsync(user);

        //    //update role
        //    var currentUserRole = await _userManager.GetRolesAsync(user);
        //    await _userManager.RemoveFromRolesAsync(user, currentUserRole.ToArray());

        //    //save changes
        //    await _userManager.UpdateAsync(user);
        //    await _userManager.AddToRoleAsync(user,input.FebrisOccupationType.ToString());


        //    return user;
        //}
        public async Task<LocalApplicationUser> Update(LocalApplicationUser input)
        {           
            try
            {
                await _userManager.UpdateAsync(input);
           
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return input;
        }
        public async Task<UserSettingsViewModel> Update(UserSettingsViewModel input)
        {
            UserSettingsViewModel output = new UserSettingsViewModel();
            try
            {
                LocalApplicationUser user = await _userManager.FindByIdAsync(input.ToString());
                if (!string.IsNullOrEmpty(input.FirstName))
                {
                    user.FirstName = input.FirstName;
                }
                if (!string.IsNullOrEmpty(input.LastName))
                {
                    user.LastName = input.LastName;
                }
                if (!string.IsNullOrEmpty(input.EmailAddress))
                {
                    user.Email = input.EmailAddress;
                    user.UserName = input.EmailAddress;
                    user.NormalizedUserName = input.EmailAddress.ToUpper();
                }
                if (!string.IsNullOrEmpty(input.PhoneNumber))
                {
                    user.PhoneNumber = input.PhoneNumber;
                }
                if (!string.IsNullOrEmpty(input.ProfilePicturePath))
                {
                    user.ProfilePicturePath = input.ProfilePicturePath;
                }

                _appDbContext.Users.Update(user);
                await _appDbContext.SaveChangesAsync();

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<LocalApplicationUser> Update(LocalUserSettingsViewModel input)
        {
            LocalApplicationUser output = new LocalApplicationUser();
            try
            {
                output = await _userManager.FindByIdAsync(input.ToString());
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }


        //public async Task<ApplicationUser> UpdatePassword(AdminUserViewModel input)
        //{
        //    //var store = this.Store as IUserPasswordStore;
        //    //Get user
        //    ApplicationUser user = await Get(input.Id);

        //    _userManager.PasswordHasher(user, input.password);

        //    return user;
        //}

        ////public async void UpdateRole(ApplicationUser input, FebrisOccupationType type)
        //{
        //    await _userManager.AddToRoleAsync(input, type.ToString());
        //}
        #endregion 

        #region Delete
        public async void RemoveRole(LocalApplicationUser input)
        {
            var currentUserRole = await _userManager.GetRolesAsync(input);
            await _userManager.RemoveFromRolesAsync(input, currentUserRole.ToArray());
            await _userManager.UpdateAsync(input);
        }

       




        #endregion

        #region Lock User Accounts

        #endregion
    }
        
}
