// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.LauncherModels;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.ViewModels;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.DataAccessLayer.Queries.UserQueries;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.UserNode.DataAccessLayer.Queries.XAPIQueries;
using Febris.UserNode.LogicLayer.Logic.AnalyticsLogic;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.Models.UserModels;

namespace Febris.UserNode.LogicLayer.Logic.LauncherLogic
{
    public interface ILauncherLogic
    {
        Task<HardwareInitializationResponse> Initalize();
        Task<Statement> InitalizeStatement(SimulationInitializerViewModel input);
        Task<Statement> SubmitStatement(Statement input);
        Task<Statement> SubmitStatement(JObject input);
        // Phase 3.3c typed-DTO ingress -- EndUser BLL twin of the shared
        // ILauncherLogic surface. Controllers using the new XApiStatementBinding
        // pass the populated XApiStatementSubmission straight through
        // instead of doing their own JObject reparse off the captured bytes.
        Task<Statement> SubmitStatement(Febris.ModelLibrary.ViewModels.XApi.XApiStatementSubmission submission);
        Task<StatementInitalizationResponseViewModel> InitalizeStatement(StatementInitalizationRequestViewModel input);
    }

    // HardwareOwner enum moved to Febris.EnumLibrary per the "all enums live in FebrisEnumLibrary" rule
    // (was duplicated here and in shared/FebrisSharedLogicLayer; now a single canonical definition).

    public class LauncherLogic : ILauncherLogic
    {
        private readonly IHardwareQueries _context;
        private readonly IUserQueries _applicationUserContext;
        //private readonly IHardwareLinkedContentDeveloperQueries _devLinkedContext;
        //private readonly IHardwareLinkedAccreditationBodyQueries _accLinkedContext;
        //private readonly IHardwareLinkedFebrisQueries _febrisLinkedContext;
        //private readonly IHardwareLinkedCurriculumQueries _curricLinkedContext;
        private readonly IHardwareLinkedModuleQueries _modLinkedContext;
        private readonly IMessageBoardQueries _messageboardContext;
        //private readonly IProfessionalQueries _professionalContext;
        //private readonly IProfessionalLinkedAccreditationBodyQueries _professionallinkedAccreditationBodyContext;
        //private readonly IProfessionalLinkedContentDeveloperQueries _professionallinkedContentDeveloperContext;
        //private readonly IProfessionalLinkedFebrisQueries _professionallinkedFebrisContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IModuleQueries _moduleContext;

        //xApi info
        private readonly IModuleLinkedObjectQueries _modLinkedObjectContext;
        //private readonly IProfessionalLinkedActorQueries _professionalLinkedActorContext;
        private readonly IActorQueries _actorContext;
        private readonly IObjectQueries _objectContext;
        private readonly IVerbQueries _verbContext;
        private readonly IStatementLogic _statementContext;
        private readonly Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic _recordingContext;
        #region [Historical] central SeatCheck context (MDM-B1, resolved by the local entitlement gate)
        // RESOLVED (2026-07-17): _purchaseContext was declared but never
        // assigned in either constructor, so the central SeatCheck round-trip in both
        // InitalizeStatement overloads dereferenced null the moment it was reached (MDM-B1).
        // The node now gates launches LOCALLY on the tenant's own HardwareLinkedModule link --
        // the same authority the package-download gate (HardwareLinkedModuleLogic.Download)
        // already used -- so the central commerce dependency is gone rather than repaired.
        // Optional hub-federated seat enforcement is a future federation-gate feature.
        //private readonly IPurchaseLogic _purchaseContext;
        #endregion

        private readonly IHardwareLinkedCohortQueries _hardwareLinkedCohortContext;
        private readonly ICohortMemberQueries _cohortMemberContext;
        private readonly IModuleUsageAnalyticsLogic _moduleUsageAnalyticscontext;
        // SCBA-B3 port (node hygiene D): null on the legacy self-newing path, where
        // ScopedBackgroundWork's legacy fallback preserves the pre-fix behavior.
        private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;
        //private readonly ITestUserLinkedAccreditationBodyQueries _professionallinkedAccreditationBodyContext;
        //private readonly ITestUserLinkedContentDeveloperQueries _professionallinkedContentDeveloperContext;
        //private readonly ITestUserLinkedFebrisQueries _professionallinkedFebrisContext;
        //private readonly ITestUserLinkedActorQueries _professionalLinkedActorContext;
        private readonly License _license;
        private readonly Hardware _hardware;
        

