// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Febris.UserNode.LogicLayer.Logic.HealthLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Node health site sub-slice 1: DI-resolution proof in the
    /// marker/boot-smoke pattern (<c>NoHubBootSmokeTests</c>) -- each host's REAL
    /// <c>Startup.ConfigureServices</c> is run and the health registrations are asserted from the
    /// container, so the checks provably resolve through DI, not through any fallback:
    /// <list type="bullet">
    /// <item>Ownership-driven registration: both hosts now register the Identity
    /// ApplicationDbContext (the Portal via AddIdentity's EF stores; the API via a dedicated
    /// AddDbContext so LauncherLogic's IUserQueries.Get(List&lt;Guid&gt;) reads flow through the DI
    /// seam -- sub-slice A), so both get the user database check alongside the
    /// three tenant database checks.</item>
    /// <item>Redis is OPTIONAL: no <c>RedisConnectionStrings</c> entry, no redis check; a
    /// configured connection string turns exactly its own check on.</item>
    /// <item>Every registered check's factory CONSTRUCTS from the host's scoped provider -- the
    /// same resolution the health middleware performs per evaluation.</item>
    /// </list>
    /// </summary>
    public class NodeHealthRegistrationResolutionTests
    {
        private const string DummyConnectionString = "Host=localhost;Database=x;Username=x;Password=x";

        /// <summary>Node-local-only configuration (same shape the boot smoke uses), optionally
        /// with a Redis connection configured.</summary>
        private static IConfiguration BuildNodeConfiguration(bool withRedis = false)
        {
            var settings = new Dictionary<string, string>
            {
                ["ConnectionStrings:DataDBConnection"] = DummyConnectionString,
                ["ConnectionStrings:XAPIDBConnection"] = DummyConnectionString,
                ["ConnectionStrings:AnalyticsDBConnection"] = DummyConnectionString,
                ["ConnectionStrings:UserDBConnection"] = DummyConnectionString,
                ["AppKeys:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "febris-health-reg-keys-" + Guid.NewGuid().ToString("N")),
                ["Storage:Provider"] = "FileSystem",
                ["Storage:BasePath"] = Path.Combine(Path.GetTempPath(), "febris-health-reg-store-" + Guid.NewGuid().ToString("N")),
                ["JwtSettings:Secret"] = "health-registration-signing-secret-0123456789abcdef0123456789abcdef",
                ["JwtSettings:Issuer"] = "https://node.local",
                ["JwtSettings:Audience"] = "https://node.local",
            };
            if (withRedis)
            {
                settings["RedisConnectionStrings:HardwareConnection"] = "localhost:6379";
                settings["RedisConnectionStrings:AuthConnection"] = "localhost:6380";
            }
            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }

        private static IServiceCollection ConfigureApiHost(IConfiguration config)
        {
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(e => e.EnvironmentName).Returns("Development");
            environment.SetupGet(e => e.ApplicationName).Returns(typeof(Febris.UserNode.Api.Startup).Assembly.GetName().Name);
            environment.SetupGet(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);

            var startup = new Febris.UserNode.Api.Startup(config, environment.Object);
            var services = new ServiceCollection();
            services.AddSingleton(environment.Object);
            services.AddSingleton(config);
            services.AddLogging();
            startup.ConfigureServices(services);
            return services;
        }

        private static IServiceCollection ConfigurePortalHost(IConfiguration config)
        {
            var startup = new Febris.UserNode.Portal.Startup(config);
            var services = new ServiceCollection();
            services.AddSingleton(config);
            services.AddLogging();
            startup.ConfigureServices(services);
            return services;
        }

        private static IReadOnlyList<HealthCheckRegistration> RegistrationsOf(IServiceCollection services)
        {
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            return provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ToList();
        }

        [Fact]
        public void ApiHost_RegistersExactly_TheChecksForWhatItOwns()
        {
            IServiceCollection services = ConfigureApiHost(BuildNodeConfiguration());

            IEnumerable<string> names = RegistrationsOf(services).Select(r => r.Name);

            names.Should().BeEquivalentTo(new[]
            {
                // database-user IS present now: the API host registers the Identity
                // ApplicationDbContext (sub-slice A) so LauncherLogic's IUserQueries.Get(List<Guid>)
                // reads flow through the DI seam; ownership-driven registration therefore emits the
                // user database readiness check on the API too.
                NodeHealthRegistration.DatabaseUserCheckName,
                NodeHealthRegistration.DatabaseDataCheckName,
                NodeHealthRegistration.DatabaseXApiCheckName,
                NodeHealthRegistration.DatabaseAnalyticsCheckName,
                // T11: schema readiness for the three MIGRATION-MANAGED contexts. Connectivity is
                // not readiness -- CanConnectAsync answers "reachable" for a database with zero
                // tables, so a failed migration reported green and the compose healthcheck released
                // traffic onto an unusable node. AnalyticsDbContext is deliberately absent: it is
                // provisioned with EnsureCreated() and would report its whole chain pending forever.
                NodeHealthRegistration.SchemaUserCheckName,
                NodeHealthRegistration.SchemaDataCheckName,
                NodeHealthRegistration.SchemaXApiCheckName,
                NodeHealthRegistration.StorageCheckName,
                NodeHealthRegistration.HubFederationCheckName,
                // NO redis-*: no RedisConnectionStrings in the node-local configuration.
            });
        }

        [Fact]
        public void PortalHost_AdditionallyRegisters_TheUserDatabaseCheck()
        {
            IServiceCollection services = ConfigurePortalHost(BuildNodeConfiguration());

            IEnumerable<string> names = RegistrationsOf(services).Select(r => r.Name);

            names.Should().BeEquivalentTo(new[]
            {
                NodeHealthRegistration.DatabaseUserCheckName,   // Portal owns the Identity context
                NodeHealthRegistration.DatabaseDataCheckName,
                NodeHealthRegistration.DatabaseXApiCheckName,
                NodeHealthRegistration.DatabaseAnalyticsCheckName,
                // T11: schema readiness for the three MIGRATION-MANAGED contexts. Connectivity is
                // not readiness -- CanConnectAsync answers "reachable" for a database with zero
                // tables, so a failed migration reported green and the compose healthcheck released
                // traffic onto an unusable node. AnalyticsDbContext is deliberately absent: it is
                // provisioned with EnsureCreated() and would report its whole chain pending forever.
                NodeHealthRegistration.SchemaUserCheckName,
                NodeHealthRegistration.SchemaDataCheckName,
                NodeHealthRegistration.SchemaXApiCheckName,
                NodeHealthRegistration.StorageCheckName,
                NodeHealthRegistration.HubFederationCheckName,
                NodeHealthRegistration.IdentityRolesCheckName,  // Portal wires AddIdentity -> RoleManager present
            });
        }

        [Fact]
        public void ConfiguredRedis_TurnsOn_ExactlyItsOwnCheck_PerHost()
        {
            // API registers only the hardware cache abstraction; the Portal registers both.
            IEnumerable<string> apiNames = RegistrationsOf(ConfigureApiHost(BuildNodeConfiguration(withRedis: true)))
                .Select(r => r.Name).ToList();
            IEnumerable<string> portalNames = RegistrationsOf(ConfigurePortalHost(BuildNodeConfiguration(withRedis: true)))
                .Select(r => r.Name).ToList();

            apiNames.Should().Contain(NodeHealthRegistration.RedisHardwareCheckName);
            apiNames.Should().NotContain(NodeHealthRegistration.RedisAuthCheckName,
                "the API host does not register IDistributedUserCache");
            portalNames.Should().Contain(NodeHealthRegistration.RedisHardwareCheckName);
            portalNames.Should().Contain(NodeHealthRegistration.RedisAuthCheckName);
        }

        [Fact]
        public void ApiHost_EveryRegisteredCheck_ConstructsThroughTheScopedContainer()
        {
            IServiceCollection services = ConfigureApiHost(BuildNodeConfiguration());
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using IServiceScope scope = provider.CreateScope();

            var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

            foreach (HealthCheckRegistration registration in registrations)
            {
                // The exact resolution DefaultHealthCheckService performs per evaluation: the
                // factory against a scoped provider. A silently-degraded ctor
                // or missing companion registration throws here.
                IHealthCheck check = registration.Factory(scope.ServiceProvider);
                check.Should().NotBeNull("check '{0}' must resolve through DI", registration.Name);
            }

            provider.GetRequiredService<HealthCheckService>().Should().NotBeNull();
        }

        [Fact]
        public void PortalHost_StatusPageLogic_ResolvesThroughTheScopedContainer()
        {
            // Sub-slice 2: the status page's whole aggregation graph -- HealthCheckService +
            // the convention-registered local queries + the storage seam + the federation gate --
            // must resolve through DI on the REAL Portal registrations (NodeStatusLogic has no
            // fallback ctor, so an unresolvable collaborator throws here instead of silently
            // degrading).
            IServiceCollection services = ConfigurePortalHost(BuildNodeConfiguration());
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using IServiceScope scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<INodeStatusLogic>()
                .Should().BeOfType<NodeStatusLogic>();
        }

        [Fact]
        public void PortalHost_EveryRegisteredCheck_ConstructsThroughTheScopedContainer()
        {
            IServiceCollection services = ConfigurePortalHost(BuildNodeConfiguration());
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using IServiceScope scope = provider.CreateScope();

            var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

            foreach (HealthCheckRegistration registration in registrations)
            {
                IHealthCheck check = registration.Factory(scope.ServiceProvider);
                check.Should().NotBeNull("check '{0}' must resolve through DI", registration.Name);
            }

            provider.GetRequiredService<HealthCheckService>().Should().NotBeNull();
        }
    }
}
