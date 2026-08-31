// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading;
using System.Threading.Tasks;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.Queries.AnalyticsQueries;
using Microsoft.Extensions.Configuration;

namespace Febris.UserNode.LogicLayer.Logic.AnalyticsLogic
{
    public interface IAnalyticsRetentionReaper
    {
        /// <summary>Deletes request-analytics rows past the retention window. Returns rows removed.</summary>
        Task<int> ReapExpiredRequestAnalyticsAsync(DateTime nowUtc, CancellationToken cancellationToken);

        /// <summary>Clears per-request identifiers on old module-launch rows. Returns rows changed.</summary>
        Task<int> AnonymiseOldLaunchAnalyticsAsync(DateTime nowUtc, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Retention for the analytics databases (T11).
    ///
    /// <para>
    /// These tables grow one row per event and had NO retention of any kind. They are not the
    /// learning record, which lives in the xAPI database and is never swept on a timer, but they do
    /// hold per-request personal data about students: IP address, user agent, referer and path, on
    /// rows that are rendered to Org Admins. Finding H-26 is the standing proof that keeping this
    /// forever is actively dangerous rather than merely untidy, so here the failure mode is
    /// RETENTION, not deletion, and the delete job is ON by default.
    /// </para>
    ///
    /// <para>
    /// <b>Three tables, two different treatments, and the difference matters.</b>
    /// </para>
    ///
    /// <list type="bullet">
    /// <item>
    /// <c>LocalAnalytics</c> and <c>ModuleDownloadAnalytics</c> are DELETED past the window. A
    /// request served and a package fetched over the wire are delivery bookkeeping. Neither is the
    /// only record of anything a learner did.
    /// </item>
    /// <item>
    /// <c>ModuleUsageAnalytics</c> is NEVER deleted, only ANONYMISED. <c>LauncherLogic</c> does not
    /// persist the launch statement, so for a learner who launches a module and never completes it,
    /// that row is the only record this node holds that they engaged with it at all. Sweeping it
    /// would silently delete a student record, which is the precise failure this audit exists to
    /// prevent. Clearing the four per-request columns removes the privacy liability and keeps the
    /// fact of the launch, its user and its module.
    /// </item>
    /// </list>
    ///
    /// <para>
    /// <b>Batched, and bounded per run.</b> A single DELETE across a table that has been collecting
    /// a row per HTTP request since installation is a long transaction holding locks. Each pass
    /// removes at most <see cref="MaxRowsPerRun"/> rows in batches, so the first run after an upgrade
    /// on a long-lived node trims steadily over several days instead of stalling the database once.
    /// </para>
    /// </summary>
    public class AnalyticsRetentionReaper : IAnalyticsRetentionReaper
    {
        /// <summary>
        /// Generous on purpose. This is the Portal's only durable per-request access log now that the
        /// Serilog files roll at 90 files, so a year still serves an incident investigation while
        /// bounding the table.
        /// </summary>
        private const int DefaultPurgeAfterDays = 365;

        /// <summary>
        /// Short, because the identifiers are the liability and the row is what has value. The launch
        /// itself is kept forever.
        /// </summary>
        private const int DefaultAnonymiseAfterDays = 90;

        private const int BatchSize = 1000;

        /// <summary>Ceiling per run, so one pass cannot monopolise the database.</summary>
        private const int MaxRowsPerRun = 50000;

        private readonly ILocalAnalyticsQueries _local;
        private readonly IModuleDownloadAnalyticsQueries _downloads;
        private readonly IModuleUsageAnalyticsQueries _usage;
        private readonly int? _purgeAfterDays;
        private readonly int _anonymiseAfterDays;

        public AnalyticsRetentionReaper(
            ILocalAnalyticsQueries local,
            IModuleDownloadAnalyticsQueries downloads,
            IModuleUsageAnalyticsQueries usage,
            IConfiguration config)
        {
            _local = local;
            _downloads = downloads;
            _usage = usage;

            // Unlike VideoRetention:PurgeAfterDays and AccountLifecycle:PurgeAfterDays, this DEFAULTS
            // ON. Those two guard learner records, where deleting because nobody set a value is
            // indefensible. This guards request exhaust, where KEEPING it because nobody set a value
            // is the defect. An operator can still disable it explicitly with 0 or a negative value.
            _purgeAfterDays = config?.GetValue<int?>("AnalyticsRetention:PurgeAfterDays") ?? DefaultPurgeAfterDays;
            _anonymiseAfterDays = config?.GetValue<int?>("AnalyticsRetention:AnonymiseAfterDays") ?? DefaultAnonymiseAfterDays;
        }

        /// <inheritdoc />
        public async Task<int> ReapExpiredRequestAnalyticsAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            int deleted = 0;
            try
            {
                if (!_purgeAfterDays.HasValue || _purgeAfterDays.Value <= 0)
                {
                    FebrisLog.Info("AnalyticsRetention: request-analytics purge is disabled by configuration.");
                    return 0;
                }

                DateTime cutoff = nowUtc.AddDays(-_purgeAfterDays.Value);

                deleted += await PurgeAsync(
                    "LocalAnalytics",
                    (c, b) => _local.DeleteOlderThan(c, b),
                    cutoff, cancellationToken);

                deleted += await PurgeAsync(
                    "ModuleDownloadAnalytics",
                    (c, b) => _downloads.DeleteOlderThan(c, b),
                    cutoff, cancellationToken);

                // Reported even at zero. A silent sweeper is indistinguishable from a broken one,
                // which is a defect family this node has had repeatedly.
                FebrisLog.Info("AnalyticsRetention: deleted " + deleted +
                    " request-analytics row(s) older than " + _purgeAfterDays.Value + " day(s).");
            }
            catch (Exception ex)
            {
                FebrisLog.Error(ex, "AnalyticsRetentionReaper.ReapExpiredRequestAnalyticsAsync");
            }
            return deleted;
        }

        /// <inheritdoc />
        public async Task<int> AnonymiseOldLaunchAnalyticsAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            int changed = 0;
            try
            {
                if (_anonymiseAfterDays <= 0)
                {
                    FebrisLog.Info("AnalyticsRetention: launch-analytics anonymisation is disabled by configuration.");
                    return 0;
                }

                DateTime cutoff = nowUtc.AddDays(-_anonymiseAfterDays);

                while (changed < MaxRowsPerRun)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    int batch = await _usage.AnonymiseOlderThan(cutoff, BatchSize);
                    if (batch <= 0) break;
                    changed += batch;
                }

                FebrisLog.Info("AnalyticsRetention: cleared the request identifiers on " + changed +
                    " module-launch row(s) older than " + _anonymiseAfterDays +
                    " day(s). The launches themselves are kept.");
            }
            catch (Exception ex)
            {
                FebrisLog.Error(ex, "AnalyticsRetentionReaper.AnonymiseOldLaunchAnalyticsAsync");
            }
            return changed;
        }

        /// <summary>
        /// Batched delete loop for one table. A failure on one table is logged and the caller moves
        /// to the next rather than losing the whole run.
        /// </summary>
        private static async Task<int> PurgeAsync(
            string label,
            Func<DateTime, int, Task<int>> deleteOlderThan,
            DateTime cutoff,
            CancellationToken cancellationToken)
        {
            int deleted = 0;
            try
            {
                while (deleted < MaxRowsPerRun)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    int batch = await deleteOlderThan(cutoff, BatchSize);
                    if (batch <= 0) break;
                    deleted += batch;
                }

                if (deleted >= MaxRowsPerRun)
                {
                    // Expected on the first run over a long-lived table, and worth saying out loud
                    // so nobody reads a partial trim as a finished one.
                    FebrisLog.Info("AnalyticsRetention: hit the per-run ceiling on " + label +
                        ", so more rows remain past the window. The next run continues.");
                }
            }
            catch (Exception ex)
            {
                FebrisLog.Error(ex, "AnalyticsRetentionReaper: purging " + label);
            }
            return deleted;
        }
    }
}
