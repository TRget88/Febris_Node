// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.Models.XApiModels.ExtraModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.XApiQueries
{
    public interface IXApiResultExtrasQueries
    {
        Task<XApiResultExtras> GetByResult(Result result);
        Task<XApiResultExtras> Create(XApiResultExtras extras);
    }

    public class XApiResultExtrasQueries : IXApiResultExtrasQueries
    {
        private readonly XApiDbContext _context;
        public XApiResultExtrasQueries(XApiDbContext xApiDbContext)
        {
            _context = xApiDbContext;
        }
        public XApiResultExtrasQueries()
        {
            _context = new XApiDbContext(XApiDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<XApiResultExtras> Get(long input)
        {
            XApiResultExtras output = new XApiResultExtras();
            try
            {
                output = await _context.XApiResultExtras
                    .AsNoTracking()
                    .Include(i => i.Result)
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
        public async Task<XApiResultExtras> Get(Guid input)
        {
            XApiResultExtras output = new XApiResultExtras();
            try
            {
                output = await _context.XApiResultExtras
                    .AsNoTracking()
                    .Include(i => i.Result)
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
        public async Task<XApiResultExtras> GetByResult(Result input)
        {
            XApiResultExtras output = new XApiResultExtras();
            try
            {
                output = await _context.XApiResultExtras
                    .AsNoTracking()
                    .Include(i => i.Result).ThenInclude(i => i.Score)
                    .Include(i => i.Result).ThenInclude(i=>i.Extensions)
                    .Where(i => i.Result.Id == input.Id)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<XApiResultExtras> GetByResult(long input)
        {
            XApiResultExtras output = new XApiResultExtras();
            try
            {
                output = await _context.XApiResultExtras
                    .AsNoTracking()
                    .Include(i => i.Result)
                    .Where(i => i.Result.Id == input)
                    .FirstOrDefaultAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "XApiResultExtrasQueries.GetByResult: suppressed exception");
            }
            return output;
        }
        public async Task<XApiResultExtras> GetByResult(Guid input)
        {
            XApiResultExtras output = new XApiResultExtras();
            try
            {
                output = await _context.XApiResultExtras
                    .AsNoTracking()
                    .Include(i => i.Result)
                    .Where(i => i.ResultUUID == input)
                    .FirstOrDefaultAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "XApiResultExtrasQueries.GetByResult: suppressed exception");
            }
            return output;
        }

        #endregion

        #region Create
        public async Task<XApiResultExtras> Create(XApiResultExtras input)
        {
            try
            {
                //await _xApiDbContext.XApiResultExtras.AddAsync(input);
                _context.XApiResultExtras.Update(input);
                await _context.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "XApiResultExtrasQueries.Create: suppressed exception");
            }
            return input;
        }
        #endregion

        #region Update
        public async Task<XApiResultExtras> Update(XApiResultExtras input)
        {
            try
            {
                _context.XApiResultExtras.Update(input);
                await _context.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "XApiResultExtrasQueries.Update: suppressed exception");
            }
            return input;
        }

        #endregion

        #region Delete
        public async Task<bool> Delete(XApiResultExtras input)
        {
            XApiResultExtras output = new XApiResultExtras();
            try
            {
                _context.XApiResultExtras.Remove(input);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "XApiResultExtrasQueries.Delete: suppressed exception");
                return false;
            }
        }
        #endregion
    }
}
