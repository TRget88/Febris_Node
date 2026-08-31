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
using Febris.ModelLibrary.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.UserNode.LogicLayer.Logic.DataLogic;

namespace Febris.UserNode.Portal.Controllers.Data
{
    #region Generic controller
    //    public class TestUserController : Controller
    //    {
    //        //templates MVCControllerwithContext generator used                
    //        private readonly ILogger<TestUserController> _logger;        
    //        private readonly ITestUserLogic _context;        
    //        public TestUserController(TestUserLogic context, ILogger<TestUserController> logger)
    //        {
    //            _context = context;
    //            _logger = logger;
    //        }

    //        //[TempData]
    //        //public string StatusMessage { get; set; }        
    //        [TempData]
    //        private string StatusMessage
    //        {
    //            get
    //            {
    //                return StatusMessage;
    //            }
    //            set
    //            {
    //                TempData["StatusMessage"] = value;
    //                return;
    //            }
    //        }

    //#region Index
    //        // GET: TestUser
    //        public async Task<IActionResult> Index()
    //        {
    //            return View();
    //       }
    //        // GET: TestUser/IndexPartial
    //        public async Task<IActionResult> IndexPartial(string currentFilter, string searchString, int? page)
    //        {   
    //            List<TestUser> outputSetup = new List<TestUser>();
    //            try
    //            {
    //                if (searchString != null)
    //                {
    //                    page = 1;
    //                }
    //                else
    //                {
    //                    searchString = currentFilter;
    //                }

    //                if (!string.IsNullOrEmpty(searchString))
    //                {
    //                    TempData["CurrentFilter"] = searchString;
    //                }

    //                outputSetup = await _context.Get();        

    //                if (!String.IsNullOrEmpty(searchString))
    //                {
    //                    //set up how to search db
    //                    //outputSetup = await outputSetup.Where(b => (b.AccreditationBody.Name ?? "").ToLower().Contains(searchString.ToLower())
    //                                            //|| (b.AccreditationBody.Description ?? "").ToLower().Contains(searchString.ToLower())
    //                                            //|| (b.ContentDeveloper.Name ?? "").ToLower().Contains(searchString.ToLower())
    //                                            //|| (b.ContentDeveloper.ZipCode.ToString() ?? "").ToLower().Contains(searchString.ToLower())
    //                                            //|| (b.ContentDeveloper.State ?? "").ToLower().Contains(searchString.ToLower())
    //                                            //|| (b.ContentDeveloper.ContentDeveloperType.ToString() ?? "").ToLower().Contains(searchString.ToLower())
    //                                            //|| (b.ContentDeveloper.Address ?? "").ToLower().Contains(searchString.ToLower())
    //                                            //).ToListAsync();
    //                }

    //                int pageNumber = (page ?? 1);
    //                var output = await outputSetup.ToPagedListAsync(pageNumber, 25);
    //                return PartialView("IndexPartial", output);
    //            }
    //            catch(Exception ex)
    //            {
    //                _logger.LogWarning(ex.Message);
    //                StatusMessage = ex.Message;                
    //            }            
    //            return PartialView();   
    //        }
    //#endregion

    //#region Details
    //        // GET: TestUser/Details/5
    //        public async Task<IActionResult> Details(long? id)
    //        {
    //            if (id == null)
    //            {
    //                return NotFound();
    //            }            
    //            try
    //            {
    //                TestUser output = await _context.Get(id);
    //                 if (output == null)
    //                {
    //                    return NotFound();
    //                }
    //                return View(output);
    //            }
    //            catch(Exception ex)
    //            {
    //                _logger.LogWarning(ex.Message);
    //                StatusMessage = ex.Message;

    //            }
    //            return View();            
    //        }

    //        // GET: TestUser/DetailsModal/5
    //        public async Task<IActionResult> DetailsModal(long id)
    //        {
    //            if (id == null)
    //            {
    //                return NotFound();
    //            }            
    //            try
    //            {
    //                TestUser output = await _context.Get(id);
    //                if (output == null)
    //                {
    //                    return NotFound();
    //                }
    //                return PartialView(output);
    //            }
    //            catch(Exception ex)
    //            {
    //                _logger.LogWarning(ex.Message);
    //                StatusMessage = ex.Message;                
    //            }
    //            return PartialView();
    //        }    
    //#endregion

