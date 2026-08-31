// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
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
    /// Pins the node's LOCAL single-tenant identity (auth severance, slice 2): provision
    /// seeds exactly one NodeIdentity row (idempotent, stable InstitutionUUID), and the
    /// license-claim-derived institution/settings reads resolve from it when no license is
    /// present -- i.e. with the hub-federation gate closed, no license claim attached, and
    /// zero HTTP.
    /// </summary>
    public class NodeIdentityLocalTests
    {
        private static DataDbContext BuildDataContext(string dbName)
        {
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new DataDbContext(options);
        }

        private static IHttpContextAccessor AccessorWithoutLicense()
        {
            // A plain request context: NO Items["License"] -- the node never attaches one (the
            // scheme-B middleware does not run tenant-side).
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            return accessor.Object;
        }

        #region Provision seeds exactly once

        [Fact]
        public void Seeder_SeedsExactlyOneRow_AndIsIdempotent()
        {
            using DataDbContext context = BuildDataContext(nameof(Seeder_SeedsExactlyOneRow_AndIsIdempotent));

            NodeIdentitySeeder.Seed(context);
            NodeIdentity first = context.NodeIdentity.Single();
            first.InstitutionUUID.Should().NotBeEmpty("the identity is generated at provision time");
            first.Name.Should().Be(NodeIdentitySeeder.DefaultName);

            // Re-provisioning (every host restart) must not add rows or regenerate the identity.
            NodeIdentitySeeder.Seed(context);
            NodeIdentitySeeder.Seed(context);

            NodeIdentity after = context.NodeIdentity.Single();
            after.InstitutionUUID.Should().Be(first.InstitutionUUID,
                "the node's institution identity is provision-once and must never drift");
        }

        [Fact]
        public void Seeder_HonorsConfiguredName_AtProvisionTimeOnly()
        {
            using DataDbContext context = BuildDataContext(nameof(Seeder_HonorsConfiguredName_AtProvisionTimeOnly));
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { ["NodeIdentity:Name"] = "Ward 7 Training Node" })
                .Build();

            NodeIdentitySeeder.Seed(context, config);
            context.NodeIdentity.Single().Name.Should().Be("Ward 7 Training Node");

            // A later config change does not rewrite the provisioned row (seed-once semantics).
            IConfiguration renamed = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { ["NodeIdentity:Name"] = "Renamed" })
                .Build();
            NodeIdentitySeeder.Seed(context, renamed);
            context.NodeIdentity.Single().Name.Should().Be("Ward 7 Training Node");
        }

        #endregion

        #region Settings / institution resolve with no license

        [Fact]
        public async Task InstitutionSettings_ResolveLocally_WithNoLicenseAndNoHub()
        {
            using DataDbContext context = BuildDataContext(nameof(InstitutionSettings_ResolveLocally_WithNoLicenseAndNoHub));
            NodeIdentitySeeder.Seed(context);
            NodeIdentity node = context.NodeIdentity.Single();

            var logic = new InstitutionSettingsLogic(
                AccessorWithoutLicense(),
                new NodeIdentityQueries(context),
                HubFederationSettings.Disabled());

            InstitutionSettings settings = await logic.GetSettings();
            settings.Should().NotBeNull();
            settings.UUID.Should().Be(node.InstitutionUUID,
                "single-tenant: the node's institution identity is the settings scope");

            List<InstitutionSettings> list = await logic.Get();
            list.Should().HaveCount(1);
            (await logic.Get(Guid.NewGuid())).Should().NotBeNull();
        }

        [Fact]
        public async Task Institution_ResolvesLocally_WithNoLicenseAndNoHub()
        {
            using DataDbContext context = BuildDataContext(nameof(Institution_ResolvesLocally_WithNoLicenseAndNoHub));
            NodeIdentitySeeder.Seed(context);
            NodeIdentity node = context.NodeIdentity.Single();

            var logic = new InstitutionLogic(
                AccessorWithoutLicense(),
                new NodeIdentityQueries(context),
                HubFederationSettings.Disabled());

            Institution institution = await logic.GetLocalInstitution();
            institution.UUID.Should().Be(node.InstitutionUUID);
            institution.Name.Should().Be(node.Name);

            List<Institution> list = await logic.Get();
            list.Should().HaveCount(1, "a hub-less node is single-tenant");
            list.Single().UUID.Should().Be(node.InstitutionUUID);
        }

        [Fact]
        public async Task LocalAnswers_DegradeQuietly_OnAnUnprovisionedStore()
        {
            using DataDbContext context = BuildDataContext(nameof(LocalAnswers_DegradeQuietly_OnAnUnprovisionedStore));

            var settingsLogic = new InstitutionSettingsLogic(
                AccessorWithoutLicense(), new NodeIdentityQueries(context), HubFederationSettings.Disabled());
            var institutionLogic = new InstitutionLogic(
                AccessorWithoutLicense(), new NodeIdentityQueries(context), HubFederationSettings.Disabled());

            (await settingsLogic.GetSettings()).Should().NotBeNull();
            (await institutionLogic.GetLocalInstitution()).Should().NotBeNull();
        }

        #endregion

        #region DI

        [Fact]
        public void NodeIdentityQueries_ResolveThroughTheConventionMap_WithTheScopedContext()
        {
            var settings = new Dictionary<string, string>
            {
                ["ConnectionStrings:DataDBConnection"] = "Host=localhost;Database=x;Username=x;Password=x",
            };
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            using ServiceProvider provider = new ServiceCollection()
                .AddFebrisUserNodeDataAccess(config)
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using IServiceScope scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<INodeIdentityQueries>()
                .Should().BeOfType<NodeIdentityQueries>();
        }

        #endregion
    }
}
