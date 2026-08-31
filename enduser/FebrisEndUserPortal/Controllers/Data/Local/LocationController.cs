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

namespace Febris.UserNode.Portal.Controllers.Data
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    [Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]
    public class LocationController : Controller
    {
        //templates MVCControllerwithContext generator used                
        private readonly ILogger<LocationController> _logger;
        private readonly ILocationLogic _context;
        public LocationController(
            ILocationLogic context,
            ILogger<LocationController> logger)
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

        #region Index
        // GET: Location
        public async Task<IActionResult> Index()
        {
            return View();
        }
        // GET: Location/IndexPartial
        public async Task<IActionResult> IndexPartial(string currentFilter, string searchString, int? page)
        {
            List<Location> outputSetup = new List<Location>();
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
                    || (b.City ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.Country ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.ZipCode.ToString() ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.State ?? "").ToLower().Contains(searchString.ToLower())                    
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
        // GET: Location/Details/5
        //public async Task<IActionResult> Details(long? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }
        //    try
        //    {
        //        Location output = await _context.Get(id);
        //        if (output == null)
        //        {
        //            return NotFound();
        //        }
        //        return View(output);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogWarning(ex.Message);
        //        StatusMessage = ex.Message;

        //    }
        //    return View();
        //}

        // GET: Location/DetailsModal/5
        public async Task<IActionResult> DetailsModal(long id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                Location output = await _context.Get(id);
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
        // GET: Location/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Location/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UUID,TimeStamp,LastUpdateTimeStamp,Name,Address,City,ZipCode,State,Country")] Location location)
        {
            if (ModelState.IsValid)
            {
                Location output = await _context.Create(location);
                return RedirectToAction(nameof(Index));
            }
            return View(location);
        }
        #endregion

        #region Edit
        // GET: Location/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            Location output = new Location();            
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

        // POST: Location/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Name,Address,City,ZipCode,State,Country,Longitude,Latitude,Id,UUID")] Location location)
        {
            if (id != location.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    Location output = await _context.Update(location);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex.Message);
                    StatusMessage = ex.Message;
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(location);
        }
        #endregion

        #region Delete
        // GET: Location/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Location output = new Location();
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

        // POST: Location/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            bool output = await _context.Delete(id);
            if (output)
            {
                //TempData["StatusMessage"] = "Item was deleted successfully";
                StatusMessage = "Item was deleted successfully";
            }
            else
            {
                //TempData["StatusMessage"] = "Item was not deleted";
                StatusMessage = "Item was not deleted";
            }

            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Miss        
        //private bool LocationExists(long id)
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
        //    //Location output = new Location;
        //    bool output;
        //    try
        //    {
        //        output = await _context.LockOut(Id);
        //    }
        //    catch (Exception ex)
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


    }
}
