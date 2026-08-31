// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;

namespace Febris.UserNode.DataAccessLayer.Queries.XApiQueries
{
    public interface IAuthorityQueries
    {
    }

    public class AuthorityQueries : IAuthorityQueries
    {
        private readonly XApiDbContext _xApiDbContext;
        public AuthorityQueries(XApiDbContext xApiDbContext)
        {
            _xApiDbContext = xApiDbContext;
        }
        public AuthorityQueries()
        {
            _xApiDbContext = new XApiDbContext(XApiDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<Authority> Get(long input)
        {
            Authority Authority = new Authority();
            try
            {
                Authority = await _xApiDbContext.Authority.FindAsync(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "AuthorityQueries.Get: suppressed exception");

            }

            return Authority;
        }
        public async Task<List<Authority>> Get()
        {
            List<Authority> AuthorityList = new List<Authority>();
            try
            {
                AuthorityList = await _xApiDbContext.Authority.AsNoTracking().ToListAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "AuthorityQueries.Get: suppressed exception");

            }

            return AuthorityList;
        }
        #endregion 

        #region Create
        public async Task<Authority> Create(Authority input)
        {
            try
            {
                await _xApiDbContext.Authority.AddAsync(input);
                await _xApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "AuthorityQueries.Create: suppressed exception");

            }

            return input;
        }

        #endregion 

        #region Update
        public async Task<Authority> Update(Authority input)
        {
            try
            {
                _xApiDbContext.Authority.Update(input);
                await _xApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "AuthorityQueries.Update: suppressed exception");

            }

            return input;
        }
        #endregion 

        #region Delete
        public async Task<Authority> Delete(Authority input)
        {
            try
            {
                _xApiDbContext.Authority.Remove(input);
                await _xApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "AuthorityQueries.Delete: suppressed exception");

            }

            return input;
        }
        #endregion 
    }
}
