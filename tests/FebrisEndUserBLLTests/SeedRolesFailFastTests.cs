// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.UserModels;
using Febris.UserNode.LogicLayer.Logic.HealthLogic;
using Febris.UserNode.Portal.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// <c>SeedData.SeedRolesAsync</c> must FAIL LOUD, not swallow. Roles are a hard account-provisioning
    /// prerequisite (<c>UserManager.AddToRoleAsync</c> throws without them), so a seeding failure has to
    /// propagate and abort startup (via Program.Main's guard) rather than let the node boot mis-seeded and
    /// 500 every registration. These pin the four outcomes against a mocked RoleManager.
    /// </summary>
    public class SeedRolesFailFastTests
    {
        private static IServiceProvider ProviderWith(RoleManager<ApplicationRole> roleManager)
        {
            var services = new ServiceCollection();
            services.AddSingleton(roleManager);
            return services.BuildServiceProvider();
        }

        private static Mock<RoleManager<ApplicationRole>> MockRoleManager()
        {
            var store = new Mock<IRoleStore<ApplicationRole>>();
            return new Mock<RoleManager<ApplicationRole>>(store.Object, null, null, null, null);
        }

        [Fact]
        public async Task AllRolesPresent_DoesNotThrow_AndCreatesNothing()
        {
            Mock<RoleManager<ApplicationRole>> mgr = MockRoleManager();
            mgr.Setup(m => m.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

            Func<Task> act = () => SeedData.SeedRolesAsync(ProviderWith(mgr.Object));

            await act.Should().NotThrowAsync();
            mgr.Verify(m => m.CreateAsync(It.IsAny<ApplicationRole>()), Times.Never);
        }

        [Fact]
        public async Task MissingRoles_AreCreated_ThenVerified()
        {
            // First pass: nothing exists -> create each. Verify pass: they now exist -> no throw.
            var created = new HashSet<string>();
            Mock<RoleManager<ApplicationRole>> mgr = MockRoleManager();
            mgr.Setup(m => m.RoleExistsAsync(It.IsAny<string>()))
                .Returns((string r) => Task.FromResult(created.Contains(r)));
            mgr.Setup(m => m.CreateAsync(It.IsAny<ApplicationRole>()))
                .Returns((ApplicationRole role) => { created.Add(role.Name); return Task.FromResult(IdentityResult.Success); });

            Func<Task> act = () => SeedData.SeedRolesAsync(ProviderWith(mgr.Object));

            await act.Should().NotThrowAsync();
            created.Should().BeEquivalentTo(NodeIdentityRoles.Required);
        }

        [Fact]
        public async Task CreateReturnsFailure_Throws_InsteadOfSwallowing()
        {
            Mock<RoleManager<ApplicationRole>> mgr = MockRoleManager();
            mgr.Setup(m => m.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            mgr.Setup(m => m.CreateAsync(It.IsAny<ApplicationRole>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "X", Description = "constraint violation" }));

            Func<Task> act = () => SeedData.SeedRolesAsync(ProviderWith(mgr.Object));

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task RoleStoreThrows_Propagates_InsteadOfSwallowing()
        {
            Mock<RoleManager<ApplicationRole>> mgr = MockRoleManager();
            mgr.Setup(m => m.RoleExistsAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("role store unreachable"));

            Func<Task> act = () => SeedData.SeedRolesAsync(ProviderWith(mgr.Object));

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
