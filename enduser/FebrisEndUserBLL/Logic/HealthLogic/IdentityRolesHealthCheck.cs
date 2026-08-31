// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.UserModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Febris.UserNode.LogicLayer.Logic.HealthLogic
{
    /// <summary>
    /// Readiness probe: the Identity roles the node provisions accounts with (<see cref="NodeIdentityRoles"/>)
    /// must exist. Boot seeding (<c>SeedData.SeedRolesAsync</c>) now FAILS startup if any is absent, so this
    /// probe's real job is the RUNTIME window seeding cannot cover -- a role dropped after boot (manual
    /// delete, a migration). Without the role, <c>UserManager.AddToRoleAsync</c> THROWS and every account
    /// creation 500s; this surfaces that on the node status page instead of leaving it a mystery.
    /// </summary>
    public class IdentityRolesHealthCheck : IHealthCheck
    {
        private readonly RoleManager<ApplicationRole> _roleManager;

        public IdentityRolesHealthCheck(RoleManager<ApplicationRole> roleManager)
        {
            _roleManager = roleManager;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var missing = new List<string>();
                foreach (string role in NodeIdentityRoles.Required)
                {
                    if (!await _roleManager.RoleExistsAsync(role))
                    {
                        missing.Add(role);
                    }
                }

                if (missing.Count > 0)
                {
                    return HealthCheckResult.Unhealthy(
                        "Identity roles missing (account creation will fail): " + string.Join(", ", missing));
                }

                return HealthCheckResult.Healthy("All required Identity roles present.");
            }
            catch (Exception ex)
            {
                // The endpoints are anonymous, so surface the type only -- the store's exception message
                // can embed connection details.
                return HealthCheckResult.Unhealthy("Identity role store query failed: " + ex.GetType().Name);
            }
        }
    }
}
