// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.PrimaryLogicLayer.Logic.DataLogic
{
    public interface IMessageBoardLogic
    {
        Task<List<MessageBoard>> GetLastFive();
        Task<List<MessageBoard>> Get();
        Task<MessageBoard> Get(long? id);
        Task<MessageBoard> Create(MessageBoard messageBoard);
        Task<MessageBoard> Update(MessageBoard messageBoard);
        Task<bool> ToggleArchive(long id);
        Task<List<MessageBoard>> GetActive();
    }

    public class MessageBoardLogic: IMessageBoardLogic
    {       
        private IMessageBoardQueries _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;

        // DI refactor
        public MessageBoardLogic(IHttpContextAccessor httpContextAccessor, IMessageBoardQueries context)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _context = context;
        }

        public MessageBoardLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;
            _context = new MessageBoardQueries();
        }


        #region Get
        public async Task<MessageBoard> Get(long? Id)
        {
            MessageBoard output = await _context.Get(Id);
            return output;
        }
        public async Task<List<MessageBoard>> Get()
        {
            try
            {
                List<MessageBoard> output = await _context.Get();
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
        public async Task<List<MessageBoard>> GetActive()
        {
            try
            {
                List<MessageBoard> output = await _context.GetActive();
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<List<MessageBoard>> Get(DateTime startDate, DateTime endDate)
        {
            List<MessageBoard> output = new List<MessageBoard>();
            try
            {
                output = await _context.Get(startDate, endDate);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "MessageBoardLogic.Get: suppressed exception");

            }

            return output;
        }
        public async Task<List<MessageBoard>> Get(DateTime input)
        {
            List<MessageBoard> output = new List<MessageBoard>();
            try
            {
                output = await _context.Get(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "MessageBoardLogic.Get: suppressed exception");

            }
            return output;
        }
        public async Task<List<MessageBoard>> GetLastFive()
        {
            List<MessageBoard> output = new List<MessageBoard>();
            try
            {
                output = await _context.GetLastFive();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "MessageBoardLogic.GetLastFive: suppressed exception");

            }

            return output;
        }
        #endregion 

        #region Create
        public async Task<MessageBoard> Create(MessageBoard input)
        {
            MessageBoard output = new MessageBoard();
            try
            {
                
                output = new MessageBoard()
                {
                    Subject = input.Subject,
                    Message = input.Message,
                    UserId = Guid.Parse(User.GetUserId()),
                    UserEmail = User.GetUserEmail(),
                    UserName = User.GetUserName()
                };
               
                output = await _context.Create(output);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        #endregion

        #region Update
        public async Task<MessageBoard> Update(MessageBoard input)
        {
            MessageBoard output = new MessageBoard();
            try
            {                
                output = new MessageBoard()
                {
                    Subject = input.Subject,
                    Message = input.Message
                };
                

                output = await _context.Update(input);

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }
        #endregion

      

        #region Archive
        /// <summary>
        /// Need to finish this one up
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<bool> ToggleArchive(long id)
        {
            bool output = false;
            try
            {
                ///This needs to be added

                ContentDeveloper developer = new ContentDeveloper();
                AccreditationBody accreditationBody = new AccreditationBody();
                MessageBoard message = new MessageBoard();
                message = await _context.Get(id);               
                
                message.Archive = !message.Archive;
                message = await Update(message);

                // ROADMAP 20: `output` was declared, never assigned, and returned -- so this method
                // reported FAILURE on every call, including the ones that worked. Write side with no
                // read side, the dominant defect family in this audit.
                //
                // It went unnoticed because the only caller discarded the result and returned Ok()
                // regardless. Making that caller honour the boolean, which is the point of ROADMAP
                // 20, would therefore have turned a working feature into a 404 on every click. Found
                // by fact-checking the pull request, not by any test.
                //
                // `output = true` after the update matches CohortLogic.ArchiveToggle, the sibling
                // method with the same shape. The catch rethrows, so there is no silent-failure path
                // left for the caller to misread.
                output = true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

       
        #endregion
    }


}
