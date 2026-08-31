// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using X.PagedList;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Febris.ModelLibrary.LookupModels;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.SharedServices;

namespace Febris.UserNode.Portal.Controllers.Data
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]    
    [Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]
    public class HardwareController : Controller
    {
        //templates MVCControllerwithContext generator used                
        private readonly ILogger<HardwareController> _logger;
        private readonly IHardwareLogic _context;
        private readonly IModuleLogic _modContext;
        private readonly IHardwareLinkedModuleLogic _moduleLinkedContext;
        private readonly IHardwareLinkedCohortLogic _cohortLinkedContext;
        private readonly ICohortLogic _cohortContext;
        // The device row lives in DataDb and the statements in XApiDb, so the detail screen composes
        // two BLLs rather than joining. Injecting the LOGIC, never the queries, per the layering rule.
        private readonly Febris.PrimaryLogicLayer.Logic.XApiLogic.IStatementLogic _statementContext;
        private readonly IRecordingLogic _recordingContext;

        public HardwareController(
            IHardwareLogic context,
            IModuleLogic modcontext,
            ILogger<HardwareController> logger,
            IHardwareLinkedModuleLogic linkedContext,
            IHardwareLinkedCohortLogic cohortLinkedContext,
            ICohortLogic cohortContext,
            Febris.PrimaryLogicLayer.Logic.XApiLogic.IStatementLogic statementContext,
            IRecordingLogic recordingContext
            )
        {
            _context = context;
            _logger = logger;
            _modContext = modcontext;
            _moduleLinkedContext = linkedContext;
            _cohortContext = cohortContext;
            _cohortLinkedContext = cohortLinkedContext;
            _statementContext = statementContext;
            _recordingContext = recordingContext;
            //User.IsLocalAdmin();
        }


        [TempData]
        private string StatusMessage
        {
            get
            {
                // The getter used to be `return StatusMessage;` -- itself, unconditionally. Reading
                // it recursed until the stack overflowed, which takes the PROCESS down rather than
                // throwing something a catch block could handle. It survived because every use site
                // only ever WRITES it and the view reads TempData directly, so the getter had no
                // caller to expose it.
                //
                // The owner ruling was to leave this until the class was touched for another reason.
                // That is now, so it reads the backing store the setter actually writes.
                return TempData["StatusMessage"] as string;
            }
            set
            {
                TempData["StatusMessage"] = value;
                return;
            }
        }

        #region Index
        // GET: Hardware
        public async Task<IActionResult> Index()
        {
            return View();
        }
        // GET: Hardware/IndexPartial
        public async Task<IActionResult> IndexPartial(string currentFilter, string searchString, int? page)
        {
            List<LocalHardware> outputSetup = new List<LocalHardware>();
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
                    // Was b.HardwareType.Name/.Description, a navigation LocalHardware does not
                    // carry -- it held only an unenforced id into a lookup the node no longer has.
                    // Search the kind instead. Spaces are stripped from the term so an operator
                    // typing "Laptop PC" still matches the member name LaptopPC.
                    string kindTerm = searchString.Replace(" ", string.Empty).ToLower();
                    outputSetup = await outputSetup.Where(b => (b.HardwareKind.ToString() ?? "").ToLower().Contains(kindTerm)
                    || (b.HardwareCondition.ToString() ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.PhysicalLicense ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.DescriptiveName ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.Description ?? "").ToLower().Contains(searchString.ToLower())
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
        // GET: Hardware/DetailsModal/5
        public async Task<IActionResult> DetailsModal(long id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                LocalHardware hardware = await _context.Get(id);
                if (hardware == null)
                {
                    return NotFound();
                }

                LocalHardwareDetailsViewModel output = new LocalHardwareDetailsViewModel
                {
                    Hardware = hardware
                };

                // The attribution trail. This is the READ SIDE of SubmittedByHardwareUUID, which
                // shipped as a column with two writers and no reader at all -- so the property it
                // was added for, that a forged record is investigable rather than indistinguishable,
                // could until now only be exercised with direct database access.
                //
                // A SEPARATE READ against a SEPARATE DATABASE (XApiDb, where the device row is in
                // DataDb), so it is composed here rather than joined. Wrapped on its own because a
                // statement-store problem must not take down the device detail screen: the device
                // half is what an operator needs to lock or regenerate, which is exactly what they
                // are trying to do during the incident that makes this panel interesting.
                try
                {
                    output.Submissions = await _statementContext.GetSubmissionsByDevice(
                        hardware.UUID,
                        Febris.PrimaryLogicLayer.Logic.XApiLogic.StatementLogic.DefaultSubmissionPageSize);
                }
                catch (Exception statementEx)
                {
                    _logger.LogWarning(statementEx.Message);
                    output.Submissions = null;
                }

                // Video this device minted, per the ownership ruling of 2026-08-18. The actor on a
                // recording is claimed by whoever called the launcher and is never checked against
                // the caller; the hardware is proven. Surfacing the proven half is the attribution
                // half of that ruling. Nothing here refuses or hides a recording.
                //
                // Separately wrapped for the same reason as the statements above: a failure in one
                // store must not take down the device screen an operator uses to lock the device.
                try
                {
                    output.Recordings = await _recordingContext.GetRecordingsByDevice(
                        hardware.UUID,
                        Febris.UserNode.LogicLayer.Logic.DataLogic.RecordingLogic.DefaultRecordingPageSize);
                }
                catch (Exception recordingEx)
                {
                    _logger.LogWarning(recordingEx.Message);
                    output.Recordings = null;
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
        // GET: Hardware/Create
        public async Task<IActionResult> Create()
        {
            // No lookup round trip. The kind dropdown renders straight off the enum in the
            // view, so registration no longer depends on the HardwareType table being seeded.
            // This used to THROW out of a GET if the lookup failed, taking the whole screen down.
            LocalHardwareCreationViewModel output = await _context.CreationPreperation();
            return View(output);
        }

        // POST: Hardware/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Hardware")] LocalHardwareCreationViewModel input)
        {
            if (ModelState.IsValid)
            {
                (LocalHardware output, string credential) = await _context.Create(input);

                // SHOWN ONCE. Only the hash is stored (audit T9), so this is the only moment the
                // credential exists in readable form anywhere. TempData carries it across the
                // redirect and is consumed on display -- a refresh will not bring it back, which is
                // the intended behaviour and is stated in the message itself.
                StatusMessage =
                    "Device registered. Its credential is shown ONCE, now: " + credential +
                    " -- copy it into the device before leaving this page. It is stored only as a hash " +
                    "and cannot be retrieved later; if it is lost, use Regenerate Credential to issue a new one.";

                return RedirectToAction(nameof(Index));
            }
            return View(input);
        }

        // POST: Hardware/RegenerateCredential/5
        //
        // The recovery path for a lost credential, and the only one: the stored hash cannot be
        // reversed. POST-only and antiforgery-validated because it is destructive -- it invalidates
        // the device's current credential immediately and the device stops authenticating until the
        // new one is entered on it.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateCredential(long id)
        {
            try
            {
                string credential = await _context.RegenerateCredential(id);

                StatusMessage = credential == null
                    ? "That device no longer exists, or you are not permitted to regenerate its credential."
                    : "NEW credential issued, shown ONCE, now: " + credential +
                      " -- the device's previous credential is now invalid and it will not authenticate " +
                      "until this value is entered on it.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Regenerating the credential failed for hardware id {HardwareId}", id);
                StatusMessage = "The credential could not be regenerated: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Edit
        // GET: Hardware/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            LocalHardwareCreationViewModel output = new LocalHardwareCreationViewModel();

            try
            {
                // No select list and no selected-UUID round trip. The old line dereferenced
                // HardwareTypeUUID.Value unguarded, which threw for any device whose carrier was
                // null, and nothing enforced that it was not.
                output.Hardware = await _context.Get(id);
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

        // POST: Hardware/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Hardware")] LocalHardwareCreationViewModel input)
        {
            if (id != input.Hardware.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    LocalHardware output = await _context.Update(input);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex.Message);
                    StatusMessage = ex.Message;
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(input);
        }
        #endregion


        #region module management - manage the modules that will be on specific hardware
        public async Task<IActionResult> ManageModuleIndex(long? id)
        {
            LocalHardware hardware = null;
            try
            {
                hardware = await _context.Get(id);
                return View(hardware);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                throw;                
            }
        }
        public async Task<IActionResult> ManageModuleIndexPartial(long? id)
        {
            List<LocalHardwareLinkedModuleViewModel> moduleList = new List<LocalHardwareLinkedModuleViewModel>();
            try
            {
                moduleList = await _moduleLinkedContext.GetByHardware(id);
                return PartialView("ManageModuleIndexPartial", moduleList);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                throw;
            }
        }
        public async Task<IActionResult> AccessableModuleIndexPartial(long id, string currentFilter, string searchString, int? page)
        {
            List<Module> outputSetup = new List<Module>();
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

                //outputSetup = await _modContext.GetAccessableModules();
                LocalHardware hardware = new LocalHardware()
                {
                    Id = id
                };
                outputSetup = await _modContext.GetAccessableModules(hardware);

                if (!String.IsNullOrEmpty(searchString))
                {
                    //set up how to search db
                    outputSetup = await outputSetup.Where(b => (b.Name ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.Description ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.Name ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.ZipCode.ToString() ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.State ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.ContentDeveloperType.ToString() ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.Address ?? "").ToLower().Contains(searchString.ToLower())
                    ).ToListAsync();
                }

                int pageNumber = (page ?? 1);
                var output = await outputSetup.ToPagedListAsync(pageNumber, 25);
                return PartialView("AccessableModuleIndexPartial", output);
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
        public async Task<IActionResult> AddModule(long hardwareId, long moduleId)
        {
            string response = string.Empty;
            bool output = false;

            try
            {
                LocalHardwareLinkedModule link = await _moduleLinkedContext.Create(hardwareId, moduleId);
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
        public async Task<IActionResult> RemoveModule(long id)
        {
            string response = string.Empty;
            bool output = false;
            bool threw = false;

            try
            {
                LocalHardwareLinkedModule link = new LocalHardwareLinkedModule()
                {
                    Id = id
                };
                output = await _moduleLinkedContext.Remove(link);
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


        #region Add Cohort
        public async Task<IActionResult> ManageCohortIndex(long? id)
        {
            LocalHardware hardware = null;
            try
            {
                hardware = await _context.Get(id);
                return View(hardware);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                throw;
            }
        }
        public async Task<IActionResult> ManageCohortIndexPartial(long? id)
        {
            List<HardwareLinkedCohort> cohortList = new List<HardwareLinkedCohort>();
            try
            {
                cohortList = await _cohortLinkedContext.GetByHardware(id);
                return PartialView("ManageCohortIndexPartial", cohortList);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                throw;
            }
        }

        public async Task<IActionResult> AccessableCohortIndexPartial(string currentFilter, string searchString, int? page)
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

                outputSetup = await _cohortContext.Get();

                if (!String.IsNullOrEmpty(searchString))
                {
                    //set up how to search db
                    outputSetup = await outputSetup.Where(b => (b.Name ?? "").ToLower().Contains(searchString.ToLower())
                    || (b.Description ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.Name ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.ZipCode.ToString() ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.State ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.ContentDeveloperType.ToString() ?? "").ToLower().Contains(searchString.ToLower())
                    //|| (b.ContentDeveloper.Address ?? "").ToLower().Contains(searchString.ToLower())
                    ).ToListAsync();
                }

                int pageNumber = (page ?? 1);
                var output = await outputSetup.ToPagedListAsync(pageNumber, 25);
                return PartialView("AccessableCohortIndexPartial", output);
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
        public async Task<IActionResult> AddCohort(long hardwareId, long cohortId)
        {
            string response = string.Empty;
            bool output = false;

            try
            {
                HardwareLinkedCohort link = await _cohortLinkedContext.Create(hardwareId, cohortId);
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
        public async Task<IActionResult> RemoveCohort(long id)
        {
            string response = string.Empty;
            bool output = false;
            bool threw = false;

            try
            {
                HardwareLinkedCohort link = new HardwareLinkedCohort()
                {
                    Id = id
                };
                output = await _cohortLinkedContext.Remove(link);
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

        #region Detail widgets

        public async Task<IActionResult> LoadModuleAccessList(LocalHardware input)
        {            
            try
            {
                List<Module> output = await _moduleLinkedContext.Get(input);
                return PartialView("../Widget/_ModuleIndexPartial", output);
                //return PartialView(output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                throw;
            }            
        }

        public async Task<IActionResult> LoadCohortList(LocalHardware input)
        {
            try
            {
                List<HardwareLinkedCohort> preoutput = await _cohortLinkedContext.Get(input);
                List<Cohort> output = preoutput.Select(i => i.Cohort).ToList();
                return PartialView("../Widget/_CohortIndexPartial", output);
                //return PartialView(output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                throw;
            }
        }




        #endregion
    }
}
