// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    /// <summary>
    /// Reaps video storage. Two jobs with deliberately different defaults, because they delete
    /// materially different things.
    ///
    /// <para>
    /// <b>Abandoned parts</b> are fragments of uploads that never completed, sitting in
    /// <c>SplitVideos/</c>. They are not learner records: a part is only useful as input to a merge
    /// that has not happened, and the producers re-send a whole recording from part 1 on every poll
    /// rather than resuming, so a stale part is never the thing a retry needs. This job is ON by
    /// default with a conservative 7-day age.
    /// </para>
    ///
    /// <para>
    /// <b>Finished recordings</b> ARE learner records. This job is OFF unless
    /// <c>VideoRetention:PurgeAfterDays</c> is configured, matching
    /// <c>SoftDeletedUserPurger</c>, which fails safe the same way for the same reason: deleting a
    /// person's records because nobody set a config value is not a defensible default.
    /// </para>
    ///
    /// <para>
    /// The tenant's <c>VideoStorageTimeSpan</c> is deliberately NOT consulted. It lives on the
    /// central <c>Institution</c> row, which is torn out of the node context, and like
    /// <c>MaxVideoStorage</c> it is never compared anywhere in the repo.
    /// </para>
    /// </summary>
    public interface IVideoRetentionReaper
    {
        /// <summary>
        /// Deletes abandoned split parts older than the configured age. Returns the count deleted.
        /// </summary>
        Task<int> ReapAbandonedPartsAsync(DateTime nowUtc, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes finished recordings retained past <c>VideoRetention:PurgeAfterDays</c>, and their
        /// ownership rows. Returns the count deleted. Fails SAFE: an unset or non-positive value
        /// deletes nothing.
        /// </summary>
        Task<int> ReapExpiredRecordingsAsync(DateTime nowUtc, CancellationToken cancellationToken);
    }

    public class VideoRetentionReaper : IVideoRetentionReaper
    {
        private const int DefaultAbandonedPartDays = 7;

        private readonly IRecordingQueries _context;
        private readonly int _abandonedPartDays;
        private readonly int? _purgeAfterDays;

        public VideoRetentionReaper(IRecordingQueries context, IConfiguration config)
        {
            _context = context;
            _abandonedPartDays = config?.GetValue<int?>("VideoRetention:AbandonedPartDays") ?? DefaultAbandonedPartDays;
            _purgeAfterDays = config?.GetValue<int?>("VideoRetention:PurgeAfterDays");
        }

        public async Task<int> ReapAbandonedPartsAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            int deleted = 0;
            try
            {
                if (_abandonedPartDays <= 0)
                {
                    return 0; // explicitly disabled
                }

                string dir = StaticDetails.SplitVideoFileSystemPath;
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                {
                    return 0;
                }

                DateTime cutoff = nowUtc.AddDays(-_abandonedPartDays);
                string[] allParts = Directory.GetFiles(dir, "*.part_*");

                // CORRECTION to the first version of this reaper. It matched on glob plus mtime
                // alone and never asked whether the part set was COMPLETE, so it could delete a
                // recording that was fully uploaded and merely never merged. That is reachable:
                // VideoUploadLogic answers 200 on VideoMergeOutcome.Skipped and on an incomplete
                // set, MergeFile has no retry job, and the PC producer moves its own copy out of
                // the watched folder once it sees that 200. The parts are then the only copy in
                // existence, and this swept them.
                //
                // Completeness is knowable from the name: parts are {base}.part_{index}.{count},
                // and VideoUploadLogic merges when the file count equals that declared count. A set
                // that has all its parts is mergeable, not abandoned, so it is left alone and
                // reported instead.
                System.Collections.Generic.HashSet<string> completeSets = CompletePartSetBaseNames(allParts);
                int completeSetsSkipped = 0;

                foreach (string path in allParts)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    try
                    {
                        string baseName = PartSetBaseName(path);
                        if (baseName != null && completeSets.Contains(baseName))
                        {
                            completeSetsSkipped++;
                            continue;
                        }

                        // LastWriteTimeUtc, not creation: a part re-sent by a retrying producer is
                        // rewritten, so this measures "untouched for N days" rather than "first
                        // seen N days ago". A recording still being retried is never reaped.
                        if (File.GetLastWriteTimeUtc(path) >= cutoff) continue;
                        File.Delete(path);
                        deleted++;
                    }
                    catch (Exception ex)
                    {
                        // One undeletable file must not stop the sweep.
                        FebrisLog.Error(ex, "VideoRetentionReaper: could not delete abandoned part '" + path + "'");
                    }
                }

                if (deleted > 0)
                {
                    if (completeSetsSkipped > 0)
                    {
                        // Loud on purpose. A complete set sitting unmerged means a merge was skipped
                        // or failed and nothing retried it, which is a defect to chase rather than
                        // a set of files to delete.
                        FebrisLog.Warn("VideoRetention: left " + completeSetsSkipped +
                            " part file(s) belonging to COMPLETE but unmerged recordings. These are still assemblable and were not reaped.");
                    }
                    FebrisLog.Info("VideoRetention: deleted " + deleted +
                        " abandoned video part(s) untouched for more than " + _abandonedPartDays + " day(s).");
                }
            }
            catch (Exception ex)
            {
                FebrisLog.Error(ex, "VideoRetentionReaper.ReapAbandonedPartsAsync");
            }
            return deleted;
        }

        public async Task<int> ReapExpiredRecordingsAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            int deleted = 0;
            try
            {
                if (!_purgeAfterDays.HasValue || _purgeAfterDays.Value <= 0)
                {
                    return 0; // purging disabled -> retain everything (fails safe)
                }

                DateTime cutoff = nowUtc.AddDays(-_purgeAfterDays.Value);
                System.Collections.Generic.List<Febris.ModelLibrary.Models.DataModels.Recording> expired =
                    await _context.GetOlderThan(cutoff);
                if (expired == null || expired.Count == 0)
                {
                    return 0;
                }

                string dir = StaticDetails.RecordingsFileSystemPath;
                int skippedNoFile = 0;
                foreach (var recording in expired)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    try
                    {
                        // The file first. If the row went first and the delete then failed, the
                        // recording would be permanently unviewable but still occupying disk, with
                        // nothing left to identify it by.
                        bool fileFound = false;
                        if (!string.IsNullOrWhiteSpace(dir))
                        {
                            string file = dir + recording.Name + ".mp4";
                            if (File.Exists(file)) { File.Delete(file); fileFound = true; }
                            string bare = dir + recording.Name;
                            if (File.Exists(bare)) { File.Delete(bare); fileFound = true; }
                        }

                        // CORRECTION to the first version of this reaper. The row delete was
                        // unconditional, and Recording.TimeStamp is stamped at module LAUNCH
                        // (RecordingLogic.Register, called from LauncherLogic before the session
                        // starts), NOT when a video arrives. So a learner who launched a module and
                        // uploaded later than the retention window had their ownership row deleted
                        // with no file ever present, and RecordingLogic.MayAcceptPart then refuses
                        // every part with "no recording was minted by this node". The upload became
                        // permanently and silently impossible.
                        //
                        // The row is the ownership record and costs almost nothing. Only reclaim it
                        // alongside the disk it was actually holding.
                        if (!fileFound)
                        {
                            skippedNoFile++;
                            continue;
                        }

                        await _context.Delete(recording.Id);
                        deleted++;
                    }
                    catch (Exception ex)
                    {
                        FebrisLog.Error(ex, "VideoRetentionReaper: could not reap recording '" + recording.Name + "'");
                    }
                }

                if (deleted > 0)
                {
                    if (skippedNoFile > 0)
                    {
                        FebrisLog.Info("VideoRetention:PurgeAfterDays: left " + skippedNoFile +
                            " ownership row(s) whose recording was never uploaded. Deleting them would block that upload permanently.");
                    }
                    FebrisLog.Info("VideoRetention:PurgeAfterDays: deleted " + deleted +
                        " recording(s) retained past " + _purgeAfterDays.Value + " day(s).");
                }
            }
            catch (Exception ex)
            {
                FebrisLog.Error(ex, "VideoRetentionReaper.ReapExpiredRecordingsAsync");
            }
            return deleted;
        }

        /// <summary>Base name of a part file, everything before <c>.part_</c>. Null when there is no token.</summary>
        private static string PartSetBaseName(string path)
        {
            string name = Path.GetFileName(path);
            int tokenAt = name.IndexOf(".part_", StringComparison.Ordinal);
            return tokenAt <= 0 ? null : name.Substring(0, tokenAt);
        }

        /// <summary>
        /// Base names whose part set is COMPLETE, and therefore still mergeable rather than
        /// abandoned. Parts are named <c>{base}.part_{index}.{count}</c>, and VideoUploadLogic
        /// merges when the number of files for a base equals that declared count, so the same test
        /// decides it here.
        ///
        /// <para>
        /// A set whose declared count cannot be parsed counts as INCOMPLETE, so it stays eligible
        /// for reaping. Unparseable debris must not become immortal.
        /// </para>
        /// </summary>
        private static System.Collections.Generic.HashSet<string> CompletePartSetBaseNames(string[] paths)
        {
            System.Collections.Generic.Dictionary<string, int> present =
                new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            System.Collections.Generic.Dictionary<string, int> declared =
                new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in paths)
            {
                string baseName = PartSetBaseName(path);
                if (baseName == null) continue;

                int seen;
                present.TryGetValue(baseName, out seen);
                present[baseName] = seen + 1;

                string name = Path.GetFileName(path);
                int lastDot = name.LastIndexOf('.');
                int declaredCount;
                if (lastDot > 0 && int.TryParse(name.Substring(lastDot + 1), out declaredCount) && declaredCount > 0)
                {
                    declared[baseName] = declaredCount;
                }
            }

            System.Collections.Generic.HashSet<string> complete =
                new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in present)
            {
                int want;
                if (declared.TryGetValue(pair.Key, out want) && pair.Value >= want)
                {
                    complete.Add(pair.Key);
                }
            }
            return complete;
        }

    }
}
