// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.XApiModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.XApiQueries
{
    public interface IContextQueries
    {
    }

    public class ContextQueries : IContextQueries
    {
        private readonly XApiDbContext _xApiDbContext;
        public ContextQueries(XApiDbContext xApiDbContext)
        {
            _xApiDbContext = xApiDbContext;
        }
        public ContextQueries()
        {
            _xApiDbContext = new XApiDbContext(XApiDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<Context> Get(long input)
        {
            Context Context = new Context();
            try
            {
                Context = await _xApiDbContext.Context.FindAsync(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ContextQueries.Get: suppressed exception");

            }

            return Context;
        }
        public async Task<List<Context>> Get()
        {
            List<Context> ContextList = new List<Context>();
            try
            {
                ContextList = await _xApiDbContext.Context.AsNoTracking().ToListAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ContextQueries.Get: suppressed exception");

            }

            return ContextList;
        }
        #endregion 

        #region Create
        public async Task<Context> Create(Context input)
        {
            try
            {
                await _xApiDbContext.Context.AddAsync(input);
                await _xApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ContextQueries.Create: suppressed exception");

            }

            return input;
        }

        #endregion 

        #region Update
        public async Task<Context> Update(Context input)
        {
            try
            {
                _xApiDbContext.Context.Update(input);
                await _xApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ContextQueries.Update: suppressed exception");

            }

            return input;
        }
        #endregion 

        #region Delete
        public async Task<Context> Delete(Context input)
        {
            try
            {
                _xApiDbContext.Context.Remove(input);
                await _xApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ContextQueries.Delete: suppressed exception");

            }

            return input;
        }
        #endregion  
    }
}
