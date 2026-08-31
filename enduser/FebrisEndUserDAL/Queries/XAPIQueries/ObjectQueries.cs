// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.XApiQueries
{
    public interface IObjectQueries
    {
        Task<ModelLibrary.Models.XApiModels.Object> Create(ModelLibrary.Models.XApiModels.Object input);
        Task<List<ModelLibrary.Models.XApiModels.Object>> Get();
        Task<List<ModelLibrary.Models.XApiModels.Object>> Get(List<long> ids);
        Task<ModelLibrary.Models.XApiModels.Object> Get(Guid input);
        Task<ModelLibrary.Models.XApiModels.Object> Get(long input);
        Task<ModelLibrary.Models.XApiModels.Object> Get(Uri uri);
        /// <summary>Persist changes to an existing activity row (hub-pull sync:
        /// hub-authored updates refresh matching local rows). Mirrors the VerbQueries twin.</summary>
        Task<ModelLibrary.Models.XApiModels.Object> Update(ModelLibrary.Models.XApiModels.Object input);
    }

    /// <summary>
    /// Node-local xAPI Object (Activity) store (local-first vocabulary).
    ///
    /// <para>
    /// Previously a Remote-HTTP client resolving Objects from central FebrisSharedAPI. The node
    /// now owns Objects locally in the tenant XApiDbContext. Every read Includes the Definition
    /// nav to match what the remote path returned (central serialized the full object graph).
    /// Local-EF bodies mirror the proven shared/central ObjectQueries twin; interface unchanged.
    /// </para>
    /// </summary>
    public class ObjectQueries : IObjectQueries
    {
        private readonly XApiDbContext _dataDbContext;

        // DI refactor
        public ObjectQueries(XApiDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }

        public ObjectQueries()
        {
            _dataDbContext = new XApiDbContext(XApiDbContext.ops.DbOptions);
        }

        public async Task<ModelLibrary.Models.XApiModels.Object> Get(Guid input)
        {
            ModelLibrary.Models.XApiModels.Object output = null;
            try
            {
                output = await _dataDbContext.Object
                    .AsNoTracking()
                    .Include(i => i.Definition)
                    .Where(i => i.UUID == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<List<ModelLibrary.Models.XApiModels.Object>> Get()
        {
            List<ModelLibrary.Models.XApiModels.Object> output = new List<ModelLibrary.Models.XApiModels.Object>();
            try
            {
                output = await _dataDbContext.Object
                    .AsNoTracking()
                    .Include(i => i.Definition)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<ModelLibrary.Models.XApiModels.Object> Get(long input)
        {
            ModelLibrary.Models.XApiModels.Object output = null;
            try
            {
                output = await _dataDbContext.Object
                    .AsNoTracking()
                    .Include(i => i.Definition)
                    .Where(i => i.Key == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<List<ModelLibrary.Models.XApiModels.Object>> Get(List<long> input)
        {
            // Batch fetch for xAPI statement assembly (single round-trip). Previously one HTTP
            // POST to central `Object/batch`; now one local query.
            List<ModelLibrary.Models.XApiModels.Object> output = new List<ModelLibrary.Models.XApiModels.Object>();
            try
            {
                if (input == null || input.Count == 0)
                {
                    return output;
                }
                output = await _dataDbContext.Object
                    .AsNoTracking()
                    .Include(i => i.Definition)
                    .Where(o => input.Contains(o.Key))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<ModelLibrary.Models.XApiModels.Object> Get(Uri input)
        {
            ModelLibrary.Models.XApiModels.Object output = null;
            try
            {
                output = await _dataDbContext.Object
                    .AsNoTracking()
                    .Include(i => i.Definition)
                    .Where(i => i.Id == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<ModelLibrary.Models.XApiModels.Object> Create(ModelLibrary.Models.XApiModels.Object input)
        {
            // Node-local activity registration: the node owns its Object vocabulary, so a
            // content-emitted activity unseen by this node is persisted on first ingest
            // (StatementFactor persist-on-miss) and resolves on every later read. Definition
            // cascades via the nav property. Mirrors the shared twin's Create.
            try
            {
                await _dataDbContext.Object.AddAsync(input);
                await _dataDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return input;
        }

        public async Task<ModelLibrary.Models.XApiModels.Object> Update(ModelLibrary.Models.XApiModels.Object input)
        {
            // (hub-pull sync): refresh path for hub-authored activity updates.
            // Mirrors VerbQueries.Update exactly.
            try
            {
                _dataDbContext.Object.Update(input);
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
        // resolve every Object from central FebrisSharedAPI over HTTP with the license-key Bearer
        // (same MakeGet/Post/PutRequest shape as VerbQueries -- see its historical region), with
        // Get(List<long>) POSTing to "{DataApi}Object/batch". The optional hub-sync path reuses
        // the same central endpoints as a pull-only source.
#endif
        #endregion
    }
}
