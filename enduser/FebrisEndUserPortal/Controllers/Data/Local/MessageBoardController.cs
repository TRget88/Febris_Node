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
using Febris.PrimaryLogicLayer.Logic.DataLogic;
using Microsoft.AspNetCore.Authorization;

namespace Febris.UserNode.Portal.Controllers.Data.Local
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    [Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]
    //[Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]
    public class MessageBoardController : Controller
    {
        //templates MVCControllerwithContext generator used                
        private readonly ILogger<MessageBoardController> _logger;
        private readonly IMessageBoardLogic _context;
        public MessageBoardController(
            IMessageBoardLogic context,
            ILogger<MessageBoardController> logger
            )
        {
            _context = context;
            _logger = logger;
        }

        //[TempData]
        //public string StatusMessage { get; set; }        
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
        // GET: MessageBoard
        public async Task<IActionResult> Index()
        {
            return View();
        }
        // GET: MessageBoard/IndexPartial
        public async Task<IActionResult> IndexPartial(string currentFilter, string searchString, int? page)
        {
            List<MessageBoard> outputSetup = new List<MessageBoard>();
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

                outputSetup = await _context.Get();

                if (!String.IsNullOrEmpty(searchString))
                {
                    outputSetup = await outputSetup.Where(b => (b.Subject ?? "").ToLower().Contains(searchString.ToLower())
                  || (b.Message ?? "").ToLower().Contains(searchString.ToLower())
                  || (b.TimeStamp.ToShortDateString() ?? "").ToLower().Contains(searchString.ToLower())
                  ).ToListAsync();
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

        // GET: MessageBoard
        public async Task<IActionResult> MessageIndex()
        {
            return View();
        }
        // GET: MessageBoard/IndexPartial
        public async Task<IActionResult> MessageIndexPartial(string currentFilter, string searchString, int? page)
        {
            List<MessageBoard> outputSetup = new List<MessageBoard>();
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

                outputSetup = await _context.GetActive();

                if (!String.IsNullOrEmpty(searchString))
                {
                    //set up how to search db
                    outputSetup = await outputSetup.Where(b => (b.Subject ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.Message ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.TimeStamp.ToShortDateString() ?? "").ToLower().Contains(searchString.ToLower())                  
                    ).ToListAsync();
                }

                int pageNumber = (page ?? 1);
                var output = await outputSetup.ToPagedListAsync(pageNumber, 25);
                return PartialView("MessageIndexPartial", output);
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
        // GET: MessageBoard/Details/5
        //public async Task<IActionResult> Details(long? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }            
        //    try
        //    {
        //        MessageBoard output = await _context.Get(id);
        //         if (output == null)
        //        {
        //            return NotFound();
        //        }
        //        return View(output);
        //    }
        //    catch(Exception ex)
        //    {
        //        _logger.LogWarning(ex.Message);
        //        StatusMessage = ex.Message;

        //    }
        //    return View();            
        //}

        // GET: MessageBoard/DetailsModal/5
        public async Task<IActionResult> DetailsModal(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                MessageBoard output = await _context.Get(id);
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
        // GET: MessageBoard/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: MessageBoard/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Subject,Message,TimeStamp,LastUpdateTimeStamp")] MessageBoard messageBoard)
        {
            if (ModelState.IsValid)
            {
                MessageBoard output = await _context.Create(messageBoard);
                return RedirectToAction(nameof(Index));
            }
            return View(messageBoard);
        }
        #endregion

        #region Edit
        // GET: MessageBoard/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            MessageBoard output = new MessageBoard();
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

        // POST: MessageBoard/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Id,UUID,TimeStamp,LastUpdateTimeStamp,Archive,Subject,Message")] MessageBoard messageBoard)
        {
            if (id != messageBoard.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    MessageBoard output = await _context.Update(messageBoard);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex.Message);
                    StatusMessage = ex.Message;
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(messageBoard);
        }
        #endregion

        #region Delete
        // GET: MessageBoard/Delete/5
        //public async Task<IActionResult> Delete(long? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }

        //    MessageBoard output = new MessageBoard;//();
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

        //// POST: MessageBoard/Delete/5
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

        // Audit C-07: was GET-reachable with no antiforgery token, so any page a logged-in
        // admin visited could trigger it via an image tag. Caller updated to a tokenised POST.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleArchive(long Id)
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
            //MessageBoard output = new MessageBoard;
            bool output;
            try
            {
                output = await _context.ToggleArchive(Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return BadRequest();
            }

            // ROADMAP 20: `output` was assigned and then DISCARDED -- the action returned Ok()
            // regardless, so the caller announced "Archive state changed." whatever happened.
            //
            // Honouring the boolean required FIXING THE BLL FIRST. MessageBoardLogic.ToggleArchive
            // declared `output`, never assigned it, and returned it, so it reported failure on every
            // call including the successful ones. This action discarding the result is the only
            // reason the feature worked at all, and reading it without that fix would have turned
            // every archive click into a 404. See the note in MessageBoardLogic.
            //
            // NotFound rather than Forbid, unlike the cohort twin: this method has no authorization
            // filter, and now that the BLL sets output = true after its update and rethrows on
            // exception, false is unreachable. Kept as defence in depth rather than deleted, because
            // an unreachable branch that fails closed is cheaper than the one this replaced.
            if (!output)
            {
                return NotFound();
            }

            return Ok();
        }
        #endregion


    }
}
