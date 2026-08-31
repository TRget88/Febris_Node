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
    public interface IVersionQueries
    {
        Task<ModelLibrary.Models.XApiModels.Version> Get(long id);
        Task<ModelLibrary.Models.XApiModels.Version> Get(Guid id);
        Task<List<ModelLibrary.Models.XApiModels.Version>> Get();
        Task<List<ModelLibrary.Models.XApiModels.Version>> Get(List<long> ids);
        Task<ModelLibrary.Models.XApiModels.Version> GetLast();
        Task<ModelLibrary.Models.XApiModels.Version> Create(ModelLibrary.Models.XApiModels.Version input);
    }

    /// <summary>
    /// Node-local xAPI Version store (local-first vocabulary).
    ///
    /// <para>
    /// Previously a Remote-HTTP client resolving Version (incl. <c>GetLast</c>, the value
    /// StatementLogic stamps on inbound statements) from central FebrisSharedAPI. The node now
    /// owns Version locally (seeded with "2.0" by <see cref="XApiVocabularySeeder"/>). Interface
    /// shape unchanged except the added <see cref="Create"/> the seeder needs (mirrors the shared
    /// twin's member). <c>GetLast</c> is ordered explicitly: the shared twin's bare
    /// <c>LastOrDefaultAsync()</c> is not translatable by EF Core without an OrderBy.
    /// </para>
    /// </summary>
    public class VersionQueries : IVersionQueries
    {
        private readonly XApiDbContext _dataDbContext;

        // DI refactor
        public VersionQueries(XApiDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }

        public VersionQueries()
        {
            _dataDbContext = new XApiDbContext(XApiDbContext.ops.DbOptions);
        }

        public async Task<ModelLibrary.Models.XApiModels.Version> Get(Guid input)
        {
            ModelLibrary.Models.XApiModels.Version output = null;
            try
            {
                output = await _dataDbContext.Version.AsNoTracking().Where(i => i.UUID == input).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<List<ModelLibrary.Models.XApiModels.Version>> Get()
        {
            List<ModelLibrary.Models.XApiModels.Version> output = new List<ModelLibrary.Models.XApiModels.Version>();
            try
            {
                output = await _dataDbContext.Version
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<ModelLibrary.Models.XApiModels.Version> Get(long input)
        {
            ModelLibrary.Models.XApiModels.Version output = null;
            try
            {
                output = await _dataDbContext.Version.FindAsync(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<List<ModelLibrary.Models.XApiModels.Version>> Get(List<long> input)
        {
            // Batch fetch for xAPI statement assembly (single round-trip). Previously one HTTP
            // POST to central `Version/batch`; now one local query.
            List<ModelLibrary.Models.XApiModels.Version> output = new List<ModelLibrary.Models.XApiModels.Version>();
            try
            {
                if (input == null || input.Count == 0)
                {
                    return output;
                }
                output = await _dataDbContext.Version
                    .AsNoTracking()
                    .Where(v => input.Contains(v.Id))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<ModelLibrary.Models.XApiModels.Version> GetLast()
        {
            ModelLibrary.Models.XApiModels.Version output = null;
            try
            {
                output = await _dataDbContext.Version
                    .AsNoTracking()
                    .OrderByDescending(v => v.Id)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<ModelLibrary.Models.XApiModels.Version> Create(ModelLibrary.Models.XApiModels.Version input)
        {
            try
            {
                await _dataDbContext.Version.AddAsync(input);
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
        // resolve every Version from central FebrisSharedAPI over HTTP with the license-key Bearer
        // (same MakeGet/Post/PutRequest shape as VerbQueries -- see its historical region), with:
        //   - GetLast -> GET "{DataApi}Version/GetLast"
        //   - Get(List<long>) -> POST "{DataApi}Version/batch"
        //   - a TokenQueries constructed per instance for the license bootstrap.
        // The optional hub-sync path reuses the same central endpoints as a pull-only source.
#endif
        #endregion
    }
}