        // DI refactor
        public LauncherLogic(
            IHttpContextAccessor httpContextAccessor,
            IHardwareQueries context,
            IHardwareLinkedModuleQueries modLinkedContext,
            IMessageBoardQueries messageboardContext,
            IUserQueries applicationUserContext,
            IModuleQueries moduleContext,
            IModuleLinkedObjectQueries modLinkedObjectContext,
            IActorQueries actorContext,
            IObjectQueries objectContext,
            IVerbQueries verbContext,
            IHardwareLinkedCohortQueries hardwareLinkedCohortContext,
            IStatementLogic statementContext,
            ICohortMemberQueries cohortMemberContext,
            IModuleUsageAnalyticsLogic moduleUsageAnalyticscontext,
            // Records which actor a freshly minted recording name belongs to. This is the ONLY
            // point where that is knowable: the upload that follows carries a device token and
            // nothing identifying the learner.
            Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic recordingContext,
            // SCBA-B3 port (node hygiene D): backs the fire-and-forget usage-analytics write with
            // a FRESH DI scope so the request-scoped analytics logic (and its DbContext) is never
            // captured by a Task that outlives the request. Mirrors the shared LauncherLogic twin.
            Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory
            )
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            //_devLinkedContext = new HardwareLinkedContentDeveloperQueries();
            //_accLinkedContext = new HardwareLinkedAccreditationBodyQueries();
            //_febrisLinkedContext = new HardwareLinkedFebrisQueries();
            //_curricLinkedContext = new HardwareLinkedCurriculumQueries();
            _modLinkedContext = modLinkedContext;
            _messageboardContext = messageboardContext;
            _applicationUserContext = applicationUserContext;
            _moduleContext = moduleContext;
            //_professionallinkedAccreditationBodyContext = new TestUserLinkedAccreditationBodyQueries();
            //_professionallinkedContentDeveloperContext = new TestUserLinkedContentDeveloperQueries();
            //_professionallinkedFebrisContext = new TestUserLinkedFebrisQueries();
            //_professionalLinkedActorContext = new ProfessionalLinkedActorQueries();
            _modLinkedObjectContext = modLinkedObjectContext;
            _actorContext = actorContext;
            _objectContext = objectContext;
            _verbContext = verbContext;
            _hardwareLinkedCohortContext = hardwareLinkedCohortContext;
            _statementContext = statementContext;
            _recordingContext = recordingContext;
            _cohortMemberContext = cohortMemberContext;
            //_license = (License)_httpContextAccessor.HttpContext.Items["License"] ?? null;
            _license = LicenseClaimsPrincipalExtension.GetLicense(_httpContextAccessor).Result;
            _hardware = (Hardware)_httpContextAccessor.HttpContext.Items["Hardware"] ?? null;
            //_TestUserContext = new TestUserQueries();
            _moduleUsageAnalyticscontext = moduleUsageAnalyticscontext;
            _scopeFactory = scopeFactory;
        }


