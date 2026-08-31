// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using Febris.UserNode.DataAccessLayer;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.XAPIQueries;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Container-resolution test for <c>AddFebrisUserNodeDataAccess</c>, the EndUser
    /// ("primary tenant") data-access registration entry point. It proves that one call
    /// registers the three per-tenant <see cref="DbContext"/> types (Data, XApi, Analytics)
    /// as scoped from their IConfiguration connection strings AND auto-registers each
    /// <c>IXxxQueries</c> to its <c>XxxQueries</c> implementation, so a Local query's strangler
    /// DI constructor resolves with the injected per-tenant context.
    /// <para>
    /// Resolution never opens a database connection, so dummy Npgsql connection strings suffice.
    /// The provider is built with <see cref="ServiceProviderOptions.ValidateScopes"/> so a
    /// scoped-from-root captive dependency would fail (the scoped query classes depend on a scoped
    /// DbContext).
    /// </para>
    /// <para>
    /// This GATES the helper. It does NOT mean any host is wired to call it -- that is the
    /// separate, runtime-verified activation step.
    /// </para>
    /// </summary>
    public class FebrisUserNodeDataAccessResolutionTests
    {
        private const string DummyConnectionString = "Host=localhost;Database=x;Username=x;Password=x";

        private static IConfiguration BuildConfiguration()
        {
            // GetConnectionString(name) reads "ConnectionStrings:{name}", matching the keys
            // AddFebrisUserNodeDataAccess reads.
            var settings = new Dictionary<string, string>
            {
                ["ConnectionStrings:DataDBConnection"] = DummyConnectionString,
                ["ConnectionStrings:XAPIDBConnection"] = DummyConnectionString,
                ["ConnectionStrings:AnalyticsDBConnection"] = DummyConnectionString,
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static ServiceProvider BuildProvider()
        {
            return new ServiceCollection()
                .AddFebrisUserNodeDataAccess(BuildConfiguration())
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        [Fact]
        public void PerTenantDbContexts_RegisteredViaAddFebrisUserNodeDataAccess()
        {
            // Assert the three per-tenant contexts are REGISTERED, without constructing them. The
            // EndUser DataDbContext ctor calls Database.EnsureCreated() (a live-DB round-trip on every
            // construction), so a descriptor check proves the registration without needing a database.
            IServiceCollection services = new ServiceCollection().AddFebrisUserNodeDataAccess(BuildConfiguration());

            services.Should().Contain(d => d.ServiceType == typeof(DataDbContext));
            services.Should().Contain(d => d.ServiceType == typeof(XApiDbContext));
            services.Should().Contain(d => d.ServiceType == typeof(AnalyticsDbContext));
        }

        [Fact]
        public void LocalQuery_ResolvesViaStranglerCtor_InjectingPerTenantContext()
        {
            using (ServiceProvider provider = BuildProvider())
            using (IServiceScope scope = provider.CreateScope())
            {
                // ActorQueries (Local, XApi-backed) has a strangler DI ctor that injects
                // XApiDbContext. Resolving it proves the reflection map wired IActorQueries ->
                // ActorQueries and that the per-tenant context flows in. Its ctor reads no static
                // config and it has no IHttpContextAccessor ctor, so it constructs cleanly here.
                scope.ServiceProvider.GetRequiredService<IActorQueries>().Should().NotBeNull();
            }
        }

        [Fact]
        public void StatementLogic_ResolvesThroughTheGreedyDiCtor_NotTheLegacySelfNewingOne()
        {
            // Review finding (vocab-severance review, DI MAJOR): with IStatementFileHandler
            // unregistered, MS.DI silently discards the greedy ctor and falls back to the legacy
            // self-newing one -- the exact degradation the DI seam must not have. The hosts now
            // register IStatementFileHandler (+ IStatementLogic). Proof of ctor selection: the
            // container carries a MARKER handler instance; only the greedy ctor receives it (the
            // legacy ctor news its own StatementFileHandler), so the private field must be the
            // marker.
            var marker = new Moq.Mock<SharedServices.IStatementFileHandler>().Object;
            var accessor = new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new Microsoft.AspNetCore.Http.DefaultHttpContext());

            using ServiceProvider provider = new ServiceCollection()
                .AddFebrisUserNodeDataAccess(BuildConfiguration())
                .AddSingleton(accessor.Object)
                .AddSingleton(marker)
                .AddScoped<PrimaryLogicLayer.Logic.XApiLogic.IStatementLogic, PrimaryLogicLayer.Logic.XApiLogic.StatementLogic>()
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using IServiceScope scope = provider.CreateScope();

            var logic = scope.ServiceProvider.GetRequiredService<PrimaryLogicLayer.Logic.XApiLogic.IStatementLogic>();

            var field = typeof(PrimaryLogicLayer.Logic.XApiLogic.StatementLogic)
                .GetField("_statementFileHandler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.GetValue(logic).Should().BeSameAs(marker,
                "the greedy DI ctor must be selected; the legacy ctor would have newed its own handler");
        }

        [Fact]
        public void ModuleCatalogQueries_ResolveAsLocalDbContextBackedInstances()
        {
            // ModuleQueries and ModuleLinkedObjectQueries were Remote-HTTP
            // clients and are now node-local EF classes over the tenant DataDbContext. Resolving
            // them through the container proves the convention map binds them AND their scoped
            // DataDbContext flows in -- the module catalog resolves through the DI seam with no
            // HTTP dependency (LauncherLogic / ModuleLogic / HardwareLinkedModuleLogic call sites).
            using (ServiceProvider provider = BuildProvider())
            using (IServiceScope scope = provider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<Febris.UserNode.DataAccessLayer.Queries.DataQueries.IModuleQueries>()
                    .Should().BeOfType<Febris.UserNode.DataAccessLayer.Queries.DataQueries.ModuleQueries>();
                scope.ServiceProvider.GetRequiredService<Febris.UserNode.DataAccessLayer.Queries.DataQueries.IModuleLinkedObjectQueries>()
                    .Should().BeOfType<Febris.UserNode.DataAccessLayer.Queries.DataQueries.ModuleLinkedObjectQueries>();
            }
        }

        [Fact]
        public void ArtifactStoreQueries_ResolveAsLocalDbContextBackedInstances()
        {
            // The node's software-package catalog (previously a Remote-HTTP
            // proxy of central) and the new PackageArtifact bookkeeping both resolve through the
            // convention map with the scoped DataDbContext -- the artifact store's DAL surface has
            // no HTTP dependency.
            using (ServiceProvider provider = BuildProvider())
            using (IServiceScope scope = provider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<Febris.UserNode.DataAccessLayer.Queries.DataQueries.ILocalSoftwarePackageQueries>()
                    .Should().BeOfType<Febris.UserNode.DataAccessLayer.Queries.DataQueries.LocalSoftwarePackageQueries>();
                scope.ServiceProvider.GetRequiredService<Febris.UserNode.DataAccessLayer.Queries.DataQueries.IPackageArtifactQueries>()
                    .Should().BeOfType<Febris.UserNode.DataAccessLayer.Queries.DataQueries.PackageArtifactQueries>();
            }
        }

        [Fact]
        public void PackageIngestLogic_ResolvesWithTheObjectBll_SoTheLaunchChainIsWired()
        {
            // ROADMAP 15: the ingest path mints the module's xAPI activity through IObjectLogic.
            // That BLL was registered on the PORTAL only, so without an equivalent registration
            // the API host's ingest endpoint would fail to construct at request time. This pins
            // the resolution graph both hosts must supply: IObjectLogic plus ObjectLogic's own
            // peers (IDefinitionLogic, IModuleLinkedObjectLogic), whose absence would make MS.DI
            // silently select ObjectLogic's legacy self-newing ctor instead.
            var accessor = new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new Microsoft.AspNetCore.Http.DefaultHttpContext());

            using ServiceProvider provider = new ServiceCollection()
                .AddFebrisUserNodeDataAccess(BuildConfiguration())
                .AddSingleton(accessor.Object)
                .AddSingleton(new Moq.Mock<Febris.SharedServices.Storage.IStorageProvider>().Object)
                .AddScoped<PrimaryLogicLayer.Logic.XApiLogic.IDefinitionLogic, PrimaryLogicLayer.Logic.XApiLogic.DefinitionLogic>()
                .AddScoped<LogicLayer.Logic.DataLogic.IModuleLinkedObjectLogic, LogicLayer.Logic.DataLogic.ModuleLinkedObjectLogic>()
                .AddScoped<PrimaryLogicLayer.Logic.XApiLogic.IObjectLogic, PrimaryLogicLayer.Logic.XApiLogic.ObjectLogic>()
                .AddScoped<LogicLayer.Logic.DataLogic.IPackageIngestLogic, LogicLayer.Logic.DataLogic.PackageIngestLogic>()
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using IServiceScope scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<LogicLayer.Logic.DataLogic.IPackageIngestLogic>()
                .Should().NotBeNull("the module-launch chain must resolve on every host that ingests modules");

            // Proof of ctor selection on ObjectLogic: only the greedy DI ctor receives the
            // container's IObjectQueries; the legacy ctor news its own from static config.
            var objectLogic = scope.ServiceProvider.GetRequiredService<PrimaryLogicLayer.Logic.XApiLogic.IObjectLogic>();
            var field = typeof(PrimaryLogicLayer.Logic.XApiLogic.ObjectLogic)
                .GetField("_objectQueries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.GetValue(objectLogic).Should().BeSameAs(
                scope.ServiceProvider.GetRequiredService<DataAccessLayer.Queries.XApiQueries.IObjectQueries>(),
                "the greedy DI ctor must be selected; the legacy ctor would have newed its own queries from static config");
        }

        [Fact]
        public void VocabularyQueries_ResolveAsLocalDbContextBackedInstances()
        {
            // The three vocabulary queries were Remote-HTTP clients and are now
            // node-local EF classes with strangler DI ctors. Resolving them through the container
            // proves the convention map binds them AND their scoped XApiDbContext flows in --
            // vocabulary resolution runs through the DI seam with no HTTP dependency.
            using (ServiceProvider provider = BuildProvider())
            using (IServiceScope scope = provider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<Febris.UserNode.DataAccessLayer.Queries.XApiQueries.IVerbQueries>()
                    .Should().BeOfType<Febris.UserNode.DataAccessLayer.Queries.XApiQueries.VerbQueries>();
                scope.ServiceProvider.GetRequiredService<Febris.UserNode.DataAccessLayer.Queries.XApiQueries.IVersionQueries>()
                    .Should().BeOfType<Febris.UserNode.DataAccessLayer.Queries.XApiQueries.VersionQueries>();
                scope.ServiceProvider.GetRequiredService<Febris.UserNode.DataAccessLayer.Queries.XApiQueries.IObjectQueries>()
                    .Should().BeOfType<Febris.UserNode.DataAccessLayer.Queries.XApiQueries.ObjectQueries>();
            }
        }
    }
}
