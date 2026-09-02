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
using System.IO;
using Febris.SharedServices;
using Microsoft.Extensions.Options;
using Febris.EnumLibrary;
using Microsoft.AspNetCore.Authorization;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.LogicLayer.Logic.DataLogic;

namespace Febris.UserNode.Portal.Controllers.Data
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    [Authorize(Roles = Febris.Constants.RoleConstants.EducatorAndOrgAdmins)]   
    public class LocalSoftwarePackageController : Controller
    {
        //templates MVCControllerwithContext generator used
        private readonly ILogger<LocalSoftwarePackageController> _logger;
        private readonly ILocalSoftwarePackageLogic _context;
        private readonly IPackageIngestLogic _ingestContext;
        private readonly IPackageFeedSyncLogic _feedSyncContext;
        private readonly ClientDownloadOptions _clientDownloads;
        public LocalSoftwarePackageController(
            ILocalSoftwarePackageLogic context,
            IPackageIngestLogic ingestContext,
            IPackageFeedSyncLogic feedSyncContext,
            IOptions<ClientDownloadOptions> clientDownloads,
            ILogger<LocalSoftwarePackageController> logger
            )
        {
            _context = context;
            _ingestContext = ingestContext;
            _feedSyncContext = feedSyncContext;
            // Never null: Configure<T> always yields an instance, and an absent section leaves
            // the class defaults, which is the point of link-out working unconfigured.
            _clientDownloads = clientDownloads?.Value ?? new ClientDownloadOptions();
            _logger = logger;
            //if (!User.HasSignedServiceAgreement() || !User.HasSignedServiceAgreement())
            //{
            //    return Forbid();
            //}
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

        #region Authoring (ROADMAP 16: the writes moved here from the API)

        #endregion

        #region Feed sync (ROADMAP 16: the trigger moved here from the API)

        // GET: LocalSoftwarePackage/FeedSync
        // ROADMAP 16: the feed-sync trigger moved from POST api/SoftwarePackage/SyncFromFeed
        // (NodeAdmin token) to a Portal form, mirroring HubFederation's Sync now. Manually
        // triggered on purpose for a first ship: a timer would run this before anyone had watched
        // it work once, and the operator needs to see the report.
        [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
        public IActionResult FeedSync()
        {
            return View(new PackageFeedSyncRequestViewModel { DryRun = true });
        }

        // POST: LocalSoftwarePackage/FeedSync
        // Runs one sync pass and renders the per-package report inline (the HubFederation SyncNow
        // shape: no redirect, the operator reads the outcome of the run they triggered). A run
        // does not fail as a whole because one package did -- check Refused and Failed in the
        // report rather than assuming.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
        public async Task<IActionResult> FeedSync(PackageFeedSyncRequestViewModel input)
        {
            try
            {
                if (input == null || string.IsNullOrWhiteSpace(input.ManifestUrl))
                {
                    ModelState.AddModelError(nameof(PackageFeedSyncRequestViewModel.ManifestUrl),
                        "A manifest URL is required.");
                    return View(input ?? new PackageFeedSyncRequestViewModel { DryRun = true });
                }

                PackageFeedSyncResultViewModel result = await _feedSyncContext.SyncFromFeed(input);
                ViewBag.SyncResult = result;
                StatusMessage = (result.DryRun ? "Dry run: " : "Sync finished: ")
                    + result.Ingested + " ingested, " + result.AlreadyCurrent + " already current, "
                    + result.Filtered + " filtered, " + result.Refused + " refused, "
                    + result.Failed + " failed.";
                return View(input);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                StatusMessage = ex.Message;
                return BadRequest();
            }
        }

        #endregion

        #region Details
        // GET: LocalSoftwarePackage/Details/5
        //public async Task<IActionResult> Details(long? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }
        //    try
        //    {
        //        LocalSoftwarePackage output = await _context.Get(id);
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

        // GET: LocalSoftwarePackage/DetailsModal/5
        public async Task<IActionResult> DetailsModal(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                LocalSoftwarePackage output = await _context.Get(id);
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

        public async Task<IActionResult> Download(Guid input)
        {
            if (input == null || input == Guid.Empty)
            {
                //TempData["StatusMessage"] = "not a valid choice";
                StatusMessage = "not a valid choice";
                return BadRequest();
            }

            //variables
            //LocalSoftwarePackage output = new LocalSoftwarePackage;            
            try
            {
                LocalSoftwarePackage package = await _context.Get(input);
                if(package == null)
                {
                    _logger.LogWarning("local software package get calls failing");
                    return BadRequest();
                }
                else if (package.LocalSoftwarePackageType == LocalSoftwarePackageType.AndroidMobileCompanion)
                {
                    string result = "<h1>The mobile companion application now automatically downloads directly to your mobile server application.</h1>";
                    //var resp = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK);
                    //resp.Content = new System.Net.Http.StringContent(result, System.Text.Encoding.UTF8, "text/plain");
                    return base.Content(result);

                }
                //FileStream output = await _context.DownloadPackage(package.UUID);
                Stream output = await _context.DownloadPackage(package.UUID);
                //string path = StaticDetails.LocalSoftwarePackage+package.UUID.ToString()+".zip";
                //return File(output, GetMimeTypes(), package.Name + ".zip");
                return File(output, "application/zip", package.Name+".zip");
                //return File(output, GetMimeTypes()[ext], Path.GetFileName(path));
                //return new File.Create(output,)
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);                
                StatusMessage = ex.Message;                
                return BadRequest();
            }            
        }


        private Dictionary<string, string> GetMimeTypes()
        {
            return new Dictionary<string, string>
            {            
                //{".zip", "application/octet-stream"},
                {".zip", "application/zip"}
            };
        }
        #endregion

        #region Download Pages
        public async Task<IActionResult> PackageDownload(LocalSoftwarePackageType input)
        {
            LocalSoftwarePackage output = new LocalSoftwarePackage();
            try
            {
                output = await _context.Get(input);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw;
            }

            // Empty state, not a 500. Get() returns null when no package of this kind has been
            // ingested -- which is EVERY node until an operator uploads or syncs one. View(null)
            // then NRE'd inside the view and the catch above had already been passed, so this was
            // an unhandled 500 on a documented, linked-from-the-nav path on a fresh node.
            // PackageArchive survived the same conditions only because it returns an empty list.
            if (output == null)
            {
                // ROUTE OUT. A node's catalogue starts empty and packages arrive only through the
                // feed, so rendering a blank page here would be a dead end on every fresh
                // deployment for software that demonstrably exists. Send the operator to the
                // public page for this component instead of describing it.
                string offsite = _clientDownloads.DownloadUrlFor(input);
                if (offsite != null)
                {
                    return Redirect(offsite);
                }

                // Link-out disabled (air-gapped). Keep the local empty state rather than inventing
                // a destination the operator cannot reach.
                StatusMessage = "No " + input.ToString() + " package has been added to this node yet.";
                return View(new LocalSoftwarePackage());
            }

            return View(output);
        }

        //this seems redundat compaired to the index
        public async Task<IActionResult> PackageArchive(LocalSoftwarePackageType input)
        {
            List<LocalSoftwarePackage> output = new List<LocalSoftwarePackage>();
            try
            {
                output = await _context.GetList(input);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw;
            }
            // Empty archive routes out for the same reason Download does. A node holding
            // versions still gets the table, which is the whole point of this page.
            if (output == null || output.Count == 0)
            {
                string offsite = _clientDownloads.DownloadUrlFor(input);
                if (offsite != null)
                {
                    return Redirect(offsite);
                }
            }

            ViewBag.PackageType = input;
            return View(output);
        }
        #endregion


        #region Documentation

        public async Task<IActionResult> Documentation(LocalSoftwarePackageType input)
        {
            // ROUTE OUT. The per-kind documentation views here were static copies that drifted
            // from the real thing, and the project's own repositories are where the current
            // instructions live. The public site links onward to each one, so this needs exactly
            // one URL rather than five that have to be corrected together.
            string offsite = _clientDownloads.DocumentationUrlFor(input);
            if (offsite != null)
            {
                return Redirect(offsite);
            }

            LocalSoftwarePackage output = new LocalSoftwarePackage();
            try
            {
                switch (input)
                {
                    case LocalSoftwarePackageType.PC:
                        {
                            return View("PCDocumentation");
                            break;
                        }
                    case LocalSoftwarePackageType.AndroidMobileServer:
                        {
                            return View("MobileServerDocumentation");
                            break;
                        }
                    case LocalSoftwarePackageType.AndroidMobileCompanion:
                        {
                            return View("MobileCompanionDocumentation");
                            break;
                        }                   
                    // Integration libraries. The enum carried CSharp and CPP all along, and the
                    // download and archive actions were already generic -- only Documentation
                    // switched on type, so these two fell through to the error redirect. Adding the
                    // nav links without these cases would have shipped two dead links. Views ported
                    // from the developer portal, which carries the same two with no central-tier
                    // content in them.
                    case LocalSoftwarePackageType.CSharp:
                        {
                            return View("CSharpDocumentation");
                        }
                    case LocalSoftwarePackageType.CPP:
                        {
                            return View("CPPDocumentation");
                        }
                    default:
                        return RedirectToAction("Error", "Home");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw;
            }
        }

        #endregion
    }
}
