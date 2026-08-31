// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.ViewModels;
using Febris.PrimaryLogicLayer.Logic.UserLogic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Febris.UserNode.Portal.Controllers.User
{
    /// <summary>
    /// Admin-only UI for managing parent/guardian accounts and the students linked
    /// to them. A link is what grants a parent FERPA read-only access to a student's
    /// learning records (XApiAccessScope reads these links), so the surface is
    /// restricted to Admin / IT Admin / Super Admin. Educators are intentionally
    /// excluded by design: they can read learning records but may not
    /// manage guardianship. The BLL re-checks the admin role on every call, so the
    /// controller gate and the BLL gate are defense in depth.
    /// </summary>
    [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
    public class ParentLinkController : Controller
    {
        private readonly IParentLinkLogic _parentLinks;
        private readonly IUserLogic _users;

        public ParentLinkController(IParentLinkLogic parentLinks, IUserLogic users)
        {
            _parentLinks = parentLinks;
            _users = users;
        }

        // GET: /ParentLink
        // List every parent/guardian account in the tenant.
        public async Task<IActionResult> Index()
        {
            List<LocalUserViewModel> parents = await _users.Get(InstitutionUserAccountType.UserParent);
            return View(parents ?? new List<LocalUserViewModel>());
        }

        // GET: /ParentLink/Manage/{id}
        // Show the students already linked to this parent and the learners available
        // to link.
        public async Task<IActionResult> Manage(Guid id)
        {
            ParentLinkManagementViewModel vm = new ParentLinkManagementViewModel
            {
                ParentUserId = id,
                LinkedStudents = await _parentLinks.GetLinkedStudents(id),
                LinkableStudents = await _users.Get(InstitutionUserAccountType.User)
            };
            return View(vm);
        }

        // POST: /ParentLink/Link
        // Link one student to a parent. The BLL enforces admin role, parent-account
        // type, and student-has-an-actor, so a failure here is a guard rejection.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Link(Guid parentUserId, Guid studentUserId)
        {
            bool ok = await _parentLinks.Link(parentUserId, studentUserId);
            TempData["StatusMessage"] = ok
                ? "Student linked to the guardian."
                : "Could not link the student (confirm the student has a learner profile).";
            return RedirectToAction(nameof(Manage), new { id = parentUserId });
        }

        // POST: /ParentLink/Unlink
        // Remove a parent's link to one student actor.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlink(Guid parentUserId, Guid studentActorId)
        {
            bool ok = await _parentLinks.Unlink(parentUserId, studentActorId);
            TempData["StatusMessage"] = ok
                ? "Student unlinked from the guardian."
                : "Could not unlink the student.";
            return RedirectToAction(nameof(Manage), new { id = parentUserId });
        }

        // GET: /ParentLink/Create
        // Form to create a new parent/guardian account.
        public IActionResult Create()
        {
            return View(new LocalUserCreation { UserAccountType = InstitutionUserAccountType.UserParent });
        }

        // POST: /ParentLink/Create
        // Create the guardian account, then jump straight to managing its links.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LocalUserCreation input)
        {
            if (input == null)
            {
                return View(new LocalUserCreation { UserAccountType = InstitutionUserAccountType.UserParent });
            }

            // This controller only ever creates guardians, so force the role
            // regardless of what was posted.
            input.UserAccountType = InstitutionUserAccountType.UserParent;

            var created = await _users.Create(input);
            if (created == null)
            {
                TempData["StatusMessage"] = "Could not create the guardian account.";
                return View(input);
            }

            TempData["StatusMessage"] = "Guardian account created. Now link their students.";
            return RedirectToAction(nameof(Manage), new { id = created.Id });
        }
    }
}
