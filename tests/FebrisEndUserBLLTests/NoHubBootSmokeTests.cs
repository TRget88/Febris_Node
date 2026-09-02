// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.UserNode.LogicLayer.Logic.LauncherLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Config-less boot smoke (auth severance, slice 1b): the node
    /// boots and its request-serving object graphs construct with ZERO hub credentials -- no
    /// <c>ApiUrlPath</c>, no <c>LicenseKey</c>, no <c>HubFederation</c> section anywhere.
    /// <para>
    /// These tests run the REAL <c>Startup.ConfigureServices</c> of both EndUser hosts against
    /// exactly such a configuration (only node-local settings: databases, key ring, JWT signing
    /// secret) and then resolve the controllers' dependency graphs -- the same resolutions the
    /// first inbound request performs. Before the federation gate, several of these graphs NRE'd
    /// (unconditional <c>PassedBackConfig</c> dereferences in Remote-query ctors) the moment they
    /// were constructed without hub config.
    /// </para>
    /// <para>
    /// Deliberately NOT resolved: the Redis-backed caches (<c>IDistributedHardwareCache</c> /
    /// ticket stores and their consumers such as <c>IHardwareKeyAuthorization</c> and
    /// <c>IUserLogic</c>). Redis is a separate, orthogonal config concern (optional, not
    /// required) whose legacy factory still reads the passed-back static config --
    /// severing THAT seam is cache-posture work, not hub-credential work.
    /// </para>
    /// </summary>
    public class NoHubBootSmokeTests
    {
        private const string DummyConnectionString = "Host=localhost;Database=x;Username=x;Password=x";

        /// <summary>Node-local-only configuration: everything a self-sufficient node needs,
        /// NOTHING a hub-coupled deployment would add.</summary>
        private static IConfiguration BuildNoHubConfiguration()
        {
            string keyRing = Path.Combine(Path.GetTempPath(), "febris-nohub-boot-keys-" + Guid.NewGuid().ToString("N"));
            var settings = new Dictionary<string, string>
            {
                ["ConnectionStrings:DataDBConnection"] = DummyConnectionString,
                ["ConnectionStrings:XAPIDBConnection"] = DummyConnectionString,
                ["ConnectionStrings:AnalyticsDBConnection"] = DummyConnectionString,
                ["ConnectionStrings:UserDBConnection"] = DummyConnectionString,
                ["AppKeys:KeyRingPath"] = keyRing,
                // Node-local artifact store -- node-owned, not a hub credential.
                ["Storage:Provider"] = "FileSystem",
                ["Storage:BasePath"] = Path.Combine(Path.GetTempPath(), "febris-nohub-boot-store-" + Guid.NewGuid().ToString("N")),
                ["JwtSettings:Secret"] = "no-hub-boot-smoke-signing-secret-0123456789abcdef0123456789abcdef",
                ["JwtSettings:Issuer"] = "https://node.local",
                ["JwtSettings:Audience"] = "https://node.local",
                // NO ApiUrlPath:*, NO LicenseKey, NO HubFederation:* -- the point of the test.
            };
            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }

        private static IHttpContextAccessor RequestScopedAccessor()
        {
            // The resolutions below happen inside a request in production, where HttpContext is
            // populated; several legacy ctors dereference it unconditionally.
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            return accessor.Object;
        }

        [Fact]
        public void EndUserApi_ConfigureServices_BootsAndServes_WithZeroHubCredentials()
        {
            IConfiguration config = BuildNoHubConfiguration();

            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(e => e.EnvironmentName).Returns("Development");
            environment.SetupGet(e => e.ApplicationName).Returns(typeof(Febris.UserNode.Api.Startup).Assembly.GetName().Name);
            environment.SetupGet(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);

            var startup = new Febris.UserNode.Api.Startup(config, environment.Object);
            var services = new ServiceCollection();
            services.AddSingleton(environment.Object);
            services.AddSingleton(config);   // the generic host registers IConfiguration
            services.AddLogging();           // supplied by the generic host at runtime
            startup.ConfigureServices(services);
            services.AddSingleton(RequestScopedAccessor()); // last-registration-wins over AddHttpContextAccessor

            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using IServiceScope scope = provider.CreateScope();

            // The API controllers' dependency graphs (Token/Launcher/Module/CompanionApp
            // surfaces): every one must construct with no hub credentials. IPackageIngestLogic
            // left this list with ROADMAP 16 -- the ingest writes moved to the Portal, so the
            // API host no longer registers the ingest chain at all.
            scope.ServiceProvider.GetRequiredService<ITokenQueries>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<ILauncherLogic>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<IModuleLogic>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<IHardwareLinkedModuleLogic>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<ILocalSoftwarePackageLogic>().Should().NotBeNull();

            // IUserQueries resolves through the DI seam, NOT the static-ops fallback. The API
            // genuinely reads the Users table -- LauncherLogic (resolved above) turns cohort
            // members into the hardware-user list via IUserQueries.Get(List<Guid>)
            // over ApplicationDbContext. The API now registers that context (AddDbContext, mirroring
            // the Portal), so UserQueries' injected ctor wins and the read never touches the static
            // ApplicationDbContext.ops path (which reads app config at type load / requires the
            // developer's private appsettings.Development.json in DEBUG). No mock needed anymore.
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<Febris.UserNode.DataAccessLayer.Queries.UserQueries.IUserQueries>()
                .Should().BeOfType<Febris.UserNode.DataAccessLayer.Queries.UserQueries.UserQueries>();

            // The two analytics logics, and this is NOT decoration.
            //
            // NODE_REMOTE_TEARDOWN_PLAN.md 1.15 says of the companion
            // IContentDeveloperLinkedModuleLogic registration that "the no-hub boot smoke gates
            // this". It did not: neither logic was resolved here, so the safety net the plan cited
            // for that exact change did not exist. Added when 1.15 removed the registration.
            //
            // WHAT THIS DOES AND DOES NOT PROVE, stated precisely because the first version of this
            // comment overclaimed and mutation testing caught it.
            //
            // PROVES: both logics resolve at all with zero hub credentials, so a missing or broken
            // registration, or a ctor that throws on no-hub config, fails here.
            //
            // DOES NOT PROVE which constructor MS.DI chose. Both classes carry a greedy DI ctor AND
            // a legacy self-newing one, and GetRequiredService succeeds either way -- the legacy
            // path just builds a static-ops DbContext and news up its own queries, a working object
            // with the DI seam silently bypassed. Catching THAT needs an assertion about the
            // instance's collaborators, which their private fields do not currently expose.
            //
            // Recorded rather than left implied, because "the boot smoke gates this" is exactly the
            // sort of claim the teardown plan made about this registration when nothing here
            // resolved these types at all.
            scope.ServiceProvider.GetRequiredService<Febris.UserNode.LogicLayer.Logic.AnalyticsLogic.IModuleUsageAnalyticsLogic>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<Febris.UserNode.LogicLayer.Logic.AnalyticsLogic.IModuleDownloadAnalyticsLogic>().Should().NotBeNull();

            // And the gate the host registered is CLOSED for this configuration.
            provider.GetRequiredService<ModelLibrary.ViewModels.IHubFederationSettings>()
                .Enabled.Should().BeFalse();
        }

        [Fact]
        public void EndUserPortal_ConfigureServices_BootsAndServes_WithZeroHubCredentials()
        {
            IConfiguration config = BuildNoHubConfiguration();

            var startup = new Febris.UserNode.Portal.Startup(config);
            var services = new ServiceCollection();
            services.AddSingleton(config);   // the generic host registers IConfiguration
            services.AddLogging();           // supplied by the generic host at runtime
            startup.ConfigureServices(services);
            services.AddSingleton(RequestScopedAccessor()); // last-registration-wins over AddHttpContextAccessor

            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using IServiceScope scope = provider.CreateScope();

            // The Portal's hub-facing controller graphs: before the gate these were the classes
            // whose ctors newed Remote queries (and TokenQueries) straight into an unconditional
            // PassedBackConfig dereference. They must all construct hub-less now.
            scope.ServiceProvider.GetRequiredService<IInstitutionLogic>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<IInstitutionSettingsLogic>().Should().NotBeNull();
            // IMarketplaceListingLogic was asserted here until the marketplace teardown
            // (owner ruling 2026-08-01). The type no longer exists in the node at all, so the
            // strongest remaining statement is that nothing re-registers it -- see
            // MarketplaceAndCommerceAreGone below.
            // IMicrocredentialLogic was asserted here until the microcredential removal
            // (2026-08-18). Same shape as the marketplace note above: the type no longer exists in
            // the node, so there is nothing left to resolve.
            // ICurriculumLogic and IModuleLinkedCurriculumLogic were asserted here until the
            // curriculum removal (owner ruling 2026-09-02). Same shape as the two notes above.
            // Neither type exists in the node any more, so there is nothing left to resolve.

            // ROADMAP 16: the whole ingest chain now lives on the Portal (package upload, module
            // upload, feed sync), so the Portal graph must construct it hub-less. The feed-sync
            // logic has exactly one constructor, so an unregistered dependency fails HERE rather
            // than silently degrading.
            scope.ServiceProvider.GetRequiredService<IPackageIngestLogic>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<IPackageFeedSyncLogic>().Should().NotBeNull();

            // WidgetController's media loaders (images/badges/profile pictures/recordings) now take
            // IFileServerHandler through DI instead of self-newing it (hygiene C); an unresolvable
            // registration would fail activation for every Portal page that embeds media.
            scope.ServiceProvider.GetRequiredService<SharedServices.IFileServerHandler>()
                .Should().BeOfType<SharedServices.FileServerHandler>();

            // Registration policy is now DB-first (node initialization design 2026-08-18), so the
            // Portal's boot must not depend on the store being reachable. This configuration points
            // at a database that does not exist, which is exactly the fault case: resolution must
            // succeed and land on AdminOnly rather than throwing during activation of the login or
            // register page. Asserting the CONCRETE type as well, because a silent revert to the
            // config-only RegistrationPolicy registration would still satisfy the mode assertion on
            // a node whose configuration happens to say AdminOnly -- which this one does.
            Febris.UserNode.Portal.IdentityPolicy.IRegistrationPolicy registration =
                provider.GetRequiredService<Febris.UserNode.Portal.IdentityPolicy.IRegistrationPolicy>();
            registration.Should().BeOfType<Febris.UserNode.Portal.IdentityPolicy.NodeRegistrationPolicyResolver>();
            registration.Mode.Should().Be(Febris.UserNode.Portal.IdentityPolicy.RegistrationMode.AdminOnly);
            registration.SelfRegistrationEnabled.Should().BeFalse();
            registration.AutoProvisionJitEnabled.Should().BeFalse();

            // The save path's cache hook resolves to the SAME instance, or an admin save would
            // invalidate a snapshot nobody reads and appear not to take effect until the TTL.
            provider.GetRequiredService<Febris.UserNode.Portal.IdentityPolicy.IRegistrationPolicyCache>()
                .Should().BeSameAs(registration);

            provider.GetRequiredService<ModelLibrary.ViewModels.IHubFederationSettings>()
                .Enabled.Should().BeFalse();
        }
    }
}
