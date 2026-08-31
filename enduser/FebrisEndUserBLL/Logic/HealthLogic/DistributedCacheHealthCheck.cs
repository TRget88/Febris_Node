// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.HealthLogic
{
    /// <summary>
    /// Node health site: Redis readiness probe through one of the platform's
    /// existing distributed-cache abstractions (<c>IDistributedHardwareCache</c> /
    /// <c>IDistributedUserCache</c> -- the exact seams the request path caches through), never a
    /// raw connection of its own. Redis is OPTIONAL for a node: the
    /// registration helper only adds this check when the host's <c>RedisConnectionStrings</c>
    /// configuration carries the matching connection string, so an un-configured node has no
    /// Redis check at all rather than a failing one.
    /// <para>
    /// Probe shape: set a tiny unique value with a one-minute expiry, get it back, compare,
    /// best-effort remove. Failure text reports the exception TYPE only (messages can embed the
    /// Redis endpoint, and the health endpoints are anonymous).
    /// </para>
    /// </summary>
    /// <typeparam name="TCache">The cache abstraction to probe (one registration per configured
    /// connection, so a host with separate auth/hardware Redis instances sees them separately).</typeparam>
    public sealed class DistributedCacheHealthCheck<TCache> : IHealthCheck
        where TCache : class, IDistributedCache
    {
        /// <summary>Internal probe budget; an answer slower than this is a failed probe.</summary>
        public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

        private readonly TCache _cache;

        /// <summary>DI constructor (the only one): the same cache singleton the request path uses.</summary>
        public DistributedCacheHealthCheck(TCache cache)
        {
            _cache = cache;
        }

        /// <inheritdoc />
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            string key = "febris:health:probe:" + Guid.NewGuid().ToString("N");
            byte[] payload = Guid.NewGuid().ToByteArray();

            try
            {
                using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(ProbeTimeout);

                await _cache.SetAsync(key, payload, new DistributedCacheEntryOptions()
                {
                    // Self-cleaning even when the Remove below never runs (e.g. probe crash).
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
                }, cts.Token);

                byte[] readBack = await _cache.GetAsync(key, cts.Token);

                await RemoveProbeValueBestEffort(key, cts.Token);

                if (readBack == null || !readBack.SequenceEqual(payload))
                {
                    return new HealthCheckResult(context.Registration.FailureStatus,
                        "cache round-trip returned different bytes");
                }

                return HealthCheckResult.Healthy("cache round-trip ok");
            }
            catch (OperationCanceledException)
            {
                return new HealthCheckResult(context.Registration.FailureStatus,
                    "cache probe timed out after " + ProbeTimeout.TotalSeconds + "s");
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(context.Registration.FailureStatus,
                    "cache probe failed (" + ex.GetType().Name + ")");
            }
        }

        /// <summary>Cleanup must never turn a completed round-trip into a failure verdict (the
        /// probe value also self-expires in a minute).</summary>
        private async Task RemoveProbeValueBestEffort(string key, CancellationToken cancellationToken)
        {
            try
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
        }
    }
}
