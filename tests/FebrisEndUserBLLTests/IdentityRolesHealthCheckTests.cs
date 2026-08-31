// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.UserModels;
using Febris.UserNode.LogicLayer.Logic.HealthLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// The runtime Identity-roles readiness probe (<see cref="IdentityRolesHealthCheck"/>): Healthy when
    /// every required role exists, Unhealthy (naming the gap) when one is missing, and Unhealthy WITHOUT
    /// leaking store internals when the role store throws. Complements the boot-time fail-fast in
    /// <c>SeedData.SeedRolesAsync</c> (which this probe cannot observe -- a mis-seeded node never serves).
    /// </summary>
    public class IdentityRolesHealthCheckTests
    {
        private static HealthCheckContext Context(IHealthCheck check) =>
            new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("identity-roles", check, HealthStatus.Unhealthy, null)
            };

        private static RoleManager<ApplicationRole> RoleManagerWith(Func<string, Task<bool>> roleExists)
        {
            var store = new Mock<IRoleStore<ApplicationRole>>();
            var manager = new Mock<RoleManager<ApplicationRole>>(
                store.Object, null, null, null, null);
            manager.Setup(m => m.RoleExistsAsync(It.IsAny<string>()))
                .Returns((string role) => roleExists(role));
            return manager.Object;
        }

        [Fact]
        public async Task AllRolesPresent_ReportsHealthy()
        {
            var check = new IdentityRolesHealthCheck(RoleManagerWith(_ => Task.FromResult(true)));

            HealthCheckResult result = await check.CheckHealthAsync(Context(check));

            result.Status.Should().Be(HealthStatus.Healthy);
        }

        [Fact]
        public async Task AMissingRole_ReportsUnhealthy_AndNamesIt()
        {
            string missing = NodeIdentityRoles.Required[0];
            var check = new IdentityRolesHealthCheck(
                RoleManagerWith(role => Task.FromResult(role != missing)));

            HealthCheckResult result = await check.CheckHealthAsync(Context(check));

            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Description.Should().Contain(missing);
        }

        [Fact]
        public async Task ThrowingRoleStore_ReportsUnhealthy_WithTypeNameOnly()
        {
            var check = new IdentityRolesHealthCheck(
                RoleManagerWith(_ => throw new InvalidOperationException("cannot reach roledb-secret.internal")));

            HealthCheckResult result = await check.CheckHealthAsync(Context(check));

            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Description.Should().Contain(nameof(InvalidOperationException));
            result.Description.Should().NotContain("roledb-secret.internal");
        }

        [Fact]
        public void RequiredRoles_AreNonEmpty()
        {
            // Guards the single source of truth the boot seed and this probe share.
            NodeIdentityRoles.Required.Should().NotBeEmpty();
        }

        /// <summary>
        /// Pins the 2026-08-01 owner ruling, which until now lived only in a comment.
        ///
        /// <para>
        /// SuperAdmin is a <c>FebrisUserType</c> -- a VENDOR staff role from when hosted support was
        /// offered. A self-hosted node has no vendor, so its top account is the top LOCAL role,
        /// ITAdmin.
        /// </para>
        ///
        /// <para>
        /// This is not cosmetic tidying. Re-adding SuperAdmin to the seed silently changes what
        /// several live gates MEAN, because <c>IsLocalFebrisAdmin()</c> is literally
        /// <c>IsInRole("SuperAdmin")</c> and appears in ten BLL filters. The same coupling has
        /// already caused one real defect in the other direction: <c>UserLogic:842</c> records that
        /// when the bootstrap admin moved to ITAdmin, a literal SuperAdmin check stopped matching
        /// and the node's sole administrator began rendering in the Educator-visible user index.
        /// </para>
        /// </summary>
        [Fact]
        public void RequiredRoles_AreTheLocalInstitutionRolesOnly_AndNeverTheVendorSuperAdmin()
        {
            NodeIdentityRoles.Required.Should().BeEquivalentTo(new[]
            {
                InstitutionUserAccountType.User.ToString(),
                InstitutionUserAccountType.Educator.ToString(),
                InstitutionUserAccountType.Admin.ToString(),
                InstitutionUserAccountType.ITAdmin.ToString(),
                InstitutionUserAccountType.UserParent.ToString()
            });

            NodeIdentityRoles.Required.Should().NotContain(
                FebrisUserType.SuperAdmin.ToString(),
                "SuperAdmin is a vendor staff role, and a self-hosted node has no vendor to grant it to");

            NodeIdentityRoles.Required.Should().Contain(
                InstitutionUserAccountType.ITAdmin.ToString(),
                "ITAdmin is the node's top local role and what the bootstrap admin is seeded as");
        }
    }
}