    //#region Create
    //        // GET: TestUser/Create
    //        public IActionResult Create()
    //        {
    //            return View();
    //        }

    //        // POST: TestUser/Create
    //        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
    //        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    //        [HttpPost]
    //        [ValidateAntiForgeryToken]
    //        public async Task<IActionResult> Create([Bind("UserName,FirstName,LastName,IdentificationNumber,PhotoOfProfessional,PhoneNumber,ActorId,EmailAddress,Id,UUID,TimeStamp,LastUpdateTimeStamp")] TestUser testUser)
    //        {
    //            if (ModelState.IsValid)
    //            {
    //                TestUser output = await _context.Create(testUser);
    //                return RedirectToAction(nameof(Index));
    //            }
    //            return View(testUser);
    //        }
    //#endregion

    //#region Edit
    //        // GET: TestUser/Edit/5
    //        public async Task<IActionResult> Edit(long? id)
    //        {
    //            if (id == null)
    //            {
    //                return NotFound();
    //            }
    //            TestUser output = new TestUser();            
    //            try
    //            {
    //                output = await _context.Get(id);                
    //            }
    //            catch(Exception ex)
    //            {
    //                _logger.LogWarning(ex.Message);
    //                StatusMessage = ex.Message;
    //                return RedirectToAction(nameof(Index));
    //            }
    //            if (output == null)
    //            {
    //                return NotFound();
    //            }

    //            return View(output);
    //        }

    //        // POST: TestUser/Edit/5
    //        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
    //        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    //        [HttpPost]
    //        [ValidateAntiForgeryToken]
    //        public async Task<IActionResult> Edit(long id, [Bind("UserName,FirstName,LastName,IdentificationNumber,PhotoOfProfessional,PhoneNumber,ActorId,EmailAddress,Id,UUID,TimeStamp,LastUpdateTimeStamp")] TestUser testUser)
    //        {
    //            if (id != testUser.Id)
    //            {
    //                return NotFound();
    //            }

    //            if (ModelState.IsValid)
    //            {
    //                try
    //                {
    //                    TestUser output = await _context.Update(testUser);                    
    //                }
    //                catch(Exception ex)
    //                {
    //                    _logger.LogWarning(ex.Message);                    
    //                    StatusMessage = ex.Message;
    //                    throw;
    //                }                 
    //                return RedirectToAction(nameof(Index));
    //            }
    //            return View(testUser);
    //        }
    //#endregion

    //#region Delete
    //         // GET: TestUser/Delete/5
    //        public async Task<IActionResult> Delete(long? id)
    //        {
    //            if (id == null)
    //            {
    //                return NotFound();
    //            }

    //            TestUser output = new TestUser();
    //            try
    //            {
    //                output = await _context.Get(id);                
    //            }
    //            catch(Exception ex)
    //            {
    //                _logger.LogWarning(ex.Message);
    //                StatusMessage = ex.Message;
    //                return RedirectToAction(nameof(Index));
    //            }          
    //            if (output == null)
    //            {
    //                return NotFound();
    //            }

    //            return View(output);
    //        }

    //        // POST: TestUser/Delete/5
    //        [HttpPost, ActionName("Delete")]
    //        [ValidateAntiForgeryToken]
    //        public async Task<IActionResult> DeleteConfirmed(long id)
    //        {
    //            bool output = await _context.Delete(id);
    //            if(output){
    //                //TempData["StatusMessage"] = "Item was deleted successfully";
    //                StatusMessage = "Item was deleted successfully";
    //            }else{
    //                //TempData["StatusMessage"] = "Item was not deleted";
    //                StatusMessage = "Item was not deleted";
    //            }

    //            return RedirectToAction(nameof(Index));
    //        }
    //#endregion

    //#region Miss        
    //        //private bool TestUserExists(long id)
    //        //{
    //        //    return _context.Exists(id);
    //        //}

