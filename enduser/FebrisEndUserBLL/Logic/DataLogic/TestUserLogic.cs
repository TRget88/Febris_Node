// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.SharedServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    public interface ITestUserLogic
    {
        Task<TestUser> Create(TestUser input);
        Task<TestUser> Update(TestUser input);
        Task<TestUser> Get(long id);
        Task<TestUser> Get(long? id);        
        
        //Task<(bool created, string StatusMessage)> Create(ClaimsPrincipal user);
        Task<(bool created, string StatusMessage)> Create();
        Task<List<TestUser>> Get();
        Task<Actor> GetActor(TestUser input);
        
        //Task<Actor> GetActor(TestUser input);
    }

    public class TestUserLogic : ITestUserLogic
    {
        private readonly ITestUserQueries _context;       
        private readonly IActorLogic _actorLogic;        
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        public TestUserLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new TestUserQueries();           
            _actorLogic = new ActorLogic(_httpContextAccessor);           
            User = _httpContextAccessor.HttpContext.User;
        }

        // DI refactor
        public TestUserLogic(IHttpContextAccessor httpContextAccessor,
            ITestUserQueries context,
            IActorLogic actorLogic)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _actorLogic = actorLogic;
            User = _httpContextAccessor?.HttpContext?.User;
        }

        [TempData]
        private string StatusMessage { get; set; }

        #region Get
        public async Task<List<TestUser>> Get()
        {
            List<TestUser> output = new List<TestUser>();
            try
            {
                //if (User.IsFebrisUser())
                //{ 
                    output = await _context.Get();
                //}
                //else if (User.IsContentDeveloper())
                //{
                //    ContentDeveloper item = await _devContext.Get(Guid.Parse(User.ContentDeveloper()));
                //    if (item.IsLockedOut)
                //    {
                //        return null;
                //    }
                //    List<TestUserLinkedContentDeveloper> linkedList = await _testUserLinkedContentDeveloperLogic.Get(item);
                //    output = linkedList.Select(i => i.TestUser).ToList();
                //}
                //else if (User.IsAccreditationBody())
                //{
                //    AccreditationBody item = await _accContext.Get(Guid.Parse(User.AccreditationBody()));
                //    if (item.IsLockedOut)
                //    {
                //        return null;
                //    }
                //    List<TestUserLinkedAccreditationBody> linkedList = await _testUserLinkedAccreditationBodyLogic.Get(item);
                //    output = linkedList.Select(i => i.TestUser).ToList();
                //}
               
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<TestUser> Get(Guid? id)
        {

            TestUser output = new TestUser();
            try
            {
                output = await _context.Get(id);

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;

        }
        public async Task<TestUser> Get(long? id)
        {

            TestUser output = new TestUser();
            try
            {
                output = await _context.Get(id);

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;

        }
        public async Task<TestUser> Get(long id)
        {

            TestUser output = new TestUser();
            try
            {
                output = await _context.Get(id);

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;

        }
      
        public async Task<Actor> GetActor(TestUser? input)
        {

            Actor output = new Actor();
            try
            {
                input = await _context.Get(input.Id);
                output = await _actorLogic.Get(input.ActorId);


                //output = await _context.Get(id);
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

        public async Task<TestUser> Create(TestUser input)
        {
            TestUser output = new TestUser();

            try
            {
                output = await _context.Create(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "TestUserLogic.Create: suppressed exception");
            }
            return output;
        }
        public async Task<(bool created, string StatusMessage)> Create()
        {
            bool output = false;
            ContentDeveloper contentDeveloper = new ContentDeveloper();
            AccreditationBody accreditationBody = new AccreditationBody();
            TestUser newUser = new TestUser();
            //TestUserSettings settings = new TestUserSettings();
            int possibleSeats = 0;
            
            try
            {
                string userRole = string.Empty;
                                
                (output, newUser) = await TestUserCreation();
                

                if (newUser.Id==0)
                {
                    return (false, "A new test user was not created");
                }

                #region Create Actor
                Actor actor = await _actorLogic.Create(newUser);
                #endregion

                #region Link Actor to TestUser
                newUser.ActorId = actor.UUID;
                newUser = await _context.Update(newUser);


                //TestUserLinkedActor linkedActor = await _actorLinkLogic.Create(actor, newUser);
                #endregion

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                StatusMessage = ex.Message;
            }
            return (output, StatusMessage);
        }
        
        private async Task<(bool output, TestUser newUser)> TestUserCreation()
        {
            TestUser newUser = new TestUser();
            bool output = false;
            try
            {
                #region Generate TestUser
                newUser = await NameGenerator.GenerateName();
                newUser = await _context.Create(newUser);
                //newUser.TestUserSettingsUUID = newUser.TestUserSettings.UUID;
                //newUser = await _context.Update(newUser);
                #endregion

                #region Link TestUser to Insitution
                //TestUserLinkedFebris link = await _contextLink.Create(newUser, User);
                //if (link != null)
                //{
                //    output = true;
                //}
                #endregion

                #region Create Actor
                Actor actor = await _actorLogic.Create(newUser);
                #endregion

                #region Link Actor to TestUser
                //TestUserLinkedActor linkedActor = await _actorLinkLogic.Create(actor, newUser);
                #endregion
                output = true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return (output, newUser);
        }

        
        #endregion

        #region update
        public async Task<TestUser> Update(TestUser input)
        {
            TestUser output = new TestUser();
            try
            {
                output = await _context.Update(input);
            }
            catch (Exception ex)
            {
                throw;
            }
            return output;
        }

        #endregion
               
        
        #region partial view data


        #endregion
    }

}
