// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.SharedServices.Storage;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    /// <summary>
    /// Pulls published client software from a distribution feed into the node's own catalog
    /// -- the optional hub pull, aimed at a static feed.
    /// <para>
    /// This exists so the catalog is fed by a job instead of a person. Everything downstream is
    /// unchanged: once a row is in the catalog, the pre-existing
    /// <c>SoftwarePackage/GetLatestVersion</c> and <c>CompanionApp/GetLatestVersion</c> routes serve
    /// it, and the mobile Server keeps pulling the Companion and installing it over ADB exactly as
    /// before. Nothing on the mobile side has to change, which matters because the mobile heads are
    /// the expensive thing to rebuild.
    /// </para>
    /// </summary>
    public interface IPackageFeedSyncLogic
    {
        /// <summary>
        /// Fetch a feed and ingest what is new. Never throws for a bad feed or a bad entry: each
        /// package reports its own outcome so one broken entry cannot block every good one.
        /// </summary>
        Task<PackageFeedSyncResultViewModel> SyncFromFeed(PackageFeedSyncRequestViewModel request);
    }

    /// <inheritdoc />
    public class PackageFeedSyncLogic : IPackageFeedSyncLogic
    {
        /// <summary>The only manifest shape this build understands. Anything else is refused, not guessed at.</summary>
        private const int SupportedSchemaVersion = 1;

        private const string DefaultChannel = "stable";

        /// <summary>
        /// Hard ceiling on a single artifact, independent of what the manifest claims. A feed is
        /// remote input, so its declared <c>sizeBytes</c> is a hint and not a limit.
        /// </summary>
        private const long MaxArtifactBytes = 512L * 1024L * 1024L;

        private readonly IPackageFeedQueries _feedContext;
        private readonly IPackageIngestLogic _ingestContext;
        private readonly ILocalSoftwarePackageQueries _softwarePackageContext;
        private readonly IPackageArtifactQueries _artifactContext;

        public PackageFeedSyncLogic(
            IPackageFeedQueries feedContext,
            IPackageIngestLogic ingestContext,
            ILocalSoftwarePackageQueries softwarePackageContext,
            IPackageArtifactQueries artifactContext)
        {
            _feedContext = feedContext;
            _ingestContext = ingestContext;
            _softwarePackageContext = softwarePackageContext;
            _artifactContext = artifactContext;
        }

        /// <inheritdoc />
        public async Task<PackageFeedSyncResultViewModel> SyncFromFeed(PackageFeedSyncRequestViewModel request)
        {
            string channel = string.IsNullOrWhiteSpace(request?.Channel)
                ? DefaultChannel
                : request.Channel.Trim();

            PackageFeedSyncResultViewModel result = new PackageFeedSyncResultViewModel()
            {
                ManifestUrl = request?.ManifestUrl,
                Channel = channel,
                DryRun = request?.DryRun ?? false
            };

            try
            {
                PackageFeedManifest manifest = await _feedContext.GetManifest(request?.ManifestUrl);
                if (manifest == null)
                {
                    result.Failed = 1;
                    result.Items.Add(new PackageFeedSyncItemViewModel()
                    {
                        Outcome = PackageFeedSyncOutcome.Failed,
                        Detail = "The manifest could not be fetched or parsed. See the node log for the cause."
                    });
                    return result;
                }

                result.SchemaVersion = manifest.SchemaVersion;
                if (manifest.SchemaVersion != SupportedSchemaVersion)
                {
                    // Refuse rather than best-effort. A manifest declaring an unknown shape may have
                    // moved or repurposed a field, and a sync that guesses could publish the wrong
                    // artifact under the right name.
                    result.Refused = 1;
                    result.Items.Add(new PackageFeedSyncItemViewModel()
                    {
                        Outcome = PackageFeedSyncOutcome.Refused,
                        Detail = "Manifest declares schemaVersion " + manifest.SchemaVersion +
                                 " and this node understands " + SupportedSchemaVersion +
                                 ". Refusing rather than guessing at an unknown shape."
                    });
                    return result;
                }

                List<PackageFeedEntry> entries = manifest.Packages ?? new List<PackageFeedEntry>();

                // ASCENDING order is a correctness requirement, not a preference. The node resolves
                // "latest" by row TimeStamp, not by version
                // (LocalSoftwarePackageQueries.Get: OrderByDescending(TimeStamp)), so whatever is
                // ingested LAST becomes what clients are offered. Ingesting newest-first would leave
                // every node pointing at the oldest release in the feed, and it would look like the
                // feed had worked right up until someone tried to install.
                List<PackageFeedEntry> ordered = entries
                    .Where(i => i != null)
                    .OrderBy(i => i.VersionCode ?? 0)
                    .ThenBy(i => i.Version, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (PackageFeedEntry entry in ordered)
                {
                    PackageFeedSyncItemViewModel item = await ProcessEntry(entry, channel, request);
                    result.Items.Add(item);
                    switch (item.Outcome)
                    {
                        case PackageFeedSyncOutcome.Ingested: result.Ingested++; break;
                        case PackageFeedSyncOutcome.WouldIngest: result.Ingested++; break;
                        case PackageFeedSyncOutcome.AlreadyCurrent: result.AlreadyCurrent++; break;
                        case PackageFeedSyncOutcome.Filtered: result.Filtered++; break;
                        case PackageFeedSyncOutcome.Refused: result.Refused++; break;
                        case PackageFeedSyncOutcome.Failed: result.Failed++; break;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// One package. Returns an outcome rather than throwing, so a single malformed entry in an
        /// otherwise good feed does not abandon the rest of the run.
        /// </summary>
        private async Task<PackageFeedSyncItemViewModel> ProcessEntry(
            PackageFeedEntry entry, string channel, PackageFeedSyncRequestViewModel request)
        {
            PackageFeedSyncItemViewModel item = new PackageFeedSyncItemViewModel()
            {
                Uuid = entry.Uuid,
                Kind = entry.Kind,
                Version = entry.Version
            };

            // kind and kindId are redundant on purpose so no consumer keeps a name-to-enum mapping in
            // sync. That makes a disagreement a manifest bug, and resolving it in favour of either
            // side would ingest something under a kind the publisher did not intend.
            if (!TryResolveKind(entry, out LocalSoftwarePackageType kind, out string kindProblem))
            {
                item.Outcome = PackageFeedSyncOutcome.Refused;
                item.Detail = kindProblem;
                return item;
            }

            if (!string.Equals(entry.Channel, channel, StringComparison.OrdinalIgnoreCase))
            {
                item.Outcome = PackageFeedSyncOutcome.Filtered;
                item.Detail = "Channel '" + entry.Channel + "' is not the requested '" + channel + "'.";
                return item;
            }

            if (request?.Kinds != null && request.Kinds.Count > 0 && !request.Kinds.Contains(kind))
            {
                item.Outcome = PackageFeedSyncOutcome.Filtered;
                item.Detail = "Kind " + kind + " is not in the requested kind filter.";
                return item;
            }

            // `consumers` says who is expected to fetch a package. A node skips anything not offered
            // to nodes, so a future human-only artifact cannot silently occupy catalog space.
            if (entry.Consumers != null && entry.Consumers.Count > 0 &&
                !entry.Consumers.Any(i => string.Equals(i, "node", StringComparison.OrdinalIgnoreCase)))
            {
                item.Outcome = PackageFeedSyncOutcome.Filtered;
                item.Detail = "Not offered to nodes (consumers: " + string.Join(", ", entry.Consumers) + ").";
                return item;
            }

            if (entry.Obsolete)
            {
                // Obsolete entries stay in the feed so an operator can fetch an old build deliberately,
                // but pulling them automatically would spend bandwidth and disk on history. They remain
                // available from the feed by hand.
                item.Outcome = PackageFeedSyncOutcome.Filtered;
                item.Detail = "Marked obsolete in the feed.";
                return item;
            }

            if (entry.Uuid == Guid.Empty)
            {
                item.Outcome = PackageFeedSyncOutcome.Refused;
                item.Detail = "Entry has no uuid, so it has no stable identity to upsert against.";
                return item;
            }

            if (entry.Artifact == null || string.IsNullOrWhiteSpace(entry.Artifact.Url))
            {
                item.Outcome = PackageFeedSyncOutcome.Refused;
                item.Detail = "Entry has no artifact URL.";
                return item;
            }

            if (!IsSha256(entry.Artifact.Sha256))
            {
                // Without a usable checksum there is nothing to verify against, and ingesting anyway
                // would mean trusting the transport completely.
                item.Outcome = PackageFeedSyncOutcome.Refused;
                item.Detail = "Artifact checksum is missing or is not 64 lowercase hex characters.";
                return item;
            }

            // Already held? Compare what the feed now claims against what was actually stored. Equal
            // means nothing to do. DIFFERENT means the same release identity is advertising different
            // bytes, which is either an upstream mistake or tampering. Either way, refuse and report
            // rather than overwrite: silently replacing a stored artifact is how a node would
            // distribute something nobody chose to publish.
            string storageKey = StorageKeys.SoftwarePackage(entry.Uuid.ToString() + ".zip");
            LocalSoftwarePackage existing = await _softwarePackageContext.Get(entry.Uuid);
            if (existing != null)
            {
                PackageArtifact storedArtifact = await _artifactContext.GetByStorageKey(storageKey);
                if (storedArtifact == null)
                {
                    item.Outcome = PackageFeedSyncOutcome.Refused;
                    item.Detail = "A catalog row exists for this uuid but its artifact was never " +
                                  "ingested. Resolve by hand: re-ingesting would overwrite a row whose " +
                                  "provenance is unclear.";
                    return item;
                }

                if (string.Equals(storedArtifact.Sha256, entry.Artifact.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    item.Outcome = PackageFeedSyncOutcome.AlreadyCurrent;
                    item.Detail = "Already held with a matching checksum.";
                    return item;
                }

                item.Outcome = PackageFeedSyncOutcome.Refused;
                item.Detail = "REFUSED: uuid " + entry.Uuid + " is already held with checksum " +
                              storedArtifact.Sha256 + " but the feed now advertises " +
                              entry.Artifact.Sha256 + ". A release identity must never change its " +
                              "bytes. Not overwriting.";
                return item;
            }

            if (request?.DryRun == true)
            {
                item.Outcome = PackageFeedSyncOutcome.WouldIngest;
                item.Detail = "Would download and ingest " + entry.Artifact.FileName + ".";
                return item;
            }

            return await DownloadVerifyAndIngest(entry, kind, item);
        }

        /// <summary>
        /// Download to a temporary file, verify the checksum, and only then ingest.
        /// <para>
        /// The order is the point. <c>IngestSoftwarePackage</c> writes to the store and creates the
        /// catalog row before anything could be checked, so verifying afterwards would mean a truncated
        /// or substituted download had already become a published package. Verifying first costs one
        /// temp file and makes "a partial download must never create a catalog row" true by
        /// construction rather than by cleanup.
        /// </para>
        /// </summary>
        private async Task<PackageFeedSyncItemViewModel> DownloadVerifyAndIngest(
            PackageFeedEntry entry, LocalSoftwarePackageType kind, PackageFeedSyncItemViewModel item)
        {
            string tempPath = null;
            try
            {
                Stream remote = await _feedContext.OpenArtifact(entry.Artifact.Url);
                if (remote == null)
                {
                    item.Outcome = PackageFeedSyncOutcome.Failed;
                    item.Detail = "Artifact download failed. See the node log for the cause.";
                    return item;
                }

                string computed;
                long written;
                tempPath = Path.Combine(Path.GetTempPath(), "febris-feed-" + Guid.NewGuid().ToString("N") + ".zip");

                using (remote)
                using (FileStream temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    (computed, written) = await CopyAndHash(remote, temp, MaxArtifactBytes);
                }

                if (computed == null)
                {
                    item.Outcome = PackageFeedSyncOutcome.Refused;
                    item.Detail = "Artifact exceeded the " + MaxArtifactBytes +
                                  " byte ceiling while downloading. Refusing.";
                    return item;
                }

                if (!string.Equals(computed, entry.Artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    item.Outcome = PackageFeedSyncOutcome.Refused;
                    item.Detail = "Checksum mismatch: the feed advertised " + entry.Artifact.Sha256 +
                                  " and the " + written.ToString(CultureInfo.InvariantCulture) +
                                  " bytes received hashed to " + computed + ". Nothing was ingested.";
                    return item;
                }

                SoftwarePackageUploadViewModel metadata = new SoftwarePackageUploadViewModel()
                {
                    UUID = entry.Uuid,
                    Name = entry.Name,
                    Version = entry.Version,
                    Description = entry.Description,
                    LocalSoftwarePackageType = kind,
                    Language = ResolveLanguage(entry.Language),
                    Obsolete = false
                };

                using (FileStream verified = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    SoftwarePackageIngestResultViewModel ingested =
                        await _ingestContext.IngestSoftwarePackage(verified, entry.Artifact.FileName, metadata);

                    if (ingested == null)
                    {
                        // IngestSoftwarePackage returns null for a non-.zip name, which is the only
                        // rejection it makes.
                        item.Outcome = PackageFeedSyncOutcome.Refused;
                        item.Detail = "Ingest rejected '" + entry.Artifact.FileName +
                                      "'. Only .zip packages are accepted.";
                        return item;
                    }

                    item.Outcome = PackageFeedSyncOutcome.Ingested;
                    item.Detail = "Ingested " + written.ToString(CultureInfo.InvariantCulture) +
                                  " bytes, checksum verified.";
                    return item;
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                item.Outcome = PackageFeedSyncOutcome.Failed;
                item.Detail = "Unexpected failure while ingesting: " + ex.Message;
                return item;
            }
            finally
            {
                if (tempPath != null)
                {
                    try
                    {
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        // A leaked temp file is not worth failing a successful ingest over, but it
                        // should not vanish silently either.
                        Febris.SharedServices.FebrisLog.Error(cleanupEx);
                    }
                }
            }
        }

        /// <summary>
        /// Copy while hashing in one pass, so the bytes are never read twice and never held whole in
        /// memory. Returns (null, bytesRead) when the cap is exceeded.
        /// </summary>
        private static async Task<(string sha256, long written)> CopyAndHash(Stream source, Stream destination, long cap)
        {
            byte[] buffer = new byte[81920];
            long total = 0;

            using (SHA256 hasher = SHA256.Create())
            {
                int read;
                while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    total += read;
                    if (total > cap)
                    {
                        return (null, total);
                    }
                    hasher.TransformBlock(buffer, 0, read, null, 0);
                    await destination.WriteAsync(buffer, 0, read);
                }

                hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                await destination.FlushAsync();
                return (Convert.ToHexString(hasher.Hash).ToLowerInvariant(), total);
            }
        }

        /// <summary>
        /// Resolve the kind, requiring <c>kind</c> and <c>kindId</c> to agree. Both are checked because
        /// either alone could be the typo.
        /// </summary>
        private static bool TryResolveKind(PackageFeedEntry entry, out LocalSoftwarePackageType kind, out string problem)
        {
            kind = LocalSoftwarePackageType.None;
            problem = null;

            if (!Enum.TryParse(entry.Kind, ignoreCase: false, out LocalSoftwarePackageType byName) ||
                byName == LocalSoftwarePackageType.None)
            {
                problem = "Unknown kind '" + entry.Kind + "'.";
                return false;
            }

            if (!Enum.IsDefined(typeof(LocalSoftwarePackageType), entry.KindId))
            {
                problem = "Unknown kindId " + entry.KindId + ".";
                return false;
            }

            LocalSoftwarePackageType byId = (LocalSoftwarePackageType)entry.KindId;
            if (byId != byName)
            {
                problem = "kind '" + entry.Kind + "' and kindId " + entry.KindId +
                          " disagree (kind '" + entry.Kind + "' is " + (int)byName +
                          "). They are redundant on purpose, so this is a manifest bug rather than " +
                          "something to resolve in favour of one side.";
                return false;
            }

            kind = byName;
            return true;
        }

        /// <summary>Language is optional in the feed. Absent or unrecognised falls back to the enum default.</summary>
        private static LanguageMapTypeEnum ResolveLanguage(string language)
        {
            if (!string.IsNullOrWhiteSpace(language) &&
                Enum.TryParse(language, ignoreCase: true, out LanguageMapTypeEnum parsed))
            {
                return parsed;
            }
            return default;
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
            {
                return false;
            }
            foreach (char c in value)
            {
                bool isLowerHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!isLowerHex)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
