// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using X.PagedList;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.ViewModels;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using Febris.SharedServices;
using Microsoft.AspNetCore.Authorization;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.UserNode.Portal.ViewModels;

namespace Febris.UserNode.Portal.Controllers.Data
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.FebrisStaff)]
    //[Authorize(Roles = Febris.Constants.RoleConstants.OrgMemberAndAdmins)]
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserNoParent)]
    // Raised from EndUserNoParent to EducatorAndOrgAdmins: this is now a WRITE-capable
    // controller, and EndUserNoParent includes the plain User role, which would have let any
    // signed-in user POST Create/Edit. Matches every write-capable sibling (Cohort, Module,
    // Hardware, Location, User).
    [Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]
    public class CurriculumController : Controller
    {
        private readonly ILogger<CurriculumController> _logger;
        private readonly ICurriculumLogic _context;
        private readonly IModuleLogic _moduleContext;
        // Industry / Category / Focus / Tag removed: that taxonomy is marketplace-scoped
        // (owner ruling 2026-08-01). It was injected here and never read. The MODELS stay in
        // shared/FebrisModelLibrary. Only the node-side plumbing goes.
        //
        // IContentDeveloperLogic and IMicrocredentialLogic also removed: ContentDeveloper is a hub
        // claim no node user carries, and neither was read by any surviving action. Module linking
        // goes through ICurriculumLogic, which already owns GetLinkedModules/ToggleModuleLink, so
        // injecting a second link BLL would duplicate it.
        //
        // The microcredential types were removed outright on 2026-08-28. The feature only ever
        // worked against the central accreditation system, where accreditation bodies authored
        // the credentials, and that system no longer exists.

        public CurriculumController(
            ICurriculumLogic context,
            ILogger<CurriculumController> logger,
            IModuleLogic moduleContext
            )
        {
            _context = context;
            _logger = logger;
            _moduleContext = moduleContext;
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
        // GET: Curriculum
        public async Task<IActionResult> Index()
        {
            return View();
        }

        // GET: Curriculum/IndexPartial
        public async Task<IActionResult> IndexPartial(string currentFilter, string searchString, int? page, bool includeObsolete = false)
        {
            List<Curriculum> outputSetup = new List<Curriculum>();
            // Surfaced to the view so the archived toggle renders in the right state and the pager
            // keeps the flag across pages.
            ViewData["IncludeObsolete"] = includeObsolete;
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

                outputSetup = await _context.Get(includeObsolete);

                if (!String.IsNullOrEmpty(searchString))
                {
                    // In-memory: the list is already materialised, so the original ToListAsync()
                    // here could never have compiled. Null-conditional on CurriculumClassification
                    // is required -- the FK is nullable, so an unclassified curriculum would NRE
                    // the moment anyone typed in the search box.
                    outputSetup = outputSetup.Where(b =>
                        (b.CurriculumClassification?.Name ?? "").ToLower().Contains(searchString.ToLower())
                        || (b.CurriculumClassification?.Description ?? "").ToLower().Contains(searchString.ToLower())
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
            // An EMPTY paged list, never a bare PartialView(). The view calls
            // Html.PagedListPager(Model, ...), so a null model throws a second time and buries the
            // original error behind a NullReferenceException from the pager.
            return PartialView("IndexPartial", new List<Curriculum>().ToPagedList(1, 25));
        }
        #endregion

        #region Details
        

        // GET: Curriculum/DetailsModal/5
        public async Task<IActionResult> DetailsModal(long? id)
        {
            if (id == 0)
            {
                return NotFound();
            }
            try
            {
                Curriculum output = await _context.Get(id);
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

        
        #region Widget partials

        // GET: Curriculum/LoadModuleIndex
        // Dispatched by Widget/LoadPartialDetail from the live Element0="ModuleIndex" slot in
        // Views/Curriculum/DetailsModal.cshtml. The slot shipped without this action, so the
        // modal's Module Index panel spun forever (ROADMAP 17). Mirrors Hardware/LoadModuleAccessList.
        public async Task<IActionResult> LoadModuleIndex(Curriculum input)
        {
            List<Module> output = new List<Module>();
            try
            {
                List<ModuleLinkedCurriculum> linkList = await _context.GetLinkedModules(input.Id);
                output = linkList.Select(i => i.Module).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                throw;
            }
            return PartialView("../Widget/_ModuleIndexPartial", output);
        }

        #endregion


        #region Authoring

        // GET: Curriculum/Create
        public async Task<IActionResult> Create()
        {
            CurriculumAuthoringViewModel output = new CurriculumAuthoringViewModel()
            {
                Curriculum = new Curriculum()
            };
            try
            {
                output.CurriculumClassificationList = await BuildClassificationList(null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
            }
            return View(output);
        }

        // POST: Curriculum/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Curriculum,SelectedCurriculumClassification")] CurriculumAuthoringViewModel input)
        {
            try
            {
                if (!ModelState.IsValid || input?.Curriculum == null)
                {
                    // Rebuild the SelectList before re-rendering. It is not in the [Bind] list, so
                    // without this the invalid-ModelState re-render shows an empty dropdown.
                    input = input ?? new CurriculumAuthoringViewModel() { Curriculum = new Curriculum() };
                    input.CurriculumClassificationList = await BuildClassificationList(input.SelectedCurriculumClassification);
                    return View(input);
                }

                input.Curriculum.UUID = Guid.NewGuid();
                input.Curriculum.TimeStamp = DateTime.Now;
                input.Curriculum.LastUpdateTimeStamp = DateTime.Now;
                input.Curriculum.CurriculumClassificationUUID = input.SelectedCurriculumClassification;
                // FK only. The DAL defends this too, but setting the navigation here would ask EF
                // to cascade-insert a duplicate classification row.
                input.Curriculum.CurriculumClassification = null;

                Curriculum output = await _context.Save(input.Curriculum);
                if (output == null)
                {
                    // Save returns null on a blank name; surface it instead of redirecting to an
                    // index that will not contain what the operator just "created".
                    StatusMessage = "Curriculum was not saved -- a name is required.";
                    input.CurriculumClassificationList = await BuildClassificationList(input.SelectedCurriculumClassification);
                    return View(input);
                }

                StatusMessage = "Curriculum created.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return BadRequest();
            }
        }

        // GET: Curriculum/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                Curriculum curriculum = await _context.Get(id);
                if (curriculum == null)
                {
                    return NotFound();
                }

                CurriculumAuthoringViewModel output = new CurriculumAuthoringViewModel()
                {
                    Curriculum = curriculum,
                    SelectedCurriculumClassification = curriculum.CurriculumClassificationUUID
                };
                output.CurriculumClassificationList = await BuildClassificationList(output.SelectedCurriculumClassification);
                return View(output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return BadRequest();
            }
        }

        // POST: Curriculum/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Curriculum,SelectedCurriculumClassification")] CurriculumAuthoringViewModel input)
        {
            if (input?.Curriculum == null || id != input.Curriculum.Id)
            {
                return NotFound();
            }
            try
            {
                if (!ModelState.IsValid)
                {
                    input.CurriculumClassificationList = await BuildClassificationList(input.SelectedCurriculumClassification);
                    return View(input);
                }

                input.Curriculum.CurriculumClassificationUUID = input.SelectedCurriculumClassification;
                input.Curriculum.CurriculumClassification = null;
                // Stamped server-side. A posted LastUpdateTimeStamp is a tamperable audit field.
                input.Curriculum.LastUpdateTimeStamp = DateTime.Now;

                Curriculum output = await _context.Save(input.Curriculum);
                if (output == null)
                {
                    StatusMessage = "Curriculum was not saved -- a name is required.";
                    input.CurriculumClassificationList = await BuildClassificationList(input.SelectedCurriculumClassification);
                    return View(input);
                }

                StatusMessage = "Curriculum saved.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return BadRequest();
            }
        }

        // POST: Curriculum/ObsoleteToggle
        // Soft delete, both directions. Curricula are referenced by CohortLinkedCurriculum and
        // ModuleLinkedCurriculum rows, so there is no hard-delete path at all.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ObsoleteToggle(long Id, bool obsolete)
        {
            try
            {
                return Ok(await _context.SetObsolete(Id, obsolete));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return BadRequest();
            }
        }

        private async Task<SelectList> BuildClassificationList(Guid? selected)
        {
            return new SelectList(await _context.GetClassifications(), "UUID", "Name", selected);
        }

        #endregion

        #region Module linking

        // GET: Curriculum/ModuleToCurriculumIndex?id=5
        public async Task<IActionResult> ModuleToCurriculumIndex(long id)
        {
            try
            {
                Curriculum output = await _context.Get(id);
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

        // GET: Curriculum/LinkingIndexPartial?id=5
        public async Task<IActionResult> LinkingIndexPartial(long id)
        {
            CurriculumModuleLinkingViewModel output = new CurriculumModuleLinkingViewModel()
            {
                ModuleList = new List<Module>(),
                LinkedModuleUuidList = new HashSet<Guid>()
            };
            try
            {
                output.Curriculum = await _context.Get(id);
                output.ModuleList = await _moduleContext.Get();
                List<ModuleLinkedCurriculum> linked = await _context.GetLinkedModules(id);
                output.LinkedModuleUuidList = linked.Select(i => i.ModuleUUID).ToHashSet();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
            }
            return PartialView("LinkingIndexPartial", output);
        }

        // POST: Curriculum/ToggleModuleToCurriculumLink
        // POST + antiforgery deliberately: this creates and deletes rows. The pattern it replaces
        // was a state-mutating GET.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleModuleToCurriculumLink(Guid curriculumGuid, Guid moduleGuid)
        {
            try
            {
                bool linked = await _context.ToggleModuleLink(curriculumGuid, moduleGuid);
                return Ok(linked);
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
