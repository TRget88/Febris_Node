// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.EmailModels;
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
using Febris.ModelLibrary.Models.UserModels;
using Febris.PrimaryLogicLayer.Logic.UserLogic;
using Febris.ModelLibrary.ViewModels;
using Febris.SharedServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using Microsoft.Extensions.Configuration;
using Febris.EnumLibrary;
using Febris.UserNode.Portal.IdentityPolicy;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Microsoft.AspNetCore.Authorization;

namespace Febris.UserNode.Portal.Controllers.User
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    [Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]
    public class UserController : Controller
    {
        //templates MVCControllerwithContext generator used                
        private readonly ILogger<UserController> _logger;
        private readonly UserManager<LocalApplicationUser> _userManager;
        private readonly IUserLogic _context;
        private readonly IConfiguration _config;
        private readonly ICohortMemberLogic _memberContext;
        private readonly ICsvUserImporter _csvImporter;

        public UserController(
            IUserLogic context,
            ILogger<UserController> logger,
            UserManager<LocalApplicationUser> userManager,
            IConfiguration config,
            ICohortMemberLogic memberContext,
            ICsvUserImporter csvImporter
            )
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _config = config;
            _memberContext = memberContext;
            _csvImporter = csvImporter;
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
        // GET: LocalApplicationUser
        public async Task<IActionResult> Index()
        {
            return View();
        }
        // GET: LocalApplicationUser/IndexPartial
        public async Task<IActionResult> IndexPartial(string currentFilter, string searchString, int? page)
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


                outputSetup = await _context.Get();



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
                return PartialView("IndexPartial", output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
            }
            return PartialView();
        }

        public async Task<IActionResult> StudentIndex()
        {
            return View();
        }
        // GET: LocalApplicationUser/IndexPartial
        public async Task<IActionResult> StudentIndexPartial(string currentFilter, string searchString, int? page)
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

                outputSetup = await _context.Get(InstitutionUserAccountType.User);

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
                return PartialView("IndexPartial", output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
            }
            return PartialView();
        }


        public async Task<IActionResult> EducatorIndex()
        {
            return View();
        }
        // GET: LocalApplicationUser/IndexPartial
        public async Task<IActionResult> EducatorIndexPartial(string currentFilter, string searchString, int? page)
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

                outputSetup = await _context.Get(InstitutionUserAccountType.Educator);

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
                return PartialView("IndexPartial", output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
            }
            return PartialView();
        }


        public async Task<IActionResult> AdminIndex()
        {
            return View();
        }
        // GET: LocalApplicationUser/IndexPartial
        public async Task<IActionResult> AdminIndexPartial(string currentFilter, string searchString, int? page)
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

                outputSetup = await _context.Get(InstitutionUserAccountType.Admin);
                var secondOutputSetup = await _context.Get(InstitutionUserAccountType.ITAdmin);
                outputSetup.AddRange(secondOutputSetup);

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
        // GET: LocalApplicationUser/Details/5
        //public async Task<IActionResult> Details(Guid? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }
        //    try
        //    {
        //        LocalApplicationUser output = await _context.Get(id);
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

        // GET: LocalApplicationUser/DetailsModal/5
        public async Task<IActionResult> DetailsModal(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                LocalUserViewModel output = await _context.Get(id);
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
        // GET: LocalApplicationUser/Create
        public IActionResult Create()
        {
            //LocalUserCreation vm = new LocalUserCreation();
            return View();
        }

        // POST: LocalApplicationUser/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FirstName,LastName,IdentificationNumber,EmailAddress,PhoneNumber,UserAccountType")] LocalUserCreation input)
        {
            if (ModelState.IsValid)
            {
                //localApplicationUser.Id = Guid.NewGuid();
                LocalApplicationUser output = await _context.Create(input);
                if (output != null)
                {
                    //send varification email                                
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(output);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = output.Id, code = code },
                        protocol: Request.Scheme);
                    EmailService emailService = new EmailService(_config)
                    {
                        EmailType = EmailType.EmailVerification,
                        EmailModel = new EmailModel()
                        {
                            RecipientName = output.FirstName,
                            RecipientEmailAddress = output.Email,
                            SpecialHyperlink = callbackUrl
                        }
                    };
                    bool sent = false;
                    try
                    {
                        sent = await emailService.SendEmail();
                    }
                    catch (Exception ex)
                    {
                        //the account is already persisted -- a send failure (no SMTP configured is the
                        //shipped default) must not present as "create failed" and push the admin into a
                        //retry that then trips on the duplicate email
                        _logger.LogWarning(ex.Message);
                    }
                    StatusMessage = sent
                        ? "User was created and the verification email was sent."
                        : "User was created, but the verification email could not be sent. Configure SMTP, then have the user run forgot password.";
                }
                else
                {
                    StatusMessage = "User was not created.";
                }
                return RedirectToAction(nameof(Index));
            }
            return View(input);
        }
        #endregion

        #region Bulk Operations
        
        #region Bulk Create

        // GET: Lead/Create
        public IActionResult BulkCreate()
        {
            return View();
        }

        public async Task<IActionResult> BulkCreatePartial()
        {
            BulkUserCreationViewModel output = new BulkUserCreationViewModel();
            try
            {
                output = await _context.BulkCreationPreperation();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
            }
            return PartialView(output);
        }

        // POST: Lead/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> BulkCreate([Bind("FirstName,LastName,EmailAddress,PhoneNumber,JobTitle,CompanyName,LeadSource,LeadDetails")] Lead lead)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        Lead output = await _context.Create(lead);
        //        return RedirectToAction(nameof(Index));
        //    }
        //    return View(lead);
        //}


        // POST: Lead/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkCreatePost([FromBody] BulkUserCreationSubmitListViewModel bulkInput)
        {
            // Mirrors the guard BulkCreateCsvPost already had. A JSON body whose property
            // names do not match the view model deserializes to a model with a null
            // SubmissionList, which used to reach the BLL and throw -- the catch below
            // rethrows, so malformed input surfaced as an unhandled 500 rather than a 400.
            // An empty batch is a user error worth saying out loud, not a silent "0 were added".
            if (bulkInput?.SubmissionList == null || bulkInput.SubmissionList.Count == 0)
            {
                return BadRequest("No user rows were submitted. Paste your data and check the column format before creating.");
            }

            int DuplicateEmailAddresses = 0;
            int cohortLinksMade = 0;
            int UsersNotAdded = 0;
            int UsersAdded = 0;
            try
            {
                if (ModelState.IsValid)
                {
                    (UsersAdded, UsersNotAdded, cohortLinksMade, DuplicateEmailAddresses) = await _context.Create(bulkInput);
                    return Json(
                        UsersAdded + " Were added. "
                        + UsersNotAdded + " Were not added. "
                        + DuplicateEmailAddresses + " had duplicate email addresses. "
                        + cohortLinksMade + " cohort links made with users.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw;
            }
            return BadRequest("An error occured when trying to add these leads. Please check your data format again");
        }

        // POST: bulk-create users from an uploaded CSV file. Reuses the same backend as the Excel-paste
        // flow -- ICsvUserImporter parses the file into the bulk VM; AccountType and optional cohorts
        // come from the form (as with BulkCreatePost). Admin-only via the controller [Authorize].
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkCreateCsvPost(IFormFile file, InstitutionUserAccountType accountType, [FromForm] List<Guid?> selectedCohorts)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No CSV file was uploaded.");
            }
            try
            {
                using var stream = file.OpenReadStream();
                var parsed = _csvImporter.Parse(stream);
                if (parsed.Model?.SubmissionList == null || parsed.Model.SubmissionList.Count == 0)
                {
                    return BadRequest("No valid user rows were found in the CSV. " + string.Join(" ", parsed.Errors));
                }
                parsed.Model.AccountType = accountType;
                parsed.Model.SelectedCohortList = selectedCohorts ?? new List<Guid?>();
                var (usersAdded, usersNotAdded, cohortLinksMade, duplicateEmails) = await _context.Create(parsed.Model);
                string summary = usersAdded + " added, " + usersNotAdded + " not added, "
                    + duplicateEmails + " duplicate emails, " + cohortLinksMade + " cohort links made.";
                if (parsed.Errors.Count > 0)
                {
                    summary += " CSV row issues: " + string.Join(" ", parsed.Errors);
                }
                return Json(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                return BadRequest("An error occurred parsing or importing the CSV file.");
            }
        }
        #endregion

        #region Bulk Create

        // POST: Bulk User Management
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkRemovalPost([FromBody] BulkUserCreationSubmitListViewModel bulkInput)
        {
            // Mirrors the guard BulkCreateCsvPost already had. A JSON body whose property
            // names do not match the view model deserializes to a model with a null
            // SubmissionList, which used to reach the BLL and throw -- the catch below
            // rethrows, so malformed input surfaced as an unhandled 500 rather than a 400.
            // Especially here: a silent "0 removed" on a bad paste reads as a completed removal.
            if (bulkInput?.SubmissionList == null || bulkInput.SubmissionList.Count == 0)
            {
                return BadRequest("No user rows were submitted. Paste your data and check the column format before removing.");
            }

            int DuplicateEmailAddresses = 0;
            int cohortLinksMade = 0;
            int UsersNotAdded = 0;
            int UsersAdded = 0;
            try
            {
                if (ModelState.IsValid)
                {
                    (UsersAdded, UsersNotAdded, cohortLinksMade, DuplicateEmailAddresses) = await _context.Removal(bulkInput);
                    return Json(
                        UsersAdded + " Were added. "
                        + UsersNotAdded + " did not exist. "
                        //+ DuplicateEmailAddresses + " did not exist. "
                        + cohortLinksMade + " cohort links were removed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw;
            }
            return BadRequest("An error occured when trying to add these leads. Please check your data format again");
        }
        #endregion
        #endregion

        #region Edit
        // GET: LocalApplicationUser/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            LocalUserSettingsViewModel output = new LocalUserSettingsViewModel();
            try
            {
                output = await _context.GetEdit(id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            //if (output == null)
            //{
            //    return NotFound();
            //}

            return View(output);
        }

        // POST: LocalApplicationUser/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,FirstName,LastName,IdentificationNumber,EmailAddress,PhoneNumber,UserAccountType")] LocalUserSettingsViewModel input)
        {
            //if (id != localApplicationUser.Id)
            //{
            //    return NotFound();
            //}
            IFormFileCollection files = HttpContext.Request.Form.Files;
            if (ModelState.IsValid)
            {
                try
                {
                    //UserSettingsViewModel output = await _context.Update(files, input);
                    LocalApplicationUser output = await _context.Update(files, input);

                    // The BLL returns null when it REFUSES the edit -- the rank gate
                    // (RoleRankPolicy.CanAssign) or the no-self-role-change rule -- as well as when
                    // Identity itself fails. Redirecting unconditionally reported every one of
                    // those as success: the operator landed on a list still showing the old values
                    // with no sign the write was refused. Peers stay VISIBLE in that list by
                    // design, so hitting a refusal is a routine outcome here, not an edge case.
                    // Matches the sibling Create action, which already reports its null return.
                    if (output == null)
                    {
                        StatusMessage = "That account was not updated. You may not have permission to change it.";
                        return RedirectToAction(nameof(Index));
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex.Message);
                    StatusMessage = ex.Message;
                    throw;
                }

            }
            return View(input);
        }

        // GET: FebrisUser/Edit/5
        [Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
        public async Task<IActionResult> SelfEdit(Guid? id)
        {
            UserSettingsViewModel output = new UserSettingsViewModel();
            try
            {
                if (id != null)
                {
                    bool isSelf = User.IsCurrentUser(id.ToString());
                    if (!isSelf)
                    {
                        return Forbid();
                    }
                }
                output = await _context.GetSettings(Guid.Parse(User.GetUserId()));
                //output = await _context.Get(id);
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
            //if (id == null)
            //{
            //    return NotFound();
            //}
            //UserSettingsViewModel output = new UserSettingsViewModel();
            //try
            //{
            //    output = await _context.Get(id);
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogWarning(ex.Message);
            //    StatusMessage = ex.Message;
            //    return RedirectToAction(nameof(Index));
            //}
            //if (output == null)
            //{
            //    return NotFound();
            //}

            ////Creation output = new UserCreation()
            ////{
            ////    FirstName = appUser.ApplicationUser.UserName,
            ////    Id = appUser.ApplicationUser.Id,
            ////    UserId = appUser.ApplicationUser.Id,
            ////    PhoneNumber = appUser.ApplicationUser.PhoneNumber,
            ////    EmailAddress = appUser.ApplicationUser.Email,
            ////    FebrisUserType = (FebrisUserType)Enum.Parse(typeof(FebrisUserType), appUser.Role, true)
            ////};

            //return View(output);
        }

        // POST: FebrisUser/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelfEdit(Guid id, [Bind("Id,FirstName,LastName,EmailAddress,PhoneNumber")] UserSettingsViewModel input)
        {
            IFormFileCollection files = HttpContext.Request.Form.Files;
            if (ModelState.IsValid)
            {
                try
                {
                    UserSettingsViewModel output = await _context.Update(files, input);
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

        #region Delete
        // GET: LocalApplicationUser/Delete/5
        //public async Task<IActionResult> Delete(Guid? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }

        //    LocalApplicationUser output = new LocalApplicationUser;//();
        //    try
        //    {
        //        output = await _context.Get(id);
        //    }
        //    catch (Exception ex)
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

        //// POST: LocalApplicationUser/Delete/5
        //[HttpPost, ActionName("Delete")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> DeleteConfirmed(Guid id)
        //{
        //    bool output = await _context.Delete(id);
        //    if (output)
        //    {
        //        //TempData["StatusMessage"] = "Item was deleted successfully";
        //        StatusMessage = "Item was deleted successfully";
        //    }
        //    else
        //    {
        //        //TempData["StatusMessage"] = "Item was not deleted";
        //        StatusMessage = "Item was not deleted";
        //    }

        //    return RedirectToAction(nameof(Index));
        //}
        #endregion

        #region Miss        
        //private bool LocalApplicationUserExists(Guid id)
        //{
        //    return _context.Exists(id);
        //}
        // B-07 fixed: POST-only + antiforgery, method-level Admin/ITAdmin/SuperAdmin gate (the class
        // gate still admits Educators for the other actions on this controller), and the rank gate +
        // parent cascade now live in the BLL (UserLogic.LockoutToggle) so the API path is covered. A
        // 403 is returned when the acting operator does not strictly outrank the target.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
        public async Task<IActionResult> LockoutToggle(Guid id)
        {
            LocalApplicationUser user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return NotFound();
            }

            (bool allowed, bool nowLockedOut) = await _context.LockoutToggle(id);
            if (!allowed)
            {
                return Forbid();
            }

            return Ok(new { id = id, lockedOut = nowLockedOut });
        }

        // POST: /User/ResendActivation
        /// <summary>
        /// Re-send the account-activation email for an existing account (2026-08-21). The
        /// activation mail's own copy tells a recipient whose link lapsed to ask an administrator to
        /// resend, and until now there was nothing for that administrator to click.
        ///
        /// <para>
        /// Gated to <c>EducatorAndOrgAdmins</c>, matching the CREATE gate on this controller rather
        /// than the narrower one on LockoutToggle, because resending is the same act as creating:
        /// both mail a setup link to the account's own address, and neither hands the requester
        /// anything. WHICH accounts an operator may do it to is the separate question, answered by
        /// the rank policy in the BLL so the decision is covered wherever it is called from.
        /// </para>
        ///
        /// <para>
        /// Post-redirect-get back to the Edit page with a status message, following the ParentLink
        /// pattern. Deliberately NOT the JSON shape LockoutToggle uses -- this is a plain form post
        /// from a full page, so there is no jQuery to hand a token to and none to read a body.
        /// </para>
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendActivation(Guid id)
        {
            (bool allowed, bool sent) = await _context.ResendActivation(id);
            if (!allowed)
            {
                // Same answer whether the account is missing, soft-deleted, or simply outranks the
                // operator. Nothing here should confirm which.
                return Forbid();
            }

            TempData["StatusMessage"] = sent
                ? "Sent. They have been emailed a link to set their password."
                : "The email could not be sent. The account is fine -- check the mail settings, or "
                  + "have them use Forgot Password.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        //public async Task<IActionResult> ToggleLockOut(long Id, bool lockedout)
        //{
        //    //bool output = default;
        //    //if (Id == null || Id == 0)
        //    //{
        //    //    //TempData["StatusMessage"] = "not a valid choice";
        //    //    StatusMessage = "not a valid choice";
        //    //    return BadRequest();
        //    //}

        //    ////variables
        //    //LocalApplicationUser output = await _userContext.Users.FindAsync(Id);
        //    //if (user == null)
        //    //{
        //    //    return NotFound();
        //    //}

        //    //try
        //    //{
        //    //    output = await _userContext.LockOut(Id);
        //    //}
        //    //catch(Exception ex)
        //    //{
        //    //    _logger.LogWarning(ex.Message);
        //    //    //TempData["StatusMessage"] = ex.Message;
        //    //    StatusMessage = ex.Message;
        //    //    //throw;
        //    //    return BadRequest();
        //    //}
        //    return Ok();
        //}


        #endregion

        #region Detail Partials
        public async Task<IActionResult> LoadCohortList(Guid? id)
        {
            //if (id == null && id == 0)
            //{
            //    return NotFound();
            //}
            try
            {
                LocalUserViewModel userData = await _context.Get(id);
                //CohortMemberViewModel member = await _context.Get(id);
                List<CohortMember> preoutput = await _memberContext.GetCohortsByMember(userData.ApplicationUser.Id);
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

        // REMOVED 2026-08-18, node remote teardown Phase 1.8. The twin of the CohortMember one:
        // it redirected to `nameof(Purchase)`, a CENTRAL-tier controller removed from the node with
        // the commerce surface, and it compiled because nameof binds to the entity type. Runtime
        // 404 only, feeding the PurchaseList widget slot on User/DetailsModal, which goes with it.


        #endregion

        // "Febris Help Toggle" removed (owner ruling 2026-08-01). It presented itself as a
        // switch for VENDOR support access, but there is no Febris support account: it called
        // GetUsersInRoleAsync(SuperAdmin) -- the node's OWN admin role -- and set
        // LockoutEnd to DateTimeOffset.MaxValue on every match. On a node whose only admin is
        // the seeded SuperAdmin, that is a permanent self-lockout with no recovery path when
        // SMTP is unconfigured, reachable in two clicks by the very admin it locks out.
    }
}
