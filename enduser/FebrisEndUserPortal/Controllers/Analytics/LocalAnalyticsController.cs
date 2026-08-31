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
using Febris.ModelLibrary.Models.AnalyticsModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.ModelLibrary.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Febris.UserNode.LogicLayer.Logic.AnalyticsLogic;

namespace Febris.UserNode.Portal.Controllers.Analytics
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    [Authorize(Roles = Febris.Constants.RoleConstants.OrgAdmins)]
    public class LocalAnalyticsController : Controller
    {
        //templates MVCControllerwithContext generator used                
        private readonly ILogger<LocalAnalyticsController> _logger;
        private readonly ILocalAnalyticsLogic _context;
        public LocalAnalyticsController(
            ILocalAnalyticsLogic context,
            ILogger<LocalAnalyticsController> logger
            )
        {
            _context = context;
            _logger = logger;
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
        // GET: LocalAnalytics
        public async Task<IActionResult> Index()
        {
            return View();
        }
        // GET: LocalAnalytics/IndexPartial
        public async Task<IActionResult> IndexPartial(string currentFilter, string searchString, int? page)
        {
            List<LocalAnalytics> outputSetup = new List<LocalAnalytics>();
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

                // T11. This used to call _context.Get(), which materialises EVERY analytics row
                // ever recorded, ordered by a column with no index, then filtered it in memory
                // across six columns, then took 25. Every view of this screen paid for the entire
                // history, and the table grows one row per HTTP request. The search and the paging
                // now happen in PostgreSQL, so the cost is the page rather than the history.
                int pageNumber = (page ?? 1);
                const int pageSize = 25;

                (List<LocalAnalytics> pageRows, int totalCount) =
                    await _context.GetPage(searchString, pageNumber, pageSize);

                // StaticPagedList, not ToPagedList: the rows are ALREADY the page, and the total
                // comes from a COUNT. Handing a 25-row list to ToPagedList would tell the pager
                // there is only one page.
                IPagedList<LocalAnalytics> output =
                    new StaticPagedList<LocalAnalytics>(pageRows, pageNumber, pageSize, totalCount);

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
        // GET: LocalAnalytics/Details/5
        //public async Task<IActionResult> Details(long? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }            
        //    try
        //    {
        //        LocalAnalytics output = await _context.Get(id);
        //         if (output == null)
        //        {
        //            return NotFound();
        //        }
        //        return View(output);
        //    }
        //    catch(Exception ex)
        //    {
        //        _logger.LogWarning(ex.Message);
        //        StatusMessage = ex.Message;

        //    }
        //    return View();            
        //}

        #endregion

        #region chart page
        public async Task<IActionResult> Charts()
        {
            return View();
        }
        #endregion



        #region actual charts
        #region line Graphs
        public async Task<IActionResult> LoadRequestByDate()
        {
            LineChart output = new LineChart();
            //{
            //    Title = "Requests By Date",
            //    Description = "Gross Number of Requests By Date",
            //    IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
            //    ChartEntryList = new List<LineChartEntry>()
            //};
            try
            {
                output = await _context.GetRequestByDateChartData();
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                //throw;
                //return null;
            }
            return PartialView("../Widget/_LineGraphPartial", output);
        }
        public async Task<IActionResult> LoadRequestsByUniqueIP()
        {
            LineChart output = new LineChart();
            //{
            //    Title = "Unique Requests",
            //    IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
            //    Description = "Requests with unique IP Addresses over the last 30 days",
            //    ChartEntryList = new List<LineChartEntry>()
            //};
            try
            {
                output = await _context.GetUniqueIPChartData();

            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                //throw;
                //return null;
            }
            return PartialView("../Widget/_LineGraphPartial", output);
        }
        #endregion

        #region Bar Charts
        public async Task<IActionResult> LoadPageRequestByUser()
        {
            BarChart output = new BarChart();
            //{
            //    Title = "User Requests",
            //    IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
            //    Description = "user stay on site over the last 30 days",
            //    ChartEntryList = new List<BarChartEntry>()
            //};
            try
            {
                output = await _context.GetPageRequestByUser();

                //for (var i = 0; i <= 5; i++)
                //{
                //    Random rand = new Random();
                //    int quantity = rand.Next(101);
                //    string itemName = "item " + i.ToString();
                //    BarChartEntry temp = new BarChartEntry()
                //    {
                //        Label = itemName,
                //        Quantity = quantity
                //    };
                //    output.ChartEntryList.Add(temp);
                //}

            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                //throw;

            }
            return PartialView("../Widget/_BarChartPartial", output);
        }

        public async Task<IActionResult> LoadBotOrUser()
        {
            BarChart output = new BarChart();
            //{
            //    Title = "Bot or User",
            //    IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
            //    Description = "Bot or User requests over 30 days",
            //    ChartEntryList = new List<BarChartEntry>()
            //};
            try
            {
                output = await _context.GetBotOrUser();

                //for (var i = 0; i <= 5; i++)
                //{
                //    Random rand = new Random();
                //    int quantity = rand.Next(101);
                //    string itemName = "item " + i.ToString();
                //    BarChartEntry temp = new BarChartEntry()
                //    {
                //        Label = itemName,
                //        Quantity = quantity
                //    };
                //    output.ChartEntryList.Add(temp);
                //}                
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                //throw;
            }
            return PartialView("../Widget/_BarChartPartial", output);
        }
        #endregion


        #region PieCharts                
        public async Task<IActionResult> LoadOddRequestsPortion()
        {
            PieChart output = new PieChart();
            //{
            //    Title = "Odd Requests",
            //    IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
            //    Description = "Number of requests that seem odd",
            //    ChartEntryList = new List<PieChartEntry>()
            //};
            try
            {
                output = await _context.GetOddRequestChartData();

            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.StackTrace);
                //throw;
                //return null;
            }
            return PartialView("../Widget/_PieGraphPartial", output);
        }


        #endregion



        //public async Task<IActionResult> LoadWorldGeoMap()
        //{
        //    WorldMapChart output = new WorldMapChart();
        //    //{
        //    //    Title = "Traffic",
        //    //    IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
        //    //    Description = "Request Traffic by country",
        //    //    ChartEntryList = new List<WorldMapChartEntry>()
        //    //};
        //    try
        //    {
        //        output = await _context.GetWorldChartData();
        //        //output.ChartEntryList = await _context.GetChartData();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogInformation(ex.StackTrace);
        //        //throw;
        //        //return null;
        //    }
        //    return PartialView("../Widget/_GeoWorldMapPartial", output);
        //}
        #endregion

    }
}
