// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.Models.XApiModels.ModifiedForSharing;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.XApiQueries
{
    public interface IStatementQueries
    {
        Task<LocalStatement> Create(LocalStatement input);
        Task<List<LocalStatement>> Get();
        Task<LocalStatement> Get(long? id);
        Task<LocalStatement> Get(Guid? id);
        /// <summary>Dedupe lookup: sees voided statements, unlike every other read.</summary>
        Task<LocalStatement> GetIncludingVoided(Guid? id);
        /// <summary>Sets the voiding marker. One-way: never clears it.</summary>
        Task<bool> MarkVoided(long id, DateTime voidedAtUtc, Guid? voidedByUserId);
        Task<List<LocalStatement>> SearchGet(string searchString);
        Task<List<LocalStatement>> SearchGet(Guid actorId, string searchString);
        Task<List<LocalStatement>> Get(DateTime startDate, DateTime endDate);
        Task<List<LocalStatement>> GetByActorList(List<long> input);
        Task<List<LocalStatement>> GetByActorList(List<Guid> actorList);
        Task<List<LocalStatement>> GetByActorList(List<Guid> actorIdList, DateTime startDate, DateTime endDate);
        Task<List<LocalStatement>> GetByActor(long input);
        Task<List<LocalStatement>> GetByActor(Guid input);
        Task<List<LocalStatement>> GetByActor(Guid input, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Statements SUBMITTED by a given device, newest first, capped at <paramref name="limit"/>.
        ///
        /// <para>
        /// This is the READ SIDE of <c>LocalStatement.SubmittedByHardwareUUID</c>. That column was
        /// added so a forged record would be "investigable instead of indistinguishable", and it
        /// shipped with two writers and NO reader, which meant the investigation it promised needed
        /// direct database access. This is the query that makes the promise real.
        /// </para>
        ///
        /// <para>
        /// DELIBERATELY DISTINCT FROM <c>GetByActor</c>. The actor is who performed the activity and
        /// the submitter is who sent it, and the entire point of the column is that those two can
        /// disagree. A shared classroom device submitting for thirty learners returns thirty actors
        /// here and that is correct, not a defect.
        /// </para>
        ///
        /// <para>
        /// CAPPED because a busy device accumulates statements without bound, and an uncapped read
        /// behind a Portal page is how a detail view becomes a timeout. Callers wanting the true
        /// total ask <see cref="CountBySubmittingHardware"/>, which does not materialise the rows.
        /// </para>
        ///
        /// <para>
        /// KNOWN LIMITATION, STATED SO NOBODY IS MISLED MID-INCIDENT. Like every other read on this
        /// interface except <see cref="GetIncludingVoided"/>, this passes through the global query
        /// filter <c>VoidedAt == null</c>, so a VOIDED statement does not appear here and is not
        /// counted. That is the right default for reporting and the wrong one for a forensic
        /// question, because voiding is exactly what someone covering their tracks would do. An
        /// including-voided twin is the obvious follow-up and is deliberately not built here, since
        /// it needs its own decision about who may see retracted records.
        /// </para>
        /// </summary>
        Task<List<LocalStatement>> GetBySubmittingHardware(Guid hardwareUuid, int limit);

        /// <summary>
        /// How many statements this device has submitted. Separate from
        /// <see cref="GetBySubmittingHardware"/> so a page can honestly say "showing 20 of 4,312"
        /// rather than implying the capped list is the whole story.
        /// </summary>
        Task<int> CountBySubmittingHardware(Guid hardwareUuid);
    }
    public class StatementQueries : IStatementQueries
    {
        private readonly XApiDbContext _context;
        public StatementQueries(XApiDbContext xApiDbContext)
        {
            _context = xApiDbContext;
        }
        public StatementQueries()
        {
            _context = new XApiDbContext(XApiDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<LocalStatement> Get(long? input)
        {
            LocalStatement output = new LocalStatement();
            try
            {
                // output = await _xApiDbContext.LocalStatement.FindAsync(input);
                output = await _context.LocalStatement
                   .AsNoTracking()
                   .Include(a => a.Actor).ThenInclude(i => i.Account)
                   .Include(a => a.Actor).ThenInclude(i => i.Member)
                   //.Include(a => a.Actor.Account)
                   //.Include(v => v.Verb)
                   //.Include(o => o.Object)
                   //.Include(d => d.Object.Definition)
                   .Include(r => r.Result).ThenInclude(i => i.Score)
                   .Include(r => r.Result).ThenInclude(i => i.Extensions)
                   //.Include(r => r.Result.Score)
                   //.Include(s => s.Result.Extensions)
                   .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                   .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                   .Include(c => c.Context).ThenInclude(c => c.Instructor)
                   .Include(c => c.Context).ThenInclude(c => c.Extensions)
                   //.Include(c => c.Context.ContextActivities)
                   //.Include(c => c.Context.Extensions)
                   //.Include(c => c.Context.StatementReference)
                   //.Include(c => c.Context.Instructor)
                   .Include(a => a.Authority).ThenInclude(a => a.Actor)
                   //.Include(a => a.Authority.Actor)
                   //.Include(v => v.Version)
                   .Include(a => a.Attachments)
                   .Where(i => i.Id == input).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        /// <summary>
        /// Dedupe lookup by producer UUID, which MUST see voided statements.
        ///
        /// <para>
        /// T5 put a global query filter on LocalStatement excluding voided rows, so every read on
        /// this context hides them. That is right for reads, and wrong here: if the dedupe lookup
        /// could not see a voided statement, re-sending that statement's id would find nothing and
        /// insert a SECOND row carrying the same producer id. Voiding a statement would quietly
        /// become a way to defeat idempotent ingest.
        /// </para>
        ///
        /// <para>
        /// A separate method rather than relaxing the filter on <see cref="Get(Guid?)"/>, because
        /// that overload also backs the public single-statement read, which must keep hiding voided
        /// rows. Same query, one call different.
        /// </para>
        /// </summary>
        public async Task<LocalStatement> GetIncludingVoided(Guid? input)
        {
            try
            {
                return await _context.LocalStatement
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(i => i.UUID == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// Sets the voiding marker on a statement, and NOTHING else. The target's verb, result,
        /// context and attachments are left exactly as the producer sent them -- the 2021
        /// implementation overwrote the verb, which destroyed the record of what the learner did.
        ///
        /// <para>
        /// IgnoreQueryFilters because the row being marked is about to become invisible to every
        /// ordinary read, and a second call must be able to see it in order to be a no-op rather
        /// than a silent miss.
        /// </para>
        /// </summary>
        public async Task<bool> MarkVoided(long id, DateTime voidedAtUtc, Guid? voidedByUserId)
        {
            try
            {
                LocalStatement target = await _context.LocalStatement
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(i => i.Id == id);
                if (target == null)
                {
                    return false;
                }
                target.VoidedAt = voidedAtUtc;
                target.VoidedByUserId = voidedByUserId;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<LocalStatement> Get(Guid? input)
        {
            LocalStatement output = new LocalStatement();
            try
            {
                output = await _context.LocalStatement
                  .AsNoTracking()
                  .Include(a => a.Actor).ThenInclude(i => i.Account)
                  .Include(a => a.Actor).ThenInclude(i => i.Member)
                  //.Include(a => a.Actor.Account)
                  //.Include(v => v.Verb)
                  //.Include(o => o.Object)
                  //.Include(d => d.Object.Definition)
                  .Include(r => r.Result).ThenInclude(i => i.Score)
                  .Include(r => r.Result).ThenInclude(i => i.Extensions)
                  //.Include(r => r.Result.Score)
                  //.Include(s => s.Result.Extensions)
                  .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                  .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                  .Include(c => c.Context).ThenInclude(c => c.Instructor)
                  .Include(c => c.Context).ThenInclude(c => c.Extensions)
                  //.Include(c => c.Context.ContextActivities)
                  //.Include(c => c.Context.Extensions)
                  //.Include(c => c.Context.StatementReference)
                  //.Include(c => c.Context.Instructor)
                  .Include(a => a.Authority).ThenInclude(a => a.Actor)
                  //.Include(a => a.Authority.Actor)
                  //.Include(v => v.Version)
                  .Include(a => a.Attachments)
                  .Where(i => i.UUID == input).FirstOrDefaultAsync();
                //output = await _xApiDbContext.LocalStatement
                //    .Where(i => i.UUID == input)
                //    .Include(a => a.Actor.Account)
                //    //.Include(v => v.Verb)
                //    //.Include(o => o.Object)
                //    //.Include(d => d.Object.Definition)
                //    .Include(r => r.Result)
                //    .Include(r => r.Result.Score)
                //    .Include(s => s.Result.Extensions)
                //    .Include(c => c.Context)
                //    //.Include(c => c.Context.ContextActivities)
                //    .Include(c => c.Context.Extensions)
                //    .Include(c => c.Context.StatementReference)
                //    .Include(c => c.Context.Instructor)
                //    .Include(a => a.Authority)
                //    .Include(a => a.Authority.Actor)
                //    //.Include(v => v.Version)
                //    .Include(a => a.Attachments)
                //    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        public async Task<List<LocalStatement>> Get()
        {
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {
                output = await _context.LocalStatement
                    .AsNoTracking()
                    .Include(a => a.Actor).ThenInclude(i => i.Account)
                    .Include(a => a.Actor).ThenInclude(i => i.Member)
                    //.Include(a => a.Actor.Account)
                    //.Include(v => v.Verb)
                    //.Include(o => o.Object)
                    //.Include(d => d.Object.Definition)
                    .Include(r => r.Result).ThenInclude(i => i.Score)
                    .Include(r => r.Result).ThenInclude(i => i.Extensions)
                    //.Include(r => r.Result.Score)
                    //.Include(s => s.Result.Extensions)
                    .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                    .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                    .Include(c => c.Context).ThenInclude(c => c.Instructor)
                    .Include(c => c.Context).ThenInclude(c => c.Extensions)
                    //.Include(c => c.Context.ContextActivities)
                    //.Include(c => c.Context.Extensions)
                    //.Include(c => c.Context.StatementReference)
                    //.Include(c => c.Context.Instructor)
                    .Include(a => a.Authority).ThenInclude(a => a.Actor)
                    //.Include(a => a.Authority.Actor)
                    //.Include(v => v.Version)
                    .Include(a => a.Attachments)
                    .ToListAsync();
                IEnumerable<LocalStatement> outputSorted = output.OrderBy(t => t.Timestamp);
                output = outputSorted.ToList();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        public async Task<List<LocalStatement>> Get(DateTime startDate, DateTime endDate)
        {
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {
                output = await _context.LocalStatement
                    .AsNoTracking()
                    .Include(a => a.Actor).ThenInclude(i => i.Account)
                    .Include(a => a.Actor).ThenInclude(i => i.Member)
                    .Include(r => r.Result).ThenInclude(i => i.Score)
                    .Include(r => r.Result).ThenInclude(i => i.Extensions)
                    .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                    .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                    .Include(c => c.Context).ThenInclude(c => c.Instructor)
                    .Include(c => c.Context).ThenInclude(c => c.Extensions)
                    .Include(a => a.Authority).ThenInclude(a => a.Actor)
                    .Include(a => a.Attachments)
                    .Where(i => i.Timestamp.Date >= startDate.Date
                    && i.Timestamp.Date <= endDate.AddDays(1).Date
                    )
                    .OrderByDescending(i => i.Timestamp).ToListAsync();
                // IEnumerable<LocalStatement> outputSorted = output.OrderBy(t => t.Timestamp);
                //output = outputSorted.ToList();

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        public async Task<List<LocalStatement>> SearchGet(string searchString)
        {
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {
                output = await _context.LocalStatement
                    .AsNoTracking()
                    .Include(a => a.Actor).ThenInclude(i => i.Account)
                    .Include(a => a.Actor).ThenInclude(i => i.Member)
                    .Include(r => r.Result).ThenInclude(i => i.Score)
                    .Include(r => r.Result).ThenInclude(i => i.Extensions)
                    .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                    .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                    .Include(c => c.Context).ThenInclude(c => c.Instructor)
                    .Include(c => c.Context).ThenInclude(c => c.Extensions)
                    .Include(a => a.Authority).ThenInclude(a => a.Actor)
                    .Include(a => a.Attachments)
                    .Where(b => (b.VerbId.ToString().Contains(searchString))
                        || (b.ObjectId.ToString().Contains(searchString))
                        || (b.Timestamp.ToString().Contains(searchString))
                        || (b.Stored.ToString().Contains(searchString))
                        || (b.Actor.Name.Contains(searchString)))
                        .ToListAsync();

                IEnumerable<LocalStatement> outputSorted = output.OrderBy(t => t.Timestamp);
                output = outputSorted.ToList();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        public async Task<List<LocalStatement>> SearchGet(Guid actorId, string searchString)
        {
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {
                output = await _context.LocalStatement
                    .AsNoTracking()
                    .Include(a => a.Actor).ThenInclude(i => i.Account)
                    .Include(a => a.Actor).ThenInclude(i => i.Member)
                    .Include(r => r.Result).ThenInclude(i => i.Score)
                    .Include(r => r.Result).ThenInclude(i => i.Extensions)
                    .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                    .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                    .Include(c => c.Context).ThenInclude(c => c.Instructor)
                    .Include(c => c.Context).ThenInclude(c => c.Extensions)
                    .Include(a => a.Authority).ThenInclude(a => a.Actor)
                    .Include(a => a.Attachments)
                    .Where(b => actorId == b.Actor.UUID
                        && ((b.VerbId.ToString().Contains(searchString))
                        || (b.ObjectId.ToString().Contains(searchString))
                        || (b.Timestamp.ToString().Contains(searchString))
                        || (b.Stored.ToString().Contains(searchString))
                        || (b.Actor.Name.Contains(searchString))))
                        .ToListAsync();
                IEnumerable<LocalStatement> outputSorted = output.OrderBy(t => t.Timestamp);
                output = outputSorted.ToList();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        public async Task<List<LocalStatement>> GetByActorList(List<long> input)
        {
            // Batch fetch: single query with `WHERE Actor.Id IN (...)` instead
            // of one heavy multi-Include query per actor id. With cohorts of
            // 50+ actors and the ten-deep Include chain, the previous foreach
            // was the slowest call on cohort-roster pages.
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {
                if (input == null || input.Count == 0)
                {
                    return output;
                }

                output = await _context.LocalStatement
                    .AsNoTracking()
                    .Include(a => a.Actor).ThenInclude(i => i.Account)
                    .Include(a => a.Actor).ThenInclude(i => i.Member)
                    .Include(r => r.Result).ThenInclude(i => i.Score)
                    .Include(r => r.Result).ThenInclude(i => i.Extensions)
                    .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                    .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                    .Include(c => c.Context).ThenInclude(c => c.Instructor)
                    .Include(c => c.Context).ThenInclude(c => c.Extensions)
                    .Include(a => a.Authority).ThenInclude(a => a.Actor)
                    .Include(a => a.Attachments)
                    .Where(i => input.Contains(i.Actor.Id))
                    .OrderBy(t => t.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }


        public async Task<List<LocalStatement>> GetByActorList(List<Guid> input)
        {
            // Batch fetch by Actor UUID; see GetByActorList(List<long>) above.
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {
                if (input == null || input.Count == 0)
                {
                    return output;
                }

                output = await _context.LocalStatement
                    .AsNoTracking()
                    .Include(a => a.Actor).ThenInclude(i => i.Account)
                    .Include(a => a.Actor).ThenInclude(i => i.Member)
                    .Include(r => r.Result).ThenInclude(i => i.Score)
                    .Include(r => r.Result).ThenInclude(i => i.Extensions)
                    .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                    .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                    .Include(c => c.Context).ThenInclude(c => c.Instructor)
                    .Include(c => c.Context).ThenInclude(c => c.Extensions)
                    .Include(a => a.Authority).ThenInclude(a => a.Actor)
                    .Include(a => a.Attachments)
                    .Where(i => input.Contains(i.Actor.UUID))
                    .OrderBy(t => t.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<LocalStatement>> GetByActor(long input)
        {
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {

                List<LocalStatement> temp = await _context.LocalStatement
                .AsNoTracking()
                .Include(a => a.Actor).ThenInclude(i => i.Account)
                .Include(a => a.Actor).ThenInclude(i => i.Member)
                //.Include(a => a.Actor.Account)
                //.Include(v => v.Verb)
                //.Include(o => o.Object)
                //.Include(d => d.Object.Definition)
                .Include(r => r.Result).ThenInclude(i => i.Score)
                .Include(r => r.Result).ThenInclude(i => i.Extensions)
                //.Include(r => r.Result.Score)
                //.Include(s => s.Result.Extensions)
                .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                .Include(c => c.Context).ThenInclude(c => c.Instructor)
                .Include(c => c.Context).ThenInclude(c => c.Extensions)
                //.Include(c => c.Context.ContextActivities)
                //.Include(c => c.Context.Extensions)
                //.Include(c => c.Context.StatementReference)
                //.Include(c => c.Context.Instructor)
                .Include(a => a.Authority).ThenInclude(a => a.Actor)
                //.Include(a => a.Authority.Actor)
                //.Include(v => v.Version)
                .Include(a => a.Attachments)
                .Where(i => input == i.Actor.Id)
                .OrderByDescending(i => i.Timestamp)
                .ToListAsync();

                output.AddRange(temp);
                IEnumerable<LocalStatement> outputSorted = output.OrderBy(t => t.Timestamp);
                output = outputSorted.ToList();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        public async Task<List<LocalStatement>> GetByActor(Guid input)
        {
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {
                List<LocalStatement> temp = await _context.LocalStatement
                .AsNoTracking()
                .Include(a => a.Actor).ThenInclude(i => i.Account)
                .Include(a => a.Actor).ThenInclude(i => i.Member)
                //.Include(a => a.Actor.Account)
                //.Include(v => v.Verb)
                //.Include(o => o.Object)
                //.Include(d => d.Object.Definition)
                .Include(r => r.Result).ThenInclude(i => i.Score)
                .Include(r => r.Result).ThenInclude(i => i.Extensions)
                //.Include(r => r.Result.Score)
                //.Include(s => s.Result.Extensions)
                .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                .Include(c => c.Context).ThenInclude(c => c.Instructor)
                .Include(c => c.Context).ThenInclude(c => c.Extensions)
                //.Include(c => c.Context.ContextActivities)
                //.Include(c => c.Context.Extensions)
                //.Include(c => c.Context.StatementReference)
                //.Include(c => c.Context.Instructor)
                .Include(a => a.Authority).ThenInclude(a => a.Actor)
                //.Include(a => a.Authority.Actor)
                //.Include(v => v.Version)
                .Include(a => a.Attachments)
                .Where(i => input == i.Actor.UUID)
                .OrderByDescending(i => i.Timestamp)
                .ToListAsync();
                output.AddRange(temp);
                IEnumerable<LocalStatement> outputSorted = output.OrderBy(t => t.Timestamp);
                output = outputSorted.ToList();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<LocalStatement>> GetByActor(Guid input, DateTime startDate, DateTime endDate)
        {
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {
                List<LocalStatement> temp = await _context.LocalStatement
                .AsNoTracking()
                .Include(a => a.Actor).ThenInclude(i => i.Account)
                .Include(a => a.Actor).ThenInclude(i => i.Member)
                //.Include(a => a.Actor.Account)
                //.Include(v => v.Verb)
                //.Include(o => o.Object)
                //.Include(d => d.Object.Definition)
                .Include(r => r.Result).ThenInclude(i => i.Score)
                .Include(r => r.Result).ThenInclude(i => i.Extensions)
                //.Include(r => r.Result.Score)
                //.Include(s => s.Result.Extensions)
                .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                .Include(c => c.Context).ThenInclude(c => c.Instructor)
                .Include(c => c.Context).ThenInclude(c => c.Extensions)
                //.Include(c => c.Context.ContextActivities)
                //.Include(c => c.Context.Extensions)
                //.Include(c => c.Context.StatementReference)
                //.Include(c => c.Context.Instructor)
                .Include(a => a.Authority).ThenInclude(a => a.Actor)
                //.Include(a => a.Authority.Actor)
                //.Include(v => v.Version)
                .Include(a => a.Attachments)
                .Where(i => input == i.Actor.UUID &&
                    i.Timestamp.Date >= startDate.Date &&
                    i.Timestamp.Date <= endDate.AddDays(1).Date)
                .OrderByDescending(i => i.Timestamp)
                .ToListAsync();
                output.AddRange(temp);
                IEnumerable<LocalStatement> outputSorted = output.OrderBy(t => t.Timestamp);
                output = outputSorted.ToList();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }



        /// <inheritdoc />
        public async Task<List<LocalStatement>> GetBySubmittingHardware(Guid hardwareUuid, int limit)
        {
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {
                // Guid.Empty is what an unpopulated hardware reference looks like, and the column is
                // null for every statement that did not arrive over a device-authenticated route.
                // Matching on it would sweep together everything unattributed and present it as one
                // device's activity, which is worse than returning nothing because it reads as real.
                if (hardwareUuid == Guid.Empty)
                {
                    return output;
                }

                // Actor is included because the whole question this answers is "which learners did
                // this device submit for", so a caller displaying the result needs the actor. The
                // heavier Result/Context/Attachment graphs that GetByActor pulls are deliberately
                // NOT included: this is an attribution list, not a statement export, and loading
                // them would make an investigation screen expensive for data it does not show.
                output = await _context.LocalStatement
                .AsNoTracking()
                .Include(a => a.Actor).ThenInclude(i => i.Account)
                .Where(i => i.SubmittedByHardwareUUID == hardwareUuid)
                .OrderByDescending(i => i.Timestamp)
                .Take(limit)
                .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        /// <inheritdoc />
        public async Task<int> CountBySubmittingHardware(Guid hardwareUuid)
        {
            int output = 0;
            try
            {
                if (hardwareUuid == Guid.Empty)
                {
                    return 0;
                }

                output = await _context.LocalStatement
                    .AsNoTracking()
                    .Where(i => i.SubmittedByHardwareUUID == hardwareUuid)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<LocalStatement>> GetByActorList(List<Guid> input, DateTime startDate, DateTime endDate)
        {
            // Batch fetch by Actor UUID with date-range filter. Single query
            // replaces previous foreach (one query per actor id).
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {
                if (input == null || input.Count == 0)
                {
                    return output;
                }

                DateTime startCutoff = startDate.Date;
                DateTime endCutoff = endDate.AddDays(1).Date;

                output = await _context.LocalStatement
                    .AsNoTracking()
                    .Include(a => a.Actor).ThenInclude(i => i.Account)
                    .Include(a => a.Actor).ThenInclude(i => i.Member)
                    .Include(r => r.Result).ThenInclude(i => i.Score)
                    .Include(r => r.Result).ThenInclude(i => i.Extensions)
                    .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                    .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                    .Include(c => c.Context).ThenInclude(c => c.Instructor)
                    .Include(c => c.Context).ThenInclude(c => c.Extensions)
                    .Include(a => a.Authority).ThenInclude(a => a.Actor)
                    .Include(a => a.Attachments)
                    .Where(i => input.Contains(i.Actor.UUID) &&
                                i.Timestamp.Date >= startCutoff &&
                                i.Timestamp.Date <= endCutoff)
                    .OrderBy(t => t.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        #region Get by object
        public async Task<List<LocalStatement>> Get(ModelLibrary.Models.XApiModels.Object input)
        {
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {
                output = await _context.LocalStatement
                  .AsNoTracking()
                  .Include(a => a.Actor).ThenInclude(i => i.Account)
                  .Include(a => a.Actor).ThenInclude(i => i.Member)
                  //.Include(a => a.Actor.Account)
                  //.Include(v => v.Verb)
                  //.Include(o => o.Object)
                  //.Include(d => d.Object.Definition)
                  .Include(r => r.Result).ThenInclude(i => i.Score)
                  .Include(r => r.Result).ThenInclude(i => i.Extensions)
                  //.Include(r => r.Result.Score)
                  //.Include(s => s.Result.Extensions)
                  .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                  .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                  .Include(c => c.Context).ThenInclude(c => c.Instructor)
                  .Include(c => c.Context).ThenInclude(c => c.Extensions)
                  //.Include(c => c.Context.ContextActivities)
                  //.Include(c => c.Context.Extensions)
                  //.Include(c => c.Context.StatementReference)
                  //.Include(c => c.Context.Instructor)
                  .Include(a => a.Authority).ThenInclude(a => a.Actor)
                  //.Include(a => a.Authority.Actor)
                  //.Include(v => v.Version)
                  .Include(a => a.Attachments)
                  .Where(i => i.ObjectUUID == input.UUID).ToListAsync();
                //output = await _xApiDbContext.LocalStatement
                //    .Include(a => a.Actor)
                //    .Include(a => a.Actor.Account)
                //    //.Include(v => v.Verb)
                //    //.Include(o => o.Object)
                //    //.Include(d => d.Object.Definition)
                //    .Include(r => r.Result)
                //    .Include(r => r.Result.Score)
                //    .Include(s => s.Result.Extensions)
                //    .Include(c => c.Context)
                //    //.Include(c => c.Context.ContextActivities)
                //    .Include(c => c.Context.Extensions)
                //    .Include(c => c.Context.StatementReference)
                //    .Include(c => c.Context.Instructor)
                //    .Include(a => a.Authority)
                //    .Include(a => a.Authority.Actor)
                //    //.Include(v => v.Version)
                //    .Include(a => a.Attachments)
                //    .Where(i => i.ObjectUUID == input.UUID)
                //    .ToListAsync();
                IEnumerable<LocalStatement> outputSorted = output.OrderBy(t => t.Timestamp);
                output = outputSorted.ToList();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        #endregion

        #region Get by verb
        public async Task<List<LocalStatement>> Get(Verb input)
        {
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {
                output = await _context.LocalStatement
                  .AsNoTracking()
                  .Include(a => a.Actor).ThenInclude(i => i.Account)
                  .Include(a => a.Actor).ThenInclude(i => i.Member)
                  //.Include(a => a.Actor.Account)
                  //.Include(v => v.Verb)
                  //.Include(o => o.Object)
                  //.Include(d => d.Object.Definition)
                  .Include(r => r.Result).ThenInclude(i => i.Score)
                  .Include(r => r.Result).ThenInclude(i => i.Extensions)
                  //.Include(r => r.Result.Score)
                  //.Include(s => s.Result.Extensions)
                  .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                  .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                  .Include(c => c.Context).ThenInclude(c => c.Instructor)
                  .Include(c => c.Context).ThenInclude(c => c.Extensions)
                  //.Include(c => c.Context.ContextActivities)
                  //.Include(c => c.Context.Extensions)
                  //.Include(c => c.Context.StatementReference)
                  //.Include(c => c.Context.Instructor)
                  .Include(a => a.Authority).ThenInclude(a => a.Actor)
                  //.Include(a => a.Authority.Actor)
                  //.Include(v => v.Version)
                  .Include(a => a.Attachments)
                  .Where(i => i.VerbUUID == input.UUID)
                  .ToListAsync();
                //output = await _xApiDbContext.LocalStatement
                //    .Include(a => a.Actor)
                //    .Include(a => a.Actor.Account)
                //    //.Include(v => v.Verb)
                //    //.Include(o => o.Object)
                //    //.Include(d => d.Object.Definition)
                //    .Include(r => r.Result)
                //    .Include(r => r.Result.Score)
                //    .Include(s => s.Result.Extensions)
                //    .Include(c => c.Context)
                //    //.Include(c => c.Context.ContextActivities)
                //    .Include(c => c.Context.Extensions)
                //    .Include(c => c.Context.StatementReference)
                //    .Include(c => c.Context.Instructor)
                //    .Include(a => a.Authority)
                //    .Include(a => a.Authority.Actor)
                //    //.Include(v => v.Version)
                //    .Include(a => a.Attachments)
                //    .Where(i => i.VerbUUID == input.UUID)
                //    .ToListAsync();
                IEnumerable<LocalStatement> outputSorted = output.OrderBy(t => t.Timestamp);
                output = outputSorted.ToList();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        #endregion

        #region Get by Actor
        public async Task<List<LocalStatement>> Get(Actor input)
        {
            List<LocalStatement> output = new List<LocalStatement>();
            try
            {
                output = await _context.LocalStatement
                  .AsNoTracking()
                  .Include(a => a.Actor).ThenInclude(i => i.Account)
                  .Include(a => a.Actor).ThenInclude(i => i.Member)
                  .Include(r => r.Result).ThenInclude(i => i.Score)
                  .Include(r => r.Result).ThenInclude(i => i.Extensions)
                  .Include(c => c.Context).ThenInclude(c => c.ContextActivities)
                  .Include(c => c.Context).ThenInclude(c => c.StatementReference)
                  .Include(c => c.Context).ThenInclude(c => c.Instructor)
                  .Include(c => c.Context).ThenInclude(c => c.Extensions)
                  .Include(a => a.Authority).ThenInclude(a => a.Actor)
                  .Include(a => a.Attachments)
                  .Where(i => i.Actor.UUID == input.UUID)
                  .ToListAsync();
                IEnumerable<LocalStatement> outputSorted = output.OrderBy(t => t.Timestamp);
                output = outputSorted.ToList();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        #endregion

        #endregion

        #region Create
        public async Task<LocalStatement> Create(LocalStatement input)
        {
            try
            {
                //await _xApiDbContext.LocalStatement.AddAsync(input);
                _context.LocalStatement.Update(input);
                await _context.SaveChangesAsync();
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
        public async Task<LocalStatement> Update(LocalStatement input)
        {
            try
            {
                _context.LocalStatement.Update(input);
                await _context.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementQueries.Update: suppressed exception");
            }

            return input;
        }
        #endregion

        #region Delete
        public async Task<LocalStatement> Delete(LocalStatement input)
        {
            try
            {
                _context.LocalStatement.Remove(input);
                await _context.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementQueries.Delete: suppressed exception");
            }

            return input;
        }


        #endregion
    }

}
