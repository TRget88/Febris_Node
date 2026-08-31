// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Febris.UserNode.LogicLayer.Logic.HealthLogic
{
    /// <summary>
    /// Readiness check that the database's SCHEMA is actually usable, not merely reachable.
    ///
    /// <para>
    /// T11. <see cref="DbContextHealthCheck{TContext}"/> probes
    /// <c>Database.CanConnectAsync</c>, which is exactly right for liveness and completely blind to
    /// this failure: a connectable PostgreSQL database with ZERO tables answers it "database
    /// reachable" and readiness goes green. Combined with two other behaviours on this node, that
    /// produced a node that took production traffic while unable to serve a single request:
    /// </para>
    ///
    /// <code>
    /// migrations fail   -> EndUserDatabaseProvisioner catches EVERY exception and writes one line
    ///                      to Console.Error, then lets startup continue
    ///                   -> DbContextHealthCheck still says "database reachable"
    ///                   -> /health/ready returns green
    ///                   -> the docker-compose healthcheck passes
    ///                   -> depends_on: condition: service_healthy releases the reverse proxy
    ///                   -> traffic is routed to a node whose schema was never created
    /// </code>
    ///
    /// <para>
    /// Pending migrations are the honest signal for that. A database that was never migrated reports
    /// its entire chain as pending, and a partially applied one reports the remainder, so both the
    /// never-ran and the half-ran case go red instead of green.
    /// </para>
    ///
    /// <para>
    /// <b>Only for migration-managed contexts.</b> <c>AnalyticsDbContext</c> is provisioned with
    /// <c>EnsureCreated()</c>, which builds the schema straight from the model and writes no
    /// <c>__EFMigrationsHistory</c>. It nonetheless HAS a migration chain, so asking it for pending
    /// migrations would report all of them forever and pin readiness red on a perfectly working
    /// node. That mismatch is a real defect in its own right and is recorded in docs/BUGS.md, but it
    /// is not this check's job to fail on it.
    /// </para>
    /// </summary>
    public sealed class DbContextSchemaHealthCheck<TContext> : IHealthCheck
        where TContext : DbContext
    {
        /// <summary>Matches the connectivity probe's budget: readiness must answer quickly.</summary>
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

        private readonly TContext _context;

        public DbContextSchemaHealthCheck(TContext context)
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

                IEnumerable<string> pending = await _context.Database.GetPendingMigrationsAsync(cts.Token);
                List<string> pendingList = pending == null ? new List<string>() : pending.ToList();

                if (pendingList.Count == 0)
                {
                    return HealthCheckResult.Healthy("schema up to date");
                }

                // Migration NAMES are safe to report: they are compile-time constants in the repo,
                // not data. Connection details never appear here, matching the sibling check's rule
                // for an endpoint that answers anonymously.
                return new HealthCheckResult(
                    context.Registration.FailureStatus,
                    "schema is not up to date, " + pendingList.Count + " migration(s) pending: "
                        + string.Join(", ", pendingList.Take(3))
                        + (pendingList.Count > 3 ? ", ..." : string.Empty));
            }
            catch (OperationCanceledException)
            {
                return new HealthCheckResult(context.Registration.FailureStatus,
                    "schema probe timed out after " + ProbeTimeout.TotalSeconds + "s");
            }
            catch (Exception ex)
            {
                // Type name only, same reasoning as the connectivity probe: no secrets on an
                // anonymous endpoint. An unreachable database lands here too, and reporting it as
                // not-ready is correct.
                return new HealthCheckResult(context.Registration.FailureStatus,
                    "schema probe failed (" + ex.GetType().Name + ")");
            }
        }
    }
}
