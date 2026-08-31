// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.ViewModels;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.HealthLogic
{
    /// <summary>
    /// Node health site: gate-aware hub reachability probe. Consults the ONE
    /// <see cref="IHubFederationSettings"/> gate first:
    /// <list type="bullet">
    /// <item>Gate CLOSED -- reports <see cref="HealthStatus.Healthy"/> with
    /// <see cref="DisabledDescription"/>. A standalone node is a fully supported deployment
    /// shape; having no hub is NOT a degradation.</item>
    /// <item>Gate OPEN -- a short-timeout HTTP reachability probe of the configured DataApi
    /// base. ANY HTTP response (including 401/404) proves reachability; only no-response
    /// (DNS/refused/timeout) fails the probe.</item>
    /// </list>
    /// <para>
    /// An unreachable hub reports <see cref="HealthStatus.Degraded"/>, deliberately NOT
    /// unhealthy: the node keeps serving its local catalog/ingest paths with the hub down, and an
    /// Unhealthy readiness verdict would make K8s pull a perfectly functional node out of
    /// rotation over an OPTIONAL enrichment link.
    /// </para>
    /// </summary>
    public sealed class HubFederationHealthCheck : IHealthCheck
    {
        /// <summary>The closed-gate description ("hub federation disabled").</summary>
        public const string DisabledDescription = "hub federation disabled";

        /// <summary>The named <see cref="IHttpClientFactory"/> client this probe uses.</summary>
        public const string HttpClientName = "NodeHealth.HubFederation";

        /// <summary>Internal probe budget for the reachability round-trip.</summary>
        public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

        private readonly IHubFederationSettings _federation;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>DI constructor (the only one): the singleton gate plus the framework's
        /// pooled-handler client factory (registered by <c>AddNodeHealthChecks</c>).</summary>
        public HubFederationHealthCheck(IHubFederationSettings federation, IHttpClientFactory httpClientFactory)
        {
            _federation = federation;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (!_federation.Enabled)
            {
                return HealthCheckResult.Healthy(DisabledDescription);
            }

            if (!_federation.CanReachDataApi)
            {
                return HealthCheckResult.Degraded("hub federation enabled but no DataApi endpoint is configured");
            }

            if (!Uri.TryCreate(_federation.DataApi, UriKind.Absolute, out Uri dataApiBase))
            {
                return HealthCheckResult.Degraded("hub federation enabled but the DataApi endpoint is not an absolute URI");
            }

            try
            {
                HttpClient client = _httpClientFactory.CreateClient(HttpClientName);

                using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(ProbeTimeout);

                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, dataApiBase);
                using HttpResponseMessage response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                // Reachability, not correctness: a 401 from the hub still proves the wire works.
                return HealthCheckResult.Healthy("hub reachable (HTTP " + (int)response.StatusCode + ")");
            }
            catch (OperationCanceledException)
            {
                return HealthCheckResult.Degraded("hub unreachable (probe timed out after " + ProbeTimeout.TotalSeconds + "s)");
            }
            catch (Exception ex)
            {
                // Exception TYPE only -- HttpRequestException messages can embed the hub URL,
                // and the health endpoints are anonymous.
                return HealthCheckResult.Degraded("hub unreachable (" + ex.GetType().Name + ")");
            }
        }
    }
}
