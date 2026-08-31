// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using AspNetCoreRateLimit;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.LogicLayer.Logic.AnalyticsLogic;
using Febris.UserNode.LogicLayer.Logic.AuthorizationLogic;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.UserNode.LogicLayer.Logic.HealthLogic;
using Febris.UserNode.LogicLayer.Logic.LauncherLogic;
using Febris.SharedServices;
using Febris.SharedServices.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
// NET8 Wave 4: deprecated Microsoft.Extensions.Caching.Redis package removed;
// StackExchangeRedis provides the same RedisCache/RedisCacheOptions type names.
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Febris.UserNode.DataAccessLayer;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;

namespace Febris.UserNode.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            _environment = environment;
            StaticConfig.Configuration = configuration;
        }
        public static class StaticConfig
        {
            public static IConfiguration Configuration;
        }

        public IConfiguration Configuration { get; }
        private readonly IWebHostEnvironment _environment;

        private DirectoryInfo GetKyRingDirectoryInfo()
        {
            // MDM-B8 fix (net8 boot): the previous do/while(keyRingPath != null) spun FOREVER when the
            // configured key-ring directory did not already exist -- keyRingPath was read once and never
            // mutated, and the loop only returned on Exists==true, so a fresh deployment whose
            // AppKeys:KeyRingPath dir hadn't been created yet hung the host in ConfigureServices (found
            // while booting EndUserApi 2026-07-06). Replaced with an idempotent create-if-missing that
            // fails fast on missing config or an unwritable path. Old (infinite-loop) version:
            //   string keyRingPath = Configuration.GetSection("AppKeys").GetValue<string>("KeyRingPath");
            //   do { var d = new DirectoryInfo($"{keyRingPath}"); if (d.Exists) return d; }
            //   while (keyRingPath != null);
            //   throw new Exception($"key ring path not found");
            string keyRingPath = Configuration.GetSection("AppKeys").GetValue<string>("KeyRingPath");
            if (string.IsNullOrWhiteSpace(keyRingPath))
            {
                throw new InvalidOperationException(
                    "AppKeys:KeyRingPath is not configured -- the DataProtection key ring has no persistence " +
                    "location, so every auth cookie/ticket would be invalidated on each restart. Set it in " +
                    "appsettings or the environment.");
            }
            DirectoryInfo keyRingDirectoryInfo = new DirectoryInfo(keyRingPath);
            if (!keyRingDirectoryInfo.Exists)
            {
                keyRingDirectoryInfo.Create();   // idempotent; throws fast on an invalid/unwritable path
                keyRingDirectoryInfo.Refresh();
            }
            return keyRingDirectoryInfo;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            #region data protection keys persistance
            services.AddDataProtection()
                        .PersistKeysToFileSystem(GetKyRingDirectoryInfo())
                        .SetApplicationName("Febris.UserAuth");
            #endregion

            Log.Information("Key Persistance complete");

            // DI refactor activation: scoped per-tenant DbContexts +
            // convention-registered IXxxQueries, so the BLL's greedy DI ctors resolve with
            // DbContext-backed LOCAL query instances (incl. the node-local vocabulary stores)
            // instead of the legacy self-newing path. TryAddScoped keeps explicit registrations.
            services.AddFebrisUserNodeDataAccess(Configuration);

            // Identity ApplicationDbContext on the API host, mirroring the
            // Portal's registration. The API does NOT do AddIdentity (the Portal owns login /
            // UserManager), but it DOES read the Users table: LauncherLogic.HardwareInialization
            // resolves cohort members' user records via IUserQueries.Get(List<Guid>). Without this
            // context registered, UserQueries' greedy DI ctor was unresolvable and MS.DI fell back
            // to its static-ops ctor (new ApplicationDbContext(ops.DbOptions)) -- the same silent
            // degradation the companion registrations below guard against, and the exact seam the
            // no-hub boot smoke pinned. This restores the DI-injected read path.
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(Configuration.GetConnectionString("UserDBConnection")));

            // Storage seam LIVE: binds the "Storage" config section
            // (FileSystem default, legacy SmbClient:Path fallback) and registers IStorageProvider.
            // NEW node paths -- module-package ingest, the software-package store, and their
            // downloads -- flow through this seam from day one; legacy FileServerHandler call
            // sites are untouched (their migration is the separate Phase 3 cutover).
            services.AddFebrisStorage(Configuration);

            // ROADMAP 16: the package-ingest chain (IPackageIngestLogic, its ObjectLogic peers
            // from ROADMAP 15, and IPackageFeedSyncLogic) is no longer registered here. Both
            // ingest writes and the feed-sync trigger moved to the Portal behind cookie auth,
            // which deleted the NodeAdmin token that existed solely to reach them, and no
            // API-resolved logic depends on any of the five (LauncherLogic takes only *Queries,
            // which the convention sweep covers). The Portal registers the whole chain.

            // Companions to the DAL registration so the BLL greedy DI ctors are fully
            // satisfiable (an unresolvable parameter makes MS.DI silently fall back to the
            // legacy self-newing ctor -- the exact degradation the DI seam must not have):
            // StatementLogic needs IStatementFileHandler; LauncherLogic additionally needs
            // IStatementLogic + IModuleUsageAnalyticsLogic (non-*Queries, so the convention
            // sweep does not cover them).
            services.AddScoped<Febris.SharedServices.IStatementFileHandler, Febris.SharedServices.StatementFileHandler>();
            services.AddScoped<Febris.PrimaryLogicLayer.Logic.XApiLogic.IStatementLogic, Febris.PrimaryLogicLayer.Logic.XApiLogic.StatementLogic>();
            // Video ownership. LauncherLogic.VideoAttachmentHandler records which actor a minted
            // recording name belongs to. Unregistered, LauncherLogic's greedy ctor becomes
            // unresolvable and MS.DI silently falls back to the self-newing ctor, where the
            // recording logic is null and ownership is never recorded -- exactly the degradation
            // the comment above warns about, and it would defeat the Portal entitlement check.
            services.AddScoped<Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic, Febris.UserNode.LogicLayer.Logic.DataLogic.RecordingLogic>();
            // Video retention. The reaper is scoped (it touches the DataDb through
            // IRecordingQueries); the hosted service resolves it in a fresh scope per run.
            // Deleting finished recordings is OFF unless VideoRetention:PurgeAfterDays is set, so
            // by default this only clears abandoned upload fragments.
            services.AddScoped<Febris.UserNode.LogicLayer.Logic.DataLogic.IVideoRetentionReaper, Febris.UserNode.LogicLayer.Logic.DataLogic.VideoRetentionReaper>();
            services.AddHostedService<Febris.UserNode.Api.BackgroundTasks.VideoRetentionService>();
            services.AddScoped<IModuleUsageAnalyticsLogic, ModuleUsageAnalyticsLogic>();
            // Auth severance companion registration, surfaced by the no-hub
            // boot smoke: HardwareLinkedModuleLogic's greedy DI ctor needs IModuleFileHandler;
            // unresolvable, MS.DI silently degraded to the legacy self-newing ctor -- which also
            // nulls the storage seam (_storage = null), so the DI-live IStorageProvider download
            // path never actually ran on this host. Registering the handler restores the greedy
            // ctor (and with it the storage-backed module download).
            services.AddScoped<Febris.SharedServices.IModuleFileHandler, Febris.SharedServices.ModuleFileHandler>();

            #region caching
            // A-02 Stage 2: the revocation list the JWT middleware consults per request and
            // HardwareLogic publishes to on lock. Scoped, so it enters JwtHardwareMiddleware
            // through Invoke rather than its constructor.
            services.AddScoped<IHardwareRevocationList, HardwareRevocationList>();
            services.AddSingleton<IDistributedHardwareCache>(x =>
            {
                var options = x.GetRequiredService<IOptions<RedisCacheOptions>>();
                //options.Value.Configuration = ...  set you server IP, etc
                options.Value.Configuration = StaticDetails.PassedBackConfig.GetSection("RedisConnectionStrings").GetValue<string>("HardwareConnection");
                return new DistributedHardwareCache(options);
            });

            //no idea if this is needed
            services.AddResponseCaching(options =>
            {
                options.UseCaseSensitivePaths = true;
                options.MaximumBodySize = 1024;
            });
            #endregion

            Log.Information("cach adding complete");

            #region Form options

            services.Configure<FormOptions>(options =>
            {
                options.MemoryBufferThreshold = Int32.MaxValue;
                options.ValueCountLimit = 50000; //default 1024
                options.ValueLengthLimit = int.MaxValue; //not recommended value
                // T6 quota: was `?? long.MaxValue`, i.e. UNBOUNDED, on the one host that ingests
                // video. Its sibling the Portal already defaults to 10 GiB with the same key
                // (Portal Startup.cs), and neither host sets the key in any config file, so this
                // host was running unbounded purely because its default differed. Matched to the
                // Portal rather than invented.
                //
                // Deliberately NOT lowered further: this host also ingests module and software
                // packages (ModuleController.Upload, SoftwarePackageController.Upload), which are
                // archives and legitimately large. A limit small enough to bound a 5 MB video part
                // would silently break package ingest, which is why the per-part and
                // per-recording video caps live on the video route instead (VideoUploadLogic).
                options.MultipartBodyLengthLimit = Configuration.GetValue<long?>("UploadLimits:MaxMultipartBodyBytes") ?? 10737418240; // 10 GiB, matching the Portal host
                options.MultipartHeadersLengthLimit = Int32.MaxValue;
                options.MultipartBoundaryLengthLimit = Int32.MaxValue;
                options.BufferBodyLengthLimit = Int64.MaxValue;
            });

            #endregion

            Log.Information("Form options");

            #region set static options for injectable values
            services.Configure<JWTSettingsModel>
                (Configuration.GetSection("JwtSettings"));
            // CertificationSettings binding REMOVED (ROADMAP 18): nothing anywhere injected
            // IOptions<CertificationSettingsViewModel>, the Portal never bound it, and the type
            // is gone with it.
            #endregion

            Log.Information("Auth Settings Added");

            #region Authentication

            #region JWT Authentication   

            //jwt settings can be passed back
            var jwtSection = Configuration.GetSection("JwtSettings");
            services.Configure<JwtSettings>(jwtSection);
            var jwtBearerTokenSettings = jwtSection.Get<JwtSettings>();

            // HIGH-2 (2026-05-24): resolve the JWT signing secret via the
            // centralized provider -- prefers the FEBRIS_JWT_SIGNING_SECRET
            // env var, falls back to JwtSettings:Secret in config, fails fast
            // in non-Development on unsubstituted templates / short keys.
            var jwtKeyProvider = new JwtSigningKeyProvider(Configuration, _environment.IsDevelopment());
            if (jwtKeyProvider.DevelopmentSecretWaiver != null)
            {
                // ROADMAP 18: the Development carve-out used to be silent. On a dev box with no
                // FEBRIS_JWT_SIGNING_SECRET and no JwtSettings:Secret in the overlay, the literal
                // placeholder "{JwtTokenSecret}" IS the HMAC key every device token is signed with,
                // and nothing said so. Now it does, once, at boot, as a warning.
                Log.Warning(
                    "JWT signing secret accepted ONLY because this host is Development: {Reason} " +
                    "Every token this host mints is signed with a development-grade key.",
                    jwtKeyProvider.DevelopmentSecretWaiver);
            }
            services.AddSingleton<IJwtSigningKeyProvider>(jwtKeyProvider);
            services.PostConfigure<JwtSettings>(s => s.Secret = jwtKeyProvider.GetSecret());

            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                //x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
