// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    /// <summary>
    /// Read/write surface for the node's artifact bookkeeping rows. An
    /// artifact row exists for every binary ingested through IStorageProvider (module .zips and
    /// client-software packages) and records the storage key, SHA-256, and length; its existence
    /// for a catalog item's conventional key marks that item as store-ingested.
    /// </summary>
    public interface IPackageArtifactQueries
    {
        /// <summary>Resolve the artifact row for a storage key. Null when the key was never ingested.</summary>
        Task<PackageArtifact> GetByStorageKey(string storageKey);

        /// <summary>
        /// Create-or-update by storage key: re-ingesting a key updates the checksum/length/source
        /// row in place (the stored object was overwritten). Returns the persisted row.
        /// </summary>
        Task<PackageArtifact> Upsert(PackageArtifact input);
    }

    /// <summary>
    /// Node-local EF implementation of <see cref="IPackageArtifactQueries"/> over the tenant
    /// DataDbContext. New DAL code under the node's DI rules: DI-scoped context,
    /// provider-clean LINQ, each method a complete unit of work.
    /// </summary>
    public class PackageArtifactQueries : IPackageArtifactQueries
    {
        private readonly DataDbContext _dataDbContext;

        // DI refactor
        public PackageArtifactQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }

        public PackageArtifactQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        /// <inheritdoc />
        public async Task<PackageArtifact> GetByStorageKey(string storageKey)
        {
            PackageArtifact output = null;
            try
            {
                output = await _dataDbContext.PackageArtifact
                    .AsNoTracking()
                    .Where(i => i.StorageKey == storageKey)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        /// <inheritdoc />
        public async Task<PackageArtifact> Upsert(PackageArtifact input)
        {
            try
            {
                PackageArtifact existing = await _dataDbContext.PackageArtifact
                    .Where(i => i.StorageKey == input.StorageKey)
                    .FirstOrDefaultAsync();
                if (existing == null)
                {
                    await _dataDbContext.PackageArtifact.AddAsync(input);
                    await _dataDbContext.SaveChangesAsync();
                    return input;
                }

                existing.Sha256 = input.Sha256;
                existing.ContentLength = input.ContentLength;
                existing.SourceFileName = input.SourceFileName;
                await _dataDbContext.SaveChangesAsync();
                return existing;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
    }
}
