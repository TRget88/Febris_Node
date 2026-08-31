// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.PrimaryLogicLayer.Logic.DataLogic;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.UserNode.LogicLayer.Logic.AnalyticsLogic;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.Portal.Controllers
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    [Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    public class SwitchboardController : Controller
    {
        private readonly ILogger<SwitchboardController> _logger;
        private readonly IMessageBoardLogic _messageboardContext;
        private readonly IStatementLogic _statementContext;
        private readonly ILocalAnalyticsLogic _localAnalyticsContext;
        private readonly IUserAnalyticsLogic _userAnalyticsContext;
        private readonly IAnalyticsLogic _analyticsContext;
        private readonly IHardwareLogic _hardwareContext;
        private readonly IModuleUsageAnalyticsLogic _moduleUsageAnalyticsContext;
        private readonly IModuleDownloadAnalyticsLogic _moduleDownloadAnalyticsContext;

        public SwitchboardController(
            ILogger<SwitchboardController> logger,
            IMessageBoardLogic messageboardContext,
            IStatementLogic statementContext,
            ILocalAnalyticsLogic localAnalyticsContext,
             IUserAnalyticsLogic userAnalyticsContext,
             IAnalyticsLogic analyticsContext,
             IHardwareLogic hardwareContext,
             IModuleDownloadAnalyticsLogic moduleDownloadAnalyticsContext,
              IModuleUsageAnalyticsLogic moduleUsageAnalyticsContext
            )
        {
            _logger = logger;
            _messageboardContext = messageboardContext;
            _statementContext = statementContext;
            _localAnalyticsContext = localAnalyticsContext;
            _userAnalyticsContext = userAnalyticsContext;
            _analyticsContext = analyticsContext;
            _hardwareContext = hardwareContext;
            _moduleUsageAnalyticsContext = moduleUsageAnalyticsContext;
            _moduleDownloadAnalyticsContext = moduleDownloadAnalyticsContext;
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

        public IActionResult Index()
        {
            return View();
        }



        #region used Charts        

        ///done
        #region Message board charts
        /// <summary>
        /// 
        /// </summary>        
        /// <returns></returns>
        public async Task<IActionResult> LoadMiniMessageBoardPartial()
        {
            try
            {
                List<MessageBoard> messageBoardList = new List<MessageBoard>();
                messageBoardList = await _messageboardContext.GetLastFive();
                return PartialView("../Widget/_MiniMessageboard", messageBoardList);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                throw;
                return default;
            }
        }

        #endregion

        ///done - maybe force line to keep 0 in place
        #region Messageing charts
        //public async Task<IActionResult> LoadLeadInboxByDateLineChart()
        //{
        //    LineChart output = new LineChart();
        //    //{
        //    //    Title = "Lead Inbox Status",
        //    //    IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
        //    //    Description = "The standing of inbound lead messages",
        //    //    ChartEntryList = new List<BarChartEntry>()
        //    //};
        //    try
        //    {
        //        output = await _leadInboxContext.GetLeadInboxActivityDataSet();
        //        return PartialView("../Widget/_LineGraphPartial", output);
        //        //List<BarChartEntry> chartContents = await _leadInboxContext.GetLeadInboxStatusDataSet();
        //        //output.ChartEntryList = chartContents;

        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);
        //        //throw;
        //    }
        //    return PartialView("../Widget/_LineGraphPartial", output);
        //}
        //public async Task<IActionResult> LoadLeadInboxBarChart()
        //{
        //    BarChart output = new BarChart();
        //    //{
        //    //    Title = "Lead Inbox Status",
        //    //    IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
        //    //    Description = "The standing of inbound lead messages",
        //    //    ChartEntryList = new List<BarChartEntry>()
        //    //};
        //    try
        //    {
        //        output = await _leadInboxContext.GetLeadInboxBreakdownDataSet();
        //        return PartialView("../Widget/_BarChartPartial", output);
        //        //List<BarChartEntry> chartContents = await _leadInboxContext.GetLeadInboxStatusDataSet();
        //        //output.ChartEntryList = chartContents;

        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);
        //        //throw;
        //    }
        //    return PartialView("../Widget/_BarChartPartial", output);
        //}
        //public async Task<IActionResult> LoadInboxBarChart()
        //{
        //    BarChart output = new BarChart();
        //    //{
        //    //    Title = "Lead Inbox Status",
        //    //    IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
        //    //    Description = "The standing of inbound lead messages",
        //    //    ChartEntryList = new List<BarChartEntry>()
        //    //};
        //    try
        //    {
        //        output = await _messageContext.GetInboxBreakdownDataSet();
        //        return PartialView("../Widget/_BarChartPartial", output);
        //        //List<BarChartEntry> chartContents = await _leadInboxContext.GetLeadInboxStatusDataSet();
        //        //output.ChartEntryList = chartContents;

        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);
        //        //throw;
        //    }
        //    return PartialView("../Widget/_BarChartPartial", output);
        //}
        #endregion

        /// <summary>
        /// need 3rd chart 
        /// </summary>
        /// <returns></returns>
        #region Marketing
        //public async Task<IActionResult> LoadLeadQualityChart()
        //{
        //    BarChart output = new BarChart();
        //    try
        //    {
        //        output = await _leadContext.LeadQualityChart();
        //        return PartialView("../Widget/_BarChartPartial", output);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);

        //    }
        //    return PartialView("../Widget/_BarChartPartial", output);
        //}

        //public async Task<IActionResult> LoadWorldGeoMapMarketingAnalytics()
        //{
        //    WorldMapChart output = new WorldMapChart();
        //    //{
        //    //    Title = "Marketing",
        //    //    IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
        //    //    Description = "Request Traffic by location to marketing website",
        //    //    ChartEntryList = new List<WorldMapChartEntry>()
        //    //};
        //    try
        //    {
        //        //output = await _marketingAnalyticsContext.GetWorldChartData();
        //        //output.ChartEntryList = await _marketingAnalyticsContext.GetChartData();
        //        output = await _analyticsContext.GetWorldChartData();

        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);
        //        //throw;
        //        //return null;
        //    }
        //    return PartialView("../Widget/_GeoWorldMapPartial", output);
        //}

        //public async Task<IActionResult> LoadMarketingChart3()
        //{
        //    return null;
        //    BarChart output = new BarChart();
        //    try
        //    {
        //        output = await _leadContext.LeadQualityChart();
        //        return PartialView("../Widget/_BarChartPartial", output);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);

        //    }
        //    return PartialView("../Widget/_BarChartPartial", output);

        //}

        #endregion

        #region Testing charts
        //public async Task<IActionResult> LoadDailyStatementSubmissionChart()
        //{
        //    LineChart output = new LineChart();
        //    try
        //    {
        //        output = await _statementContext.DailyStatementSubmissionChart();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);
        //    }
        //    return PartialView("../Widget/_LineGraphPartial", output);
        //}


        //public async Task<IActionResult> LoadDailyTimeSpentTestingChart()
        //{
        //    LineChart output = new LineChart();
        //    try
        //    {
        //        output = await _statementContext.DailyTimeSpentTesting();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);
        //    }
        //    return PartialView("../Widget/_LineGraphPartial", output);
        //}


        //public async Task<IActionResult> LoadCompletionChart()
        //{
        //    PieChart output = new PieChart();
        //    try
        //    {
        //        output = await _statementContext.CompletionChart();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);
        //    }
        //    return PartialView("../Widget/_PieGraphPartial", output);
        //}


        #endregion



        #region User charts        
        /// <summary>
        /// Need to check the redis cache and look back at cookies
        /// </summary>
        /// <returns></returns>
        //[Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
        //public async Task<IActionResult> LoadDailyActiveUserChart()
        //{
        //    LineChart output = new LineChart();
        //    try
        //    {
        //        output = await _userAnalyticsContext.GetUniqueIPChartData();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);
        //    }
        //    return PartialView("../Widget/_LineGraphPartial", output);
        //}
        ////public async Task<IActionResult> LoadDailyActiveUserChart()
        ////{

        ////    LineChart output = new LineChart()
        ////    {
        ////        Title = "Daily Active Users",
        ////        Description = "Users who are active by day",
        ////        IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
        ////        ChartEntryList = new List<LineChartEntry>()
        ////    };
        ////    try
        ////    {

        ////        for (var i = 0; i <= 25; i++)
        ////        {
        ////            Random rand = new Random();
        ////            int quantity = rand.Next(101);
        ////            string itemName = "1/" + i.ToString() + "/22";
        ////            LineChartEntry temp = new LineChartEntry()
        ////            {
        ////                Label = itemName,
        ////                Quantity = quantity
        ////            };
        ////            output.ChartEntryList.Add(temp);
        ////        }

        ////        return PartialView("../Widget/_LineGraphPartial", output);
        ////    }
        ////    catch (Exception ex)
        ////    {
        ////        _logger.LogInformation(ex.StackTrace);
        ////        throw;
        ////        //return null;
        ////    }
        ////}


        //todo
        //public async Task<IActionResult> LoadLeadGenerationChart()
        //{
        //    LineChart output = new LineChart();
        //    try
        //    {
        //        output = await _statementContext.DailyStatementSubmissionChart();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);
        //    }
        //    return PartialView("../Widget/_LineGraphPartial", output);
        //}
        //public async Task<IActionResult> LoadLeadGenerationChart()
        //{
        //    LineChart output = new LineChart()
        //    {
        //        Title = "This is a place holder",
        //        Description = "Users who are active by day",
        //        IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
        //        ChartEntryList = new List<LineChartEntry>()
        //    };
        //    try
        //    {

        //        for (var i = 0; i <= 25; i++)
        //        {
        //            Random rand = new Random();
        //            int quantity = rand.Next(101);
        //            string itemName = "1/" + i.ToString() + "/22";
        //            LineChartEntry temp = new LineChartEntry()
        //            {
        //                Label = itemName,
        //                Quantity = quantity
        //            };
        //            output.ChartEntryList.Add(temp);
        //        }

        //        return PartialView("../Widget/_LineGraphPartial", output);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);
        //        throw;
        //        //return null;
        //    }
        //}

        #endregion

        #endregion

        #region Switchboard Charts

        #region User charts        
        /// <summary>
        /// Need to check the redis cache and look back at cookies
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
        public async Task<IActionResult> LoadDailyActiveUserChart()
        {
            LineChart output = new LineChart();
            try
            {
                //output = await _userAnalyticsContext.GetUniqueIPChartData();
                output = await _localAnalyticsContext.GetUniqueIPChartData();
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
            }
            return PartialView("../Widget/_LineGraphPartial", output);
        }
       

        #endregion


        #region Downloads         
        /// <summary>
        /// Element18
        /// 
        /// Shows the Modules that have been downloaded in the past few days (Mixed chart line and Pie)
        /// </summary>
        /// <returns></returns>
        //public async Task<IActionResult> LoadModuleDownloadMixedChart()
        //{
        //    GenericMixedChart output = new GenericMixedChart();
        //    try
        //    {
        //        output = await _moduleDownloadAnalyticsContext.GetModuleDownloadMixedChart();
        //        //return PartialView("../Widget/_GenericMixedChartPartial", output);
        //        //OrderChartLists(output);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);
        //        //throw;
        //    }
        //    return PartialView("../Widget/_GenericMixedChartPartial", output);
        //}


        #endregion

        #region Licensing (Accounts and Purchases)
        /// <summary>
        /// Element22
        /// 
        /// Shows module usage line chart and Pie chart
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
        public async Task<IActionResult> LoadModuleUsageAnalyticsMixedChart()
        {
            GenericMixedChart output = new GenericMixedChart();
            try
            {
                output = await _moduleUsageAnalyticsContext.GetModuleUsageAnalyticsMixedChart();
                //return PartialView("../Widget/_GenericMixedChartPartial", output);
                //OrderChartLists(output);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                //throw;
            }
            return PartialView("../Widget/_GenericMixedChartPartial", output);
        }
        #endregion

        #region Statement Data 
        /// <summary>
        /// Element2
        /// 
        /// This is a full mixture of statement time, quatity, and object pie chart
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> LoadStatementDataMixedChart()
        {
            GenericMixedChart output = new GenericMixedChart();
            try
            {
                output = await _statementContext.GetStatementDataMixedChart();
                //return PartialView("../Widget/_GenericMixedChartPartial", output);
                //OrderChartLists(output);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                //throw;
            }
            return PartialView("../Widget/_GenericMixedChartPartial", output);
        }


        #endregion

        #region Miss 
        /// <summary>
        /// Element3
        /// 
        /// This is shows the number of pieces of hardware added to developer system and a pie chart saying what it is.
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
        public async Task<IActionResult> LoadNewHardwareMixedChart()
        {
            GenericMixedChart output = new GenericMixedChart();
            try
            {
                output = await _hardwareContext.GetNewHardwareMixedChart();
                //return PartialView("../Widget/_GenericMixedChartPartial", output);

            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                //throw;
            }
            return PartialView("../Widget/_GenericMixedChartPartial", output);
        }

        /// <summary>
        /// Element25
        /// 
        /// ???
        /// </summary>
        /// <returns></returns>
        //public async Task<IActionResult> LoadBackgroundTaskChart()
        //{
        //    BackgroundTaskStatusResponseViewModel output = default;
        //    try
        //    {

        //        output = await _widgetContext.GetBackgroundTaskStatus();
        //        return PartialView("../Widget/_BackgroundTaskChartPartial", output);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);
        //        //throw;
        //    }
        //    return PartialView("../Widget/_BackgroundTaskChartPartial", output);
        //}
        #endregion
        #endregion
    }
}
