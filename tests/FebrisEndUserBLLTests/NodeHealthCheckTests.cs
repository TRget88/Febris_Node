// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.LogicLayer.Logic.HealthLogic;
using Febris.SharedServices;
using Febris.SharedServices.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Node health site sub-slice 1: pins each custom <see cref="IHealthCheck"/>'s
    /// healthy / degraded / unhealthy verdicts against mocks, the hub check's gate-aware
    /// closed-gate answer (a standalone node is Healthy, not degraded), and the response
    /// writer's secret discipline (no connection strings on the anonymous probe endpoints).
    /// Host-level registration/DI proof lives in <c>NodeHealthRegistrationResolutionTests</c>.
    /// </summary>
    public class NodeHealthCheckTests
    {
        /// <summary>A registration context whose FailureStatus is Unhealthy (the default hosts use).</summary>
        private static HealthCheckContext ContextFor(IHealthCheck check, HealthStatus failureStatus = HealthStatus.Unhealthy)
        {
            return new HealthCheckContext()
            {
                Registration = new HealthCheckRegistration("test-check", check, failureStatus, null)
            };
        }

        #region database check

        [Fact]
        public async Task DbCheck_ReachableDatabase_ReportsHealthy()
        {
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(nameof(DbCheck_ReachableDatabase_ReportsHealthy))
                .Options;
            using DataDbContext context = new DataDbContext(options);
            var check = new DbContextHealthCheck<DataDbContext>(context);

            HealthCheckResult result = await check.CheckHealthAsync(ContextFor(check));

            result.Status.Should().Be(HealthStatus.Healthy);
        }

        [Fact]
        public async Task DbCheck_UnreachableDatabase_ReportsUnhealthy_WithoutLeakingTheConnectionString()
        {
            // Port 1 on loopback refuses instantly, so CanConnectAsync fails fast -- the check
            // must convert that to the registration's failure status without surfacing the
            // connection string (the endpoints are anonymous).
            const string connectionString = "Host=127.0.0.1;Port=1;Username=probe-user;Password=probe-secret;Database=probe;Timeout=1";
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            using DataDbContext context = new DataDbContext(options);
            var check = new DbContextHealthCheck<DataDbContext>(context);

            HealthCheckResult result = await check.CheckHealthAsync(ContextFor(check));

            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Description.Should().NotContainAny("probe-secret", "probe-user", "127.0.0.1");
        }

        #endregion

        #region storage check

        private static FileSystemStorageProvider TempStorage(out string root)
        {
            root = Path.Combine(Path.GetTempPath(), "febris-health-storage-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new FileSystemStorageProvider(new StorageOptions() { BasePath = root });
        }

        [Fact]
        public async Task StorageCheck_RoundTrip_ReportsHealthy_AndDeletesTheProbeObject()
        {
            FileSystemStorageProvider storage = TempStorage(out string root);
            try
            {
                var check = new StorageProviderHealthCheck(storage);

                HealthCheckResult result = await check.CheckHealthAsync(ContextFor(check));

                result.Status.Should().Be(HealthStatus.Healthy);
                IReadOnlyList<string> leftovers = await storage.ListAsync("healthprobe");
                leftovers.Should().BeEmpty("the probe must clean up its own object");
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public async Task StorageCheck_SlowRoundTrip_ReportsDegraded()
        {
            FileSystemStorageProvider storage = TempStorage(out string root);
            try
            {
                // A zero threshold makes any successful round-trip "slow" deterministically.
                var check = new StorageProviderHealthCheck(storage, TimeSpan.Zero);

                HealthCheckResult result = await check.CheckHealthAsync(ContextFor(check));

                result.Status.Should().Be(HealthStatus.Degraded);
                result.Description.Should().Contain("slow");
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public async Task StorageCheck_FailingProvider_ReportsUnhealthy_WithTypeNameOnly()
        {
            var storage = new Mock<IStorageProvider>();
            storage.SetupGet(s => s.Kind).Returns(EnumLibrary.StorageProviderKind.FileSystem);
            storage.Setup(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>()))
                .ThrowsAsync(new IOException("disk full at /mnt/secret-base-path"));
            var check = new StorageProviderHealthCheck(storage.Object);

            HealthCheckResult result = await check.CheckHealthAsync(ContextFor(check));

            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Description.Should().Contain(nameof(IOException));
            result.Description.Should().NotContain("secret-base-path", "provider messages may embed paths and must not surface");
        }

        [Fact]
        public async Task StorageCheck_GarbledRoundTrip_ReportsUnhealthy()
        {
            // A store that returns DIFFERENT bytes than were written is corrupting artifacts --
            // strictly worse than being down, never healthy.
            var storage = new Mock<IStorageProvider>();
            storage.SetupGet(s => s.Kind).Returns(EnumLibrary.StorageProviderKind.S3);
            storage.Setup(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>())).Returns(Task.CompletedTask);
            storage.Setup(s => s.OpenReadAsync(It.IsAny<string>()))
                .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));
            storage.Setup(s => s.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            var check = new StorageProviderHealthCheck(storage.Object);

            HealthCheckResult result = await check.CheckHealthAsync(ContextFor(check));

            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Description.Should().Contain("different bytes");
        }

        #endregion

        #region redis check

        [Fact]
        public async Task RedisCheck_RoundTrip_ReportsHealthy()
        {
            var stored = new Dictionary<string, byte[]>();
            var cache = new Mock<IDistributedHardwareCache>();
            cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((k, v, _, __) => stored[k] = v)
                .Returns(Task.CompletedTask);
            cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string k, CancellationToken _) => stored.TryGetValue(k, out byte[] v) ? v : null);
            cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var check = new DistributedCacheHealthCheck<IDistributedHardwareCache>(cache.Object);

            HealthCheckResult result = await check.CheckHealthAsync(ContextFor(check));

            result.Status.Should().Be(HealthStatus.Healthy);
            cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once,
                "the probe value should be removed rather than left to expire");
        }

        [Fact]
        public async Task RedisCheck_ValueDoesNotComeBack_ReportsUnhealthy()
        {
            var cache = new Mock<IDistributedHardwareCache>();
            cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[])null);
            var check = new DistributedCacheHealthCheck<IDistributedHardwareCache>(cache.Object);

            HealthCheckResult result = await check.CheckHealthAsync(ContextFor(check));

            result.Status.Should().Be(HealthStatus.Unhealthy);
        }

        [Fact]
        public async Task RedisCheck_ThrowingCache_ReportsUnhealthy_WithTypeNameOnly()
        {
            var cache = new Mock<IDistributedHardwareCache>();
            cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("It was not possible to connect to redis-internal.example:6379"));
            var check = new DistributedCacheHealthCheck<IDistributedHardwareCache>(cache.Object);

            HealthCheckResult result = await check.CheckHealthAsync(ContextFor(check));

            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Description.Should().Contain(nameof(InvalidOperationException));
            result.Description.Should().NotContain("redis-internal.example", "cache exception messages may embed the endpoint");
        }

        #endregion

        #region hub federation check

        /// <summary>Test double: a canned HTTP outcome without any network.</summary>
        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode? _statusCode;
            private readonly Exception _exception;

            public StubHttpMessageHandler(HttpStatusCode statusCode) { _statusCode = statusCode; }
            public StubHttpMessageHandler(Exception exception) { _exception = exception; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (_exception != null)
                {
                    throw _exception;
                }
                return Task.FromResult(new HttpResponseMessage(_statusCode.Value));
            }
        }

        private static IHttpClientFactory FactoryFor(HttpMessageHandler handler)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
            return factory.Object;
        }

        private static IHubFederationSettings FederationSettings(bool enabled, string dataApi = null)
        {
            return new HubFederationSettings() { Enabled = enabled, DataApi = dataApi };
        }

        [Fact]
        public async Task HubCheck_GateClosed_ReportsHealthy_WithDisabledDescription_AndNeverTouchesHttp()
        {
            var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);   // any CreateClient call throws
            var check = new HubFederationHealthCheck(FederationSettings(enabled: false), factory.Object);

            HealthCheckResult result = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded));

            // A standalone node is a supported deployment shape, NOT a degraded one.
            result.Status.Should().Be(HealthStatus.Healthy);
            result.Description.Should().Be(HubFederationHealthCheck.DisabledDescription);
        }

        [Fact]
        public async Task HubCheck_GateOpen_AnyHttpResponse_ReportsHealthy()
        {
            // Even a 401 proves the hub answers -- this is a reachability probe, not an auth check.
            var check = new HubFederationHealthCheck(
                FederationSettings(enabled: true, dataApi: "https://hub.example/dataapi/"),
                FactoryFor(new StubHttpMessageHandler(HttpStatusCode.Unauthorized)));

            HealthCheckResult result = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded));

            result.Status.Should().Be(HealthStatus.Healthy);
            result.Description.Should().Contain("401");
        }

        [Fact]
        public async Task HubCheck_GateOpen_NoResponse_ReportsDegraded_NotUnhealthy()
        {
            // The node keeps serving locally with the hub down; Unhealthy would let K8s pull a
            // functional node out of rotation over an OPTIONAL enrichment link.
            var check = new HubFederationHealthCheck(
                FederationSettings(enabled: true, dataApi: "https://hub.example/dataapi/"),
                FactoryFor(new StubHttpMessageHandler(new HttpRequestException("No route to host hub.example:443"))));

            HealthCheckResult result = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded));

            result.Status.Should().Be(HealthStatus.Degraded);
            result.Description.Should().Contain(nameof(HttpRequestException));
            result.Description.Should().NotContain("hub.example", "HTTP exception messages may embed the hub URL");
        }

        [Fact]
        public async Task HubCheck_GateOpen_ButNoDataApiEndpoint_ReportsDegraded()
        {
            var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
            var check = new HubFederationHealthCheck(FederationSettings(enabled: true, dataApi: null), factory.Object);

            HealthCheckResult result = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded));

            result.Status.Should().Be(HealthStatus.Degraded);
            result.Description.Should().Contain("no DataApi endpoint");
        }

        #endregion

        #region response writer

        [Fact]
        public async Task ResponseWriter_EmitsStatusAndPerCheckEntries_AndLeaksNoConnectionStrings()
        {
            const string connectionString = "Host=db-internal.example;Port=5432;Username=febris;Password=super-secret";

            var entries = new Dictionary<string, HealthReportEntry>()
            {
                // Well-behaved check: its authored description passes through.
                ["storage"] = new HealthReportEntry(
                    HealthStatus.Healthy, "storage round-trip ok (FileSystem)", TimeSpan.FromMilliseconds(12), null, null),
                // Misbehaving check: it THREW, and the health service stores the raw exception
                // message as the description -- the writer must replace both with the type name.
                ["database-data"] = new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    "Failed to connect using " + connectionString,
                    TimeSpan.FromMilliseconds(31),
                    new InvalidOperationException("Failed to connect using " + connectionString),
                    null),
            };
            var report = new HealthReport(entries, TimeSpan.FromMilliseconds(43));

            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            using var body = new MemoryStream();
            httpContext.Response.Body = body;

            // detailed: true -- this test is about the DETAILED body specifically, because that is
            // the only body that can leak a provider message in the first place.
            await NodeHealthResponseWriter.WriteAsync(httpContext, report, detailed: true);

            string json = Encoding.UTF8.GetString(body.ToArray());
            httpContext.Response.ContentType.Should().StartWith("application/json");
            json.Should().Contain("\"status\":\"Unhealthy\"");
            json.Should().Contain("\"name\":\"storage\"");
            json.Should().Contain("storage round-trip ok (FileSystem)");
            json.Should().Contain("\"name\":\"database-data\"");
            json.Should().Contain(nameof(InvalidOperationException));
            json.Should().Contain("durationMs");
            json.Should().NotContainAny("super-secret", "db-internal.example", connectionString);
        }

        /// <summary>
        /// The probe endpoints are anonymous by necessity, so the DEFAULT body must not hand an
        /// unauthenticated caller an inventory of the deployment. The per-check array names every
        /// registered check, which says which databases this node owns, whether Redis is configured
        /// and whether hub federation is on.
        ///
        /// This lives in the app rather than in the bundled Caddy config on purpose: the proxy rule
        /// protects only operators who run OUR proxy, and a self-hoster fronting the node with their
        /// own nginx or Traefik got no protection and no warning.
        /// </summary>
        [Fact]
        public async Task TerseResponse_OmitsTheCheckInventory_ButKeepsTheOverallStatus()
        {
            var entries = new Dictionary<string, HealthReportEntry>()
            {
                ["database-data"] = new HealthReportEntry(
                    HealthStatus.Healthy, "npgsql reachable", TimeSpan.FromMilliseconds(4), null, null),
                ["redis-hardware"] = new HealthReportEntry(
                    HealthStatus.Healthy, "cache round-trip ok", TimeSpan.FromMilliseconds(2), null, null),
                ["hub-federation"] = new HealthReportEntry(
                    HealthStatus.Healthy, "hub federation disabled", TimeSpan.FromMilliseconds(1), null, null),
            };
            var report = new HealthReport(entries, TimeSpan.FromMilliseconds(7));

            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            using var body = new MemoryStream();
            httpContext.Response.Body = body;

            await NodeHealthResponseWriter.WriteAsync(httpContext, report, detailed: false);

            string json = Encoding.UTF8.GetString(body.ToArray());

            // The one field the container healthcheck and the release smoke gate assert on SURVIVES.
            // Reducing detail must not cost the operator the field that says the node is serving.
            json.Should().Contain("\"status\":\"Healthy\"");
            json.Should().Contain("totalDurationMs");

            // The inventory does not.
            json.Should().NotContain("checks");
            json.Should().NotContainAny("database-data", "redis-hardware", "hub-federation");
        }

        /// <summary>
        /// An unhealthy node still says so tersely. A terse body that could not express failure
        /// would be worse than the exposure it prevents: the probe would report success on a node
        /// that cannot serve, which is the silent-success family this audit exists to remove.
        /// </summary>
        [Fact]
        public async Task TerseResponse_StillReportsUnhealthy()
        {
            var entries = new Dictionary<string, HealthReportEntry>()
            {
                ["database-data"] = new HealthReportEntry(
                    HealthStatus.Unhealthy, "unreachable", TimeSpan.FromMilliseconds(9), null, null),
            };
            var report = new HealthReport(entries, TimeSpan.FromMilliseconds(9));

            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            using var body = new MemoryStream();
            httpContext.Response.Body = body;

            await NodeHealthResponseWriter.WriteAsync(httpContext, report, detailed: false);

            string json = Encoding.UTF8.GetString(body.ToArray());
            json.Should().Contain("\"status\":\"Unhealthy\"");
            json.Should().NotContain("database-data");
        }

        /// <summary>
        /// Unset means "detailed on a Development host, terse everywhere else". The nullable read
        /// matters: <c>GetValue&lt;bool&gt;</c> cannot tell "operator set false" from "operator set
        /// nothing", which would make the Development default unreachable.
        /// </summary>
        [Theory]
        [InlineData(null, true, true)]      // unset on a dev box -> detailed, so VS keeps its detail
        [InlineData(null, false, false)]    // unset in production -> terse, the safe default
        [InlineData("true", false, true)]   // explicit opt-in wins in production
        [InlineData("false", true, false)]  // explicit opt-out wins on a dev box
        public void DetailResolution_PrefersTheOperatorSetting_AndFallsBackToDevelopment(
            string configured, bool isDevelopment, bool expected)
        {
            var values = new Dictionary<string, string>();
            if (configured != null)
            {
                values[NodeHealthRegistration.DetailedResponseKey] = configured;
            }

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            NodeHealthRegistration.ResolveDetailedResponse(configuration, isDevelopment)
                .Should().Be(expected);
        }

        #endregion
    }
}
