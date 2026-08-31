// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    public interface IMessageBoardQueries
    {
        Task<List<MessageBoard>> Get();
        Task<MessageBoard> Get(long? id);
        Task<List<MessageBoard>> Get(DateTime startDate, DateTime endDate);
        Task<List<MessageBoard>> Get(DateTime input);
        Task<List<MessageBoard>> GetLastFive();
        Task<MessageBoard> Create(MessageBoard input);
        Task<MessageBoard> Update(MessageBoard input);
        Task<List<MessageBoard>> GetActive();
    }

    public class MessageBoardQueries:IMessageBoardQueries
    {
        private readonly DataDbContext _dataDbContext;

        public MessageBoardQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }
        public MessageBoardQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<MessageBoard> Get(long? input)
        {
            MessageBoard MessageBoard = new MessageBoard();
            try
            {
                MessageBoard = await _dataDbContext.MessageBoard.FindAsync(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return MessageBoard;
        }
        public async Task<List<MessageBoard>> Get()
        {
            List<MessageBoard> MessageBoardList = new List<MessageBoard>();
            try
            {
                MessageBoardList = await _dataDbContext.MessageBoard.AsNoTracking().OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return MessageBoardList;
        }
        public async Task<List<MessageBoard>> GetActive()
        {
            List<MessageBoard> MessageBoardList = new List<MessageBoard>();
            try
            {
                MessageBoardList = await _dataDbContext.MessageBoard
                    .AsNoTracking()
                    .Where(i=>i.Archive==false)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return MessageBoardList;
        }
        public async Task<List<MessageBoard>> Get(DateTime startDate, DateTime endDate)
        {
            List<MessageBoard> outputList = new List<MessageBoard>();
            try
            {
                outputList = await _dataDbContext.MessageBoard
                    .AsNoTracking()
                    .Where(s => s.TimeStamp > startDate 
                    && s.TimeStamp < endDate
                    && s.Archive == false)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return outputList;
        }
        public async Task<List<MessageBoard>> Get(DateTime input)
        {
            List<MessageBoard> outputList = new List<MessageBoard>();
            try
            {
                outputList = await _dataDbContext.MessageBoard
                    .AsNoTracking()
                    .Where(s => s.TimeStamp == input 
                    && s.Archive == false)
                    .OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return outputList;
        }
        public async Task<List<MessageBoard>> GetLastFive()
        {
            List<MessageBoard> output = new List<MessageBoard>();
            try
            {
                output = await _dataDbContext.MessageBoard
                    .AsNoTracking()
                    .Where(i => i.Archive == false)
                    .OrderByDescending(t => t.TimeStamp)
                    .Take(5).OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        #endregion 

        #region Create
        public async Task<MessageBoard> Create(MessageBoard input)
        {
            try
            {
                await _dataDbContext.MessageBoard.AddAsync(input);
                await _dataDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return input;
        }

        #endregion 

        #region Update
        public async Task<MessageBoard> Update(MessageBoard input)
        {
            try
            {
                _dataDbContext.MessageBoard.Update(input);
                await _dataDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return input;
        }
        #endregion 

        #region Delete
        public async Task<bool> Delete(MessageBoard input)
        {
            try
            {
                _dataDbContext.MessageBoard.Remove(input);
                await _dataDbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
        #endregion 
    }

    
}
