// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using X.PagedList;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.ViewModels;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Febris.ModelLibrary.Models.XApiModels.ExtraModels;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;

namespace Febris.UserNode.Portal.Controllers.xAPI
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.FebrisStaff)]
    [Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    public class StatementController : Controller
    {
        //templates MVCControllerwithContext generator used                
        private readonly ILogger<StatementController> _logger;
        private readonly IStatementLogic _context;
        private readonly IStatementVoidingLogic _voidingContext;
        private readonly IStatementDownloadLogic _downloadContext;
        public StatementController(
            IStatementLogic context,
            IStatementVoidingLogic voidingContext,
            IStatementDownloadLogic downloadContext,
            ILogger<StatementController> logger)
        {
            _context = context;
            _voidingContext = voidingContext;
            _downloadContext = downloadContext;
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

        #region Index
        // GET: Statement
        public async Task<IActionResult> Index()
        {
            return View();
        }
        // GET: Statement/IndexPartial
        public async Task<IActionResult> IndexPartial(string currentFilter, string searchString, int? page)
        {
            List<Statement> outputSetup = new List<Statement>();
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

                // These two branches were INVERTED. With no search term it called SearchGet(null),
                // which matches nothing, so the statement list rendered EMPTY in its default state;
                // with a search term it called Get(), which ignores the term and returns everything.
                // Found at runtime on 2026-08-15 while verifying the T5 void button, which is
                // reachable only through a row in this list.
                if (!string.IsNullOrEmpty(searchString))
                {
                    outputSetup = await _context.SearchGet(searchString);
                }
                else
                {
                    outputSetup = await _context.Get();
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
        // GET: Statement/DetailsModal/5
        public async Task<IActionResult> DetailsModal(long id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                Statement output = await _context.Get(id);
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

        #region Download
        /// <summary>
        /// Restores the statement JSON download that existed in the 2021 Portal as
        /// <c>xAPIController.StatementDownloader</c> and was lost in the port.
        ///
        /// <para>
        /// <b>GET, unlike voiding.</b> This reads, it does not mutate, so audit C-07 does not apply
        /// and it deliberately carries no antiforgery token: a download the browser cannot navigate
        /// to is not a download. The gate is the access check in the BLL.
        /// </para>
        ///
        /// <para>
        /// <b>No role attribute, also unlike voiding.</b> Anyone who may VIEW a statement may export
        /// it, because the export discloses exactly what the details modal already shows.
        /// <c>StatementDownloadLogic</c> enforces that through the same per-role filter every other
        /// statement read uses, so a learner gets their own statements and nobody else's. The 2021
        /// version was arranged the same way, with download rendering for educators and admins
        /// while void rendered for staff alone.
        /// </para>
        ///
        /// <para>
        /// A refusal and a missing file both return 404 on purpose, so this cannot be used to probe
        /// which statements exist.
        /// </para>
        /// </summary>
        // GET: Statement/StatementDownload
        [HttpGet]
        public async Task<IActionResult> StatementDownload(Guid statementId)
        {
            try
            {
                byte[] content = await _downloadContext.Get(statementId);
                if (content == null)
                {
                    return NotFound();
                }

                // application/json with a real .json name. The 2021 version served text/plain and
                // had its file extension explicitly commented out, so downloads arrived unnamed and
                // mistyped.
                return File(content, "application/json", statementId + ".json");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                return NotFound();
            }
        }
        #endregion

        #region Voiding
        /// <summary>
        /// T5. Retracts a statement that turns out to be wrong. The statement is never edited and
        /// never deleted: it stops counting, and a voiding statement records the retraction.
        ///
        /// <para>
        /// <b>The BLL is the real gate.</b> <c>StatementVoidingLogic.Void</c> re-checks Admin-and-up
        /// against the caller's claims, refuses an unknown statement, and refuses a second void. The
        /// attributes here are the outer door, not the authority -- an action that trusted its
        /// attributes alone would be one <c>[Authorize]</c> typo away from open.
        /// </para>
        ///
        /// <para>
        /// <b>POST, not GET.</b> The 2021 route this replaces was <c>GET /XAPI/VoidStatement</c>,
        /// which audit C-07 is precisely about: a GET-reachable mutator fires from any page a
        /// logged-in admin visits, via something as small as an image tag. That matters more here
        /// than anywhere else on the node, because voiding is irreversible by design.
        /// </para>
        ///
        /// <para>
        /// The Guid is the xAPI statement UUID, NOT the table's long primary key. Every other action
        /// on this controller takes the key, so the difference is deliberate and easy to get wrong:
        /// the UUID is the identity the xAPI world uses, and it is what the BLL and the voiding
        /// statement's <c>urn:uuid:</c> reference are built on.
        /// </para>
        /// </summary>
        // POST: Statement/VoidStatement
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdminAndItAdmin)]
        public async Task<IActionResult> VoidStatement(Guid statementId)
        {
            try
            {
                bool voided = await _voidingContext.Void(statementId);
                return Json(new { success = voided });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                return Json(new { success = false });
            }
        }
        #endregion

        #region Create
        //// GET: Statement/Create
        //public IActionResult Create()
        //{
        //    return View();
        //}

        //// POST: Statement/Create
        //// To protect from overposting attacks, enable the specific properties you want to bind to, for 
        //// more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Create([Bind("Id,UUID,Timestamp,Stored,VerbId,VerbUUID,ObjectId,ObjectUUID,VersionUUID,VersionId")] LocalStatement localStatement)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        LocalStatement output = await _context.Create(localStatement);
        //        return RedirectToAction(nameof(Index));
        //    }
        //    return View(localStatement);
        //}

        #endregion

        #region Edit
        // DELETED, and it must not come back. Editing a statement is an xAPI spec violation:
        // statements are IMMUTABLE, and that immutability is the entire reason voiding exists.
        // The MVC generator scaffolding that sat here was commented out, and the orphaned Edit
        // button partial that pointed at it was rendered by no view, so between them they
        // described a feature that could never be correct. Removed 2026-08-15 by owner ruling.
        //
        // To RETRACT a wrong statement, void it (see the Voiding region above). To CORRECT one,
        // issue a new statement. Never edit the stored row.
        #endregion

        #region Delete
        // GET: Statement/Delete/5
        //public async Task<IActionResult> Delete(long? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }

        //    LocalStatement output = new LocalStatement;//();
        //    try
        //    {
        //        output = await _context.Get(id);                
        //    }
        //    catch(Exception ex)
        //    {
        //        _logger.LogWarning(ex.Message);
        //        StatusMessage = ex.Message;
        //        return RedirectToAction(nameof(Index));
        //    }          
        //    if (output == null)
        //    {
        //        return NotFound();
        //    }

        //    return View(output);
        //}

        //// POST: Statement/Delete/5
        //[HttpPost, ActionName("Delete")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> DeleteConfirmed(long id)
        //{
        //    bool output = await _context.Delete(id);
        //    if(output){
        //        //TempData["StatusMessage"] = "Item was deleted successfully";
        //        StatusMessage = "Item was deleted successfully";
        //    }else{
        //        //TempData["StatusMessage"] = "Item was not deleted";
        //        StatusMessage = "Item was not deleted";
        //    }

        //    return RedirectToAction(nameof(Index));
        //}
        #endregion

        #region Miss        
        //private bool LocalStatementExists(long id)
        //{
        //    return _context.Exists(id);
        //}

        //public async Task<IActionResult> ToggleLockOut(long Id, bool lockedout)
        //{
        //    if (Id == null || Id == 0)
        //    {
        //        //TempData["StatusMessage"] = "not a valid choice";
        //        StatusMessage = "not a valid choice";
        //        return Ok();
        //    }

        //    //variables
        //    //LocalStatement output = new LocalStatement;
        //    bool output;
        //    try
        //    {
        //        output = await _context.LockOut(Id);
        //    }
        //    catch(Exception ex)
        //    {
        //        _logger.LogWarning(ex.Message);
        //        //TempData["StatusMessage"] = ex.Message;
        //        StatusMessage = ex.Message;
        //        //throw;
        //        return BadRequest();
        //    }
        //    return Ok();
        //}
        #endregion


        #region Widgets

        public async Task<IActionResult> LoadStatementExtras(Statement input)
        {
            XApiResultExtrasViewModel output = new XApiResultExtrasViewModel();            
            try
            {
                
                output = await _context.GetExtras(input);

                return PartialView("../Statement/_StatementExtrasPartial", output);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                throw;
                //return null;
            }
        }

        #endregion             

    }
}
