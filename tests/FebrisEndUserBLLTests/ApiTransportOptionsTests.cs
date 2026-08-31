// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using Febris.SharedServices;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// ROADMAP 5: the API host's HSTS policy RESOLVES to the operator's Transport:Hsts values.
    ///
    /// <para>
    /// The companion source guard in FebrisArchitectureTests reads the pipeline and proves the
    /// wiring is written. This one proves it WORKS: it runs the real
    /// <c>Startup.ConfigureServices</c> against an in-memory configuration and resolves
    /// <see cref="HstsOptions"/> out of the built container, which is the same object the
    /// framework's HSTS middleware reads at request time. A regex can be satisfied by code that
    /// binds the wrong section or is overwritten by a later registration; this cannot.
    /// </para>
    ///
    /// <para>
    /// The defect it pins shut: before this change the API called <c>app.UseHsts()</c> with no
    /// <c>AddHsts</c> anywhere, so it emitted the framework default of 30 days with no
    /// includeSubDomains and no preload, while the shared options object and the Portal both
    /// documented 365 days with subdomains. An operator hardening HSTS configured one host.
    /// </para>
    /// </summary>
    public class ApiTransportOptionsTests
    {
        private static ServiceProvider BuildApiContainer(Dictionary<string, string> transportSettings)
        {
            // The minimum a node host demands to build its container, mirroring
            // NoHubBootSmokeTests: connection strings, a DataProtection key ring (the host throws
            // without one), a local artifact store and a signing secret.
            const string dummyConnection = "Host=localhost;Database=x;Username=x;Password=x";
            var settings = new Dictionary<string, string>
            {
                ["ConnectionStrings:UserDBConnection"] = dummyConnection,
                ["ConnectionStrings:DataDBConnection"] = dummyConnection,
                ["ConnectionStrings:XAPIDBConnection"] = dummyConnection,
                ["ConnectionStrings:AnalyticsDBConnection"] = dummyConnection,
                ["AppKeys:KeyRingPath"] =
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "febris-transport-keys-" + Guid.NewGuid().ToString("N")),
                ["Storage:Provider"] = "FileSystem",
                ["Storage:BasePath"] =
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "febris-transport-store-" + Guid.NewGuid().ToString("N")),
                ["JwtSettings:Secret"] = "api-transport-options-test-secret-0123456789abcdef0123456789abcdef",
            };
            foreach (KeyValuePair<string, string> pair in transportSettings)
            {
                settings[pair.Key] = pair.Value;
            }

            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(e => e.EnvironmentName).Returns("Development");
            environment.SetupGet(e => e.ApplicationName)
                .Returns(typeof(Febris.UserNode.Api.Startup).Assembly.GetName().Name);
            environment.SetupGet(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);

            var startup = new Febris.UserNode.Api.Startup(config, environment.Object);
            var services = new ServiceCollection();
            services.AddSingleton(environment.Object);
            services.AddSingleton(config);
            services.AddLogging();
            startup.ConfigureServices(services);

            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            services.AddSingleton(accessor.Object);

            return services.BuildServiceProvider();
        }

        [Fact]
        public void WithNoTransportSection_TheApiUsesTheSafeSharedDefaults_NotTheFrameworkDefaults()
        {
            // The framework's own defaults are 30 days, no includeSubDomains, no preload. The
            // shared NodeTransportOptions defaults are 365 days WITH subdomains. A node that
            // configures nothing must get the latter, which is the whole reason AddHsts exists
            // here rather than a bare UseHsts.
            using ServiceProvider provider = BuildApiContainer(new Dictionary<string, string>());

            HstsOptions hsts = provider.GetRequiredService<IOptions<HstsOptions>>().Value;

            hsts.MaxAge.Should().Be(TimeSpan.FromDays(365),
                "an unconfigured node must get the node's documented policy, not the framework's weaker one");
            hsts.IncludeSubDomains.Should().BeTrue();
            hsts.Preload.Should().BeFalse("preload is only safe once the domain is actually submitted");
        }

        [Fact]
        public void TheOperatorsTransportValues_ReachTheHstsPolicy()
        {
            using ServiceProvider provider = BuildApiContainer(new Dictionary<string, string>
            {
                ["Transport:Hsts:MaxAgeDays"] = "30",
                ["Transport:Hsts:IncludeSubdomains"] = "false",
                ["Transport:Hsts:Preload"] = "true",
            });

            HstsOptions hsts = provider.GetRequiredService<IOptions<HstsOptions>>().Value;

            hsts.MaxAge.Should().Be(TimeSpan.FromDays(30), "the operator's max-age must be the one that ships");
            hsts.IncludeSubDomains.Should().BeFalse();
            hsts.Preload.Should().BeTrue();
        }

        [Fact]
        public void TheApiBindsTheWholeTransportSection_NotOnlyCors()
        {
            // The API bound this section for CORS long before it honoured the rest of it, which is
            // exactly why the gap was invisible: the section had a reader, so every
            // section-level configuration ratchet stayed green while three quarters of it did
            // nothing on this host.
            using ServiceProvider provider = BuildApiContainer(new Dictionary<string, string>
            {
                ["Transport:HttpsRedirection"] = "true",
                ["Transport:SecurityHeaders:XFrameOptions"] = "Deny",
                ["Transport:Cors:AllowedHosts:0"] = "app.example.com",
            });

            NodeTransportOptions transport =
                provider.GetRequiredService<IOptions<NodeTransportOptions>>().Value;

            transport.HttpsRedirection.Should().BeTrue();
            transport.SecurityHeaders.XFrameOptions.Should().Be("Deny");
            transport.Cors.AllowedHosts.Should().Contain("app.example.com");
        }
    }
}
