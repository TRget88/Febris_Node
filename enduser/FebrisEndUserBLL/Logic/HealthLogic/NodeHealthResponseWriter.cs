// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.HealthLogic
{
    /// <summary>
    /// Node health site: the JSON body both probe endpoints emit --
    /// <c>{status, totalDurationMs, checks:[{name, status, description, durationMs}]}</c>.
    /// <para>
    /// SECRET DISCIPLINE: the endpoints are anonymous (K8s/Docker probes cannot authenticate), so
    /// nothing configuration-derived may appear in the body. Check-authored descriptions are
    /// passed through (each check in this folder writes only backend kinds / exception type
    /// names), but an entry that carries an EXCEPTION has its description REPLACED: when an
    /// IHealthCheck throws instead of returning, the health service stores the raw exception
    /// message as the entry description, and provider messages routinely embed host names, ports,
    /// or full connection strings. Only the exception's type name survives serialization.
    /// </para>
    /// </summary>
    public static class NodeHealthResponseWriter
    {
        /// <summary>
        /// Serialize <paramref name="report"/> as the node's probe JSON.
        ///
        /// <para>
        /// TERSE BY DEFAULT (2026-08-25). <paramref name="detailed"/> false emits only
        /// <c>{status, totalDurationMs}</c> and OMITS the per-check array. The array names every
        /// registered check, so it tells an unauthenticated caller which databases this node owns,
        /// whether Redis is configured and whether hub federation is on. That is an inventory of
        /// the deployment, and these endpoints are anonymous by necessity.
        /// </para>
        /// <para>
        /// WHY THIS MOVED INTO THE APP. The exposure used to be prevented one layer up, by a
        /// <c>respond 404</c> rule in the BUNDLED Caddy config. That protects only operators who run
        /// OUR proxy. A self-hoster who already runs their own reverse proxy and points it at this
        /// node -- the common setup -- got no protection at all and no warning, because the control
        /// lived in a component they never deployed. A policy that only holds for one deployment
        /// shape is not a policy. The proxy rule stays as defence in depth.
        /// </para>
        /// <para>
        /// The overall <c>status</c> field is retained in BOTH modes on purpose: container
        /// healthchecks and the release smoke gate assert on it, and reducing detail must not cost
        /// the operator the one field that says whether the node is serving.
        /// </para>
        /// </summary>
        public static Task WriteAsync(HttpContext httpContext, HealthReport report, bool detailed)
        {
            httpContext.Response.ContentType = "application/json; charset=utf-8";

            string json = detailed
                ? JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    totalDurationMs = (long)report.TotalDuration.TotalMilliseconds,
                    checks = report.Entries.Select(entry => new
                    {
                        name = entry.Key,
                        status = entry.Value.Status.ToString(),
                        description = SanitizedDescription(entry.Value),
                        durationMs = (long)entry.Value.Duration.TotalMilliseconds
                    }).ToArray()
                })
                : JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    totalDurationMs = (long)report.TotalDuration.TotalMilliseconds
                });

            return httpContext.Response.WriteAsync(json);
        }

        /// <summary>Check-authored descriptions pass through; exception-derived ones are reduced
        /// to the exception type name (see the class doc on secret discipline).</summary>
        private static string SanitizedDescription(HealthReportEntry entry)
        {
            if (entry.Exception != null)
            {
                return "probe threw (" + entry.Exception.GetType().Name + ")";
            }

            return entry.Description;
        }
    }
}
