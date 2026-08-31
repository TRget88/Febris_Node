// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Interfaces.XApiModelInterfaces;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.XApiQueries
{
    public interface IDefinitionQueries
    {
        Task<List<Definition>> Get();
        Task<Definition> Get(Guid input);
        Task<Definition> Get(long input);
        Task<bool> Delete(Definition input);
        Task<Definition> Update(Definition input);
        Task<Definition> Create(Definition input);
    }
    public class DefinitionQueries : IDefinitionQueries
    {
        private readonly XApiDbContext _XApiDbContext;
        public DefinitionQueries(XApiDbContext XApiDbContext)
        {
            _XApiDbContext = XApiDbContext;
        }
        public DefinitionQueries()
        {
            _XApiDbContext = new XApiDbContext(XApiDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<Definition> Get(long input)
        {
            Definition Definition = new Definition();
            try
            {
                Definition = await _XApiDbContext.Definition.FindAsync(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "DefinitionQueries.Get: suppressed exception");

            }

            return Definition;
        }
        public async Task<List<Definition>> Get()
        {
            List<Definition> DefinitionList = new List<Definition>();
            try
            {
                DefinitionList = await _XApiDbContext.Definition.AsNoTracking().ToListAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "DefinitionQueries.Get: suppressed exception");

            }

            return DefinitionList;
        }

        public async Task<Definition> Get(Guid input)
        {
            Definition Definition = new Definition();
            try
            {
                Definition = await _XApiDbContext.Definition.AsNoTracking().Where(i => i.UUID == input).FirstAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "DefinitionQueries.Get: suppressed exception");

            }

            return Definition;
        }
        #endregion

        #region Create
        public async Task<Definition> Create(Definition input)
        {
            try
            {
                await _XApiDbContext.Definition.AddAsync(input);
                await _XApiDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return input;
        }

        #endregion 

        #region Update
        public async Task<Definition> Update(Definition input)
        {
            try
            {
                _XApiDbContext.Definition.Update(input);
                await _XApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "DefinitionQueries.Update: suppressed exception");

            }

            return input;
        }
        #endregion 

        #region Delete
        public async Task<bool> Delete(Definition input)
        {
            bool output = false;
            try
            {
                _XApiDbContext.Definition.Remove(input);
                await _XApiDbContext.SaveChangesAsync();
                output = true;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "DefinitionQueries.Delete: suppressed exception");

            }
            return output;
        }
        #endregion 
    }
}
