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
using Microsoft.AspNetCore.Authorization;

namespace Febris.UserNode.Portal.Controllers.Data
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    [Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]
    public class CohortMemberController : Controller
    {
        //templates MVCControllerwithContext generator used                
        private readonly ILogger<CohortMemberController> _logger;
        private readonly ICohortMemberLogic _context;
        public CohortMemberController(
            ICohortMemberLogic context,
            ILogger<CohortMemberController> logger
            )
        {
            _context = context;
            _logger = logger;
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


        #region Details       

        // GET: CohortMember/DetailsModal/5
        public async Task<IActionResult> DetailsModal(long id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                CohortMemberViewModel output = await _context.Get(id);
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
               
        // Audit C-07: RemoveMember(Guid) and AddMember(Guid, Guid) were REMOVED here 2026-08-05.
        //
        // Both were state-mutating, GET-reachable and carried no antiforgery token. Unlike the two
        // Cohort toggles -- which were also unreachable but were the ONLY implementation of their
        // capability and so were rewired -- these were genuinely REDUNDANT: the live pair on
        // CohortController (AddMember :458 / RemoveMember :483, reached from
        // Views/Cohort/ManageMemberIndex.cshtml:46 and :57) writes the same CohortMember rows via
        // the same BLL methods. The differing identifier types were not a capability difference:
        // Id and UUID are paired unique columns on the same rows through BaseModel.
        //
        // Nothing referenced them -- no view, no script, no button partial -- in this repo, in the
        // the pre-v4 reference checkout, or in either repo's history. Deleted rather than
        // converted, because giving an uncallable action [HttpPost] just preserves dead code behind
        // a token. CohortMemberLogic.AddMember / RemoveMember are RETAINED: the live pair uses them.

        #region Detail Partials       

        // GET: CohortMember/DetailsModal/5
        public async Task<IActionResult> LoadCohortList(long id)
        {
            if (id == null && id==0)
            {
                return NotFound();
            }
            try
            {
                CohortMemberViewModel member = await _context.Get(id);
                List<CohortMember> preoutput = await  _context.GetCohortsByMember(member.CohortMember.UserId);
                if (preoutput == null)
                {
                    return NotFound();
                }
                List<Cohort> output = preoutput.Select(i => i.Cohort).ToList();
                return PartialView("../Widget/_CohortIndexPartial", output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
            }
            return PartialView();
        }

        // REMOVED 2026-08-18, node remote teardown Phase 1.9.
        //
        // LoadPurchaseList redirected to `nameof(Purchase)`, a controller that lives in the CENTRAL
        // tier and was removed from the node when it stopped calling central commerce. The redirect
        // still COMPILED, because `nameof(Purchase)` binds to the entity type via the
        // Febris.ModelLibrary.Models.DataModels using at the top of this file, so nothing failed at
        // build time and the only symptom was a 404 at runtime.
        //
        // That made it exactly what NODE_REMOTE_TEARDOWN_PLAN.md:18 predicted: "the easiest defect
        // to ship by accident". It was shipped. CohortMember/DetailsModal IS reachable by ordinary
        // navigation, so every operator who opened a cohort member got a permanent spinner in the
        // widget slot that called this. The slot is removed with it (plan 1.10).
        //
        // This is NOT the cohort membership feature. Add/remove members moved to CohortController
        // (AddMember, RemoveMember) and is reached from Cohort/ManageMemberIndex, which works.

        #endregion

    }
}
