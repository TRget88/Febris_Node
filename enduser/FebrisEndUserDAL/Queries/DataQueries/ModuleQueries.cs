// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
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
    /// Tenant module-catalog read surface. Signatures are unchanged from the Remote-HTTP era so no
    /// BLL call site changes (LauncherLogic / ModuleLogic / HardwareLinkedModuleLogic /
    /// ModuleLinkedObjectLogic).
    /// </summary>
    public interface IModuleQueries
    {
        Task<List<Module>> Get();
        //Task<Module> Get(long input);
        Task<Module> Get(long? input);
        Task<Module> Get(Guid? input);
        Task<List<Module>> GetByUserList(List<Guid> userIdList);
        Task<List<Module>> GetByLicense();
        Task<List<Module>> Get(List<Guid> input);
        /// <summary>
        /// Create-or-update by UUID for the node's package-ingest path. An
        /// ingested package whose UUID already exists updates the catalog row in place; a new UUID
        /// inserts one. Returns the persisted row.
        /// </summary>
        Task<Module> Upsert(Module input);
    }

    /// <summary>
    /// Node-local module catalog store (delivery-path severance).
    ///
    /// <para>
    /// Previously this class was a Remote-HTTP client: every Get resolved the module from central
    /// FebrisSharedAPI over <c>APIRequestFactory</c> with the license-key Bearer, so the node could
    /// not initialize a launcher or serve a download without a reachable central. The node now OWNS
    /// its catalog in the tenant DataDbContext (rows created by the package-ingest path); a central
    /// hub, when configured, becomes an optional sync source instead of a hard dependency.
    /// Local-EF bodies mirror the proven shared/central ModuleQueries twin. The pre-existing
    /// interface shape is unchanged so no BLL call site changes.
    /// </para>
    /// </summary>
    public class ModuleQueries : IModuleQueries
    {
        private readonly DataDbContext _dataDbContext;

        // DI refactor
        public ModuleQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }

        public ModuleQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        #region Get
        /// <summary>Resolve a batch of modules by UUID (launcher initialize path).</summary>
        public async Task<List<Module>> Get(List<Guid> input)
        {
            List<Module> output = new List<Module>();
            try
            {
                if (input == null || input.Count == 0)
                {
                    return output;
                }
                output = await _dataDbContext.Module
                    .AsNoTracking()
                    .Include(i => i.ModuleClassification)
                    .Where(i => input.Contains(i.UUID))
                    .OrderByDescending(i => i.TimeStamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        /// <summary>Resolve one module by UUID. Null on miss (matching the shared twin).</summary>
        public async Task<Module> Get(Guid? input)
        {
            Module output = null;
            try
            {
                output = await _dataDbContext.Module
                    .AsNoTracking()
                    .Include(i => i.ModuleClassification)
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

        /// <summary>The full local catalog, newest first.</summary>
        public async Task<List<Module>> Get()
        {
            List<Module> output = new List<Module>();
            try
            {
                output = await _dataDbContext.Module
                    .AsNoTracking()
                    .Include(i => i.ModuleClassification)
                    .OrderByDescending(i => i.TimeStamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        /// <summary>Resolve one module by surrogate key. Null on miss.</summary>
        public async Task<Module> Get(long? input)
        {
            Module output = null;
            try
            {
                output = await _dataDbContext.Module
                    .AsNoTracking()
                    .Include(i => i.ModuleClassification)
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

        /// <summary>
        /// Kept for interface parity. The Remote era POSTed the user list to central so commerce
        /// (Purchase -> listing -> curriculum -> module) could scope the answer; the node has no
        /// commerce data, so this resolves to the local catalog (the node's delivery authority).
        /// No BLL call site invokes this member today.
        /// </summary>
        public async Task<List<Module>> GetByUserList(List<Guid> input)
        {
            return await GetByLicense();
        }

        /// <summary>
        /// The modules this deployment may deliver. The Remote era asked central to expand the
        /// LICENSE into a module list; on the self-sufficient node the local catalog IS the set of
        /// delivered modules (rows only exist once ingested/granted), so this returns every
        /// non-obsolete local row with zero HTTP.
        /// </summary>
        public async Task<List<Module>> GetByLicense()
        {
            List<Module> output = new List<Module>();
            try
            {
                output = await _dataDbContext.Module
                    .AsNoTracking()
                    .Include(i => i.ModuleClassification)
                    .Where(i => !i.Obsolete)
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
        public async Task<Module> Upsert(Module input)
        {
            try
            {
                Module existing = await _dataDbContext.Module
                    .Include(i => i.ModuleClassification)
                    .Where(i => i.UUID == input.UUID)
                    .FirstOrDefaultAsync();
                if (existing == null)
                {
                    await _dataDbContext.Module.AddAsync(input);
                    await _dataDbContext.SaveChangesAsync();
                    return input;
                }

                existing.Name = input.Name;
                existing.Version = input.Version;
                existing.Description = input.Description;
                existing.Obsolete = input.Obsolete;
                existing.Language = input.Language;
                existing.XApiInteractionType = input.XApiInteractionType;
                existing.MainSectionCount = input.MainSectionCount;
                existing.TotalSectionCount = input.TotalSectionCount;
                existing.InteractionComponents = input.InteractionComponents;
                existing.EstimatedCompletionTime = input.EstimatedCompletionTime;
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

        #region [Historical] Remote-HTTP implementation (pre node-local catalog)
#if false
        // Superseded by the local-EF implementation above. The tenant used
        // to resolve every Module from central FebrisSharedAPI over HTTP with the license-key
        // Bearer:
        //
        //   public string _endpoint;   // = StaticDetails.PassedBackConfig ApiUrlPath:DataApi
        //   private readonly ITokenQueries _tokenContext;   // RenewToken() on non-200, then retry
        //   private async Task<string> MakeGetRequest(string method)
        //   {
        //       IAPIRequestFactory request = new APIRequestFactory()
        //       {
        //           endPoint = _endpoint + "Module/" + method,
        //           httpMethod = httpVerb.GET,
        //           authTech = AuthenticaitonTechnique.Token,
        //           authType = Authenticationtype.BearerToken,
        //           token = StaticDetails.LicenseAuthenticateResponse?.JwtToken ?? string.Empty,
        //       };
        //       (string response, HttpStatusCode status) = await request.MakeStringRequest();
        //       if (status != HttpStatusCode.OK && await _tokenContext.RenewToken())
        //       {
        //           request.token = StaticDetails.LicenseAuthenticateResponse.JwtToken;
        //           (response, status) = await request.MakeStringRequest();
        //       }
        //       return response;
        //   }
        //   // ...MakePostRequest / MakePutRequest identical with POST/PUT + a JSON body...
        //   // Get()            -> GET  Module/
        //   // Get(long?)       -> GET  Module/{id}
        //   // Get(Guid?)       -> GET  Module/getbyuuid/{uuid}
        //   // Get(List<Guid>)  -> POST Module/GetListByUUID
        //   // GetByUserList    -> POST Module/GetByUserList
        //   // GetByLicense     -> GET  Module/GetByLicense
        //
        // The optional hub-sync path (pull published catalog rows down into the local store) is the
        // planned replacement for this coupling and reuses the same central endpoints.
#endif
        #endregion
    }
}
