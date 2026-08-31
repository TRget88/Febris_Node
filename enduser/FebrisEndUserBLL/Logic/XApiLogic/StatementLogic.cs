// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.Models.XApiModels.ExtraModels;
using Febris.ModelLibrary.Models.XApiModels.ModifiedForSharing;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.UserNode.DataAccessLayer.Queries.XAPIQueries;
using Febris.SharedServices;
using Febris.SharedServices.XApi;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Febris.PrimaryLogicLayer.Logic.XApiLogic
{
    public interface IStatementLogic
    {
        Task<List<Statement>> Get();
        Task<Statement> Get(long? id);
        Task<Statement> Get(Guid? input);
        Task<Statement> Create(JObject input);
        Task<Statement> Submit(Statement input);
        Task<Statement> Submit(JObject input);
        // Phase 3.3c typed-DTO BLL entry point -- mirror of the
        // shared IStatementLogic surface. Controllers using the new
        // XApiStatementBinding hand the populated XApiStatementSubmission
        // straight through, eliminating per-controller JObject reparse.
        // The deep typed factor (eliminating the JObject step in
        // StatementFactor itself) will obsolete this bridge later.
        Task<Statement> Submit(Febris.ModelLibrary.ViewModels.XApi.XApiStatementSubmission submission);
        //Task<XApiResultExtras> GetExtras(Statement input);
        Task<XApiResultExtrasViewModel> GetExtras(Statement input);
        Task<LineChart> DailyStatementSubmissionChart();
        Task<LineChart> DailyTimeSpentTesting();
        Task<PieChart> CompletionChart();
        Task<GenericMixedChart> GetStatementDataMixedChart();
        Task<List<Statement>> SearchGet(string searchString);
        Task<LineChart> GetStatementCountDataByActor(Actor input, DateTime start, DateTime end);
        Task<BarChart> GetStatementTimeDataByActor(Actor input, DateTime start, DateTime end);

        /// <summary>
        /// What a given device has SUBMITTED. The read side of
        /// <c>LocalStatement.SubmittedByHardwareUUID</c>, which shipped with two writers and no
        /// reader, so the "investigable rather than indistinguishable" property it was added for
        /// could only be exercised with direct database access.
        ///
        /// <para>
        /// Returns the PERSISTENCE shape rather than the xAPI <c>Statement</c> export shape on
        /// purpose. This is the node's own attribution trail, it is deliberately not part of any
        /// exported statement, and projecting it through the export type is precisely how a
        /// node-internal field ends up leaking into one.
        /// </para>
        /// </summary>
        Task<DeviceSubmissionSummaryViewModel> GetSubmissionsByDevice(Guid hardwareUuid, int limit);
    }

    public class StatementLogic : IStatementLogic
    {
        private readonly IStatementQueries _context;
        private readonly IActorQueries _actorContext;
        //private readonly ILocalStatementQueries _localContext;
        private readonly IVerbQueries _verbContext;
        private readonly IObjectQueries _objectContext;
        private readonly IVersionQueries _versionContext;
        //private readonly IContentDeveloperQueries _devContext;
        //private readonly IAccreditationBodyQueries _accContext;
        private readonly IXApiResultExtrasQueries _extrasContext;
        // Held only to construct StatementFactor with the SAME DI-resolved query instances this
        // logic uses, instead of the factor re-newing its own.
        private readonly IMemberQueries _memberContext;
        private readonly IExtensionsQueries _extensionsContext;
        //private readonly IProfessionalLinkedActorLogic _actorLinkLogic;
        //private readonly IProfessionalLinkedContentDeveloperLogic _professionalLinkedContentDeveloperLogic;
        //private readonly IProfessionalLinkedAccreditationBodyLogic _professionalLinkedAccreditationBodyLogic;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        private readonly IStatementFileHandler _statementFileHandler;
        // _hardware REMOVED 2026-08-10 (audit T2). It was declared here and assigned from
        // HttpContext.Items["Hardware"] in BOTH constructors, and then read NOWHERE in this
        // 2100-line file -- a write side with no read side, in the class that performs the
        // statement write. It looked like a device-to-actor binding and was not one.
        //
        // NOT replaced with a real binding, deliberately. Statements carry no cohort or device
        // coupling at all (verified: no reference to cohort anywhere in the xAPI logic or
        // models), and an xAPI statement is an independent record. Constraining writes through
        // HardwareLinkedCohort would tie a compliance record to mutable membership state, which
        // is the wrong direction. Owner ruling 2026-08-10.

        public StatementLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new StatementQueries();
            User = _httpContextAccessor.HttpContext.User;
            //_devContext = new ContentDeveloperQueries();
            //_accContext = new AccreditationBodyQueries();
            _verbContext = new VerbQueries();
            _versionContext = new VersionQueries();
            _objectContext = new ObjectQueries();
            _extrasContext = new XApiResultExtrasQueries();
            _memberContext = new MemberQueries();
            _extensionsContext = new ExtensionsQueries();
            //_professionalLinkedContentDeveloperLogic = new ProfessionalLinkedContentDeveloperLogic(_httpContextAccessor);
            //_professionalLinkedAccreditationBodyLogic = new ProfessionalLinkedAccreditationBodyLogic(_httpContextAccessor);
            //_actorLinkLogic = new ProfessionalLinkedActorLogic(_httpContextAccessor);
            // _localContext = new LocalStatementQueries();
            _statementFileHandler = new StatementFileHandler();
            _actorContext = new ActorQueries();
        }


        /// <summary>
        /// The device that submitted the current request, or null when there is no device
        /// credential (Portal-originated, a seed, an import).
        ///
        /// <para>
        /// T2. This reads the SAME seam the old <c>_hardware</c> field used before it was removed on
        /// 2026-08-10 for being a write side with no read side. It now has a read side: the value is
        /// recorded on the statement so a record can be traced to its submitter. It is ATTRIBUTION,
        /// never a gate. Nothing here refuses a statement, which keeps the owner ruling against
        /// binding writes through mutable membership state intact.
        /// </para>
        /// </summary>
        private Guid? SubmittingHardwareUuid()
        {
            try
            {
                var items = _httpContextAccessor?.HttpContext?.Items;
                if (items == null || !items.ContainsKey("Hardware")) return null;

                var hardware = items["Hardware"] as Febris.ModelLibrary.Models.DataModels.Hardware;
                if (hardware == null || hardware.UUID == Guid.Empty) return null;

                return hardware.UUID;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementLogic.SubmittingHardwareUuid");
                return null;
            }
        }

        /// <summary>
        /// Default page size for <see cref="GetSubmissionsByDevice"/>. Enough to see a pattern on a
        /// device detail screen without pulling an entire term of a busy headset's activity.
        /// </summary>
        public const int DefaultSubmissionPageSize = 25;

        /// <inheritdoc />
        public async Task<DeviceSubmissionSummaryViewModel> GetSubmissionsByDevice(Guid hardwareUuid, int limit)
        {
            DeviceSubmissionSummaryViewModel output = new DeviceSubmissionSummaryViewModel
            {
                HardwareUUID = hardwareUuid,
                Limit = limit <= 0 ? DefaultSubmissionPageSize : limit
            };

            try
            {
                // Guid.Empty means no device, not "every unattributed statement". The DAL refuses it
                // too; refusing here as well keeps the BLL honest if that query is ever reused.
                if (hardwareUuid == Guid.Empty)
                {
                    return output;
                }

                output.Statements = await _context.GetBySubmittingHardware(hardwareUuid, output.Limit);
                output.TotalCount = await _context.CountBySubmittingHardware(hardwareUuid);

                // Counted over the RETURNED page, not the whole history, and named accordingly at
                // the point of display. Counting distinct actors across every statement a device
                // ever sent would be a second unbounded read for a number nobody is acting on.
                output.DistinctActorCount = output.Statements
                    .Where(s => s.Actor != null)
                    .Select(s => s.Actor.UUID)
                    .Distinct()
                    .Count();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementLogic.GetSubmissionsByDevice");
                throw;
            }

            return output;
        }

        // DI refactor
        public StatementLogic(IHttpContextAccessor httpContextAccessor, IStatementQueries context, IVerbQueries verbContext, IVersionQueries versionContext, IObjectQueries objectContext, IXApiResultExtrasQueries extrasContext, IStatementFileHandler statementFileHandler, IActorQueries actorContext, IMemberQueries memberContext, IExtensionsQueries extensionsContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            User = _httpContextAccessor?.HttpContext?.User;
            //_devContext = new ContentDeveloperQueries();
            //_accContext = new AccreditationBodyQueries();
            _verbContext = verbContext;
            _versionContext = versionContext;
            _objectContext = objectContext;
            _extrasContext = extrasContext;
            _memberContext = memberContext;
            _extensionsContext = extensionsContext;
            //_professionalLinkedContentDeveloperLogic = new ProfessionalLinkedContentDeveloperLogic(_httpContextAccessor);
            //_professionalLinkedAccreditationBodyLogic = new ProfessionalLinkedAccreditationBodyLogic(_httpContextAccessor);
            //_actorLinkLogic = new ProfessionalLinkedActorLogic(_httpContextAccessor);
            // _localContext = new LocalStatementQueries();
            _statementFileHandler = statementFileHandler;
            _actorContext = actorContext;
        }

        public async Task<List<Statement>> Get()
        {
            List<Statement> output = new List<Statement>();
            List<LocalStatement> localStatementList = new List<LocalStatement>();
            //List<Actor> actorList = new List<Actor>();
            try
            {
                #region Filter
                if (User.IsLocalAdmin() || User.IsLocalFebrisAdmin() || User.IsLocalEducator())
                {
                    localStatementList = await _context.Get();
                }
                else if (User.IsLocalParent())
                {
                    ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User);
                    if (scope.AllowedActorUuids.Count == 0) { return default; }
                    localStatementList = await _context.GetByActorList(scope.AllowedActorUuids.ToList());
                }
                else if (User.IsLocalUser())
                {
                    if (User.HasActor())
                    {
                        Guid actorId = Guid.Parse(User.GetActor());
                        localStatementList = await _context.GetByActor(actorId);
                        localStatementList = await CompileStatement(output, localStatementList, actorId);
                    }
                    else { return default; }
                }
                else { return default; }
                #endregion

                await CompileStatementList(output, localStatementList);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;

        }

        public async Task<List<Statement>> Get(DateTime startDate, DateTime endDate)
        {
            List<Statement> output = new List<Statement>();
            List<LocalStatement> localStatementList = new List<LocalStatement>();
            //List<Actor> actorList = new List<Actor>();
            try
            {
                #region Filter
                if (User.IsLocalAdmin() || User.IsLocalFebrisAdmin() || User.IsLocalEducator())
                {
                    localStatementList = await _context.Get(startDate, endDate);
                }
                else if (User.IsLocalParent())
                {
                    ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User);
                    if (scope.AllowedActorUuids.Count == 0) { return default; }
                    localStatementList = await _context.GetByActorList(scope.AllowedActorUuids.ToList(), startDate, endDate);
                }
                else if (User.IsLocalUser())
                {
                    if (User.HasActor())
                    {
                        Guid actorId = Guid.Parse(User.GetActor());
                        localStatementList = await _context.GetByActor(actorId, startDate, endDate);
                        localStatementList = await CompileStatement(output, localStatementList, actorId);
                    }
                    else { return default; }
                }
                else { return default; }
                #endregion


                //if (actorList.Count > 0)
                //{
                //    localStatementList = await _context.GetByActorList(actorList);
                //}
                await CompileStatementList(output, localStatementList);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;

        }

        public async Task<Statement> Get(long? id)
        {
            Statement output = new Statement();
            try
            {
                LocalStatement local = await _context.Get(id);

                #region Filter
                if (User.IsLocalFebrisAdmin() || User.IsLocalAdmin() || User.IsLocalEducator())
                {

                }
                else if (User.IsLocalParent())
                {
                    ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User);
                    if (local == null || local.Actor == null || !scope.AllowedActorUuids.Contains(local.Actor.UUID))
                    {
                        return default;
                    }
                }
                else if (User.IsLocalUser())
                {
                    if (User.HasActor())
                    {
                        var singleActorId = Guid.Parse(User.GetActor());
                        if (singleActorId != local.Actor.UUID)
                        {
                            return default;
                        }
                    }
                }
                else
                {
                    return default;
                }
                #endregion

                Verb tempVerb = await _verbContext.Get(local.VerbId);
                ModelLibrary.Models.XApiModels.Version tempVersion = await _versionContext.Get(local.VersionId);
                ModelLibrary.Models.XApiModels.Object tempObject = await _objectContext.Get(local.ObjectId);
                output = new Statement()
                {
                    Timestamp = local.Timestamp,
                    Stored = local.Stored,
                    Id = local.Id,
                    UUID = local.UUID,
                    Actor = local.Actor,
                    Object = tempObject,
                    Verb = tempVerb,
                    Result = local.Result,
                    Context = local.Context,
                    Authority = local.Authority,
                    Version = tempVersion,
                    Attachments = local.Attachments
                };

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<Statement> Get(Guid? id)
        {
            Statement output = new Statement();
            try
            {
                LocalStatement local = await _context.Get(id);

                #region Filter
                if (User.IsLocalFebrisAdmin() || User.IsLocalAdmin() || User.IsLocalEducator())
                {

                }
                else if (User.IsLocalParent())
                {
                    ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User);
                    if (local == null || local.Actor == null || !scope.AllowedActorUuids.Contains(local.Actor.UUID))
                    {
                        return default;
                    }
                }
                else if (User.IsLocalUser())
                {
                    if (User.HasActor())
                    {
                        var singleActorId = Guid.Parse(User.GetActor());
                        if (singleActorId != local.Actor.UUID)
                        {
                            return default;
                        }
                    }
                }
                else
                {
                    return default;
                }
                #endregion


                Verb tempVerb = await _verbContext.Get(local.VerbId);
                ModelLibrary.Models.XApiModels.Version tempVersion = await _versionContext.Get(local.VersionId);
                ModelLibrary.Models.XApiModels.Object tempObject = await _objectContext.Get(local.ObjectId);
                output = new Statement()
                {
                    Timestamp = local.Timestamp,
                    Stored = local.Stored,
                    Id = local.Id,
                    UUID = local.UUID,
                    Actor = local.Actor,
                    Object = tempObject,
                    Verb = tempVerb,
                    Result = local.Result,
                    Context = local.Context,
                    Authority = local.Authority,
                    Version = tempVersion,
                    Attachments = local.Attachments
                };

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<Statement>> SearchGet(string searchString)
        {
            List<Statement> output = new List<Statement>();
            List<LocalStatement> localStatementList = new List<LocalStatement>();
            try
            {
                #region Filter
                if (User.IsLocalAdmin() || User.IsLocalFebrisAdmin() || User.IsLocalEducator())
                {
                    localStatementList = await _context.SearchGet(searchString);
                }
                else if (User.IsLocalParent())
                {
                    ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User);
                    if (scope.AllowedActorUuids.Count == 0) { return default; }
                    foreach (Guid linkedActorId in scope.AllowedActorUuids)
                    {
                        localStatementList.AddRange(await _context.SearchGet(linkedActorId, searchString));
                    }
                }
                else if (User.IsLocalUser())
                {
                    if (User.HasActor())
                    {
                        Guid actorId = Guid.Parse(User.GetActor());
                        localStatementList = await _context.SearchGet(actorId, searchString);
                        localStatementList = await CompileStatement(output, localStatementList, actorId);
                    }
                    else { return default; }
                }
                else { return default; }
                #endregion
                await CompileStatementList(output, localStatementList);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        private async Task CompileStatementList(List<Statement> output, List<LocalStatement> localStatementList)
        {
            try
            {
                #region Gather batched info
                // Three batched HTTP calls (POST api/{Version,Object,Verb}/batch)
                // replace three foreach loops of single-id awaits, cutting the
                // statement-page assembly from O(N) round-trips to 3.
                List<long> versionIdList = localStatementList.Select(i => i.VersionId).Distinct().ToList();
                List<long> objectIdList = localStatementList.Select(i => i.ObjectId).Distinct().ToList();
                List<long> verbIdList = localStatementList.Select(i => i.VerbId).Distinct().ToList();
                List<ModelLibrary.Models.XApiModels.Version> versionList = await _versionContext.Get(versionIdList);
                List<ModelLibrary.Models.XApiModels.Object> objectList = await _objectContext.Get(objectIdList);
                List<Verb> verbList = await _verbContext.Get(verbIdList);

                // Keyed dictionaries -- O(1) lookups during assembly instead
                // of `.Where().FirstOrDefault()` scans (O(N*M) previously).
                Dictionary<long, ModelLibrary.Models.XApiModels.Version> versionById =
                    versionList?.Where(v => v != null).GroupBy(v => v.Id).ToDictionary(g => g.Key, g => g.First())
                    ?? new Dictionary<long, ModelLibrary.Models.XApiModels.Version>();
                Dictionary<long, ModelLibrary.Models.XApiModels.Object> objectByKey =
                    objectList?.Where(o => o != null).GroupBy(o => o.Key).ToDictionary(g => g.Key, g => g.First())
                    ?? new Dictionary<long, ModelLibrary.Models.XApiModels.Object>();
                Dictionary<long, Verb> verbByKey =
                    verbList?.Where(v => v != null).GroupBy(v => v.Key).ToDictionary(g => g.Key, g => g.First())
                    ?? new Dictionary<long, Verb>();
                #endregion


                foreach (var i in localStatementList)
                {
                    Statement temp = new Statement()
                    {
                        Timestamp = i.Timestamp,
                        Stored = i.Stored,
                        Id = i.Id,
                        UUID = i.UUID,
                        Actor = i.Actor,
                        Result = i.Result,
                        Context = i.Context,
                        Authority = i.Authority,
                        Attachments = i.Attachments
                    };

                    if (objectByKey.TryGetValue(i.ObjectId, out var matchedObject))
                    {
                        temp.Object = matchedObject;
                    }
                    if (verbByKey.TryGetValue(i.VerbId, out var matchedVerb))
                    {
                        temp.Verb = matchedVerb;
                    }
                    if (versionById.TryGetValue(i.VersionId, out var matchedVersion))
                    {
                        temp.Version = matchedVersion;
                    }

                    output.Add(temp);
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<XApiResultExtrasViewModel> GetExtras(Statement input)
        {
            XApiResultExtrasViewModel output = new XApiResultExtrasViewModel();
            XApiResultExtras extras = new XApiResultExtras();
            try
            {
                long statementId = 0;
                Guid statementUUID = Guid.Empty;
                LocalStatement local = new LocalStatement();
                if (input.Id != 0)
                {
                    local = await _context.Get(input.Id);
                }
                else
                {
                    local = await _context.Get(input.UUID);
                }

                #region Filter
                if (User.IsLocalFebrisAdmin() || User.IsLocalAdmin() || User.IsLocalEducator())
                {

                }
                else if (User.IsLocalParent())
                {
                    ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User);
                    if (local == null || local.Actor == null || !scope.AllowedActorUuids.Contains(local.Actor.UUID))
                    {
                        return default;
                    }
                }
                else if (User.IsLocalUser())
                {
                    if (User.HasActor())
                    {
                        var singleActorId = Guid.Parse(User.GetActor());
                        if (singleActorId != local.Actor.UUID)
                        {
                            return default;
                        }
                    }
                }
                else
                {
                    return default;
                }
                #endregion

                extras = await _extrasContext.GetByResult(local.Result);
                if (extras == null)
                {
                    return default;
                }

                RadarChart chart = new RadarChart();
                List<RadarChartEntry> chartList = new List<RadarChartEntry>();

                RadarChartEntry entry1 = new RadarChartEntry()
                {
                    Label = "Success",
                    // == true, not .Value: absent is NOT an assertion of true: xAPI success/completion are OPTIONAL, and a producer that never said so has not said false either,
                    // so an absent value contributes 0 rather than throwing.
                    Quantity = (extras.Result.Success == true ? 1 : 0) * 100
                };
                chartList.Add(entry1);
                RadarChartEntry entry2 = new RadarChartEntry()
                {
                    Label = "Completion",
                    Quantity = (extras.Result.Completion == true ? 1 : 0) * 100
                };
                chartList.Add(entry2);
                RadarChartEntry entry3 = new RadarChartEntry()
                {
                    Label = "Duration in minutes",
                    Quantity = (int)extras.Result.Duration.TotalMinutes
                };
                chartList.Add(entry3);
                RadarChartEntry entry4 = new RadarChartEntry()
                {
                    Label = "Scaled",
                    Quantity = (int)extras.Result.Score.Scaled
                };
                chartList.Add(entry4);
                RadarChartEntry entry5 = new RadarChartEntry()
                {
                    Label = "Raw",
                    Quantity = (int)extras.Result.Score.Raw
                };
                chartList.Add(entry5);
                RadarChartEntry entry6 = new RadarChartEntry()
                {
                    Label = "Restart Count",
                    Quantity = extras.RestartCount
                };
                chartList.Add(entry6);


                chart = new RadarChart()
                {
                    Title = "Result Chart",
                    IdToUse = extras.UUID.ToString(),
                    Description = "results generated from simulation.",
                    ChartEntryList = chartList
                };



                output = new XApiResultExtrasViewModel()
                {
                    XApiResultExtras = extras,
                    RadarChart = chart
                };


            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                //throw;
                return null;
            }
            return output;
        }

        //public async Task<XApiResultExtras> GetExtras(Statement input)
        //{
        //    XApiResultExtras output = new XApiResultExtras();
        //    try
        //    {
        //        long statementId = 0;
        //        Guid statementUUID = Guid.Empty;
        //        LocalStatement local = new LocalStatement();
        //        if (input.Id != 0)
        //        {
        //            local = await _context.Get(input.Id);
        //        }
        //        else
        //        {
        //            local = await _context.Get(input.UUID);
        //        }


        //        output = await _extrasContext.GetByResult(input.Result);

        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}



        public async Task<Statement> Create(JObject input)
        {
            Statement output = new Statement();
            bool success = false;
            try
            {
                // Construct the factor with THIS logic's query instances (DI-scoped when the host
                // resolves the DI ctor) instead of letting it re-new its own.
                StatementFactor _statementFactor = new StatementFactor(_httpContextAccessor, _context, _actorContext, _memberContext, _objectContext, _verbContext, _versionContext, _extensionsContext);
                (output, success) = await _statementFactor.FactorStatement(input);
                if (!success)
                {
                    return null;
                }

                //output = await _context.Create(output);


            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        #region Ingest idempotency (SDKV-19/20)
        // The PC StatementManager and mobile Server upload at-least-once: a
        // lost /Submit response re-POSTs the SAME statement to /Backup, and a
        // crash between a successful upload and the file-move re-uploads it on
        // the next poll. Without node-side dedupe every retry inserted a fresh
        // LocalStatement row (StatementQueries.Create is a blind insert with a
        // DB-generated UUID). These helpers make ingest idempotent on the
        // producer-assigned statement UUID: a statement whose id/uuid is
        // already persisted is treated as SUCCESS and the existing record is
        // returned -- no second insert. Statements carrying NO usable id (the
        // current SDK dialect emits id:0 / uuid:00000000-...) keep the old
        // insert-always behavior.

        /// <summary>
        /// Extracts the producer-assigned statement UUID from an inbound
        /// JObject statement: <c>id</c> first (xAPI 1.0.3 statement ids ARE
        /// UUID strings), then the Febris-dialect <c>uuid</c> slot. Keys are
        /// matched case-insensitively. Placeholder values (the SDK's numeric
        /// <c>id: 0</c> and empty GUID <c>uuid</c>) do not parse / count, so
        /// they yield <see cref="Guid.Empty"/> ("no identifier -- no dedupe").
        /// </summary>
        private static Guid ExtractStatementUuid(JObject input)
        {
            if (input == null)
            {
                return Guid.Empty;
            }
            foreach (string name in new[] { "id", "uuid" })
            {
                JToken token = input.GetValue(name, StringComparison.OrdinalIgnoreCase);
                if (token == null || token.Type == JTokenType.Null)
                {
                    continue;
                }
                if (Guid.TryParse(token.ToString(), out Guid parsed) && parsed != Guid.Empty)
                {
                    return parsed;
                }
            }
            return Guid.Empty;
        }

        /// <summary>
        /// Typed-DTO twin of <see cref="ExtractStatementUuid(JObject)"/>:
        /// reads <c>Dto.Id</c> (spec statement id), then the dialect
        /// <c>uuid</c> key preserved in the DTO's extension data.
        /// </summary>
        private static Guid ExtractStatementUuid(Febris.ModelLibrary.ViewModels.XApi.XApiStatementDto dto)
        {
            if (dto == null)
            {
                return Guid.Empty;
            }
            if (Guid.TryParse(dto.Id, out Guid fromId) && fromId != Guid.Empty)
            {
                return fromId;
            }
            if (dto.ExtensionData != null)
            {
                foreach (KeyValuePair<string, JToken> pair in dto.ExtensionData)
                {
                    if (string.Equals(pair.Key, "uuid", StringComparison.OrdinalIgnoreCase)
                        && pair.Value != null && pair.Value.Type != JTokenType.Null
                        && Guid.TryParse(pair.Value.ToString(), out Guid fromUuid) && fromUuid != Guid.Empty)
                    {
                        return fromUuid;
                    }
                }
            }
            return Guid.Empty;
        }

        /// <summary>
        /// Dedupe lookup: returns the already-persisted statement for
        /// <paramref name="statementUuid"/> (compiled with its Verb / Version /
        /// Object vocabulary like the read paths do), or null when the UUID is
        /// empty or unseen -- in which case the caller proceeds with the
        /// normal insert. Backed by the IX_LocalStatement_UUID index
        /// (StatementUuidDedupeIndex migration).
        /// </summary>
        private async Task<Statement> FindExistingStatement(Guid statementUuid)
        {
            if (statementUuid == Guid.Empty)
            {
                return null;
            }
            // T5: MUST see voided statements. The global query filter hides them from every
            // other read, but dedupe matches on the producer id -- if it could not see a voided
            // row, re-sending that id would insert a SECOND statement with the same producer id,
            // making voiding a way to defeat idempotent ingest.
            LocalStatement existing = await _context.GetIncludingVoided((Guid?)statementUuid);
            if (existing == null || existing.Id == 0)
            {
                return null;
            }

            Verb tempVerb = await _verbContext.Get(existing.VerbId);
            ModelLibrary.Models.XApiModels.Version tempVersion = await _versionContext.Get(existing.VersionId);
            ModelLibrary.Models.XApiModels.Object tempObject = await _objectContext.Get(existing.ObjectId);
            return new Statement()
            {
                Timestamp = existing.Timestamp,
                Stored = existing.Stored,
                Id = existing.Id,
                UUID = existing.UUID,
                Actor = existing.Actor,
                Object = tempObject,
                Verb = tempVerb,
                Result = existing.Result,
                Context = existing.Context,
                Authority = existing.Authority,
                Version = tempVersion,
                Attachments = existing.Attachments
            };
        }
        #endregion

        /// <summary>
        /// Legacy typed-Statement ingest (the parameterless [HttpPost] route).
        /// Resolves the verb/version vocabulary and persists a LocalStatement.
        /// SDKV-19/20: idempotent on the statement UUID -- a re-submitted
        /// statement returns the existing record instead of double-inserting,
        /// and a producer-assigned UUID is persisted (empty UUID keeps the DB
        /// default uuid_generate_v4(), the historical behavior).
        /// </summary>
        public async Task<Statement> Submit(Statement input)
        {
            Statement output = new Statement();
            bool success = false;
            try
            {
                // SDKV-19/20: host retries must not double-commit.
                Statement existingStatement = await FindExistingStatement(input?.UUID ?? Guid.Empty);
                if (existingStatement != null)
                {
                    return existingStatement;
                }

                //StatementFactor _statementFactor = new StatementFactor();
                //(output, success) = await _statementFactor.FactorStatement(input);
                //if (!success)
                //{
                //    return null;
                //}
                // Audit C-03, second half: an actor is mandatory under xAPI 1.0.3, and a null
                // one reaches EF as an FK violation on LocalStatement. Reject at the boundary.
                if (input?.Actor == null)
                {
                    Febris.SharedServices.FebrisLog.ErrorMessage(
                        "StatementLogic: statement rejected -- no actor supplied.");
                    return null;
                }

                Verb verb = await _verbContext.Get(input.Verb.Id);
                if (verb != null)
                {
                    input.Verb = verb;
                }
                else
                {
                    ///Probably need to just create a new verb
                    return null;
                }
                //can create a new verb if it does not exist



                //version
                ModelLibrary.Models.XApiModels.Version version = new ModelLibrary.Models.XApiModels.Version();
                if (input.Version == null)
                {
                    version = await _versionContext.GetLast();
                    input.Version = version;
                }


                LocalStatement localStatement = new LocalStatement()
                {
                    // Audit C-01/T3: xAPI 1.0.3 -- Timestamp is the PRODUCER's time and Stored is
                    // the LRS's. Assign Timestamp explicitly (falling back to now when the producer
                    // sent none) so EF writes it instead of treating the CLR default as "let the
                    // store generate it". Stored is deliberately NOT assigned: it is the store's
                    // own record-keeping value and comes from the column default.
                    Timestamp = input.Timestamp == default ? DateTime.UtcNow : input.Timestamp,
                    // SDKV-19/20: persist under the producer-assigned UUID so a
                    // retry of the same statement dedupes. Guid.Empty (no id on
                    // the wire) keeps the DB default uuid_generate_v4().
                    UUID = input.UUID,
                    Actor = input.Actor,
                    // T2: who SUBMITTED this, as distinct from the actor who performed it.
                    SubmittedByHardwareUUID = SubmittingHardwareUuid(),
                    VerbId = input.Verb.Key,
                    VerbUUID = input.Verb.UUID,
                    ObjectId = input.Object.Key,
                    ObjectUUID = input.Object.UUID,
                    VersionId = input.Version?.Id ?? 0,
                    VersionUUID = input.Version?.UUID ?? Guid.Empty,


                    Result = input.Result ?? null,
                    Context = input.Context ?? null,
                    Authority = input.Authority ?? null,
                    Attachments = input.Attachments ?? null
                };

                localStatement = await _context.Create(localStatement);

                if (localStatement.Result != null)
                {
                    XApiResultExtras extras = StatementFactory.FactorResultExtensionExtras(localStatement.Result);
                    if (extras != null)
                    {
                        extras = await _extrasContext.Create(extras);
                    }
                }

                // Audit C-02: `output` was never assigned, so this method returned a blank
                // Statement -- the caller's `statement.Id != default` was therefore ALWAYS false
                // and the route reported failure for a statement that did persist, leaving clients
                // retrying forever. It also meant the JSON backup below serialized an empty object.
                // Return the submitted statement carrying the keys the store assigned.
                output = input;
                output.Id = localStatement.Id;
                output.UUID = localStatement.UUID;
                output.Timestamp = localStatement.Timestamp;
                output.Stored = localStatement.Stored;

                ///Save the statement

                JObject jObj = (JObject)JToken.FromObject(output);
                bool saved = await SavingJSONStatement(jObj, output);
                if (!saved)
                {
                    Febris.SharedServices.FebrisLog.Info("Attempt to save statement backup has failed");
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        /// <summary>
        /// JObject ingest -- serves the legacy /Backup route directly and the
        /// default /Submit route via the DTO bridge in
        /// <see cref="Submit(Febris.ModelLibrary.ViewModels.XApi.XApiStatementSubmission)"/>.
        /// SDKV-19/20: idempotent on the producer-assigned statement UUID
        /// (spec <c>id</c> or dialect <c>uuid</c>) -- a host retry of an
        /// already-persisted statement returns the existing record as success
        /// instead of double-inserting; statements with no usable id keep the
        /// historical insert-always behavior.
        /// </summary>
        public async Task<Statement> Submit(JObject input)
        {
            Statement output = new Statement();
            bool success = false;
            try
            {
                // SDKV-19/20: dedupe BEFORE factoring -- a retried statement
                // must not re-run persist-on-miss side effects or re-insert.
                Guid inboundUuid = ExtractStatementUuid(input);
                Statement existingStatement = await FindExistingStatement(inboundUuid);
                if (existingStatement != null)
                {
                    return existingStatement;
                }

                // Construct the factor with THIS logic's query instances (DI-scoped when the host
                // resolves the DI ctor) instead of letting it re-new its own.
                StatementFactor _statementFactor = new StatementFactor(_httpContextAccessor, _context, _actorContext, _memberContext, _objectContext, _verbContext, _versionContext, _extensionsContext);
                (output, success) = await _statementFactor.FactorStatement(input);
                if (!success)
                {
                    return null;
                }


                // Audit C-03, second half -- found on 2026-08-05 by running a real statement through
                // the PC uploader, which no unit test could have caught: they all mock IActorQueries
                // to return an actor.
                //
                // Making the DAL lookups return null on a miss was necessary but NOT sufficient.
                // StatementFactor correctly yields a null Actor for an unprovisioned learner, but
                // nothing rejected it, so the null was written onto LocalStatement and EF failed
                // with "violates foreign key constraint FK_LocalStatement_Actor_ActorId" -- turning
                // the old SILENT defect (a blank Actor cascading into an IFI-less ghost row) into an
                // unhandled exception instead of into the REJECTION the audit asked for.
                //
                // The check lives HERE, at the ingest boundary, and deliberately not in
                // StatementFactor: the factor is a parser, and parsing a partial statement is a
                // legitimate thing to do (its own tests do exactly that). Requiring an actor is an
                // INGEST policy -- xAPI 1.0.3 makes actor mandatory -- so it belongs where the write
                // happens.
                if (output?.Actor == null)
                {
                    Febris.SharedServices.FebrisLog.ErrorMessage(
                        "StatementLogic: statement rejected -- its actor is not provisioned on this node.");
                    return null;
                }

                LocalStatement localStatement = new LocalStatement()
                {
                    // Audit C-01/T3: this path set no Timestamp at all, so the factored producer
                    // timestamp was parsed and then dropped, and EF omitted the column from the
                    // INSERT as store-generated. Carry it through, falling back to now.
                    Timestamp = output.Timestamp == default ? DateTime.UtcNow : output.Timestamp,
                    // SDKV-19/20: persist under the producer-assigned UUID so a
                    // retry of the same statement dedupes. Guid.Empty (no id on
                    // the wire) keeps the DB default uuid_generate_v4().
                    UUID = inboundUuid,
                    Actor = output.Actor,
                    // T2: who SUBMITTED this, as distinct from the actor who performed it.
                    SubmittedByHardwareUUID = SubmittingHardwareUuid(),
                    VerbId = output.Verb.Key,
                    VerbUUID = output.Verb.UUID,
                    ObjectId = output.Object.Key,
                    ObjectUUID = output.Object.UUID,
                    VersionId = output.Version?.Id ?? 0,
                    VersionUUID = output.Version?.UUID ?? Guid.Empty,


                    Result = output.Result ?? null,
                    Context = output.Context ?? null,
                    Authority = output.Authority ?? null,
                    Attachments = output.Attachments ?? null
                };

                localStatement = await _context.Create(localStatement);

                if (localStatement.Result != null)
                {
                    XApiResultExtras extras = StatementFactory.FactorResultExtensionExtras(localStatement.Result);
                    if (extras != null)
                    {
                        extras = await _extrasContext.Create(extras);
                    }
                }

                // Audit C-02: back-fill the store-assigned keys onto the returned statement. The
                // controllers decide Success from Id, and without this it stayed 0 on a statement
                // that DID persist.
                output.Id = localStatement.Id;
                output.UUID = localStatement.UUID;
                output.Timestamp = localStatement.Timestamp;
                output.Stored = localStatement.Stored;

                ///Save the statement
                bool saved = await SavingJSONStatement(input, output);
                if (!saved)
                {
                    Febris.SharedServices.FebrisLog.Info("Attempt to save statement backup has failed");
                }

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        /// <summary>
        /// Phase 3.3c-deep cutover flag (EndUser BLL twin). See shared
        /// <c>StatementLogic.UseTypedXApiFactor</c> for design rationale.
        /// Default false; flip to true only after equivalence tests pass.
        /// </summary>
        public static bool UseTypedXApiFactor { get; set; } = false;

        /// <summary>
        /// Phase 3.3c typed-DTO ingest entry point (EndUser BLL twin of
        /// the shared <c>StatementLogic.Submit(XApiStatementSubmission)</c>).
        /// <para>
        /// Routes through the typed factor when <see cref="UseTypedXApiFactor"/>
        /// is true; otherwise bridges to the legacy JObject path via
        /// <c>JObject.FromObject(submission.Dto)</c>. The EndUser SubmitJObject
        /// path additionally calls <c>SavingJSONStatement</c> for legacy
        /// file persistence -- the typed path replicates that step inline
        /// to preserve behavior.
        /// </para>
        /// <para>
        /// SDKV-19/20: BOTH routes are idempotent on the producer-assigned
        /// statement UUID -- the typed branch dedupes here, the bridge
        /// branch dedupes inside <see cref="Submit(JObject)"/>.
        /// </para>
        /// </summary>
        public async Task<Statement> Submit(Febris.ModelLibrary.ViewModels.XApi.XApiStatementSubmission submission)
        {
            if (submission == null) throw new ArgumentNullException(nameof(submission));
            if (submission.Dto == null) throw new ArgumentException("Submission DTO is null; binder rejected this payload upstream.", nameof(submission));

            if (UseTypedXApiFactor)
            {
                // Phase 3.3c-deep typed path. Mirror of Submit(JObject)
                // structure but using the typed factor twin. The
                // SavingJSONStatement step at the end requires a JObject;
                // for the typed path we either skip it (raw bytes already
                // captured by XApiStatementBinding.PersistRawBytesAsync at
                // the controller layer) or build the JObject lazily. To
                // preserve full behavior parity, we still build the JObject
                // (FromObject(submission.Dto)) and call SavingJSONStatement.
                // The "deep" win here is FactorStatement(JObject) being
                // replaced; the legacy JSON-backup file is orthogonal.
                Statement output = new Statement();
                bool success = false;
                try
                {
                    // SDKV-19/20: dedupe BEFORE factoring (typed twin of the
                    // Submit(JObject) check).
                    Guid inboundUuid = ExtractStatementUuid(submission.Dto);
                    Statement existingStatement = await FindExistingStatement(inboundUuid);
                    if (existingStatement != null)
                    {
                        return existingStatement;
                    }

                    // Construct the factor with THIS logic's query instances (DI-scoped when the host
                // resolves the DI ctor) instead of letting it re-new its own.
                StatementFactor _statementFactor = new StatementFactor(_httpContextAccessor, _context, _actorContext, _memberContext, _objectContext, _verbContext, _versionContext, _extensionsContext);
                    (output, success) = await _statementFactor.FactorStatementFromDto(submission.Dto);
                    if (!success)
                    {
                        return null;
                    }


                    // Audit C-03, second half -- found on 2026-08-05 by running a real statement through
                    // the PC uploader, which no unit test could have caught: they all mock IActorQueries
                    // to return an actor.
                    //
                    // Making the DAL lookups return null on a miss was necessary but NOT sufficient.
                    // StatementFactor correctly yields a null Actor for an unprovisioned learner, but
                    // nothing rejected it, so the null was written onto LocalStatement and EF failed
                    // with "violates foreign key constraint FK_LocalStatement_Actor_ActorId" -- turning
                    // the old SILENT defect (a blank Actor cascading into an IFI-less ghost row) into an
                    // unhandled exception instead of into the REJECTION the audit asked for.
                    //
                    // The check lives HERE, at the ingest boundary, and deliberately not in
                    // StatementFactor: the factor is a parser, and parsing a partial statement is a
                    // legitimate thing to do (its own tests do exactly that). Requiring an actor is an
                    // INGEST policy -- xAPI 1.0.3 makes actor mandatory -- so it belongs where the write
                    // happens.
                    if (output?.Actor == null)
                    {
                        Febris.SharedServices.FebrisLog.ErrorMessage(
                            "StatementLogic: statement rejected -- its actor is not provisioned on this node.");
                        return null;
                    }

                    LocalStatement localStatement = new LocalStatement()
                    {
                        // Audit C-01/T3: carry the producer's timestamp (twin of the JObject path).
                        Timestamp = output.Timestamp == default ? DateTime.UtcNow : output.Timestamp,
                        // SDKV-19/20: persist under the producer-assigned UUID
                        // (Guid.Empty keeps the DB default uuid_generate_v4()).
                        UUID = inboundUuid,
                        Actor = output.Actor,
                        VerbId = output.Verb.Key,
                        VerbUUID = output.Verb.UUID,
                        ObjectId = output.Object.Key,
                        ObjectUUID = output.Object.UUID,
                        VersionId = output.Version?.Id ?? 0,
                        VersionUUID = output.Version?.UUID ?? Guid.Empty,
                        Result = output.Result ?? null,
                        Context = output.Context ?? null,
                        Authority = output.Authority ?? null,
                        Attachments = output.Attachments ?? null
                    };
                    localStatement = await _context.Create(localStatement);

                    if (localStatement.Result != null)
                    {
                        XApiResultExtras extras = StatementFactory.FactorResultExtensionExtras(localStatement.Result);
                        if (extras != null)
                        {
                            extras = await _extrasContext.Create(extras);
                        }
                    }

                    // Audit C-02: back-fill the store-assigned keys (twin of the JObject path).
                    output.Id = localStatement.Id;
                    output.UUID = localStatement.UUID;
                    output.Timestamp = localStatement.Timestamp;
                    output.Stored = localStatement.Stored;

                    // Legacy file-backup mirror: SavingJSONStatement takes
                    // a JObject. Build it on-demand so the JObject allocation
                    // is paid only for the file-backup pass, not for the
                    // factor pass (which is what's been optimized away).
                    JObject jobj = JObject.FromObject(submission.Dto);
                    bool saved = await SavingJSONStatement(jobj, output);
                    if (!saved)
                    {
                        Febris.SharedServices.FebrisLog.Info("Attempt to save statement backup has failed");
                    }
                }
                catch (Exception ex)
                {
                    Febris.SharedServices.FebrisLog.Error(ex);
                    throw;
                }
                return output;
            }

            // Legacy JObject bridge (default path).
            JObject jobjBridge = JObject.FromObject(submission.Dto);
            return await Submit(jobjBridge);
        }

        public async Task<LineChart> DailyStatementSubmissionChart()
        {
            LineChart output = new LineChart()
            {
                Title = "Daily Statement Submissions",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "Mapping test user submissions",
                ChartEntryList = new List<LineChartEntry>()
            };
            List<LineChartEntry> entryList = new List<LineChartEntry>();

            List<LocalStatement> localStatementList = new List<LocalStatement>();
            //List<Actor> actorList = new List<Actor>();
            List<long> actorList = new List<long>();
            //List<Professional> professionalList = new List<Professional>();
            try
            {
                #region Filter
                if (User.IsLocalFebrisAdmin() || User.IsLocalAdmin() || User.IsLocalEducator())
                {

                }
                //else if (User.IsLocalParent() || User.IsLocalUser())
                //{
                //    if (User.HasActor())
                //    {
                //        var singleActorId = Guid.Parse(User.GetActor());
                //        if (singleActorId != local.Actor.UUID)
                //        {
                //            return default;
                //        }
                //    }
                //}
                else
                {
                    return default;
                }
                #endregion

                if (actorList.Count > 0)
                {
                    localStatementList = await _context.GetByActorList(actorList);
                }

                localStatementList.OrderBy(i => i.Stored).ToList();
                //tempList.OrderByDescending(i => i.CreationTimeStamp).ToList();
                foreach (var i in localStatementList)
                {
                    if (entryList.Any(j => j.Label == i.Stored.ToShortDateString()))
                    {
                        LineChartEntry thing = entryList.Where(j => j.Label == i.Stored.ToShortDateString()).First();
                        thing.Quantity++;
                    }
                    else
                    {
                        LineChartEntry temp = new LineChartEntry()
                        {
                            Label = i.Stored.ToShortDateString(),
                            Quantity = 1
                        };
                        entryList.Add(temp);
                    }
                }
                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                //throw;
            }
            return output;
        }

        public async Task<LineChart> DailyTimeSpentTesting()
        {
            LineChart output = new LineChart()
            {
                Title = "Daily Time",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "module testing",
                ChartEntryList = new List<LineChartEntry>()
            };
            List<LineChartEntry> entryList = new List<LineChartEntry>();

            List<LocalStatement> localStatementList = new List<LocalStatement>();
            //List<Actor> actorList = new List<Actor>();
            List<long> actorList = new List<long>();
            //List<Professional> professionalList = new List<Professional>();
            try
            {
                #region Filter
                if (User.IsLocalFebrisAdmin() || User.IsLocalAdmin() || User.IsLocalEducator())
                {

                }
                //else if (User.IsLocalParent() || User.IsLocalUser())
                //{
                //    if (User.HasActor())
                //    {
                //        var singleActorId = Guid.Parse(User.GetActor());
                //        if (singleActorId != local.Actor.UUID)
                //        {
                //            return default;
                //        }
                //    }
                //}
                else
                {
                    return default;
                }
                #endregion


                if (actorList.Count > 0)
                {
                    localStatementList = await _context.GetByActorList(actorList);
                }

                localStatementList.OrderBy(i => i.Stored).ToList();
                localStatementList = localStatementList.Where(i => i.Result != null).ToList();
                //tempList.OrderByDescending(i => i.CreationTimeStamp).ToList();
                foreach (var i in localStatementList)
                {
                    if (entryList.Any(j => j.Label == i.Stored.ToShortDateString()))
                    {
                        LineChartEntry thing = entryList.Where(j => j.Label == i.Stored.ToShortDateString()).First();
                        thing.Quantity += (int)i.Result?.Duration.TotalMinutes;
                    }
                    else
                    {
                        LineChartEntry temp = new LineChartEntry()
                        {
                            Label = i.Stored.ToShortDateString(),
                            Quantity = (int)i.Result?.Duration.TotalMinutes
                        };
                        entryList.Add(temp);
                    }
                }
                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                //throw;
            }
            return output;
        }

        public async Task<PieChart> CompletionChart()
        {
            PieChart output = new PieChart()
            {
                Title = "Completed",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "modules",
                ChartEntryList = new List<PieChartEntry>()
            };
            List<PieChartEntry> entryList = new List<PieChartEntry>();
            try
            {
                List<LocalStatement> localStatementList = new List<LocalStatement>();
                //List<Actor> actorList = new List<Actor>();
                List<long> actorList = new List<long>();

                #region Filter
                if (User.IsLocalFebrisAdmin() || User.IsLocalAdmin() || User.IsLocalEducator())
                {

                }
                //else if (User.IsLocalParent() || User.IsLocalUser())
                //{
                //    if (User.HasActor())
                //    {
                //        var singleActorId = Guid.Parse(User.GetActor());
                //        if (singleActorId != local.Actor.UUID)
                //        {
                //            return default;
                //        }
                //    }
                //}
                else
                {
                    return default;
                }
                #endregion

                if (actorList.Count > 0)
                {
                    localStatementList = await _context.GetByActorList(actorList);
                }

                localStatementList.OrderBy(i => i.Stored).ToList();


                PieChartEntry active = new PieChartEntry()
                {
                    Label = "Complete",
                    Quantity = 0
                };
                entryList.Add(active);
                PieChartEntry notActive = new PieChartEntry()
                {
                    Label = "Not Complete",
                    Quantity = 0
                };
                entryList.Add(notActive);



                List<LocalStatement> tempList = localStatementList.Where(i => i.Result != null).ToList();

                int uncompleted = localStatementList.Count() - tempList.Count();
                if (uncompleted > 0)
                {
                    PieChartEntry thing = entryList
                            .Where(j => j.Label == "Not Complete")
                            .First();
                    thing.Quantity = uncompleted;
                }


                foreach (var i in tempList)
                {
                    // == true: an absent completion is not a completion.
                    if (i.Result.Completion == true)
                    {
                        PieChartEntry thing = entryList
                            .Where(j => j.Label == "Complete")
                            .First();
                        thing.Quantity++;
                    }
                    else
                    {
                        PieChartEntry thing = entryList
                           .Where(j => j.Label == "Not Complete")
                           .First();
                        thing.Quantity++;
                    }
                }

                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                //throw;
            }
            return output;
        }

        #region Helpers
        private JObject CleanupDefaultValues(JObject statementJObject)
        {
            //test if id and uuid and key is set to default value
            if (statementJObject.ContainsKey("object") || statementJObject.ContainsKey("Object"))
            {
                SetPropertyToNull(statementJObject, "object", "key", null);
                SetPropertyToNull(statementJObject, "object", "uuid", null);
                SetPropertyToNull(statementJObject, "object", "definition", "id");
                SetPropertyToNull(statementJObject, "object", "definition", "uuid");
                SetPropertyToNull(statementJObject, "object", "definition", "extensions", "id");
                SetPropertyToNull(statementJObject, "object", "definition", "extensions", "uuid");
            }
            if (statementJObject.ContainsKey("result") || statementJObject.ContainsKey("Result"))
            {
                SetPropertyToNull(statementJObject, "result", "id", null);
                SetPropertyToNull(statementJObject, "result", "uuid", null);
                SetPropertyToNull(statementJObject, "result", "score", "id");
                SetPropertyToNull(statementJObject, "result", "score", "uuid");
                SetPropertyToNull(statementJObject, "result", "extensions", "id");
                SetPropertyToNull(statementJObject, "result", "extensions", "uuid");
            }
            if (statementJObject.ContainsKey("id") || statementJObject.ContainsKey("Id"))
            {
                SetPropertyToNull(statementJObject, "uuid", null, null);
                SetPropertyToNull(statementJObject, "id", null, null);
            }
            if (statementJObject.ContainsKey("actor") || statementJObject.ContainsKey("Actor"))
            {
                SetPropertyToNull(statementJObject, "actor", "member", "id");
                SetPropertyToNull(statementJObject, "actor", "account", "id");
                SetPropertyToNull(statementJObject, "actor", "member", "uuid");
                SetPropertyToNull(statementJObject, "actor", "account", "uuid");
            }
            if (statementJObject.ContainsKey("context") || statementJObject.ContainsKey("Context"))
            {
                SetPropertyToNull(statementJObject, "context", "id", null);
                SetPropertyToNull(statementJObject, "context", "uuid", null);
                SetPropertyToNull(statementJObject, "context", "instructor", "id");
                SetPropertyToNull(statementJObject, "context", "instructor", "uuid");
                SetPropertyToNull(statementJObject, "context", "contextactivities", "id");
                SetPropertyToNull(statementJObject, "context", "contextactivities", "uuid");
                SetPropertyToNull(statementJObject, "context", "statementreference", "key");
                SetPropertyToNull(statementJObject, "context", "statementreference", "id");
                SetPropertyToNull(statementJObject, "context", "statementreference", "uuid");
                SetPropertyToNull(statementJObject, "context", "extensions", "id");
                SetPropertyToNull(statementJObject, "context", "extensions", "uuid");
                try
                {
                    if ((string)statementJObject["context"]["statementreference"]["id"] == "00000000-0000-0000-0000-000000000000")
                    {
                        SetPropertyToNull(statementJObject, "context", "statementreference", "id");
                    }
                }
                // intentional: optional JSON path probe; absent statementreference/id is expected
                catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Warn("StatementLogic.CleanupDefaultValues: optional context.statementreference.id probe missed"); }

                try
                {
                    if ((string)statementJObject["context"]["registration"] == "00000000-0000-0000-0000-000000000000")
                    {
                        SetPropertyToNull(statementJObject, "context", "registration", null);
                    }
                }
                // intentional: optional JSON path probe; absent context.registration is expected
                catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Warn("StatementLogic.CleanupDefaultValues: optional context.registration probe missed"); }


            }
            if (statementJObject.ContainsKey("authority") || statementJObject.ContainsKey("Authority"))
            {
                SetPropertyToNull(statementJObject, "authority", "id", null);
                SetPropertyToNull(statementJObject, "authority", "uuid", null);
                SetPropertyToNull(statementJObject, "authority", "actor", "id");
                SetPropertyToNull(statementJObject, "authority", "actor", "uuid");
            }
            if (statementJObject.ContainsKey("version") || statementJObject.ContainsKey("Version"))
            {
                SetPropertyToNull(statementJObject, "version", "id", null);
                SetPropertyToNull(statementJObject, "version", "uuid", null);
            }
            if (statementJObject.ContainsKey("attachments") || statementJObject.ContainsKey("Attachments"))
            {
                for (var i = 0; i < statementJObject["attachments"].Count(); i++)
                {
                    SetPropertyToNull(statementJObject, "attachments", i, "id");
                    SetPropertyToNull(statementJObject, "attachments", i, "uuid");
                }
            }


            return statementJObject;
        }

        #region set null properties
        private static void SetPropertyToNull(JObject jobj, string root)
        {
            try
            {
                jobj[root] = null;
            }
            // intentional: best-effort optional JSON null-set; missing path is expected
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Warn("StatementLogic.SetPropertyToNull(root): optional JSON path absent"); }
        }
        private static void SetPropertyToNull(JObject jobj, string root, string child1)
        {
            try
            {
                if (child1 != null && root != null)
                {
                    jobj[root][child1] = null;
                }
                else if (root != null)
                {
                    jobj[root] = null;
                }
            }
            // intentional: best-effort optional JSON null-set; missing path is expected
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Warn("StatementLogic.SetPropertyToNull(root,child1): optional JSON path absent"); }
        }
        private static void SetPropertyToNull(JObject jobj, string root, string child1, string child2)
        {
            try
            {
                if (child2 != null && child1 != null && root != null)
                {
                    jobj[root][child1][child2] = null;
                }
                else if (child1 != null && root != null)
                {
                    jobj[root][child1] = null;
                }
                else if (root != null)
                {
                    jobj[root] = null;
                }
            }
            // intentional: best-effort optional JSON null-set; missing path is expected
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Warn("StatementLogic.SetPropertyToNull(root,child1,child2): optional JSON path absent"); }
        }
        private static void SetPropertyToNull(JObject jobj, string root, string child1, string child2, string child3)
        {
            try
            {
                if (child2 != null && child3 != null && child1 != null && root != null)
                {
                    jobj[root][child1][child2][child3] = null;
                }
                else if (child1 != null && root != null)
                {
                    jobj[root][child1] = null;
                }
                else if (root != null)
                {
                    jobj[root] = null;
                }
            }
            // intentional: best-effort optional JSON null-set; missing path is expected
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Warn("StatementLogic.SetPropertyToNull(root,child1,child2,child3): optional JSON path absent"); }
        }
        private static void SetPropertyToNull(JObject jobj, string root, int child1, string child2)
        {
            try
            {
                if (child2 != null && child1 != null && root != null)
                {
                    jobj[root][child1][child2] = null;
                }
                else if (child1 != null && root != null)
                {
                    jobj[root][child1] = null;
                }
                else if (root != null)
                {
                    jobj[root] = null;
                }
            }
            // intentional: best-effort optional JSON null-set; missing path is expected
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Warn("StatementLogic.SetPropertyToNull(root,int child1,child2): optional JSON path absent"); }
        }
        #endregion
        private static JToken RemoveNullTokens(JToken token)
        {
            try
            {
                if (token.Type == JTokenType.Object)
                {
                    JObject copy = new JObject();
                    foreach (JProperty prop in token.Children<JProperty>())
                    {
                        JToken child = prop.Value;
                        if (child.HasValues)
                        {
                            child = RemoveNullTokens(child);
                        }
                        if (!IsEmpty(child))
                        {
                            copy.Add(prop.Name, child);
                        }
                    }
                    return copy;
                }
                else if (token.Type == JTokenType.Array)
                {
                    JArray copy = new JArray();
                    foreach (JToken item in token.Children())
                    {
                        JToken child = item;
                        if (child.HasValues)
                        {
                            child = RemoveNullTokens(child);
                        }
                        if (!IsEmpty(child))
                        {
                            copy.Add(child);
                        }
                    }
                    return copy;
                }
                return token;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "StatementLogic.RemoveNullTokens: suppressed exception"); return null; }
        }

        public static bool IsEmpty(JToken token)
        {
            return (token.Type == JTokenType.Null);
        }

        private async Task<bool> SavingJSONStatement(JObject statementJObject, Statement statement)
        {
            try
            {
                string stringifiedStatement = JsonConvert.SerializeObject(statement);
                JObject jobj = JsonConvert.DeserializeObject<JObject>(stringifiedStatement);
                ChangePropertiesToLowerCase(jobj);
                string root = string.Empty;
                string child1 = string.Empty;
                string child2 = string.Empty;

                if (statementJObject.ContainsKey("result") || statementJObject.ContainsKey("Result"))
                {
                    string isoTimeSpan = XmlConvert.ToString(statement.Result.Duration);
                    jobj["result"]["duration"] = isoTimeSpan;
                    SetPropertyToNull(jobj, "result", "id", null);
                    SetPropertyToNull(jobj, "result", "uuid", null);
                    SetPropertyToNull(jobj, "result", "score", "id");
                    SetPropertyToNull(jobj, "result", "score", "uuid");
                    SetPropertyToNull(jobj, "result", "extensions", "id");
                    SetPropertyToNull(jobj, "result", "extensions", "uuid");
                }
                else
                {
                    SetPropertyToNull(jobj, "result", "id", null);
                    SetPropertyToNull(jobj, "result", "uuid", null);
                    SetPropertyToNull(jobj, "result", "score", "id");
                    SetPropertyToNull(jobj, "result", "score", "uuid");
                    SetPropertyToNull(jobj, "result", "extensions", "id");
                    SetPropertyToNull(jobj, "result", "extensions", "uuid");
                }
                if (jobj.ContainsKey("id") || jobj.ContainsKey("Id"))
                {
                    jobj["id"] = jobj["uuid"];
                    SetPropertyToNull(jobj, "uuid", null, null);
                }
                if (statementJObject.ContainsKey("actor") || statementJObject.ContainsKey("Actor"))
                {
                    SetPropertyToNull(jobj, "actor", "id", null);
                    SetPropertyToNull(jobj, "actor", "uuid", null);
                    SetPropertyToNull(jobj, "actor", "member", "id");
                    SetPropertyToNull(jobj, "actor", "account", "id");
                }
                if (statementJObject.ContainsKey("verb") || statementJObject.ContainsKey("Verb"))
                {
                    SetPropertyToNull(jobj, "verb", "key", null);
                    SetPropertyToNull(jobj, "verb", "uuid", null);
                }
                if (statementJObject.ContainsKey("object") || statementJObject.ContainsKey("Object"))
                {
                    SetPropertyToNull(jobj, "object", "key", null);
                    SetPropertyToNull(jobj, "object", "uuid", null);
                    SetPropertyToNull(jobj, "object", "definition", "id");
                    SetPropertyToNull(jobj, "object", "definition", "uuid");
                }
                if (statementJObject.ContainsKey("context") || statementJObject.ContainsKey("Context"))
                {
                    SetPropertyToNull(jobj, "context", "id", null);
                    SetPropertyToNull(jobj, "context", "uuid", null);
                    SetPropertyToNull(jobj, "context", "instructor", "id");
                    SetPropertyToNull(jobj, "context", "actor", "id");
                    SetPropertyToNull(jobj, "context", "actor", "uuid");
                    SetPropertyToNull(jobj, "context", "contextactivities", "id");
                    SetPropertyToNull(jobj, "context", "contextactivities", "uuid");
                    SetPropertyToNull(jobj, "context", "statementreference", "key");
                    SetPropertyToNull(jobj, "context", "statementreference", "uuid");
                }
                if (statementJObject.ContainsKey("authority") || statementJObject.ContainsKey("Authority"))
                {
                    SetPropertyToNull(jobj, "authority", "id", null);
                    SetPropertyToNull(jobj, "authority", "uuid", null);
                    SetPropertyToNull(jobj, "authority", "actor", "id");
                    SetPropertyToNull(jobj, "authority", "actor", "uuid");
                }
                if (statementJObject.ContainsKey("version") || statementJObject.ContainsKey("Version"))
                {
                    SetPropertyToNull(jobj, "version", "id", null);
                    SetPropertyToNull(jobj, "version", "uuid", null);
                }
                if (statementJObject.ContainsKey("attachments") || statementJObject.ContainsKey("Attachments"))
                {
                    SetPropertyToNull(jobj, "attachments", "id", null);
                    SetPropertyToNull(jobj, "attachments", "uuid", null);
                }

                //stringifiedStatement = JsonConvert.SerializeObject(jobj);
                stringifiedStatement = jsonConversionToRemoveNulls(jobj);
                stringifiedStatement = JSONCharacterRemoval(stringifiedStatement);
                bool complete = await _statementFileHandler.UploadPackage(statement.UUID.ToString(), stringifiedStatement);
                return complete;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                return false;
            }
        }

        private static string JSONCharacterRemoval(string input)
        {
            var sb = new StringBuilder(input);
            sb.Replace(@"\", string.Empty);
            sb.Replace("\"{", "{");
            sb.Replace("}\"", "}");
            var testDataInput = sb.ToString();
            return testDataInput;
        }

        //#########################################################################################################################        
        // convert jobject keys to lowercase (cant do this with everything though)
        //#########################################################################################################################
        private static void ChangePropertiesToLowerCase(JObject jsonObject)
        {
            foreach (var property in jsonObject.Properties().ToList())
            {
                if (property.Value.Type == JTokenType.Object)// replace property names in child object
                    ChangePropertiesToLowerCase((JObject)property.Value);

                if (property.Value.Type == JTokenType.Array)
                {
                    var arr = JArray.Parse(property.Value.ToString());
                    foreach (var pr in arr)
                    {
                        ChangePropertiesToLowerCase((JObject)pr);
                    }

                    property.Value = arr;
                }

                property.Replace(new JProperty(property.Name.ToLower(), property.Value));// properties are read-only, so we have to replace them
            }
        }

        //#########################################################################################################################        
        // Json convert to remove nulls
        //#########################################################################################################################
        private string jsonConversionToRemoveNulls(JObject input)
        {
            try
            {
                JToken token = RemoveNullTokens(input);
                string convertedStatement = JsonConvert.SerializeObject(token, new JsonSerializerSettings()
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Ignore
                });
                return convertedStatement;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementLogic.jsonConversionToRemoveNulls: suppressed exception");
                return null;
            }
        }

        #endregion

        public async Task<GenericMixedChart> GetStatementDataMixedChart()
        {
            GenericMixedChart output = new GenericMixedChart()
            {
                Title = "Statement Breakdown",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "30 Days",
                GenericChartList = new List<GenericChart>()
            };
            List<GenericChartEntry> objectPieChartEntryList = new List<GenericChartEntry>();
            List<GenericChartEntry> verbPieChartEntryList = new List<GenericChartEntry>();
            List<GenericChartEntry> timeLineChartEntryList = new List<GenericChartEntry>();
            List<GenericChartEntry> quantityLineChartEntryList = new List<GenericChartEntry>();
            try
            {
                //Build lists
                GenericChart timeLineChart = new GenericChart()
                {
                    ChartType = ChartType.Line,
                    GenericChartEntryList = timeLineChartEntryList
                };
                GenericChart quantityLineChart = new GenericChart()
                {
                    ChartType = ChartType.Line,
                    GenericChartEntryList = quantityLineChartEntryList
                };
                GenericChart objectPieChart = new GenericChart()
                {
                    ChartType = ChartType.Pie,
                    GenericChartEntryList = objectPieChartEntryList
                };
                GenericChart verbPieChart = new GenericChart()
                {
                    ChartType = ChartType.Pie,
                    GenericChartEntryList = verbPieChartEntryList
                };
                output.GenericChartList.Add(timeLineChart);
                output.GenericChartList.Add(quantityLineChart);
                output.GenericChartList.Add(verbPieChart);
                output.GenericChartList.Add(objectPieChart);
                List<Statement> tempList = default;
                //DateTime startDate = DateTime.UtcNow.AddYears(-1).Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;
                DateTime endDate = DateTime.UtcNow.Date;
#if (DEBUG)
                tempList = await Get();
                //if (User.IsLocalAdmin() || User.IsLocalFebrisAdmin() || User.IsLocalEducator())
                //{
                //    tempList = await Get();
                //}
                //else if (User.IsLocalUser() || User.IsLocalParent())
                //{
                //    Guid actorId = Guid.Parse(User.GetActor());
                //    List<LocalStatement> localStatementList = await _context.GetByActor(actorId); 
                //    localStatementList = await CompileStatement(tempList, localStatementList, actorId);
                //}
                //else { return default; }                
#else                
tempList = await Get(startDate, endDate);
                //if (User.IsLocalAdmin() || User.IsLocalFebrisAdmin() || User.IsLocalEducator())
                //{
                //    tempList = await Get(DateTime.UtcNow.AddDays(-30).Date, DateTime.UtcNow.Date);
                //}
                //else if (User.IsLocalUser() || User.IsLocalParent())
                //{
                //    Guid actorId = Guid.Parse(User.GetActor());
                //    List<LocalStatement> localStatementList = await _context.GetByActor(actorId, DateTime.UtcNow.AddDays(-30).Date, DateTime.UtcNow.Date);
                //    localStatementList = await CompileStatement(tempList, localStatementList, actorId);
                //}
                //else { return default; }


 //tempList = await Get(DateTime.UtcNow.AddDays(-30).Date, DateTime.UtcNow.Date);
#endif
                //List<Statement> tempList = await Get(DateTime.UtcNow.AddDays(-30).Date, DateTime.UtcNow.Date);
                tempList = tempList.OrderBy(i => i.Timestamp).ToList();

                for (DateTime i = startDate; endDate >= i; i = i.AddDays(1))
                {
                    int qty = tempList.Where(j => j.Timestamp.Date == i).Count();
                    GenericChartEntry temp = new GenericChartEntry()
                    {
                        Label = i.ToShortDateString(),
                        Quantity = qty
                    };
                    quantityLineChartEntryList.Add(temp);
                }

                for (DateTime i = startDate; endDate >= i; i = i.AddDays(1))
                {
                    double qty = tempList.Where(j => j.Timestamp.Date == i)
                        .Select(i => i.Result != null && i.Result.Duration != null ? i.Result.Duration.TotalMinutes : 0)
                        .Sum();
                    //.Select(i => i.Result?.Duration?.TotalMinutes ?? 0).Sum();
                    GenericChartEntry temp = new GenericChartEntry()
                    {
                        Label = i.ToShortDateString(),
                        Quantity = (int)qty
                    };
                    timeLineChartEntryList.Add(temp);
                }



                List<Verb> verbList = tempList.Select(i => i.Verb).Distinct().ToList();
                List<ModelLibrary.Models.XApiModels.Object> objectList = tempList.Select(i => i.Object).Distinct().ToList();

                foreach (var i in verbList)
                {
                    GenericChartEntry pieTemp = new GenericChartEntry()
                    {
                        Label = i.Id.ToString(),
                        Quantity = tempList.Where(j => j.Verb.Key == i.Key).Count()
                    };
                    verbPieChartEntryList.Add(pieTemp);
                }
                foreach (var i in objectList)
                {
                    GenericChartEntry pieTemp = new GenericChartEntry()
                    {
                        Label = i.Id.ToString(),
                        Quantity = tempList.Where(j => j.Object.Key == i.Key).Count()
                    };
                    objectPieChartEntryList.Add(pieTemp);
                }


                //foreach (var i in tempList)
                //{
                //    /////add to line charts
                //    /////Time
                //    //if (timeLineChartEntryList?.Any(j => j.Label == i.Timestamp.ToShortDateString()) ?? false)
                //    //{
                //    //    GenericChartEntry lineTemp = timeLineChartEntryList.Where(j => j.Label == i.Timestamp.ToShortDateString()).FirstOrDefault();
                //    //    if (lineTemp != default)
                //    //    {
                //    //        lineTemp.Quantity++;
                //    //    }

                //    //}
                //    //else
                //    //{
                //    //    GenericChartEntry lineTemp = new GenericChartEntry()
                //    //    {
                //    //        Label = i.Timestamp.ToShortDateString(),
                //    //        Quantity = 1
                //    //    };
                //    //    timeLineChartEntryList.Add(lineTemp);
                //    //}
                //    /////Quantity
                //    //if (quantityLineChartEntryList?.Any(j => j.Label == i.Timestamp.ToShortDateString()) ?? false)
                //    //{
                //    //    GenericChartEntry lineTemp = quantityLineChartEntryList.Where(j => j.Label == i.Timestamp.ToShortDateString()).FirstOrDefault();
                //    //    if (lineTemp != default)
                //    //    {
                //    //        lineTemp.Quantity++;
                //    //    }
                //    //}
                //    //else
                //    //{
                //    //    GenericChartEntry lineTemp = new GenericChartEntry()
                //    //    {
                //    //        Label = i.Timestamp.ToShortDateString(),
                //    //        Quantity = 1
                //    //    };
                //    //    quantityLineChartEntryList.Add(lineTemp);
                //    //}

                //    /////add to pie charts
                //    /////Verb
                //    ////if (verbPieChartEntryList.Any(j => j.Label == (i.Verb?.Id??"Default").ToString()))
                //    //if (verbPieChartEntryList?.Any(j => j.Label == i?.Verb?.Id?.ToString()) ?? false)
                //    //{
                //    //    GenericChartEntry pieTemp = verbPieChartEntryList.Where(j => j.Label == i.Verb.Id.ToString()).FirstOrDefault();
                //    //    if (pieTemp != default)
                //    //    {
                //    //        pieTemp.Quantity++;
                //    //    }
                //    //}
                //    //else
                //    //{
                //    //    if (i.Verb != default)
                //    //    {
                //    //        GenericChartEntry pieTemp = new GenericChartEntry()
                //    //        {
                //    //            Label = i?.Verb?.Id?.ToString(),
                //    //            Quantity = 1
                //    //        };
                //    //        verbPieChartEntryList.Add(pieTemp);
                //    //    }
                //    //}

                //    /////Object
                //    //if (objectPieChartEntryList?.Any(j => j.Label == i.Object.Id.ToString()) ?? false)
                //    //{
                //    //    GenericChartEntry pieTemp = objectPieChartEntryList.Where(j => j.Label == i.Object.Id.ToString()).FirstOrDefault();
                //    //    if (pieTemp != default)
                //    //    {
                //    //        pieTemp.Quantity++;
                //    //    }
                //    //}
                //    //else
                //    //{
                //    //    GenericChartEntry pieTemp = new GenericChartEntry()
                //    //    {
                //    //        Label = i.Object.Id.ToString(),
                //    //        Quantity = 1
                //    //    };
                //    //    objectPieChartEntryList.Add(pieTemp);
                //    //}
                //}

                //output = await ManagementAlgorithms.OrderChartLists(output);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                //throw;
            }
            return output;
        }

        private async Task<List<LocalStatement>> CompileStatement(List<Statement> output, List<LocalStatement> localStatementList, List<Guid> actorList)
        {
            if (actorList.Count > 0)
            {
                localStatementList = await _context.GetByActorList(actorList);
            }

            foreach (var i in localStatementList)
            {
                Verb tempVerb = await _verbContext.Get(i.VerbId);
                ModelLibrary.Models.XApiModels.Version tempVersion = await _versionContext.Get(i.VersionId);
                ModelLibrary.Models.XApiModels.Object tempObject = await _objectContext.Get(i.ObjectId);
                Statement temp = new Statement()
                {
                    Timestamp = i.Timestamp,
                    Stored = i.Stored,
                    Id = i.Id,
                    UUID = i.UUID,
                    Actor = i.Actor,
                    Object = tempObject,
                    Verb = tempVerb,
                    Result = i.Result,
                    Context = i.Context,
                    Authority = i.Authority,
                    Version = tempVersion,
                    Attachments = i.Attachments
                };
                output.Add(temp);
            }

            return localStatementList;
        }

        private async Task<List<LocalStatement>> CompileStatement(List<Statement> output, List<LocalStatement> localStatementList, Guid actorId)
        {
            foreach (var i in localStatementList)
            {
                Verb tempVerb = await _verbContext.Get(i.VerbId);
                ModelLibrary.Models.XApiModels.Version tempVersion = await _versionContext.Get(i.VersionId);
                ModelLibrary.Models.XApiModels.Object tempObject = await _objectContext.Get(i.ObjectId);
                Statement temp = new Statement()
                {
                    Timestamp = i.Timestamp,
                    Stored = i.Stored,
                    Id = i.Id,
                    UUID = i.UUID,
                    Actor = i.Actor,
                    Object = tempObject,
                    Verb = tempVerb,
                    Result = i.Result,
                    Context = i.Context,
                    Authority = i.Authority,
                    Version = tempVersion,
                    Attachments = i.Attachments
                };
                output.Add(temp);
            }

            return localStatementList;
        }

        public async Task<LineChart> GetStatementCountDataByActor(Actor input, DateTime start, DateTime end)
        {

            try
            {
                List<LocalStatement> statementList = new List<LocalStatement>();
                #region Filter
                if (User.IsLocalAdmin() || User.IsLocalFebrisAdmin() || User.IsLocalEducator())
                {
                    Actor actor = await _actorContext.Get(input.Id);
                    Guid actorId = input.UUID;
                    statementList = await _context.GetByActor(actorId, start, end);
                }
                else if (User.IsLocalParent())
                {
                    ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User);
                    if (!scope.AllowedActorUuids.Contains(input.UUID)) { return default; }
                    statementList = await _context.GetByActor(input.UUID, start, end);
                }
                else if (User.IsLocalUser())
                {
                    if (User.HasActor())
                    {
                        Guid actorId = Guid.Parse(User.GetActor());
                        statementList = await _context.GetByActor(actorId, start, end);
                    }
                    else { return default; }
                }
                else { return default; }
                #endregion

                List<LineChartEntry> chartEntryList = new List<LineChartEntry>();
                LineChart output = new LineChart
                {
                    Title = "Actor Statement Chart",
                    Description = "Last 30 Days",
                    IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                    ChartEntryList = new List<LineChartEntry>()
                };

                statementList = statementList.OrderBy(i => i.Timestamp).ToList();

                for (DateTime i = start; end >= i; i = i.AddDays(1))
                {
                    int qty = statementList.Where(j => j.Timestamp.Date == i).Count();
                    LineChartEntry temp = new LineChartEntry()
                    {
                        Label = i.ToShortDateString(),
                        Quantity = qty
                    };
                    chartEntryList.Add(temp);
                }

                output.ChartEntryList = chartEntryList;

                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                Febris.SharedServices.FebrisLog.Error(ex);
                return default;
                //throw;
            }
        }

        public async Task<BarChart> GetStatementTimeDataByActor(Actor input, DateTime start, DateTime end)
        {
            try
            {
                List<LocalStatement> statementList = new List<LocalStatement>();
                #region Filter
                if (User.IsLocalAdmin() || User.IsLocalFebrisAdmin() || User.IsLocalEducator())
                {
                    Actor actor = await _actorContext.Get(input.Id);
                    Guid actorId = input.UUID;
                    statementList = await _context.GetByActor(actorId, start, end);
                }
                else if (User.IsLocalParent())
                {
                    ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User);
                    if (!scope.AllowedActorUuids.Contains(input.UUID)) { return default; }
                    statementList = await _context.GetByActor(input.UUID, start, end);
                }
                else if (User.IsLocalUser())
                {
                    if (User.HasActor())
                    {
                        Guid actorId = Guid.Parse(User.GetActor());
                        statementList = await _context.GetByActor(actorId, start, end);
                    }
                    else { return default; }
                }
                else { return default; }
                #endregion

                List<BarChartEntry> chartEntryList = new List<BarChartEntry>();
                BarChart output = new BarChart
                {
                    Title = "Actor Statement Time Chart",
                    Description = "Last 30 Days",
                    IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                    ChartEntryList = new List<BarChartEntry>()
                };

                statementList = statementList.OrderBy(i => i.Timestamp).ToList();

                for (DateTime i = start; end >= i; i = i.AddDays(1))
                {
                    double qty = statementList.Where(j => j.Timestamp.Date == i)
                        .Select(i => i.Result != null && i.Result.Duration != null ? i.Result.Duration.TotalMinutes : 0)
                        .Sum();
                    //.Select(i => i.Result?.Duration?.TotalMinutes ?? 0).Sum();
                    BarChartEntry temp = new BarChartEntry()
                    {
                        Label = i.ToShortDateString(),
                        Quantity = (int)qty
                    };
                    chartEntryList.Add(temp);
                }

                output.ChartEntryList = chartEntryList;

                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                Febris.SharedServices.FebrisLog.Error(ex);
                return default;
                //throw;
            }
        }
    }
}
