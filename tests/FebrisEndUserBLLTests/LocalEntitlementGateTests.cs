// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Febris.ModelLibrary.LauncherModels;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.LauncherLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using XM = Febris.ModelLibrary.Models.XApiModels;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the LOCAL entitlement gate (delivery-path severance / MDM-B1): a
    /// launch is authorized by the tenant's own HardwareLinkedModule link -- the same authority
    /// the package-download gate already used -- instead of the central SeatCheck round-trip.
    /// The old path dereferenced a never-assigned _purchaseContext (guaranteed NPE the moment a
    /// launch reached it); these tests drive the previously-fatal statement-initialization path
    /// end to end, entitled and unentitled, through the greedy DI constructor.
    /// </summary>
    public class LocalEntitlementGateTests
    {
        private const string DummyConnectionString = "Host=localhost;Database=x;Username=x;Password=x";

        private static DataDbContext BuildDataContext(string dbName)
        {
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new DataDbContext(options);
        }

        /// <summary>
        /// Build a LauncherLogic through its greedy DI ctor: real InMemory-backed catalog +
        /// entitlement queries (the gate under test), mocks for the xAPI lookups and the
        /// analytics/statement collaborators, and the request Hardware in HttpContext.Items
        /// exactly as the JwtHardwareMiddleware stores it.
        /// </summary>
        private static LauncherLogic BuildLauncherLogic(DataDbContext context, Hardware requestHardware, Guid objectUuid)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items["Hardware"] = requestHardware;
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(httpContext);

            var actorQueries = new Mock<Febris.UserNode.DataAccessLayer.Queries.XAPIQueries.IActorQueries>();
            actorQueries.Setup(a => a.Get(It.IsAny<Guid>()))
                .ReturnsAsync(new XM.Actor() { UUID = Guid.NewGuid() });

            var objectQueries = new Mock<Febris.UserNode.DataAccessLayer.Queries.XApiQueries.IObjectQueries>();
            objectQueries.Setup(o => o.Get(It.IsAny<long>()))
                .ReturnsAsync(new XM.Object() { UUID = objectUuid, ObjectType = "Activity" });

            var verbQueries = new Mock<Febris.UserNode.DataAccessLayer.Queries.XApiQueries.IVerbQueries>();
            verbQueries.Setup(v => v.Get(It.IsAny<Uri>()))
                .ReturnsAsync(new XM.Verb() { Id = new Uri("https://febr.is/Verb/Details/Initialized") });

            return new LauncherLogic(
                accessor.Object,
                new HardwareQueries(context),
                new HardwareLinkedModuleQueries(context),
                new Mock<IMessageBoardQueries>().Object,
                new Mock<Febris.UserNode.DataAccessLayer.Queries.UserQueries.IUserQueries>().Object,
                new ModuleQueries(context),
                new ModuleLinkedObjectQueries(context),
                actorQueries.Object,
                objectQueries.Object,
                verbQueries.Object,
                new Mock<IHardwareLinkedCohortQueries>().Object,
                new Mock<PrimaryLogicLayer.Logic.XApiLogic.IStatementLogic>().Object,
                new Mock<ICohortMemberQueries>().Object,
                new Mock<Logic.AnalyticsLogic.IModuleUsageAnalyticsLogic>().Object,
                // Video-ownership recorder. A mock rather than null: LauncherLogic guards the call,
                // but supplying it keeps this construction honest about the greedy ctor's shape,
                // which is what MS.DI resolves against in both hosts.
                new Mock<Logic.DataLogic.IRecordingLogic>().Object,
                // SCBA-B3 port (hygiene D): null scope factory -> ScopedBackgroundWork's legacy
                // fallback runs the analytics write against the mock above; the entitlement path
                // under test is unaffected.
                null);
        }

        /// <summary>Seed a module + its activity link, returning the module row.</summary>
        private static Module SeedModule(DataDbContext context)
        {
            var module = new Module()
            {
                UUID = Guid.NewGuid(),
                Name = "Lockout Tagout",
                ModuleClassification = new ModuleClassification() { UUID = Guid.NewGuid(), Name = "Training" }
            };
            context.Module.Add(module);
            context.ModuleLinkedObject.Add(new ModuleLinkedObject()
            {
                UUID = Guid.NewGuid(),
                Module = module,
                ModuleUUID = module.UUID,
                ObjectId = 5,
                ObjectUUID = Guid.NewGuid()
            });
            context.SaveChanges();
            return module;
        }

        [Fact]
        public async Task EntitledHardware_InitializesTheStatement_WithZeroCommerce()
        {
            using DataDbContext context = BuildDataContext(nameof(EntitledHardware_InitializesTheStatement_WithZeroCommerce));
            Module module = SeedModule(context);
            var hardwareRow = new LocalHardware() { UUID = Guid.NewGuid() };
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
            LauncherLogic logic = BuildLauncherLogic(context, new Hardware() { Id = hardwareRow.Id }, Guid.NewGuid());

            // This exact call previously threw NullReferenceException at the SeatCheck line
            // (MDM-B1: _purchaseContext declared-never-assigned). It now completes locally.
            StatementInitalizationResponseViewModel response = await logic.InitalizeStatement(
                new StatementInitalizationRequestViewModel() { ActorId = Guid.NewGuid(), ModuleId = module.UUID });

            response.Should().NotBeNull("an entitled hardware must be able to launch with zero HTTP and zero commerce");
            response.Statement.Should().NotBeNull();
            response.Statement.Verb.Id.ToString().Should().Contain("Initialized");
        }

        [Fact]
        public async Task UnentitledHardware_IsRefused()
        {
            using DataDbContext context = BuildDataContext(nameof(UnentitledHardware_IsRefused));
            Module module = SeedModule(context);
            var hardwareRow = new LocalHardware() { UUID = Guid.NewGuid() };
            context.Hardware.Add(hardwareRow);
            context.SaveChanges();
            // NO HardwareLinkedModule link for this hardware.
            LauncherLogic logic = BuildLauncherLogic(context, new Hardware() { Id = hardwareRow.Id }, Guid.NewGuid());

            StatementInitalizationResponseViewModel response = await logic.InitalizeStatement(
                new StatementInitalizationRequestViewModel() { ActorId = Guid.NewGuid(), ModuleId = module.UUID });

            response.Should().BeNull("no local HardwareLinkedModule link means no launch");
        }

        [Fact]
        public async Task NoHardwareOnTheRequest_FailsClosed()
        {
            using DataDbContext context = BuildDataContext(nameof(NoHardwareOnTheRequest_FailsClosed));
            Module module = SeedModule(context);
            LauncherLogic logic = BuildLauncherLogic(context, requestHardware: null, Guid.NewGuid());

            StatementInitalizationResponseViewModel response = await logic.InitalizeStatement(
                new StatementInitalizationRequestViewModel() { ActorId = Guid.NewGuid(), ModuleId = module.UUID });

            response.Should().BeNull("a request with no hardware identity must not be entitled to anything");
        }

        [Fact]
        public void LauncherLogic_ResolvesThroughTheGreedyDiCtor_NotTheLegacySelfNewingOne()
        {
            // DI-reachability proof, mirroring the StatementLogic greedy-ctor test: the container
            // carries a MARKER IModuleQueries; only the greedy DI ctor receives it (the legacy
            // accessor-only ctor news its own ModuleQueries), so the private field must be the
            // marker. Collaborators whose legacy constructors reach into static config
            // (UserQueries/ApplicationDb, the Remote messageboard client, the analytics logic) are
            // pre-registered as mocks BEFORE the convention sweep -- TryAddScoped leaves explicit
            // registrations in place, exactly as a host could.
            var settings = new Dictionary<string, string>
            {
                ["ConnectionStrings:DataDBConnection"] = DummyConnectionString,
                ["ConnectionStrings:XAPIDBConnection"] = DummyConnectionString,
                ["ConnectionStrings:AnalyticsDBConnection"] = DummyConnectionString,
            };
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            var markerModuleQueries = new Mock<IModuleQueries>().Object;

            using ServiceProvider provider = new ServiceCollection()
                .AddSingleton(accessor.Object)
                .AddSingleton(markerModuleQueries)
                .AddSingleton(new Mock<Febris.UserNode.DataAccessLayer.Queries.UserQueries.IUserQueries>().Object)
                .AddSingleton(new Mock<PrimaryLogicLayer.Logic.XApiLogic.IStatementLogic>().Object)
                .AddSingleton(new Mock<Logic.AnalyticsLogic.IModuleUsageAnalyticsLogic>().Object)
                // Video-ownership recorder: another non-*Queries collaborator the convention sweep
                // does not cover, so it must be registered explicitly here exactly as both hosts
                // do. This test earned its keep by catching that omission: without it the greedy
                // ctor was unresolvable and MS.DI silently selected the legacy self-newing ctor,
                // where the recorder is null and video ownership is never written -- which would
                // have left the Portal entitlement check with nothing to check against.
                .AddSingleton(new Mock<Logic.DataLogic.IRecordingLogic>().Object)
                .AddFebrisUserNodeDataAccess(config)
                .AddScoped<ILauncherLogic, LauncherLogic>()
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using IServiceScope scope = provider.CreateScope();

            var logic = scope.ServiceProvider.GetRequiredService<ILauncherLogic>();

            var field = typeof(LauncherLogic).GetField("_moduleContext",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.GetValue(logic).Should().BeSameAs(markerModuleQueries,
                "the greedy DI ctor must be selected; the legacy ctor would have newed its own ModuleQueries");
        }
    }
}
