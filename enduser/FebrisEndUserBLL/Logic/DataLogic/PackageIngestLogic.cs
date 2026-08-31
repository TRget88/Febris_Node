// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.SharedServices.Storage;
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    /// <summary>
    /// The node's package-ingest path (delivery-path severance): the write side
    /// the tenant tier never had. Module <c>.zip</c>s and client-software packages (mobile Server
    /// APK, Companion APK, PC launcher installer, integration SDKs -- zip-wrapped, matching the
    /// legacy central handler's .zip-only contract) stream into the node's own store through
    /// <c>IStorageProvider</c>; the SHA-256 of the STORED bytes is recorded on a
    /// <see cref="PackageArtifact"/> row (write-then-verify: hashing re-reads the store, so the
    /// recorded checksum describes what the node will actually serve), and the local catalog row
    /// (Module / LocalSoftwarePackage) is created or updated. Built DI-only against the storage
    /// seam -- no statics, no legacy path fallbacks.
    /// </summary>
    public interface IPackageIngestLogic
    {
        /// <summary>
        /// Ingest one module package: store the .zip at <c>modules/{uuid}.zip</c>, record its
        /// artifact row, upsert the local Module catalog row, and link the module to its xAPI
        /// activity so it can launch. The linkage is taken from the metadata when the caller
        /// supplies it (hub-authored Objects); otherwise the node MINTS the Object with the
        /// module (ROADMAP 15 owner ruling). Returns null when the payload is missing or not a
        /// .zip.
        /// </summary>
        Task<ModulePackageIngestResultViewModel> IngestModulePackage(Stream content, string sourceFileName, ModulePackageUploadViewModel metadata);

        /// <summary>
        /// Ingest one client-software package: store the .zip at
        /// <c>localsoftwarepackage/{uuid}.zip</c>, record its artifact row, and upsert the local
        /// LocalSoftwarePackage catalog row. Returns null when the payload is missing or not a .zip.
        /// </summary>
        Task<SoftwarePackageIngestResultViewModel> IngestSoftwarePackage(Stream content, string sourceFileName, SoftwarePackageUploadViewModel metadata);
    }

    /// <summary>
    /// DI-only implementation of <see cref="IPackageIngestLogic"/>. Greenfield node code: unlike
    /// the strangler-era logic classes there is deliberately NO legacy self-newing constructor --
    /// the storage seam cannot be newed from static config, and node code is DI-only by rule.
    /// </summary>
    public class PackageIngestLogic : IPackageIngestLogic
    {
        private readonly IStorageProvider _storage;
        private readonly IModuleQueries _moduleContext;
        private readonly IModuleLinkedObjectQueries _moduleLinkedObjectContext;
        private readonly ILocalSoftwarePackageQueries _softwarePackageContext;
        private readonly IPackageArtifactQueries _artifactContext;
        private readonly IObjectLogic _objectContext;

        public PackageIngestLogic(
            IStorageProvider storage,
            IModuleQueries moduleContext,
            IModuleLinkedObjectQueries moduleLinkedObjectContext,
            ILocalSoftwarePackageQueries softwarePackageContext,
            IPackageArtifactQueries artifactContext,
            IObjectLogic objectContext)
        {
            _storage = storage;
            _moduleContext = moduleContext;
            _moduleLinkedObjectContext = moduleLinkedObjectContext;
            _softwarePackageContext = softwarePackageContext;
            _artifactContext = artifactContext;
            _objectContext = objectContext;
        }

        /// <inheritdoc />
        public async Task<ModulePackageIngestResultViewModel> IngestModulePackage(Stream content, string sourceFileName, ModulePackageUploadViewModel metadata)
        {
            try
            {
                if (content == null || metadata == null || !IsZip(sourceFileName) || !IsReadableArchive(content))
                {
                    return null;
                }

                Guid uuid = metadata.UUID ?? Guid.NewGuid();
                string storageKey = StorageKeys.Module(uuid.ToString() + ".zip");
                PackageArtifact artifact = await StoreAndRecord(storageKey, content, sourceFileName);

                Module module = await _moduleContext.Upsert(new Module()
                {
                    UUID = uuid,
                    Name = metadata.Name,
                    Version = metadata.Version,
                    Description = metadata.Description,
                    Language = metadata.Language,
                    XApiInteractionType = metadata.XApiInteractionType,
                    MainSectionCount = metadata.MainSectionCount,
                    TotalSectionCount = metadata.TotalSectionCount,
                    InteractionComponents = metadata.InteractionComponents,
                    EstimatedCompletionTime = metadata.EstimatedCompletionTime
                });

                (ModuleLinkedObject link, string linkStatus) = await LinkActivity(module, metadata);

                return new ModulePackageIngestResultViewModel()
                {
                    Module = module,
                    Artifact = artifact,
                    Link = link,
                    StatusMessage = linkStatus
                };
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<SoftwarePackageIngestResultViewModel> IngestSoftwarePackage(Stream content, string sourceFileName, SoftwarePackageUploadViewModel metadata)
        {
            try
            {
                if (content == null || metadata == null || !IsZip(sourceFileName) || !IsReadableArchive(content))
                {
                    return null;
                }

                Guid uuid = metadata.UUID ?? Guid.NewGuid();
                string storageKey = StorageKeys.SoftwarePackage(uuid.ToString() + ".zip");
                PackageArtifact artifact = await StoreAndRecord(storageKey, content, sourceFileName);

                LocalSoftwarePackage package = await _softwarePackageContext.Upsert(new LocalSoftwarePackage()
                {
                    UUID = uuid,
                    Name = metadata.Name,
                    Version = metadata.Version,
                    Description = metadata.Description,
                    LocalSoftwarePackageType = metadata.LocalSoftwarePackageType,
                    Language = metadata.Language,
                    Obsolete = metadata.Obsolete
                });

                return new SoftwarePackageIngestResultViewModel()
                {
                    LocalSoftwarePackage = package,
                    Artifact = artifact
                };
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// Give the module the xAPI activity link it needs to launch (ROADMAP 15).
        ///
        /// <para>
        /// Three cases, in order. (1) The caller supplied an Object -- the hub-era shape, where the
        /// activity was authored elsewhere and arrived with the package. Link what was given.
        /// (2) The module is already linked -- a re-ingest replacing the package bytes. Keep the
        /// existing activity: its IRI is derived from the module's stable UUID, so re-minting would
        /// register a SECOND Object under the same IRI. (3) Otherwise the node mints the activity
        /// with the module, which is the standalone node's only source of one.
        /// </para>
        ///
        /// <para>
        /// A mint that fails returns null rather than writing a link to a nonexistent Object: an
        /// Id-0 link row would make <c>LauncherLogic</c> resolve a null activity at launch time,
        /// which is a worse failure than the caller being told the module is unlinked.
        /// </para>
        /// </summary>
        private async Task<(ModuleLinkedObject Link, string StatusMessage)> LinkActivity(Module module, ModulePackageUploadViewModel metadata)
        {
            if (metadata.ObjectId.HasValue && metadata.ObjectUUID.HasValue)
            {
                return (await _moduleLinkedObjectContext.Upsert(new ModuleLinkedObject()
                {
                    UUID = Guid.NewGuid(),
                    Module = module,
                    ModuleUUID = module.UUID,
                    ObjectId = metadata.ObjectId.Value,
                    ObjectUUID = metadata.ObjectUUID.Value
                }), null);
            }

            ModuleLinkedObject existing = await _moduleLinkedObjectContext.GetByModule(module.UUID);
            if (existing != null)
            {
                return (existing, null);
            }

            // ObjectLogic.Create builds the Activity from the module: IRI from
            // StaticDetails.xApiObjectUri + Module.UUID, the xAPI 1.0.3 language maps from
            // Module.Language/Name/Description, and the interaction type from
            // Module.XApiInteractionType. Every input it needs is already on the catalog row this
            // ingest just wrote.
            (ModelLibrary.Models.XApiModels.Object xApiObject, string statusMessage) =
                await _objectContext.Create(new ModuleCreationViewModel() { Module = module });

            if (xApiObject == null || xApiObject.Key == default)
            {
                // ObjectLogic swallows its own exceptions and returns a blank Object, so an unset
                // Key is what a failed mint looks like from here.
                string reason = "The module was ingested but its xAPI activity could not be created, so it cannot launch yet. " + statusMessage;
                Febris.SharedServices.FebrisLog.ErrorMessage(
                    "PackageIngestLogic: module " + module.UUID + " was ingested but its xAPI activity could not be minted, so it cannot launch. " + statusMessage);
                return (null, reason);
            }

            ModuleLinkedObject link = await _moduleLinkedObjectContext.Upsert(new ModuleLinkedObject()
            {
                UUID = Guid.NewGuid(),
                Module = module,
                ModuleUUID = module.UUID,
                ObjectId = xApiObject.Key,
                ObjectUUID = xApiObject.UUID
            });

            // A null here means the Activity exists but the link row did not persist. Re-ingesting
            // repairs it, but say so rather than reporting a launchable module.
            return (link, link == null
                ? "The module was ingested and its xAPI activity created, but the link between them did not persist, so it cannot launch yet."
                : null);
        }

        /// <summary>
        /// .zip-only gate, mirroring the legacy FileServerHandler upload handlers' acceptable-type
        /// check (APKs/installers/SDKs ship zip-wrapped, and the existing client download paths
        /// already expect that).
        /// </summary>
        private static bool IsZip(string sourceFileName)
        {
            string extension = Path.GetExtension(sourceFileName ?? string.Empty);
            return string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// ROADMAP 15: the BYTES have to be an archive, not just the name.
        ///
        /// <see cref="IsZip"/> alone reads the filename, so a renamed .txt used to ingest cleanly:
        /// it was written to the store, hashed, given a PackageArtifact row, upserted as a Module,
        /// minted an xAPI Activity and listed in the catalog. The operator was told it worked. The
        /// failure surfaced later, on a learner's station, as a launch that could not open.
        ///
        /// This opens the stream as a zip and requires at least one entry. An empty archive is
        /// refused because it cannot carry a module, and refusing it here is cheaper than a launch
        /// failure. The stream is rewound afterwards so the caller can still store it, and
        /// leaveOpen is set because <see cref="ZipArchive"/> otherwise disposes the caller's stream.
        ///
        /// NOT a manifest check. What a module .zip must CONTAIN is an open contract question for
        /// the owner. This is the floor: it is a real archive with something in it.
        /// </summary>
        private static bool IsReadableArchive(Stream content)
        {
            if (content == null || !content.CanRead || !content.CanSeek)
            {
                // A non-seekable stream cannot be validated and then re-read for storage. Both
                // callers pass a seekable stream, so this is a guard against a future caller
                // rather than a live path, and it fails CLOSED.
                return false;
            }

            long origin = content.Position;
            try
            {
                using (ZipArchive archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true))
                {
                    return archive.Entries.Count > 0;
                }
            }
            catch (InvalidDataException)
            {
                return false;
            }
            finally
            {
                content.Position = origin;
            }
        }

        /// <summary>
        /// Write the payload to the store, then re-read the STORED bytes to compute the SHA-256
        /// (so the recorded checksum verifies what the node will serve, not just what was sent),
        /// and upsert the artifact bookkeeping row.
        /// </summary>
        private async Task<PackageArtifact> StoreAndRecord(string storageKey, Stream content, string sourceFileName)
        {
            await _storage.WriteAsync(storageKey, content);

            string sha256;
            using (Stream stored = await _storage.OpenReadAsync(storageKey))
            using (SHA256 hasher = SHA256.Create())
            {
                byte[] hash = await hasher.ComputeHashAsync(stored);
                sha256 = Convert.ToHexString(hash).ToLowerInvariant();
            }

            long length = await _storage.GetLengthAsync(storageKey);

            return await _artifactContext.Upsert(new PackageArtifact()
            {
                UUID = Guid.NewGuid(),
                StorageKey = storageKey,
                Sha256 = sha256,
                ContentLength = length,
                SourceFileName = sourceFileName
            });
        }
    }
}
