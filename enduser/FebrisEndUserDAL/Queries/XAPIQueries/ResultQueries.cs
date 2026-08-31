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
    public interface IResultQueries
    {
    }
    public class ResultQueries : IResultQueries
    {
        private readonly XApiDbContext _xApiDbContext;
        public ResultQueries(XApiDbContext xApiDbContext)
        {
            _xApiDbContext = xApiDbContext;
        }
        public ResultQueries()
        {
            _xApiDbContext = new XApiDbContext(XApiDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<Result> Get(long input)
        {
            Result Result = new Result();
            try
            {
                Result = await _xApiDbContext.Result.FindAsync(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ResultQueries.Get: suppressed exception");

            }

            return Result;
        }
        public async Task<List<Result>> Get()
        {
            List<Result> ResultList = new List<Result>();
            try
            {
                ResultList = await _xApiDbContext.Result.AsNoTracking().ToListAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ResultQueries.Get: suppressed exception");

            }

            return ResultList;
        }
        #endregion 

        #region Create
        public async Task<Result> Create(Result input)
        {
            try
            {
                await _xApiDbContext.Result.AddAsync(input);
                await _xApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ResultQueries.Create: suppressed exception");

            }

            return input;
        }

        #endregion 

        #region Update
        public async Task<Result> Update(Result input)
        {
            try
            {
                _xApiDbContext.Result.Update(input);
                await _xApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ResultQueries.Update: suppressed exception");

            }

            return input;
        }
        #endregion 

        #region Delete
        public async Task<Result> Delete(Result input)
        {
            try
            {
                _xApiDbContext.Result.Remove(input);
                await _xApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ResultQueries.Delete: suppressed exception");

            }

            return input;
        }
        #endregion 
    }
}
