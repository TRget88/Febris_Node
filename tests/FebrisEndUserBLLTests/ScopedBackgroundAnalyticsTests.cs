// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.AnalyticsModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.AnalyticsQueries;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.AnalyticsLogic;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.SharedServices;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the SCBA-B3 port of the tenant analytics write path (node hygiene D): fire-and-forget
    /// analytics writes must NOT capture a request-scoped service (and its DbContext) across the
    /// background-task boundary -- the request scope is disposed when the request returns, so a
    /// captured context is a use-after-dispose race. The pattern (proven on central/shared/SSO)
    /// is: resolve a FRESH service from a new DI scope inside the background task
    /// (<see cref="ScopedBackgroundWork"/>, extracted to SharedServices so this tier can use it),
    /// and let LogRequest AWAIT its write because the caller already backgrounded it.
    /// </summary>
    public class ScopedBackgroundAnalyticsTests
    {
        private const int WaitMs = 5000;

        /// <summary>Scoped probe recording which instance ran the background work.</summary>
        private sealed class ScopedProbe
        {
            public static readonly ConcurrentBag<ScopedProbe> Executed = new ConcurrentBag<ScopedProbe>();
        }

        [Fact]
        public async Task FireAndForget_RunsOnAFreshScope_EvenAfterTheCallerScopeIsDisposed()
        {
            var services = new ServiceCollection();
            services.AddScoped<ScopedProbe>();
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var factory = provider.GetRequiredService<IServiceScopeFactory>();

            ScopedProbe requestScoped;
            var done = new TaskCompletionSource<ScopedProbe>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (IServiceScope requestScope = provider.CreateScope())
            {
                requestScoped = requestScope.ServiceProvider.GetRequiredService<ScopedProbe>();
                // The request returns (scope disposes) before the background work necessarily runs.
            }

            ScopedBackgroundWork.FireAndForget<ScopedProbe>(
                factory,
                probe => { done.TrySetResult(probe); return Task.CompletedTask; });

            ScopedProbe backgroundInstance = await done.Task.WaitAsync(TimeSpan.FromMilliseconds(WaitMs));
            backgroundInstance.Should().NotBeSameAs(requestScoped,
                "the background work must own a FRESH scope, never the (already disposed) request scope's instance");
        }

        [Fact]
        public async Task FireAndForget_WithoutAScopeFactory_RunsTheLegacyFallback()
        {
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            ScopedBackgroundWork.FireAndForget<ScopedProbe>(
                null,
                probe => Task.CompletedTask,
                () => { done.TrySetResult(true); return Task.CompletedTask; });

            (await done.Task.WaitAsync(TimeSpan.FromMilliseconds(WaitMs))).Should().BeTrue(
                "the legacy self-newing constructor path must keep its pre-fix behavior (strangler-safe)");
        }

        [Fact]
        public async Task ModuleDownload_LogRequest_AwaitsTheWrite_InsteadOfDetachingIt()
        {
            var accessor = new Mock<IHttpContextAccessor>();
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.Id = "test-connection"; // SetUpBackgroundInfo calls Connection.Id.ToString()
            accessor.Setup(a => a.HttpContext).Returns(httpContext);
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>() { ["UsingRevProxy"] = "true" })
                .Build();
            var queries = new Mock<IModuleDownloadAnalyticsQueries>();
            queries.Setup(q => q.Create(It.IsAny<ModuleDownloadAnalytics>())).ReturnsAsync(true);

            // The three contentmgmt mocks are gone with the constructor parameters (teardown 1.15).
            // They were never read by anything: assigned in four constructors and dereferenced in
            // no method body, so removing them is behaviour-preserving.
            var logic = new ModuleDownloadAnalyticsLogic(
                accessor.Object,
                config,
                queries.Object);

            await logic.LogRequest(null, new Hardware() { Id = 7, UUID = Guid.NewGuid() }, new Module() { Id = 3, UUID = Guid.NewGuid() });

            // Pre-fix this was Task.Run(() => Create(output)) -- the row was NOT guaranteed by
            // return, and the detached task raced the owning scope's DbContext. LogRequest is
            // always invoked backgrounded by its callers now, so it must await its own write.
            queries.Verify(q => q.Create(It.IsAny<ModuleDownloadAnalytics>()), Times.Once);
        }

        [Fact]
        public async Task ModuleUsage_LogRequest_AwaitsTheWrite_InsteadOfDetachingIt()
        {
            var accessor = new Mock<IHttpContextAccessor>();
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.Id = "test-connection"; // SetUpBackgroundInfo calls Connection.Id.ToString()
            accessor.Setup(a => a.HttpContext).Returns(httpContext);
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>() { ["UsingRevProxy"] = "true" })
                .Build();
            var queries = new Mock<IModuleUsageAnalyticsQueries>();
            queries.Setup(q => q.Create(It.IsAny<ModuleUsageAnalytics>())).ReturnsAsync(true);

            var logic = new ModuleUsageAnalyticsLogic(
                accessor.Object,
                config,
                queries.Object);

            await logic.LogRequest(null, new Hardware() { Id = 7, UUID = Guid.NewGuid() }, new Module() { Id = 3, UUID = Guid.NewGuid() });

            queries.Verify(q => q.Create(It.IsAny<ModuleUsageAnalytics>()), Times.Once);
        }

        [Fact]
        public async Task Download_RecordsAnalytics_OnItsOwnScope_NotTheRequestScopedInstance()
        {
            // Local link store: an entitled hardware-module pair.
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(nameof(Download_RecordsAnalytics_OnItsOwnScope_NotTheRequestScopedInstance))
                .Options;
            using DataDbContext context = new DataDbContext(options);
            var module = new Module() { UUID = Guid.NewGuid(), Name = "Welding 101" };
            var hardwareRow = new LocalHardware() { UUID = Guid.NewGuid() };
            context.Module.Add(module);
            context.Hardware.Add(hardwareRow);
            context.SaveChanges();
            context.HardwareLinkedModule.Add(new LocalHardwareLinkedModule()
            {
                UUID = Guid.NewGuid(),
                Hardware = hardwareRow,
                HardwareUUID = hardwareRow.UUID,
                ModuleId = module.Id,
                ModuleUUID = module.UUID
            });
            context.SaveChanges();

            // Container-owned scoped analytics logic: every scope resolution mints a recording
            // fake, so we can see WHICH instance (and hence which scope) performed the write.
            var resolved = new ConcurrentBag<IModuleDownloadAnalyticsLogic>();
            var logged = new TaskCompletionSource<IModuleDownloadAnalyticsLogic>(TaskCreationOptions.RunContinuationsAsynchronously);
            var services = new ServiceCollection();
            services.AddScoped<IModuleDownloadAnalyticsLogic>(sp =>
            {
                var mock = new Mock<IModuleDownloadAnalyticsLogic>();
                IModuleDownloadAnalyticsLogic self = null;
                mock.Setup(l => l.LogRequest(It.IsAny<ClaimsPrincipal>(), It.IsAny<Hardware>(), It.IsAny<Module>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() => logged.TrySetResult(self));
                self = mock.Object;
                resolved.Add(self);
                return self;
            });
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            // IModuleFileHandler.Download returns a FileStream, so serve a real temp file.
            string tempFile = Path.Combine(Path.GetTempPath(), "febris-scba-test-" + Guid.NewGuid().ToString("N") + ".zip");
            File.WriteAllBytes(tempFile, new byte[] { 1, 2, 3 });
            var fileHandler = new Mock<IModuleFileHandler>();
            fileHandler.Setup(f => f.Download(It.IsAny<Module>()))
                .Returns(() => Task.FromResult(File.OpenRead(tempFile)));
            // The REQUEST-scoped analytics instance the logic holds: strict -- the port's whole
            // point is that the background write must never touch it.
            var requestScopedAnalytics = new Mock<IModuleDownloadAnalyticsLogic>(MockBehavior.Strict);

            var logic = new HardwareLinkedModuleLogic(
                accessor.Object,
                new HardwareLinkedModuleQueries(context),
                fileHandler.Object,
                new Mock<IHardwareQueries>().Object,
                new ModuleQueries(context),
                requestScopedAnalytics.Object,
                new Mock<Febris.UserNode.DataAccessLayer.Queries.DataQueries.IPackageArtifactQueries>().Object,
                null, // no storage seam -> legacy file handler serves the bytes
                provider.GetRequiredService<IServiceScopeFactory>());

            using Stream stream = await logic.Download(new Hardware() { Id = hardwareRow.Id }, module);
            stream.Should().NotBeNull();

            IModuleDownloadAnalyticsLogic backgroundInstance = await logged.Task.WaitAsync(TimeSpan.FromMilliseconds(WaitMs));
            backgroundInstance.Should().NotBeSameAs(requestScopedAnalytics.Object,
                "the download-analytics write must run on a service resolved from a FRESH scope");
            resolved.Should().Contain(backgroundInstance, "the background instance must come from the DI container's own scope");
            requestScopedAnalytics.Verify(l => l.LogRequest(It.IsAny<ClaimsPrincipal>(), It.IsAny<Hardware>(), It.IsAny<Module>()), Times.Never,
                "the request-scoped instance (and its DbContext) must not be captured across the fire-and-forget boundary");
        }
    }
}
