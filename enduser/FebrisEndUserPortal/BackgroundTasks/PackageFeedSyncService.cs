// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading;
using System.Threading.Tasks;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Febris.UserNode.Portal.BackgroundTasks
{
    /// <summary>
    /// Syncs the software catalogue from a package feed on an interval.
    ///
    /// <para>
    /// WHY THIS EXISTS. The catalogue used to be fillable two ways: an operator uploading a zip
    /// through the portal, or an operator running a feed sync by hand. The upload path was removed
    /// (packages now arrive only through a feed whose checksums are verified), which left feed sync
    /// as the ONLY ingest path, and nothing triggered it. A node nobody remembered to sync would
    /// hold nothing, and a node holding nothing cannot serve devices: the Mobile Server fetches the
    /// Companion FROM the node, so headsets could never be updated.
    /// </para>
    ///
    /// <para>
    /// WHY A BACKGROUND SERVICE RATHER THAN AN API ROUTE. ROADMAP 16 deleted the NodeAdmin bearer
    /// token and moved the admin writes behind the portal's cookie identity, on the ruling that no
    /// API-side write credential should exist. An external scheduler would need one back. A service
    /// inside the node needs none: it fetches a public HTTPS manifest anonymously, exposes no
    /// inbound route, and adds no trust surface. The security win survives, and the friction does
    /// not.
    /// </para>
    ///
    /// <para>
    /// OFF BY DEFAULT. With no <c>PackageFeed:Url</c> configured this service starts, logs that it
    /// is idle, and does nothing for the life of the process. An air-gapped node is unaffected, and
    /// so is any operator who prefers to keep running syncs by hand from the portal, which still
    /// works exactly as before.
    /// </para>
    ///
    /// <para>
    /// It lives on the Portal because the Portal owns the sync surface (the form, the report, and
    /// the OrgAdmins gate that guards them). Putting the automatic run beside the manual one keeps
    /// one ingest path rather than two implementations that can drift.
    /// </para>
    /// </summary>
    public class PackageFeedSyncService : BackgroundService
    {
        // A feed changes only when a release ships, so this is a safety net rather than a poll.
        // Below one hour is refused: it would hammer a public host for no benefit.
        private static readonly TimeSpan MinimumInterval = TimeSpan.FromHours(1);

        // Let the host finish starting before the first run. Migrations and seeding happen at
        // boot, and a sync racing them would log confusing failures on a fresh node.
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PackageFeedOptions _options;
        private readonly ILogger<PackageFeedSyncService> _logger;

        public PackageFeedSyncService(
            IServiceScopeFactory scopeFactory,
            IOptions<PackageFeedOptions> options,
            ILogger<PackageFeedSyncService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options?.Value ?? new PackageFeedOptions();
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_options.Url))
            {
                // Stated once at boot rather than silently. An operator wondering why nothing
                // syncs should find the answer in the log without reading source.
                _logger.LogInformation(
                    "PackageFeedSyncService: idle. No PackageFeed:Url is configured, so the software "
                    + "catalogue will only change when an administrator runs a sync from the portal.");
                return;
            }

            TimeSpan interval = TimeSpan.FromHours(_options.IntervalHours);
            if (interval < MinimumInterval)
            {
                _logger.LogWarning(
                    "PackageFeedSyncService: PackageFeed:IntervalHours of {Configured} is below the "
                    + "one hour minimum; using one hour.", _options.IntervalHours);
                interval = MinimumInterval;
            }

            _logger.LogInformation(
                "PackageFeedSyncService: syncing {Url} (channel {Channel}) every {Hours} hour(s).",
                _options.Url, _options.Channel, interval.TotalHours);

            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnce(stoppingToken);

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task RunOnce(CancellationToken stoppingToken)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IPackageFeedSyncLogic sync =
                    scope.ServiceProvider.GetRequiredService<IPackageFeedSyncLogic>();

                PackageFeedSyncResultViewModel result = await sync.SyncFromFeed(
                    new PackageFeedSyncRequestViewModel
                    {
                        ManifestUrl = _options.Url,
                        Channel = _options.Channel,
                        // Never a dry run: a scheduled run that reports and changes nothing would
                        // be a silent no-op forever. The manual portal form keeps the dry-run box
                        // for the operator who wants to look before committing.
                        DryRun = false,
                    });

                // Log at Information only when something actually changed, so a healthy node that
                // is already current does not fill its log with identical lines every interval.
                if (result.Ingested > 0 || result.Refused > 0 || result.Failed > 0)
                {
                    _logger.LogInformation(
                        "PackageFeedSyncService: {Ingested} ingested, {Current} already current, "
                        + "{Filtered} filtered, {Refused} refused, {Failed} failed.",
                        result.Ingested, result.AlreadyCurrent, result.Filtered,
                        result.Refused, result.Failed);
                }
                else
                {
                    _logger.LogDebug(
                        "PackageFeedSyncService: no change ({Current} already current, {Filtered} filtered).",
                        result.AlreadyCurrent, result.Filtered);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down, not a failure.
            }
            catch (Exception ex)
            {
                // A feed that is unreachable, malformed, or serving a bad checksum must never take
                // the host down. Log it and try again next interval, matching VideoRetentionService.
                _logger.LogError(ex, "PackageFeedSyncService: sync run failed against {Url}.", _options.Url);
            }
        }
    }

    /// <summary>
    /// Operator configuration for the scheduled sync, bound from the
    /// "<see cref="SectionName"/>" appsettings section. Absent section means no automatic sync.
    /// </summary>
    public class PackageFeedOptions
    {
        /// <summary>The configuration section these options bind from.</summary>
        public const string SectionName = "PackageFeed";

        /// <summary>
        /// HTTPS URL of the manifest to sync from. BLANK BY DEFAULT: a node does not reach out to
        /// anything until an operator names a feed they trust. The sync itself enforces https.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Release channel to accept. Entries on other channels are filtered.</summary>
        public string Channel { get; set; } = "stable";

        /// <summary>Hours between runs. Values below one are raised to one.</summary>
        public int IntervalHours { get; set; } = 24;
    }
}
