// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.UserModels;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.DataAccessLayer.Queries.XAPIQueries;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.PrimaryLogicLayer.Logic.XApiLogic
{
    public interface IActorLogic
    {
        Task<Actor> Create(LocalApplicationUser newUser);
        Task<List<Actor>> Get();
        Task<Actor> Get(long? id);
        Task<Actor> Create(Actor actor);
        Task<Actor> Get(Guid actorId);
        Task<bool> Exists(string mbox);
        Task<Actor> GetByHashedMbox(string mbox_sha1sum);

        /// <summary>
        /// Removes the directly identifying fields from an Actor while KEEPING the row and its
        /// <c>Mbox_sha1sum</c>. Returns true when a row was changed. Never deletes anything.
        /// </summary>
        Task<bool> Pseudonymise(Guid actorUuid);
        Task<List<Actor>> GetByHashedMboxList(List<string> mbox_sha1sums);
    }

    public class ActorLogic : IActorLogic
    {
        private readonly IActorQueries _dataContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        // DI refactor
        public ActorLogic(IHttpContextAccessor httpContextAccessor, IActorQueries dataContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _dataContext = dataContext;
            User = _httpContextAccessor?.HttpContext?.User;
        }

        public ActorLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _dataContext = new ActorQueries();
            User = _httpContextAccessor.HttpContext.User;
        }

        #region Get                      
        public async Task<List<Actor>> Get()
        {
            //bool output = true;
            List<Actor> output = new List<Actor>();
            try
            {
                #region Filter
                if (User.IsLocalFebrisAdmin()|| User.IsLocalAdmin() || User.IsLocalEducator())
                {
                    output = await _dataContext.Get();
                }               
                else
                {
                    return default;
                }
                #endregion
                //output = await _dataContext.Get();
                //output.AddRange(preoutput);
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "ActorLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<Actor> Get(Guid input)
        {
            //bool output = true;
            Actor output = new Actor();
            try
            {
                #region Filter
                ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User);
                if (scope.Unrestricted)
                {
                    output = await _dataContext.Get(input);
                }
                else if (scope.AllowedActorUuids.Contains(input))
                {
                    // Learner: their own actor. Parent: one of their linked students' actors.
                    output = await _dataContext.Get(input);
                }
                else
                {
                    return default;
                }
                #endregion

                //use input to find subscription

                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "ActorLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<Actor> Get(long input)
        {
            //bool output = true;
            Actor output = new Actor();
            try
            {
                //use input to find subscription
                //output = await _dataContext.Get(input);
                //output = subscription;
                #region Filter
                ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User);
                if (scope.Unrestricted)
                {
                    output = await _dataContext.Get(input);
                }
                else
                {
                    Actor requested = await _dataContext.Get(input);
                    if (requested != null && requested.Id != 0 && scope.AllowedActorUuids.Contains(requested.UUID))
                    {
                        output = requested;
                    }
                    else
                    {
                        return default;
                    }
                }
                #endregion
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "ActorLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<Actor> Get(long? input)
        {
            //bool output = true;
            Actor output = new Actor();
            try
            {
                //use input to find subscription
                //output = await _dataContext.Get(input);
                //output = subscription;
                #region Filter
                ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User);
                if (scope.Unrestricted)
                {
                    output = await _dataContext.Get(input);
                }
                else
                {
                    Actor requested = await _dataContext.Get(input);
                    if (requested != null && requested.Id != 0 && scope.AllowedActorUuids.Contains(requested.UUID))
                    {
                        output = requested;
                    }
                    else
                    {
                        return default;
                    }
                }
                #endregion
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        // GetTestActors was deleted with the Actor Test Index (owner ruling 2026-08-24: that
        // surface was superseded by Test User). It was this class's only reader of
        // ITestUserQueries and the only caller of IActorQueries.Get(List<Guid>), both of which
        // went with it rather than being left as dependencies nothing uses.
        public async Task<Actor> GetByHashedMbox(string input)
        {
            //bool output = true;
            Actor output = new Actor();
            try
            {
                //use input to find subscription
                //output = subscription;
                #region Filter
                ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User);
                if (scope.Unrestricted)
                {
                    output = await _dataContext.GetByHashedMbox(input);
                }
                else
                {
                    Actor requested = await _dataContext.GetByHashedMbox(input);
                    if (requested != null && scope.AllowedActorUuids.Contains(requested.UUID))
                    {
                        output = requested;
                    }
                    else
                    {
                        return default;
                    }
                }
                #endregion
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<Actor>> GetByHashedMboxList(List<string> input)
        {
            // Batch variant for bulk-import paths (UserLogic.BulkCreate). Same
            // role gating as the single-hash overload above: callers without
            // the local-admin / educator / local-user scope get an empty list.
            List<Actor> output = new List<Actor>();
            try
            {
                if (input == null || input.Count == 0)
                {
                    return output;
                }
                ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User);
                if (scope.Unrestricted)
                {
                    output = await _dataContext.GetByHashedMboxList(input);
                }
                else
                {
                    List<Actor> candidates = await _dataContext.GetByHashedMboxList(input);
                    output = candidates?.Where(a => a != null && scope.AllowedActorUuids.Contains(a.UUID)).ToList() ?? new List<Actor>();
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<bool> Exists(string mbox)
        {
            bool output = false;
            try
            {
                output = await _dataContext.Exists(mbox);
                //output.AddRange(preoutput);
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "ActorLogic.Exists: suppressed exception"); }
            return output;
        }
        #endregion

        #region Post
        public async Task<Actor> Create(Actor input)
        {
            Actor output = new Actor();
            try
            {
                output = await _dataContext.Create(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        public async Task<Actor> Create(LocalApplicationUser newUser)
        {
            Actor output = new Actor();
            try
            {
                string mbox = Sha1Handler.TextToHash(newUser.Email);
                output = new Actor()
                {
                    Name = newUser.UserName,
                    //Mbox = new Uri(mbox),
                    Mbox_sha1sum = mbox,
                    ObjectType = "Agent"
                };
                output = await _dataContext.Create(output);
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
        public async Task<Actor> Update(Actor input)
        {
            Actor output = new Actor();
            try
            {
                output = await _dataContext.Update(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ActorLogic.Update: suppressed exception");
            }

            return output;
        }
        //public async Task<IActor> Update(IActor input)
        //{
        //    IActor output = new Actor();
        //    try
        //    {
        //        output = await _ActorQueries.Update(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion

        #region Delete
        // DELETED, and it must not come back. Deleting an Actor is not a tidy-up, it is destruction
        // of learning records: FK_LocalStatement_Actor_ActorId is ON DELETE CASCADE over a NOT NULL
        // column, so removing one Actor row deletes EVERY statement that learner ever produced.
        //
        // This wrapper had no caller, was not even on IActorLogic, and suppressed its own exception,
        // so a future "remove orphaned actors" job would have compiled, destroyed learner history,
        // and reported nothing. To remove a learner's identity, use Pseudonymise above: it clears
        // Name, Mbox and OpenId and keeps Mbox_sha1sum, which is a legal xAPI Inverse Functional
        // Identifier on its own, so the Agent stays valid and every statement stays attributable.
        // Removed 2026-08-15 alongside the actor pseudonymisation work.


        #endregion


        /// <summary>
        /// Strips a learner's identity from their xAPI Actor without destroying their learning
        /// record. <c>Name</c>, <c>Mbox</c> and <c>OpenId</c> are cleared, <c>Mbox_sha1sum</c> is
        /// KEPT, and the row itself is never removed.
        ///
        /// <para>
        /// <b>Why pseudonymise instead of delete.</b> Deleting the Actor row is not merely
        /// unsupported, it is destructive: <c>FK_LocalStatement_Actor_ActorId</c> is
        /// <c>ON DELETE CASCADE</c> with a NOT NULL column, so removing one Actor deletes EVERY
        /// statement that learner ever produced. That is the learning record this node exists to
        /// keep, and an LRS that quietly discards it on an account deletion is worthless.
        /// </para>
        ///
        /// <para>
        /// <b>Why this shape is spec-valid.</b> xAPI identifies an Agent by an Inverse Functional
        /// Identifier, exactly one of mbox, mbox_sha1sum, openid or account. <c>mbox_sha1sum</c> is
        /// a legal IFI on its own and exists in the standard precisely so an LRS can identify a
        /// learner without holding their raw address, so an Actor reduced to its hash is still a
        /// valid Agent and every statement referencing it stays attributable.
        /// </para>
        ///
        /// <para>
        /// <b>What this is not.</b> mbox_sha1sum is a SHA1 of the mailto IRI, so for a known email
        /// domain it is brute-forceable. This is pseudonymisation, not anonymisation. It removes the
        /// directly readable identity and reduces exposure; it does not make the learner
        /// unidentifiable to someone who already has the address.
        /// </para>
        /// </summary>
        public async Task<bool> Pseudonymise(Guid actorUuid)
        {
            try
            {
                if (actorUuid == Guid.Empty)
                {
                    return false;
                }

                Actor actor = await _dataContext.Get(actorUuid);
                if (actor == null)
                {
                    return false;
                }

                if (actor.Name == null && actor.Mbox == null && actor.OpenId == null)
                {
                    // Already pseudonymised. Idempotent, so a repeated purge is a no-op rather than
                    // a second write.
                    return false;
                }

                actor.Name = null;
                actor.Mbox = null;
                actor.OpenId = null;
                // Mbox_sha1sum is deliberately retained: it is the remaining IFI, and without it the
                // Actor would no longer be a valid xAPI Agent at all.

                await _dataContext.Update(actor);

                Febris.SharedServices.FebrisLog.Info(
                    "ActorLogic.Pseudonymise: cleared the identifying fields on actor " + actorUuid +
                    ". The actor row and its statements are retained.");
                return true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ActorLogic.Pseudonymise");
                return false;
            }
        }
    }

    public interface IMemberLogic
    {
        //Task<Member> Create(Professional newUser);
        Task<List<Member>> Get();
        Task<Member> Get(long? id);
        Task<Member> Create(Member actor);
    }

    public class MemberLogic : IMemberLogic
    {
        private readonly IMemberQueries _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        //private readonly ClaimsPrincipal User;
        public MemberLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new MemberQueries();
           // User = _httpContextAccessor.HttpContext.User;
        }

        #region Get                      
        public async Task<List<Member>> Get()
        {
            //bool output = true;
            List<Member> output = new List<Member>();
            try
            {
                output = await _context.Get();
                //output.AddRange(preoutput);
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "MemberLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<Member> Get(Guid input)
        {
            //bool output = true;
            Member output = new Member();
            try
            {
                //use input to find subscription
                output = await _context.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "MemberLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<Member> Get(long input)
        {
            //bool output = true;
            Member output = new Member();
            try
            {
                //use input to find subscription
                output = await _context.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "MemberLogic.Get: suppressed exception"); }
            return output;
        }
        public async Task<Member> Get(long? input)
        {
            //bool output = true;
            Member output = new Member();
            try
            {
                //use input to find subscription
                output = await _context.Get(input);
                //output = subscription;
            }
            catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "MemberLogic.Get: suppressed exception"); }
            return output;
        }

        #endregion

        #region Post
        public async Task<Member> Create(Member input)
        {
            Member output = new Member();
            try
            {
                output = await _context.Create(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "MemberLogic.Create: suppressed exception");
            }

            return output;
        }
        //public Task<Member> Create(Professional newUser)
        //{
        //    Member output = new Member();
        //    try
        //    {
        //        output = await _context.Create(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion

        #region Update
        public async Task<Member> Update(Member input)
        {
            Member output = new Member();
            try
            {
                output = await _context.Update(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "MemberLogic.Update: suppressed exception");
            }

            return output;
        }

        #endregion

        #region Delete
        public async Task<bool> Delete(Member input)
        {
            bool output = false;
            try
            {
                output = await _context.Delete(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "MemberLogic.Delete: suppressed exception");
            }

            return output;
        }


        #endregion


    }
}
