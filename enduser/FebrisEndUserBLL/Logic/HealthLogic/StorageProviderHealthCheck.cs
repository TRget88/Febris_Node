// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.SharedServices.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.HealthLogic
{
    /// <summary>
    /// Node health site: artifact-store readiness probe over the live
    /// <see cref="IStorageProvider"/> seam -- the same seam module .zips and
    /// client-software packages ingest/serve through, so a green result means the DISTRIBUTION
    /// path works, not merely that a directory exists.
    /// <para>
    /// Probe shape: write a tiny unique object under the dedicated
    /// <see cref="StorageKeys.HealthProbe(string)"/> prefix, read it back, verify the bytes,
    /// delete it. Slow round-trip =&gt; <see cref="HealthStatus.Degraded"/> (the store works but an
    /// operator should look at it); failed/garbled round-trip =&gt; unhealthy. Descriptions name
    /// the backend kind only -- never the base path, bucket, or endpoint (the health endpoints
    /// are anonymous).
    /// </para>
    /// </summary>
    public sealed class StorageProviderHealthCheck : IHealthCheck
    {
        /// <summary>Round-trips slower than this report Degraded.</summary>
        public static readonly TimeSpan DefaultDegradedThreshold = TimeSpan.FromSeconds(2);

        private readonly IStorageProvider _storage;
        private readonly TimeSpan _degradedThreshold;

        /// <summary>DI constructor: the node's one storage seam. Marked so container activation
        /// never considers the threshold overload below (its TimeSpan is not resolvable).</summary>
        [ActivatorUtilitiesConstructor]
        public StorageProviderHealthCheck(IStorageProvider storage)
            : this(storage, DefaultDegradedThreshold)
        {
        }

        /// <summary>Test seam: same probe with an explicit slow threshold (a zero threshold makes
        /// every successful round-trip report Degraded deterministically).</summary>
        public StorageProviderHealthCheck(IStorageProvider storage, TimeSpan degradedThreshold)
        {
            _storage = storage;
            _degradedThreshold = degradedThreshold;
        }

        /// <inheritdoc />
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            string key = StorageKeys.HealthProbe("probe-" + Guid.NewGuid().ToString("N") + ".bin");
            byte[] payload = Guid.NewGuid().ToByteArray();

            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                using (MemoryStream content = new MemoryStream(payload))
                {
                    await _storage.WriteAsync(key, content);
                }

                byte[] readBack;
                using (Stream stream = await _storage.OpenReadAsync(key))
                using (MemoryStream buffer = new MemoryStream())
                {
                    await stream.CopyToAsync(buffer, cancellationToken);
                    readBack = buffer.ToArray();
                }

                stopwatch.Stop();

                if (!readBack.SequenceEqual(payload))
                {
                    return new HealthCheckResult(context.Registration.FailureStatus,
                        "storage round-trip returned different bytes (" + _storage.Kind + ")");
                }

                if (stopwatch.Elapsed > _degradedThreshold)
                {
                    return HealthCheckResult.Degraded(
                        "storage round-trip slow: " + stopwatch.ElapsedMilliseconds + " ms (" + _storage.Kind + ")");
                }

                return HealthCheckResult.Healthy("storage round-trip ok (" + _storage.Kind + ")");
            }
            catch (Exception ex)
            {
                // Backend kind + exception TYPE only: provider messages can embed the base
                // path / bucket / endpoint, and the health endpoints are anonymous.
                return new HealthCheckResult(context.Registration.FailureStatus,
                    "storage probe failed (" + _storage.Kind + ", " + ex.GetType().Name + ")");
            }
            finally
            {
                await DeleteProbeObjectBestEffort(key);
            }
        }

        /// <summary>Probe cleanup must never turn a completed probe verdict into a throw -- a
        /// leftover probe object is cosmetic, the verdict is the point.</summary>
        private async Task DeleteProbeObjectBestEffort(string key)
        {
            try
            {
                await _storage.DeleteAsync(key);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
        }
    }
}
