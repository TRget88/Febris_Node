// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.HealthLogic
{
    /// <summary>
    /// Node health site: database readiness probe for ONE of the host's own
    /// DbContexts. A registration exists only for a context the host actually registered in DI
    /// (see <see cref="NodeHealthRegistration.AddNodeHealthChecks"/>) -- a deployment that does
    /// not own a database is simply missing the check, it is never "unhealthy for a database it
    /// doesn't have".
    /// <para>
    /// Probe shape: <c>Database.CanConnectAsync</c> under a short internal timeout. The scoped
    /// <typeparamref name="TContext"/> is injected per evaluation (the health service resolves
    /// each check inside its own DI scope), so this stays on the same DI seam the request path
    /// uses -- no self-newed contexts, no static ops fallback.
    /// </para>
    /// <para>
    /// Failure text deliberately reports only the exception TYPE, never the message: provider
    /// exception messages can embed host names, ports, or full connection strings, and the
    /// health endpoints are anonymous (K8s/Docker probes cannot authenticate).
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The host-owned DbContext to probe.</typeparam>
    public sealed class DbContextHealthCheck<TContext> : IHealthCheck
        where TContext : DbContext
    {
        /// <summary>Internal probe budget: a database that cannot answer a connectivity check in
        /// this window is not ready, whatever the eventual outcome would have been.</summary>
        public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

        private readonly TContext _context;

        /// <summary>DI constructor (the only one): the health service resolves the scoped
        /// context through the same registration the request path uses.</summary>
        public DbContextHealthCheck(TContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(ProbeTimeout);

                bool canConnect = await _context.Database.CanConnectAsync(cts.Token);
                return canConnect
                    ? HealthCheckResult.Healthy("database reachable")
                    : new HealthCheckResult(context.Registration.FailureStatus, "database unreachable");
            }
            catch (OperationCanceledException)
            {
                return new HealthCheckResult(context.Registration.FailureStatus,
                    "database probe timed out after " + ProbeTimeout.TotalSeconds + "s");
            }
            catch (Exception ex)
            {
                // Type name only -- see the class XML doc (no secrets on an anonymous endpoint).
                return new HealthCheckResult(context.Registration.FailureStatus,
                    "database probe failed (" + ex.GetType().Name + ")");
            }
        }
    }
}
