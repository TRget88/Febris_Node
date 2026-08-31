// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.HealthLogic;
using Febris.SharedServices.Storage;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Node health site sub-slice 2: pins the Portal status page's aggregation
    /// logic with mocks -- health-report flattening (incl. the exception-description scrub),
    /// latest-package-per-type listing with the artifact-checksum join on the ingest key
    /// convention, the storage-usage graceful "n/a" path vs a real filesystem measurement, and
    /// the identity/gate pass-through (the boolean only, never hub endpoints or keys).
    /// </summary>
    public class NodeStatusLogicTests
    {
        private static Mock<HealthCheckService> HealthServiceReturning(HealthReport report)
        {
            var service = new Mock<HealthCheckService>();
            service.Setup(s => s.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(report);
            return service;
        }

        private static HealthReport ReportWith(params (string Name, HealthStatus Status, string Description, Exception Exception)[] entries)
        {
            var dictionary = new Dictionary<string, HealthReportEntry>();
            foreach ((string name, HealthStatus status, string description, Exception exception) in entries)
            {
                dictionary[name] = new HealthReportEntry(status, description, TimeSpan.FromMilliseconds(7), exception, null);
            }
            return new HealthReport(dictionary, TimeSpan.FromMilliseconds(21));
        }

        private static Mock<IStorageProvider> StorageOfKind(StorageProviderKind kind)
        {
            var storage = new Mock<IStorageProvider>();
            storage.SetupGet(s => s.Kind).Returns(kind);
            return storage;
        }

        private static NodeStatusLogic BuildLogic(
            HealthReport report = null,
            NodeIdentity identity = null,
            IDictionary<LocalSoftwarePackageType, LocalSoftwarePackage> packages = null,
            IDictionary<string, PackageArtifact> artifacts = null,
            StorageProviderKind storageKind = StorageProviderKind.FileSystem,
            string basePath = null,
            bool federationEnabled = false)
        {
            var identityQueries = new Mock<INodeIdentityQueries>();
            identityQueries.Setup(q => q.Get()).ReturnsAsync(identity);

            var packageQueries = new Mock<ILocalSoftwarePackageQueries>();
            packageQueries.Setup(q => q.Get(It.IsAny<LocalSoftwarePackageType>()))
                .ReturnsAsync((LocalSoftwarePackageType type) =>
                    packages != null && packages.TryGetValue(type, out LocalSoftwarePackage package) ? package : null);

            var artifactQueries = new Mock<IPackageArtifactQueries>();
            artifactQueries.Setup(q => q.GetByStorageKey(It.IsAny<string>()))
                .ReturnsAsync((string key) =>
                    artifacts != null && artifacts.TryGetValue(key, out PackageArtifact artifact) ? artifact : null);

            return new NodeStatusLogic(
                HealthServiceReturning(report ?? ReportWith()).Object,
                identityQueries.Object,
                packageQueries.Object,
                artifactQueries.Object,
                StorageOfKind(storageKind).Object,
                new StorageOptions() { Provider = storageKind, BasePath = basePath },
                new HubFederationSettings() { Enabled = federationEnabled });
        }

        [Fact]
        public async Task Status_AggregatesHealthReport_IntoOverallAndPerComponentRows()
        {
            HealthReport report = ReportWith(
                ("database-data", HealthStatus.Healthy, "database reachable", null),
                ("storage", HealthStatus.Degraded, "storage round-trip slow: 2400 ms (FileSystem)", null));
            NodeStatusLogic logic = BuildLogic(report: report);

            NodeStatusViewModel status = await logic.GetStatus();

            status.OverallStatus.Should().Be(nameof(HealthStatus.Degraded), "the report's aggregate verdict is the page's headline");
            status.Components.Should().HaveCount(2);
            NodeStatusComponentViewModel storageRow = status.Components.Single(c => c.Name == "storage");
            storageRow.Status.Should().Be(nameof(HealthStatus.Degraded));
            storageRow.Description.Should().Contain("slow");
            storageRow.DurationMs.Should().Be(7);
        }

        [Fact]
        public async Task Status_ScrubsExceptionDerivedDescriptions_ToTheTypeName()
        {
            // When a check THROWS, the health service stores the raw exception message as the
            // entry description -- same scrub as the anonymous endpoints, defense in depth.
            const string leaky = "Failed to connect: Host=db-internal.example;Password=super-secret";
            HealthReport report = ReportWith(
                ("database-data", HealthStatus.Unhealthy, leaky, new InvalidOperationException(leaky)));
            NodeStatusLogic logic = BuildLogic(report: report);

            NodeStatusViewModel status = await logic.GetStatus();

            NodeStatusComponentViewModel row = status.Components.Single();
            row.Description.Should().Contain(nameof(InvalidOperationException));
            row.Description.Should().NotContainAny("super-secret", "db-internal.example");
        }

        [Fact]
        public async Task Status_CarriesNodeIdentity_Version_AndGateState()
        {
            Guid institution = Guid.NewGuid();
            NodeStatusLogic logic = BuildLogic(
                identity: new NodeIdentity() { Name = "Northgate High", InstitutionUUID = institution },
                federationEnabled: true);

            NodeStatusViewModel status = await logic.GetStatus();

            status.NodeName.Should().Be("Northgate High");
            status.InstitutionUUID.Should().Be(institution);
            status.NodeVersion.Should().NotBeNullOrWhiteSpace("the page always shows a version string");
            status.HubFederationEnabled.Should().BeTrue();
        }

        [Fact]
        public async Task Status_UnprovisionedIdentity_RendersNulls_NotThrows()
        {
            NodeStatusLogic logic = BuildLogic(identity: null);

            NodeStatusViewModel status = await logic.GetStatus();

            status.NodeName.Should().BeNull();
            status.InstitutionUUID.Should().BeNull();
        }

        [Fact]
        public async Task Packages_ListsLatestPerType_JoiningTheArtifactChecksum_ByIngestKeyConvention()
        {
            Guid companionUuid = Guid.NewGuid();
            Guid pcUuid = Guid.NewGuid();
            var packages = new Dictionary<LocalSoftwarePackageType, LocalSoftwarePackage>()
            {
                [LocalSoftwarePackageType.AndroidMobileCompanion] = new LocalSoftwarePackage()
                {
                    UUID = companionUuid,
                    Name = "Mobile Companion",
                    Version = "3.2",
                    LocalSoftwarePackageType = LocalSoftwarePackageType.AndroidMobileCompanion,
                    TimeStamp = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc)
                },
                [LocalSoftwarePackageType.PC] = new LocalSoftwarePackage()
                {
                    UUID = pcUuid,
                    Name = "PC Launcher",
                    Version = "1.9",
                    LocalSoftwarePackageType = LocalSoftwarePackageType.PC,
                    TimeStamp = new DateTime(2026, 6, 15, 8, 30, 0, DateTimeKind.Utc)
                },
            };
            var artifacts = new Dictionary<string, PackageArtifact>()
            {
                // Only the companion was store-ingested; the PC row predates the artifact store.
                ["localsoftwarepackage/" + companionUuid + ".zip"] = new PackageArtifact()
                {
                    StorageKey = "localsoftwarepackage/" + companionUuid + ".zip",
                    Sha256 = "abc123def456"
                },
            };
            NodeStatusLogic logic = BuildLogic(packages: packages, artifacts: artifacts);

            NodeStatusViewModel status = await logic.GetStatus();

            status.InstalledPackages.Should().HaveCount(2, "types with no package do not appear");
            NodeStatusPackageViewModel companion = status.InstalledPackages
                .Single(p => p.PackageType == nameof(LocalSoftwarePackageType.AndroidMobileCompanion));
            companion.Version.Should().Be("3.2");
            companion.Sha256.Should().Be("abc123def456", "the checksum joins on the ingest key convention");
            companion.UploadedUtc.Should().Be(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
            NodeStatusPackageViewModel pc = status.InstalledPackages
                .Single(p => p.PackageType == nameof(LocalSoftwarePackageType.PC));
            pc.Sha256.Should().BeNull("a never-ingested catalog row has no artifact checksum (the page shows n/a)");
        }

        [Fact]
        public async Task Packages_EmptyStore_YieldsEmptyList()
        {
            NodeStatusLogic logic = BuildLogic(packages: null);

            NodeStatusViewModel status = await logic.GetStatus();

            status.InstalledPackages.Should().BeEmpty();
        }

        [Fact]
        public async Task StorageUsage_FileSystemBackend_MeasuresTheBasePathVolume()
        {
            string root = Path.Combine(Path.GetTempPath(), "febris-node-status-usage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                NodeStatusLogic logic = BuildLogic(storageKind: StorageProviderKind.FileSystem, basePath: root);

                NodeStatusViewModel status = await logic.GetStatus();

                status.StorageUsage.HasUsage.Should().BeTrue();
                status.StorageUsage.ProviderKind.Should().Be(nameof(StorageProviderKind.FileSystem));
                status.StorageUsage.TotalBytes.Should().BePositive();
                status.StorageUsage.AvailableBytes.Should().BeInRange(0, status.StorageUsage.TotalBytes);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public async Task StorageUsage_NonFilesystemBackend_IsGracefullyNotAvailable()
        {
            NodeStatusLogic logic = BuildLogic(storageKind: StorageProviderKind.S3, basePath: null);

            NodeStatusViewModel status = await logic.GetStatus();

            status.StorageUsage.HasUsage.Should().BeFalse("DriveInfo has no meaning for an object store");
            status.StorageUsage.ProviderKind.Should().Be(nameof(StorageProviderKind.S3));
        }

        [Fact]
        public async Task StorageUsage_FilesystemWithMissingBasePath_IsGracefullyNotAvailable()
        {
            NodeStatusLogic logic = BuildLogic(
                storageKind: StorageProviderKind.FileSystem,
                basePath: Path.Combine(Path.GetTempPath(), "febris-does-not-exist-" + Guid.NewGuid().ToString("N")));

            NodeStatusViewModel status = await logic.GetStatus();

            status.StorageUsage.HasUsage.Should().BeFalse("a not-yet-created store directory must render n/a, not throw");
        }
    }
}
