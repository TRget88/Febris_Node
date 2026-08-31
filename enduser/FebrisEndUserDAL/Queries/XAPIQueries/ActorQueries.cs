// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.XApiModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.ModelLibrary.Models.DataModels;

namespace Febris.UserNode.DataAccessLayer.Queries.XAPIQueries
{
    public interface IActorQueries
    {
        Task<List<Actor>> Get();
        Task<Actor> Get(Guid input);
        Task<Actor> Get(long input);
        Task<bool> Delete(Actor input);
        Task<Actor> Update(Actor input);
        Task<Actor> Create(Actor input);
        Task<Actor> Get(long? input);
        Task<Actor> GetByMbox(Uri uri);
        Task<Actor> GetByHashedMbox(string v);
        Task<List<Actor>> GetByHashedMboxList(List<string> hashes);
        Task<bool> Exists(string mbox);
        Task<List<Actor>> Get(List<long> actorIdList);
        //Task<Actor> GetByMboxSha(string input);
    }
    /// <summary>
    /// Audit C-03: every single-entity lookup here used <c>FirstAsync()</c> (10 calls, zero
    /// <c>FirstOrDefaultAsync</c>) against a field pre-initialised to <c>new Actor()</c>, with the
    /// resulting throw swallowed by the catch. A miss therefore returned a BLANK NON-NULL Actor,
    /// which made <c>StatementFactor</c>'s unknown-actor rejection (<c>if (!actorFound) return
    /// null;</c>) unreachable dead code: the very first probe "succeeded", the mbox and
    /// mbox_sha1sum fallbacks never ran, and statements for unprovisioned learners were stored
    /// against IFI-less ghost rows carrying database-generated UUIDs no read path can reach.
    ///
    /// <para>
    /// The lookups now return null on a miss so that guard is live. Callers must treat null as
    /// "not provisioned" -- that is the point, not a regression.
    /// </para>
    /// </summary>
    public class ActorQueries : IActorQueries
    {
        private readonly XApiDbContext _xApiDbContext;
        public ActorQueries(XApiDbContext xApiDbContext)
        {
            _xApiDbContext = xApiDbContext;
        }
        public ActorQueries()
        {
            _xApiDbContext = new XApiDbContext(XApiDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<Actor> Get(long input)
        {
            Actor Actor = null;
            try
            {
                Actor = await _xApiDbContext.Actor
                    .AsNoTracking()
                    .Include(i => i.Member)
                    .Include(i => i.Account)
                    .Where(i => i.Id == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }

            return Actor;
        }
        public async Task<List<Actor>> Get(List<long> input)
        {
            List<Actor> Actor = new List<Actor>();
            try
            {
                Actor = await _xApiDbContext.Actor
                    .AsNoTracking()
                    .Include(i => i.Member)
                    .Include(i => i.Account)
                    .Where(i => input.Any(j=>j==i.Id))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }

            return Actor;
        }
        // The Get(List<Guid>) batch overload was deleted with the Actor Test Index (owner ruling
        // 2026-08-24). ActorLogic.GetTestActors was its only caller.
        public async Task<Actor> Get(long? input)
        {
            Actor Actor = null;
            try
            {
                Actor = await _xApiDbContext.Actor
                   .AsNoTracking()
                   .Include(i => i.Member)
                   .Include(i => i.Account)
                   .Where(i => i.Id == input)
                   .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }

            return Actor;
        }
        public async Task<List<Actor>> Get()
        {
            List<Actor> ActorList = new List<Actor>();
            try
            {
                ActorList = await _xApiDbContext.Actor
                   .AsNoTracking()
                   .Include(i => i.Member)
                   .Include(i => i.Account)
                   .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }

            return ActorList;
        }
        public async Task<Actor> Get(Guid input)
        {
            Actor Actor = null;
            try
            {
                Actor = await _xApiDbContext.Actor
                   .AsNoTracking()
                   .Include(i => i.Member)
                   .Include(i => i.Account)
                   .Where(i => i.UUID == input)
                   .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }

            return Actor;
        }
        public async Task<Actor> GetByMbox(Uri input)
        {
            Actor Actor = null;
            try
            {
                Actor = await _xApiDbContext.Actor
                 .AsNoTracking()
                 .Include(i => i.Member)
                 .Include(i => i.Account)
                 .Where(i => i.Mbox == input)
                 .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }

            return Actor;
        }
        public async Task<Actor> GetByHashedMbox(string input)
        {
            Actor Actor = null;
            try
            {
                Actor = await _xApiDbContext.Actor
                .AsNoTracking()
                .Include(i => i.Member)
                .Include(i => i.Account)
                .Where(i => i.Mbox_sha1sum == input)
                .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }

            return Actor;
        }
        public async Task<List<Actor>> GetByHashedMboxList(List<string> input)
        {
            // Batch fetch for bulk-user-import scenarios. Single query with
            // `WHERE Mbox_sha1sum IN (...)` instead of two round-trips per
            // user (Exists + GetByHashedMbox) in the previous BLL loop.
            List<Actor> output = new List<Actor>();
            try
            {
                if (input == null || input.Count == 0)
                {
                    return output;
                }

                output = await _xApiDbContext.Actor
                    .AsNoTracking()
                    .Include(i => i.Member)
                    .Include(i => i.Account)
                    .Where(i => input.Contains(i.Mbox_sha1sum))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }
        #endregion

        #region Create
        public async Task<Actor> Create(Actor input)
        {
            try
            {
                await _xApiDbContext.Actor.AddAsync(input);
                await _xApiDbContext.SaveChangesAsync();
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
        public async Task<Actor> Update(Actor input)
        {
            try
            {
                _xApiDbContext.Actor.Update(input);
                await _xApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ActorQueries.Update: suppressed exception");
            }

            return input;
        }
        #endregion 

        #region Delete
        public async Task<bool> Delete(Actor input)
        {
            bool output = false;
            try
            {
                _xApiDbContext.Actor.Remove(input);
                await _xApiDbContext.SaveChangesAsync();
                output = true;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ActorQueries.Delete: suppressed exception");
            }

            return output;
        }
        #endregion
        public async Task<bool> Exists(string mbox)
        {
            bool output = false;
            try
            {
                output = await _xApiDbContext.Actor
                    .Include(i => i.Member)
                    .Include(i => i.Account)
                    .Where(i => i.Mbox_sha1sum == mbox)
                    .AnyAsync();
                //await _xApiDbContext.SaveChangesAsync();

            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ActorQueries.Exists: suppressed exception");
            }

            return output;
        }



    }

    public interface IMemberQueries
    {
        Task<List<Member>> Get();
        Task<Member> Get(Guid input);
        Task<Member> Get(long input);
        Task<bool> Delete(Member input);
        Task<Member> Update(Member input);
        Task<Member> Create(Member input);
        Task<Member> Get(long? input);
        //Task<Member> GetByMbox(Uri uri);
        //Task<Member> GetByHashedMbox(string v);
    }
    public class MemberQueries : IMemberQueries
    {
        private readonly XApiDbContext _xApiDbContext;
        public MemberQueries(XApiDbContext xApiDbContext)
        {
            _xApiDbContext = xApiDbContext;
        }
        public MemberQueries()
        {
            _xApiDbContext = new XApiDbContext(XApiDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<Member> Get(long input)
        {
            Member Member = null;
            try
            {
                Member = await _xApiDbContext.Member
                    .AsNoTracking()
                    .Include(i => i.Actors)
                    .Where(i => i.Id == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }

            return Member;
        }
        public async Task<Member> Get(long? input)
        {
            Member Member = null;
            try
            {
                Member = await _xApiDbContext.Member
                   .AsNoTracking()
                   .Include(i => i.Actors)
                   .Where(i => i.Id == input)
                   .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }

            return Member;
        }
        public async Task<List<Member>> Get()
        {
            List<Member> ActorList = new List<Member>();
            try
            {
                ActorList = await _xApiDbContext.Member
                   .AsNoTracking()
                   .Include(i => i.Actors)
                   .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }

            return ActorList;
        }
        public async Task<Member> Get(Guid input)
        {
            Member Member = null;
            try
            {
                Member = await _xApiDbContext.Member
                   .AsNoTracking()
                   .Include(i => i.Actors)
                   .Where(i => i.UUID == input)
                   .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }

            return Member;
        }
        //public async Task<Member> GetByMbox(Uri input)
        //{
        //    Member Member = new Member();
        //    try
        //    {
        //        Member = await _xApiDbContext.Member
        //         .Include(i => i.Member)
        //         .Include(i => i.Account)
        //         .Where(i => i.Mbox == input)
        //         .FirstAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //    }

        //    return Member;
        //}
        //public async Task<Member> GetByHashedMbox(string input)
        //{
        //    Member Member = new Member();
        //    try
        //    {
        //        Member = await _xApiDbContext.Member
        //        .Include(i => i.Member)
        //        .Include(i => i.Account)
        //        .Where(i => i.Mbox_sha1sum == input)
        //        .FirstAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //    }

        //    return Member;
        //}
        #endregion

        #region Create
        public async Task<Member> Create(Member input)
        {
            try
            {
                await _xApiDbContext.Member.AddAsync(input);
                await _xApiDbContext.SaveChangesAsync();
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
        public async Task<Member> Update(Member input)
        {
            try
            {
                _xApiDbContext.Member.Update(input);
                await _xApiDbContext.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "MemberQueries.Update: suppressed exception");
            }

            return input;
        }
        #endregion 

        #region Delete
        public async Task<bool> Delete(Member input)
        {
            bool output = false;
            try
            {
                _xApiDbContext.Member.Remove(input);
                await _xApiDbContext.SaveChangesAsync();
                output = true;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "MemberQueries.Delete: suppressed exception");
            }

            return output;
        }


        #endregion
    }
}