    //        public async Task<IActionResult> ToggleLockOut(long Id, bool lockedout)
    //        {
    //            if (Id == null || Id == 0)
    //            {
    //                //TempData["StatusMessage"] = "not a valid choice";
    //                StatusMessage = "not a valid choice";
    //                return Ok();
    //            }

    //            //variables
    //            //TestUser output = new TestUser;
    //            bool output;
    //            try
    //            {
    //                output = await _context.LockOut(Id);
    //            }
    //            catch(Exception ex)
    //            {
    //                _logger.LogWarning(ex.Message);
    //                //TempData["StatusMessage"] = ex.Message;
    //                StatusMessage = ex.Message;
    //                //throw;
    //                return BadRequest();
    //            }
    //            return Ok();
    //        }
    //        #endregion


    //    }
    #endregion
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    [Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]    
    public class TestUserController : Controller
    {
        //templates MVCControllerwithContext generator used                
        private readonly ILogger<TestUserController> _logger;
        private readonly ITestUserLogic _context;
        //private readonly ITestUserLinkedFebrisLogic _professionalsLinkedToFebrisContext;
        //private readonly ITestUserLinkedContentDeveloperLogic _linkedContentDevContext;
        //private readonly ITestUserLinkedAccreditationBodyLogic _linkedAccredBodyContext;

        public TestUserController(
            ITestUserLogic context,
            ILogger<TestUserController> logger//,
                                                  //ITestUserLinkedFebrisLogic professionalsLinkedToFebrisContext,
                                                  //ITestUserLinkedContentDeveloperLogic linkedContentDevContext,
                                                  //ITestUserLinkedAccreditationBodyLogic linkedAccredBodyContext
            )
        {
            _context = context;
            _logger = logger;
            //_professionalsLinkedToFebrisContext = professionalsLinkedToFebrisContext;
            //_linkedAccredBodyContext = linkedAccredBodyContext;
            //_linkedContentDevContext = linkedContentDevContext;

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
        // GET: TestUser
        public async Task<IActionResult> Index()
        {
            return View();
        }
        // GET: TestUser/IndexPartial
        public async Task<IActionResult> IndexPartial(string currentFilter, string searchString, int? page)
        {
            List<TestUser> outputSetup = new List<TestUser>();
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
                    outputSetup = await outputSetup.Where(b => (b.UserName ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.TimeStamp.ToString() ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.LastName ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.FirstName ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.EmailAddress ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.PhoneNumber ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.IdentificationNumber ?? "").ToLower().Contains(searchString.ToLower())
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
        // GET: TestUser/DetailsModal/5
        public async Task<IActionResult> DetailsModal(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                TestUser output = await _context.Get(id);
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
        public async Task<IActionResult> CreateTestUser()
        {
            try
            {
                bool created = false;
                (created, StatusMessage) = await _context.Create();
                if (!created)
                {
                    return BadRequest();
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                StatusMessage = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }



        #endregion



        #region Miss        
        //private bool TestUserExists(long id)
        //{
        //    return _context.Exists(id);
        //}

        //public async Task<IActionResult> ToggleLockOut(long Id, bool lockedout)
        //{
        //    if (Id == null || Id == 0)
        //    {
        //        StatusMessage = "not a valid choice";
        //        return Ok();
        //    }

        //    //variables
        //    //TestUser output = new TestUser;
        //    bool output;
        //    try
        //    {
        //        output = await _context.LockOut(Id);
        //    }
        //    catch(Exception ex)
        //    {
        //        _logger.LogWarning(ex.Message);
        //        StatusMessage = ex.Message;
        //        //throw;
        //        return BadRequest();
        //    }
        //    return Ok();
        //}
        #endregion

        public async Task<IActionResult> LoadActorInfo(TestUser input)
        {
            try
            {
                Actor output = await _context.GetActor(input);

                //return RedirectToAction("../Widget/_LineGraphPartial", output);
                return PartialView("../Actor/ActorDataPartial", output);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                throw;
                //return null;
            }
        }

    }
}
