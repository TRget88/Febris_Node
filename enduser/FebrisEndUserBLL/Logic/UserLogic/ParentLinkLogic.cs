// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.UserModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Febris.PrimaryLogicLayer.Logic.UserLogic
{
    /// <summary>
    /// Admin-managed parent/guardian to student links that back FERPA read-only
    /// access. Creating a link records the student's xApi Actor UUID against the
    /// parent (a <see cref="ParentLinkedStudent"/> row); that actor UUID is exactly
    /// what <c>XApiAccessScope</c> reads when it scopes a parent's xApi reads, so a
    /// link here is what actually grants a parent visibility into a student's
    /// learning records.
    ///
    /// Linking is admin-only by design: only Admin / IT Admin / Febris Super Admin
    /// may create or remove links, not educators and not the parents themselves.
    /// Every link and unlink is written to the audit log so the FERPA "who linked
    /// whom, and when" question is answerable. The codebase has no audit table, so
    /// this records through the existing logging path.
    /// </summary>
    public interface IParentLinkLogic
    {
        /// <summary>The students currently linked to a parent. Empty for non-admins.</summary>
        Task<List<ParentLinkViewModel>> GetLinkedStudents(Guid parentUserId);

        /// <summary>
        /// Link a parent to a student. Resolves the student's learner actor, verifies
        /// the target really is a UserParent account, and is idempotent (a repeat
        /// link is a no-op success, never a second row). Returns false when the caller
        /// is not an admin, the parent is not a parent account, or the student has no
        /// learner actor to grant.
        /// </summary>
        Task<bool> Link(Guid parentUserId, Guid studentUserId);

        /// <summary>
        /// Remove a parent's link to one student actor. Returns false when the caller
        /// is not an admin or no such link existed.
        /// </summary>
        Task<bool> Unlink(Guid parentUserId, Guid studentActorId);

        /// <summary>
        /// Cascade half of the EndUser lockout (B-07 sibling). Given a student user that was just
        /// locked out, lock that student's linked parent(s) only when EVERY one of that parent's
        /// linked children is now also locked. One-directional (student -> parent). Internal cascade
        /// invoked after an already-authorized lock, not a caller entry point.
        /// </summary>
        Task CascadeLockParentIfAllChildrenLocked(LocalApplicationUser lockedStudent);
    }

    /// <inheritdoc cref="IParentLinkLogic"/>
    public class ParentLinkLogic : IParentLinkLogic
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        private readonly UserManager<LocalApplicationUser> _userManager;
        private readonly IParentLinkedStudentQueries _links;

        /// <summary>
        /// Production constructor. <see cref="UserManager{TUser}"/> is resolved from
        /// the Identity container; the link DAL is constructed here, the same
        /// fallback-construction pattern the other tenant BLLs use for their query
        /// objects, so no extra DI registration is needed for the queries.
        /// </summary>
        public ParentLinkLogic(IHttpContextAccessor httpContextAccessor, UserManager<LocalApplicationUser> userManager)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _userManager = userManager;
            _links = new ParentLinkedStudentQueries();
        }

        /// <summary>
        /// Test constructor: lets a unit test inject a fake link DAL so the link/unlink
        /// behavior can be verified without a database.
        /// </summary>
        public ParentLinkLogic(IHttpContextAccessor httpContextAccessor, UserManager<LocalApplicationUser> userManager, IParentLinkedStudentQueries links)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _userManager = userManager;
            _links = links;
        }

        /// <summary>
        /// Admin-only gate for every operation here. Educators are intentionally
        /// excluded (decision 3): they can read learning records but may not manage
        /// guardianship.
        /// </summary>
        private bool IsAdmin()
        {
            return User != null && (User.IsLocalAdmin() || User.IsLocalFebrisAdmin());
        }

        /// <summary>The acting admin's user id, recorded in the audit line.</summary>
        private string ActingAdminId()
        {
            return User?.GetUserId() ?? "unknown";
        }

        public async Task<bool> Link(Guid parentUserId, Guid studentUserId)
        {
            if (!IsAdmin())
            {
                return false;
            }
            try
            {
                // The student must have a learner actor; that actor UUID is what the
                // parent will be granted read access to. No actor means there is
                // nothing to link.
                LocalApplicationUser student = await _userManager.FindByIdAsync(studentUserId.ToString());
                if (student == null || student.Actor == null || student.Actor == Guid.Empty)
                {
                    return false;
                }

                // The target must actually be a parent/guardian account, never an
                // admin or learner masquerading as one.
                LocalApplicationUser parent = await _userManager.FindByIdAsync(parentUserId.ToString());
                if (parent == null)
                {
                    return false;
                }
                bool parentIsParentRole = await _userManager.IsInRoleAsync(parent, InstitutionUserAccountType.UserParent.ToString());
                if (!parentIsParentRole)
                {
                    return false;
                }

                Guid studentActorId = student.Actor.Value;

                // Idempotent: a duplicate link is a no-op success, never a second row.
                if (await _links.Exists(parentUserId, studentActorId))
                {
                    return true;
                }

                // UUID and timestamps are set here (the BLL-sets convention the other
                // Create paths follow; the DAL Link just persists the row).
                ParentLinkedStudent link = new ParentLinkedStudent
                {
                    UUID = Guid.NewGuid(),
                    TimeStamp = DateTime.UtcNow,
                    LastUpdateTimeStamp = DateTime.UtcNow,
                    ParentUserId = parentUserId,
                    StudentUserId = studentUserId,
                    StudentActorId = studentActorId
                };
                await _links.Link(link);

                // FERPA audit: no audit table exists, so record the grant through the
                // existing logging path (design note section 7).
                Febris.SharedServices.FebrisLog.Info($"[FERPA-AUDIT] LINK parentUserId={parentUserId} studentUserId={studentUserId} studentActorId={studentActorId} byAdmin={ActingAdminId()} atUtc={DateTime.UtcNow:o}");
                return true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<bool> Unlink(Guid parentUserId, Guid studentActorId)
        {
            if (!IsAdmin())
            {
                return false;
            }
            try
            {
                bool removed = await _links.Unlink(parentUserId, studentActorId);
                if (removed)
                {
                    // FERPA audit: record the revocation of access.
                    Febris.SharedServices.FebrisLog.Info($"[FERPA-AUDIT] UNLINK parentUserId={parentUserId} studentActorId={studentActorId} byAdmin={ActingAdminId()} atUtc={DateTime.UtcNow:o}");
                }
                return removed;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task CascadeLockParentIfAllChildrenLocked(LocalApplicationUser lockedStudent)
        {
            try
            {
                // Link rows are keyed by the student's actor id (LocalApplicationUser.Actor). No actor
                // means no parent link to follow.
                if (lockedStudent == null || lockedStudent.Actor == null || lockedStudent.Actor == Guid.Empty)
                {
                    return;
                }
                Guid studentActorId = lockedStudent.Actor.Value;

                List<ParentLinkedStudent> parentRows = await _links.GetParentsForStudent(studentActorId);
                List<Guid> parentIds = parentRows.Select(r => r.ParentUserId).Distinct().ToList();

                foreach (Guid parentId in parentIds)
                {
                    List<ParentLinkedStudent> childLinks = await _links.GetByParent(parentId);
                    bool allChildrenLocked = true;
                    foreach (ParentLinkedStudent childLink in childLinks)
                    {
                        LocalApplicationUser child = await _userManager.FindByIdAsync(childLink.StudentUserId.ToString());
                        // A missing child cannot be confirmed locked; treat as not-all-locked so we
                        // never lock a parent on incomplete information.
                        if (child == null)
                        {
                            allChildrenLocked = false;
                            break;
                        }
                        DateTimeOffset? childLockoutEnd = await _userManager.GetLockoutEndDateAsync(child);
                        bool childLocked = childLockoutEnd != null && childLockoutEnd > DateTimeOffset.UtcNow;
                        if (!childLocked)
                        {
                            allChildrenLocked = false;
                            break;
                        }
                    }

                    if (!allChildrenLocked)
                    {
                        continue;
                    }

                    LocalApplicationUser parent = await _userManager.FindByIdAsync(parentId.ToString());
                    if (parent == null)
                    {
                        continue;
                    }
                    DateTimeOffset? parentLockoutEnd = await _userManager.GetLockoutEndDateAsync(parent);
                    bool parentAlreadyLocked = parentLockoutEnd != null && parentLockoutEnd > DateTimeOffset.UtcNow;
                    if (parentAlreadyLocked)
                    {
                        continue;
                    }

                    await _userManager.SetLockoutEndDateAsync(parent, DateTimeOffset.MaxValue);
                    await _userManager.UpdateAsync(parent);
                    Febris.SharedServices.FebrisLog.Info("[LOCKOUT-CASCADE] locked parentUserId=" + parentId
                        + " because all linked children are now locked (triggered by studentActorId=" + studentActorId + ")");
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<List<ParentLinkViewModel>> GetLinkedStudents(Guid parentUserId)
        {
            List<ParentLinkViewModel> output = new List<ParentLinkViewModel>();
            if (!IsAdmin())
            {
                return output;
            }
            try
            {
                List<ParentLinkedStudent> links = await _links.GetByParent(parentUserId);
                foreach (ParentLinkedStudent link in links)
                {
                    ParentLinkViewModel vm = new ParentLinkViewModel
                    {
                        ParentUserId = link.ParentUserId,
                        StudentUserId = link.StudentUserId,
                        StudentActorId = link.StudentActorId
                    };

                    // Resolve the student's display details for the admin table.
                    LocalApplicationUser student = await _userManager.FindByIdAsync(link.StudentUserId.ToString());
                    if (student != null)
                    {
                        vm.StudentName = ($"{student.FirstName} {student.LastName}").Trim();
                        vm.StudentEmail = student.Email;
                    }
                    output.Add(vm);
                }
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
