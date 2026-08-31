// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    /// <summary>
    /// Store surface for the node's single <see cref="HubFederationConfig"/> row.
    /// Both members deal in PLAINTEXT license keys at the seam: <see cref="Get"/> returns the
    /// decrypted key and <see cref="Save"/> encrypts before persisting, so the protected payload
    /// never leaks above the DAL and encrypt/decrypt live in exactly one place.
    /// </summary>
    public interface IHubFederationConfigQueries
    {
        /// <summary>The single settings row with <c>LicenseKey</c> DECRYPTED, or null when the
        /// operator never saved one. A payload that no longer unprotects (rotated/foreign key
        /// ring) comes back with a null LicenseKey rather than throwing -- endpoints and the
        /// Enabled switch still govern.</summary>
        Task<HubFederationConfig> Get();

        /// <summary>
        /// Create-or-update the single row (first row wins; single-row semantics like
        /// NodeIdentity). <paramref name="input"/> carries the PLAINTEXT license key (or
        /// null/empty for "no key"); it is protected with the dedicated purpose string before it
        /// touches the database. Stamps <c>UpdatedAt</c> (UTC). Returns the persisted row with
        /// the key DECRYPTED again, mirroring <see cref="Get"/>.
        /// </summary>
        Task<HubFederationConfig> Save(HubFederationConfig input);
    }

    /// <summary>
    /// DI-only implementation of <see cref="IHubFederationConfigQueries"/>:
    /// greenfield node code, so deliberately NO legacy self-newing constructor -- the protector
    /// cannot be newed from static config. Swept into DI by the
    /// <c>AddFebrisUserNodeDataAccess</c> naming convention.
    ///
    /// <para>
    /// Encryption at rest: the license key column stores the payload of an
    /// <see cref="IDataProtector"/> created from the host's DataProtection stack (both EndUser
    /// hosts persist a file-system key ring under <c>SetApplicationName("Febris.UserAuth")</c>,
    /// so Portal-written rows unprotect on the API host and vice versa). The purpose string is
    /// DEDICATED (<see cref="ProtectorPurpose"/>) so this ciphertext is cryptographically
    /// isolated from every other protector the hosts create.
    /// </para>
    /// </summary>
    public class HubFederationConfigQueries : IHubFederationConfigQueries
    {
        /// <summary>Dedicated protector purpose for the license-key column. Changing this string
        /// orphans every previously stored key -- treat it as part of the schema.</summary>
        public const string ProtectorPurpose = "Febris.Node.HubFederation.LicenseKey.v1";

        private readonly DataDbContext _dataDbContext;
        private readonly IDataProtector _protector;

        /// <summary>DI constructor (the only one).</summary>
        public HubFederationConfigQueries(DataDbContext dataDbContext, IDataProtectionProvider dataProtectionProvider)
        {
            _dataDbContext = dataDbContext;
            _protector = dataProtectionProvider?.CreateProtector(ProtectorPurpose)
                ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
        }

        /// <inheritdoc />
        public async Task<HubFederationConfig> Get()
        {
            try
            {
                HubFederationConfig row = await _dataDbContext.HubFederationConfig
                    .AsNoTracking()
                    .OrderBy(r => r.Id)
                    .FirstOrDefaultAsync();
                if (row != null)
                {
                    row.LicenseKey = Unprotect(row.LicenseKey);
                }
                return row;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<HubFederationConfig> Save(HubFederationConfig input)
        {
            try
            {
                if (input == null)
                {
                    throw new ArgumentNullException(nameof(input));
                }

                string plaintextKey = string.IsNullOrWhiteSpace(input.LicenseKey) ? null : input.LicenseKey;
                string protectedKey = plaintextKey == null ? null : _protector.Protect(plaintextKey);
                DateTime updatedAt = DateTime.UtcNow;

                HubFederationConfig existing = await _dataDbContext.HubFederationConfig
                    .OrderBy(r => r.Id)
                    .FirstOrDefaultAsync();
                if (existing == null)
                {
                    existing = new HubFederationConfig()
                    {
                        // Explicit UUID (rather than the column default) so the row is complete
                        // on provider-neutral stores (InMemory has no uuid_generate_v4()).
                        UUID = Guid.NewGuid()
                    };
                    _dataDbContext.HubFederationConfig.Add(existing);
                }

                existing.Enabled = input.Enabled;
                existing.DataApi = input.DataApi;
                existing.AuthenticationApi = input.AuthenticationApi;
                existing.LicenseKey = protectedKey;
                existing.UpdatedAt = updatedAt;
                await _dataDbContext.SaveChangesAsync();

                // Hand back the same plaintext-at-the-seam shape Get() produces; the tracked
                // entity keeps the protected payload, so return a detached copy.
                return new HubFederationConfig()
                {
                    Id = existing.Id,
                    UUID = existing.UUID,
                    Enabled = existing.Enabled,
                    DataApi = existing.DataApi,
                    AuthenticationApi = existing.AuthenticationApi,
                    LicenseKey = plaintextKey,
                    UpdatedAt = existing.UpdatedAt
                };
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <summary>Unprotect a stored payload; null in -> null out, and an unreadable payload
        /// (key-ring rotation, foreign ring, legacy plaintext) degrades to null with a log line
        /// instead of failing every settings read.</summary>
        private string Unprotect(string protectedValue)
        {
            if (string.IsNullOrWhiteSpace(protectedValue))
            {
                return null;
            }
            try
            {
                return _protector.Unprotect(protectedValue);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "HubFederationConfigQueries: stored license key failed to unprotect; treating as absent");
                return null;
            }
        }
    }
}
