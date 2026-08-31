// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.XApiQueries
{
    public interface IExtensionsQueries
    {
        Task<ModelLibrary.Models.XApiModels.Extensions> Create(ModelLibrary.Models.XApiModels.Extensions input);
        Task<bool> Delete(ModelLibrary.Models.XApiModels.Extensions input);
        Task<ModelLibrary.Models.XApiModels.Extensions> Get(long id);
        Task<ModelLibrary.Models.XApiModels.Extensions> Get(Guid id);
        Task<List<ModelLibrary.Models.XApiModels.Extensions>> Get();
        Task<ModelLibrary.Models.XApiModels.Extensions> Update(ModelLibrary.Models.XApiModels.Extensions input);
    }
    public class ExtensionsQueries : IExtensionsQueries
    {
        private readonly XApiDbContext _xApiDbContext;

        public ExtensionsQueries(XApiDbContext xApiDbContext)
        {
            _xApiDbContext = xApiDbContext;
        }
        public ExtensionsQueries()
        {
            _xApiDbContext = new XApiDbContext(XApiDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<ModelLibrary.Models.XApiModels.Extensions> Get(long input)
        {
            ModelLibrary.Models.XApiModels.Extensions Extensions = new ModelLibrary.Models.XApiModels.Extensions();
            try
            {
                Extensions = await _xApiDbContext.Extensions.FindAsync(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ExtensionsQueries.Get(long): suppressed exception");
            }

            return Extensions;
        }
        public async Task<ModelLibrary.Models.XApiModels.Extensions> Get(Guid input)
        {
            ModelLibrary.Models.XApiModels.Extensions Extensions = new ModelLibrary.Models.XApiModels.Extensions();
            try
            {
                Extensions = await _xApiDbContext.Extensions.AsNoTracking().Where(i => i.UUID == input).FirstAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ExtensionsQueries.Get(Guid): suppressed exception");
            }

            return Extensions;
        }
        public async Task<List<ModelLibrary.Models.XApiModels.Extensions>> Get()
        {
            List<ModelLibrary.Models.XApiModels.Extensions> ExtensionsList = new List<ModelLibrary.Models.XApiModels.Extensions>();
            try
            {
                ExtensionsList = await _xApiDbContext.Extensions.AsNoTracking().ToListAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ExtensionsQueries.Get: suppressed exception");
            }

            return ExtensionsList;
        }
        #endregion 

        #region Create
        public async Task<ModelLibrary.Models.XApiModels.Extensions> Create(ModelLibrary.Models.XApiModels.Extensions input)
        {
            try
            {
                await _xApiDbContext.Extensions.AddAsync(input);
                await _xApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ExtensionsQueries.Create: suppressed exception");
            }

            return input;
        }

        #endregion 

        #region Update
        public async Task<ModelLibrary.Models.XApiModels.Extensions> Update(ModelLibrary.Models.XApiModels.Extensions input)
        {
            try
            {
                _xApiDbContext.Extensions.Update(input);
                await _xApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ExtensionsQueries.Update: suppressed exception");
            }

            return input;
        }
        #endregion 

        #region Delete
        public async Task<bool> Delete(ModelLibrary.Models.XApiModels.Extensions input)
        {
            bool output = false;
            try
            {
                _xApiDbContext.Extensions.Remove(input);
                await _xApiDbContext.SaveChangesAsync();
                output = true;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ExtensionsQueries.Delete: suppressed exception");
            }

            return output;
        }
        #endregion 
    }
}
