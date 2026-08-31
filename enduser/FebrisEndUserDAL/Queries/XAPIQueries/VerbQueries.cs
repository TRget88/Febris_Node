// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.XApiModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.XApiQueries
{
    public interface IVerbQueries
    {
        Task<Verb> Create(Verb input);
        Task<Verb> Get(long? input);
        Task<Verb> Get(Guid input);
        Task<List<Verb>> Get();
        Task<List<Verb>> Get(List<long> ids);
        Task<Verb> Get(Uri verbUri);
        Task<Verb> Update(Verb input);
    }

    /// <summary>
    /// Node-local xAPI Verb store (local-first vocabulary).
    ///
    /// <para>
    /// Previously this class was a Remote-HTTP client: every Get resolved the verb from central
    /// FebrisSharedAPI over <c>APIRequestFactory</c>, so the node could not ingest or render a
    /// single statement without a reachable central. The node now OWNS its vocabulary in the
    /// tenant XApiDbContext (seeded at startup by <see cref="XApiVocabularySeeder"/>); a central
    /// hub, when configured, becomes an optional sync source instead of a hard dependency.
    /// Local-EF bodies mirror the proven shared/central VerbQueries twin. The interface shape is
    /// unchanged so no BLL call site changes.
    /// </para>
    /// </summary>
    public class VerbQueries : IVerbQueries
    {
        private readonly XApiDbContext _dataDbContext;

        // DI refactor
        public VerbQueries(XApiDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }

        public VerbQueries()
        {
            _dataDbContext = new XApiDbContext(XApiDbContext.ops.DbOptions);
        }

        public async Task<Verb> Get(Uri input)
        {
            Verb output = null;
            try
            {
                output = await _dataDbContext.Verb.AsNoTracking().Where(i => i.Id == input).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<Verb> Get(Guid input)
        {
            Verb output = null;
            try
            {
                output = await _dataDbContext.Verb.AsNoTracking().Where(i => i.UUID == input).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<List<Verb>> Get()
        {
            List<Verb> output = new List<Verb>();
            try
            {
                output = await _dataDbContext.Verb
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<Verb> Get(long? input)
        {
            Verb output = null;
            try
            {
                output = await _dataDbContext.Verb.FindAsync(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<List<Verb>> Get(List<long> input)
        {
            // Batch fetch for xAPI statement assembly (single round-trip). Previously one HTTP
            // POST to central `Verb/batch`; now one local query.
            List<Verb> output = new List<Verb>();
            try
            {
                if (input == null || input.Count == 0)
                {
                    return output;
                }
                output = await _dataDbContext.Verb
                    .AsNoTracking()
                    .Where(v => input.Contains(v.Key))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<Verb> Create(Verb input)
        {
            try
            {
                await _dataDbContext.Verb.AddAsync(input);
                await _dataDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return input;
        }

        public async Task<Verb> Update(Verb input)
        {
            try
            {
                _dataDbContext.Verb.Update(input);
                await _dataDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return input;
        }

        #region [Historical] Remote-HTTP implementation (pre node-local vocabulary)
#if false
        // Superseded by the local-EF implementation above. The tenant used to
        // resolve every Verb from central FebrisSharedAPI over HTTP with the license-key Bearer:
        //
        //   public string _endpoint;   // = StaticDetails.PassedBackConfig ApiUrlPath:DataApi
        //   private async Task<string> MakeGetRequest(string method, string dataPackage)
        //   {
        //       IAPIRequestFactory request = new APIRequestFactory()
        //       {
        //           endPoint = _endpoint + "Verb/" + method,
        //           httpMethod = httpVerb.GET,
        //           authTech = AuthenticaitonTechnique.Token,
        //           authType = Authenticationtype.BearerToken,
        //           postJSON = dataPackage ?? string.Empty,
        //       };
        //       (string response, HttpStatusCode status) = await request.MakeStringRequest();
        //       return response;
        //   }
        //   // ...MakePostRequest / MakePutRequest identical with POST/PUT...
        //   // Each Get overload serialized its key into `method` and deserialized the JSON body;
        //   // Get(List<long>) POSTed to "Verb/batch"; Create/Update POSTed with empty bodies.
        //
        // The optional hub-sync path (pull published vocabulary down into the local store) is the
        // planned replacement for this coupling and reuses the same central endpoints.
#endif
        #endregion
    }
}
