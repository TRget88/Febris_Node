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
    public interface IAttachmentQueries
    {
    }
    public class AttachmentQueries : IAttachmentQueries
    {
        private readonly XApiDbContext _context;
        public AttachmentQueries(XApiDbContext xApiDbContext)
        {
            _context = xApiDbContext;
        }
        public AttachmentQueries()
        {
            _context = new XApiDbContext(XApiDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<Attachment> Get(long input)
        {
            Attachment Attachment = new Attachment();
            try
            {
                Attachment = await _context.Attachments.FindAsync(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "AttachmentQueries.Get: suppressed exception");
            }

            return Attachment;
        }
        public async Task<List<Attachment>> Get()
        {
            List<Attachment> AttachmentList = new List<Attachment>();
            try
            {
                AttachmentList = await _context.Attachments.AsNoTracking().ToListAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "AttachmentQueries.Get: suppressed exception");
            }

            return AttachmentList;
        }
        #endregion 

        #region Create
        public async Task<Attachment> Create(Attachment input)
        {
            try
            {
                await _context.Attachments.AddAsync(input);
                await _context.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "AttachmentQueries.Create: suppressed exception");
            }

            return input;
        }

        #endregion 

        #region Update
        public async Task<Attachment> Update(Attachment input)
        {
            try
            {
                _context.Attachments.Update(input);
                await _context.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "AttachmentQueries.Update: suppressed exception");
            }

            return input;
        }
        #endregion 

        #region Delete
        public async Task<Attachment> Delete(Attachment input)
        {
            try
            {
                _context.Attachments.Remove(input);
                await _context.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "AttachmentQueries.Delete: suppressed exception");
            }

            return input;
        }
        #endregion 
    }
}
