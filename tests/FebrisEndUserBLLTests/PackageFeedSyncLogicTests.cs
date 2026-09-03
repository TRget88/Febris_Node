// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.SharedServices.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the package-feed sync: the node pulls published
    /// client software from a distribution feed into its OWN catalog, so the catalog is fed by a job
    /// rather than by a person uploading zips through the portal.
    /// <para>
    /// Only the HTTP half is faked. Storage is a real temp-dir <c>FileSystemStorageProvider</c> and the
    /// catalogs are real EF InMemory contexts driving the real <c>PackageIngestLogic</c>, following
    /// <see cref="NodeArtifactStoreTests"/>. That matters here: the properties worth pinning are about
    /// what does and does not reach the catalog and the store, and a mocked ingest would pin nothing.
    /// </para>
    /// </summary>
    public class PackageFeedSyncLogicTests : IDisposable
    {
        private const string ManifestUrl = "https://feed.invalid/manifest.json";
        private const string ArtifactUrl = "https://feed.invalid/a.zip";

        private readonly string _storageRoot;
        private readonly FileSystemStorageProvider _storage;

        public PackageFeedSyncLogicTests()
        {
            _storageRoot = Path.Combine(Path.GetTempPath(), "febris-feed-sync-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_storageRoot);
            _storage = new FileSystemStorageProvider(new StorageOptions() { BasePath = _storageRoot });
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_storageRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup; never fail a run over it.
            }
        }

        #region harness

        /// <summary>
        /// Stands in for the network. Serves one manifest and a set of artifact bodies keyed by URL,
        /// and counts artifact fetches so a test can prove a download did NOT happen.
        /// </summary>
        private sealed class FakeFeed : IPackageFeedQueries
        {
            private readonly PackageFeedManifest _manifest;
            private readonly Dictionary<string, byte[]> _bodies;

            public int ArtifactFetches { get; private set; }

            public FakeFeed(PackageFeedManifest manifest, Dictionary<string, byte[]> bodies = null)
            {
                _manifest = manifest;
                _bodies = bodies ?? new Dictionary<string, byte[]>();
            }

            public Task<PackageFeedManifest> GetManifest(string manifestUrl) => Task.FromResult(_manifest);

            public Task<Stream> OpenArtifact(string artifactUrl)
            {
                ArtifactFetches++;
                if (!_bodies.TryGetValue(artifactUrl, out byte[] body))
                {
                    return Task.FromResult<Stream>(null);
                }
                return Task.FromResult<Stream>(new MemoryStream(body));
            }
        }

        private static DataDbContext BuildContext(string dbName)
        {
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new DataDbContext(options);
        }

        private PackageFeedSyncLogic BuildSync(DataDbContext context, IPackageFeedQueries feed)
        {
            // Feed sync ingests SOFTWARE packages only, which have no xAPI activity, so the Object
            // BLL the module path uses (ROADMAP 15) is never reached from here. Strict: a call
            // would be a defect, not a silent no-op.
            PackageIngestLogic ingest = new PackageIngestLogic(
                _storage,
                new ModuleQueries(context),
                new ModuleLinkedObjectQueries(context),
                new LocalSoftwarePackageQueries(context),
                new PackageArtifactQueries(context),
                new Moq.Mock<Febris.PrimaryLogicLayer.Logic.XApiLogic.IObjectLogic>(Moq.MockBehavior.Strict).Object);

            return new PackageFeedSyncLogic(
                feed,
                ingest,
                new LocalSoftwarePackageQueries(context),
                new PackageArtifactQueries(context));
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using SHA256 hasher = SHA256.Create();
            return Convert.ToHexString(hasher.ComputeHash(bytes)).ToLowerInvariant();
        }

        /// <summary>
        /// ROADMAP 15: the ingest opens the payload as an archive now, so a feed entry has to serve
        /// a REAL zip. These fixtures used to serve bare UTF-8 strings named .zip, which is exactly
        /// the payload the new guard refuses. Distinct content still yields distinct bytes, so the
        /// checksum-mismatch and byte-substitution cases keep testing what they always did.
        /// </summary>
        private static byte[] ZipBytes(string content)
        {
            using var buffer = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(
                buffer, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("payload.bin");
                using Stream entryStream = entry.Open();
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
                entryStream.Write(bytes, 0, bytes.Length);
            }

            return buffer.ToArray();
        }

        private static PackageFeedEntry Entry(
            Guid uuid,
            byte[] payload,
            string version = "0.2.0",
            int versionCode = 200,
            LocalSoftwarePackageType kind = LocalSoftwarePackageType.AndroidMobileCompanion,
            string channel = "stable",
            bool obsolete = false,
            string url = ArtifactUrl,
            string sha256Override = null,
            List<string> consumers = null,
            int? kindIdOverride = null)
        {
            return new PackageFeedEntry()
            {
                Uuid = uuid,
                Kind = kind.ToString(),
                KindId = kindIdOverride ?? (int)kind,
                Name = "Febris Mobile Companion",
                Version = version,
                VersionCode = versionCode,
                Channel = channel,
                Consumers = consumers ?? new List<string>() { "node" },
                Obsolete = obsolete,
                Artifact = new PackageFeedArtifact()
                {
                    FileName = "febris-" + version + ".zip",
                    Url = url,
                    SizeBytes = payload?.Length ?? 1,
                    Sha256 = sha256Override ?? Sha256Hex(payload ?? Array.Empty<byte>())
                }
            };
        }

        private static PackageFeedManifest Manifest(params PackageFeedEntry[] entries)
        {
            return new PackageFeedManifest()
            {
                SchemaVersion = 1,
                Generated = new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc),
                Packages = entries.ToList()
            };
        }

        private static PackageFeedSyncRequestViewModel Request(bool dryRun = false, string channel = null,
            List<LocalSoftwarePackageType> kinds = null)
        {
            return new PackageFeedSyncRequestViewModel()
            {
                ManifestUrl = ManifestUrl,
                Channel = channel,
                Kinds = kinds,
                DryRun = dryRun
            };
        }

        #endregion

        [Fact]
        public async Task ValidEntry_IsIngested_WithCatalogRowArtifactAndStoredBytes()
        {
            using DataDbContext context = BuildContext(nameof(ValidEntry_IsIngested_WithCatalogRowArtifactAndStoredBytes));
            byte[] payload = ZipBytes("companion-zip-payload");
            Guid uuid = Guid.NewGuid();
            FakeFeed feed = new FakeFeed(
                Manifest(Entry(uuid, payload)),
                new Dictionary<string, byte[]>() { { ArtifactUrl, payload } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Ingested.Should().Be(1);
            result.Refused.Should().Be(0);
            result.Failed.Should().Be(0);
            result.Items.Single().Outcome.Should().Be(PackageFeedSyncOutcome.Ingested);

            // The publisher's uuid becomes the catalog identity, which is what makes a re-sync
            // idempotent through Upsert.
            LocalSoftwarePackage row = context.LocalSoftwarePackage.Single();
            row.UUID.Should().Be(uuid);
            row.Version.Should().Be("0.2.0");
            row.LocalSoftwarePackageType.Should().Be(LocalSoftwarePackageType.AndroidMobileCompanion);

            PackageArtifact artifact = context.PackageArtifact.Single();
            artifact.Sha256.Should().Be(Sha256Hex(payload));
            (await _storage.ExistsAsync(artifact.StorageKey)).Should().BeTrue();
        }

        [Fact]
        public async Task ChecksumMismatch_IsRefused_AndNothingReachesTheCatalog()
        {
            using DataDbContext context = BuildContext(nameof(ChecksumMismatch_IsRefused_AndNothingReachesTheCatalog));
            byte[] payload = ZipBytes("the-real-bytes");
            byte[] served = ZipBytes("substituted-bytes");
            FakeFeed feed = new FakeFeed(
                Manifest(Entry(Guid.NewGuid(), payload)),
                new Dictionary<string, byte[]>() { { ArtifactUrl, served } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Refused.Should().Be(1);
            result.Ingested.Should().Be(0);
            result.Items.Single().Detail.Should().Contain("Checksum mismatch");

            // The property that matters: verification happens BEFORE ingest, so a substituted or
            // truncated download never becomes a published package.
            context.LocalSoftwarePackage.Should().BeEmpty();
            context.PackageArtifact.Should().BeEmpty();
            Directory.GetFiles(_storageRoot, "*", SearchOption.AllDirectories).Should().BeEmpty();
        }

        [Fact]
        public async Task KindAndKindIdDisagree_IsRefused_WithoutDownloading()
        {
            using DataDbContext context = BuildContext(nameof(KindAndKindIdDisagree_IsRefused_WithoutDownloading));
            byte[] payload = ZipBytes("x");
            // Says Companion by name, Server by number.
            FakeFeed feed = new FakeFeed(
                Manifest(Entry(Guid.NewGuid(), payload,
                    kind: LocalSoftwarePackageType.AndroidMobileCompanion,
                    kindIdOverride: (int)LocalSoftwarePackageType.AndroidMobileServer)),
                new Dictionary<string, byte[]>() { { ArtifactUrl, payload } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Refused.Should().Be(1);
            result.Items.Single().Detail.Should().Contain("disagree");
            feed.ArtifactFetches.Should().Be(0, "a manifest bug should be caught before spending bandwidth");
            context.LocalSoftwarePackage.Should().BeEmpty();
        }

        [Fact]
        public async Task UnknownSchemaVersion_IsRefused_AndNoEntryIsProcessed()
        {
            using DataDbContext context = BuildContext(nameof(UnknownSchemaVersion_IsRefused_AndNoEntryIsProcessed));
            byte[] payload = ZipBytes("x");
            PackageFeedManifest manifest = Manifest(Entry(Guid.NewGuid(), payload));
            manifest.SchemaVersion = 2;
            FakeFeed feed = new FakeFeed(manifest, new Dictionary<string, byte[]>() { { ArtifactUrl, payload } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Refused.Should().Be(1);
            result.SchemaVersion.Should().Be(2);
            result.Items.Single().Detail.Should().Contain("Refusing rather than guessing");
            feed.ArtifactFetches.Should().Be(0);
            context.LocalSoftwarePackage.Should().BeEmpty();
        }

        [Fact]
        public async Task UnreachableManifest_ReportsFailed_AndDoesNotThrow()
        {
            using DataDbContext context = BuildContext(nameof(UnreachableManifest_ReportsFailed_AndDoesNotThrow));
            FakeFeed feed = new FakeFeed(manifest: null);

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Failed.Should().Be(1);
            result.Items.Single().Outcome.Should().Be(PackageFeedSyncOutcome.Failed);
            context.LocalSoftwarePackage.Should().BeEmpty();
        }

        [Fact]
        public async Task DryRun_ReportsWhatWouldHappen_AndChangesNothing()
        {
            using DataDbContext context = BuildContext(nameof(DryRun_ReportsWhatWouldHappen_AndChangesNothing));
            byte[] payload = ZipBytes("payload");
            FakeFeed feed = new FakeFeed(
                Manifest(Entry(Guid.NewGuid(), payload)),
                new Dictionary<string, byte[]>() { { ArtifactUrl, payload } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request(dryRun: true));

            result.Items.Single().Outcome.Should().Be(PackageFeedSyncOutcome.WouldIngest);
            feed.ArtifactFetches.Should().Be(0, "a dry run must not download either");
            context.LocalSoftwarePackage.Should().BeEmpty();
            context.PackageArtifact.Should().BeEmpty();
        }

        [Fact]
        public async Task ReSyncOfTheSameRelease_IsAlreadyCurrent_AndDoesNotRedownload()
        {
            using DataDbContext context = BuildContext(nameof(ReSyncOfTheSameRelease_IsAlreadyCurrent_AndDoesNotRedownload));
            byte[] payload = ZipBytes("stable-payload");
            Guid uuid = Guid.NewGuid();
            PackageFeedManifest manifest = Manifest(Entry(uuid, payload));
            Dictionary<string, byte[]> bodies = new Dictionary<string, byte[]>() { { ArtifactUrl, payload } };

            FakeFeed first = new FakeFeed(manifest, bodies);
            await BuildSync(context, first).SyncFromFeed(Request());

            FakeFeed second = new FakeFeed(manifest, bodies);
            PackageFeedSyncResultViewModel result = await BuildSync(context, second).SyncFromFeed(Request());

            result.AlreadyCurrent.Should().Be(1);
            result.Ingested.Should().Be(0);
            second.ArtifactFetches.Should().Be(0);
            context.LocalSoftwarePackage.Count().Should().Be(1, "the publisher uuid keeps a re-sync idempotent");
        }

        [Fact]
        public async Task SameUuidAdvertisingDifferentBytes_IsRefused_AndTheStoredArtifactIsUntouched()
        {
            using DataDbContext context = BuildContext(nameof(SameUuidAdvertisingDifferentBytes_IsRefused_AndTheStoredArtifactIsUntouched));
            byte[] original = ZipBytes("originally-published");
            Guid uuid = Guid.NewGuid();

            await BuildSync(context, new FakeFeed(
                Manifest(Entry(uuid, original)),
                new Dictionary<string, byte[]>() { { ArtifactUrl, original } })).SyncFromFeed(Request());

            string originalSha = context.PackageArtifact.Single().Sha256;

            // Same release identity, different bytes. Either an upstream mistake or tampering, and
            // silently replacing a stored artifact would mean distributing something nobody chose to
            // publish.
            byte[] swapped = ZipBytes("quietly-replaced");
            FakeFeed hostile = new FakeFeed(
                Manifest(Entry(uuid, swapped)),
                new Dictionary<string, byte[]>() { { ArtifactUrl, swapped } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, hostile).SyncFromFeed(Request());

            result.Refused.Should().Be(1);
            result.Items.Single().Detail.Should().Contain("must never change its bytes");
            hostile.ArtifactFetches.Should().Be(0);
            context.PackageArtifact.Single().Sha256.Should().Be(originalSha);
        }

        /// <summary>
        /// The counterpart to the test above, and the case that was missing when a node could only
        /// ever ingest a package once.
        ///
        /// <para>
        /// CLIENT_RELEASE_GUIDE.md line 241 instructs publishers to KEEP a row's uuid and change the
        /// version and artifact, which is exactly the shape the guard above refuses. With no version
        /// comparison the two rules contradicted, so a node took companion 0.2.0 and then refused
        /// 0.2.1 and every release after it, permanently, with no operator-visible cause beyond a
        /// REFUSED line. The version is what separates a new release from a swapped payload.
        /// </para>
        /// </summary>
        [Fact]
        public async Task SameUuidAtANewVersion_IsIngested_AndReplacesTheStoredArtifact()
        {
            using DataDbContext context = BuildContext(nameof(SameUuidAtANewVersion_IsIngested_AndReplacesTheStoredArtifact));
            byte[] original = ZipBytes("release-0-2-0");
            Guid uuid = Guid.NewGuid();

            await BuildSync(context, new FakeFeed(
                Manifest(Entry(uuid, original)),
                new Dictionary<string, byte[]>() { { ArtifactUrl, original } })).SyncFromFeed(Request());

            string originalSha = context.PackageArtifact.Single().Sha256;

            // Same uuid, MOVED version. A normal release, not tampering.
            byte[] next = ZipBytes("release-0-2-1");
            FakeFeed updated = new FakeFeed(
                Manifest(Entry(uuid, next, version: "0.2.1", versionCode: 201)),
                new Dictionary<string, byte[]>() { { ArtifactUrl, next } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, updated).SyncFromFeed(Request());

            result.Refused.Should().Be(0);
            result.Ingested.Should().Be(1);

            // Upsert on the uuid, so the catalog moves forward rather than gaining a second row.
            LocalSoftwarePackage row = context.LocalSoftwarePackage.Single();
            row.UUID.Should().Be(uuid);
            row.Version.Should().Be("0.2.1");

            PackageArtifact artifact = context.PackageArtifact.Single();
            artifact.Sha256.Should().Be(Sha256Hex(next));
            artifact.Sha256.Should().NotBe(originalSha);
            (await _storage.ExistsAsync(artifact.StorageKey)).Should().BeTrue();
        }

        [Fact]
        public async Task Entries_AreIngestedAscending_SoTheNewestReleaseWinsTheTimestampOrdering()
        {
            using DataDbContext context = BuildContext(nameof(Entries_AreIngestedAscending_SoTheNewestReleaseWinsTheTimestampOrdering));
            byte[] older = ZipBytes("v0.1.0-payload");
            byte[] newer = ZipBytes("v0.2.0-payload");
            Guid olderUuid = Guid.NewGuid();
            Guid newerUuid = Guid.NewGuid();

            // Deliberately listed NEWEST FIRST, which is the order that breaks a naive sync: the node
            // resolves "latest" by row TimeStamp, so whatever is ingested LAST is what clients get.
            PackageFeedManifest manifest = Manifest(
                Entry(newerUuid, newer, version: "0.2.0", versionCode: 200, url: "https://feed.invalid/new.zip"),
                Entry(olderUuid, older, version: "0.1.0", versionCode: 100, url: "https://feed.invalid/old.zip"));

            FakeFeed feed = new FakeFeed(manifest, new Dictionary<string, byte[]>()
            {
                { "https://feed.invalid/new.zip", newer },
                { "https://feed.invalid/old.zip", older }
            });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Ingested.Should().Be(2);

            // THE property under test: processed oldest-first regardless of manifest order.
            result.Items.Select(i => i.Version).Should().ContainInOrder("0.1.0", "0.2.0");

            // Why that matters, pinned separately because this harness cannot produce it on its own.
            // LocalSoftwarePackage.TimeStamp is a POSTGRES default
            // (DataDbContext: HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd()), and EF
            // InMemory does not apply SQL defaults, so every row here ties at default(DateTime) and
            // OrderByDescending(TimeStamp) resolves arbitrarily. Stamping the rows in the order they
            // were ingested is what the database does in production, so this asserts the consequence
            // of ascending ingest rather than pretending the harness demonstrated it.
            List<LocalSoftwarePackage> inIngestOrder = result.Items
                .Select(i => context.LocalSoftwarePackage.Single(p => p.UUID == i.Uuid))
                .ToList();
            DateTime stamp = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);
            foreach (LocalSoftwarePackage row in inIngestOrder)
            {
                row.TimeStamp = stamp;
                stamp = stamp.AddSeconds(1);
            }
            context.SaveChanges();

            LocalSoftwarePackage resolved = await new LocalSoftwarePackageQueries(context)
                .Get(LocalSoftwarePackageType.AndroidMobileCompanion);
            resolved.UUID.Should().Be(newerUuid,
                "the node resolves latest by newest TimeStamp, so ingesting ascending is what makes " +
                "the newest release the one clients are offered");
        }

        [Theory]
        [InlineData("beta", null, "Channel")]
        [InlineData("stable", "obsolete", "obsolete")]
        [InlineData("stable", "humanonly", "Not offered to nodes")]
        public async Task FilteredEntries_AreSkipped_WithoutDownloading(string channel, string variant, string expectedDetail)
        {
            using DataDbContext context = BuildContext(
                "filtered-" + channel + "-" + (variant ?? "none"));
            byte[] payload = ZipBytes("payload");

            PackageFeedEntry entry = Entry(
                Guid.NewGuid(),
                payload,
                channel: channel,
                obsolete: variant == "obsolete",
                consumers: variant == "humanonly" ? new List<string>() { "human" } : null);

            FakeFeed feed = new FakeFeed(
                Manifest(entry),
                new Dictionary<string, byte[]>() { { ArtifactUrl, payload } });

            // Requesting the default 'stable' channel.
            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Filtered.Should().Be(1);
            result.Ingested.Should().Be(0);
            result.Items.Single().Detail.Should().Contain(expectedDetail);
            feed.ArtifactFetches.Should().Be(0);
            context.LocalSoftwarePackage.Should().BeEmpty();
        }

        [Fact]
        public async Task KindFilter_ExcludesOtherKinds()
        {
            using DataDbContext context = BuildContext(nameof(KindFilter_ExcludesOtherKinds));
            byte[] payload = ZipBytes("payload");
            FakeFeed feed = new FakeFeed(
                Manifest(Entry(Guid.NewGuid(), payload, kind: LocalSoftwarePackageType.AndroidMobileCompanion)),
                new Dictionary<string, byte[]>() { { ArtifactUrl, payload } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(
                Request(kinds: new List<LocalSoftwarePackageType>() { LocalSoftwarePackageType.PC }));

            result.Filtered.Should().Be(1);
            result.Items.Single().Detail.Should().Contain("kind filter");
            context.LocalSoftwarePackage.Should().BeEmpty();
        }

        [Fact]
        public async Task MissingOrMalformedChecksum_IsRefused_WithoutDownloading()
        {
            using DataDbContext context = BuildContext(nameof(MissingOrMalformedChecksum_IsRefused_WithoutDownloading));
            byte[] payload = ZipBytes("payload");
            // Uppercase is not the recorded casing, and accepting it would make the comparison
            // ambiguous rather than strict.
            FakeFeed feed = new FakeFeed(
                Manifest(Entry(Guid.NewGuid(), payload, sha256Override: new string('A', 64))),
                new Dictionary<string, byte[]>() { { ArtifactUrl, payload } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Refused.Should().Be(1);
            result.Items.Single().Detail.Should().Contain("64 lowercase hex");
            feed.ArtifactFetches.Should().Be(0);
        }

        [Fact]
        public async Task OneBadEntry_DoesNotBlockTheGoodOnes()
        {
            using DataDbContext context = BuildContext(nameof(OneBadEntry_DoesNotBlockTheGoodOnes));
            byte[] good = ZipBytes("good-payload");
            byte[] bad = ZipBytes("bad-payload");

            PackageFeedManifest manifest = Manifest(
                Entry(Guid.NewGuid(), bad, version: "0.1.0", versionCode: 100,
                      url: "https://feed.invalid/bad.zip", sha256Override: new string('b', 64)),
                Entry(Guid.NewGuid(), good, version: "0.2.0", versionCode: 200,
                      url: "https://feed.invalid/good.zip"));

            FakeFeed feed = new FakeFeed(manifest, new Dictionary<string, byte[]>()
            {
                { "https://feed.invalid/bad.zip", bad },
                { "https://feed.invalid/good.zip", good }
            });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Ingested.Should().Be(1);
            result.Refused.Should().Be(1);
            context.LocalSoftwarePackage.Single().Version.Should().Be("0.2.0");
        }

        [Fact]
        public async Task ArtifactDownloadFailure_ReportsFailed_AndLeavesNoPartialRow()
        {
            using DataDbContext context = BuildContext(nameof(ArtifactDownloadFailure_ReportsFailed_AndLeavesNoPartialRow));
            byte[] payload = ZipBytes("payload");
            // Manifest advertises an artifact the feed cannot serve.
            FakeFeed feed = new FakeFeed(Manifest(Entry(Guid.NewGuid(), payload)), new Dictionary<string, byte[]>());

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Failed.Should().Be(1);
            result.Items.Single().Outcome.Should().Be(PackageFeedSyncOutcome.Failed);
            context.LocalSoftwarePackage.Should().BeEmpty();
            context.PackageArtifact.Should().BeEmpty();
        }

        #region contains[] payload verification

        /// <summary>
        /// SHA-256 of the bytes ZipBytes puts INSIDE the archive, which is what contains[] records.
        /// Deliberately not the archive's own hash, since the whole point of these cases is that the
        /// two are different things.
        /// </summary>
        private static string InnerSha(string content)
        {
            return Sha256Hex(System.Text.Encoding.UTF8.GetBytes(content));
        }

        private static PackageFeedEntry WithContains(PackageFeedEntry entry, string fileName, string sha256)
        {
            entry.Contains = new List<PackageFeedContent>()
            {
                new PackageFeedContent() { FileName = fileName, Sha256 = sha256 }
            };
            return entry;
        }

        [Fact]
        public async Task DeclaredPayloadThatMatches_IsIngested()
        {
            using DataDbContext context = BuildContext(nameof(DeclaredPayloadThatMatches_IsIngested));
            byte[] payload = ZipBytes("companion-zip-payload");
            PackageFeedEntry entry = WithContains(
                Entry(Guid.NewGuid(), payload), "payload.bin", InnerSha("companion-zip-payload"));
            FakeFeed feed = new FakeFeed(
                Manifest(entry), new Dictionary<string, byte[]>() { { ArtifactUrl, payload } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Ingested.Should().Be(1, "a correctly declared payload must still ingest");
            result.Refused.Should().Be(0);
        }

        [Fact]
        public async Task DeclaredPayloadWithTheWrongHash_IsRefused_AndNothingReachesTheCatalog()
        {
            using DataDbContext context = BuildContext(nameof(DeclaredPayloadWithTheWrongHash_IsRefused_AndNothingReachesTheCatalog));
            byte[] payload = ZipBytes("companion-zip-payload");

            // The ARCHIVE hash is correct, so the wrapper check passes and only the payload check can
            // catch this. That is the whole gap being closed: a sound envelope around wrong contents.
            PackageFeedEntry entry = WithContains(
                Entry(Guid.NewGuid(), payload), "payload.bin", InnerSha("something-else-entirely"));
            FakeFeed feed = new FakeFeed(
                Manifest(entry), new Dictionary<string, byte[]>() { { ArtifactUrl, payload } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Refused.Should().Be(1);
            result.Ingested.Should().Be(0);
            result.Items.Single().Detail.Should().Contain("Payload checksum mismatch");

            context.LocalSoftwarePackage.Should().BeEmpty();
            context.PackageArtifact.Should().BeEmpty();
            Directory.GetFiles(_storageRoot, "*", SearchOption.AllDirectories).Should().BeEmpty();
        }

        [Fact]
        public async Task DeclaredPayloadThatIsNotInTheArchive_IsRefused()
        {
            using DataDbContext context = BuildContext(nameof(DeclaredPayloadThatIsNotInTheArchive_IsRefused));
            byte[] payload = ZipBytes("companion-zip-payload");

            // Matched as an exact PATH, so a base name for a nested file does not resolve. This is the
            // shape the published C++ SDK row had before its paths were corrected.
            PackageFeedEntry entry = WithContains(
                Entry(Guid.NewGuid(), payload), "nested/payload.bin", InnerSha("companion-zip-payload"));
            FakeFeed feed = new FakeFeed(
                Manifest(entry), new Dictionary<string, byte[]>() { { ArtifactUrl, payload } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Refused.Should().Be(1);
            result.Ingested.Should().Be(0);
            result.Items.Single().Detail.Should().Contain("no such path is present");
            context.LocalSoftwarePackage.Should().BeEmpty();
        }

        [Fact]
        public async Task DeclaredPayloadWithAnUnusableDigest_IsRefused()
        {
            using DataDbContext context = BuildContext(nameof(DeclaredPayloadWithAnUnusableDigest_IsRefused));
            byte[] payload = ZipBytes("companion-zip-payload");
            PackageFeedEntry entry = WithContains(Entry(Guid.NewGuid(), payload), "payload.bin", "NOT-A-HASH");
            FakeFeed feed = new FakeFeed(
                Manifest(entry), new Dictionary<string, byte[]>() { { ArtifactUrl, payload } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Refused.Should().Be(1);
            result.Ingested.Should().Be(0);
            result.Items.Single().Detail.Should().Contain("without a lowercase hex sha256");
        }

        [Fact]
        public async Task AnEntryDeclaringNoContents_IsStillIngested()
        {
            // Most rows declare nothing, and an absent declaration means nothing was promised rather
            // than something was broken. If this ever fails, the new check has become mandatory and
            // every existing feed row would stop ingesting.
            using DataDbContext context = BuildContext(nameof(AnEntryDeclaringNoContents_IsStillIngested));
            byte[] payload = ZipBytes("companion-zip-payload");
            FakeFeed feed = new FakeFeed(
                Manifest(Entry(Guid.NewGuid(), payload)),
                new Dictionary<string, byte[]>() { { ArtifactUrl, payload } });

            PackageFeedSyncResultViewModel result = await BuildSync(context, feed).SyncFromFeed(Request());

            result.Ingested.Should().Be(1);
            result.Refused.Should().Be(0);
        }

        #endregion

    }
}
