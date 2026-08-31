// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.LookupModels;
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
    /// Pins the node artifact store (delivery-path severance): module .zips and
    /// client-software packages ingest through IStorageProvider into the NODE's own store with the
    /// stored bytes' SHA-256 recorded on a PackageArtifact row, the local catalog rows are
    /// created/updated, and downloads stream back from the same store -- a full
    /// upload -> store -> catalog -> download round-trip with zero HTTP and no central push.
    /// Storage uses a real temp-dir FileSystemStorageProvider; catalogs use the EF InMemory
    /// provider (relational defaults are metadata-only there, so tests set their own UUIDs and
    /// timestamps where the database normally would).
    /// </summary>
    public class NodeArtifactStoreTests : IDisposable
    {
        private readonly string _storageRoot;
        private readonly FileSystemStorageProvider _storage;

        public NodeArtifactStoreTests()
        {
            _storageRoot = Path.Combine(Path.GetTempPath(), "febris-node-artifact-tests-" + Guid.NewGuid().ToString("N"));
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
                // Best-effort temp cleanup; never fail a test run over it.
            }
        }

        private static DataDbContext BuildContext(string dbName)
        {
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new DataDbContext(options);
        }

        private static XApiDbContext BuildXApiContext(string dbName)
        {
            DbContextOptions<XApiDbContext> options = new DbContextOptionsBuilder<XApiDbContext>()
                .UseInMemoryDatabase(dbName + "-xapi")
                .Options;
            return new XApiDbContext(options);
        }

        private PackageIngestLogic BuildIngest(DataDbContext context)
        {
            return BuildIngest(context, BuildXApiContext(Guid.NewGuid().ToString("N")));
        }

        /// <summary>
        /// ROADMAP 15: the ingest mints the module's xAPI activity through ObjectLogic, so it needs
        /// the XApi store as well as the Data store. ObjectLogic is built through its DI ctor --
        /// the legacy one self-news its queries from static config and would reach for a real
        /// database.
        /// </summary>
        private PackageIngestLogic BuildIngest(DataDbContext context, XApiDbContext xApiContext)
        {
            var accessor = new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var objectLogic = new Febris.PrimaryLogicLayer.Logic.XApiLogic.ObjectLogic(
                accessor.Object,
                new Febris.UserNode.DataAccessLayer.Queries.XApiQueries.ObjectQueries(xApiContext),
                new Febris.PrimaryLogicLayer.Logic.XApiLogic.DefinitionLogic(
                    accessor.Object,
                    new Febris.UserNode.DataAccessLayer.Queries.XApiQueries.DefinitionQueries(xApiContext)),
                new ModuleLinkedObjectLogic(
                    accessor.Object,
                    new Febris.UserNode.DataAccessLayer.Queries.XApiQueries.ObjectQueries(xApiContext),
                    new ModuleQueries(context),
                    new ModuleLinkedObjectQueries(context)));

            return new PackageIngestLogic(
                _storage,
                new ModuleQueries(context),
                new ModuleLinkedObjectQueries(context),
                new LocalSoftwarePackageQueries(context),
                new PackageArtifactQueries(context),
                objectLogic);
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using SHA256 hasher = SHA256.Create();
            return Convert.ToHexString(hasher.ComputeHash(bytes)).ToLowerInvariant();
        }

        /// <summary>
        /// ROADMAP 15: a REAL zip, because the ingest now opens the bytes rather than trusting the
        /// filename. Every happy-path fixture here used to hand it a UTF-8 string named ".zip",
        /// which is precisely the payload the new guard exists to refuse, so the fixtures had to
        /// become archives for the tests to keep meaning what they claim.
        /// </summary>
        private static byte[] ZipBytes(string entryName, string entryContent)
        {
            using var buffer = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(
                buffer, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry(entryName);
                using Stream entryStream = entry.Open();
                byte[] payload = System.Text.Encoding.UTF8.GetBytes(entryContent);
                entryStream.Write(payload, 0, payload.Length);
            }

            return buffer.ToArray();
        }

        /// <summary>An archive with no entries, which the ingest refuses: it cannot carry a module.</summary>
        private static byte[] EmptyZipBytes()
        {
            using var buffer = new MemoryStream();
            using (new System.IO.Compression.ZipArchive(
                buffer, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
            }

            return buffer.ToArray();
        }

        [Fact]
        public async Task SoftwarePackage_UploadStoreCatalogDownload_RoundTrips_WithVerifiedChecksum()
        {
            using DataDbContext context = BuildContext(nameof(SoftwarePackage_UploadStoreCatalogDownload_RoundTrips_WithVerifiedChecksum));
            PackageIngestLogic ingest = BuildIngest(context);
            byte[] payload = ZipBytes("companion.apk", "companion-apk-zip-payload");

            SoftwarePackageIngestResultViewModel result = await ingest.IngestSoftwarePackage(
                new MemoryStream(payload),
                "companion-3.2.zip",
                new SoftwarePackageUploadViewModel()
                {
                    Name = "Mobile Companion",
                    Version = "3.2",
                    LocalSoftwarePackageType = LocalSoftwarePackageType.AndroidMobileCompanion
                });

            // Catalog row + artifact bookkeeping.
            result.Should().NotBeNull();
            result.LocalSoftwarePackage.Version.Should().Be("3.2");
            context.LocalSoftwarePackage.Count().Should().Be(1);
            result.Artifact.StorageKey.Should().Be("localsoftwarepackage/" + result.LocalSoftwarePackage.UUID + ".zip");
            result.Artifact.Sha256.Should().Be(Sha256Hex(payload), "the recorded checksum must describe the STORED bytes");
            result.Artifact.ContentLength.Should().Be(payload.Length);
            (await _storage.ExistsAsync(result.Artifact.StorageKey)).Should().BeTrue();

            // Download streams the same bytes back from the node's store (the path the
            // CompanionAppController Download route and the portal downloads page use).
            var accessor = new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var logic = new LocalSoftwarePackageLogic(accessor.Object, new LocalSoftwarePackageQueries(context), _storage);
            using Stream downloaded = await logic.Download(result.LocalSoftwarePackage.UUID);
            using var buffer = new MemoryStream();
            await downloaded.CopyToAsync(buffer);
            buffer.ToArray().Should().Equal(payload);
            Sha256Hex(buffer.ToArray()).Should().Be(result.Artifact.Sha256, "a client can verify its download against the recorded checksum");
        }

        [Fact]
        public async Task SoftwarePackage_Reingest_SameUuid_UpdatesCatalogAndArtifactInPlace()
        {
            using DataDbContext context = BuildContext(nameof(SoftwarePackage_Reingest_SameUuid_UpdatesCatalogAndArtifactInPlace));
            PackageIngestLogic ingest = BuildIngest(context);
            Guid uuid = Guid.NewGuid();
            byte[] v1 = ZipBytes("payload.bin", "v1");
            byte[] v2 = ZipBytes("payload.bin", "v2-bigger-payload");

            await ingest.IngestSoftwarePackage(new MemoryStream(v1), "pkg.zip",
                new SoftwarePackageUploadViewModel() { UUID = uuid, Version = "1.0", LocalSoftwarePackageType = LocalSoftwarePackageType.PC });
            SoftwarePackageIngestResultViewModel second = await ingest.IngestSoftwarePackage(new MemoryStream(v2), "pkg.zip",
                new SoftwarePackageUploadViewModel() { UUID = uuid, Version = "1.1", LocalSoftwarePackageType = LocalSoftwarePackageType.PC });

            context.LocalSoftwarePackage.Count().Should().Be(1, "same catalog UUID must not duplicate");
            context.PackageArtifact.Count().Should().Be(1, "same storage key must not duplicate");
            second.LocalSoftwarePackage.Version.Should().Be("1.1");
            second.Artifact.Sha256.Should().Be(Sha256Hex(v2), "re-ingest must re-record the checksum of the replacing bytes");
            second.Artifact.ContentLength.Should().Be(v2.Length);
        }

        [Fact]
        public async Task Ingest_RejectsNonZipPayloads()
        {
            using DataDbContext context = BuildContext(nameof(Ingest_RejectsNonZipPayloads));
            PackageIngestLogic ingest = BuildIngest(context);

            // Mirrors the legacy FileServerHandler acceptable-type gate: .zip only.
            (await ingest.IngestSoftwarePackage(new MemoryStream(new byte[] { 1 }), "raw.apk",
                new SoftwarePackageUploadViewModel() { LocalSoftwarePackageType = LocalSoftwarePackageType.AndroidMobileCompanion }))
                .Should().BeNull();
            (await ingest.IngestModulePackage(new MemoryStream(new byte[] { 1 }), "module.rar",
                new ModulePackageUploadViewModel() { Name = "x" }))
                .Should().BeNull();
            context.PackageArtifact.Count().Should().Be(0);
        }

        /// <summary>
        /// ROADMAP 15, the recorded remainder of the launch-chain item. The extension check alone
        /// let a renamed file through the WHOLE pipeline: stored, hashed, given an artifact row,
        /// upserted as a Module, granted an xAPI activity and listed in the catalog. The operator
        /// was told it succeeded, and the failure surfaced later on a learner's station as a launch
        /// that could not open. That is the silent-success family this audit exists to remove, with
        /// the report arriving at the wrong person days after the cause.
        /// </summary>
        [Fact]
        public async Task Ingest_RejectsAFileMerelyRENAMEDToZip_RatherThanAcceptingItOnTheExtension()
        {
            using DataDbContext context = BuildContext(nameof(Ingest_RejectsAFileMerelyRENAMEDToZip_RatherThanAcceptingItOnTheExtension));
            PackageIngestLogic ingest = BuildIngest(context);
            byte[] notAnArchive = System.Text.Encoding.UTF8.GetBytes("this is a text file with a lying name");

            (await ingest.IngestSoftwarePackage(new MemoryStream(notAnArchive), "companion-3.2.zip",
                new SoftwarePackageUploadViewModel() { LocalSoftwarePackageType = LocalSoftwarePackageType.AndroidMobileCompanion }))
                .Should().BeNull("the NAME said .zip but the BYTES are not an archive");
            (await ingest.IngestModulePackage(new MemoryStream(notAnArchive), "module.zip",
                new ModulePackageUploadViewModel() { Name = "x" }))
                .Should().BeNull();

            context.PackageArtifact.Count().Should().Be(0, "nothing may be stored or hashed for a payload that was refused");
            context.LocalSoftwarePackage.Count().Should().Be(0);
            context.Module.Count().Should().Be(0, "and no module row, so nothing appears in the catalog to be launched");
        }

        /// <summary>
        /// An archive with no entries is a real zip and still cannot carry a module. Refused at
        /// ingest, where it costs the operator one message, rather than at launch, where it costs a
        /// learner a session.
        /// </summary>
        [Fact]
        public async Task Ingest_RejectsAnEmptyArchive()
        {
            using DataDbContext context = BuildContext(nameof(Ingest_RejectsAnEmptyArchive));
            PackageIngestLogic ingest = BuildIngest(context);

            (await ingest.IngestModulePackage(new MemoryStream(EmptyZipBytes()), "module.zip",
                new ModulePackageUploadViewModel() { Name = "x" }))
                .Should().BeNull();
            context.PackageArtifact.Count().Should().Be(0);
        }

        /// <summary>
        /// The guard reads the stream to validate it, so it must REWIND. Without this the stored
        /// bytes would be empty or truncated and the recorded SHA-256 would describe nothing, which
        /// would be a worse defect than the one being fixed: a package that ingests "successfully"
        /// and downloads as garbage.
        /// </summary>
        [Fact]
        public async Task Ingest_StoresTheWholePayload_AfterReadingItToValidateTheArchive()
        {
            using DataDbContext context = BuildContext(nameof(Ingest_StoresTheWholePayload_AfterReadingItToValidateTheArchive));
            PackageIngestLogic ingest = BuildIngest(context);
            byte[] payload = ZipBytes("module.json", "{\"a\":1}");

            SoftwarePackageIngestResultViewModel result = await ingest.IngestSoftwarePackage(
                new MemoryStream(payload), "pkg.zip",
                new SoftwarePackageUploadViewModel() { Version = "1.0", LocalSoftwarePackageType = LocalSoftwarePackageType.PC });

            result.Should().NotBeNull();
            result.Artifact.ContentLength.Should().Be(payload.Length, "validation must not consume the payload");
            result.Artifact.Sha256.Should().Be(Sha256Hex(payload));
        }

        [Fact]
        public async Task CompanionVersionResolution_ReturnsTheStoredLatest_SkippingObsolete()
        {
            using DataDbContext context = BuildContext(nameof(CompanionVersionResolution_ReturnsTheStoredLatest_SkippingObsolete));
            // Explicit timestamps: on PostgreSQL the DB stamps CURRENT_TIMESTAMP; InMemory does not.
            context.LocalSoftwarePackage.AddRange(
                new LocalSoftwarePackage() { UUID = Guid.NewGuid(), Version = "3.0", LocalSoftwarePackageType = LocalSoftwarePackageType.AndroidMobileCompanion, TimeStamp = new DateTime(2026, 1, 1) },
                new LocalSoftwarePackage() { UUID = Guid.NewGuid(), Version = "3.1", LocalSoftwarePackageType = LocalSoftwarePackageType.AndroidMobileCompanion, TimeStamp = new DateTime(2026, 6, 1) },
                new LocalSoftwarePackage() { UUID = Guid.NewGuid(), Version = "3.2-bad", LocalSoftwarePackageType = LocalSoftwarePackageType.AndroidMobileCompanion, TimeStamp = new DateTime(2026, 7, 1), Obsolete = true },
                new LocalSoftwarePackage() { UUID = Guid.NewGuid(), Version = "9.9", LocalSoftwarePackageType = LocalSoftwarePackageType.PC, TimeStamp = new DateTime(2026, 7, 2) });
            context.SaveChanges();
            var accessor = new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var logic = new LocalSoftwarePackageLogic(accessor.Object, new LocalSoftwarePackageQueries(context), _storage);

            // The CompanionAppController.GetLatestVersion path: latest NON-obsolete of the kind,
            // resolved from the node's own catalog with zero HTTP.
            LocalSoftwarePackage latest = await logic.Get(LocalSoftwarePackageType.AndroidMobileCompanion, new Hardware() { IsLockedOut = false });
            latest.Should().NotBeNull();
            latest.Version.Should().Be("3.1");

            // Locked-out hardware still gets nothing (unchanged gate).
            (await logic.Get(LocalSoftwarePackageType.AndroidMobileCompanion, new Hardware() { IsLockedOut = true })).Should().BeNull();
        }

        [Fact]
        public async Task ModulePackage_Ingest_ThenEntitledDownload_StreamsFromTheNodeStore()
        {
            using DataDbContext context = BuildContext(nameof(ModulePackage_Ingest_ThenEntitledDownload_StreamsFromTheNodeStore));
            PackageIngestLogic ingest = BuildIngest(context);
            byte[] payload = ZipBytes("module.json", "module-zip-payload");
            Guid objectUuid = Guid.NewGuid();

            ModulePackageIngestResultViewModel result = await ingest.IngestModulePackage(
                new MemoryStream(payload),
                "welding-101.zip",
                new ModulePackageUploadViewModel()
                {
                    Name = "Welding 101",
                    Version = "1.0",
                    ObjectId = 11,
                    ObjectUUID = objectUuid
                });

            // Catalog + activity link + artifact.
            result.Should().NotBeNull();
            context.Module.Count().Should().Be(1);
            context.ModuleLinkedObject.Single().ObjectUUID.Should().Be(objectUuid);
            result.Artifact.StorageKey.Should().Be("modules/" + result.Module.UUID + ".zip");
            result.Artifact.Sha256.Should().Be(Sha256Hex(payload));

            // ROADMAP 15: a caller-supplied activity is honoured as-is. The mint is the FALLBACK
            // for the standalone node, so it must not overwrite a hub-authored Object.
            result.Link.Should().NotBeNull();
            result.Link.ObjectId.Should().Be(11);
            result.Link.ObjectUUID.Should().Be(objectUuid);

            // Entitle a hardware to the module, then download through the existing
            // HardwareLinkedModuleLogic gate -- the bytes must come from the node store
            // (IStorageProvider), NOT the legacy file handler.
            var hardwareRow = new LocalHardware() { UUID = Guid.NewGuid() };
            context.Hardware.Add(hardwareRow);
            context.SaveChanges();
            context.HardwareLinkedModule.Add(new LocalHardwareLinkedModule()
            {
                UUID = Guid.NewGuid(),
                Hardware = hardwareRow,
                HardwareUUID = hardwareRow.UUID,
                ModuleId = result.Module.Id,
                ModuleUUID = result.Module.UUID
            });
            context.SaveChanges();

            var accessor = new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new Microsoft.AspNetCore.Http.DefaultHttpContext());
            var fileHandler = new Moq.Mock<Febris.SharedServices.IModuleFileHandler>(Moq.MockBehavior.Strict);
            var logic = new HardwareLinkedModuleLogic(
                accessor.Object,
                new HardwareLinkedModuleQueries(context),
                fileHandler.Object,
                new Moq.Mock<IHardwareQueries>().Object,
                new ModuleQueries(context),
                new Moq.Mock<Febris.UserNode.LogicLayer.Logic.AnalyticsLogic.IModuleDownloadAnalyticsLogic>().Object,
                new PackageArtifactQueries(context),
                _storage,
                // SCBA-B3 port (hygiene D): null scope factory -> ScopedBackgroundWork's legacy
                // fallback runs the analytics write against the mock above; the download path
                // under test is unaffected.
                null);

            using Stream entitled = await logic.Download(new Hardware() { Id = hardwareRow.Id }, result.Module);
            entitled.Should().NotBeNull();
            using var buffer = new MemoryStream();
            await entitled.CopyToAsync(buffer);
            buffer.ToArray().Should().Equal(payload);
            fileHandler.Verify(f => f.Download(Moq.It.IsAny<Module>()), Moq.Times.Never,
                "a store-ingested package must stream through IStorageProvider, not the legacy file path");

            // No entitlement link -> no bytes (the tenant-local gate holds).
            (await logic.Download(new Hardware() { Id = hardwareRow.Id + 999 }, result.Module)).Should().BeNull();
        }

        [Fact]
        public async Task ModulePackage_IngestWithoutSuppliedActivity_MintsTheXApiObjectAndLinksIt()
        {
            // ROADMAP 15 / BUGS.md BLOCKER: the portal module form collects no ObjectId or
            // ObjectUUID, and before this the ingest linked an activity ONLY when the caller
            // supplied both -- so every node-authored module was downloadable and unlaunchable.
            // The node now mints the Object with the module. This is the regression pin: the
            // previous behaviour leaves ModuleLinkedObject empty and fails here.
            using DataDbContext context = BuildContext(nameof(ModulePackage_IngestWithoutSuppliedActivity_MintsTheXApiObjectAndLinksIt));
            using XApiDbContext xApiContext = BuildXApiContext(nameof(ModulePackage_IngestWithoutSuppliedActivity_MintsTheXApiObjectAndLinksIt));
            PackageIngestLogic ingest = BuildIngest(context, xApiContext);

            ModulePackageIngestResultViewModel result = await ingest.IngestModulePackage(
                new MemoryStream(ZipBytes("module.json", "module-zip-payload")),
                "welding-201.zip",
                new ModulePackageUploadViewModel()
                {
                    Name = "Welding 201",
                    Version = "1.0",
                    Description = "Advanced welding",
                    Language = LanguageMapTypeEnum.en_US,
                    XApiInteractionType = XApiInteractionType.performance
                });

            result.Should().NotBeNull();

            // The activity exists as a ROW, not just an in-memory instance -- that is the whole
            // point of the persist ObjectLogic had commented out.
            Febris.ModelLibrary.Models.XApiModels.Object activity = xApiContext.Object
                .Include(o => o.Definition)
                .Single();
            activity.Key.Should().NotBe(default(long));
            activity.ObjectType.Should().Be("Activity");
            activity.Id.Should().Be(new Uri("https://febr.is/Module/" + result.Module.UUID),
                "the IRI is derived from the module's stable UUID");

            // The xAPI 1.0.3 language map is built from the module's own metadata.
            activity.Definition.Should().NotBeNull();
            activity.Definition.Name["en-US"].Should().Be("Welding 201");
            activity.Definition.Description["en-US"].Should().Be("Advanced welding");
            activity.Definition.InteractionType.Should().Be("performance");

            // And the module points at it, which is what LauncherLogic resolves at launch time.
            // (ObjectUUID is db-generated by uuid_generate_v4 on PostgreSQL; the InMemory provider
            // treats that default as metadata only, so only the surrogate key is asserted here.)
            ModuleLinkedObject link = context.ModuleLinkedObject.Single();
            link.ModuleUUID.Should().Be(result.Module.UUID);
            link.ObjectId.Should().Be(activity.Key);
            result.Link.Should().NotBeNull();
            result.Link.ObjectId.Should().Be(activity.Key);
        }

        [Fact]
        public async Task ModulePackage_ReingestWithoutSuppliedActivity_KeepsOneActivity_NotTwo()
        {
            // Re-ingesting the same UUID replaces the package bytes. The activity IRI is derived
            // from that same stable UUID, so a second mint would register a DUPLICATE Object under
            // an identical IRI and leave the link pointing at whichever won.
            using DataDbContext context = BuildContext(nameof(ModulePackage_ReingestWithoutSuppliedActivity_KeepsOneActivity_NotTwo));
            using XApiDbContext xApiContext = BuildXApiContext(nameof(ModulePackage_ReingestWithoutSuppliedActivity_KeepsOneActivity_NotTwo));
            PackageIngestLogic ingest = BuildIngest(context, xApiContext);
            Guid uuid = Guid.NewGuid();

            ModulePackageIngestResultViewModel first = await ingest.IngestModulePackage(
                new MemoryStream(ZipBytes("module.json", "v1")),
                "welding-201.zip",
                new ModulePackageUploadViewModel() { UUID = uuid, Name = "Welding 201", Version = "1.0" });
            ModulePackageIngestResultViewModel second = await ingest.IngestModulePackage(
                new MemoryStream(ZipBytes("module.json", "v2-bigger-payload")),
                "welding-201.zip",
                new ModulePackageUploadViewModel() { UUID = uuid, Name = "Welding 201", Version = "1.1" });

            xApiContext.Object.Count().Should().Be(1, "the module's activity is minted once and reused");
            context.ModuleLinkedObject.Count().Should().Be(1);
            second.Module.Version.Should().Be("1.1", "the catalog row still updates in place");
            second.Link.ObjectId.Should().Be(first.Link.ObjectId, "the module keeps the activity it launched with");
        }
    }
}
