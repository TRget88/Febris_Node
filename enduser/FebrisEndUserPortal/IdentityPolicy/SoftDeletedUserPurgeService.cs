// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Febris.UserNode.Portal.IdentityPolicy
{
    /// <summary>
    /// Runs <see cref="ISoftDeletedUserPurger"/> on a daily cadence to enforce
    /// <c>AccountLifecycle.PurgeAfterDays</c>. Resolves the (scoped) purger in a fresh scope each run.
    /// The purger fails safe when PurgeAfterDays is unset, so this is a cheap no-op by default; a purge
    /// failure is logged and retried next interval rather than crashing the host.
    /// </summary>
    public class SoftDeletedUserPurgeService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SoftDeletedUserPurgeService> _logger;

        public SoftDeletedUserPurgeService(IServiceScopeFactory scopeFactory, ILogger<SoftDeletedUserPurgeService> logger)
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
                    ISoftDeletedUserPurger purger = scope.ServiceProvider.GetRequiredService<ISoftDeletedUserPurger>();
                    await purger.PurgeExpiredAsync(DateTimeOffset.UtcNow, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // A purge run must never crash the host; log and try again next interval.
                    _logger.LogError(ex, "SoftDeletedUserPurgeService: purge run failed.");
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