        public LauncherLogic(
            IHttpContextAccessor httpContextAccessor
            )
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new HardwareQueries();
            //_devLinkedContext = new HardwareLinkedContentDeveloperQueries();
            //_accLinkedContext = new HardwareLinkedAccreditationBodyQueries();
            //_febrisLinkedContext = new HardwareLinkedFebrisQueries();
            //_curricLinkedContext = new HardwareLinkedCurriculumQueries();
            _modLinkedContext = new HardwareLinkedModuleQueries();
            _messageboardContext = new MessageBoardQueries();
            _applicationUserContext = new UserQueries();
            _moduleContext = new ModuleQueries();
            //_professionallinkedAccreditationBodyContext = new TestUserLinkedAccreditationBodyQueries();
            //_professionallinkedContentDeveloperContext = new TestUserLinkedContentDeveloperQueries();
            //_professionallinkedFebrisContext = new TestUserLinkedFebrisQueries();
            //_professionalLinkedActorContext = new ProfessionalLinkedActorQueries();
            _modLinkedObjectContext = new ModuleLinkedObjectQueries();
            _actorContext = new ActorQueries();
            _objectContext = new ObjectQueries();
            _verbContext = new VerbQueries();
            _hardwareLinkedCohortContext = new HardwareLinkedCohortQueries();
            _statementContext = new StatementLogic(_httpContextAccessor);
            _cohortMemberContext = new CohortMemberQueries();
            //_license = (License)_httpContextAccessor.HttpContext.Items["License"] ?? null;
            _license = LicenseClaimsPrincipalExtension.GetLicense(_httpContextAccessor).Result;
            _hardware = (Hardware)_httpContextAccessor.HttpContext.Items["Hardware"] ?? null;
            //_TestUserContext = new TestUserQueries();
            _moduleUsageAnalyticscontext = new ModuleUsageAnalyticsLogic(_httpContextAccessor);
        }


        public async Task<HardwareInitializationResponse> Initalize()
        {
            HardwareInitializationResponse output = new HardwareInitializationResponse();
            try
            {
                HardwareOwner ownerRouting = HardwareOwner.NoneYet;


                Hardware hardware = (Hardware)_httpContextAccessor.HttpContext.Items["Hardware"];


                //bool exists = await _febrisLinkedContext.Exists(hardware);
                //if (exists)
                //{
                //    ownerRouting = HardwareOwner.Febris;
                //}
                //if (ownerRouting == HardwareOwner.NoneYet)
                //{
                //    exists = await _devLinkedContext.Exists(hardware);
                //    if (exists)
                //    {
                //        ownerRouting = HardwareOwner.ContentDeveloper;
                //    }
                //}
                //if (ownerRouting == HardwareOwner.NoneYet)
                //{
                //    exists = await _accLinkedContext.Exists(hardware);
                //    if (exists)
                //    {
                //        ownerRouting = HardwareOwner.AccreditationBody;
                //    }
                //}
                output = await HardwareInialization(hardware);

                //switch (ownerRouting)
                //{
                //    case HardwareOwner.Febris:
                //        output = await FebrisHardwareInialization(hardware);
                //        break;
                //    case HardwareOwner.ContentDeveloper:
                //        output = await ContentDeveloperHardwareInialization(hardware);
                //        break;
                //    case HardwareOwner.AccreditationBody:
                //        output = await AccreditationBodyHardwareInialization(hardware);
                //        break;
                //    default:
                //        return null;
                //        break;
                //}

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        private async Task<HardwareInitializationResponse> HardwareInialization(Hardware hardware)
        {
            HardwareInitializationResponse output = new HardwareInitializationResponse();
            try
            {
                ///Get Generic Info
                List<MessageBoard> messageBoard = await _messageboardContext.GetLastFive();


                ///Geting User Info                
                List<HardwareLinkedCohort> linkedCohortList = await _hardwareLinkedCohortContext.GetByHardware(hardware.UUID);//.Get(hardware);
                List<Cohort> cohortList = linkedCohortList.Select(i => i.Cohort).ToList();
                List<CohortMember> memberList = await _cohortMemberContext.Get(cohortList);//.Get();//.Select(i => i.Cohort).ToList();
                List<Guid> userIdList = memberList.Select(i => i.UserId).ToList();
                List<LocalApplicationUser> applicationUserList = await _applicationUserContext.Get(userIdList);
                // TEST USERS REMOVED. _testUserContext.Get() took no argument, so every device
                // that authenticated received the ENTIRE TestUser table, while real users are
                // narrowed hardware to cohort to member to user on the three lines above. TestUser
                // carries no link to hardware or cohort to scope it by, so it could not be made per
                // device without new schema. Removed rather than left broadcasting to every device
                // on every node. The rows stay in the database and HardwareUserViewModel keeps
                // IsTestUser, which every client now sends as false.
                List<HardwareUserViewModel> userList = new List<HardwareUserViewModel>();

                foreach (var i in applicationUserList)
                {
                    HardwareUserViewModel temp = new HardwareUserViewModel()
                    {
                        IsTestUser = false,
                        UserId = i.Id,
                        FirstName = i.FirstName,
                        LastName = i.LastName,
                        IdentificationNumber = i.IdentificationNumber ?? i.Id.ToString(),
                        EmailAddress = i.Email,
                        //PhoneNumber = i.PhoneNumber,
                        ActorId = i.Actor.Value,
                        PicturePath = i.ProfilePicturePath
                    };
                    userList.Add(temp);
                }





                ///Module Info
                List<LocalHardwareLinkedModule> linkedModuleList = await _modLinkedContext.GetByHardware(hardware.Id);
                List<Guid> modUUIDList = linkedModuleList.Select(i => i.ModuleUUID).ToList();
                List<Module> moduleList = await _moduleContext.Get(modUUIDList);


                ///partial building
                HardwareMessageboardViewModels messageboardVM = new HardwareMessageboardViewModels()
                {
                    MessageBoardList = messageBoard
                };
                HardwareUserInitaliztionViewModels userVM = new HardwareUserInitaliztionViewModels()
                {
                    UserViewModelList = userList
                };
                HardwareModuleInitaliztionViewModels moduleVM = new HardwareModuleInitaliztionViewModels()
                {

                };

                output = new HardwareInitializationResponse()
                {
                    MessageboardViewModels = messageboardVM,
                    UserInitaliztionViewModels = userVM,
                    ModuleInitaliztionViewModels = moduleVM,
                    ModuleList = moduleList
                };

                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

        }

        //private async Task<HardwareInitializationResponse> AccreditationBodyHardwareInialization(Hardware hardware)
        //{
        //    HardwareInitializationResponse output = new HardwareInitializationResponse();
        //    try
        //    {
        //        HardwareLinkedAccreditationBody ownerlink = new HardwareLinkedAccreditationBody();
        //        ownerlink = await _accLinkedContext.Get(hardware);
        //        List<AdminMessageBoard> adminMessageBoard = await _adminMessageboardContext.GetLastFive();
        //        List<MessageBoard> messageBoard = await _messageboardContext.GetLastFive(ownerlink.AccreditationBody);
        //        List<TestUserLinkedAccreditationBody> linkedProfessionalList = await _professionallinkedAccreditationBodyContext.Get(ownerlink.AccreditationBody);
        //        List<TestUser> professionalList = linkedProfessionalList.Select(i => i.TestUser).ToList();
        //        List<HardwareLinkedModule> linkedModuleList = await _modLinkedContext.GetByHardware(hardware.Id);
        //        List<Module> modList = linkedModuleList.Select(i => i.Module).ToList();

        //        List<HardwareUserViewModel> userList = new List<HardwareUserViewModel>();
        //        foreach (var i in professionalList)
        //        {
        //            HardwareUserViewModel temp = new HardwareUserViewModel()
        //            {
        //                IsTestUser = true,
        //                UserId = i.UUID,
        //                FirstName = i.FirstName,
        //                LastName = i.LastName,
        //                IdentificationNumber = i.IdentificationNumber,
        //                EmailAddress = i.EmailAddress,
        //                //PhoneNumber = i.PhoneNumber,
        //                ActorId = i.ActorId,
        //                PicturePath = i.PhotoOfProfessional
        //            };
        //            userList.Add(temp);
        //        }

        //        output = new HardwareInitializationResponse()
        //        {
        //            //AdminMessageBoardList = adminMessageBoard,
        //            //MessageBoardList = messageBoard,
        //            //UserList = userList,
        //            //ModuleList = modList
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        //private async Task<HardwareInitializationResponse> ContentDeveloperHardwareInialization(Hardware hardware)
        //{
        //    HardwareInitializationResponse output = new HardwareInitializationResponse();
        //    try
        //    {
        //        HardwareLinkedContentDeveloper ownerlink = new HardwareLinkedContentDeveloper();
        //        ownerlink = await _devLinkedContext.Get(hardware);
        //        List<AdminMessageBoard> adminMessageBoard = await _adminMessageboardContext.GetLastFive();
        //        List<MessageBoard> messageBoard = await _messageboardContext.GetLastFive(ownerlink.ContentDeveloper);
        //        List<TestUserLinkedContentDeveloper> linkedProfessionalList = await _professionallinkedContentDeveloperContext.Get(ownerlink.ContentDeveloper);
        //        List<TestUser> professionalList = linkedProfessionalList.Select(i => i.TestUser).ToList();
        //        List<HardwareLinkedModule> linkedModuleList = await _modLinkedContext.GetByHardware(hardware.Id);
        //        List<Module> modList = linkedModuleList.Select(i => i.Module).ToList();

        //        List<HardwareUserViewModel> userList = new List<HardwareUserViewModel>();
        //        foreach (var i in professionalList)
        //        {
        //            HardwareUserViewModel temp = new HardwareUserViewModel()
        //            {
        //                IsTestUser = true,
        //                UserId = i.UUID,
        //                FirstName = i.FirstName,
        //                LastName = i.LastName,
        //                IdentificationNumber = i.IdentificationNumber,
        //                EmailAddress = i.EmailAddress,
        //                //PhoneNumber = i.PhoneNumber,
        //                ActorId = i.ActorId,
        //                PicturePath = i.PhotoOfProfessional
        //            };
        //            userList.Add(temp);
        //        }

        //        HardwareMessageboardViewModels messageboardVM = new HardwareMessageboardViewModels()
        //        {
        //            AdminMessageBoardList = adminMessageBoard,
        //            MessageBoardList = messageBoard
        //        };
        //        HardwareUserInitaliztionViewModels userVM = new HardwareUserInitaliztionViewModels()
        //        {
        //            UserViewModelList = userList
        //        };
        //        HardwareModuleInitaliztionViewModels moduleVM = new HardwareModuleInitaliztionViewModels()
        //        {

        //        };

        //        output = new HardwareInitializationResponse()
        //        {
        //            MessageboardViewModels = messageboardVM,
        //            UserInitaliztionViewModels = userVM,
        //            ModuleInitaliztionViewModels = moduleVM,
        //            ModuleList = modList
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        //private async Task<HardwareInitializationResponse> FebrisHardwareInialization(Hardware hardware)
        //{
        //    HardwareInitializationResponse output = new HardwareInitializationResponse();
        //    try
        //    {
        //        //HardwareLinkedFebris ownerlink = new HardwareLinkedFebris();                
        //        //ownerlink = await _febrisLinkedContext.Get(hardware);
        //        // Hardware hardware = await _context.Get(hardware);
        //        List<AdminMessageBoard> adminMessageBoard = await _adminMessageboardContext.GetLastFive();
        //        // List<MessageBoard> messageBoard = await _messageboardContext.GetLastFive(ownerlink);
        //        List<TestUserLinkedFebris> linkedProfessionalList = await _professionallinkedFebrisContext.Get();
        //        List<TestUser> professionalList = linkedProfessionalList.Select(i => i.TestUser).ToList();
        //        List<HardwareLinkedModule> linkedModuleList = await _modLinkedContext.GetByHardware(hardware.Id);
        //        List<Module> modList = linkedModuleList.Select(i => i.Module).ToList();


        //        List<HardwareUserViewModel> userList = new List<HardwareUserViewModel>();
        //        foreach (var i in professionalList)
        //        {
        //            HardwareUserViewModel temp = new HardwareUserViewModel()
        //            {
        //                IsTestUser = true,
        //                UserId = i.UUID,
        //                FirstName = i.FirstName,
        //                LastName = i.LastName,
        //                IdentificationNumber = i.IdentificationNumber,
        //                EmailAddress = i.EmailAddress,
        //                //PhoneNumber = i.PhoneNumber,
        //                ActorId = i.ActorId,
        //                PicturePath = i.PhotoOfProfessional
        //            };
        //            userList.Add(temp);
        //        }
        //        HardwareMessageboardViewModels messageboardVM = new HardwareMessageboardViewModels()
        //        {
        //            AdminMessageBoardList = adminMessageBoard,
        //            MessageBoardList = default
        //        };
        //        HardwareUserInitaliztionViewModels userVM = new HardwareUserInitaliztionViewModels()
        //        {
        //            UserViewModelList = userList
        //        };
        //        HardwareModuleInitaliztionViewModels moduleVM = new HardwareModuleInitaliztionViewModels()
        //        {

        //        };

        //        output = new HardwareInitializationResponse()
        //        {
        //            MessageboardViewModels = messageboardVM,
        //            UserInitaliztionViewModels = userVM,
        //            ModuleInitaliztionViewModels = moduleVM,
        //            ModuleList = modList
        //            //AdminMessageBoardList = adminMessageBoard,
        //            //MessageBoardList = messageBoard,
        //            //UserList = userList,
        //            //ModuleList = modList
        //        };

        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}


        public async Task<Statement> InitalizeStatement(SimulationInitializerViewModel input)
        {
            Statement output = new Statement();
            try
            {
                //ProfessionalLinkedActor professionalLinkedActor = await _professionalLinkedActorContext.Get(input.Professional);
                //ProfessionalLinkedActor professionalLinkedActor = await _professionalLinkedActorContext.Get(input.Professional);
                //Actor actor = await _actorContext.Get(professionalLinkedActor.ActorId);
                Actor actor = await _actorContext.Get(input.ActorId);

                ModuleLinkedObject moduleLinkedObject = await _modLinkedObjectContext.GetByModule(input.Module.UUID);
                ModelLibrary.Models.XApiModels.Object xApiObject = await _objectContext.Get(moduleLinkedObject.ObjectId);

                ///Local entitlement gate: the launch is authorized by the
                ///tenant's own HardwareLinkedModule link -- the same authority the package-download
                ///gate uses -- with zero HTTP. Fail closed when no hardware is on the request.
                #region [Historical] central SeatCheck (replaced by the local entitlement gate; MDM-B1)
                ///Check if this User has a seat for this module
                //bool actorHasASeat = await _purchaseContext.SeatCheck(actor.UUID, moduleLinkedObject);
                //if (!actorHasASeat) { return default; }
                // The remote check asked central commerce whether the ACTOR held a purchased seat
                // (and NPE'd tenant-side because _purchaseContext was never assigned -- MDM-B1).
                // Optional hub-federated seat enforcement is a future federation-gate feature.
                #endregion
                if (_hardware == null) { return default; }
                bool hardwareIsEntitled = await _modLinkedContext.Exists(_hardware, moduleLinkedObject.Module);
                if (!hardwareIsEntitled) { return default; }

                Verb verb = new Verb();
                //if(input.Module.ListingType)
                ModuleClassification classification = moduleLinkedObject.Module.ModuleClassification;
                verb = await _verbContext.Get(new Uri("https://febr.is/Verb/Details/Initialized"));
                //switch (classification.Name.ToLower())
                //{
                //    case "test":

                //        verb = await _verbContext.Get(new Uri("https://febr.is/Verb/Details/Initialized"));
                //        break;
                //    case "training":

                //        break;
                //    default:
                //        break;
                //}

                output = new Statement()
                {
                    Actor = actor,
                    Object = xApiObject,
                    Verb = verb

                };


                // ROADMAP 22: the node decides, from the educator's per-cohort policy. The
                // request's own RecordSession bool is deliberately not consulted -- see
                // ShouldRecordSession for why a client cannot be allowed to vote on this.
                if (await ShouldRecordSession(actor))
                {
                    Attachment videoAttachment = await VideoAttachmentHandler(actor);
                    //convert url to sha2
                    string shaHash = ShaHandler.TextToSha2(videoAttachment.FileURL.ToString());
                    videoAttachment.Sha2 = shaHash;

                    output.Attachments = new List<Attachment>();
                    output.Attachments.Add(videoAttachment);
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<StatementInitalizationResponseViewModel> InitalizeStatement(StatementInitalizationRequestViewModel input)
        {
            StatementInitalizationResponseViewModel output = new StatementInitalizationResponseViewModel();
            try
            {

                Actor actor = await _actorContext.Get(input.ActorId);
                ModuleLinkedObject moduleLinkedObject = await _modLinkedObjectContext.GetByModule(input.ModuleId);

                // SCBA-B3 port (node hygiene D): record module usage on its own DI scope -- the
                // request-scoped _moduleUsageAnalyticscontext (and its DbContext) is no longer
                // captured by a Task that outlives the request; faults are observed and logged by
                // the helper. Mirrors the shared LauncherLogic twin; the legacy fallback keeps the
                // pre-fix behavior for the self-newing constructor path.
                // NOTE (MDM-B6, remaining half): SetUpBackgroundInfo still dereferences
                // HttpContext off the request thread inside the background scope. The robust fix
                // snapshots IP/user-agent/referer/path synchronously here and passes an immutable
                // snapshot in. Deferred -- same shape as the shared twin, one fix for both.
                Module usedModule = moduleLinkedObject.Module;
                ScopedBackgroundWork.FireAndForget<IModuleUsageAnalyticsLogic>(
                    _scopeFactory,
                    l => l.LogRequest(null, _hardware, usedModule),
                    () => _moduleUsageAnalyticscontext.LogRequest(null, _hardware, usedModule));

                ModelLibrary.Models.XApiModels.Object xApiObject = await _objectContext.Get(moduleLinkedObject.ObjectId);

                ///Local entitlement gate: the launch is authorized by the
                ///tenant's own HardwareLinkedModule link -- the same authority the package-download
                ///gate uses -- with zero HTTP. Fail closed when no hardware is on the request.
                #region [Historical] central SeatCheck (replaced by the local entitlement gate; MDM-B1)
                ///Check if this User has a seat for this module
                //bool actorHasASeat = await _purchaseContext.SeatCheck(actor.UUID, moduleLinkedObject);
                //if (!actorHasASeat) { return default; }
                // The remote check asked central commerce whether the ACTOR held a purchased seat
                // (and NPE'd tenant-side because _purchaseContext was never assigned -- MDM-B1).
                // Optional hub-federated seat enforcement is a future federation-gate feature.
                #endregion
                if (_hardware == null) { return default; }
                bool hardwareIsEntitled = await _modLinkedContext.Exists(_hardware, moduleLinkedObject.Module);
                if (!hardwareIsEntitled) { return default; }


                Verb verb = new Verb();
                //if(input.Module.ListingType)
                ModuleClassification classification = moduleLinkedObject.Module.ModuleClassification;
                verb = await _verbContext.Get(new Uri("https://febr.is/Verb/Details/Initialized"));
                if (verb == default)
                {
                    verb = new Verb()
                    {
                        Id = new Uri("https://febr.is/Verb/Details/Initialized"),


                    };
                }

                Statement statement = new Statement()
                {
                    Actor = actor,
                    Object = xApiObject,
                    Verb = verb
                };

                output = new StatementInitalizationResponseViewModel()
                {
                    Statement = statement
                };

                // ROADMAP 22: the node decides, from the educator's per-cohort policy. This is the
                // live launch route both shipped clients call, and the request's RecordSession bool
                // that used to gate it was never populated by either of them.
                if (await ShouldRecordSession(actor))
                {
                    Attachment videoAttachment = await VideoAttachmentHandler(actor);
                    //convert url to sha2
                    string shaHash = ShaHandler.TextToSha2(videoAttachment.FileURL.ToString());
                    videoAttachment.Sha2 = shaHash;

                    output.Statement.Attachments = new List<Attachment>();
                    output.Statement.Attachments.Add(videoAttachment);
                }


                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

        }
        public async Task<Statement> SubmitStatement(Statement input)
        {

            try
            {
                input = await _statementContext.Submit(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return input;
        }
        public async Task<Statement> SubmitStatement(JObject input)
        {
            Statement output = new Statement();
            try
            {
                output = await _statementContext.Submit(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        /// <summary>
        /// Phase 3.3c typed-DTO ingress wrapper (EndUser BLL twin of the
        /// shared <c>LauncherLogic.SubmitStatement(XApiStatementSubmission)</c>).
        /// Delegates to <see cref="IStatementLogic.Submit(Febris.ModelLibrary.ViewModels.XApi.XApiStatementSubmission)"/>,
        /// which owns the JObject bridge to <c>StatementFactor</c> until
        /// the typed factor lands. Exists at this layer so controllers
        /// can call <c>_context.SubmitStatement(submission)</c> with the
        /// same ergonomic shape as the existing <c>SubmitStatement(JObject)</c>
        /// overload they already know.
        /// </summary>
        public async Task<Statement> SubmitStatement(Febris.ModelLibrary.ViewModels.XApi.XApiStatementSubmission submission)
        {
            Statement output = new Statement();
            try
            {
                output = await _statementContext.Submit(submission);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }



        /// <summary>
        /// ROADMAP 22: whether THIS launch is recorded, decided by the node from the educator's
        /// per-cohort policy. The client does not get a vote.
        ///
        /// <para>
        /// WHY DERIVED. The record decision used to be read off <c>input.RecordSession</c>, a bool
        /// on the request. Two things were wrong with that. The lesser: neither shipped client ever
        /// populated it, so the branch was dead and no launch has ever been recorded. The greater:
        /// the only identity a launch request PROVES is the device, whose hardware JWT the
        /// middleware validated. The ActorId on the request is client-asserted and checked against
        /// nothing, so any request that could vote on its own recording could also dodge recording
        /// by naming a different learner.
        /// </para>
        ///
        /// <para>
        /// UNION, by owner ruling 2026-08-24. A launch can reach two cohort sets: the ones this
        /// DEVICE is linked to, and the ones the LEARNER belongs to. If either says record, the
        /// session records. The device half is keyed on <c>Hardware.Id</c>, deliberately the same
        /// key the entitlement gate on this code path uses, and it is the half that cannot be
        /// spoofed. The learner half rests on a client-asserted ActorId, so it can only ever ADD a
        /// recording, never suppress the device's -- which is why the union is safe in a way a
        /// learner-only rule would not be.
        /// </para>
        ///
        /// <para>
        /// ARCHIVED COHORTS DO NOT VOTE. An archived cohort is a retired one, and the node's own
        /// default cohort read filters it out; letting a retired class keep recording live sessions
        /// would be a policy nobody can see in the UI that shows it.
        /// </para>
        ///
        /// <para>
        /// FAILS CLOSED, meaning it fails to NOT recording: every branch that cannot answer leaves
        /// the decision false. Recording learner session video without a policy that says to is the
        /// worse error in both directions -- privacy, and a recording nobody can account for.
        /// </para>
        /// </summary>
        internal async Task<bool> ShouldRecordSession(Actor actor)
        {
            // The device's cohorts. _hardware is the middleware-attached identity; both callers
            // already refused the request when it is null, but this stays defensive because it is
            // a security decision and the cost is one null check.
            if (_hardware != null)
            {
                List<HardwareLinkedCohort> deviceLinks =
                    await _hardwareLinkedCohortContext.GetByHardware(_hardware.Id);
                if (RecordsAnySession(deviceLinks?.Select(i => i.Cohort)))
                {
                    return true;
                }
            }

            // The learner's cohorts, reached through the stored Actor link on the account.
            if (actor != null && actor.UUID != Guid.Empty)
            {
                LocalApplicationUser learner = await _applicationUserContext.GetByActor(actor.UUID);
                if (learner != null)
                {
                    List<CohortMember> memberships =
                        await _cohortMemberContext.GetCohortsByMember(learner.Id);
                    if (RecordsAnySession(memberships?.Select(i => i.Cohort)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>True when any live cohort in the set carries the educator's record policy.</summary>
        private static bool RecordsAnySession(IEnumerable<Cohort> cohorts)
        {
            return cohorts != null && cohorts.Any(c => c != null && c.RecordSessions && !c.Archive);
        }

        /// <summary>
        /// Mints the recording name for the statement's xAPI attachment, and RECORDS WHO IT BELONGS
        /// TO.
        /// <para>
        /// The owner has to be captured here because this is the only moment it is known. The
        /// upload that follows carries a device token and nothing identifying the learner, the name
        /// itself encodes nothing, and the statement carrying the attachment is never persisted, so
        /// there is no attachment row to join back to an actor later. Without this row the Portal's
        /// video loaders have nothing to check and serve any recording to any signed-in end user
        /// who knows the Guid.
        /// </para>
        /// <para>
        /// The actor is passed in rather than resolved here: both callers already hold it from
        /// <c>_actorContext.Get(input.ActorId)</c> one frame up.
        /// </para>
        /// </summary>
        internal async Task<Attachment> VideoAttachmentHandler(Actor actor)
        {
            Attachment output = new Attachment();
            IJsonStringDictionaryBuilder stringBuilder = new JsonStringDictionaryBuilder();
            Dictionary<string, string> serializedNameDictionaryString = null;
            Dictionary<string, string> serializedDescriptionDictionaryString = null;
            try
            {
                string tempUUID = Guid.NewGuid().ToString();

                // Record ownership BEFORE the name leaves the node. _recordingContext is null on
                // the legacy self-newing constructor path, the same way _scopeFactory is, so this
                // is guarded rather than assumed. A recording with no owner row is not viewable by
                // a learner, which is the safe direction.
                if (_recordingContext != null)
                {
                    await _recordingContext.Register(tempUUID, actor?.UUID ?? Guid.Empty, _hardware?.UUID ?? Guid.Empty);
                }
                else
                {
                    Febris.SharedServices.FebrisLog.Warn(
                        "LauncherLogic.VideoAttachmentHandler: no recording logic available, so video '" +
                        tempUUID + "' will have no owner recorded and will not be viewable by its learner.");
                }
                serializedNameDictionaryString = new Dictionary<string, string> { ["en"] = tempUUID };
                serializedDescriptionDictionaryString = new Dictionary<string, string> { ["en"] = "Video of conducted Module" };
                output = new Attachment()
                {
                    UsageType = new Uri("https://febr.is/xApi/attachments/video_review"),
                    //Display = "{\"en\":\""+tempUUID+"\"}",
                    //Description = "{\"en\":\"Video of conducted Module\"}",
                    Display = serializedNameDictionaryString,
                    Description = serializedDescriptionDictionaryString,
                    ContentType = "video/mp4",
                    Length = 0,
                    FileURL = new Uri("https://febr.is/widget/videoloader?videoName=" + tempUUID)
                };
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "LauncherLogic.VideoAttachmentHandler: suppressed exception"); }

            return output;
        }
    }
}
