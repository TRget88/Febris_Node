// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using X.PagedList;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.ModelLibrary.ViewModels;
using Febris.EnumLibrary;
using Febris.PrimaryLogicLayer.Logic.UserLogic;
using Microsoft.AspNetCore.Authorization;

namespace Febris.UserNode.Portal.Controllers.Data
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    [Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]
    public class CohortController : Controller
    {
        //templates MVCControllerwithContext generator used                
        private readonly ILogger<CohortController> _logger;
        private readonly ICohortLogic _context;
        private readonly ICohortMemberLogic _memberContext;
        private readonly IUserLogic _userContext;
        public CohortController(
            ICohortLogic context,
            ICohortMemberLogic memberContext,
            IUserLogic userContext,
            ILogger<CohortController> logger)
        {
            _context = context;
            _logger = logger;
            _memberContext = memberContext;
            _userContext = userContext;
        }

        [TempData]
        private string StatusMessage
        {
            get
            {
                // Was `return StatusMessage;` -- the property returning ITSELF. Any read was
                // unbounded recursion and a StackOverflowException, which .NET cannot catch and
                // which takes the whole process down rather than failing one request. It survived
                // only because nothing ever read it. Now reads the store the setter writes.
                return TempData["StatusMessage"] as string;
            }
            set
            {
                TempData["StatusMessage"] = value;
                return;
            }
        }

        #region Index
        // GET: Cohort
        public async Task<IActionResult> Index()
        {
            return View();
        }
        // GET: Cohort/IndexPartial
        // ROADMAP 19: includeArchived drives the "Show archived" toggle on the index. Archived
        // cohorts are excluded by default so a finished term stops cluttering the list, but the
        // include path has to exist in the SAME change -- the un-archive toggle lives on the row
        // that archiving removes, so without it archiving is a one-way door. That is the trap
        // already recorded for curricula in ROADMAP 11.
        public async Task<IActionResult> IndexPartial(string currentFilter, string searchString, int? page, bool includeArchived = false)
        {
            List<Cohort> outputSetup = new List<Cohort>();
            try
            {
                if (searchString != null)
                {
                    page = 1;
                }
                else
                {
                    searchString = currentFilter;
                }

                if (!string.IsNullOrEmpty(searchString))
                {
                    TempData["CurrentFilter"] = searchString;
                }

                outputSetup = await _context.Get(includeArchived);
                ViewData["IncludeArchived"] = includeArchived;

                if (!String.IsNullOrEmpty(searchString))
                {
                    //set up how to search db
                    //outputSetup = await outputSetup.Where(b => (b.AccreditationBody.Name ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.AccreditationBody.Description ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.Name ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.ZipCode.ToString() ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.State ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.ContentDeveloperType.ToString() ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.Address ?? "").ToLower().Contains(searchString.ToLower())
                    //).ToListAsync();
                }

                int pageNumber = (page ?? 1);
                var output = await outputSetup.ToPagedListAsync(pageNumber, 25);
                return PartialView("IndexPartial", output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
            }
            return PartialView();
        }
        #endregion

        #region Details

        // GET: Cohort/DetailsModal/5
        public async Task<IActionResult> DetailsModal(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                Cohort output = await _context.Get(id);
                if (output == null)
                {
                    return NotFound();
                }
                return PartialView(output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
            }
            return PartialView();
        }
        #endregion

        #region Create
        // GET: Cohort/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Cohort/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description")] Cohort cohort)
        {
            if (ModelState.IsValid)
            {
                Cohort output = await _context.Create(cohort);
                return RedirectToAction(nameof(Index));
            }
            return View(cohort);
        }
        #endregion

        #region Edit
        // GET: Cohort/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            Cohort output = new Cohort();
            try
            {
                output = await _context.Get(id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            if (output == null)
            {
                return NotFound();
            }

            return View(output);
        }

        // POST: Cohort/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        // RecordSessions is deliberately NOT in this bind list (ROADMAP 22), for the same reason
        // Archive and LockMembers are not: CohortLogic.Update copies only the editable fields onto
        // the stored row, so the policy flag is preserved by never being touched, and keeping it
        // off the form denies an overposting surface for the one field the role-gated toggle owns.
        public async Task<IActionResult> Edit(long id, [Bind("Name,Description,InstructorId,Id,UUID,TimeStamp,LastUpdateTimeStamp")] Cohort cohort)
        {
            if (id != cohort.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    Cohort output = await _context.Update(cohort);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex.Message);
                    StatusMessage = ex.Message;
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(cohort);
        }
        #endregion

        #region Delete

        // HARD delete, restored 2026-08-09 on an owner ruling. It had been commented out since the
        // repo's first commit, and could not simply be uncommented: ICohortLogic declared no Delete,
        // so `_context.Delete(id)` did not compile, and there was no Delete view for it to return.
        //
        // Archive is still the ordinary way to retire a cohort and is unchanged. This is the
        // irreversible one, for a cohort that should never have existed.

        // GET: Cohort/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Cohort output = new Cohort();
            try
            {
                output = await _context.Get(id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return RedirectToAction(nameof(Index));
            }

            if (output == null)
            {
                return NotFound();
            }

            return View(output);
        }

        // POST: Cohort/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            bool output = false;
            try
            {
                output = await _context.Delete(id);
            }
            catch (Exception ex)
            {
                // The DAL rethrows, and the most likely cause is a foreign key this delete does not
                // know about. Say so rather than reporting a silent failure.
                _logger.LogError(ex, "Cohort delete failed for id {CohortId}", id);
                StatusMessage = "The cohort could not be deleted: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }

            StatusMessage = output
                ? "Cohort deleted. Its members, curriculum, location and hardware links were removed; the users, curricula, locations and devices themselves were not."
                : "That cohort no longer exists, or you are not permitted to delete it.";

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Detail partials
        public async Task<IActionResult> LoadMemberList(long Id)
        {
            if (Id == null || Id == 0)
            {

                StatusMessage = "not a valid choice";
                return Ok();
            }

            //variables
            //Cohort output = new Cohort;
            List<CohortMemberViewModel> output = default;
            try
            {
                output = await _memberContext.GetByCohort(Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return BadRequest();
            }
            //return PartialView(output);
            return PartialView("ManageMemberIndexPartial", output);
        }

        public async Task<IActionResult> LoadCurriculumAccessListing(long Id)
        {
            if (Id == null || Id == 0)
            {

                StatusMessage = "not a valid choice";
                return Ok();
            }

            //variables
            //Cohort output = new Cohort;
            CohortAccessListViewModel output = default;
            try
            {

                output = await _context.GetCohortAccessList(Id);//.GetByCohort(Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return BadRequest();
            }
            //return PartialView(output);
            return PartialView("CurriculumAccessListing", output);
        }
        #endregion

        #region Miss        
        //private bool CohortExists(long id)
        //{
        //    return _context.Exists(id);
        //}

        // Audit C-07: was GET-reachable with no antiforgery token, so any page a logged-in admin
        // visited could archive a cohort via an image tag. Now POST + token.
        //
        // It is REWIRED rather than deleted (the earlier "no caller, therefore delete" reading was
        // wrong): CohortLogic.ArchiveToggle is the ONLY code path in the product that can set
        // Cohort.Archive, and cohort deletion is commented out at :220-252, so this is the sole way
        // to retire a finished cohort. The checkbox in IndexPartial.cshtml was rendered `disabled`
        // in every generation of this codebase -- scaffolded and never wired.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveToggle(long Id)
        {
            if (Id == null || Id == 0)
            {
                // ROADMAP 20: was `return Ok()`, so a refusal arrived at the browser looking exactly
                // like a success and the caller announced one. StatusMessage is a redirect-time
                // mechanism and an AJAX caller never sees it.
                StatusMessage = "not a valid choice";
                return BadRequest("not a valid choice");
            }

            //variables
            //Cohort output = new Cohort;
            bool output;
            try
            {
                output = await _context.ArchiveToggle(Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return BadRequest();
            }

            // ROADMAP 20: `output` was assigned and then DISCARDED -- the action returned Ok()
            // regardless, so the browser could not tell a refusal from a success and the caller
            // announced "Cohort archived." either way.
            //
            // FORBID, not NotFound: CohortLogic.ArchiveToggle sets output = true after a successful
            // update and rethrows on any exception (caught above), so the ONLY way to arrive here
            // with false is its authorization filter -- `!IsLocalFebrisAdmin && !IsLocalAdmin &&
            // !IsLocalEducator` returns default. False means "you may not", not "no such cohort",
            // and answering 404 would tell a learner the cohort does not exist when it does.
            if (!output)
            {
                return Forbid();
            }

            return Ok();
        }

        // ROADMAP 22: the educator's recording policy for this cohort. Shaped exactly like
        // ArchiveToggle above -- POST, antiforgery, Forbid on a refusal rather than 404 -- because
        // it is the same kind of decision by the same people.
        //
        // Unlike MemberLockToggle (removed for gating nothing), this flag HAS a read side from the
        // day it ships: LauncherLogic.ShouldRecordSession derives every launch's record decision
        // from it, and no client can override that.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordSessionsToggle(long Id)
        {
            if (Id == 0)
            {
                StatusMessage = "not a valid choice";
                return BadRequest("not a valid choice");
            }

            bool output;
            try
            {
                output = await _context.RecordSessionsToggle(Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return BadRequest();
            }

            // False means "you may not" -- CohortLogic.RecordSessionsToggle returns default only
            // from its role filter and rethrows everything else.
            if (!output)
            {
                return Forbid();
            }

            return Ok();
        }
        // MemberLockToggle was REMOVED 2026-08-05 by owner decision (ROADMAP 19).
        //
        // Cohort.LockMembers was enforced NOWHERE: it had exactly one writer (this action's BLL
        // method), three display-only indicators, and zero readers that gated anything.
        // CohortMemberLogic.AddMember / RemoveMember never consulted it, so a "locked" cohort's
        // roster stayed fully editable. It was a switch that did nothing, and shipping a control
        // that visibly does nothing is worse than not shipping one.
        //
        // Briefly rewired earlier the same day as part of C-07, on the mistaken belief that it was
        // the only implementation of a real capability. It was the only WRITER of a flag with no
        // semantics -- a different thing.
        //
        // The Cohort.LockMembers COLUMN is deliberately left in the database. Migrations here are
        // append-only, so dropping it ships permanently and destroys any stored values, and this
        // way the decision stays reversible if the semantics are ever specified and implemented.
        // Archive is NOT removed, and unlike LockMembers it IS enforced (corrected 2026-08-25,
        // this comment used to say it was unenforced too): CohortQueries.Get() filters `!Archive`,
        // GetIncludingArchived() is the include path, and an archived cohort does not vote on the
        // ROADMAP 22 recording policy. The difference is that "archived" had an obvious meaning and
        // "locked" never did.
        #endregion

        #region Add Cohort Member
        public async Task<IActionResult> ManageMemberIndex(long? id)
        {
            Cohort item = new Cohort();
            try
            {
                item = await _context.Get(id);
                return View(item);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                throw;
            }
        }
        public async Task<IActionResult> ManageMemberIndexPartial(long? id)
        {
            List<CohortMemberViewModel> itemList = new List<CohortMemberViewModel>();
            try
            {
                itemList = await _memberContext.GetByCohort(id);
                return PartialView("ManageMemberIndexPartial", itemList);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                throw;
            }
        }

        public async Task<IActionResult> AccessableMemberIndexPartial(string currentFilter, string searchString, int? page)
        {
            List<LocalUserViewModel> outputSetup = new List<LocalUserViewModel>();
            try
            {
                if (searchString != null)
                {
                    page = 1;
                }
                else
                {
                    searchString = currentFilter;
                }

                if (!string.IsNullOrEmpty(searchString))
                {
                    TempData["CurrentFilter"] = searchString;
                }

                outputSetup = await _userContext.Get(InstitutionUserAccountType.User);

                if (!String.IsNullOrEmpty(searchString))
                {
                    //set up how to search db
                    outputSetup = await outputSetup.Where(b => (b.Role ?? "").ToLower().Contains(searchString.ToLower())
                 || (b.ApplicationUser.FirstName ?? "").ToLower().Contains(searchString.ToLower())
                 || (b.ApplicationUser.LastName ?? "").ToLower().Contains(searchString.ToLower())
                 || (b.ApplicationUser.NormalizedEmail ?? "").ToLower().Contains(searchString.ToLower())
                 || (b.ApplicationUser.PhoneNumber ?? "").ToLower().Contains(searchString.ToLower())
                 || (b.ApplicationUser.UserName.ToString() ?? "").ToLower().Contains(searchString.ToLower())
                 ).ToListAsync();
                }

                int pageNumber = (page ?? 1);
                var output = await outputSetup.ToPagedListAsync(pageNumber, 25);
                return PartialView("AccessableMemberIndexPartial", output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
            }
            return PartialView();
        }

        // Audit C-07: was GET-reachable with no antiforgery token, so any page a logged-in
        // admin visited could trigger it via an image tag. Caller updated to a tokenised POST.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember(long cohortId, Guid userId)
        {
            string response = string.Empty;
            bool output = false;

            try
            {
                CohortMember link = await _memberContext.Create(cohortId, userId);
                output = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
            }

            // ROADMAP 20: the failure path used to return HTTP 200 with the string
            // "No new Item was added", so the caller could not tell it from a success and the
            // Manage*Index page announced one. The only way to reach it is the catch below, since
            // `output` is set unconditionally after Create -- so a failure here IS a server error
            // and is now reported as one. The success body is unchanged.
            if (!output)
            {
                response = "No new Item was added";
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }

            response = "New Item was added";
            return Json(response);
        }

        // Audit C-07: was GET-reachable with no antiforgery token, so any page a logged-in
        // admin visited could trigger it via an image tag. Caller updated to a tokenised POST.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMember(long id)
        {
            string response = string.Empty;
            bool output = false;
            bool threw = false;

            try
            {
                CohortMember link = new CohortMember()
                {
                    Id = id
                };
                output = await _memberContext.Remove(link);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                threw = true;
            }

            // ROADMAP 20: the failure path used to return HTTP 200 with the string
            // "No Item was removed", indistinguishable from a success to the caller. Two different
            // failures are folded together here, and they are now separated: the catch is a server
            // error, while Remove returning false means there was no such link to remove, which is
            // a 404. The success body is unchanged.
            if (!output)
            {
                response = "No Item was removed";
                return threw
                    ? StatusCode(StatusCodes.Status500InternalServerError, response)
                    : NotFound(response);
            }

            response = "Item was removed";
            return Json(response);
        }

        #endregion

    }
}
