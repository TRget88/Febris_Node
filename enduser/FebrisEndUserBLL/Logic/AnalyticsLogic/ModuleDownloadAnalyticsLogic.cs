// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.AnalyticsModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.AnalyticsQueries;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.AnalyticsLogic
{
    public interface IModuleDownloadAnalyticsLogic
    {
        Task<List<ModuleDownloadAnalytics>> Get();
        Task Create(ModuleDownloadAnalytics analytics);
        Task<bool> Create(List<ModuleDownloadAnalytics> analytics);
        Task<ModuleDownloadAnalytics> Get(long? id);
        //Task<List<AnalyticsViewModel>> GetSorted();

        Task<LineChart> GetRequestByDateChartData();
        Task<LineChart> GetUniqueIPChartData();
        Task<PieChart> GetOddRequestChartData();
        Task<BarChart> GetPageRequestByUser();
        Task<BarChart> GetBotOrUser();
        //Task<List<WorldMapChartEntry>> GetChartData();
        Task<WorldMapChart> GetWorldChartData();
        Task<List<ModuleDownloadAnalytics>> SearchGet(string searchString);
        Task LogRequest(ClaimsPrincipal user, License license, Module package);
        Task LogRequest(ClaimsPrincipal user, Hardware license, Module package);
        Task<LineChart> LastWeekModuleDownload();
        Task<GenericMixedChart> GetModuleDownloadMixedChart();
    }
    public class ModuleDownloadAnalyticsLogic : IModuleDownloadAnalyticsLogic
    {
        private readonly IModuleDownloadAnalyticsQueries _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        //private readonly IAnalyticsQueries _geoContext;
        private readonly ClaimsPrincipal User;
        private readonly IConfiguration _config;
        //private readonly IContentDeveloperLinkedModuleLogic _linkedModuleContext;
        public ModuleDownloadAnalyticsLogic()
        {
            _context = new ModuleDownloadAnalyticsQueries();
            //_devLinkedModuleContext = new ContentDeveloperLinkedModuleLogic();
            //_linkedModuleContext = new ContentDeveloperLinkedModuleLogic(_httpContextAccessor);

        }
        public ModuleDownloadAnalyticsLogic(IHttpContextAccessor httpContextAccessor, IConfiguration config)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new ModuleDownloadAnalyticsQueries();
            _config = config;
            User = _httpContextAccessor.HttpContext.User;
            //_geoContext = new AnalyticsQueries();
        }
        // DI refactor
        public ModuleDownloadAnalyticsLogic(IHttpContextAccessor httpContextAccessor, IConfiguration config, IModuleDownloadAnalyticsQueries context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _config = config;
            User = _httpContextAccessor?.HttpContext?.User;
            //_geoContext = new AnalyticsQueries();
        }
        public ModuleDownloadAnalyticsLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new ModuleDownloadAnalyticsQueries();
            _config = StaticDetails.PassedBackConfig;
            User = _httpContextAccessor.HttpContext.User;
            //_geoContext = new AnalyticsQueries();
        }
        public async Task Create(ModuleDownloadAnalytics input)
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
        public async Task<bool> Create(List<ModuleDownloadAnalytics> input)
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
        public async Task<List<ModuleDownloadAnalytics>> Get()
        {
            List<ModuleDownloadAnalytics> output = new List<ModuleDownloadAnalytics>();
            try
            {
                if (User.IsLocalITAdmin() || User.IsLocalFebrisAdmin())
                {
                    output = await _context.Get();
                }
                else { return default; }
                //output = await _context.Get();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<ModuleDownloadAnalytics> Get(long? id)
        {
            ModuleDownloadAnalytics output = new ModuleDownloadAnalytics();
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
        private async Task<List<ModuleDownloadAnalytics>> Get(DateTime startDate, DateTime endDate)
        {
            List<ModuleDownloadAnalytics> output = new List<ModuleDownloadAnalytics>();
            try
            {
                if(User.IsLocalITAdmin()||User.IsLocalFebrisAdmin())                
                {
                    output = await _context.Get(startDate, endDate);
                }
                else { return default; }
                //output = await _context.Get(startDate, endDate);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }


        public async Task<List<ModuleDownloadAnalytics>> SearchGet(string searchString)
        {
            List<ModuleDownloadAnalytics> output = new List<ModuleDownloadAnalytics>();
            try
            {
                output = await _context.SearchGet(searchString);
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
        public async Task<List<ModuleDownloadAnalytics>> GetSorted()
        {
            List<ModuleDownloadAnalytics> output = new List<ModuleDownloadAnalytics>();
            try
            {
                //var preoutput = Get();

                //sort everything out

                ModuleDownloadAnalytics notsetup = new ModuleDownloadAnalytics()
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
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<WorldMapChart> GetWorldChartData()
        {
            WorldMapChart output = new WorldMapChart()
            {
                Title = "Traffic ",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "Last 7 Days",
                ChartEntryList = new List<WorldMapChartEntry>()
            };
            List<WorldMapChartEntry> entryList = new List<WorldMapChartEntry>();
            try
            {
                List<GeoIPData> chartData = new List<GeoIPData>();
                if (User.IsFebrisUser())
                {
                    //chartData = await _context.GetGeoIPData();
                    //chartData = await _context.GetGeoIPData(DateTime.UtcNow.Date.AddDays(-7), DateTime.UtcNow.Date);
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
        //private async Task<List<ModuleDownloadAnalytics>> Get(DateTime startDate, DateTime endDate)
        //{
        //    List<ModuleDownloadAnalytics> output = new List<ModuleDownloadAnalytics>();
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
        public async Task<LineChart> LastWeekModuleDownload()
        {
            LineChart output = new LineChart()
            {
                Title = "Module Downloads",
                Description = "last 7 days",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                ChartEntryList = new List<LineChartEntry>()
            };
            List<LineChartEntry> entryList = new List<LineChartEntry>();
            try
            {
                DateTime endDate = DateTime.UtcNow.Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;

                List<ModuleDownloadAnalytics> rawData = await Get(startDate, endDate);

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

                //throw;
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

                List<ModuleDownloadAnalytics> rawData = await Get(startDate, endDate);

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

                List<ModuleDownloadAnalytics> rawData = await Get(startDate, endDate);

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

                List<ModuleDownloadAnalytics> rawData = await Get(startDate, endDate);

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

                List<ModuleDownloadAnalytics> rawData = await Get(startDate, endDate);
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

                List<ModuleDownloadAnalytics> rawData = await Get(startDate, endDate);

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

                List<ModuleDownloadAnalytics> rawData = await Get(startDate, endDate);
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

        private async Task<int> DefineOddRequests(List<ModuleDownloadAnalytics> rawData)
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

        public async Task<ModuleDownloadAnalytics> SetUpBackgroundInfo(IHttpContextAccessor context)
        {
            try
            {
                ModuleDownloadAnalytics analytics = new ModuleDownloadAnalytics();
                // ONE trust boundary. This used to branch on UsingRevProxy and read the raw
                // X-Real-IP header, which no component trust-checks: any caller could set it. The
                // ForwardedHeaders middleware is now the single place that decides which proxies
                // are trusted (operator-declared), and it rewrites RemoteIpAddress accordingly, so
                // reading RemoteIpAddress here inherits that decision and is correct in every
                // topology -- bare metal, compose, cluster, CDN.
                    analytics = new ModuleDownloadAnalytics()
                    {
                        IPAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                        UserAgent = context.HttpContext.Request.Headers[HeaderNames.UserAgent],
                        Referer = context.HttpContext.Request.Headers[HeaderNames.Referer],
                        Path = context.HttpContext.Request.Path,
                        // H-26 was only HALF closed. The redaction landed on the two analytics
                        // MIDDLEWARES (AnalyticsLogic.cs:2361 and :2452) and never reached the two
                        // analytics loggers, so this row kept storing the query string verbatim into
                        // a table that is rendered to Org Admins. That is the same defect H-26 rated
                        // as account-takeover material retained forever: ASP.NET Identity puts
                        // password-reset and email-confirmation tokens in the query of the emailed
                        // link. Redacting at EVERY write site, not just the ones someone remembered.
                        Query = Febris.SharedServices.SensitiveQueryRedactor.Redact(context.HttpContext.Request.QueryString.ToString()),
                        SourceId = context.HttpContext.Connection.Id.ToString()
                    };
                //MarketingAnalytics analytics = new MarketingAnalytics()
                //{
                //    IPAddress = context.Connection.RemoteIpAddress?.ToString(),
                //    UserAgent = context.Request.Headers[HeaderNames.UserAgent],
                //    Referer = context.Request.Headers[HeaderNames.Referer],
                //    Path = context.Request.Path,
                //    Query = context.Request.QueryString.ToString(),
                //    SourceId = context.Connection.Id.ToString()
                //};
                //_context = new AnalyticsLogic(context,_configuration);
                //_context = new SoftwareDownloadAnalytics();
                //Task.Run(() => _context.Create(analytics));
                //Task.Run(() => Create(analytics));
                return analytics;
            }
            catch (Exception ex)
            {
                // we don't care much about exceptions in analytics
                // _logger.LogError($"Some error in analytics middleware. {ex.Message}");
                throw;
            }

            //await _next(context);
        }

        public async Task LogRequest(ClaimsPrincipal user, License license, Module package)
        {
            try
            {
                ModuleDownloadAnalytics output = await SetUpBackgroundInfo(_httpContextAccessor);
                output.ModuleUUID = package.UUID;
                output.ModuleId = package.Id;
                //if (license != default)
                //{
                    
                //}
                
                                // SCBA-B3 port (node hygiene D): LogRequest is always invoked backgrounded by its
                // callers (on a fresh DI scope via ScopedBackgroundWork), so await the write here
                // instead of detaching it onto a Task that would outlive (and race) the scope's
                // DbContext. Mirrors the shared UsageLogic twin.
                await Create(output);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.ErrorMessage("Error logging software download analytics: " + ex.StackTrace);
                throw;
            }
        }

        public async Task LogRequest(ClaimsPrincipal user, Hardware hardware, Module package)
        {
            try
            {
                ModuleDownloadAnalytics output = await SetUpBackgroundInfo(_httpContextAccessor);
                output.ModuleUUID = package.UUID;
                output.ModuleId = package.Id;
                if (hardware != default)
                {
                    output.HardwareId = hardware.Id;
                    output.HardwareUUID = hardware.UUID;
                }

                                // SCBA-B3 port (node hygiene D): LogRequest is always invoked backgrounded by its
                // callers (on a fresh DI scope via ScopedBackgroundWork), so await the write here
                // instead of detaching it onto a Task that would outlive (and race) the scope's
                // DbContext. Mirrors the shared UsageLogic twin.
                await Create(output);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.ErrorMessage("Error logging software download analytics: " + ex.StackTrace);
                throw;
            }
        }


        public async Task<GenericMixedChart> GetModuleDownloadMixedChart()
        {
            GenericMixedChart output = new GenericMixedChart()
            {
                Title = "Module Download Breakdown",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "30 Days",
                GenericChartList = new List<GenericChart>()
            };
            List<GenericChartEntry> pieChartEntryList = new List<GenericChartEntry>();
            List<GenericChartEntry> lineChartEntryList = new List<GenericChartEntry>();
            try
            {
                //Build lists
                GenericChart lineChart = new GenericChart()
                {
                    ChartType = ChartType.Line,
                    GenericChartEntryList = lineChartEntryList
                };
                GenericChart pieChart = new GenericChart()
                {
                    ChartType = ChartType.Pie,
                    GenericChartEntryList = pieChartEntryList
                };
                output.GenericChartList.Add(lineChart);
                output.GenericChartList.Add(pieChart);
                List<ModuleDownloadAnalytics> tempList = default;// await Get();
                DateTime startDate = DateTime.UtcNow.AddYears(-1).Date;
                DateTime endDate = DateTime.UtcNow.Date;
#if (DEBUG)

                tempList = await Get();

#else
startDate = DateTime.UtcNow.AddDays(-30).Date;
                endDate = DateTime.UtcNow.Date;
                tempList = await Get(startDate, endDate);
                //if (User.IsFebrisUser())

                //{
                //    tempList = await Get(DateTime.UtcNow.AddDays(-30).Date, DateTime.UtcNow.Date);
                //}
                //else if (User.IsContentDeveloper())
                //{
                //    ContentDeveloper dev = await _devContext.Get(Guid.Parse(User.ContentDeveloper()));
                //    if (dev.IsLockedOut)
                //    {
                //        return default;
                //    }
                //    List<ContentDeveloperLinkedModule> linkedList = await _linkedModuleContext.Get(dev);
                //    List<Module> tempModList = linkedList.Select(i => i.Module).ToList();
                //    tempList = await Get(tempModList, DateTime.UtcNow.AddDays(-30).Date, DateTime.UtcNow.Date);

                //}
                //else { return default; }


#endif
                tempList = tempList.OrderBy(i => i.TimeStamp).ToList();
                for (DateTime i = startDate; endDate >= i; i = i.AddDays(1))
                {
                    int qty = tempList.Where(j => j.TimeStamp.Date == i).Count();
                    GenericChartEntry temp = new GenericChartEntry()
                    {
                        Label = i.ToShortDateString(),
                        Quantity = qty
                    };
                    lineChartEntryList.Add(temp);
                }

                tempList = tempList.OrderBy(i => i.ModuleId).ToList();
                List<long> orderedList = tempList.Select(i => i.LicenseId.Value).Distinct().ToList();
                foreach (long j in orderedList)
                {
                    GenericChartEntry temp = new GenericChartEntry()
                    {
                        Label = tempList.Where(i => i.ModuleId == j).FirstOrDefault().ModuleUUID?.ToString() ?? "unknown",
                        Quantity = tempList.Where(i => i.ModuleId == j).Count()
                    };
                    pieChartEntryList.Add(temp);
                }






                ////List<ModuleDownloadAnalytics> tempList = await Get(DateTime.UtcNow.AddDays(-30).Date, DateTime.UtcNow.Date);
                //tempList = tempList.OrderBy(i => i.TimeStamp).ToList();
                ////tempList.OrderByDescending(i => i.CreationTimeStamp).ToList();
                //foreach (var i in tempList)
                //{
                //    if (lineChartEntryList.Any(j => j.Label == i.TimeStamp.ToShortDateString()))
                //    {
                //        GenericChartEntry lineTemp = lineChartEntryList.Where(j => j.Label == i.TimeStamp.ToShortDateString()).First();
                //        lineTemp.Quantity++;
                //    }
                //    else
                //    {
                //        GenericChartEntry lineTemp = new GenericChartEntry()
                //        {
                //            Label = i.TimeStamp.ToShortDateString(),
                //            Quantity = 1
                //        };
                //        lineChartEntryList.Add(lineTemp);
                //    }

                //    if (lineChartEntryList.Any(j => j.Label == i.ModuleId.ToString()))
                //    {
                //        GenericChartEntry pieTemp = lineChartEntryList.Where(j => j.Label == i.ModuleId.ToString()).First();
                //        pieTemp.Quantity++;
                //    }
                //    else
                //    {
                //        GenericChartEntry pieTemp = new GenericChartEntry()
                //        {
                //            Label = i.ModuleId.ToString(),
                //            Quantity = 1
                //        };
                //        lineChartEntryList.Add(pieTemp);
                //    }
                //}

                ////output = await ManagementAlgorithms.OrderChartLists(output);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                //throw;
            }
            return output;
        }




    }
}
