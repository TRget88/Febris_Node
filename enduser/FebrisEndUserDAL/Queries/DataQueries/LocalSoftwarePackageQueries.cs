// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    /// <summary>
    /// Tenant client-software catalog surface (mobile Server APK, Companion APK, PC launcher
    /// installer, integration SDKs). Read signatures are unchanged from the Remote-HTTP era so no
    /// BLL call site changes (LocalSoftwarePackageLogic / CompanionAppController path). The
    /// Remote-era <c>DownloadPackage(Guid)</c> byte fetch is gone from this contract: bytes now
    /// live in the node's artifact store and stream through <c>IStorageProvider</c> at the BLL
    /// (a file read is not a database query).
    /// </summary>
    public interface ILocalSoftwarePackageQueries
    {
        //Task<List<LocalSoftwarePackage>> Get();
        Task<LocalSoftwarePackage> Get(LocalSoftwarePackageType input);
        Task<LocalSoftwarePackage> Get(long? id);
        Task<LocalSoftwarePackage> Get(Guid? input);
        Task<List<LocalSoftwarePackage>> GetList(LocalSoftwarePackageType input);
        /// <summary>
        /// Create-or-update by UUID for the node's package-ingest path. An
        /// ingested package whose UUID already exists updates the catalog row in place; a new UUID
        /// inserts one. Returns the persisted row.
        /// </summary>
        Task<LocalSoftwarePackage> Upsert(LocalSoftwarePackage input);
    }

    /// <summary>
    /// Node-local client-software catalog store (client-software distribution).
    ///
    /// <para>
    /// Previously this class was a Remote-HTTP client: every Get (and the package bytes
    /// themselves) came from central's LocalSoftwarePackage endpoints over
    /// <c>APIRequestFactory</c>, so a node could not update a companion app or hand out a launcher
    /// installer without a reachable central. The node now OWNS its software-package catalog in
    /// the tenant DataDbContext (rows created by the admin upload-ingest path); a central hub,
    /// when configured, becomes an optional pull source instead of a hard dependency. Local-EF
    /// bodies mirror the proven shared/central LocalSoftwarePackageQueries twin.
    /// </para>
    /// </summary>
    public class LocalSoftwarePackageQueries : ILocalSoftwarePackageQueries
    {
        private readonly DataDbContext _dataDbContext;

        // DI refactor
        public LocalSoftwarePackageQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }

        public LocalSoftwarePackageQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        #region Get
        /// <summary>Resolve one package by surrogate key. Null on miss.</summary>
        public async Task<LocalSoftwarePackage> Get(long? input)
        {
            LocalSoftwarePackage output = null;
            try
            {
                output = await _dataDbContext.LocalSoftwarePackage
                    .AsNoTracking()
                    .Where(i => i.Id == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        /// <summary>Resolve one package by UUID. Null on miss.</summary>
        public async Task<LocalSoftwarePackage> Get(Guid? input)
        {
            LocalSoftwarePackage output = null;
            try
            {
                output = await _dataDbContext.LocalSoftwarePackage
                    .AsNoTracking()
                    .Where(i => i.UUID == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        /// <summary>
        /// The ACTIVE (latest non-obsolete) package of a kind -- the companion/launcher
        /// version-resolution authority (CompanionApp/GetLatestVersion). Mirrors the central
        /// GetActive semantics: newest TimeStamp wins.
        /// </summary>
        public async Task<LocalSoftwarePackage> Get(LocalSoftwarePackageType input)
        {
            LocalSoftwarePackage output = null;
            try
            {
                output = await _dataDbContext.LocalSoftwarePackage
                    .AsNoTracking()
                    .Where(i => i.LocalSoftwarePackageType == input
                    && !i.Obsolete)
                    .OrderByDescending(i => i.TimeStamp)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        /// <summary>Every package of a kind (incl. obsolete), newest first -- the archive listing.</summary>
        public async Task<List<LocalSoftwarePackage>> GetList(LocalSoftwarePackageType input)
        {
            List<LocalSoftwarePackage> output = new List<LocalSoftwarePackage>();
            try
            {
                output = await _dataDbContext.LocalSoftwarePackage
                    .AsNoTracking()
                    .Where(i => i.LocalSoftwarePackageType == input)
                    .OrderByDescending(i => i.TimeStamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }
        #endregion

        #region Upsert (package ingest)
        /// <inheritdoc />
        public async Task<LocalSoftwarePackage> Upsert(LocalSoftwarePackage input)
        {
            try
            {
                LocalSoftwarePackage existing = await _dataDbContext.LocalSoftwarePackage
                    .Where(i => i.UUID == input.UUID)
                    .FirstOrDefaultAsync();
                if (existing == null)
                {
                    await _dataDbContext.LocalSoftwarePackage.AddAsync(input);
                    await _dataDbContext.SaveChangesAsync();
                    return input;
                }

                existing.Name = input.Name;
                existing.Version = input.Version;
                existing.Description = input.Description;
                existing.Obsolete = input.Obsolete;
                existing.LocalSoftwarePackageType = input.LocalSoftwarePackageType;
                existing.Language = input.Language;
                await _dataDbContext.SaveChangesAsync();
                return existing;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
        #endregion

        #region [Historical] Remote-HTTP implementation (pre node-local software-package store)
#if false
        // Superseded by the local-EF implementation above. The tenant used to
        // resolve every LocalSoftwarePackage -- including the package BYTES -- from central
        // FebrisSharedAPI over HTTP with the license-key Bearer:
        //
        //   public string _endpoint;   // = StaticDetails.PassedBackConfig ApiUrlPath:DataApi
        //   private readonly ITokenQueries _tokenContext;   // RenewToken() on non-200, then retry
        //   private async Task<string> MakeGetRequest(string method, string dataPackage) { ... }
        //   private async Task<byte[]> MakeDownloadRequest(string method, string dataPackage)
        //   {
        //       IAPIRequestFactory request = new APIRequestFactory()
        //       {
        //           endPoint = _endpoint + "LocalSoftwarePackage/" + method,
        //           httpMethod = httpVerb.GET,
        //           authTech = AuthenticaitonTechnique.Token,
        //           authType = Authenticationtype.BearerToken,
        //           token = StaticDetails.LicenseAuthenticateResponse?.JwtToken ?? string.Empty,
        //       };
        //       (byte[] response, HttpStatusCode status) = await request.MakeByteArrayRequest();
        //       return response;
        //   }
        //   // Get(long?)                       -> GET LocalSoftwarePackage/{id}
        //   // Get(Guid?)                       -> GET LocalSoftwarePackage/GetByUUID/{uuid}
        //   // Get(LocalSoftwarePackageType)    -> GET LocalSoftwarePackage/GetActive/{type}
        //   // GetList(LocalSoftwarePackageType)-> GET LocalSoftwarePackage/GetList/{type}
        //   // DownloadPackage(Guid)            -> GET LocalSoftwarePackage/DownloadPackage/{uuid} (byte[])
        //
        // DownloadPackage left the interface entirely: the node stores the bytes itself and the
        // BLL streams them through IStorageProvider (LocalSoftwarePackageLogic.Download). The
        // optional hub-pull path (fetch published packages down into the local store) is the
        // planned replacement for this coupling and reuses the same central endpoints.
#endif
        #endregion
    }
}
