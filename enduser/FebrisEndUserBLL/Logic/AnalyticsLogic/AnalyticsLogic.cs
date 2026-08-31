// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.AnalyticsModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.AnalyticsQueries;
using Febris.SharedServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.AnalyticsLogic
{
    #region logic
    public interface IAnalyticsLogic
    {
        //Task<List<Analytics>> Get();
        //Task Create(Analytics analytics);
        //Task<WorldMapChart> GetWorldChartData();
    }
    public class AnalyticsLogic : IAnalyticsLogic
    {
        //private readonly IAnalyticsQueries _context;
        //private readonly IHttpContextAccessor _httpContextAccessor;
        //private readonly ClaimsPrincipal User;
        //private readonly IConfiguration _config;
        //public AnalyticsLogic()//IHttpContextAccessor httpContextAccessor, IConfiguration config)
        //{
        //    //_httpContextAccessor = httpContextAccessor;
        //    _context = new AnalyticsQueries();
        //    //User = _httpContextAccessor.HttpContext.User;
        //    //_config = config;
        //}
        //public AnalyticsLogic(IHttpContextAccessor httpContextAccessor, IConfiguration config)
        //{
        //    _httpContextAccessor = httpContextAccessor;
        //    _context = new AnalyticsQueries();
        //    _config = config;
        //    User = _httpContextAccessor.HttpContext.User;
        //}


        //public async Task Create(Analytics input)
        //{
        //    Febris.SharedServices.FebrisLog.Info(input);

        //    try
        //    {
        //        bool created = await _context.Create(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //}

        //public async Task<List<Analytics>> Get()
        //{
        //    List<Analytics> output = new List<Analytics>();
        //    try
        //    {
        //        output = await _context.Get();
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        //public async Task<Analytics> Get(long? id)
        //{
        //    Analytics output = new Analytics();
        //    try
        //    {
        //        output = await _context.Get(id);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}

        //public async Task<WorldMapChart> GetWorldChartData()
        //{
        //    WorldMapChart output = new WorldMapChart()
        //    {
        //        Title = "Total Traffic",
        //        IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
        //        Description = "Request Traffic by city",
        //        ChartEntryList = new List<WorldMapChartEntry>()
        //    };
        //    List<WorldMapChartEntry> entryList = new List<WorldMapChartEntry>();
        //    try
        //    {
        //        List<GeoIPData> chartData = new List<GeoIPData>();
        //        if (User.IsFebrisUser())
        //        {
        //            chartData = await _context.GetGeoIPData();
        //        }               
        //        else
        //        {
        //            return null;
        //        }



        //        foreach (var i in chartData)
        //        {
        //            if (entryList.Where(j => j.Reference == i.Id).Any())
        //            {
        //                WorldMapChartEntry temp = entryList.Where(j => j.Reference == i.Id).First();
        //                temp.Quantity++;
        //            }
        //            else
        //            {

        //                WorldMapChartEntry temp = new WorldMapChartEntry()
        //                {
        //                    Reference = i.Id,
        //                    Name = i?.GeoIPByCountry?.GeoIPByCountryData?.Country_Name ?? string.Empty,
        //                    Quantity = 1,
        //                    Radius = i?.GeoIPByCity?.Accuracy_Radius ?? 1000,
        //                    Latitude = 0.0,
        //                    Longitude = 0.0
        //                };
        //                if (i.GeoIPByCity != null)
        //                {
        //                    temp.Latitude = i.GeoIPByCity.Latitude;
        //                    temp.Longitude = i.GeoIPByCity.Longitude;
        //                }
        //                //else if (i.GeoIPByCountry != null)
        //                //{
        //                //    temp.Latitude = i.GeoIPByCity.Latitude;
        //                //    temp.Longitude = i.GeoIPByCity.Longitude;
        //                //}
        //                //else if (i.GeoASN != null)
        //                //{
        //                //    temp.Latitude = i.GeoIPByCity.Latitude;
        //                //    temp.Longitude = i.GeoIPByCity.Longitude;
        //                //}

        //                entryList.Add(temp);
        //            }
        //        }

        //        output.ChartEntryList = entryList;
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        //throw;
        //    }
        //    return output;
        //}
    }

    public interface ILocalAnalyticsLogic
    {
        //Task<List<LocalAnalytics>> Get();
        //Task Create(LocalAnalytics analytics);
        //Task<bool> Create(List<LocalAnalytics> analytics);
        //Task<LocalAnalytics> Get(long? id);
        //Task<List<AnalyticsViewModel>> GetSorted();
        ////Task<List<LineChartEntry>> GetRequestByDateChartData();
        ////Task<List<LineChartEntry>> GetUniqueIPChartData();
        ////Task<List<PieChartEntry>> GetOddRequestChartData();
        ////Task<List<BarChartEntry>> GetPageRequestByUser();
        //Task<LineChart> GetRequestByDateChartData();
        //Task<LineChart> GetUniqueIPChartData();
        //Task<PieChart> GetOddRequestChartData();
        //Task<BarChart> GetPageRequestByUser();
        //Task<BarChart> GetBotOrUser();
        ////Task<List<WorldMapChartEntry>> GetChartData();
        //Task<WorldMapChart> GetWorldChartData();
        Task<List<LocalAnalytics>> Get();        

        /// <summary>
        /// One page of analytics rows, newest first, with the search applied in the DATABASE.
        /// T11: the list screen used to load every row ever recorded to render 25 of them.
        /// </summary>
        Task<(List<LocalAnalytics> Page, int TotalCount)> GetPage(string searchString, int pageNumber, int pageSize);
        Task<LocalAnalytics> Get(long? id);        
        Task Create(LocalAnalytics analytics);
        Task<PieChart> GetOddRequestChartData();
        Task<BarChart> GetBotOrUser();
        Task<BarChart> GetPageRequestByUser();
        Task<LineChart> GetUniqueIPChartData();
        Task<LineChart> GetRequestByDateChartData();
        //Task<WorldMapChart> GetWorldChartData();
    }
    public class LocalAnalyticsLogic : ILocalAnalyticsLogic
    {
        private readonly ILocalAnalyticsQueries _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        //private readonly IAnalyticsQueries _geoContext;
        /// <summary>
        /// LAZY on purpose. This used to be a readonly field assigned in the constructor.
        ///
        /// <para>
        /// The analytics middleware fire-and-forgets its write with <c>Task.Run</c>, and resolves
        /// this logic from a fresh DI scope INSIDE that task. AsyncLocal flows into Task.Run, so
        /// IHttpContextAccessor there still points at the request that is at that moment still
        /// travelling through <c>await _next(context)</c>. A constructor that read
        /// <c>HttpContext.User</c> therefore touched a LIVE HttpContext from a second thread.
        /// </para>
        ///
        /// <para>
        /// That is not merely untidy. <c>HttpContext.User</c> and <c>HttpContext.RequestAborted</c>
        /// both go through <c>FeatureReferences.Fetch</c>, which WRITES to a shared struct and
        /// zeroes the whole feature cache on flush, so one thread can null the other's slot between
        /// assignment and return. Fetch then returns null and the pipeline thread dereferences it.
        /// Observed once in the wild as a 500 with a NullReferenceException at
        /// <c>DefaultHttpContext.get_RequestAborted()</c> inside the rate limiter.
        /// </para>
        ///
        /// <para>
        /// Reading it lazily means the background path (Create) never touches HttpContext at all,
        /// while request-thread callers such as the traffic chart resolve exactly as before.
        /// </para>
        /// </summary>
        private ClaimsPrincipal User => _httpContextAccessor?.HttpContext?.User;
        private readonly IConfiguration _config;
        public LocalAnalyticsLogic()//IHttpContextAccessor httpContextAccessor, IConfiguration config)
        {
            _context = new LocalAnalyticsQueries();
            //User = _httpContextAccessor.HttpContext.User;
            //_config = config;
        }

        // DI refactor
        public LocalAnalyticsLogic(IHttpContextAccessor httpContextAccessor, IConfiguration config, ILocalAnalyticsQueries context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _config = config;
            // Deliberately does NOT read HttpContext -- see the User property.
        }
        public LocalAnalyticsLogic(IHttpContextAccessor httpContextAccessor, IConfiguration config)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new LocalAnalyticsQueries();
            _config = config;
            //_geoContext = new AnalyticsQueries();
        }

        public async Task Create(LocalAnalytics input)
        {
            //Febris.SharedServices.FebrisLog.Info(input);
            try
            {
                bool created = await _context.Create(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
        public async Task<bool> Create(List<LocalAnalytics> input)
        {
            bool output = false;
            try
            {
                output = await _context.Create(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        /// <inheritdoc />
        public async Task<(List<LocalAnalytics> Page, int TotalCount)> GetPage(string searchString, int pageNumber, int pageSize)
        {
            try
            {
                return await _context.GetPage(searchString, pageNumber, pageSize);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// UNBOUNDED, and kept only for the chart builders that aggregate over the whole history.
        /// Do not use it to render a list: see <see cref="GetPage"/>.
        /// </summary>
        public async Task<List<LocalAnalytics>> Get()
        {
            List<LocalAnalytics> output = new List<LocalAnalytics>();
            try
            {
                output = await _context.Get();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<LocalAnalytics> Get(long? id)
        {
            LocalAnalytics output = new LocalAnalytics();
            try
            {
                output = await _context.Get(id);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        private async Task<List<LocalAnalytics>> Get(DateTime startDate, DateTime endDate)
        {
            List<LocalAnalytics> output = new List<LocalAnalytics>();
            try
            {
                output = await _context.Get(startDate, endDate);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        /// <summary>
        /// Sort out 
        /// OS
        /// Device
        /// 
        /// </summary>
        /// <returns></returns>
        //public async Task<List<LocalAnalyticsViewModel>> GetSorted()
        //{
        //    List<AnalyticsViewModel> output = new List<AnalyticsViewModel>();
        //    try
        //    {
        //        //var preoutput = Get();

        //        //sort everything out

        //        AnalyticsViewModel notsetup = new AnalyticsViewModel()
        //        {
        //            IPAddress = "not yet setup"
        //        };

        //        output.Add(notsetup);

        //        //foreach(var i in preoutput)
        //        //{

        //        //}

        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        public async Task<WorldMapChart> GetWorldChartData()
        {
            WorldMapChart output = new WorldMapChart()
            {
                Title = "Traffic",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "Request Traffic by country",
                ChartEntryList = new List<WorldMapChartEntry>()
            };
            List<WorldMapChartEntry> entryList = new List<WorldMapChartEntry>();
            try
            {
                List<GeoIPData> chartData = new List<GeoIPData>();
                if (User.IsFebrisUser())
                {
                    //chartData = await _context.GetGeoIPData();
                }
                //else if (User.IsContentDeveloper())
                //{
                //    ContentDeveloper developer = await _devContext.Get(Guid.Parse(User.ContentDeveloper()));
                //    //get marketplace listings
                //    List<ContentDeveloperLinkedMarketplaceListing> linked = await _devLinkContext.Get(developer);
                //    List<MarketplaceListing> listingList = linked.Select(i => i.MarketplaceListing).ToList();
                //    //search analityics for any of the listings 
                //    List<MarketplaceAnalytics> analyticsList = await _context.Get(listingList);
                //    chartData = await _context.GetGeoIPData(analyticsList);
                //    //add them to lists

                //}
                else
                {
                    return null;
                }



                foreach (var i in chartData)
                {
                    if (i != null)
                    {
                        if (entryList.Where(j => j.Reference == i.Id).Any())
                        {
                            WorldMapChartEntry temp = entryList.Where(j => j.Reference == i.Id).FirstOrDefault();
                            temp.Quantity++;
                        }
                        else
                        {

                            WorldMapChartEntry temp = new WorldMapChartEntry()
                            {
                                Reference = i.Id,
                                Name = i?.GeoIPByCountry?.GeoIPByCountryData?.Country_Name ?? string.Empty,
                                Quantity = 1,
                                Radius = i?.GeoIPByCity?.Accuracy_Radius ?? 1000,
                                Latitude = 0.0,
                                Longitude = 0.0
                            };
                            if (i.GeoIPByCity != null)
                            {
                                temp.Latitude = i.GeoIPByCity.Latitude;
                                temp.Longitude = i.GeoIPByCity.Longitude;
                            }
                            //else if (i.GeoIPByCountry != null)
                            //{
                            //    temp.Latitude = i.GeoIPByCity.Latitude;
                            //    temp.Longitude = i.GeoIPByCity.Longitude;
                            //}
                            //else if (i.GeoASN != null)
                            //{
                            //    temp.Latitude = i.GeoIPByCity.Latitude;
                            //    temp.Longitude = i.GeoIPByCity.Longitude;
                            //}

                            entryList.Add(temp);
                        }
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
        #region Chart requests
        //private async Task<List<LocalAnalytics>> Get(DateTime startDate, DateTime endDate)
        //{
        //    List<LocalAnalytics> output = new List<LocalAnalytics>();
        //    try
        //    {
        //        output = await _context.Get(startDate, endDate);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}
        public async Task<LineChart> GetRequestByDateChartData()
        {
            LineChart output = new LineChart()
            {
                Title = "Requests By Date",
                Description = "Gross Number of Requests By Date",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                ChartEntryList = new List<LineChartEntry>()
            };
            List<LineChartEntry> entryList = new List<LineChartEntry>();
            try
            {
                DateTime endDate = DateTime.UtcNow.Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

                List<LocalAnalytics> rawData = await Get(startDate, endDate);

                //for (DateTime i = startDate; endDate.CompareTo(i) > 0; i = i.AddDays(1))
                for (DateTime i = startDate; endDate >= i; i = i.AddDays(1))
                {
                    int qty = rawData.Where(j => j.TimeStamp.Date == i).Count();
                    LineChartEntry temp = new LineChartEntry()
                    {
                        Label = i.ToShortDateString(),
                        Quantity = qty
                    };
                    entryList.Add(temp);
                }
                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {

                throw;
            }
            return output;
        }
        public async Task<LineChart> GetUniqueIPChartData()
        {
            LineChart output = new LineChart()
            {
                Title = "Unique Requests",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "Requests with unique IP Addresses over the last 30 days",
                ChartEntryList = new List<LineChartEntry>()
            };
            List<LineChartEntry> entryList = new List<LineChartEntry>();
            //List<LineChartEntry> output = new List<LineChartEntry>();
            try
            {
                DateTime endDate = DateTime.UtcNow.Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

                List<LocalAnalytics> rawData = await Get(startDate, endDate);

                //for (DateTime i = startDate; endDate.CompareTo(i) > 0; i = i.AddDays(1))
                for (DateTime i = startDate; endDate >= i; i = i.AddDays(1))
                {
                    int qty = rawData.Where(j => j.TimeStamp.Date == i).Select(k => k.IPAddress).Distinct().Count();
                    LineChartEntry temp = new LineChartEntry()
                    {
                        Label = i.ToShortDateString(),
                        Quantity = qty
                    };
                    entryList.Add(temp);
                }
                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {

                throw;
            }
            return output;
        }
        //public async Task<LineChart> GetUniqueIPChartData()
        //{
        //    LineChart output = new LineChart()
        //    {
        //        Title = "Unique Requests",
        //        IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
        //        Description = "Requests with unique IP Addresses over the last 30 days",
        //        ChartEntryList = new List<LineChartEntry>()
        //    };
        //    List<LineChartEntry> entryList = new List<LineChartEntry>();
        //    //List<LineChartEntry> output = new List<LineChartEntry>();
        //    try
        //    {
        //        DateTime endDate = DateTime.UtcNow.Date;
        //        DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

        //        List<LocalAnalytics> rawData = await Get(startDate, endDate);

        //        //for (DateTime i = startDate; endDate.CompareTo(i) > 0; i = i.AddDays(1))
        //        for (DateTime i = startDate; endDate >= i; i = i.AddDays(1))
        //        {
        //            int qty = rawData.Where(j => j.TimeStamp.Date == i).Select(k => k.IPAddress).Distinct().Count();
        //            LineChartEntry temp = new LineChartEntry()
        //            {
        //                Label = i.ToShortDateString(),
        //                Quantity = qty
        //            };
        //            entryList.Add(temp);
        //        }
        //        output.ChartEntryList = entryList;
        //    }
        //    catch (Exception ex)
        //    {

        //        throw;
        //    }
        //    return output;
        //}
        public async Task<PieChart> GetOddRequestChartData()
        {
            PieChart output = new PieChart()
            {
                Title = "Odd Requests",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "Number of requests that seem odd",
                ChartEntryList = new List<PieChartEntry>()
            };
            List<PieChartEntry> entryList = new List<PieChartEntry>();
            // List<PieChartEntry> output = new List<PieChartEntry>();
            try
            {
                DateTime endDate = DateTime.UtcNow.Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

                List<LocalAnalytics> rawData = await Get(startDate, endDate);

                int total = rawData.Count();
                int oddRequest = await DefineOddRequests(rawData);
                int normal = total - oddRequest;

                PieChartEntry normalEntries = new PieChartEntry()
                {
                    Label = "Normal",
                    Quantity = normal
                };
                PieChartEntry oddEntries = new PieChartEntry()
                {
                    Label = "Odd",
                    Quantity = oddRequest
                };
                entryList.Add(normalEntries);
                entryList.Add(oddEntries);
                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);

            }
            return output;
        }
        public async Task<BarChart> GetPageRequestByUser()
        {
            BarChart output = new BarChart()
            {
                Title = "User Requests",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "user stay on site over the last 30 days",
                ChartEntryList = new List<BarChartEntry>()
            };
            List<BarChartEntry> entryList = new List<BarChartEntry>();
            try
            {
                DateTime endDate = DateTime.UtcNow.Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

                List<LocalAnalytics> rawData = await Get(startDate, endDate);
                List<string> IpAddresses = rawData.Select(k => k.IPAddress).Distinct().ToList();
                //List<Tuple<string, int>> IpAddressVists = new List<Tuple<string, int>>();
                List<int> visitList = new List<int>();
                int veryLowUsage = 0;
                int LowUsage = 0;
                int LowMedUsage = 0;
                int MedUsage = 0;
                int HighMedUsage = 0;
                int HighUsage = 0;
                int VeryHighUsage = 0;
                int MassiveUsage = 0;


                //range between 0 and 5, 5 and 10, 10 and 20, 20 and 50, 50 and 150, 150 and 1000, 100+
                foreach (var i in IpAddresses)
                {
                    int visits = rawData.Where(j => j.IPAddress == i).Count();
                    //IpAddressVists.Add(new Tuple<string, int>(i,visits));
                    visitList.Add(visits);
                }

                veryLowUsage = visitList.Where(i => i <= 5).Count();
                LowUsage = visitList.Where(i => i > 5 && i <= 10).Count();
                LowMedUsage = visitList.Where(i => i > 10 && i <= 20).Count();
                MedUsage = visitList.Where(i => i > 20 && i <= 50).Count();
                HighMedUsage = visitList.Where(i => i > 50 && i <= 150).Count();
                HighUsage = visitList.Where(i => i > 150 && i <= 1000).Count();
                VeryHighUsage = visitList.Where(i => i > 1000 && i <= 5000).Count();
                MassiveUsage = visitList.Where(i => i > 5000).Count();

                BarChartEntry veryLowEntry = new BarChartEntry()
                {
                    Label = "0-5",
                    Quantity = veryLowUsage
                };
                entryList.Add(veryLowEntry);
                BarChartEntry lowEntry = new BarChartEntry()
                {
                    Label = "5-10",
                    Quantity = LowUsage
                };
                entryList.Add(lowEntry);
                BarChartEntry lowMedEntry = new BarChartEntry()
                {
                    Label = "10-20",
                    Quantity = LowMedUsage
                };
                entryList.Add(lowMedEntry);
                BarChartEntry medEntry = new BarChartEntry()
                {
                    Label = "20-50",
                    Quantity = MedUsage
                };
                entryList.Add(medEntry);
                BarChartEntry highMedEntry = new BarChartEntry()
                {
                    Label = "50-150",
                    Quantity = HighMedUsage
                };
                entryList.Add(highMedEntry);
                BarChartEntry highEntry = new BarChartEntry()
                {
                    Label = "150-1000",
                    Quantity = HighUsage
                };
                entryList.Add(highEntry);
                BarChartEntry veryhighEntry = new BarChartEntry()
                {
                    Label = "1000-5000",
                    Quantity = VeryHighUsage
                };
                entryList.Add(veryhighEntry);
                BarChartEntry massiveEntry = new BarChartEntry()
                {
                    Label = "5000+",
                    Quantity = MassiveUsage
                };
                entryList.Add(massiveEntry);

                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }
        public async Task<PieChart> GetBrowserChartData()
        {
            PieChart output = new PieChart()
            {
                Title = "Browser Use",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "Different browsers used",
                ChartEntryList = new List<PieChartEntry>()
            };
            List<PieChartEntry> entryList = new List<PieChartEntry>();
            try
            {
                DateTime endDate = DateTime.UtcNow.Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

                List<LocalAnalytics> rawData = await Get(startDate, endDate);

                int total = rawData.Count();
                int oddRequest = await DefineOddRequests(rawData);
                int normal = total - oddRequest;

                PieChartEntry normalEntries = new PieChartEntry()
                {
                    Label = "Normal",
                    Quantity = normal
                };
                PieChartEntry oddEntries = new PieChartEntry()
                {
                    Label = "Odd",
                    Quantity = oddRequest
                };
                entryList.Add(normalEntries);
                entryList.Add(oddEntries);
                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {

                throw;
            }
            return output;
        }
        public async Task<BarChart> GetBotOrUser()
        {
            BarChart output = new BarChart()
            {
                Title = "Bot or User",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "Bot or User requests over 30 days",
                ChartEntryList = new List<BarChartEntry>()
            };
            List<BarChartEntry> entryList = new List<BarChartEntry>();
            try
            {
                DateTime endDate = DateTime.UtcNow.Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

                List<LocalAnalytics> rawData = await Get(startDate, endDate);
                int isBot = 0;
                int isUser = 0;

                isBot = rawData.Where(i => i.UserAgent == null || i.Referer == null).Count();
                isUser = rawData.Count() - isBot;
                //isUser = rawData.Where(i => i.UserAgent != null).Count();

                BarChartEntry veryLowEntry = new BarChartEntry()
                {
                    Label = "Bot",
                    Quantity = isBot
                };
                entryList.Add(veryLowEntry);
                BarChartEntry lowEntry = new BarChartEntry()
                {
                    Label = "User",
                    Quantity = isUser
                };
                entryList.Add(lowEntry);
                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        //public async Task<WorldMapChart> GetWorldChartData()
        //{
        //    WorldMapChart output = new WorldMapChart()
        //    {
        //        Title = "Traffic",
        //        IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
        //        Description = "Request Traffic by country",
        //        ChartEntryList = new List<WorldMapChartEntry>()
        //    };
        //    List<WorldMapChartEntry> entryList = new List<WorldMapChartEntry>();
        //    try
        //    {
        //        List<GeoIPData> chartData = await _geoContext.GetGeoIPData();

        //        foreach (var i in chartData)
        //        {
        //            if (entryList.Where(j => j.Reference == i.Id).Any())
        //            {
        //                WorldMapChartEntry temp = entryList.Where(j => j.Reference == i.Id).First();
        //                temp.Quantity++;
        //            }
        //            else
        //            {

        //                WorldMapChartEntry temp = new WorldMapChartEntry()
        //                {
        //                    Reference = i.Id,
        //                    Name = i?.GeoIPByCountry?.GeoIPByCountryData?.Country_Name ?? string.Empty,
        //                    Quantity = 1,
        //                    Radius = i?.GeoIPByCity?.Accuracy_Radius ?? 1000,
        //                    Latitude = 0.0,
        //                    Longitude = 0.0
        //                };
        //                if (i.GeoIPByCity != null)
        //                {
        //                    temp.Latitude = i.GeoIPByCity.Latitude;
        //                    temp.Longitude = i.GeoIPByCity.Longitude;
        //                }
        //                //else if (i.GeoIPByCountry != null)
        //                //{
        //                //    temp.Latitude = i.GeoIPByCity.Latitude;
        //                //    temp.Longitude = i.GeoIPByCity.Longitude;
        //                //}
        //                //else if (i.GeoASN != null)
        //                //{
        //                //    temp.Latitude = i.GeoIPByCity.Latitude;
        //                //    temp.Longitude = i.GeoIPByCity.Longitude;
        //                //}

        //                entryList.Add(temp);
        //            }
        //        }

        //        output.ChartEntryList = entryList;
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        //throw;
        //    }
        //    return output;
        //}

        //public async Task<List<WorldMapChartEntry>> GetChartData()
        //{
        //    List<WorldMapChartEntry> output = new List<WorldMapChartEntry>();
        //    try
        //    {
        //        List<GeoIPData> chartData = await _geoContext.GetGeoIPData();

        //        foreach (var i in chartData)
        //        {
        //            if (output.Where(j => j.Reference == i.Id).Any())
        //            {
        //                WorldMapChartEntry temp = output.Where(j => j.Reference == i.Id).First();
        //                temp.Quantity++;
        //            }
        //            else
        //            {

        //                WorldMapChartEntry temp = new WorldMapChartEntry()
        //                {
        //                    Reference = i.Id,
        //                    Name = i?.GeoIPByCountry?.GeoIPByCountryData?.Country_Name ?? string.Empty,
        //                    Quantity = 1,
        //                    Radius = i?.GeoIPByCity?.Accuracy_Radius ?? 1000,
        //                    Latitude = 0.0,
        //                    Longitude = 0.0
        //                };
        //                if (i.GeoIPByCity != null)
        //                {
        //                    temp.Latitude = i.GeoIPByCity.Latitude;
        //                    temp.Longitude = i.GeoIPByCity.Longitude;
        //                }
        //                //else if (i.GeoIPByCountry != null)
        //                //{
        //                //    temp.Latitude = i.GeoIPByCity.Latitude;
        //                //    temp.Longitude = i.GeoIPByCity.Longitude;
        //                //}
        //                //else if (i.GeoASN != null)
        //                //{
        //                //    temp.Latitude = i.GeoIPByCity.Latitude;
        //                //    temp.Longitude = i.GeoIPByCity.Longitude;
        //                //}

        //                output.Add(temp);
        //            }
        //        }


        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}

        private async Task<int> DefineOddRequests(List<LocalAnalytics> rawData)
        {
            int output = 0;
            try
            {
                List<string> oddList = new List<string>()
                {
                    "SELECT","*","FROM","WHERE","DROP","TABLE","STATEMENT","INSERT","INTO","UPDATE","DELETE","CREATE","ALTER","DATABASE","INDEX","wordpress","wp","blog",
                    "wlwmanifest",".git","GponForm","WP","ads",".env","console","auth","x.js","owa","wp-includes","php","login","feed","wlwmanifest","BARFOO",
                    "actuator","gateway","routes","_ignition","execute-solution","owa","GponForm","diag_Form","Autodiscover","mifs","Logservice",
                    "log","credentials","elfinder","vendor","license","admin","rsd","test","env","backup","bk","bc","old","new","main","wordpress",
                    "users","WPnBr.dll","Additional","cplugError","doc","nmaplowercheck","Portal0000","start","mwsl","NDrX","pool","sdk",
                    "functionRouter","portal",".aspx",".inc",".cgi",".jsp","menu",".asp",".env","x.js",".aspx",".git","config",".dll","ecp","backend"

                };
                //odd paths
                output = rawData.Where(i => oddList.Any(j => i.Path.ToLower().Contains(j.ToLower()) || i.Query.ToLower().Contains(j.ToLower()))).Count();
                //rawData.Where(i => i.Path.Any(j => oddList.Contains())).Count();
                // oddList.Contains((j => j == i.Path)) == true);//.Count();



                //output = rawData.Where(i => oddList.Any(j => j == i.Query||j==i.Path))
                //    .Count();
                //output = rawData.Where(i => oddList.Any(j => j.Contains(i.Query)))
                //    .Count();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }


        #endregion
        
    }

    public interface IUserAnalyticsLogic
    {
        Task<List<UserAnalytics>> Get();
        Task Create(UserAnalytics analytics);
        Task<bool> Create(List<UserAnalytics> analytics);
        Task<UserAnalytics> Get(long? id);
        Task<List<AnalyticsViewModel>> GetSorted();
        //Task<List<WorldMapChartEntry>> GetChartData();
        Task<LineChart> GetRequestByDateChartData();
        Task<LineChart> GetUniqueIPChartData();
        Task<PieChart> GetOddRequestChartData();
        Task<BarChart> GetPageRequestByUser();
        Task<BarChart> GetBotOrUser();
        //Task<List<WorldMapChartEntry>> GetChartData();
        Task<WorldMapChart> GetWorldChartData();
    }
    public class UserAnalyticsLogic : IUserAnalyticsLogic
    {
        private readonly IUserAnalyticsQueries _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        /// <summary>
        /// LAZY on purpose. This used to be a readonly field assigned in the constructor.
        ///
        /// <para>
        /// The analytics middleware fire-and-forgets its write with <c>Task.Run</c>, and resolves
        /// this logic from a fresh DI scope INSIDE that task. AsyncLocal flows into Task.Run, so
        /// IHttpContextAccessor there still points at the request that is at that moment still
        /// travelling through <c>await _next(context)</c>. A constructor that read
        /// <c>HttpContext.User</c> therefore touched a LIVE HttpContext from a second thread.
        /// </para>
        ///
        /// <para>
        /// That is not merely untidy. <c>HttpContext.User</c> and <c>HttpContext.RequestAborted</c>
        /// both go through <c>FeatureReferences.Fetch</c>, which WRITES to a shared struct and
        /// zeroes the whole feature cache on flush, so one thread can null the other's slot between
        /// assignment and return. Fetch then returns null and the pipeline thread dereferences it.
        /// Observed once in the wild as a 500 with a NullReferenceException at
        /// <c>DefaultHttpContext.get_RequestAborted()</c> inside the rate limiter.
        /// </para>
        ///
        /// <para>
        /// Reading it lazily means the background path (Create) never touches HttpContext at all,
        /// while request-thread callers such as the traffic chart resolve exactly as before.
        /// </para>
        /// </summary>
        private ClaimsPrincipal User => _httpContextAccessor?.HttpContext?.User;
        private readonly IConfiguration _config;
        //private readonly IAnalyticsQueries _geoContext;
        public UserAnalyticsLogic()
        {
            _context = new UserAnalyticsQueries();
        }

        // DI refactor
        public UserAnalyticsLogic(IHttpContextAccessor httpContextAccessor, IConfiguration config, IUserAnalyticsQueries context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _config = config;
            // Deliberately does NOT read HttpContext -- see the User property.
        }
        public UserAnalyticsLogic(IHttpContextAccessor httpContextAccessor, IConfiguration config)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new UserAnalyticsQueries();
            _config = config;
           // _geoContext = new AnalyticsQueries();
        }
       

        public async Task Create(UserAnalytics input)
        {
            Febris.SharedServices.FebrisLog.Info(input);

            try
            {
                //bool created = await _context.Create(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
        public async Task<bool> Create(List<UserAnalytics> input)
        {
            bool output = false;
            try
            {
                //output = await _context.Create(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<UserAnalytics>> Get()
        {
            List<UserAnalytics> output = new List<UserAnalytics>();
            try
            {
                //output = await _context.Get();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<UserAnalytics> Get(long? id)
        {
            UserAnalytics output = new UserAnalytics();
            try
            {
                //output = await _context.Get(id);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<AnalyticsViewModel>> GetSorted()
        {
            List<AnalyticsViewModel> output = new List<AnalyticsViewModel>();
            try
            {
                //var preoutput = Get();

                //sort everything out

                AnalyticsViewModel notsetup = new AnalyticsViewModel()
                {
                    IPAddress = "not yet setup"
                };

                output.Add(notsetup);

                //foreach(var i in preoutput)
                //{

                //}

            }
            catch (Exception ex)
            {

                throw;
            }
            return output;
        }
        public async Task<WorldMapChart> GetWorldChartData()
        {
            WorldMapChart output = new WorldMapChart()
            {
                Title = "Traffic",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "Request Traffic by country",
                ChartEntryList = new List<WorldMapChartEntry>()
            };
            List<WorldMapChartEntry> entryList = new List<WorldMapChartEntry>();
            try
            {
                List<GeoIPData> chartData = new List<GeoIPData>();
                if (User.IsFebrisUser())
                {
                    //chartData = await _context.GetGeoIPData();
                }
                //else if (User.IsContentDeveloper())
                //{
                //    ContentDeveloper developer = await _devContext.Get(Guid.Parse(User.ContentDeveloper()));
                //    //get marketplace listings
                //    List<ContentDeveloperLinkedMarketplaceListing> linked = await _devLinkContext.Get(developer);
                //    List<MarketplaceListing> listingList = linked.Select(i => i.MarketplaceListing).ToList();
                //    //search analityics for any of the listings 
                //    List<MarketplaceAnalytics> analyticsList = await _context.Get(listingList);
                //    chartData = await _context.GetGeoIPData(analyticsList);
                //    //add them to lists

                //}
                else
                {
                    return null;
                }



                foreach (var i in chartData)
                {
                    if (i != null)
                    {
                        if (entryList.Where(j => j.Reference == i.Id).Any())
                        {
                            WorldMapChartEntry temp = entryList.Where(j => j.Reference == i.Id).FirstOrDefault();
                            temp.Quantity++;
                        }
                        else
                        {

                            WorldMapChartEntry temp = new WorldMapChartEntry()
                            {
                                Reference = i.Id,
                                Name = i?.GeoIPByCountry?.GeoIPByCountryData?.Country_Name ?? string.Empty,
                                Quantity = 1,
                                Radius = i?.GeoIPByCity?.Accuracy_Radius ?? 1000,
                                Latitude = 0.0,
                                Longitude = 0.0
                            };
                            if (i.GeoIPByCity != null)
                            {
                                temp.Latitude = i.GeoIPByCity.Latitude;
                                temp.Longitude = i.GeoIPByCity.Longitude;
                            }
                            //else if (i.GeoIPByCountry != null)
                            //{
                            //    temp.Latitude = i.GeoIPByCity.Latitude;
                            //    temp.Longitude = i.GeoIPByCity.Longitude;
                            //}
                            //else if (i.GeoASN != null)
                            //{
                            //    temp.Latitude = i.GeoIPByCity.Latitude;
                            //    temp.Longitude = i.GeoIPByCity.Longitude;
                            //}

                            entryList.Add(temp);
                        }
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
        #region Chart requests
        private async Task<List<UserAnalytics>> Get(DateTime startDate, DateTime endDate)
        {
            List<UserAnalytics> output = new List<UserAnalytics>();
            try
            {
                output = await _context.Get(startDate, endDate);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<LineChart> GetRequestByDateChartData()
        {
            LineChart output = new LineChart()
            {
                Title = "Requests By Date",
                Description = "Gross Number of Requests By Date",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                ChartEntryList = new List<LineChartEntry>()
            };
            List<LineChartEntry> entryList = new List<LineChartEntry>();
            try
            {
                DateTime endDate = DateTime.UtcNow.Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

                List<UserAnalytics> rawData = await Get(startDate, endDate);

                //for (DateTime i = startDate; endDate.CompareTo(i) > 0; i = i.AddDays(1))
                for (DateTime i = startDate; endDate >= i; i = i.AddDays(1))
                {
                    int qty = rawData.Where(j => j.TimeStamp.Date == i).Count();
                    LineChartEntry temp = new LineChartEntry()
                    {
                        Label = i.ToShortDateString(),
                        Quantity = qty
                    };
                    entryList.Add(temp);
                }
                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {

                throw;
            }
            return output;
        }
        public async Task<LineChart> GetUniqueIPChartData()
        {
            LineChart output = new LineChart()
            {
                Title = "Unique Requests",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "Requests with unique IP Addresses over the last 30 days",
                ChartEntryList = new List<LineChartEntry>()
            };
            List<LineChartEntry> entryList = new List<LineChartEntry>();
            //List<LineChartEntry> output = new List<LineChartEntry>();
            try
            {
                DateTime endDate = DateTime.UtcNow.Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

                List<UserAnalytics> rawData = await Get(startDate, endDate);

                //for (DateTime i = startDate; endDate.CompareTo(i) > 0; i = i.AddDays(1))
                for (DateTime i = startDate; endDate >= i; i = i.AddDays(1))
                {
                    int qty = rawData.Where(j => j.TimeStamp.Date == i).Select(k => k.IPAddress).Distinct().Count();
                    LineChartEntry temp = new LineChartEntry()
                    {
                        Label = i.ToShortDateString(),
                        Quantity = qty
                    };
                    entryList.Add(temp);
                }
                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {

                throw;
            }
            return output;
        }
        public async Task<PieChart> GetOddRequestChartData()
        {
            PieChart output = new PieChart()
            {
                Title = "Odd Requests",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "Number of requests that seem odd",
                ChartEntryList = new List<PieChartEntry>()
            };
            List<PieChartEntry> entryList = new List<PieChartEntry>();
            // List<PieChartEntry> output = new List<PieChartEntry>();
            try
            {
                DateTime endDate = DateTime.UtcNow.Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

                List<UserAnalytics> rawData = await Get(startDate, endDate);

                int total = rawData.Count();
                int oddRequest = await DefineOddRequests(rawData);
                int normal = total - oddRequest;

                PieChartEntry normalEntries = new PieChartEntry()
                {
                    Label = "Normal",
                    Quantity = normal
                };
                PieChartEntry oddEntries = new PieChartEntry()
                {
                    Label = "Odd",
                    Quantity = oddRequest
                };
                entryList.Add(normalEntries);
                entryList.Add(oddEntries);
                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);

            }
            return output;
        }
        public async Task<BarChart> GetPageRequestByUser()
        {
            BarChart output = new BarChart()
            {
                Title = "User Requests",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "user stay on site over the last 30 days",
                ChartEntryList = new List<BarChartEntry>()
            };
            List<BarChartEntry> entryList = new List<BarChartEntry>();
            try
            {
                DateTime endDate = DateTime.UtcNow.Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

                List<UserAnalytics> rawData = await Get(startDate, endDate);
                List<string> IpAddresses = rawData.Select(k => k.IPAddress).Distinct().ToList();
                //List<Tuple<string, int>> IpAddressVists = new List<Tuple<string, int>>();
                List<int> visitList = new List<int>();
                int veryLowUsage = 0;
                int LowUsage = 0;
                int LowMedUsage = 0;
                int MedUsage = 0;
                int HighMedUsage = 0;
                int HighUsage = 0;
                int VeryHighUsage = 0;
                int MassiveUsage = 0;


                //range between 0 and 5, 5 and 10, 10 and 20, 20 and 50, 50 and 150, 150 and 1000, 100+
                foreach (var i in IpAddresses)
                {
                    int visits = rawData.Where(j => j.IPAddress == i).Count();
                    //IpAddressVists.Add(new Tuple<string, int>(i,visits));
                    visitList.Add(visits);
                }

                veryLowUsage = visitList.Where(i => i <= 5).Count();
                LowUsage = visitList.Where(i => i > 5 && i <= 10).Count();
                LowMedUsage = visitList.Where(i => i > 10 && i <= 20).Count();
                MedUsage = visitList.Where(i => i > 20 && i <= 50).Count();
                HighMedUsage = visitList.Where(i => i > 50 && i <= 150).Count();
                HighUsage = visitList.Where(i => i > 150 && i <= 1000).Count();
                VeryHighUsage = visitList.Where(i => i > 1000 && i <= 5000).Count();
                MassiveUsage = visitList.Where(i => i > 5000).Count();

                BarChartEntry veryLowEntry = new BarChartEntry()
                {
                    Label = "0-5",
                    Quantity = veryLowUsage
                };
                entryList.Add(veryLowEntry);
                BarChartEntry lowEntry = new BarChartEntry()
                {
                    Label = "5-10",
                    Quantity = LowUsage
                };
                entryList.Add(lowEntry);
                BarChartEntry lowMedEntry = new BarChartEntry()
                {
                    Label = "10-20",
                    Quantity = LowMedUsage
                };
                entryList.Add(lowMedEntry);
                BarChartEntry medEntry = new BarChartEntry()
                {
                    Label = "20-50",
                    Quantity = MedUsage
                };
                entryList.Add(medEntry);
                BarChartEntry highMedEntry = new BarChartEntry()
                {
                    Label = "50-150",
                    Quantity = HighMedUsage
                };
                entryList.Add(highMedEntry);
                BarChartEntry highEntry = new BarChartEntry()
                {
                    Label = "150-1000",
                    Quantity = HighUsage
                };
                entryList.Add(highEntry);
                BarChartEntry veryhighEntry = new BarChartEntry()
                {
                    Label = "1000-5000",
                    Quantity = VeryHighUsage
                };
                entryList.Add(veryhighEntry);
                BarChartEntry massiveEntry = new BarChartEntry()
                {
                    Label = "5000+",
                    Quantity = MassiveUsage
                };
                entryList.Add(massiveEntry);

                output.ChartEntryList = entryList;

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }
        public async Task<PieChart> GetBrowserChartData()
        {
            PieChart output = new PieChart()
            {
                Title = "Browser Use",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "Different browsers used",
                ChartEntryList = new List<PieChartEntry>()
            };
            List<PieChartEntry> entryList = new List<PieChartEntry>();
            try
            {
                DateTime endDate = DateTime.UtcNow.Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

                List<UserAnalytics> rawData = await Get(startDate, endDate);

                int total = rawData.Count();
                int oddRequest = await DefineOddRequests(rawData);
                int normal = total - oddRequest;

                PieChartEntry normalEntries = new PieChartEntry()
                {
                    Label = "Normal",
                    Quantity = normal
                };
                PieChartEntry oddEntries = new PieChartEntry()
                {
                    Label = "Odd",
                    Quantity = oddRequest
                };
                entryList.Add(normalEntries);
                entryList.Add(oddEntries);
                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {

                throw;
            }
            return output;
        }
        public async Task<BarChart> GetBotOrUser()
        {
            BarChart output = new BarChart()
            {
                Title = "Bot or User",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "Bot or User requests over 30 days",
                ChartEntryList = new List<BarChartEntry>()
            };
            List<BarChartEntry> entryList = new List<BarChartEntry>();
            try
            {
                DateTime endDate = DateTime.UtcNow.Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

                List<UserAnalytics> rawData = await Get(startDate, endDate);
                int isBot = 0;
                int isUser = 0;

                isBot = rawData.Where(i => i.UserAgent == null || i.Referer == null).Count();
                isUser = rawData.Count() - isBot;
                //isUser = rawData.Where(i => i.UserAgent != null).Count();

                BarChartEntry veryLowEntry = new BarChartEntry()
                {
                    Label = "Bot",
                    Quantity = isBot
                };
                entryList.Add(veryLowEntry);
                BarChartEntry lowEntry = new BarChartEntry()
                {
                    Label = "User",
                    Quantity = isUser
                };
                entryList.Add(lowEntry);
                output.ChartEntryList = entryList;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        //public async Task<WorldMapChart> GetWorldChartData()
        //{
        //    WorldMapChart output = new WorldMapChart()
        //    {
        //        Title = "Traffic",
        //        IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
        //        Description = "Request Traffic by country",
        //        ChartEntryList = new List<WorldMapChartEntry>()
        //    };
        //    List<WorldMapChartEntry> entryList = new List<WorldMapChartEntry>();
        //    try
        //    {
        //        List<GeoIPData> chartData = await _geoContext.GetGeoIPData();

        //        foreach (var i in chartData)
        //        {
        //            if (entryList.Where(j => j.Reference == i.Id).Any())
        //            {
        //                WorldMapChartEntry temp = entryList.Where(j => j.Reference == i.Id).First();
        //                temp.Quantity++;
        //            }
        //            else
        //            {

        //                WorldMapChartEntry temp = new WorldMapChartEntry()
        //                {
        //                    Reference = i.Id,
        //                    Name = i?.GeoIPByCountry?.GeoIPByCountryData?.Country_Name ?? string.Empty,
        //                    Quantity = 1,
        //                    Radius = i?.GeoIPByCity?.Accuracy_Radius ?? 1000,
        //                    Latitude = 0.0,
        //                    Longitude = 0.0
        //                };
        //                if (i.GeoIPByCity != null)
        //                {
        //                    temp.Latitude = i.GeoIPByCity.Latitude;
        //                    temp.Longitude = i.GeoIPByCity.Longitude;
        //                }
        //                //else if (i.GeoIPByCountry != null)
        //                //{
        //                //    temp.Latitude = i.GeoIPByCity.Latitude;
        //                //    temp.Longitude = i.GeoIPByCity.Longitude;
        //                //}
        //                //else if (i.GeoASN != null)
        //                //{
        //                //    temp.Latitude = i.GeoIPByCity.Latitude;
        //                //    temp.Longitude = i.GeoIPByCity.Longitude;
        //                //}

        //                entryList.Add(temp);
        //            }
        //        }

        //        output.ChartEntryList = entryList;
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        //throw;
        //    }
        //    return output;
        //}

        //public async Task<List<WorldMapChartEntry>> GetChartData()
        //{
        //    List<WorldMapChartEntry> output = new List<WorldMapChartEntry>();
        //    try
        //    {
        //        List<GeoIPData> chartData = await _geoContext.GetGeoIPData();

        //        foreach (var i in chartData)
        //        {
        //            if (output.Where(j => j.Reference == i.Id).Any())
        //            {
        //                WorldMapChartEntry temp = output.Where(j => j.Reference == i.Id).First();
        //                temp.Quantity++;
        //            }
        //            else
        //            {

        //                WorldMapChartEntry temp = new WorldMapChartEntry()
        //                {
        //                    Reference = i.Id,
        //                    Name = i?.GeoIPByCountry?.GeoIPByCountryData?.Country_Name ?? string.Empty,
        //                    Quantity = 1,
        //                    Radius = i?.GeoIPByCity?.Accuracy_Radius ?? 1000,
        //                    Latitude = 0.0,
        //                    Longitude = 0.0
        //                };
        //                if (i.GeoIPByCity != null)
        //                {
        //                    temp.Latitude = i.GeoIPByCity.Latitude;
        //                    temp.Longitude = i.GeoIPByCity.Longitude;
        //                }
        //                //else if (i.GeoIPByCountry != null)
        //                //{
        //                //    temp.Latitude = i.GeoIPByCity.Latitude;
        //                //    temp.Longitude = i.GeoIPByCity.Longitude;
        //                //}
        //                //else if (i.GeoASN != null)
        //                //{
        //                //    temp.Latitude = i.GeoIPByCity.Latitude;
        //                //    temp.Longitude = i.GeoIPByCity.Longitude;
        //                //}

        //                output.Add(temp);
        //            }
        //        }


        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}

        private async Task<int> DefineOddRequests(List<UserAnalytics> rawData)
        {
            int output = 0;
            try
            {
                List<string> oddList = new List<string>()
                {
                    "SELECT","*","FROM","WHERE","DROP","TABLE","STATEMENT","INSERT","INTO","UPDATE","DELETE","CREATE","ALTER","DATABASE","INDEX","wordpress","wp","blog",
                    "wlwmanifest",".git","GponForm","WP","ads",".env","console","auth","x.js","owa","wp-includes","php","login","feed","wlwmanifest","BARFOO",
                    "actuator","gateway","routes","_ignition","execute-solution","owa","GponForm","diag_Form","Autodiscover","mifs","Logservice",
                    "log","credentials","elfinder","vendor","license","admin","rsd","test","env","backup","bk","bc","old","new","main","wordpress",
                    "users","WPnBr.dll","Additional","cplugError","doc","nmaplowercheck","Portal0000","start","mwsl","NDrX","pool","sdk",
                    "functionRouter","portal",".aspx",".inc",".cgi",".jsp","menu",".asp",".env","x.js",".aspx",".git","config",".dll","ecp","backend"

                };
                //odd paths
                output = rawData.Where(i => oddList.Any(j => i.Path.ToLower().Contains(j.ToLower()) || i.Query.ToLower().Contains(j.ToLower()))).Count();
                //rawData.Where(i => i.Path.Any(j => oddList.Contains())).Count();
                // oddList.Contains((j => j == i.Path)) == true);//.Count();



                //output = rawData.Where(i => oddList.Any(j => j == i.Query||j==i.Path))
                //    .Count();
                //output = rawData.Where(i => oddList.Any(j => j.Contains(i.Query)))
                //    .Count();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }


        #endregion
                
    }

    #endregion


    #region GeoIP sorting

    //public interface IGeoIPAnalyticsLogic
    //{
    //    Task SortAllGeoIPAnalyticsData();
    //}
    //public class GeoIPAnalyticsLogic : IGeoIPAnalyticsLogic
    //{
    //    private readonly IAnalyticsQueries _context;
    //    private readonly IHttpContextAccessor _httpContextAccessor;
    //    private readonly ClaimsPrincipal User;
    //    private readonly IConfiguration _config;
    //    private readonly IMarketingAnalyticsQueries _marketingContext;
    //    private readonly IMarketplaceAnalyticsQueries _marketplaceContext;
    //    private readonly IDeveloperAnalyticsQueries _developerContext;
    //    private readonly IUserAnalyticsQueries _userContext;
    //    private readonly IAdminAnalyticsQueries _adminContext;

    //    public GeoIPAnalyticsLogic()//IHttpContextAccessor httpContextAccessor, IConfiguration config)
    //    {
    //        //_httpContextAccessor = httpContextAccessor;
    //        _context = new AnalyticsQueries();
    //        _marketingContext = new MarketingAnalyticsQueries();
    //        //User = _httpContextAccessor.HttpContext.User;
    //        //_config = config;
    //        _marketplaceContext = new MarketplaceAnalyticsQueries();
    //        _developerContext = new DeveloperAnalyticsQueries();
    //        _userContext = new UserAnalyticsQueries();
    //        _adminContext = new AdminAnalyticsQueries();

    //    }

    //    public GeoIPAnalyticsLogic(IHttpContextAccessor httpContextAccessor, IConfiguration config)
    //    {
    //        _httpContextAccessor = httpContextAccessor;
    //        _context = new AnalyticsQueries();
    //        _config = config;
    //        User = _httpContextAccessor.HttpContext.User;
    //        _marketingContext = new MarketingAnalyticsQueries();
    //        _marketplaceContext = new MarketplaceAnalyticsQueries();
    //        _developerContext = new DeveloperAnalyticsQueries();
    //        _userContext = new UserAnalyticsQueries();
    //        _adminContext = new AdminAnalyticsQueries();

    //    }

    //    //public async Task<T> ProcessNewRequests<T>(T input) 
    //    //{
    //    //    //return;
    //    //    T output = default(T);
    //    //    try
    //    //    {
    //    //        input.IPAddress;



    //    //    }
    //    //    catch (Exception ex)
    //    //    {

    //    //        throw;
    //    //    }
    //    //    return output;
    //    //}



    //    public async Task<GeoIPData> ProcessNewRequests(MarketingAnalytics input)
    //    {
    //        GeoIPData output = new GeoIPData();
    //        try
    //        {
    //            //Check if IP has been used previously. 
    //            #region check if 
    //            //Group request by ip addresses

    //            //if some of the ip addresses do not have geoip linked then link them. 

    //            //if not continue with crazyness down below

    //            #endregion



    //            GeoIPByCity city = await GetByCity(input.IPAddress);
    //            if (city != null)
    //            {
    //                output.GeoIPByCity = city;
    //            }

    //            GeoIPByCountry country = await GetByCountry(input.IPAddress);
    //            if (country != null)
    //            {
    //                output.GeoIPByCountry = country;
    //            }

    //            GeoASN autonomous = await GetByASN(input.IPAddress);
    //            if (autonomous != null)
    //            {
    //                output.GeoASN = autonomous;
    //            }

    //            //check to see if this already exists and if it does just attach to that.
    //            bool exists = false;
    //            (exists, output) = await GeoIPDataExists(output);

    //            if (!exists)
    //            {
    //                output = new GeoIPData()
    //                {
    //                    GeoIPByCity = city,
    //                    GeoIPByCountry = country,
    //                    GeoASN = autonomous
    //                };

    //                output = await _context.Create(output);
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Febris.SharedServices.FebrisLog.Error(ex);
    //            throw;
    //        }
    //        return output;
    //    }
    //    public async Task<GeoIPData> ProcessNewRequests(MarketplaceAnalytics input)
    //    {
    //        GeoIPData output = new GeoIPData();
    //        try
    //        {
    //            //Check if IP has been used previously. 
    //            #region check if 
    //            //Group request by ip addresses

    //            //if some of the ip addresses do not have geoip linked then link them. 

    //            //if not continue with crazyness down below

    //            #endregion



    //            GeoIPByCity city = await GetByCity(input.IPAddress);
    //            if (city != null)
    //            {
    //                output.GeoIPByCity = city;
    //            }

    //            GeoIPByCountry country = await GetByCountry(input.IPAddress);
    //            if (country != null)
    //            {
    //                output.GeoIPByCountry = country;
    //            }

    //            GeoASN autonomous = await GetByASN(input.IPAddress);
    //            if (autonomous != null)
    //            {
    //                output.GeoASN = autonomous;
    //            }

    //            //check to see if this already exists and if it does just attach to that.
    //            bool exists = false;
    //            (exists, output) = await GeoIPDataExists(output);

    //            if (!exists)
    //            {
    //                output = new GeoIPData()
    //                {
    //                    GeoIPByCity = city,
    //                    GeoIPByCountry = country,
    //                    GeoASN = autonomous
    //                };
    //                output = await _context.Create(output);
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Febris.SharedServices.FebrisLog.Error(ex);
    //            throw;
    //        }
    //        return output;
    //    }
    //    public async Task<GeoIPData> ProcessNewRequests(DeveloperAnalytics input)
    //    {
    //        GeoIPData output = new GeoIPData();
    //        try
    //        {
    //            //Check if IP has been used previously. 
    //            #region check if 
    //            //Group request by ip addresses

    //            //if some of the ip addresses do not have geoip linked then link them. 

    //            //if not continue with crazyness down below

    //            #endregion



    //            GeoIPByCity city = await GetByCity(input.IPAddress);
    //            if (city != null)
    //            {
    //                output.GeoIPByCity = city;
    //            }

    //            GeoIPByCountry country = await GetByCountry(input.IPAddress);
    //            if (country != null)
    //            {
    //                output.GeoIPByCountry = country;
    //            }

    //            GeoASN autonomous = await GetByASN(input.IPAddress);
    //            if (autonomous != null)
    //            {
    //                output.GeoASN = autonomous;
    //            }

    //            //check to see if this already exists and if it does just attach to that.
    //            bool exists = false;
    //            (exists, output) = await GeoIPDataExists(output);

    //            if (!exists)
    //            {
    //                output = new GeoIPData()
    //                {
    //                    GeoIPByCity = city,
    //                    GeoIPByCountry = country,
    //                    GeoASN = autonomous
    //                };
    //                output = await _context.Create(output);
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Febris.SharedServices.FebrisLog.Error(ex);
    //            throw;
    //        }
    //        return output;
    //    }
    //    public async Task<GeoIPData> ProcessNewRequests(UserAnalytics input)
    //    {
    //        GeoIPData output = new GeoIPData();
    //        try
    //        {
    //            //Check if IP has been used previously. 
    //            #region check if 
    //            //Group request by ip addresses

    //            //if some of the ip addresses do not have geoip linked then link them. 

    //            //if not continue with crazyness down below

    //            #endregion



    //            GeoIPByCity city = await GetByCity(input.IPAddress);
    //            if (city != null)
    //            {
    //                output.GeoIPByCity = city;
    //            }

    //            GeoIPByCountry country = await GetByCountry(input.IPAddress);
    //            if (country != null)
    //            {
    //                output.GeoIPByCountry = country;
    //            }

    //            GeoASN autonomous = await GetByASN(input.IPAddress);
    //            if (autonomous != null)
    //            {
    //                output.GeoASN = autonomous;
    //            }

    //            //check to see if this already exists and if it does just attach to that.
    //            bool exists = false;
    //            (exists, output) = await GeoIPDataExists(output);

    //            if (!exists)
    //            {
    //                output = new GeoIPData()
    //                {
    //                    GeoIPByCity = city,
    //                    GeoIPByCountry = country,
    //                    GeoASN = autonomous
    //                };
    //                output = await _context.Create(output);
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Febris.SharedServices.FebrisLog.Error(ex);
    //            throw;
    //        }
    //        return output;
    //    }
    //    public async Task<GeoIPData> ProcessNewRequests(AdminAnalytics input)
    //    {
    //        GeoIPData output = new GeoIPData();
    //        try
    //        {
    //            //Check if IP has been used previously. 
    //            #region check if 
    //            //Group request by ip addresses

    //            //if some of the ip addresses do not have geoip linked then link them. 

    //            //if not continue with crazyness down below

    //            #endregion



    //            GeoIPByCity city = await GetByCity(input.IPAddress);
    //            if (city != null)
    //            {
    //                output.GeoIPByCity = city;
    //            }

    //            GeoIPByCountry country = await GetByCountry(input.IPAddress);
    //            if (country != null)
    //            {
    //                output.GeoIPByCountry = country;
    //            }

    //            GeoASN autonomous = await GetByASN(input.IPAddress);
    //            if (autonomous != null)
    //            {
    //                output.GeoASN = autonomous;
    //            }

    //            //check to see if this already exists and if it does just attach to that.
    //            bool exists = false;
    //            (exists, output) = await GeoIPDataExists(output);

    //            if (!exists)
    //            {
    //                output = new GeoIPData()
    //                {
    //                    GeoIPByCity = city,
    //                    GeoIPByCountry = country,
    //                    GeoASN = autonomous
    //                };
    //                output = await _context.Create(output);
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Febris.SharedServices.FebrisLog.Error(ex);
    //            throw;
    //        }
    //        return output;
    //    }

    //    private async Task<(bool Exists, GeoIPData Existing)> GeoIPDataExists(GeoIPData output)
    //    {
    //        bool exists = false;

    //        try
    //        {
    //            (exists, output) = await _context.ExistanceCheck(output);
    //        }
    //        catch (Exception ex)
    //        {
    //            Febris.SharedServices.FebrisLog.Error(ex);
    //            throw;
    //        }
    //        return (exists, output);
    //    }

    //    private async Task<GeoASN> GetByASN(string iPAddress)
    //    {
    //        GeoASN output = null;
    //        try
    //        {
    //            //var splitAddress = iPAddress.Split(".");
    //            var splitAddress = new string[] { };
    //            var splittingChar = ".";
    //            if (iPAddress.Contains(':'))
    //            {
    //                splittingChar = ":";
    //                splitAddress = iPAddress.Split(":");
    //            }
    //            else
    //            {
    //                splitAddress = iPAddress.Split(".");
    //            }
    //            string networkAddress = splitAddress[0] + splittingChar + splitAddress[1] + splittingChar + splitAddress[2];
    //            //string networkAddress = splitAddress[0];
    //            //string networkAddress = splitAddress[0] + splitAddress[1] + splitAddress[2] + splitAddress[3] + splitAddress[4];

    //            //List<GeoIPByCity> listOfNetworkedOptions = await _context.GetASNByNetwork(networkAddress);
    //            output = await _context.GetASNByNetwork(networkAddress);




    //        }
    //        catch (Exception ex)
    //        {
    //            Febris.SharedServices.FebrisLog.Error(ex);
    //            throw;
    //        }
    //        return output;
    //    }

    //    private async Task<GeoIPByCountry> GetByCountry(string iPAddress)
    //    {
    //        GeoIPByCountry output = null;
    //        try
    //        {
    //            //var splitAddress = iPAddress.Split(".");
    //            var splitAddress = new string[] { };
    //            var splittingChar = ".";
    //            if (iPAddress.Contains(':'))
    //            {
    //                splittingChar = ":";
    //                splitAddress = iPAddress.Split(":");
    //            }
    //            else
    //            {
    //                splitAddress = iPAddress.Split(".");
    //            }
    //            string networkAddress = splitAddress[0] + splittingChar + splitAddress[1] + splittingChar + splitAddress[2];
    //            //string networkAddress = splitAddress[0];
    //            //string networkAddress = splitAddress[0] + splitAddress[1] + splitAddress[2] + splitAddress[3] + splitAddress[4];
    //            //List<GeoIPByCountry> listOfNetworkedOptions = await _context.GetCountryByNetwork(networkAddress);
    //            output = await _context.GetCountryByNetwork(networkAddress);


    //        }
    //        catch (Exception ex)
    //        {
    //            Febris.SharedServices.FebrisLog.Error(ex);
    //            throw;
    //        }
    //        return output;
    //    }

    //    private async Task<GeoIPByCity> GetByCity(string iPAddress)
    //    {
    //        GeoIPByCity output = null;
    //        try
    //        {
    //            var splitAddress = new string[] { };
    //            var splittingChar = ".";
    //            if (iPAddress.Contains(':'))
    //            {
    //                splittingChar = ":";
    //                splitAddress = iPAddress.Split(":");
    //            }
    //            else
    //            {
    //                splitAddress = iPAddress.Split(".");
    //            }
    //            // var splitAddress = iPAddress.Split(".");
    //            string networkAddress = splitAddress[0] + splittingChar + splitAddress[1] + splittingChar + splitAddress[2];
    //            //List<GeoIPByCity> listOfNetworkedOptions = await _context.GetCityByNetwork(networkAddress);
    //            output = await _context.GetCityByNetwork(networkAddress);



    //        }
    //        catch (Exception ex)
    //        {
    //            Febris.SharedServices.FebrisLog.Error(ex);
    //            throw;
    //        }
    //        return output;
    //    }

    //    /// <summary>
    //    /// 1) Marketing
    //    /// 2) Marketplace
    //    /// 3) Admin
    //    /// 4) Developer
    //    /// 5) User
    //    /// 6) 
    //    /// </summary>
    //    /// <returns></returns>
    //    public async Task SortAllGeoIPAnalyticsData()
    //    {
    //        try
    //        {
    //            List<MarketingAnalytics> marketingAnalyticsList = await _marketingContext.GetNoGeoList();
    //            //filter out to distinct ip addresses only
    //            List<MarketingAnalytics> distinctMarketingAnalyticsList = marketingAnalyticsList.GroupBy(i => i.IPAddress).Select(i => i.First()).ToList();
    //            foreach (var i in distinctMarketingAnalyticsList)
    //            {
    //                GeoIPData temp = await ProcessNewRequests(i);
    //                i.GeoIPDataId = temp.Id;
    //                //List<MarketingAnalytics> otherthing = marketingAnalyticsList.GroupBy(j => j.IPAddress).Where(j=>j==i.IPAddress).ToList();
    //                List<MarketingAnalytics> listToUpdate = marketingAnalyticsList.Where(j => j.IPAddress == i.IPAddress).ToList();
    //                foreach (var j in listToUpdate)
    //                {
    //                    j.GeoIPDataId = temp.Id;
    //                }
    //                await _marketingContext.Update(listToUpdate);
    //            }


    //            List<MarketplaceAnalytics> marketplaceAnalyticsList = await _marketplaceContext.GetNoGeoList();
    //            //filter out to distinct ip addresses only
    //            List<MarketplaceAnalytics> distinctMarketplaceAnalyticsList = marketplaceAnalyticsList.GroupBy(i => i.IPAddress).Select(i => i.First()).ToList();
    //            foreach (var i in distinctMarketplaceAnalyticsList)
    //            {
    //                GeoIPData temp = await ProcessNewRequests(i);
    //                i.GeoIPDataId = temp.Id;
    //                //List<MarketingAnalytics> otherthing = marketingAnalyticsList.GroupBy(j => j.IPAddress).Where(j=>j==i.IPAddress).ToList();
    //                List<MarketplaceAnalytics> listToUpdate = marketplaceAnalyticsList.Where(j => j.IPAddress == i.IPAddress).ToList();
    //                foreach (var j in listToUpdate)
    //                {
    //                    j.GeoIPDataId = temp.Id;
    //                }
    //                await _marketplaceContext.Update(listToUpdate);
    //            }


    //            List<AdminAnalytics> adminAnalyticsList = await _adminContext.GetNoGeoList();
    //            //filter out to distinct ip addresses only
    //            List<AdminAnalytics> distinctAdminAnalyticsList = adminAnalyticsList.GroupBy(i => i.IPAddress).Select(i => i.First()).ToList();
    //            foreach (var i in distinctAdminAnalyticsList)
    //            {
    //                GeoIPData temp = await ProcessNewRequests(i);
    //                i.GeoIPDataId = temp.Id;
    //                //List<MarketingAnalytics> otherthing = marketingAnalyticsList.GroupBy(j => j.IPAddress).Where(j=>j==i.IPAddress).ToList();
    //                List<AdminAnalytics> listToUpdate = adminAnalyticsList.Where(j => j.IPAddress == i.IPAddress).ToList();
    //                foreach (var j in listToUpdate)
    //                {
    //                    j.GeoIPDataId = temp.Id;
    //                }
    //                await _adminContext.Update(listToUpdate);
    //            }


    //            List<DeveloperAnalytics> developerAnalyticsList = await _developerContext.GetNoGeoList();
    //            //filter out to distinct ip addresses only
    //            List<DeveloperAnalytics> distinctDeveloperAnalyticsList = developerAnalyticsList.GroupBy(i => i.IPAddress).Select(i => i.First()).ToList();
    //            foreach (var i in distinctDeveloperAnalyticsList)
    //            {
    //                GeoIPData temp = await ProcessNewRequests(i);
    //                i.GeoIPDataId = temp.Id;
    //                //List<MarketingAnalytics> otherthing = marketingAnalyticsList.GroupBy(j => j.IPAddress).Where(j=>j==i.IPAddress).ToList();
    //                List<DeveloperAnalytics> listToUpdate = developerAnalyticsList.Where(j => j.IPAddress == i.IPAddress).ToList();
    //                foreach (var j in listToUpdate)
    //                {
    //                    j.GeoIPDataId = temp.Id;
    //                }
    //                await _developerContext.Update(listToUpdate);
    //            }


    //            List<UserAnalytics> userAnalyticsList = await _userContext.GetNoGeoList();
    //            //filter out to distinct ip addresses only
    //            List<UserAnalytics> distinctUserAnalyticsList = userAnalyticsList.GroupBy(i => i.IPAddress).Select(i => i.First()).ToList();
    //            foreach (var i in distinctUserAnalyticsList)
    //            {
    //                GeoIPData temp = await ProcessNewRequests(i);
    //                i.GeoIPDataId = temp.Id;
    //                //List<MarketingAnalytics> otherthing = marketingAnalyticsList.GroupBy(j => j.IPAddress).Where(j=>j==i.IPAddress).ToList();
    //                List<UserAnalytics> listToUpdate = userAnalyticsList.Where(j => j.IPAddress == i.IPAddress).ToList();
    //                foreach (var j in listToUpdate)
    //                {
    //                    j.GeoIPDataId = temp.Id;
    //                }
    //                await _userContext.Update(listToUpdate);
    //            }





    //        }
    //        catch (Exception ex)
    //        {
    //            Febris.SharedServices.FebrisLog.Error(ex);
    //            throw;
    //        }
    //        //throw new NotImplementedException();
    //    }
    //}


    #endregion


    #region middleware
   
    class LocalAnalyticsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        // SCBA-B3 port (node hygiene D, mirrors the shared AdminAnalyticsMiddleware fix): the
        // singleton middleware no longer holds per-request state. The old per-request instance
        // field below raced across concurrent requests (each overwrote the shared _context) and
        // the new-outside-DI LocalAnalyticsLogic plus fire-and-forget Task.Run leaked a DbContext
        // per call because nothing scoped or disposed it.
        // private ILocalAnalyticsLogic _context;
        private readonly IServiceScopeFactory _scopeFactory; // singleton-safe factory: a fresh disposable scope per fire-and-forget Task.
        //private readonly IHttpContextAccessor _httpContextAccessor;

        public LocalAnalyticsMiddleware(
            RequestDelegate next,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory // SCBA-B3: background work gets its own scoped DbContext.
            )
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<LocalAnalyticsMiddleware>();
            _configuration = configuration;
            _scopeFactory = scopeFactory;
            //_context = LocalAnalyticsLogic();
        }
        /// <summary>
        /// customized
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context)
        {
            // T11. This wrote a row for EVERY request with no filtering, on both hosts, into one
            // shared table with no retention. Container health probes alone put 11,520 rows a day
            // into it on a node with ZERO users, and a single Portal page view added a row per
            // stylesheet, script and font. See AnalyticsRequestFilter for exactly what is excluded
            // and why. Real traffic, including failures and unauthenticated attempts, still records.
            if (!AnalyticsRequestFilter.ShouldRecord(context.Request.Path))
            {
                await _next(context);
                return;
            }

            try
            {
                LocalAnalytics analytics = new LocalAnalytics()
                {
                    IPAddress = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.Request.Headers[HeaderNames.UserAgent],
                    Referer = context.Request.Headers[HeaderNames.Referer],
                    Path = context.Request.Path,
                    // H-26: NEVER store the raw query string. ASP.NET Identity puts its
                    // password-reset and email-confirmation tokens in the query of the emailed
                    // link, and this row is rendered to Org Admins and never purged.
                    Query = Febris.SharedServices.SensitiveQueryRedactor.Redact(context.Request.QueryString.ToString()),
                    SourceId = context.Connection.Id.ToString()
                };
                //_context = new AnalyticsLogic(context,_configuration);
                // SCBA-B3 port (node hygiene D): the two lines below mutated a shared singleton
                // field and fire-and-forgot a new-outside-DI logic whose DbContext was never
                // scoped or disposed.
                //_context = new LocalAnalyticsLogic();
                //_ = Task.Run(() => _context.Create(analytics));
                // Capture the per-request data into a local, then run the create on a fresh scope
                // built inside the Task. The request scope is disposed when _next returns, so the
                // background work must own its own scope; no singleton field is touched.
                LocalAnalytics analyticsToRecord = analytics;
                _ = Task.Run(async () =>
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        try
                        {
                            var logic = scope.ServiceProvider.GetRequiredService<ILocalAnalyticsLogic>();
                            await logic.Create(analyticsToRecord);
                        }
                        catch (Exception ex)
                        {
                            // we don't care much about exceptions in analytics
                            _logger.LogError($"Some error in analytics middleware background task. {ex.Message}");
                        }
                    }
                });

            }
            catch (Exception ex)
            {
                // we don't care much about exceptions in analytics
                _logger.LogError($"Some error in analytics middleware. {ex.Message}");
            }

            await _next(context);
        }
    }
    public static class LocalAnalyticsMiddlewareExtensions
    {
        public static IApplicationBuilder UseLocalAnalytics(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<LocalAnalyticsMiddleware>();
        }
    }


    class UserAnalyticsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        // SCBA-B3 port (node hygiene D): drop the shared per-request instance field; resolve a
        // scoped IUserAnalyticsLogic from a fresh scope inside the background Task instead.
        // Mirrors the LocalAnalyticsMiddleware fix above.
        // private IUserAnalyticsLogic _context;
        private readonly IServiceScopeFactory _scopeFactory; // singleton-safe factory: a fresh disposable scope per fire-and-forget Task.
        //private readonly IHttpContextAccessor _httpContextAccessor;

        public UserAnalyticsMiddleware(
            RequestDelegate next,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory // SCBA-B3: background work gets its own scoped DbContext.
            )
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<UserAnalyticsMiddleware>();
            _configuration = configuration;
            _scopeFactory = scopeFactory;
        }
        /// <summary>
        /// customized
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                UserAnalytics analytics = new UserAnalytics()
                {
                    IPAddress = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.Request.Headers[HeaderNames.UserAgent],
                    Referer = context.Request.Headers[HeaderNames.Referer],
                    Path = context.Request.Path,
                    // H-26: NEVER store the raw query string. ASP.NET Identity puts its
                    // password-reset and email-confirmation tokens in the query of the emailed
                    // link, and this row is rendered to Org Admins and never purged.
                    Query = Febris.SharedServices.SensitiveQueryRedactor.Redact(context.Request.QueryString.ToString()),
                    SourceId = context.Connection.Id.ToString()
                };
                //_context = new AnalyticsLogic(context,_configuration);
                // SCBA-B3 port (node hygiene D): see LocalAnalyticsMiddleware above.
                //_context = new UserAnalyticsLogic();
                //Task.Run(() => _context.Create(analytics));
                UserAnalytics analyticsToRecord = analytics;
                _ = Task.Run(async () =>
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        try
                        {
                            var logic = scope.ServiceProvider.GetRequiredService<IUserAnalyticsLogic>();
                            await logic.Create(analyticsToRecord);
                        }
                        catch (Exception ex)
                        {
                            // we don't care much about exceptions in analytics
                            _logger.LogError($"Some error in analytics middleware background task. {ex.Message}");
                        }
                    }
                });

            }
            catch (Exception ex)
            {
                // we don't care much about exceptions in analytics
                _logger.LogError($"Some error in analytics middleware. {ex.Message}");
            }

            await _next(context);
        }
    }
    public static class UserAnalyticsMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserAnalytics(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UserAnalyticsMiddleware>();
        }
    }

    #endregion
}
