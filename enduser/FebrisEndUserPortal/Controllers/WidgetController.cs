// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.AnalyticsModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.LegalModels;
using Febris.ModelLibrary.Models.UserModels;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.ViewModels;
using Febris.PrimaryLogicLayer.Logic.DataLogic;
using Febris.UserNode.LogicLayer.Logic;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.SharedServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.Portal.Controllers
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    [Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    public class WidgetController : Controller
    {

        private readonly ILogger<WidgetController> _logger;
        private readonly IWidgetLogic _context;
        private readonly IFileServerHandler _fileHandler;
        private readonly IMessageBoardLogic _messageboardContext;
        private readonly Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic _recordingContext;



        /// <summary>
        /// Node hygiene (DI-only): IFileServerHandler now flows in through DI
        /// (registered in Startup) instead of the legacy `new FileServerHandler()` self-newing.
        /// The media loaders below still consume the legacy StaticDetails.*FileSystemPath layout
        /// through that handler ON PURPOSE: the IStorageProvider swap for the media areas is
        /// blocked on the Phase 3 layout reconciliation documented on
        /// <see cref="Febris.SharedServices.Storage.StorageKeys"/> (Specific-rooted, mixed-case
        /// legacy paths have no verified-clean key builders yet), so a swap today would silently
        /// read from the wrong location.
        /// </summary>
        public WidgetController(
            ILogger<WidgetController> logger,
            IWidgetLogic context,
            IFileServerHandler fileHandler,
            IMessageBoardLogic messageboardContext,
            Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic recordingContext
            )
        {
            _logger = logger;
            _context = context;
            _fileHandler = fileHandler;
            _messageboardContext = messageboardContext;
            _recordingContext = recordingContext;
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

        public async Task<IActionResult> StatusMessageModal(string input)
        {
            try
            {
                //string output = JsonConvert.SerializeObject(input);
                //return PartialView(output);

                return PartialView("StatusMessageModal", input);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WidgetController.StatusMessageModal: suppressed exception");
                return PartialView("StatusMessageModal", ex.Message);
            }

        }

        //#region Partial Detail 

        /// <summary>
        /// Triggered when a partial is opened so details can be loaded. - Auto populate
        /// </summary>
        /// <param name="modelName"></param>
        /// <param name="variableName"></param>
        /// <param name="modelId"></param>
        /// <returns></returns>
        public async Task<IActionResult> LoadPartialDetail(string modelName, string variableName, string modelId)
        {
            try
            {
                long longModelId = 0;
                Guid guidModelId = Guid.Empty;
                bool islongValue = long.TryParse(modelId, out longModelId);
                if (!islongValue)
                {
                    // modelId is OPTIONAL. A page with no #modelId input (the Switchboard is one)
                    // sends the literal string "null", because LoadViewDetails.js initialises
                    // modelId = null and string-concatenates it into the URL. That is an expected
                    // shape, not an exception: the previous Guid.Parse in a try/catch logged a
                    // full error five times per Switchboard load and then continued with
                    // Guid.Empty anyway. TryParse produces the identical Guid.Empty silently.
                    Guid.TryParse(modelId, out guidModelId);
                }

                switch (modelName.ToLower())
                {
                    #region Data
                    // "adminmessageboard" and "message" cases removed with the hub messaging
                    // teardown: both dispatched to controllers that no longer exist.
                    case "category":
                        {
                            Category category = new Category();
                            if (islongValue)
                            {
                                category = new Category()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                category = new Category()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, category);
                            //break;
                        }
                    case "contentdeveloper":
                        {
                            ContentDeveloper contentdeveloper = new ContentDeveloper();
                            if (islongValue)
                            {
                                contentdeveloper = new ContentDeveloper()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                contentdeveloper = new ContentDeveloper()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, contentdeveloper);
                            //break;

                        }
                    case "curriculum":
                        {
                            Curriculum curriculum = new Curriculum();
                            if (islongValue)
                            {
                                curriculum = new Curriculum()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                curriculum = new Curriculum()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, curriculum);
                            //break;
                        }
                    case "cohort":
                        {

                            Cohort item = new Cohort();
                            if (islongValue)
                            {
                                item = new Cohort()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                item = new Cohort()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, item);

                        }
                    case "cohortmember":
                        {

                            CohortMember item = new CohortMember();
                            if (islongValue)
                            {
                                item = new CohortMember()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                item = new CohortMember()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, item);

                            //break;
                        }
                    case "deploymenttype":
                        {
                            DeploymentType deploymenttype = new DeploymentType();
                            if (islongValue)
                            {
                                deploymenttype = new DeploymentType()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                deploymenttype = new DeploymentType()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, deploymenttype);
                            //break;
                        }
                    case "hardware":
                        {
                            Hardware hardware = new Hardware();
                            if (islongValue)
                            {
                                hardware = new Hardware()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                hardware = new Hardware()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, hardware);
                            //break;
                        }
                    case "industry":
                        {
                            Industry industry = new Industry();
                            if (islongValue)
                            {
                                industry = new Industry()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                industry = new Industry()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, industry);
                            //break;
                        }
                    case "institution":
                        {
                            Institution institution = new Institution();
                            if (islongValue)
                            {
                                institution = new Institution()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                institution = new Institution()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, institution);
                            //break;
                        }
                    case "institutiontype":
                        {
                            InstitutionType institutiontype = new InstitutionType();
                            if (islongValue)
                            {
                                institutiontype = new InstitutionType()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                institutiontype = new InstitutionType()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, institutiontype);
                            //break;
                        }
                    case "liabilitywaiver":
                        {
                            LiabilityWaiver liabilityWaiver = new LiabilityWaiver();
                            if (islongValue)
                            {
                                liabilityWaiver = new LiabilityWaiver()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                liabilityWaiver = new LiabilityWaiver()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, liabilityWaiver);
                            //break;
                        }
                    case "license":
                        {
                            License license = new License();
                            if (islongValue)
                            {
                                license = new License()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                license = new License()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, license);
                            //break;
                        }
                    case "localsoftwarepackage":
                        {
                            LocalSoftwarePackage localsoftwarepackage = new LocalSoftwarePackage();
                            if (islongValue)
                            {
                                localsoftwarepackage = new LocalSoftwarePackage()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                localsoftwarepackage = new LocalSoftwarePackage()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, localsoftwarepackage);
                            //break;
                        }
                    case "module":
                        {
                            Module module = new Module();
                            if (islongValue)
                            {
                                module = new Module()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                module = new Module()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, module);
                            //break;
                        }
                    case "switchboard":
                        {
                            return RedirectToAction("Load" + variableName, modelName);
                        }
                    case "tag":
                        {
                            Tag tag = new Tag();
                            if (islongValue)
                            {
                                tag = new Tag()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                tag = new Tag()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, tag);
                        }
                    #endregion
                    #region User
                    ///User
                    case "testuser":
                        {
                            TestUser item = new TestUser();
                            if (islongValue)
                            {
                                item = new TestUser()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                item = new TestUser()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, item);
                        }
                    ///User 
                    //case "userdata":
                    //    {
                    //        LocalApplicationUser userdata = new LocalApplicationUser();
                    //        if (islongValue)
                    //        {
                    //            userdata = new LocalApplicationUser()
                    //            {
                    //                Id = guidModelId
                    //            };
                    //        }
                    //        else
                    //        {
                    //            userdata = new LocalApplicationUser()
                    //            {
                    //                Id = guidModelId
                    //            };
                    //        }
                    //        return RedirectToAction("Load" + variableName, modelName, userdata);
                    //        //break;
                    //    }
                    case "user":
                        {
                            LocalApplicationUser user = new LocalApplicationUser();
                            if (islongValue)
                            {
                                user = new LocalApplicationUser()
                                {
                                    Id = guidModelId
                                };
                            }
                            else
                            {
                                user = new LocalApplicationUser()
                                {
                                    Id = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, user);
                            //break;

                            //Analytics
                            #region Analytics
                        }
                    #endregion
                    #region Analytics
                    ///Analytics
                    case "useranalytics":
                        {
                            UserAnalytics userAnalytics = new UserAnalytics();
                            if (islongValue)
                            {
                                userAnalytics = new UserAnalytics()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                userAnalytics = new UserAnalytics()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, userAnalytics);
                            //break;
                        }
                    case "localanalytics":
                        {
                            LocalAnalytics userAnalytics = new LocalAnalytics();
                            if (islongValue)
                            {
                                userAnalytics = new LocalAnalytics()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                userAnalytics = new LocalAnalytics()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, userAnalytics);
                            //break;
                        }
                    #endregion
                    #region xAPI
                    ///Statement
                    case "actor":
                        {
                            Actor input = new Actor();
                            if (islongValue)
                            {
                                input = new Actor()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                input = new Actor()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, input);
                            //break;
                        }
                    case "statement":
                        {

                            Statement input = new Statement();
                            if (islongValue)
                            {
                                input = new Statement()
                                {
                                    Id = longModelId
                                };
                            }
                            else
                            {
                                input = new Statement()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, input);
                            //break;

                        }
                    case "verb":
                        {
                            Verb input = new Verb();
                            if (islongValue)
                            {
                                input = new Verb()
                                {
                                    Key = longModelId
                                };
                            }
                            else
                            {
                                input = new Verb()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, input);
                            //break;
                        }
                    case "object":
                        {

                            ModelLibrary.Models.XApiModels.Object input = new ModelLibrary.Models.XApiModels.Object();
                            if (islongValue)
                            {
                                input = new ModelLibrary.Models.XApiModels.Object()
                                {
                                    Key = longModelId
                                };
                            }
                            else
                            {
                                input = new ModelLibrary.Models.XApiModels.Object()
                                {
                                    UUID = guidModelId
                                };
                            }
                            return RedirectToAction("Load" + variableName, modelName, input);
                            //break;
                        }
                    #endregion
                    default:
                        return NotFound();
                        //break;


                }
            }
            catch (Exception ex)
            {
                //_logger.
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return NotFound();
        }

        #endregion

        #region Partial Detail - old
        /// <summary>
        /// Triggered when a partial is opened so details can be loaded. - Auto populate
        /// </summary>
        /// <param name="modelName"></param>
        /// <param name="variableName"></param>
        /// <param name="modelId"></param>
        /// <returns></returns>
        //public async Task<IActionResult> LoadPartialDetail(string modelName, string variableName, string modelId)
        //{
        //    try
        //    {
        //        long longModelId = 0;
        //        Guid guidModelId = new Guid();
        //        bool islongValue = long.TryParse(modelId, out longModelId);
        //        if (!islongValue)
        //        {
        //            try
        //            {
        //                guidModelId = Guid.Parse(modelId);
        //            }
        //            catch
        //            {

        //            }
        //        }

        //        switch (modelName.ToLower())
        //        {
        //            case "accreditationbody":
        //                AccreditationBody accreditationbody = new AccreditationBody();
        //                if (islongValue)
        //                {
        //                    accreditationbody = new AccreditationBody()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    accreditationbody = new AccreditationBody()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, accreditationbody);
        //                //break;
        //            case "adminmessageboard":
        //                AdminMessageBoard adminMessageboard = new AdminMessageBoard();
        //                if (islongValue)
        //                {
        //                    adminMessageboard = new AdminMessageBoard()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    adminMessageboard = new AdminMessageBoard()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, adminMessageboard);
        //                //break;
        //            case "category":
        //                Category category = new Category();
        //                if (islongValue)
        //                {
        //                    category = new Category()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    category = new Category()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, category);
        //                //break;
        //            case "contentdeveloper":
        //                ContentDeveloper contentdeveloper = new ContentDeveloper();
        //                if (islongValue)
        //                {
        //                    contentdeveloper = new ContentDeveloper()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    contentdeveloper = new ContentDeveloper()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, contentdeveloper);
        //                //break;
        //            //case "contentdeveloperuser":
        //            //    ApplicationUser contentdeveloperuser = new ApplicationUser();
        //            //    if (islongValue)
        //            //    {
        //            //        contentdeveloperuser = new ApplicationUser()
        //            //        {
        //            //            Id = longModelId
        //            //        };
        //            //    }
        //            //    else
        //            //    {
        //            //        contentdeveloperuser = new ApplicationUser()
        //            //        {
        //            //            UUID = guidModelId
        //            //        };
        //            //    }
        //            //    return RedirectToAction("Load" + variableName, modelName, contentdeveloperuser);
        //            //    //break;
        //            case "curriculum":
        //                Curriculum curriculum = new Curriculum();
        //                if (islongValue)
        //                {
        //                    curriculum = new Curriculum()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    curriculum = new Curriculum()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, curriculum);
        //                //break;
        //            case "deploymenttype":
        //                DeploymentType deploymenttype = new DeploymentType();
        //                if (islongValue)
        //                {
        //                    deploymenttype = new DeploymentType()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    deploymenttype = new DeploymentType()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, deploymenttype);
        //                //break;
        //            case "discount":
        //                Discount discount = new Discount();
        //                if (islongValue)
        //                {
        //                    discount = new Discount()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    discount = new Discount()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, discount);
        //                //break;
        //            case "hardwaretype":
        //                HardwareType hardwaretype = new HardwareType();
        //                if (islongValue)
        //                {
        //                    hardwaretype = new HardwareType()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    hardwaretype = new HardwareType()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, hardwaretype);
        //                //break;
        //            case "industry":
        //                Industry industry = new Industry();
        //                if (islongValue)
        //                {
        //                    industry = new Industry()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    industry = new Industry()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, industry);
        //                //break;
        //            case "invoice":
        //                Invoice invoice = new Invoice();
        //                if (islongValue)
        //                {
        //                    invoice = new Invoice()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    invoice = new Invoice()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, invoice);
        //                //break;
        //            case "institution":
        //                Institution institution = new Institution();
        //                if (islongValue)
        //                {
        //                    institution = new Institution()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    institution = new Institution()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, institution);
        //                //break;
        //            case "institutiontype":
        //                InstitutionType institutiontype = new InstitutionType();
        //                if (islongValue)
        //                {
        //                    institutiontype = new InstitutionType()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    institutiontype = new InstitutionType()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, institutiontype);
        //                //break;
        //            case "liabilitywaiver":
        //                LiabilityWaiver LiabilityWaiver = new LiabilityWaiver();
        //                if (islongValue)
        //                {
        //                    LiabilityWaiver = new LiabilityWaiver()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    LiabilityWaiver = new LiabilityWaiver()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, LiabilityWaiver);
        //                //break;
        //            case "license":
        //                License license = new License();
        //                if (islongValue)
        //                {
        //                    license = new License()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    license = new License()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, license);
        //                //break;
        //            case "localsoftwarepackage":
        //                LocalSoftwarePackage localsoftwarepackage = new LocalSoftwarePackage();
        //                if (islongValue)
        //                {
        //                    localsoftwarepackage = new LocalSoftwarePackage()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    localsoftwarepackage = new LocalSoftwarePackage()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, localsoftwarepackage);
        //                //break;                    
        //            case "module":
        //                Module module = new Module();
        //                if (islongValue)
        //                {
        //                    module = new Module()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    module = new Module()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, module);
        //            case "switchboard":                        
        //                return RedirectToAction("Load" + variableName, modelName);
        //            case "tag":
        //                Tag tag = new Tag();
        //                if (islongValue)
        //                {
        //                    tag = new Tag()
        //                    {
        //                        Id = longModelId
        //                    };
        //                }
        //                else
        //                {
        //                    tag = new Tag()
        //                    {
        //                        UUID = guidModelId
        //                    };
        //                }
        //                return RedirectToAction("Load" + variableName, modelName, tag);
        //            default:
        //                return NotFound();
        //                //break;
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        //_logger.
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //    }
        //    return NotFound();
        //}

        #endregion

        #region Message board
        #region - Mini message board - moved
        /// <summary>
        /// 
        /// </summary>        
        /// <returns></returns>
        //public async Task<IActionResult> MiniMessageBoardPartial()
        //{
        //    //User.AccreditationBody();
        //    //User.ContentDeveloper();

        //    //variables            
        //    MessageBoard messageBoard = new MessageBoard();
        //    List<MessageBoard> messageBoardList = new List<MessageBoard>();

        //    messageBoardList = await _messageboardContext.GetLastFive();

        //    //MessageBoardViewModel vm = new MessageBoardViewModel()
        //    //{
        //    //    MessageBoardList = messageBoardList
        //    //};


        //    return PartialView("../Widgets/_MiniMessageboard", messageBoardList);
        //}

        #endregion

        #region - Febris Mini Messageboard - moved
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        //public async Task<IActionResult> FebrisMiniMessageBoardPartial()
        //{
        //    List<AdminMessageBoard> messageBoardList = new List<AdminMessageBoard>();

        //    messageBoardList = await _adminMessageboardContext.GetLastFive();

        //    return PartialView("../Widgets/_MiniMessageboard", messageBoardList);
        //}

        #endregion
        #endregion


        #region Media

        /// <summary>
        /// This is currently not using specified file handlers and just using generic currently
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        #region image loader 
        public async Task<IActionResult> RemoteImageLoader(string path)
        {
            try
            {
                string extension = Path.GetExtension(path);// need to remove the .
                extension = extension.Replace(".", string.Empty);
                string imageType = "image/" + extension;
                byte[] image = await _context.RemoteImageLoader(path);
                return File(image, imageType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WidgetController.RemoteImageLoader: suppressed exception");
                //probably need to put something else here.
                return PartialView(StaticDetails.DefaultLogo);
            }
        }
        public async Task<IActionResult> ImageLoader(string path)
        {
            try
            {
                string extension = Path.GetExtension(path);// need to remove the .
                extension = extension.Replace(".", string.Empty);
                string imageType = "image/" + extension;
                // Audit C-08: Path.Combine DISCARDS the base when the second argument is rooted, so
                // this raw query value gave SUBSTITUTION, not traversal -- any file the process
                // could read. Contain it to the intended root. Deliberately NOT Path.GetFileName:
                // logos are legitimately stored multi-segment (Logos\{uuid}{ext}).
                if (!MediaPathGuard.TryResolve(StaticDetails.ImageFileSystemPath, path, out string safePath))
                {
                    return NotFound();
                }
                byte[] image = await _fileHandler.GetImage(safePath);//.Result;
                return File(image, imageType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WidgetController.ImageLoader: suppressed exception");
                //probably need to put something else here.
                return PartialView(StaticDetails.DefaultLogo);
            }
        }
        public async Task<IActionResult> LoadProfilePicture(string path)
        {
            try
            {
                string extension = Path.GetExtension(path);
                extension = extension.Replace(".", string.Empty);
                string imageType = "image/" + extension;
                // Audit C-08 -- see ImageLoader above for the mechanism.
                if (!MediaPathGuard.TryResolve(StaticDetails.ProfessionalFileSystemPath, path, out string safePath))
                {
                    return NotFound();
                }
                byte[] image = await _fileHandler.GetImage(safePath);//.Result;
                return File(image, imageType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WidgetController.LoadProfilePicture: suppressed exception");
                //probably need to put something else here.
                return PartialView(StaticDetails.DefaultLogo);
            }
        }
        #endregion

        #region video loader        
        public async Task<IActionResult> VideoLoader(string videoName)
        {
            try
            {
                string extension = Path.GetExtension(videoName);// need to remove the .
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".mp4";
                    videoName = videoName + ".mp4";
                }
                extension = extension.Replace(".", string.Empty);
                string videoType = "video/" + extension;
                // Audit C-08 -- same substitution defect as the image loaders above.
                // ENTITLEMENT. Until now the only checks here were "do you hold an end-user role"
                // and "does the path stay inside the recordings folder", so any signed-in end user
                // who knew a recording's Guid could fetch it, including another learner's. The Guid
                // being unguessable was the only protection, which is secrecy of the identifier
                // rather than access control.
                //
                // MayView answers the owner's rule directly: you may watch a recording that belongs
                // to an actor you are entitled to, or you are staff. NotFound rather than Forbid
                // deliberately -- it matches both existing failure paths in this action and does not
                // confirm that a recording by that name exists.
                if (!await _recordingContext.MayView(videoName))
                {
                    return NotFound();
                }

                if (!MediaPathGuard.TryResolve(StaticDetails.RecordingsFileSystemPath, videoName, out string safeVideo))
                {
                    return NotFound();
                }
                FileStream video = await _fileHandler.GetVideo(safeVideo);//.Result;
                //byte[] video = _videoHandler.GetVideo(videoName).Result;
                return File(video, videoType);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "WidgetController.VideoLoader: suppressed exception");
                // 404 rather than a placeholder asset. The previous fallback passed an
                // asset path to PartialView(), which takes a VIEW name -- so it threw a
                // second, less informative exception on top of the first one.
                return NotFound();
            }
        }
        #endregion

        #endregion

        #region - Pending Requests
        //public async Task<IActionResult> PendingRequestCheck()
        //{
        //    //get user 
        //    var user = (await _userManager.GetUserAsync(User));
        //    if (user.Id == Guid.Empty)
        //    {
        //        return NotFound();
        //    }

        //    //variables            
        //    bool hasPendingItems = false;
        //    bool hasWaivedLiability = false;
        //    Professional professional = new Professional();
        //    try
        //    {
        //        //get professional linked to user
        //        professional = await _context.ProfessionalLinkedUser
        //            .Include(p => p.Professional)
        //            .Where(p => p.UserId == user.Id)
        //            .Select(i => i.Professional)
        //            .FirstOrDefaultAsync();
        //        if (professional == null || professional.Id == 0)
        //        {
        //            return NotFound();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return NotFound();
        //    }

        //    try
        //    {
        //        hasWaivedLiability = await LiabilityWaiverCheck(professional);
        //        if (!hasWaivedLiability)
        //        {
        //            return PartialView("../Widgets/_LiabilityWaiverButtonPartial");
        //        }
        //    }
        //    catch (Exception ex)
        //    { }

        //    try
        //    {
        //        //test to see if user or professional has any pending items.
        //        hasPendingItems = await _context.InstitutionLinkedProfessional
        //            .Include(i => i.Professional)
        //            .Where(i => i.Professional.Id == professional.Id
        //            && i.AttachmentStatus == AttachmentStatus.Pending)
        //            .AnyAsync();
        //    }
        //    catch (Exception ex)
        //    { }

        //    try
        //    {
        //        if (!hasPendingItems)
        //        {
        //            hasPendingItems = await _context.InstitutionLinkedUser
        //            .Where(i => i.UserId == user.Id
        //            && i.AttachmentStatus == AttachmentStatus.Pending)
        //            .AnyAsync();
        //        }
        //    }
        //    catch (Exception ex)
        //    { }

        //    try
        //    {
        //        if (!hasPendingItems)
        //        {
        //            hasPendingItems = await _context.LocationLinkedUser
        //            .Where(i => i.UserId == user.Id
        //            && i.AttachmentStatus == AttachmentStatus.Pending)
        //            .AnyAsync();
        //        }
        //    }
        //    catch (Exception ex)
        //    { }

        //    if (hasPendingItems)
        //    {
        //        return PartialView("../Widgets/_PendingRequestButtonPartial");
        //    }
        //    return Ok();
        //}
        //[HttpGet]
        //public async Task<IActionResult> PendingRequestResolutionPartial()
        //{
        //    //get user 
        //    var user = (await _userManager.GetUserAsync(User));
        //    if (user.Id == Guid.Empty)
        //    {
        //        return NotFound();
        //    }

        //    //variables
        //    List<InstitutionLinkedUser> providerLinkedUserList = new List<InstitutionLinkedUser>();
        //    List<LocationLinkedUser> locationLinkedUserList = new List<LocationLinkedUser>();
        //    List<InstitutionLinkedProfessional> providerLinkedProfessionalList = new List<InstitutionLinkedProfessional>();
        //    //get professional linked to user
        //    Professional professional = await _context.ProfessionalLinkedUser
        //        .Include(p => p.Professional)
        //        .Where(p => p.UserId == user.Id)
        //        .Select(i => i.Professional)
        //        .FirstOrDefaultAsync();


        //    //test to see if user or professional has any pending items.
        //    providerLinkedProfessionalList = await _context.InstitutionLinkedProfessional
        //        .Include(i => i.Professional)
        //        .Include(p => p.Institution)
        //        .Include(l => l.Location)
        //        .Where(i => i.Professional.Id == professional.Id
        //        && i.AttachmentStatus == AttachmentStatus.Pending)
        //        .ToListAsync();

        //    providerLinkedUserList = await _context.InstitutionLinkedUser
        //        .Include(p => p.Institution)
        //        .Where(i => i.UserId == user.Id
        //        && i.AttachmentStatus == AttachmentStatus.Pending)
        //        .ToListAsync();

        //    locationLinkedUserList = await _context.LocationLinkedUser
        //        .Include(l => l.Location)
        //        .Where(i => i.UserId == user.Id
        //        && i.AttachmentStatus == AttachmentStatus.Pending)
        //        .ToListAsync();

        //    PendingRequestViewModel vm = new PendingRequestViewModel()
        //    {
        //        InstitutionLinkedUserList = providerLinkedUserList,
        //        LocationLinkedUserList = locationLinkedUserList,
        //        InstitutionLinkedProfessionalList = providerLinkedProfessionalList
        //    };

        //    return PartialView("../Widgets/_PendingRequestModalPartial", vm);
        //}
        //public async Task<IActionResult> PendingRequestResolutionPartialResponse(long requestId, AttachmentType attachmentType, AttachmentStatus attachmentStatus)
        //{
        //    //get user 
        //    var user = (await _userManager.GetUserAsync(User));
        //    if (user.Id == Guid.Empty)
        //    {
        //        return NotFound();
        //    }

        //    //variables
        //    InstitutionLinkedUser providerLinkedUser = new InstitutionLinkedUser();
        //    LocationLinkedUser locationLinkedUser = new LocationLinkedUser();
        //    InstitutionLinkedProfessional providerLinkedProfessional = new InstitutionLinkedProfessional();
        //    bool matchingCredentials = false;
        //    //get professional linked to user
        //    Professional professional = await _context.ProfessionalLinkedUser
        //        .Include(p => p.Professional)
        //        .Where(p => p.UserId == user.Id)
        //        .Select(i => i.Professional)
        //        .FirstOrDefaultAsync();

        //    switch (attachmentType)
        //    {
        //        case AttachmentType.ProfessionalToInstitution:
        //            providerLinkedProfessional = await _context.InstitutionLinkedProfessional.Include(p => p.Professional).Where(i => i.Id == requestId).FirstAsync();
        //            //break;
        //        case AttachmentType.UserToLocation:
        //            locationLinkedUser = await _context.LocationLinkedUser.FindAsync(requestId);
        //            //break;
        //        case AttachmentType.UserToInstitution:
        //            providerLinkedUser = await _context.InstitutionLinkedUser.FindAsync(requestId);
        //            //break;
        //        default:
        //            return BadRequest();
        //    }

        //    if (providerLinkedUser.Id != 0)
        //    {
        //        if (providerLinkedUser.UserId == user.Id)
        //        {
        //            matchingCredentials = true;
        //        }
        //    }
        //    else if (locationLinkedUser.Id != 0)
        //    {
        //        if (locationLinkedUser.UserId == user.Id)
        //        {
        //            matchingCredentials = true;
        //        }
        //    }
        //    else if (providerLinkedProfessional.Id != 0)
        //    {
        //        if (providerLinkedProfessional.Professional.UUID == professional.UUID)
        //        {
        //            matchingCredentials = true;
        //        }
        //    }
        //    else
        //    {
        //        return BadRequest();
        //    }

        //    if (!matchingCredentials)
        //    {
        //        return BadRequest();
        //    }

        //    if (providerLinkedUser.Id != 0)
        //    {
        //        if (attachmentStatus == AttachmentStatus.Attached)
        //        {
        //            providerLinkedUser.AttachmentStatus = AttachmentStatus.Attached;
        //            _context.InstitutionLinkedUser.Update(providerLinkedUser);
        //            await _context.SaveChangesAsync();
        //        }
        //        else if (attachmentStatus == AttachmentStatus.Rejected)
        //        {
        //            providerLinkedUser.AttachmentStatus = AttachmentStatus.Rejected;
        //            _context.InstitutionLinkedUser.Remove(providerLinkedUser);
        //            await _context.SaveChangesAsync();
        //        }
        //    }
        //    else if (locationLinkedUser.Id != 0)
        //    {
        //        if (attachmentStatus == AttachmentStatus.Attached)
        //        {
        //            locationLinkedUser.AttachmentStatus = AttachmentStatus.Attached;
        //            _context.LocationLinkedUser.Update(locationLinkedUser);
        //            await _context.SaveChangesAsync();
        //        }
        //        else if (attachmentStatus == AttachmentStatus.Rejected)
        //        {
        //            locationLinkedUser.AttachmentStatus = AttachmentStatus.Rejected;
        //            _context.LocationLinkedUser.Remove(locationLinkedUser);
        //            await _context.SaveChangesAsync();
        //        }
        //    }
        //    else if (providerLinkedProfessional.Id != 0)
        //    {
        //        if (attachmentStatus == AttachmentStatus.Attached)
        //        {
        //            providerLinkedProfessional.AttachmentStatus = AttachmentStatus.Attached;
        //            _context.InstitutionLinkedProfessional.Update(providerLinkedProfessional);
        //            await _context.SaveChangesAsync();
        //        }
        //        else if (attachmentStatus == AttachmentStatus.Rejected)
        //        {
        //            providerLinkedProfessional.AttachmentStatus = AttachmentStatus.Rejected;
        //            _context.InstitutionLinkedProfessional.Remove(providerLinkedProfessional);
        //            await _context.SaveChangesAsync();
        //        }
        //    }
        //    else
        //    {
        //        return BadRequest();
        //    }

        //    return Ok();
        //}
        #endregion

        #region - Map partial (removed)
        // MapPartial REMOVED (ROADMAP 18, 2026-08-23). It served Views/Widget/_MapPartial, a Leaflet
        // map broken at three independent levels: leaflet.js was never vendored, the GeoDataUrls
        // binding was commented out so the tile host rendered empty, and the partial emitted no
        // .mapData markers for MapPlotter.js to draw. Its only caller was Location/Index, which
        // nothing links to. The owner ruled "remove the map surface, do not vendor the library".


        /// <summary>
        /// This does not seem like it is really nessicaryy
        /// </summary>
        /// <returns></returns>
        //[HttpGet]
        //[ResponseCache(Duration = 30)]
        //public async Task<IActionResult> LocationMapMarkers()
        //{
        //    ArrayList directory = new ArrayList();

        //    var list = await _context.InstitutionLinkedLocation
        //        .Include(p => p.Institution)
        //        .Include(l => l.Location)
        //        .ToListAsync();

        //    foreach (var item in list)
        //    {
        //        var link = new string[]
        //        {
        //            item.Location.Id.ToString(),
        //            item.Institution.Logo
        //        };
        //        directory.Add(link);
        //    }

        //    return Json(directory);
        //}

        #endregion

        #region Charts
        #region - DAU chart - in use
        ////***********************************************************************************************************************************
        //// DAU Widget        
        ////***********************************************************************************************************************************
        //[Authorize(Roles = Febris.Constants.RoleConstants.FebrisEmployeeAndSystemAdmins)]
        //public async Task<string> DAUCSV(long? providerId, long? locationId)
        //{
        //    var sb = new StringBuilder();
        //    sb.AppendLine("date, DAU");


        //    bool isFebris = _security.FebrisAuthorityCheck(User);
        //    //GeneralWidgetViewModel vm = new GeneralWidgetViewModel();
        //    DailyActiveUsers dailyActiveUsers = new DailyActiveUsers();
        //    List<DailyActiveUsers> dailyActiveUserList = new List<DailyActiveUsers>();
        //    //int activeUsers = 0;


        //    if (isFebris == true)
        //    {
        //        dailyActiveUserList = await _applicationUser.DailyActiveUsers.ToListAsync();
        //        dailyActiveUserList = dailyActiveUserList.OrderBy(x => x.Date.Date).ToList();
        //    }
        //    else if (providerId != null)
        //    {
        //        //Need to update database if this is somthing we want to do. and relate everything to the provider           


        //        ////get provider
        //        //HealthCareProvider provider = await _context.HealthCareProvider.FindAsync(providerId);
        //        ////get list of users tied to this provider
        //        //var users = await _context.UserHealthCareProvider.SelectMany(u => u.UserId).ToListAsync();
        //        ////get the DAU for that specific provider

        //        ////order them by date


        //    }
        //    else if (locationId != null)
        //    {
        //        //same thing here
        //    }
        //    else
        //    {

        //    }


        //    foreach (var entry in dailyActiveUserList)
        //    {
        //        sb.AppendLine(String.Join(",", entry.Date.Year + "-" + entry.Date.Month + "-" + entry.Date.Day, entry.DAUCount));
        //    }

        //    return (sb.ToString());
        //}
        //[Authorize(Roles = Febris.Constants.RoleConstants.FebrisEmployeeAndSystemAdmins)]
        //public async Task<IActionResult> DAUWidgetPartial()
        //{
        //    return PartialView("../Widgets/_DAUWidgetPartial");
        //}

        #endregion

        #region - Active user widget - in use (needs work)
        ////***********************************************************************************************************************************
        ////Active Users
        ////***********************************************************************************************************************************
        //[Authorize(Roles = Febris.Constants.RoleConstants.FebrisEmployeeAndSystemAdmins)]
        //public async Task<IActionResult> ActiveUsersWidgetPartial(long? providerId)
        //{
        //    bool isFebris = _security.FebrisAuthorityCheck(User);
        //    GeneralWidgetViewModel vm = new GeneralWidgetViewModel();
        //    IEnumerable<UserSecurityAndTracking> userList = await _applicationUser.UserSecurityAndTracking.ToListAsync();
        //    int activeUsers = 0;
        //    int allUsers = 0;

        //    if (isFebris == true)
        //    {
        //        //THIS IS BROKEN BECAUSE OF UPGRADE TO 3.0
        //        //activeUsers = await _applicationUser.ApplicationUser.Where(t => t.LastLoginTime.ToUniversalTime() > DateTime.Now.ToUniversalTime().AddMinutes(-30)).CountAsync();
        //        //allUsers = await _applicationUser.ApplicationUser.CountAsync();

        //        activeUsers = userList.Where(t => t.LastLoginTime.ToUniversalTime() > DateTime.Now.ToUniversalTime().AddMinutes(-30)).Count();
        //        allUsers = userList.Count();

        //        vm = new GeneralWidgetViewModel()
        //        {
        //            CurrentCount = activeUsers,
        //            TotalCount = allUsers
        //        };
        //    }
        //    else
        //    {
        //        //check permissions
        //        var providerUsers = await _context.InstitutionLinkedUser.Where(p => p.Institution.Id == providerId).Select(u => u.UserId).ToListAsync();

        //        foreach (var user in providerUsers)
        //        {
        //            if (true == userList.Where(t => t.UserId == user && t.LastLoginTime.ToUniversalTime() > DateTime.Now.ToUniversalTime().AddMinutes(-30)).Any())
        //            {
        //                activeUsers++;
        //            }
        //        }

        //        //broken because of upgrade
        //        //activeUsers = await _applicationUser.ApplicationUser.Where(t => t.LastLoginTime.ToUniversalTime() > DateTime.Now.ToUniversalTime().AddMinutes(-30)).CountAsync();
        //        //activeUsers = providerUsers.Where(t => t.LastLoginTime.ToUniversalTime() > DateTime.Now.ToUniversalTime().AddMinutes(-30)).Count();
        //        allUsers = providerUsers.Count();


        //        vm = new GeneralWidgetViewModel()
        //        {
        //            CurrentCount = activeUsers,
        //            TotalCount = allUsers
        //        };
        //    }



        //    return PartialView("../Widgets/_ActiveUsersWidgetPartial", vm);
        //}
        #endregion

        #region Total User - in use
        ////***********************************************************************************************************************************
        ////Admin User Count Chart
        ////***********************************************************************************************************************************
        ////[Authorize(Roles = Febris.Constants.RoleConstants.FebrisSystemAdminsSpaced)]
        //[Authorize(Roles = Febris.Constants.RoleConstants.OrgStaffLegacy)]
        //public async Task<string> TotalUserCountCSV(long? providerId)//----------------------This is still in use. 
        //{
        //    var sb = new StringBuilder();
        //    sb.AppendLine("date,users added that day,total users");

        //    bool isFebris = _security.FebrisAuthorityCheck(User);


        //    //variables 
        //    GeneralWidgetViewModel vm = new GeneralWidgetViewModel();
        //    List<GeneralWidgetViewModel> vmList = new List<GeneralWidgetViewModel>();
        //    List<UserSecurityAndTracking> userList = new List<UserSecurityAndTracking>();
        //    int allUsers = 0;

        //    //pull all users


        //    if (isFebris == true && providerId == null)
        //    {
        //        userList = await _applicationUser.UserSecurityAndTracking.ToListAsync();
        //        userList = userList.OrderBy(x => x.RegistrationDate.Date).ToList();
        //    }
        //    else
        //    {
        //        //set up for provider 
        //        var providerUsers = await _context.InstitutionLinkedUser.Where(p => p.Institution.Id == providerId).Select(u => u.UserId).ToListAsync();
        //        //pull provider users
        //        foreach (var user in providerUsers)
        //        {
        //            userList.AddRange(await _applicationUser.UserSecurityAndTracking.Where(u => u.UserId == user).ToListAsync());
        //        }

        //    }


        //    //can pull febris logic out here so it can be reused
        //    foreach (var user in userList)
        //    {
        //        if (vmList.Any(d => d.DateTime.Day == user.RegistrationDate.Day))
        //        {
        //            allUsers++;
        //            var SingleUserCount = vmList.Single(d => d.DateTime.Day == user.RegistrationDate.Day);
        //            SingleUserCount.CurrentCount++;
        //            SingleUserCount.TotalCount = allUsers;
        //        }
        //        //if not create that date tally
        //        else
        //        {
        //            allUsers++;
        //            vmList.Add(
        //                new GeneralWidgetViewModel()
        //                {
        //                    DateTime = user.RegistrationDate,
        //                    CurrentCount = 1,
        //                    TotalCount = allUsers
        //                });
        //        }
        //    }


        //    foreach (var entry in vmList)
        //    {
        //        sb.AppendLine(String.Join(",", entry.DateTime.Year + "-" + entry.DateTime.Month + "-" + entry.DateTime.Day, entry.CurrentCount, entry.TotalCount));
        //    }

        //    return (sb.ToString());
        //}
        //[Authorize(Roles = Febris.Constants.RoleConstants.OrgStaffLegacy)]
        //public async Task<IActionResult> TotalUserCountPartial()
        //{
        //    return PartialView("../Widgets/_UserCountPartial");
        //}
        #endregion

        #region - Pie charts - in use
        ////***********************************************************************************************************************************
        ////PieChart building                
        ////***********************************************************************************************************************************
        //public async Task<IActionResult> StatementPieChart(long? providerId, long? professionalId, long? locationId, long? testId, long? statementId, long? eduOrgId, bool isTest)
        //{
        //    //***********************************************************************************************************************************
        //    //Variables
        //    //***********************************************************************************************************************************
        //    bool isFebris = _security.FebrisAuthorityCheck(User);
        //    PieChartViewModel vm = new PieChartViewModel();
        //    List<Statement> statementList = new List<Statement>();
        //    int positiveCount = 0;
        //    int negativeCount = 0;
        //    int neutralCount = 0;
        //    int neutralCount2 = 0;
        //    int neutralCount3 = 0;
        //    int totalCount = 0;
        //    string positiveLabel = string.Empty;
        //    string negativeLabel = string.Empty;
        //    string neutralLabel = string.Empty;
        //    string neutralLabel2 = string.Empty;
        //    string neutralLabel3 = string.Empty;
        //    //get needed verbs
        //    Verb attemptVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("https://febr.is/xAPI/VerbDetails/Attempted")).FirstAsync();
        //    Verb completedVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("https://febr.is/xAPI/VerbDetails/Completed")).FirstAsync();
        //    Verb initializedVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("https://febr.is/xAPI/VerbDetails/Initialized")).FirstAsync();
        //    Verb terminatedVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("https://febr.is/xAPI/VerbDetails/Terminated")).FirstAsync();
        //    Verb passVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("https://febr.is/xAPI/VerbDetails/Pass")).FirstAsync();
        //    Verb notPassVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("https://febr.is/xAPI/VerbDetails/Not_Pass")).FirstAsync();
        //    Verb voidedVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("http://adlnet.gov/expapi/verbs/voided")).FirstAsync();



        //    //***********************************************************************************************************************************
        //    //getting overall for front page
        //    //***********************************************************************************************************************************
        //    if (isFebris == true && providerId == null && professionalId == null && locationId == null && testId == null && statementId == null && eduOrgId == null)
        //    {
        //        statementList = await _xAPIContext.Statement
        //            .Include(o => o.Object)
        //            .Include(d => d.Object.Definition)
        //            .Include(r => r.Result)
        //            .Include(r => r.Result.Score)
        //            .Include(v => v.Verb)
        //            .ToListAsync();
        //    }
        //    //***********************************************************************************************************************************
        //    // Get data by Statement
        //    //***********************************************************************************************************************************
        //    else if (statementId != null)
        //    {
        //        Statement statement = await _xAPIContext.Statement
        //            .Include(o => o.Object)
        //            .Include(d => d.Object.Definition)
        //            .Include(r => r.Result)
        //            .Include(r => r.Result.Score)
        //            .Include(v => v.Verb)
        //            .Where(s => s.Id == statementId)
        //            .FirstAsync();

        //        statementList = await _xAPIContext.Statement
        //            .Include(o => o.Object)
        //            .Include(d => d.Object.Definition)
        //            .Include(r => r.Result)
        //            .Include(r => r.Result.Score)
        //            .Include(v => v.Verb)
        //            .Where(o => o.Object.Key == statement.Object.Key)
        //            .ToListAsync();
        //        ModuleBase test = await _context.ModuleBaseLinkedObject
        //            .Include(t => t.ModuleBase)
        //            .Where(o => o.ObjectId == statement.Object.Key)
        //            .Select(t => t.ModuleBase)
        //            .FirstOrDefaultAsync();
        //        if (test.IsTest == true)
        //        {
        //            positiveLabel = "Pass";
        //            negativeLabel = "Not Pass";
        //            neutralLabel = "Terminated";
        //            neutralLabel2 = "Initialized";
        //            neutralLabel3 = "Voided";
        //            positiveCount = statementList.Where(p => p.Verb.Key == passVerb.Key).Count();
        //            negativeCount = statementList.Where(p => p.Verb.Key == notPassVerb.Key).Count();
        //            neutralCount = statementList.Where(p => p.Verb.Key == terminatedVerb.Key).Count();
        //            neutralCount2 = statementList.Where(p => p.Verb.Key == initializedVerb.Key).Count();
        //            neutralCount3 = statementList.Where(p => p.Verb.Key == voidedVerb.Key).Count();
        //            totalCount = statementList.Count();
        //            //positiveCount = statementList.Where(r => r.Result.Success == true).Count();
        //        }
        //        else
        //        {
        //            positiveLabel = "Completed";
        //            negativeLabel = "Attempted";
        //            neutralLabel = "Voided";
        //            positiveCount = statementList.Where(p => p.Verb.Key == completedVerb.Key).Count();
        //            negativeCount = statementList.Where(p => p.Verb.Key == attemptVerb.Key).Count();
        //            neutralCount = statementList.Where(p => p.Verb.Key == voidedVerb.Key).Count();
        //            totalCount = statementList.Count();
        //            //positiveCount = statementList.Where(r => r.Result.Completion==true).Count();
        //        }
        //    }
        //    //***********************************************************************************************************************************
        //    //get data by provider
        //    // need to filter statments by either test or training
        //    //***********************************************************************************************************************************
        //    else if (providerId != null)
        //    {
        //        Institution provider = await _context.Institution.FindAsync(providerId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProvider(provider);
        //    }
        //    //***********************************************************************************************************************************
        //    //get data by location
        //    // need to filter statments by either test or training
        //    //***********************************************************************************************************************************
        //    else if (locationId != null)
        //    {
        //        Location location = await _context.Location.FindAsync(locationId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByLocation(location);
        //    }
        //    //***********************************************************************************************************************************
        //    //get data by professional
        //    // need to filter statments by either test or training
        //    //***********************************************************************************************************************************
        //    else if (professionalId != null)
        //    {
        //        Professional professional = await _context.Professional.FindAsync(professionalId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProfessional(professional);
        //    }
        //    //***********************************************************************************************************************************
        //    //get data by edu org
        //    // need to filter statments by either test or training
        //    //***********************************************************************************************************************************
        //    else if (eduOrgId != null)
        //    {
        //        ModuleDeveloper educationOrganization = await _context.ModuleDeveloper.FindAsync(eduOrgId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByEducationOrganization(educationOrganization);
        //    }
        //    //***********************************************************************************************************************************
        //    //get data by test
        //    //***********************************************************************************************************************************
        //    else if (testId != null)
        //    {
        //        ModuleBase test = await _context.ModuleBase.FindAsync(testId);
        //        //List<Statement> statementList = new List<Statement>();
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByTest(test);
        //        if (test.IsTest == true)
        //        {
        //            positiveLabel = "Pass";
        //            negativeLabel = "Not Pass";
        //            neutralLabel = "Terminated";
        //            neutralLabel2 = "Initialized";
        //            neutralLabel3 = "Voided";
        //            positiveCount = statementList.Where(p => p.Verb.Key == passVerb.Key).Count();
        //            negativeCount = statementList.Where(p => p.Verb.Key == notPassVerb.Key).Count();
        //            neutralCount = statementList.Where(p => p.Verb.Key == terminatedVerb.Key).Count();
        //            neutralCount2 = statementList.Where(p => p.Verb.Key == initializedVerb.Key).Count();
        //            neutralCount3 = statementList.Where(p => p.Verb.Key == voidedVerb.Key).Count();
        //            totalCount = statementList.Count();
        //        }
        //        else
        //        {
        //            positiveLabel = "Completed";
        //            negativeLabel = "Attempted";
        //            neutralLabel = "Voided";
        //            positiveCount = statementList.Where(p => p.Verb.Key == completedVerb.Key).Count();
        //            negativeCount = statementList.Where(p => p.Verb.Key == attemptVerb.Key).Count();
        //            neutralCount = statementList.Where(p => p.Verb.Key == voidedVerb.Key).Count();
        //            totalCount = statementList.Count();
        //        }
        //    }


        //    //***********************************************************************************************************************************
        //    //pulling out data that is reused multipule times. 
        //    //***********************************************************************************************************************************     
        //    if (testId == null && statementId == null)
        //    {
        //        (statementList, StatusMessage) = await _statementHandler.StatementListFilter(statementList, isTest);
        //        if (isTest == true)
        //        {
        //            positiveLabel = "Pass";
        //            negativeLabel = "Not Pass";
        //            neutralLabel = "Terminated";
        //            neutralLabel2 = "Initialized";
        //            neutralLabel3 = "Voided";
        //            positiveCount = statementList.Where(p => p.Verb.Key == passVerb.Key).Count();
        //            negativeCount = statementList.Where(p => p.Verb.Key == notPassVerb.Key).Count();
        //            neutralCount = statementList.Where(p => p.Verb.Key == terminatedVerb.Key).Count();
        //            neutralCount2 = statementList.Where(p => p.Verb.Key == initializedVerb.Key).Count();
        //            neutralCount3 = statementList.Where(p => p.Verb.Key == voidedVerb.Key).Count();
        //            totalCount = statementList.Count();
        //        }
        //        else
        //        {
        //            positiveLabel = "Completed";
        //            negativeLabel = "Attempted";
        //            neutralLabel = "Voided";
        //            positiveCount = statementList.Where(p => p.Verb.Key == completedVerb.Key).Count();
        //            negativeCount = statementList.Where(p => p.Verb.Key == attemptVerb.Key).Count();
        //            neutralCount = statementList.Where(p => p.Verb.Key == voidedVerb.Key).Count();
        //            totalCount = statementList.Count();
        //        }
        //    }

        //    vm = new PieChartViewModel()
        //    {
        //        PositiveCount = positiveCount,
        //        PositiveLabel = positiveLabel,
        //        NegativeCount = negativeCount,
        //        NegativeLabel = negativeLabel,
        //        NeutralCount = neutralCount,
        //        NeutralLabel = neutralLabel,
        //        NeutralCount2 = neutralCount2,
        //        NeutralLabel2 = neutralLabel2,
        //        NeutralCount3 = neutralCount3,
        //        NeutralLabel3 = neutralLabel3,
        //        TotalCount = totalCount
        //    };

        //    return PartialView("../Widgets/_StatementPieChartPartial", vm);
        //}

        ////***********************************************************************************************************************************
        ////PieChart building                
        ////***********************************************************************************************************************************
        //public async Task<IActionResult> SecondStatementPieChart(long? providerId, long? professionalId, long? locationId, long? testId, long? statementId, long? eduOrgId, bool isTest)
        //{
        //    //***********************************************************************************************************************************
        //    //Variables
        //    //***********************************************************************************************************************************
        //    bool isFebris = _security.FebrisAuthorityCheck(User);
        //    PieChartViewModel vm = new PieChartViewModel();
        //    List<Statement> statementList = new List<Statement>();
        //    int positiveCount = 0;
        //    int negativeCount = 0;
        //    int neutralCount = 0;
        //    int neutralCount2 = 0;
        //    int neutralCount3 = 0;
        //    int totalCount = 0;
        //    string positiveLabel = string.Empty;
        //    string negativeLabel = string.Empty;
        //    string neutralLabel = string.Empty;
        //    string neutralLabel2 = string.Empty;
        //    string neutralLabel3 = string.Empty;
        //    //get needed verbs
        //    Verb attemptVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("https://febr.is/xAPI/VerbDetails/Attempted")).FirstAsync();
        //    Verb completedVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("https://febr.is/xAPI/VerbDetails/Completed")).FirstAsync();
        //    Verb initializedVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("https://febr.is/xAPI/VerbDetails/Initialized")).FirstAsync();
        //    Verb terminatedVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("https://febr.is/xAPI/VerbDetails/Terminated")).FirstAsync();
        //    Verb passVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("https://febr.is/xAPI/VerbDetails/Pass")).FirstAsync();
        //    Verb notPassVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("https://febr.is/xAPI/VerbDetails/Not_Pass")).FirstAsync();
        //    Verb voidedVerb = await _xAPIContext.Verb.Where(x => x.Id == new Uri("http://adlnet.gov/expapi/verbs/voided")).FirstAsync();



        //    //***********************************************************************************************************************************
        //    //getting overall for front page
        //    //***********************************************************************************************************************************
        //    if (isFebris == true && providerId == null && professionalId == null && locationId == null && testId == null && statementId == null && eduOrgId == null)
        //    {
        //        statementList = await _xAPIContext.Statement
        //            .Include(o => o.Object)
        //            .Include(d => d.Object.Definition)
        //            .Include(r => r.Result)
        //            .Include(r => r.Result.Score)
        //            .Include(v => v.Verb)
        //            .ToListAsync();
        //    }
        //    //***********************************************************************************************************************************
        //    // Get data by Statement
        //    //***********************************************************************************************************************************
        //    else if (statementId != null)
        //    {
        //        Statement statement = await _xAPIContext.Statement
        //            .Include(o => o.Object)
        //            .Include(d => d.Object.Definition)
        //            .Include(r => r.Result)
        //            .Include(r => r.Result.Score)
        //            .Include(v => v.Verb)
        //            .Where(s => s.Id == statementId)
        //            .FirstAsync();

        //        statementList = await _xAPIContext.Statement
        //            .Include(o => o.Object)
        //            .Include(d => d.Object.Definition)
        //            .Include(r => r.Result)
        //            .Include(r => r.Result.Score)
        //            .Include(v => v.Verb)
        //            .Where(o => o.Object.Key == statement.Object.Key)
        //            .ToListAsync();
        //        ModuleBase test = await _context.ModuleBaseLinkedObject
        //            .Include(t => t.ModuleBase)
        //            .Where(o => o.ObjectId == statement.Object.Key)
        //            .Select(t => t.ModuleBase)
        //            .FirstOrDefaultAsync();
        //        if (test.IsTest == true)
        //        {
        //            positiveLabel = "Pass";
        //            negativeLabel = "Not Pass";
        //            neutralLabel = "Terminated";
        //            neutralLabel2 = "Initialized";
        //            neutralLabel3 = "Voided";
        //            positiveCount = statementList.Where(p => p.Verb.Key == passVerb.Key).Count();
        //            negativeCount = statementList.Where(p => p.Verb.Key == notPassVerb.Key).Count();
        //            neutralCount = statementList.Where(p => p.Verb.Key == terminatedVerb.Key).Count();
        //            neutralCount2 = statementList.Where(p => p.Verb.Key == initializedVerb.Key).Count();
        //            neutralCount3 = statementList.Where(p => p.Verb.Key == voidedVerb.Key).Count();
        //            totalCount = statementList.Count();
        //            //positiveCount = statementList.Where(r => r.Result.Success == true).Count();
        //        }
        //        else
        //        {
        //            positiveLabel = "Completed";
        //            negativeLabel = "Attempted";
        //            neutralLabel = "Voided";
        //            positiveCount = statementList.Where(p => p.Verb.Key == completedVerb.Key).Count();
        //            negativeCount = statementList.Where(p => p.Verb.Key == attemptVerb.Key).Count();
        //            neutralCount = statementList.Where(p => p.Verb.Key == voidedVerb.Key).Count();
        //            totalCount = statementList.Count();
        //            //positiveCount = statementList.Where(r => r.Result.Completion==true).Count();
        //        }
        //    }
        //    //***********************************************************************************************************************************
        //    //get data by provider
        //    // need to filter statments by either test or training
        //    //***********************************************************************************************************************************
        //    else if (providerId != null)
        //    {
        //        Institution provider = await _context.Institution.FindAsync(providerId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProvider(provider);
        //    }
        //    //***********************************************************************************************************************************
        //    //get data by location
        //    // need to filter statments by either test or training
        //    //***********************************************************************************************************************************
        //    else if (locationId != null)
        //    {
        //        Location location = await _context.Location.FindAsync(locationId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByLocation(location);
        //    }
        //    //***********************************************************************************************************************************
        //    //get data by professional
        //    // need to filter statments by either test or training
        //    //***********************************************************************************************************************************
        //    else if (professionalId != null)
        //    {
        //        Professional professional = await _context.Professional.FindAsync(professionalId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProfessional(professional);
        //    }
        //    //***********************************************************************************************************************************
        //    //get data by edu org
        //    // need to filter statments by either test or training
        //    //***********************************************************************************************************************************
        //    else if (eduOrgId != null)
        //    {
        //        ModuleDeveloper educationOrganization = await _context.ModuleDeveloper.FindAsync(eduOrgId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByEducationOrganization(educationOrganization);
        //    }
        //    //***********************************************************************************************************************************
        //    //get data by test
        //    //***********************************************************************************************************************************
        //    else if (testId != null)
        //    {
        //        ModuleBase test = await _context.ModuleBase.FindAsync(testId);
        //        //List<Statement> statementList = new List<Statement>();
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByTest(test);
        //        if (test.IsTest == true)
        //        {
        //            positiveLabel = "Pass";
        //            negativeLabel = "Not Pass";
        //            neutralLabel = "Terminated";
        //            neutralLabel2 = "Initialized";
        //            neutralLabel3 = "Voided";
        //            positiveCount = statementList.Where(p => p.Verb.Key == passVerb.Key).Count();
        //            negativeCount = statementList.Where(p => p.Verb.Key == notPassVerb.Key).Count();
        //            neutralCount = statementList.Where(p => p.Verb.Key == terminatedVerb.Key).Count();
        //            neutralCount2 = statementList.Where(p => p.Verb.Key == initializedVerb.Key).Count();
        //            neutralCount3 = statementList.Where(p => p.Verb.Key == voidedVerb.Key).Count();
        //            totalCount = statementList.Count();
        //        }
        //        else
        //        {
        //            positiveLabel = "Completed";
        //            negativeLabel = "Attempted";
        //            neutralLabel = "Voided";
        //            positiveCount = statementList.Where(p => p.Verb.Key == completedVerb.Key).Count();
        //            negativeCount = statementList.Where(p => p.Verb.Key == attemptVerb.Key).Count();
        //            neutralCount = statementList.Where(p => p.Verb.Key == voidedVerb.Key).Count();
        //            totalCount = statementList.Count();
        //        }
        //    }


        //    //***********************************************************************************************************************************
        //    //pulling out data that is reused multipule times. 
        //    //***********************************************************************************************************************************     
        //    if (testId == null && statementId == null)
        //    {
        //        (statementList, StatusMessage) = await _statementHandler.StatementListFilter(statementList, isTest);
        //        if (isTest == true)
        //        {
        //            positiveLabel = "Pass";
        //            negativeLabel = "Not Pass";
        //            neutralLabel = "Terminated";
        //            neutralLabel2 = "Initialized";
        //            neutralLabel3 = "Voided";
        //            positiveCount = statementList.Where(p => p.Verb.Key == passVerb.Key).Count();
        //            negativeCount = statementList.Where(p => p.Verb.Key == notPassVerb.Key).Count();
        //            neutralCount = statementList.Where(p => p.Verb.Key == terminatedVerb.Key).Count();
        //            neutralCount2 = statementList.Where(p => p.Verb.Key == initializedVerb.Key).Count();
        //            neutralCount3 = statementList.Where(p => p.Verb.Key == voidedVerb.Key).Count();
        //            totalCount = statementList.Count();
        //        }
        //        else
        //        {
        //            positiveLabel = "Completed";
        //            negativeLabel = "Attempted";
        //            neutralLabel = "Voided";
        //            positiveCount = statementList.Where(p => p.Verb.Key == completedVerb.Key).Count();
        //            negativeCount = statementList.Where(p => p.Verb.Key == attemptVerb.Key).Count();
        //            neutralCount = statementList.Where(p => p.Verb.Key == voidedVerb.Key).Count();
        //            totalCount = statementList.Count();
        //        }
        //    }

        //    vm = new PieChartViewModel()
        //    {
        //        PositiveCount = positiveCount,
        //        PositiveLabel = positiveLabel,
        //        NegativeCount = negativeCount,
        //        NegativeLabel = negativeLabel,
        //        NeutralCount = neutralCount,
        //        NeutralLabel = neutralLabel,
        //        NeutralCount2 = neutralCount2,
        //        NeutralLabel2 = neutralLabel2,
        //        NeutralCount3 = neutralCount3,
        //        NeutralLabel3 = neutralLabel3,
        //        TotalCount = totalCount
        //    };

        //    return PartialView("../Widgets/_SecondStatementPieChartPartial", vm);
        //}
        #endregion

        #region - radar chart - does not seem used
        ////***********************************************************************************************************************************
        ////Radar Chart for statement scoring
        ////Todo: add in average score for compairing agaisnt score
        ////***********************************************************************************************************************************
        ////public async Task<IActionResult> SingleStatementResultRadarChart()
        ////{
        ////    return PartialView("../Widgets/_StatementResultRadarChart");
        ////}
        //#endregion

        //#region - Overall radar charts - in use
        ////***********************************************************************************************************************************
        ////Overall Radar Chart for statement        
        ////***********************************************************************************************************************************
        //public async Task<IActionResult> OverallStatmentResultRadarChart(long? providerId, long? professionalId, long? locationId, long? testId, long? statementId, long? eduOrgId, bool isTest)
        //{
        //    try
        //    {
        //        //***********************************************************************************************************************************
        //        //Variables
        //        //***********************************************************************************************************************************
        //        bool isFebris = _security.FebrisAuthorityCheck(User);
        //        List<Statement> statementList = new List<Statement>();
        //        Statement statement = new Statement();
        //        string positiveLabel = string.Empty;
        //        string neutralLabel = string.Empty;

        //        //get time estimate
        //        //int timeEstimate = await _context.TestToObjectLink
        //        //    .Include(t => t.Test)
        //        //    .Where(s => s.ObjectId == statement.Object.Key)
        //        //    .Select(t => t.Test.EstimatedCompletionTime)
        //        //    .FirstOrDefaultAsync();


        //        //***********************************************************************************************************************************
        //        //getting overall for front page
        //        //***********************************************************************************************************************************
        //        if (isFebris == true && providerId == null && professionalId == null && locationId == null && testId == null && statementId == null && eduOrgId == null)
        //        {
        //            statementList = await _xAPIContext.Statement
        //            .Include(o => o.Object)
        //            .Include(v => v.Verb)
        //            .Include(r => r.Result)
        //            .Include(s => s.Result.Score)
        //            .Include(s => s.Result.Extensions)
        //            .ToListAsync();
        //        }
        //        //***********************************************************************************************************************************
        //        // Get data by Statement
        //        //  -- This needs to be rethought
        //        //***********************************************************************************************************************************
        //        else if (statementId != null)
        //        {
        //            statement = await _xAPIContext.Statement
        //            .Include(o => o.Object)
        //            .Include(v => v.Verb)
        //            .Include(r => r.Result)
        //            .Include(s => s.Result.Score)
        //            .Include(s => s.Result.Extensions)
        //            .Where(i => i.Id == statementId)
        //            .FirstAsync();

        //            statementList = await _xAPIContext.Statement
        //                .Include(o => o.Object)
        //                .Include(v => v.Verb)
        //                .Include(r => r.Result)
        //                .Include(s => s.Result.Score)
        //                .Include(s => s.Result.Extensions)
        //                .Where(o => o.Object.Key == statement.Object.Key)
        //                .ToListAsync();
        //        }
        //        //***********************************************************************************************************************************
        //        //get data by provider
        //        //***********************************************************************************************************************************
        //        else if (providerId != null)
        //        {
        //            Institution provider = await _context.Institution.FindAsync(providerId);
        //            (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProvider(provider);
        //        }
        //        //***********************************************************************************************************************************
        //        //get data by location                
        //        //***********************************************************************************************************************************
        //        else if (locationId != null)
        //        {
        //            Location location = await _context.Location.FindAsync(locationId);
        //            (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByLocation(location);
        //        }
        //        //***********************************************************************************************************************************
        //        //get data by professional
        //        //***********************************************************************************************************************************
        //        else if (professionalId != null)
        //        {
        //            Professional professional = await _context.Professional.FindAsync(professionalId);
        //            (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProfessional(professional);
        //        }
        //        //***********************************************************************************************************************************
        //        //get data by edu org
        //        // need to filter statments by either test or training
        //        //***********************************************************************************************************************************
        //        else if (eduOrgId != null)
        //        {
        //            ModuleDeveloper educationOrganization = await _context.ModuleDeveloper.FindAsync(eduOrgId);
        //            (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByEducationOrganization(educationOrganization);
        //        }
        //        //***********************************************************************************************************************************
        //        //get data by test
        //        //***********************************************************************************************************************************
        //        else if (testId != null)
        //        {
        //            ModuleBase test = await _context.ModuleBase.FindAsync(testId);
        //            (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByTest(test);
        //        }




        //        //Build average list
        //        float score = 0;
        //        double duration = 0;
        //        double restartCount = 0;
        //        double successCount = 0;
        //        double completionCount = 0;
        //        //if (statementId == null)
        //        //{
        //        statementList = await _widgetHandler.FilterStatementsByType(isTest, statementList);
        //        (score, duration, restartCount, successCount, completionCount, StatusMessage) = await _widgetHandler.GetAverageValuesForRadarChartFromStatementList(statementList);
        //        //}
        //        //else
        //        //{
        //        //    score = statement.Result.Score.Raw;
        //        //    duration = statement.Result.Duration.TotalMinutes;
        //        //    restartCount = 0;
        //        //    successCount = statement.Result.Success?1:0;
        //        //    completionCount = statement.Result.Completion?1:0;
        //        //}

        //        //Build vm - need new one
        //        RadarChartViewModel vm = new RadarChartViewModel()
        //        {
        //            ScoreAverage = score,
        //            DurationAverage = duration,
        //            RestartCountAverage = restartCount,
        //            SuccessCountAverage = successCount,
        //            CompletionCountAverage = completionCount,
        //            TimeEstimate = 30//get this from testbase
        //        };

        //        return PartialView("../Widgets/_OverallStatementRadarChart", vm);
        //    }
        //    catch (Exception)
        //    {

        //        throw;
        //    }
        //}
        ////***********************************************************************************************************************************
        ////Overall Radar Chart for statement        
        ////***********************************************************************************************************************************
        //public async Task<IActionResult> SecondOverallStatmentResultRadarChart(long? providerId, long? professionalId, long? locationId, long? testId, long? statementId, long? eduOrgId, bool isTest)
        //{
        //    try
        //    {
        //        //***********************************************************************************************************************************
        //        //Variables
        //        //***********************************************************************************************************************************
        //        bool isFebris = _security.FebrisAuthorityCheck(User);
        //        List<Statement> statementList = new List<Statement>();
        //        Statement statement = new Statement();
        //        string positiveLabel = string.Empty;
        //        string neutralLabel = string.Empty;

        //        //get time estimate
        //        //int timeEstimate = await _context.TestToObjectLink
        //        //    .Include(t => t.Test)
        //        //    .Where(s => s.ObjectId == statement.Object.Key)
        //        //    .Select(t => t.Test.EstimatedCompletionTime)
        //        //    .FirstOrDefaultAsync();


        //        //***********************************************************************************************************************************
        //        //getting overall for front page
        //        //***********************************************************************************************************************************
        //        if (isFebris == true && providerId == null && professionalId == null && locationId == null && testId == null && statementId == null && eduOrgId == null)
        //        {
        //            statementList = await _xAPIContext.Statement
        //                .Include(o => o.Object)
        //                .Include(v => v.Verb)
        //                .Include(r => r.Result)
        //                .Include(s => s.Result.Score)
        //                .Include(s => s.Result.Extensions)
        //                .ToListAsync();
        //        }
        //        //***********************************************************************************************************************************
        //        // Get data by Statement
        //        //  -- This needs to be rethought
        //        //***********************************************************************************************************************************
        //        else if (statementId != null)
        //        {
        //            statement = await _xAPIContext.Statement
        //            .Include(o => o.Object)
        //            .Include(v => v.Verb)
        //            .Include(r => r.Result)
        //            .Include(s => s.Result.Score)
        //            .Include(s => s.Result.Extensions)
        //            .Where(i => i.Id == statementId)
        //            .FirstAsync();


        //            //statementList = await _xAPIContext.Statement
        //            //    .Where(o => o.Object.Key == statement.Object.Key)
        //            //    .ToListAsync();
        //        }
        //        //***********************************************************************************************************************************
        //        //get data by provider
        //        //***********************************************************************************************************************************
        //        else if (providerId != null)
        //        {
        //            Institution provider = await _context.Institution.FindAsync(providerId);
        //            (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProvider(provider);
        //        }
        //        //***********************************************************************************************************************************
        //        //get data by location                
        //        //***********************************************************************************************************************************
        //        else if (locationId != null)
        //        {
        //            Location location = await _context.Location.FindAsync(locationId);
        //            (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByLocation(location);
        //        }
        //        //***********************************************************************************************************************************
        //        //get data by professional
        //        //***********************************************************************************************************************************
        //        else if (professionalId != null)
        //        {
        //            Professional professional = await _context.Professional.FindAsync(professionalId);
        //            (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProfessional(professional);
        //        }
        //        //***********************************************************************************************************************************
        //        //get data by edu org
        //        // need to filter statments by either test or training
        //        //***********************************************************************************************************************************
        //        else if (eduOrgId != null)
        //        {
        //            ModuleDeveloper educationOrganization = await _context.ModuleDeveloper.FindAsync(eduOrgId);
        //            (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByEducationOrganization(educationOrganization);
        //        }
        //        //***********************************************************************************************************************************
        //        //get data by test
        //        //***********************************************************************************************************************************
        //        else if (testId != null)
        //        {
        //            ModuleBase test = await _context.ModuleBase.FindAsync(testId);
        //            (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByTest(test);
        //        }


        //        //Build average list
        //        float score = 0;
        //        double duration = 0;
        //        double restartCount = 0;
        //        double successCount = 0;
        //        double completionCount = 0;
        //        if (statementId == null)
        //        {
        //            statementList = await _widgetHandler.FilterStatementsByType(isTest, statementList);
        //            (score, duration, restartCount, successCount, completionCount, StatusMessage) = await _widgetHandler.GetAverageValuesForRadarChartFromStatementList(statementList);
        //        }
        //        else
        //        {
        //            score = statement.Result.Score.Raw;
        //            duration = statement.Result.Duration.TotalMinutes;
        //            restartCount = 0;
        //            successCount = statement.Result.Success ? 1 : 0;
        //            completionCount = statement.Result.Completion ? 1 : 0;
        //        }

        //        //Build vm - need new one
        //        RadarChartViewModel vm = new RadarChartViewModel()
        //        {
        //            ScoreAverage = score,
        //            DurationAverage = duration,
        //            RestartCountAverage = restartCount,
        //            SuccessCountAverage = successCount,
        //            CompletionCountAverage = completionCount,
        //            TimeEstimate = 30
        //        };

        //        return PartialView("../Widgets/_SecondOverallStatementRadarChart", vm);
        //    }
        //    catch (Exception)
        //    {

        //        throw;
        //    }
        //}
        //#endregion

        //#region - Comparison Radar chart - does not seem to be used
        ////***********************************************************************************************************************************
        ////Comparison Radar Chart for specific statement        
        ////***********************************************************************************************************************************
        ////public async Task<IActionResult> ComparisonStatmentResultRadarChart(long? statementId)
        ////{
        ////    //get object from statement
        ////    Statement statement = await _xAPIContext.Statement
        ////        .Include(o => o.Object)
        ////        .Include(v => v.Verb)
        ////        .Include(r => r.Result)
        ////        .Include(s => s.Result.Score)
        ////        .Where(i => i.Id == statementId)
        ////        .FirstAsync();
        ////    //get time estimate
        ////    int timeEstimate = await _context.EducationBaseLinkedObject
        ////        .Include(t => t.EducationBase)
        ////        .Where(s => s.ObjectId == statement.Object.Key)
        ////        .Select(t => t.EducationBase.EstimatedCompletionTime)
        ////        .FirstOrDefaultAsync();
        ////    //Get list of statments with the same object
        ////    List<Statement> statementList = await _xAPIContext.Statement
        ////        .Include(o => o.Object)
        ////        .Include(v => v.Verb)
        ////        .Include(r => r.Result)
        ////        .Include(r => r.Result.Score)
        ////        .Where(o => o.Object.Key == statement.Object.Key)
        ////        .ToListAsync();
        ////    //Build average list
        ////    float score = 0;
        ////    double duration = 0;
        ////    double restartCount = 0;
        ////    double successCount = 0;
        ////    double completionCount = 0;
        ////    (score, duration, restartCount, successCount, completionCount, StatusMessage) = await _widgetHandler.GetAverageValuesForRadarChartFromStatementList(statementList);


        ////    ComparisonRadarChartViewModel vm = new ComparisonRadarChartViewModel()
        ////    {
        ////        Statement = statement,
        ////        ScoreAverage = score,
        ////        DurationAverage = duration,
        ////        RestartCountAverage = restartCount,
        ////        SuccessCountAverage = successCount,
        ////        CompletionCountAverage = completionCount,
        ////        TimeEstimate = timeEstimate
        ////    };

        ////    return PartialView("../Widgets/_StatementComparisonRadarChart", vm);
        ////}
        //#endregion

        //#region - Statement time chart - in use
        ////***********************************************************************************************************************************
        ////Total test hours
        ////***********************************************************************************************************************************
        //public async Task<string> TotalStatementTimeCSV(long? providerId, long? professionalId, long? locationId, long? testId, long? statementId, long? eduOrgId)
        //{
        //    var sb = new StringBuilder();
        //    sb.AppendLine("date, Total Test Time, Daily Test Time, Total Training Time, Daily Training Time");

        //    List<Statement> statementList = new List<Statement>();
        //    List<Statement> statementTestingList = new List<Statement>();
        //    List<Statement> statementTrainingList = new List<Statement>();
        //    bool isFebris = _security.FebrisAuthorityCheck(User);
        //    //GeneralWidgetViewModel vm = new GeneralWidgetViewModel();
        //    List<GeneralWidgetViewModel> vmList = new List<GeneralWidgetViewModel>();
        //    TimeSpan totalTestsTime = new TimeSpan();
        //    TimeSpan totalTrainingTime = new TimeSpan();
        //    //TimeSpan dailyTestTime = new TimeSpan();


        //    if (isFebris == true && providerId == null && professionalId == null && locationId == null && testId == null && statementId == null && eduOrgId == null)
        //    {
        //        statementList = await _xAPIContext.Statement
        //            .Include(o => o.Object)
        //            .Include(r => r.Result)
        //            .ToListAsync();
        //    }
        //    //else if (statementId != null)
        //    //{
        //    //    //user this to find test that are the same?
        //    //    Statement statement = await _xAPIContext.Statement
        //    //        .Include(o => o.Object)
        //    //        .Include(r => r.Result)
        //    //        .Where(i => i.Id == statementId)
        //    //        .FirstAsync();
        //    //    //.FindAsync(statementId);

        //    //    //HealthCareProvider provider = new HealthCareProvider();
        //    //    //(statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProvider(provider);
        //    //}
        //    else if (providerId != null)
        //    {
        //        Institution provider = await _context.Institution.FindAsync(providerId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProvider(provider);
        //    }
        //    else if (locationId != null)
        //    {
        //        Location location = await _context.Location.FindAsync(locationId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByLocation(location);
        //    }
        //    else if (professionalId != null)
        //    {
        //        Professional professional = await _context.Professional.FindAsync(professionalId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProfessional(professional);
        //    }
        //    else if (testId != null)
        //    {
        //        ModuleBase test = await _context.ModuleBase.FindAsync(testId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByTest(test);
        //    }
        //    else if (eduOrgId != null)
        //    {
        //        ModuleDeveloper educationOrganization = await _context.ModuleDeveloper.FindAsync(eduOrgId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByEducationOrganization(educationOrganization);
        //    }
        //    //***********************************************************************************************************************************            
        //    //may need cloning here. 
        //    //***********************************************************************************************************************************   
        //    //(statementTrainingList, StatusMessage) = await _statementHandler.StatementListFilter(statementList, false);
        //    (statementTestingList, StatusMessage) = await _statementHandler.StatementListFilter(statementList, true);
        //    //IEnumerable<Statement> trainingList = statementTrainingList.OrderBy(d => d.Timestamp).ToList();
        //    IEnumerable<Statement> testingList = statementTestingList.OrderBy(d => d.Timestamp).ToList();
        //    IEnumerable<Statement> fullList = statementList.OrderBy(d => d.Timestamp).ToList();
        //    //***********************************************************************************************************************************            
        //    //sort out data by date
        //    //*********************************************************************************************************************************** 
        //    foreach (var data in fullList)
        //    {
        //        bool isTest = testingList.Where(t => t.Id == data.Id).Any();


        //        if (data.Result != null && data.Result.Duration != null)
        //        {
        //            if (isTest)
        //            {
        //                totalTestsTime += (data.Result.Duration);
        //            }
        //            else
        //            {
        //                totalTrainingTime += (data.Result.Duration);
        //            }

        //            if (vmList.Any(d => d.DateTime.Day == data.Timestamp.Day))
        //            {
        //                GeneralWidgetViewModel singleDayTime = vmList.Single(d => d.DateTime.Day == data.Timestamp.Day);
        //                singleDayTime.TotalTimeCount = (double)totalTestsTime.TotalMinutes;
        //                singleDayTime.TotalTrainingTimeCount = (double)totalTrainingTime.TotalMinutes;
        //                TimeSpan tempTime = data.Result.Duration;
        //                if (isTest)
        //                {

        //                    if (singleDayTime.CurrentTimeCount.HasValue)
        //                    {
        //                        singleDayTime.CurrentTimeCount += (double)tempTime.TotalMinutes;
        //                    }
        //                    else
        //                    {
        //                        singleDayTime.CurrentTimeCount = (double)tempTime.TotalMinutes;
        //                    }

        //                    //singleDayTime.TotalTimeCount = (double)totalTestsTime.TotalMinutes;// + (double)tempTime.TotalMinutes;
        //                    //singleDayTime.TotalTrainingTimeCount = (double)totalTrainingTime.TotalMinutes;
        //                }
        //                else
        //                {
        //                    if (singleDayTime.CurrentTrainingTimeCount.HasValue)
        //                    {
        //                        singleDayTime.CurrentTrainingTimeCount += (double)tempTime.TotalMinutes;
        //                    }
        //                    else
        //                    {
        //                        singleDayTime.CurrentTrainingTimeCount = (double)tempTime.TotalMinutes;
        //                    }
        //                    //singleDayTime.TotalTimeCount = (double)totalTestsTime.TotalMinutes;
        //                    //singleDayTime.TotalTrainingTimeCount = (double)totalTrainingTime.TotalMinutes;// +(double)tempTime.TotalMinutes;
        //                }
        //            }
        //            else
        //            {
        //                GeneralWidgetViewModel vm = new GeneralWidgetViewModel()
        //                {
        //                    DateTime = data.Timestamp,
        //                    TotalTrainingTimeCount = (double)totalTrainingTime.TotalMinutes,
        //                    TotalTimeCount = (double)totalTestsTime.TotalMinutes
        //                };
        //                TimeSpan tempTime = data.Result.Duration;
        //                if (isTest)
        //                {
        //                    vm.CurrentTimeCount = (double)tempTime.TotalMinutes;
        //                    //vm.TotalTimeCount += (double)tempTime.TotalMinutes;
        //                }
        //                else
        //                {
        //                    vm.CurrentTrainingTimeCount = (double)tempTime.TotalMinutes;
        //                    //vm.TotalTrainingTimeCount += (double)tempTime.TotalMinutes;
        //                }
        //                vmList.Add(vm);
        //            }
        //        }
        //    }
        //    #region old way
        //    //foreach (var data in trainingList)
        //    //{
        //    //    if (vmList.Any(d => d.DateTime.Day == data.Timestamp.Day))
        //    //    {
        //    //        if (data.Result != null && data.Result.Duration != null)
        //    //        {
        //    //            totalTrainingTime += (data.Result.Duration);
        //    //        }
        //    //        //totalTrainingTime += (data.Result.Duration);
        //    //        //single day count                   
        //    //        GeneralWidgetViewModel singleDayTrainingTime = vmList.Single(d => d.DateTime.Day == data.Timestamp.Day);
        //    //        if (data.Result != null && data.Result.Duration != null)
        //    //        {
        //    //            singleDayTrainingTime.CurrentTrainingTimeCount = singleDayTrainingTime.CurrentTrainingTimeCount + data.Result.Duration.TotalMinutes;
        //    //            //singleDayTrainingTime.TotalTrainingTimeCount = totalTrainingTime.TotalMinutes;
        //    //        }
        //    //        singleDayTrainingTime.TotalTrainingTimeCount = (double)totalTrainingTime.TotalMinutes;
        //    //    }
        //    //    else
        //    //    {
        //    //        if (data.Result != null && data.Result.Duration != null)
        //    //        {
        //    //            totalTrainingTime += (data.Result.Duration);
        //    //        }
        //    //        GeneralWidgetViewModel tempvm = new GeneralWidgetViewModel()
        //    //        {
        //    //            DateTime = data.Timestamp,
        //    //            //CurrentTrainingTimeCount = data.Result.Duration.TotalMinutes,
        //    //            TotalTrainingTimeCount = (double)totalTrainingTime.TotalMinutes
        //    //        };
        //    //        if (data.Result != null && data.Result.Duration != null)
        //    //        {
        //    //            tempvm.CurrentTrainingTimeCount = data.Result.Duration.TotalMinutes;
        //    //        }
        //    //        vmList.Add(tempvm);

        //    //        //vmList.Add(
        //    //        //    new GeneralWidgetViewModel()
        //    //        //    {
        //    //        //        DateTime = data.Timestamp,
        //    //        //        CurrentTrainingTimeCount = data.Result.Duration.TotalMinutes,
        //    //        //        TotalTrainingTimeCount = totalTrainingTime.TotalMinutes                            
        //    //        //    });
        //    //    }
        //    //}
        //    //foreach (var data in testingList)
        //    //{
        //    //    if (vmList.Any(d => d.DateTime.Day == data.Timestamp.Day))
        //    //    {
        //    //        if (data.Result != null && data.Result.Duration != null)
        //    //        {
        //    //            totalTestsTime += (data.Result.Duration);
        //    //        }
        //    //        //totalTestsTime += (data.Result.Duration);
        //    //        //Single day count
        //    //        GeneralWidgetViewModel singleDayTestTime = vmList.Single(d => d.DateTime.Day == data.Timestamp.Day);
        //    //        if (data.Result != null && data.Result.Duration != null)
        //    //        {
        //    //            singleDayTestTime.CurrentTimeCount = singleDayTestTime.CurrentTimeCount + data.Result.Duration.TotalMinutes;
        //    //            //singleDayTrainingTime.TotalTrainingTimeCount = totalTrainingTime.TotalMinutes;
        //    //        }
        //    //        //singleDayTestTime.CurrentTimeCount = singleDayTestTime.CurrentTimeCount + data.Result.Duration.TotalMinutes;
        //    //        singleDayTestTime.TotalTimeCount = (double)totalTestsTime.TotalMinutes;
        //    //    }
        //    //    else
        //    //    {
        //    //        if (data.Result != null && data.Result.Duration != null)
        //    //        {
        //    //            totalTestsTime += (data.Result.Duration);
        //    //        }

        //    //        GeneralWidgetViewModel tempvm = new GeneralWidgetViewModel()
        //    //        {
        //    //            DateTime = data.Timestamp,
        //    //            //CurrentTrainingTimeCount = data.Result.Duration.TotalMinutes,
        //    //            TotalTimeCount = (double)totalTestsTime.TotalMinutes
        //    //        };
        //    //        if (data.Result != null && data.Result.Duration != null)
        //    //        {
        //    //            tempvm.CurrentTimeCount = data.Result.Duration.TotalMinutes;
        //    //        }
        //    //        vmList.Add(tempvm);


        //    //        //totalTestsTime += (data.Result.Duration);
        //    //        //vmList.Add(
        //    //        //    new GeneralWidgetViewModel()
        //    //        //    {
        //    //        //        DateTime = data.Timestamp,
        //    //        //        CurrentTimeCount = data.Result.Duration.TotalMinutes,
        //    //        //        TotalTimeCount = totalTrainingTime.TotalMinutes
        //    //        //    });
        //    //    }
        //    //}
        //    #endregion
        //    foreach (var entry in vmList)
        //    {
        //        sb.AppendLine(String.Join(",", entry.DateTime.Year + "-" + entry.DateTime.Month + "-" + entry.DateTime.Day, entry.TotalTimeCount, entry.CurrentTimeCount, entry.TotalTrainingTimeCount, entry.CurrentTrainingTimeCount));
        //    }

        //    return (sb.ToString());
        //}

        //public async Task<IActionResult> TotalStatementTimePartial()
        //{
        //    return PartialView("../Widgets/_TotalStatementTimePartial");
        //}
        #endregion

        #region - total statement chart - in use
        ////***********************************************************************************************************************************
        ////Total tests taken
        ////***********************************************************************************************************************************
        //public async Task<string> TotalStatementCountCSV(long? providerId, long? professionalId, long? locationId, long? testId, long? statementId, long? eduOrgId)
        //{
        //    var sb = new StringBuilder();
        //    sb.AppendLine("date, Total Test Taken, Daily Test Taken, Total Training Taken, Daily Training Taken");

        //    List<Statement> statementList = new List<Statement>();
        //    List<Statement> statementTestingList = new List<Statement>();
        //    List<Statement> statementTrainingList = new List<Statement>();
        //    bool isFebris = _security.FebrisAuthorityCheck(User);
        //    //GeneralWidgetViewModel vm = new GeneralWidgetViewModel();
        //    List<GeneralWidgetViewModel> vmList = new List<GeneralWidgetViewModel>();
        //    List<ProfessionalTestData> TestDataList = new List<ProfessionalTestData>();
        //    int totalTestsCount = 0;
        //    int totalTrainingCount = 0;

        //    if (isFebris == true && providerId == null && professionalId == null && locationId == null && testId == null && statementId == null && eduOrgId == null)
        //    {
        //        statementList = await _xAPIContext.Statement
        //            .Include(a => a.Actor)
        //            .Include(a => a.Actor.Account)
        //            .Include(a => a.Actor.Member)//This is a problem *********************This has an array of Actors. That is probably the problem. 
        //            .Include(v => v.Verb)
        //            .Include(o => o.Object)
        //            .Include(d => d.Object.Definition)
        //            .Include(r => r.Result)
        //            .Include(s => s.Result.Score)
        //            .Include(s => s.Result.Extensions)
        //            .Include(c => c.Context)
        //            .Include(c => c.Context.ContextActivities)
        //            .Include(c => c.Context.Extensions)
        //            .Include(c => c.Context.StatementReference)
        //            .Include(c => c.Context.Instructor)
        //            .Include(a => a.Authority)
        //            .Include(a => a.Authority.Actor)
        //            .Include(v => v.Version)
        //            .Include(a => a.Attachments)
        //            .ToListAsync();
        //    }
        //    else if (statementId != null)
        //    {
        //        //user this to find test that are the same?
        //        Statement statement = await _xAPIContext.Statement
        //            .Include(a => a.Actor)
        //            .Include(a => a.Actor.Account)
        //            .Include(a => a.Actor.Member)//This is a problem *********************This has an array of Actors. That is probably the problem. 
        //            .Include(v => v.Verb)
        //            .Include(o => o.Object)
        //            .Include(d => d.Object.Definition)
        //            .Include(r => r.Result)
        //            .Include(s => s.Result.Score)
        //            .Include(s => s.Result.Extensions)
        //            .Include(c => c.Context)
        //            .Include(c => c.Context.ContextActivities)
        //            .Include(c => c.Context.Extensions)
        //            .Include(c => c.Context.StatementReference)
        //            .Include(c => c.Context.Instructor)
        //            .Include(a => a.Authority)
        //            .Include(a => a.Authority.Actor)
        //            .Include(v => v.Version)
        //            .Include(a => a.Attachments)
        //            .Where(i => i.Id == statementId)
        //            .FirstAsync();
        //        //.FindAsync(statementId);

        //        //HealthCareProvider provider = new HealthCareProvider();
        //        //(statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProvider(provider);
        //    }
        //    else if (providerId != null)
        //    {
        //        Institution provider = await _context.Institution.FindAsync(providerId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProvider(provider);
        //    }
        //    else if (locationId != null)
        //    {
        //        Location location = await _context.Location.FindAsync(locationId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByLocation(location);
        //    }
        //    else if (professionalId != null)
        //    {
        //        Professional professional = await _context.Professional.FindAsync(professionalId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByProfessional(professional);
        //    }
        //    else if (testId != null)
        //    {
        //        ModuleBase test = await _context.ModuleBase.FindAsync(testId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByTest(test);
        //    }
        //    else if (eduOrgId != null)
        //    {
        //        ModuleDeveloper educationOrganization = await _context.ModuleDeveloper.FindAsync(eduOrgId);
        //        (statementList, StatusMessage) = await _statementHandler.GetAllStatementsByEducationOrganization(educationOrganization);
        //    }
        //    //***********************************************************************************************************************************            
        //    //may need cloning here. 
        //    //***********************************************************************************************************************************   
        //    //(statementTrainingList, StatusMessage) = await _statementHandler.StatementListFilter(statementList, false);
        //    (statementTestingList, StatusMessage) = await _statementHandler.StatementListFilter(statementList, true);
        //    //IEnumerable<Statement> trainingList = statementTrainingList.OrderBy(d => d.Timestamp).ToList();
        //    IEnumerable<Statement> testingList = statementTestingList.OrderBy(d => d.Timestamp).ToList();
        //    IEnumerable<Statement> fullList = statementList.OrderBy(d => d.Timestamp).ToList();
        //    //***********************************************************************************************************************************            
        //    //sort out data by date
        //    //*********************************************************************************************************************************** 
        //    foreach (var data in fullList)
        //    {
        //        bool isTest = testingList.Where(t => t.Id == data.Id).Any();

        //        if (isTest)
        //        {
        //            totalTestsCount++;
        //        }
        //        else
        //        {
        //            totalTrainingCount++;
        //        }

        //        if (vmList.Any(d => d.DateTime.Day == data.Timestamp.Day))
        //        {
        //            GeneralWidgetViewModel singleDayCount = vmList.Single(d => d.DateTime.Day == data.Timestamp.Day);
        //            singleDayCount.TotalCount = totalTestsCount;
        //            singleDayCount.TotalTrainingCount = totalTrainingCount;
        //            if (isTest)
        //            {
        //                if (singleDayCount.CurrentCount.HasValue)
        //                {
        //                    singleDayCount.CurrentCount++;
        //                }
        //                else
        //                {
        //                    singleDayCount.CurrentCount = 1;
        //                }
        //            }
        //            else
        //            {
        //                if (singleDayCount.CurrentTrainingCount.HasValue)
        //                {
        //                    singleDayCount.CurrentTrainingCount++;
        //                }
        //                else
        //                {
        //                    singleDayCount.CurrentTrainingCount = 1;
        //                }

        //            }
        //        }
        //        else
        //        {
        //            GeneralWidgetViewModel vm = new GeneralWidgetViewModel()
        //            {
        //                DateTime = data.Timestamp,
        //                TotalTrainingCount = totalTrainingCount,
        //                TotalCount = totalTestsCount
        //            };
        //            if (isTest)
        //            {
        //                vm.CurrentCount = 1;
        //            }
        //            else
        //            {
        //                vm.CurrentTrainingCount = 1;
        //            }
        //            vmList.Add(vm);
        //        }
        //    }
        //    #region old way
        //    //foreach (var data in trainingList)
        //    //{
        //    //    totalTrainingCount++;
        //    //    if (vmList.Any(d => d.DateTime.Day == data.Timestamp.Day))
        //    //    {
        //    //        //totalTrainingCount++;
        //    //        //add the daily count 
        //    //        GeneralWidgetViewModel singleDayTrainingCount = vmList.Single(d => d.DateTime.Day == data.Timestamp.Day);
        //    //        singleDayTrainingCount.CurrentTrainingCount++;
        //    //        singleDayTrainingCount.TotalTrainingCount = totalTrainingCount;
        //    //    }
        //    //    else
        //    //    {
        //    //        //totalTrainingCount++;
        //    //        vmList.Add(
        //    //            new GeneralWidgetViewModel()
        //    //            {
        //    //                DateTime = data.Timestamp,
        //    //                CurrentTrainingCount = 1,
        //    //                TotalTrainingCount = totalTrainingCount,
        //    //                TotalCount = totalTestsCount
        //    //            });
        //    //    }
        //    //}
        //    //foreach (var data in testingList)
        //    //{
        //    //    totalTestsCount++;
        //    //    if (vmList.Any(d => d.DateTime.Day == data.Timestamp.Day))
        //    //    {                    
        //    //        //totalTestsCount++;
        //    //        //add the daily count 
        //    //        GeneralWidgetViewModel singleDayTestCount = vmList.Single(d => d.DateTime.Day == data.Timestamp.Day);
        //    //        singleDayTestCount.CurrentCount++;
        //    //        singleDayTestCount.TotalCount = totalTestsCount;
        //    //    }
        //    //    else
        //    //    {
        //    //        //totalTestsCount++;
        //    //        vmList.Add(
        //    //            new GeneralWidgetViewModel()
        //    //            {
        //    //                DateTime = data.Timestamp,
        //    //                CurrentCount = 1,
        //    //                TotalCount = totalTestsCount,
        //    //                TotalTrainingCount = totalTrainingCount
        //    //            });                    
        //    //    }
        //    //}
        //    #endregion
        //    foreach (var entry in vmList)
        //    {
        //        sb.AppendLine(String.Join(",", entry.DateTime.Year + "-" + entry.DateTime.Month + "-" + entry.DateTime.Day, entry.TotalCount, entry.CurrentCount, entry.TotalTrainingCount, entry.CurrentTrainingCount));
        //    }

        //    return (sb.ToString());
        //}

        //public async Task<IActionResult> TotalStatementCountPartial()
        //{
        //    return PartialView("../Widgets/_TotalStatementCountPartial");
        //}
        #endregion

        #endregion
                                
        #region Liabiliity Waiver


        #endregion

        #region Service agreement


        #endregion

        #region EULA

        #endregion

        #region unsure if needed 

        #region Liability waiver
        //public async Task<bool> LiabilityWaiverCheck(Professional input)
        //{
        //    bool hasWaivedLiability = false;
        //    try
        //    {
        //        hasWaivedLiability = await _context.LiabilityWaiver
        //            .Include(i => i.Professional)
        //            .Where(i => i.Professional.Id == input.Id)
        //            .Select(i => i.AcceptWaiver)
        //            .SingleAsync();
        //    }
        //    catch (Exception ex) { }
        //    return hasWaivedLiability;
        //}

        //[HttpGet]
        //public async Task<IActionResult> LiabilityWaiverPartial()
        //{
        //    ////get user 
        //    //var user = (await _userManager.GetUserAsync(User));
        //    //if (user.Id == Guid.Empty)
        //    //{
        //    //    return NotFound();
        //    //}




        //    return PartialView("../Widgets/_LiabilityWaiverModalPartial");
        //}

        //[HttpGet]
        //public async Task<IActionResult> LiabilityWaiverResponse(bool input)
        //{
        //    //get user 
        //    var user = (await _userManager.GetUserAsync(User));
        //    if (user.Id == Guid.Empty)
        //    {
        //        return NotFound();
        //    }

        //    bool WaiverFileExists = false;

        //    //get professional linked to user
        //    Professional professional = await _context.ProfessionalLinkedUser
        //        .Include(p => p.Professional)
        //        .Where(p => p.UserId == user.Id)
        //        .Select(i => i.Professional)
        //        .FirstOrDefaultAsync();

        //    LiabilityWaiver liabilityWaiver = new LiabilityWaiver();

        //    try
        //    {
        //        liabilityWaiver = await _context.LiabilityWaiver
        //            .Include(i => i.Professional)
        //            .Where(i => i.Professional.Id == professional.Id)
        //            .SingleAsync();
        //        if (liabilityWaiver != null && liabilityWaiver.Id != 0)
        //        {
        //            WaiverFileExists = true;
        //        }
        //        else
        //        {
        //            liabilityWaiver = new LiabilityWaiver
        //            {
        //                UserId = user.Id,
        //                Professional = professional,
        //                ProfessionalUUID = professional.UUID
        //            };
        //        }
        //    }
        //    catch
        //    {
        //        liabilityWaiver = new LiabilityWaiver
        //        {
        //            UserId = user.Id,
        //            Professional = professional,
        //            ProfessionalUUID = professional.UUID
        //        };
        //    }

        //    liabilityWaiver.AcceptWaiver = input;

        //    if (WaiverFileExists)
        //    {
        //        _context.LiabilityWaiver.Update(liabilityWaiver);
        //    }
        //    else
        //    {
        //        await _context.LiabilityWaiver.AddAsync(liabilityWaiver);
        //    }
        //    await _context.SaveChangesAsync();

        //    return Ok();
        //}
        #endregion



        #endregion
    }
}