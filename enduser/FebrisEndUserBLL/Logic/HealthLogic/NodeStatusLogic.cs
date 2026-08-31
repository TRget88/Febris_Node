// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.SharedServices.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.HealthLogic
{
    /// <summary>
    /// Node health site (sub-slice 2): the aggregation behind the Portal status
    /// page. One call assembles the operator's at-a-glance answer -- the live health report
    /// (through <see cref="HealthCheckService"/>, i.e. exactly the checks
    /// <c>AddNodeHealthChecks</c> registered for this host), the node's local identity, the node
    /// software version, the latest installed client-software package per type (with the stored
    /// bytes' SHA-256 from the artifact bookkeeping), artifact-store disk usage, and the
    /// hub-federation gate state.
    /// </summary>
    public interface INodeStatusLogic
    {
        /// <summary>Take a full status snapshot (runs every registered health check).</summary>
        Task<NodeStatusViewModel> GetStatus();
    }

    /// <summary>
    /// DI-only implementation: greenfield node code, deliberately NO legacy
    /// self-newing constructor -- every collaborator is the same DI-registered seam the request
    /// paths use (the health service, the convention-registered local queries, the storage seam,
    /// the federation gate). Registered by the Portal host; gated by the marker-pattern
    /// DI-resolution test alongside the health registrations.
    /// </summary>
    public class NodeStatusLogic : INodeStatusLogic
    {
        private readonly HealthCheckService _healthCheckService;
        private readonly INodeIdentityQueries _nodeIdentityContext;
        private readonly ILocalSoftwarePackageQueries _softwarePackageContext;
        private readonly IPackageArtifactQueries _artifactContext;
        private readonly IStorageProvider _storage;
        private readonly StorageOptions _storageOptions;
        private readonly IHubFederationSettings _federation;

        /// <summary>DI constructor (the only one).</summary>
        public NodeStatusLogic(
            HealthCheckService healthCheckService,
            INodeIdentityQueries nodeIdentityContext,
            ILocalSoftwarePackageQueries softwarePackageContext,
            IPackageArtifactQueries artifactContext,
            IStorageProvider storage,
            StorageOptions storageOptions,
            IHubFederationSettings federation)
        {
            _healthCheckService = healthCheckService;
            _nodeIdentityContext = nodeIdentityContext;
            _softwarePackageContext = softwarePackageContext;
            _artifactContext = artifactContext;
            _storage = storage;
            _storageOptions = storageOptions;
            _federation = federation;
        }

        /// <inheritdoc />
        public async Task<NodeStatusViewModel> GetStatus()
        {
            HealthReport report = await _healthCheckService.CheckHealthAsync();
            NodeIdentity identity = await _nodeIdentityContext.Get();

            NodeStatusViewModel output = new NodeStatusViewModel()
            {
                OverallStatus = report.Status.ToString(),
                Components = MapComponents(report),
                NodeName = identity?.Name,
                InstitutionUUID = identity?.InstitutionUUID,
                NodeVersion = ResolveNodeVersion(),
                InstalledPackages = await ListInstalledPackages(),
                StorageUsage = ReadStorageUsage(),
                // The boolean ONLY -- endpoints and the license key stay off the page.
                HubFederationEnabled = _federation.Enabled,
                GeneratedAtUtc = DateTime.UtcNow
            };
            return output;
        }

        /// <summary>Flatten the health report into the page's plain component rows.</summary>
        private static List<NodeStatusComponentViewModel> MapComponents(HealthReport report)
        {
            List<NodeStatusComponentViewModel> components = new List<NodeStatusComponentViewModel>();
            foreach (KeyValuePair<string, HealthReportEntry> entry in report.Entries)
            {
                components.Add(new NodeStatusComponentViewModel()
                {
                    Name = entry.Key,
                    Status = entry.Value.Status.ToString(),
                    // Same secret discipline as the anonymous endpoints (defense in depth even
                    // though this page is admin-only): a THROWN check's entry carries the raw
                    // exception message as its description -- reduce it to the type name.
                    Description = entry.Value.Exception != null
                        ? "probe threw (" + entry.Value.Exception.GetType().Name + ")"
                        : entry.Value.Description,
                    DurationMs = (long)entry.Value.Duration.TotalMilliseconds
                });
            }
            return components;
        }

        /// <summary>
        /// The node software version: the entry assembly's informational version (the deployed
        /// host is the node), falling back to this logic assembly's own when there is no entry
        /// assembly version (unit-test runners).
        /// </summary>
        private static string ResolveNodeVersion()
        {
            string version = InformationalVersionOf(Assembly.GetEntryAssembly())
                ?? InformationalVersionOf(typeof(NodeStatusLogic).Assembly);
            return version ?? "unknown";
        }

        /// <summary>Informational version of <paramref name="assembly"/>, or null.</summary>
        private static string InformationalVersionOf(Assembly assembly)
        {
            string version = assembly?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }

        /// <summary>
        /// The LATEST active package per <see cref="LocalSoftwarePackageType"/> (the same
        /// "newest non-obsolete" resolution the companion/launcher version endpoint uses),
        /// joined to its artifact row via the ingest key convention
        /// (<c>localsoftwarepackage/{uuid}.zip</c>) for the stored bytes' SHA-256. Types with no
        /// package simply do not appear.
        /// </summary>
        private async Task<List<NodeStatusPackageViewModel>> ListInstalledPackages()
        {
            List<NodeStatusPackageViewModel> packages = new List<NodeStatusPackageViewModel>();
            foreach (LocalSoftwarePackageType packageType in Enum.GetValues<LocalSoftwarePackageType>())
            {
                if (packageType == LocalSoftwarePackageType.None)
                {
                    continue;
                }

                LocalSoftwarePackage package = await _softwarePackageContext.Get(packageType);
                if (package == null)
                {
                    continue;
                }

                PackageArtifact artifact = await _artifactContext.GetByStorageKey(
                    StorageKeys.SoftwarePackage(package.UUID.ToString() + ".zip"));

                packages.Add(new NodeStatusPackageViewModel()
                {
                    PackageType = packageType.ToString(),
                    Name = package.Name,
                    Version = package.Version,
                    Sha256 = artifact?.Sha256,   // null (page shows n/a) when never store-ingested
                    UploadedUtc = package.TimeStamp
                });
            }
            return packages;
        }

        /// <summary>
        /// Disk usage of the volume hosting the store's base path -- only meaningful for the
        /// filesystem backend. Every other outcome (S3 backend, blank base path, missing
        /// directory, failed volume query) is the graceful <c>HasUsage=false</c> "n/a" answer,
        /// never a throw: the status page must render on a misconfigured node, that is when the
        /// operator needs it most.
        /// </summary>
        private NodeStorageUsageViewModel ReadStorageUsage()
        {
            NodeStorageUsageViewModel usage = new NodeStorageUsageViewModel()
            {
                HasUsage = false,
                ProviderKind = _storage.Kind.ToString()
            };

            if (_storage.Kind != StorageProviderKind.FileSystem)
            {
                return usage;
            }
            string basePath = _storageOptions?.BasePath;
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return usage;
            }

            try
            {
                string fullPath = Path.GetFullPath(basePath);
                if (!Directory.Exists(fullPath))
                {
                    return usage;
                }

                DriveInfo volume = new DriveInfo(fullPath);
                usage.TotalBytes = volume.TotalSize;
                usage.AvailableBytes = volume.AvailableFreeSpace;
                usage.UsedBytes = volume.TotalSize - volume.TotalFreeSpace;
                usage.HasUsage = true;
            }
            catch (Exception ex)
            {
                // Graceful n/a -- log it, keep the page up.
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return usage;
        }
    }
}
