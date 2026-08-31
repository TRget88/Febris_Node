// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading;
using System.Threading.Tasks;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Febris.UserNode.Api.BackgroundTasks
{
    /// <summary>
    /// Runs <see cref="IVideoRetentionReaper"/> on a daily cadence, resolving the (scoped) reaper in
    /// a fresh scope each run. Modelled on <c>SoftDeletedUserPurgeService</c> in the Portal, down to
    /// the fail-safe posture: a reap failure is logged and retried next interval rather than
    /// crashing the host.
    ///
    /// <para>
    /// It lives on the API host because that is the host that WRITES video. Putting the reaper
    /// beside the ingest it bounds keeps the two in one deployment unit -- a node running the API
    /// without the Portal would otherwise ingest video with nothing ever reclaiming it.
    /// </para>
    ///
    /// <para>
    /// Deleting finished recordings is OFF unless <c>VideoRetention:PurgeAfterDays</c> is set, so by
    /// default this service only clears abandoned upload fragments and touches no learner record.
    /// </para>
    /// </summary>
    public class VideoRetentionService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<VideoRetentionService> _logger;

        public VideoRetentionService(IServiceScopeFactory scopeFactory, ILogger<VideoRetentionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using IServiceScope scope = _scopeFactory.CreateScope();
                    IVideoRetentionReaper reaper = scope.ServiceProvider.GetRequiredService<IVideoRetentionReaper>();

                    DateTime nowUtc = DateTime.UtcNow;
                    await reaper.ReapAbandonedPartsAsync(nowUtc, stoppingToken);
                    await reaper.ReapExpiredRecordingsAsync(nowUtc, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // A reap run must never crash the host; log and try again next interval.
                    _logger.LogError(ex, "VideoRetentionService: reap run failed.");
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