#if (DEBUG)
                options.RequireHttpsMetadata = false;
#endif
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtBearerTokenSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtBearerTokenSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    // SSO Tier 1: accept legacy HMAC and new RS256 during the transition.
                    IssuerSigningKeys = jwtKeyProvider.GetAllValidationKeys(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

            #endregion



            #endregion

            Log.Information("Authentication Added");

            #region Scheduled Tasks - currently unused
            //services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            #endregion

            //Log.Information("Scheduled Tasks Added");

            #region Scoping 
            //services.AddSingleton<ITicketStore, RedisCacheTicketStore>();

            // Scoped to align all three device-token hosts (SharedAPI already uses AddScoped). Safe:
            // the only consumer is the per-request TokenController, so there is no captive singleton
            // dependency. I-04.
            services.AddScoped<IHardwareKeyAuthorization, HardwareKeyAuthorization>();

            services.AddHttpContextAccessor();
            //services.AddScoped<ConnectionFactory>();


            //services.AddScoped<ILicenseKeyAuthorization, LicenseKeyAuthorization>();

            services.AddScoped<ILocalAnalyticsLogic, LocalAnalyticsLogic>();
            services.AddScoped<IUserAnalyticsLogic, UserAnalyticsLogic>();
            services.AddScoped<IAnalyticsLogic, AnalyticsLogic>();
            services.AddScoped<IModuleDownloadAnalyticsLogic, ModuleDownloadAnalyticsLogic>();
            services.AddScoped<IModuleUsageAnalyticsLogic, ModuleUsageAnalyticsLogic>();
            // REMOVED 2026-08-18, teardown 1.15, and only safe because the other half went with it.
            //
            // This registration existed solely so MS.DI would pick ModuleUsageAnalyticsLogic's
            // greedy constructor: IContentDeveloperLinkedModuleLogic being unresolvable made MS.DI
            // silently fall back to the legacy self-newing ctor (static-ops DbContext plus a newed
            // Remote query), the exact degradation the DI seam must not have.
            //
            // That parameter is now gone from both analytics classes, so the greedy ctor no longer
            // asks for it and there is nothing left to fall back from. The plan says do both or
            // neither, and both were done in one change. The no-hub boot smoke still gates the
            // resolution.
            //
            // The parameters were removable because all four contentmgmt dependencies were
            // WRITE-ONLY: assigned in four constructors and dereferenced in no method body. The
            // plan's own fact-check called them needed, having confirmed the fields were live
            // rather than that anything read them.
            //services.AddScoped<IDeveloperAnalyticsLogic, DeveloperAnalyticsLogic>();
            services.AddScoped<ILauncherLogic, LauncherLogic>();
            services.AddScoped<IModuleLogic, ModuleLogic>();
            services.AddScoped<IHardwareLinkedModuleLogic, HardwareLinkedModuleLogic>();
            // ROADMAP 18 found that VideoLimits:* was read and then IGNORED. VideoUploadLogic's
            // greedy constructor reads the limits, but it needs IVideoFileHandler, which nothing
            // registered, so MS.DI silently fell back to the one-argument legacy constructor that
            // hardcodes the defaults -- exactly the bypass the greedy constructor's own comment
            // warns about. The quota still applied (the legacy path self-news RecordingLogic); it
            // just was not the operator's quota. VideoQuotaTests never noticed because it
            // constructs the greedy ctor directly. VideoUploadLogicResolutionTests now resolves
            // it the way the host does.
            services.AddSingleton<Febris.SharedServices.IVideoFileHandler, Febris.SharedServices.VideoFileHandler>();
            services.AddScoped<IVideoUploadLogic, VideoUploadLogic>();
            services.AddScoped<ILocalSoftwarePackageLogic, LocalSoftwarePackageLogic>();

            #endregion

            Log.Information("Scoping Added");

            // ROADMAP 5: operator-configurable HSTS, matching the Portal host. Without this the
            // pipeline's app.UseHsts() ran on the framework's defaults -- 30 days, no
            // includeSubDomains, no preload -- and Transport:Hsts was read on the Portal only, so
            // an operator who hardened HSTS got it on one of their two hosts and no warning about
            // the other. The options object carries the safe defaults (365 days, includeSubDomains
            // on, preload off), so a node with no Transport:Hsts section still gets a STRONGER
            // policy than the bare call it replaces.
            NodeTransportOptions transportOptions =
                Configuration.GetSection(NodeTransportOptions.SectionName).Get<NodeTransportOptions>()
                ?? new NodeTransportOptions();
            services.Configure<NodeTransportOptions>(Configuration.GetSection(NodeTransportOptions.SectionName));
            services.AddHsts(o =>
            {
                o.MaxAge = TimeSpan.FromDays(transportOptions.Hsts.MaxAgeDays);
                o.IncludeSubDomains = transportOptions.Hsts.IncludeSubdomains;
                o.Preload = transportOptions.Hsts.Preload;
            });

            #region  Added by VS
            //add json support
            services.AddControllers().AddNewtonsoftJson();
            //services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Febris.UserNode.Api", Version = "v1" });
            });
            #endregion

            Log.Information("Controller info Added");

            // file-upload-hardening: per-IP request rate limiting (AspNetCoreRateLimit). Rules live in appsettings under "IpRateLimiting".
            services.AddMemoryCache();
            services.Configure<AspNetCoreRateLimit.IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
            services.AddSingleton<AspNetCoreRateLimit.IIpPolicyStore, AspNetCoreRateLimit.MemoryCacheIpPolicyStore>();
            services.AddSingleton<AspNetCoreRateLimit.IRateLimitCounterStore, AspNetCoreRateLimit.MemoryCacheRateLimitCounterStore>();
            services.AddSingleton<AspNetCoreRateLimit.IRateLimitConfiguration, AspNetCoreRateLimit.RateLimitConfiguration>();
            services.AddSingleton<AspNetCoreRateLimit.IProcessingStrategy, AspNetCoreRateLimit.AsyncKeyLockProcessingStrategy>();
            services.AddInMemoryRateLimiting();

            // Node health site: readiness checks for what THIS host owns.
            // Deliberately LAST in ConfigureServices -- the helper inspects the service
            // collection (DbContexts, IStorageProvider, the Redis cache abstractions) to decide
            // which checks exist, so every ownership registration above must already be present.
            services.AddNodeHealthChecks(Configuration);

            //services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // MUST run first, before HSTS, rate limiting, analytics and auth all read the values it
            // corrects. The API had NO forwarded-headers handling at all, unlike the Portal, so
            // behind ANY reverse proxy -- the bundled Caddy container, a Kubernetes ingress --
            // Request.Scheme stayed http and every caller resolved to the proxy's own address.
            // That degraded the analytics IP on every request AND the refresh-token IP binding at
            // HardwareKeyAuthorization:133,:167, which became one constant for all devices.
            //
            // Operator-declared via the ForwardedHeaders section. Absent that section the framework
            // default (loopback only) applies, so adding this changes nothing until opted into.
            Microsoft.AspNetCore.Builder.ForwardedHeadersOptions forwardedOptions =
                Febris.SharedServices.ForwardedHeadersConfiguration.Build(Configuration);
            if (forwardedOptions != null)
            {
                app.UseForwardedHeaders(forwardedOptions);
            }

            // Operator-configurable transport CORS (Transport section; empty AllowedHosts => same-origin + localhost).
            NodeTransportOptions transport =
                Configuration.GetSection(NodeTransportOptions.SectionName).Get<NodeTransportOptions>()
                ?? new NodeTransportOptions();

            #region env specifics
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Febris.UserNode.Api v1"));
            }
            else
            {
                // MED-4: production global exception handler. Without it an unhandled exception
                // returned a bare 500. Return a consistent application/problem+json body and log it.
                app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
                {
                    var exFeature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
                    if (exFeature?.Error != null)
                    {
                        Serilog.Log.Error(exFeature.Error, "Unhandled exception for {Path}", exFeature.Path);
                    }
                    ctx.Response.StatusCode = 500;
                    ctx.Response.ContentType = "application/problem+json";
                    await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        title = "An unexpected error occurred.",
                        status = 500,
                        traceId = ctx.TraceIdentifier
                    }));
                }));
                // ROADMAP 5: HSTS is non-Development only (unchanged) but now config-driven via
                // AddHsts(Transport:Hsts) in ConfigureServices, and the operator can turn it off
                // entirely -- e.g. when TLS terminates at a proxy that owns the header itself.
                // Emission still depends on Request.IsHttps, which behind a proxy is corrected by
                // the UseForwardedHeaders call at the top of this pipeline.
                if (transport.Hsts.Enabled)
                {
                    app.UseHsts();
                }
            }
            #endregion

            Log.Information("Environment Config Complete");

            // ROADMAP 5: static security headers, operator-configured through Transport:SecurityHeaders
            // exactly as on the Portal. Hand-rolled rather than the Portal's NWebsec extension methods
            // on purpose: the API does not reference NWebsec, and adding a package to emit two constant
            // headers would be a dependency for nothing. This is also how the API already emitted
            // nosniff, so the shape matches its own precedent rather than the other host's.
            //
            // nosniff was previously UNCONDITIONAL here while the Portal read a flag for it. It now
            // reads the same flag, so the two hosts answer an operator identically.
            app.Use(async (ctx, next) =>
            {
                if (transport.SecurityHeaders.XContentTypeOptions)
                {
                    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
                }
                if (transport.SecurityHeaders.XXssProtection)
                {
                    ctx.Response.Headers["X-XSS-Protection"] = "1; mode=block";
                }
                // "Off" omits the header; "Deny" blocks all framing; anything else -- including
                // "SameOrigin" and any typo -- fails safe to SameOrigin, so protection is never
                // silently lost to a misspelling. Same rule the Portal applies.
                if (!string.Equals(transport.SecurityHeaders.XFrameOptions, "Off", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Response.Headers["X-Frame-Options"] =
                        string.Equals(transport.SecurityHeaders.XFrameOptions, "Deny", StringComparison.OrdinalIgnoreCase)
                            ? "DENY"
                            : "SAMEORIGIN";
                }
                await next();
            });

            // T4: let /api/Statement/Backup keep a verbatim copy of the body.
            //
            // /Submit owns its body (parameterless action calling XApiStatementBinding.ReadAsync)
            // so it can capture raw bytes. /Backup binds [FromBody] JObject, and MVC consumes the
            // stream during binding, which is why its comment said raw preservation was impossible
            // there. Buffering makes the consumed stream re-readable, so the action can rewind
            // after binding and persist the ORIGINAL bytes.
            //
            // Deliberately NOT solved by making /Backup parameterless like /Submit. That would have
            // moved five framework-supplied behaviours into the action, and measuring both routes
            // against a running node first showed the two are NOT equivalent today:
            //
            //   case                  /Backup            /Submit
            //   malformed/empty/null  400 problem+json   400 {"error","detail"}
            //   wrong content-type    415                200 {"success":true}
            //
            // Mirroring /Submit would have reshaped every error body a client parses AND regressed
            // the 415 into a 200, because /Submit does not check Content-Type at all. Buffering
            // leaves MVC's binding, its problem+json responses and its 415 exactly as they are.
            //
            // Scoped to the one route so no other endpoint pays the buffering cost. The 1 MiB
            // ceiling matches XApiStatementBinding.DefaultMaxBodyBytes: beyond it the body spills
            // to a temp file rather than being held in memory.
            app.Use(async (ctx, next) =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api/Statement/Backup", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Request.EnableBuffering(
                        bufferThreshold: Febris.SharedServices.XApiStatementBinding.DefaultMaxBodyBytes);
                }
                await next();
            });

            // MED-6: surface any config value still left as a literal {Placeholder} in a deployed
            // environment (a missing ConfigMap/Secret injection). Logs by default; throws only when
            // ConfigValidation:FailFastOnUnresolvedPlaceholders=true. No-op in Development.
            var unresolvedPlaceholders = ConfigurationPlaceholderValidator.Validate(Configuration);
            if (unresolvedPlaceholders.Count > 0)
            {
                Log.Warning("Unresolved configuration placeholders (no deploy-time value injected): {Placeholders}", string.Join(", ", unresolvedPlaceholders));
            }

            #region Routing
            // ROADMAP 5: off by default (Transport:HttpsRedirection), same as the Portal. Self-host
            // nodes usually terminate TLS at a reverse proxy, where an app-level redirect would
            // loop; operators fronting the API directly can turn it on. It was a commented-out line
            // before, which meant an operator who set the key on both hosts silently got it on one.
            if (transport.HttpsRedirection)
            {
                app.UseHttpsRedirection();
            }

            app.UseIpRateLimiting();
            app.UseRouting();
            #endregion

            Log.Information("Routing Config Complete");

            //app.UseDeveloperAnalytics();
            app.UseLocalAnalytics();

            Log.Information("Custom Analytics Added");

            #region Authorization   
            app.UseJwtHardwareMiddleware();
            //app.UseMiddleware<JwtMiddleware>();
            //app.UseAuthentication();
            app.UseAuthorization();
            //app.UseJwtHardwareMiddleware();
            #endregion

            Log.Information("Authorization Complete");

            #region Static Files
            app.UseStaticFiles();
            #endregion

            Log.Information("Static Files access (this may need to move up) Config Complete");

            #region cross origin                  
            app.UseCors(x =>
            {
                // Operator-configured origins (Transport:Cors:AllowedHosts) + localhost, replacing febr.is so a
                // self-host node trusts its OWN frontend. Specific-origin predicate keeps AllowCredentials valid.
                x.SetIsOriginAllowed(origin => NodeTransport.IsOriginAllowed(origin, transport.Cors.AllowedHosts))
                 .AllowAnyMethod()
                 .AllowAnyHeader();
                if (transport.Cors.AllowCredentials)
                {
                    x.AllowCredentials();
                }
            });

            #endregion

            Log.Information("Cross orgin cookies Config Complete");

            #region Caching                        

            app.UseResponseCaching();
            #endregion

            Log.Information("Caching Config Complete");

            #region Endpoints
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                // Node health site: anonymous /health/live + /health/ready for
                // Docker/K8s probes (which cannot authenticate). Mapped endpoints resolve before
                // the 401 termination middleware below, and JwtHardwareMiddleware passes
                // token-less requests through untouched.
                //
                // The body is TERSE unless this host is Development or the operator opts in with
                // HealthChecks:DetailedResponse. Anonymous plus enumerated dependencies is a
                // deployment inventory, and the previous 404 in the bundled Caddy config protected
                // only operators who ran that proxy.
                endpoints.MapNodeHealthEndpoints(
                    NodeHealthRegistration.ResolveDetailedResponse(Configuration, env.IsDevelopment()));
            });
            #endregion

            Log.Information("Set Endpoints Config Complete");

            #region termination middleware
            app.Run(async (context) =>
            {
                await ReturnErrorResponse(context);
                //await context.Response.WriteAsync("Could Not Find Anything Here");
            });
            #endregion

            Log.Information("Termination Config Complete");

            
        }

        private async Task ReturnErrorResponse(HttpContext context)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.StartAsync();
        }

        
    }
}
