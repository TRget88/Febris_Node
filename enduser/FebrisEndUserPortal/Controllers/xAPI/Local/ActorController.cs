// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.ViewModels;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using X.PagedList;

namespace Febris.UserNode.Portal.Controllers.xAPI
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.FebrisStaff)]
    [Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    public class ActorController : Controller
    {
        //templates MVCControllerwithContext generator used                
        private readonly ILogger<ActorController> _logger;
        private readonly IActorLogic _context;
        private readonly IStatementLogic _statementContext;
        public ActorController(
            IActorLogic context,
            IStatementLogic statementContext,
            ILogger<ActorController> logger
            )
        {
            _context = context;
            _statementContext = statementContext;
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
        // GET: Actor
        [Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]
        public async Task<IActionResult> Index()
        {
            return View();
        }
        // GET: Actor/IndexPartial
        [Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]
        public async Task<IActionResult> IndexPartial(string currentFilter, string searchString, int? page)
        {
            List<Actor> outputSetup = new List<Actor>();
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
                    //set up how to search db
                    outputSetup = await outputSetup.Where(b => (b.Name ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.Mbox_sha1sum ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.ObjectType ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b..ToString() ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.State ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.ContentDeveloperType.ToString() ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.Address ?? "").ToLower().Contains(searchString.ToLower())
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
        #endregion


        #region Details
        // GET: Actor/DetailsModal/5
        public async Task<IActionResult> DetailsModal(long id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                Actor output = await _context.Get(id);
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
        //// GET: Actor/Create
        //public IActionResult Create()
        //{
        //    return View();
        //}

        //// POST: Actor/Create
        //// To protect from overposting attacks, enable the specific properties you want to bind to, for 
        //// more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Create([Bind("Id,UUID,ObjectType,Name,Mbox,Mbox_sha1sum,OpenId")] Actor actor)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        Actor output = await _context.Create(actor);
        //        return RedirectToAction(nameof(Index));
        //    }
        //    return View(actor);
        //}
        #endregion



        #region Miss        

        //public async Task<IActionResult> ToggleLockOut(long Id, bool lockedout)
        //{
        //    if (Id == null || Id == 0)
        //    {
        //        //TempData["StatusMessage"] = "not a valid choice";
        //        StatusMessage = "not a valid choice";
        //        return Ok();
        //    }

        //    //variables
        //    //Actor output = new Actor;
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

        #region Actual Charts
        public async Task<IActionResult> LoadActorStatementChart(Actor input)
        {
            LineChart output = new LineChart();            
            try
            {
                DateTime start = DateTime.UtcNow.AddDays(-30).Date;
                DateTime end = DateTime.UtcNow.Date;
                output = await _statementContext.GetStatementCountDataByActor(input,start,end);
                return PartialView("../Widget/_LineGraphPartial", output);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                throw;
                //return null;
            }
        }
        public async Task<IActionResult> LoadActorTimeChart(Actor input)
        {
            BarChart output = new BarChart();            
            try
            {
                DateTime start = DateTime.UtcNow.AddDays(-30).Date;
                DateTime end = DateTime.UtcNow.Date;
                output = await _statementContext.GetStatementTimeDataByActor(input,start,end);
                return PartialView("../Widget/_BarChartPartial", output);
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
