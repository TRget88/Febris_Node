// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading;
using System.Threading.Tasks;
using Febris.UserNode.LogicLayer.Logic.AnalyticsLogic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Febris.UserNode.Portal.BackgroundTasks
{
    /// <summary>
    /// Runs <see cref="IAnalyticsRetentionReaper"/> daily, resolving the scoped reaper in a fresh
    /// scope each run. Modelled on <c>VideoRetentionService</c> and <c>SoftDeletedUserPurgeService</c>
    /// down to the fail-safe posture: a run that fails is logged and retried next interval rather
    /// than crashing the host.
    ///
    /// <para>
    /// <b>PORTAL ONLY, and that is load-bearing.</b> BOTH hosts write analytics into the SAME
    /// database, so registering this on both would have two processes deleting from one table on
    /// overlapping schedules, doing duplicate work and contending for locks. The Portal owns the
    /// analytics SCREENS, so the host that reads the data is the host that bounds it. This mirrors
    /// how retention is already split by domain: video retention runs on the API, which writes
    /// video, and account purging runs on the Portal.
    /// </para>
    ///
    /// <para>
    /// The consequence to know: a deployment running the API WITHOUT the Portal never trims
    /// analytics. That is recorded in docs/BUGS.md rather than solved by double registration, which
    /// would be worse.
    /// </para>
    /// </summary>
    public class AnalyticsRetentionService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AnalyticsRetentionService> _logger;

        public AnalyticsRetentionService(IServiceScopeFactory scopeFactory, ILogger<AnalyticsRetentionService> logger)
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
                    IAnalyticsRetentionReaper reaper =
                        scope.ServiceProvider.GetRequiredService<IAnalyticsRetentionReaper>();

                    DateTime nowUtc = DateTime.UtcNow;

                    // Anonymisation FIRST. If the purge hits its per-run ceiling on a long-lived
                    // table, the launch rows have still had their identifiers cleared this pass,
                    // rather than waiting behind a delete that may take several days to catch up.
                    await reaper.AnonymiseOldLaunchAnalyticsAsync(nowUtc, stoppingToken);
                    await reaper.ReapExpiredRequestAnalyticsAsync(nowUtc, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // A retention run must never crash the host; log and try again next interval.
                    _logger.LogError(ex, "AnalyticsRetentionService: retention run failed.");
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
