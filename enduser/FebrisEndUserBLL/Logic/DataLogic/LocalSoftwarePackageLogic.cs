// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.SharedServices.Storage;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    /// <summary>
    /// Node-local client-software distribution logic. Catalog reads resolve
    /// from the tenant's own LocalSoftwarePackage store; package BYTES stream from the node's
    /// artifact store through <c>IStorageProvider</c> (key convention
    /// <c>localsoftwarepackage/{uuid}.zip</c>) instead of being proxied from central over HTTP.
    /// This is what the CompanionApp GetLatestVersion/Download route and the portal downloads
    /// page sit on.
    /// </summary>
    public interface ILocalSoftwarePackageLogic
    {
        //Task<List<LocalSoftwarePackage>> Get();
        Task<LocalSoftwarePackage> Get(long? id);
        Task<LocalSoftwarePackage> Get(Guid input);
        Task<LocalSoftwarePackage> Get(LocalSoftwarePackageType input);
        Task<LocalSoftwarePackage> Get(LocalSoftwarePackageType mobileCompanion, Hardware hardware);


        //Task<FileStream> DownloadPackage(Guid package);
        Task<Stream> DownloadPackage(Guid package);
        //Task<FileStream> Download(LocalSoftwarePackage item);
        //Task<Stream> Download(LocalSoftwarePackage item);
        Task<Stream> Download(Guid input);

        Task<List<LocalSoftwarePackage>> GetList(LocalSoftwarePackageType input);
        //Task<FileStream> Download(LocalSoftwarePackage item);
        Task<LocalSoftwarePackage> Get(Guid? input, Hardware hardware);

    }
    public class LocalSoftwarePackageLogic : ILocalSoftwarePackageLogic
    {
        private readonly ILocalSoftwarePackageQueries _context;
        private readonly IStorageProvider _storage;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;

        #region [Historical] legacy self-newing ctor (pre node-local software-package store)
        // Superseded by the DI ctor below: package bytes now stream
        // through the DI-registered IStorageProvider, which cannot be newed from static config,
        // so the accessor-only strangler ctor (which newed LocalSoftwarePackageQueries and left
        // downloads on the remote HTTP path) is retired. Both EndUser hosts resolve this class
        // through DI.
        //public LocalSoftwarePackageLogic(IHttpContextAccessor httpContextAccessor)
        //{
        //    _httpContextAccessor = httpContextAccessor;
        //    _context = new LocalSoftwarePackageQueries();
        //    //_fileSystemHandler = new FileServerHandler();
        //    //_packageFileHandler = new LocalSoftwarePackageFileHandler();
        //    User = _httpContextAccessor.HttpContext.User;
        //}
        #endregion

        // DI refactor
        /// <summary>DI constructor: local catalog queries + the node's storage seam.</summary>
        public LocalSoftwarePackageLogic(IHttpContextAccessor httpContextAccessor, ILocalSoftwarePackageQueries context, IStorageProvider storage)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _storage = storage;
            User = _httpContextAccessor?.HttpContext?.User;
        }

        #region Get
        public async Task<LocalSoftwarePackage> Get(LocalSoftwarePackageType input)
        {
            LocalSoftwarePackage output = new LocalSoftwarePackage();
            try
            {
                output = await _context.Get(input);
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }


        }
        public async Task<LocalSoftwarePackage> Get(long? id)
        {
            LocalSoftwarePackage output = new LocalSoftwarePackage();
            try
            {
                output = await _context.Get(id);
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
        public async Task<LocalSoftwarePackage> Get(long id)
        {
            LocalSoftwarePackage output = new LocalSoftwarePackage();
            try
            {
                output = await _context.Get(id);
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
        public async Task<LocalSoftwarePackage> Get(Guid input)
        {
            LocalSoftwarePackage output = new LocalSoftwarePackage();
            try
            {
                output = await _context.Get(input);
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<List<LocalSoftwarePackage>> GetList(LocalSoftwarePackageType input)
        {
            List<LocalSoftwarePackage> output = new List<LocalSoftwarePackage>();
            try
            {
                output = await _context.GetList(input);
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
        public async Task<LocalSoftwarePackage> Get(LocalSoftwarePackageType input, Hardware hardware)
        {
            ///A null hardware means an already-vetted operator caller (the Portal's cookie
            ///Identity and role gates since ROADMAP 16, the AllowNodeAdmin filter before it) --
            ///only a PRESENT-but-locked-out hardware is refused (defense in depth, as before).
            if (hardware != null && hardware.IsLockedOut)
            {
                return null;
            }
            LocalSoftwarePackage output = new LocalSoftwarePackage();
            try
            {
                output = await _context.Get(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;

        }

        public async Task<LocalSoftwarePackage> Get(Guid? input, Hardware hardware)
        {
            ///Auth severance sub-slice 3: null hardware = admin-authorized request; only a
            ///PRESENT-but-locked-out hardware is refused (defense in depth, as before).
            if (hardware != null && hardware.IsLockedOut)
            {
                return null;
            }
            LocalSoftwarePackage output = new LocalSoftwarePackage();
            try
            {
                output = await _context.Get(input);

            }
            catch (Exception ex)
            { Febris.SharedServices.FebrisLog.Error(ex); }
            return output;
        }
        #endregion

        #region Downloader
        /// <summary>
        /// Stream a package's bytes from the node's own artifact store
        /// (<c>localsoftwarepackage/{uuid}.zip</c> through IStorageProvider). Previously a
        /// byte[] round-trip to central's LocalSoftwarePackage/DownloadPackage endpoint. Throws
        /// FileNotFoundException (surfaced by the controller as 404) when no artifact was ever
        /// ingested for the UUID.
        /// </summary>
        public async Task<Stream> DownloadPackage(Guid input)
        {
            try
            {
                return await _storage.OpenReadAsync(StorageKeys.SoftwarePackage(input.ToString() + ".zip"));
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <summary>Same local stream as <see cref="DownloadPackage"/> (kept for interface parity).</summary>
        public async Task<Stream> Download(Guid item)
        {
            try
            {
                return await _storage.OpenReadAsync(StorageKeys.SoftwarePackage(item.ToString() + ".zip"));
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        #endregion

    }


}
