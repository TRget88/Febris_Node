// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    /// <summary>
    /// Tenant Module-to-xAPI-Object link surface. <c>GetByModule</c> is unchanged from the
    /// Remote-HTTP era so no BLL call site changes (LauncherLogic statement initialization).
    /// </summary>
    public interface IModuleLinkedObjectQueries
    {
        Task<ModuleLinkedObject> GetByModule(Guid? moduleId);
        //Task<ModuleLinkedObject> Get(Module module);
        /// <summary>
        /// Create-or-update by ModuleUUID for the node's package-ingest path.
        /// Each module carries exactly one xAPI activity link, so re-ingesting a package re-points
        /// the existing link instead of duplicating it. Returns the persisted row.
        /// </summary>
        Task<ModuleLinkedObject> Upsert(ModuleLinkedObject input);
    }

    /// <summary>
    /// Node-local Module-to-xAPI-Object link store (delivery-path severance).
    ///
    /// <para>
    /// Previously this class was a Remote-HTTP client resolving the link from central
    /// FebrisSharedAPI, so statement initialization died without a reachable central. The link rows
    /// now live in the tenant DataDbContext next to the local Module catalog (created by the
    /// package-ingest path). Local-EF bodies mirror the proven shared/central
    /// ModuleLinkedObjectQueries twin; the pre-existing interface shape is unchanged.
    /// </para>
    /// </summary>
    public class ModuleLinkedObjectQueries : IModuleLinkedObjectQueries
    {
        private readonly DataDbContext _dataDbContext;

        // DI refactor
        public ModuleLinkedObjectQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }

        public ModuleLinkedObjectQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        #region Get
        /// <summary>Resolve the link for a module by the module's UUID. Null on miss.</summary>
        public async Task<ModuleLinkedObject> GetByModule(Guid? input)
        {
            ModuleLinkedObject output = null;
            try
            {
                output = await _dataDbContext.ModuleLinkedObject
                    .AsNoTracking()
                    .Include(i => i.Module).ThenInclude(i => i.ModuleClassification)
                    .Where(i => i.Module.UUID == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        /// <summary>All local links (kept for parity with the shared twin; no BLL caller today).</summary>
        public async Task<List<ModuleLinkedObject>> Get()
        {
            List<ModuleLinkedObject> output = new List<ModuleLinkedObject>();
            try
            {
                output = await _dataDbContext.ModuleLinkedObject
                    .AsNoTracking()
                    .Include(i => i.Module).ThenInclude(i => i.ModuleClassification)
                    .OrderByDescending(i => i.TimeStamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        /// <summary>Resolve one link by its own UUID. Null on miss.</summary>
        public async Task<ModuleLinkedObject> Get(Guid? input)
        {
            ModuleLinkedObject output = null;
            try
            {
                output = await _dataDbContext.ModuleLinkedObject
                    .AsNoTracking()
                    .Include(i => i.Module).ThenInclude(i => i.ModuleClassification)
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

        /// <summary>Resolve one link by surrogate key. Null on miss.</summary>
        public async Task<ModuleLinkedObject> Get(long input)
        {
            ModuleLinkedObject output = null;
            try
            {
                output = await _dataDbContext.ModuleLinkedObject
                    .AsNoTracking()
                    .Include(i => i.Module).ThenInclude(i => i.ModuleClassification)
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
        #endregion

        #region Upsert (package ingest)
        /// <inheritdoc />
        public async Task<ModuleLinkedObject> Upsert(ModuleLinkedObject input)
        {
            try
            {
                ModuleLinkedObject existing = await _dataDbContext.ModuleLinkedObject
                    .Where(i => i.ModuleUUID == input.ModuleUUID)
                    .FirstOrDefaultAsync();
                if (existing == null)
                {
                    await _dataDbContext.ModuleLinkedObject.AddAsync(input);
                    await _dataDbContext.SaveChangesAsync();
                    return input;
                }

                existing.ObjectId = input.ObjectId;
                existing.ObjectUUID = input.ObjectUUID;
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
        // to resolve every ModuleLinkedObject from central FebrisSharedAPI over HTTP (no Bearer was
        // ever attached -- the token slot was commented out):
        //
        //   public string _endpoint;   // = StaticDetails.PassedBackConfig ApiUrlPath:DataApi
        //   private async Task<string> MakeGetRequest(string method, string dataPackage)
        //   {
        //       IAPIRequestFactory request = new APIRequestFactory()
        //       {
        //           endPoint = _endpoint + "ModuleLinkedObject/" + method,
        //           httpMethod = httpVerb.GET,
        //           authTech = AuthenticaitonTechnique.Token,
        //           authType = Authenticationtype.BearerToken,
        //           postJSON = dataPackage ?? string.Empty,
        //       };
        //       (string response, HttpStatusCode status) = await request.MakeStringRequest();
        //       return response;
        //   }
        //   // ...MakePostRequest / MakePutRequest identical with POST/PUT...
        //   // Get()            -> GET ModuleLinkedObject/
        //   // Get(long)        -> GET ModuleLinkedObject/{serialized id}
        //   // Get(Guid?)       -> GET ModuleLinkedObject/{serialized uuid}
        //   // Get(List<Guid>)  -> GET ModuleLinkedObject/GetList (list in body)
        //   // GetByModule      -> GET ModuleLinkedObject/GetByModuleUUID/{serialized uuid}
        //
        // The optional hub-sync path (pull published links down into the local store) is the
        // planned replacement for this coupling and reuses the same central endpoints.
#endif
        #endregion
    }
}
