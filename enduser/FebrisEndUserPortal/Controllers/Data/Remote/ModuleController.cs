// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using X.PagedList;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.ModelLibrary.LookupModels;
using System.IO;
using System.Linq;
using Febris.SharedServices;
using Microsoft.AspNetCore.Authorization;
using Febris.UserNode.LogicLayer.Logic.DataLogic;

namespace Febris.UserNode.Portal.Controllers.Data
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    [Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]
    public class ModuleController : Controller
    {
        //templates MVCControllerwithContext generator used                
        private readonly ILogger<ModuleController> _logger;
        private readonly IModuleLogic _context;
        private readonly IPackageIngestLogic _ingestContext;
        //private readonly IIndustryLogic _industryContext;
        //private readonly ICategoryLogic _categoryContext;
        ////private readonly IFocusLogic _focusContext;
        //private readonly ITagLogic _tagContext;
        ////private readonly IObjectLogic _objectContext;
        //private readonly IModuleLinkedObjectLogic _objectLinkContext;
        //private readonly IXRHardwareModelLogic _xrModelContext;
        //private readonly IModuleHardwareCompatibilityLogic _compatibilityLogic;
        //private readonly IModuleLinkedIndustryLogic _moduleLinkedIndustryLogic;
        //private readonly IModuleLinkedCategoryLogic _moduleLinkedCategoryLogic;
        //private readonly IModuleLinkedFocusLogic _moduleLinkedFocusLogic;
        //private readonly IModuleLinkedTagLogic _moduleLinkedTagLogic;
        //private readonly IModuleLinkedClassificationLogic _moduleLinkedClassificationLogic;

        public ModuleController(
            IModuleLogic context,
            IPackageIngestLogic ingestContext,
            ILogger<ModuleController> logger//,
            //IIndustryLogic industryContext,
            //ICategoryLogic categoryContext,
            ////IFocusLogic focusContext,
            //ITagLogic tagContext,
            ////IObjectLogic objectContext,
            //IModuleLinkedObjectLogic objectLinkContext,
            //IXRHardwareModelLogic xrModelContext//,
            //IModuleHardwareCompatibilityLogic compatibilityLogic,
            //IModuleLinkedIndustryLogic moduleLinkedIndustryLogic,
            //IModuleLinkedCategoryLogic moduleLinkedCategoryLogic,
            //IModuleLinkedFocusLogic moduleLinkedFocusLogic,
            //IModuleLinkedTagLogic moduleLinkedTagLogic//,
            //IModuleLinkedClassificationLogic moduleLinkedClassificationLogic
            )
        {
            _context = context;
            _ingestContext = ingestContext;
            _logger = logger;
            //_industryContext = industryContext;
            //_categoryContext = categoryContext;
            ////_focusContext = focusContext;
            //_tagContext = tagContext;
            ////_objectContext = objectContext;
            //_objectLinkContext = objectLinkContext;
            //_xrModelContext = xrModelContext;
            //_compatibilityLogic = compatibilityLogic;
            //_moduleLinkedIndustryLogic = moduleLinkedIndustryLogic;
            //_moduleLinkedCategoryLogic = moduleLinkedCategoryLogic;
            //_moduleLinkedFocusLogic = moduleLinkedFocusLogic;
            //_moduleLinkedTagLogic = moduleLinkedTagLogic;
            //_moduleLinkedClassificationLogic = moduleLinkedClassificationLogic;
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
        // GET: Module/Details/5
        //public async Task<IActionResult> Details(long? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }
        //    try
        //    {
        //        Module output = await _context.Get(id);
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

        // GET: Module/DetailsModal/5
        public async Task<IActionResult> DetailsModal(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                Module output = await _context.Get(id);
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

        #region Index

        // GET: Module
        public async Task<IActionResult> Index()
        {
            return View();
        }

        // GET: Module/IndexPartial
        public async Task<IActionResult> IndexPartial(string currentFilter, string searchString, int? page)
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

                outputSetup = await _context.Get();

                if (!String.IsNullOrEmpty(searchString))
                {
                    // Null-conditional on ModuleClassification: the navigation is optional, so an
                    // unclassified module would NRE the moment anyone typed in the search box.
                    outputSetup = outputSetup.Where(b =>
                        (b.ModuleClassification?.Name ?? "").ToLower().Contains(searchString.ToLower())
                        || (b.Description ?? "").ToLower().Contains(searchString.ToLower())
                        || (b.Name ?? "").ToLower().Contains(searchString.ToLower())
                        ).ToList();
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
            // Empty paged list, never a bare PartialView(): the view calls Html.PagedListPager,
            // which throws again on a null model and hides the real error.
            return PartialView("IndexPartial", new List<Module>().ToPagedList(1, 25));
        }

        #endregion

        #region Authoring

        // GET: Module/Create
        public async Task<IActionResult> Create()
        {
            return View(new ModulePackageUploadViewModel());
        }

        // POST: Module/Create
        // The node's own authoring path for a module package -- and since ROADMAP 16 the ONLY
        // ingest path: stream to IStorageProvider, record the stored bytes' SHA-256 on a
        // PackageArtifact row, upsert the local Module catalog row. The API twin this once
        // mirrored (api/Module/Upload, NodeAdmin token) is deleted, cookie auth plus the
        // controller's role attribute being the right trust model for an operator action.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(1_073_741_824)]
        public async Task<IActionResult> Create(ModulePackageUploadViewModel input)
        {
            try
            {
                if (input?.File == null || input.File.Length == 0)
                {
                    ModelState.AddModelError("File", "A module package (.zip) is required.");
                }
                else if (!input.File.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    // Mirrors the ingest logic's own IsZip guard so the operator is told here
                    // rather than receiving a silent null from the ingest call.
                    ModelState.AddModelError("File", "The module package must be a .zip file.");
                }

                if (!ModelState.IsValid)
                {
                    return View(input ?? new ModulePackageUploadViewModel());
                }

                ModulePackageIngestResultViewModel result;
                using (Stream content = input.File.OpenReadStream())
                {
                    result = await _ingestContext.IngestModulePackage(content, input.File.FileName, input);
                }

                if (result == null)
                {
                    // ROADMAP 15: the extension was already checked above, so by here the usual
                    // cause is that the FILE is not really a zip. Say that rather than "not
                    // ingested", which tells the operator nothing they can act on.
                    StatusMessage = "The module package was not ingested. The file must be a "
                        + "readable .zip archive containing at least one entry -- renaming another "
                        + "file to .zip does not work.";
                    return View(input);
                }

                // ROADMAP 15: the ingest now mints the module's xAPI activity and links it. Report
                // that separately rather than as a flat success, because an unlinked module
                // downloads fine and then fails at launch -- the exact failure this chain fixes.
                // T10: prefer the specific reason the ingest now reports, since "activity could not
                // be created" and "activity created but the link did not persist" are different
                // failures with the same symptom. Falls back to the general wording.
                StatusMessage = result.Link == null
                    ? (string.IsNullOrWhiteSpace(result.StatusMessage)
                        ? "Module ingested, but its xAPI activity could not be created, so it cannot launch yet."
                        : "Module ingested. " + result.StatusMessage)
                    : "Module ingested.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return BadRequest();
            }
        }

        // GET: Module/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                Module output = await _context.Get(id);
                if (output == null)
                {
                    return NotFound();
                }
                return View(output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return BadRequest();
            }
        }

        // POST: Module/Edit/5
        // Metadata only. The stored package bytes belong to the ingest path, so editing a name
        // here must never imply the .zip changed; re-ingesting the same UUID is how you replace
        // the payload.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, Module input)
        {
            if (input == null || id != input.Id)
            {
                return NotFound();
            }
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(input);
                }

                Module output = await _context.Save(input);
                if (output == null)
                {
                    StatusMessage = "Module was not saved -- a name is required and the module must already exist.";
                    return View(input);
                }

                StatusMessage = "Module saved.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return BadRequest();
            }
        }

        #endregion

    }
}
