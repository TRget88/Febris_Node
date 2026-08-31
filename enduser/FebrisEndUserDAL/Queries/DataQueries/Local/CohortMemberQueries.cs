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
    public interface ICohortMemberQueries
    {
        Task<bool> Delete(Guid cohortMemberId);
        Task<CohortMember> Create(CohortMember preoutput);
        Task<List<CohortMember>> Get();
        Task<CohortMember> Get(Guid? input);
        Task<CohortMember> Get(long? input);
        Task<List<CohortMember>> Get(Cohort input);
        Task<List<CohortMember>> Get(List<Cohort> input);
        Task<List<CohortMember>> GetCohortsByMember(Guid? input);
        Task<List<CohortMember>> GetByCohort(long? input);
        Task<bool> Delete(CohortMember input);
        Task<List<CohortMember>> Create(List<CohortMember> memberList);

        /// <summary>
        /// Add one user to one cohort BY UUID, resolving the cohort inside this context so the
        /// navigation is set from a TRACKED instance. Returns null when no such cohort exists.
        ///
        /// <para>
        /// This exists because the obvious call site -- read the cohort through
        /// <c>ICohortQueries</c>, then assign it as the navigation -- hands EF an
        /// <c>AsNoTracking</c> copy. If the same context already tracks that cohort, attaching the
        /// duplicate throws "another instance with the same key value is already being tracked",
        /// and the caller sees a linkage that silently did not happen. A test caught exactly that.
        /// Resolving inside the DAL keeps EF identity concerns in the layer that owns them.
        /// </para>
        ///
        /// <para>
        /// ARCHIVED COHORTS ARE INCLUDED, deliberately: this resolves a selection somebody already
        /// made, and silently dropping an explicitly-chosen cohort is the silent-success failure
        /// this codebase keeps removing. Same reasoning as the bulk-import path.
        /// </para>
        /// </summary>
        Task<CohortMember> CreateForCohort(Guid userId, Guid cohortUuid);
    }

    public class CohortMemberQueries : ICohortMemberQueries
    {
        private readonly DataDbContext _dataDbContext;

        public CohortMemberQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }
        public CohortMemberQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        public async Task<List<CohortMember>> Get()
        {
            List<CohortMember> output = new List<CohortMember>();
            try
            {
                output = await _dataDbContext.CohortMember
                    .AsNoTracking()
                    .Include(i => i.Cohort)
                    .OrderByDescending(i => i.TimeStamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        public async Task<CohortMember> Get(Guid? input)
        {
            CohortMember output = new CohortMember();
            try
            {
                output = await _dataDbContext.CohortMember
                    .AsNoTracking()
                    .Include(i => i.Cohort)
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

        public async Task<CohortMember> Get(long? input)
        {
            CohortMember output = new CohortMember();
            try
            {
                output = await _dataDbContext.CohortMember
                    .AsNoTracking()
                    .Include(i => i.Cohort)
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

        public async Task<List<CohortMember>> Get(Cohort input)
        {
            List<CohortMember> output = new List<CohortMember>();
            try
            {
                output = await _dataDbContext.CohortMember
                    .AsNoTracking()
                    .Include(i => i.Cohort)
                    .Where(i => i.Cohort.Id == input.Id).OrderByDescending(i => i.TimeStamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        public async Task<List<CohortMember>> Get(List<Cohort> input)
        {
            List<CohortMember> output = new List<CohortMember>();
            try
            {
                if (input == null || input.Count == 0) return output;

                // Two-bug fix here:
                //
                // (1) N+1: previously one query per cohort in the input list.
                //     Replaced with a single batched query using
                //     `input.Any(j => j.Id == i.Cohort.Id)` which translates
                //     to a SQL IN (...) clause.
                //
                // (2) "Output reassigned instead of accumulated" bug -- the
                //     previous body did `output = await ...` inside the
                //     foreach, so each iteration OVERWROTE output instead of
                //     appending. `temp` was created but never populated and
                //     the `output.AddRange(temp)` was a no-op. Net effect:
                //     the function returned ONLY the LAST cohort's members,
                //     never the union. Same family of bug as the recently-
                //     fixed FebrisPeerListListener.OnPeersAvailable.
                //
                // The commented-out lines below this block were the original
                // intent; restoring them and adding the timestamp ordering.
                List<long> cohortIds = input.Select(c => c.Id).ToList();
                output = await _dataDbContext.CohortMember
                    .AsNoTracking()
                    .Include(i => i.Cohort)
                    .Where(i => cohortIds.Contains(i.Cohort.Id))
                    .OrderByDescending(i => i.TimeStamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        public async Task<List<CohortMember>> GetCohortsByMember(Guid? input)
        {
            List<CohortMember> output = new List<CohortMember>();
            try
            {
                output = await _dataDbContext.CohortMember
                    .AsNoTracking()
                    .Include(i => i.Cohort)
                   .Where(i => i.UserId == input)
                   .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        public async Task<List<CohortMember>> GetByCohort(long? input)
        {
            List<CohortMember> output = new List<CohortMember>();
            try
            {
                output = await _dataDbContext.CohortMember
                    .AsNoTracking()
                    .Include(i => i.Cohort)
                    .Where(i => i.Cohort.Id == input)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        public async Task<bool> Delete(Guid cohortMemberId)
        {
            bool output = false;
            try
            {
                CohortMember cohortMember = await Get(cohortMemberId);
                _dataDbContext.CohortMember.Remove(cohortMember);
                await _dataDbContext.SaveChangesAsync();
                output = true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }
        public async Task<bool> Delete(CohortMember input)
        {
            bool output = false;
            try
            {                
                _dataDbContext.CohortMember.Remove(input);
                await _dataDbContext.SaveChangesAsync();
                output = true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        /// <inheritdoc />
        public async Task<CohortMember> CreateForCohort(Guid userId, Guid cohortUuid)
        {
            try
            {
                if (userId == Guid.Empty || cohortUuid == Guid.Empty)
                {
                    return null;
                }

                // TRACKED read (no AsNoTracking): the instance assigned to the navigation below must
                // be the one this context already knows about, or EF refuses the duplicate key.
                Cohort cohort = await _dataDbContext.Cohort
                    .FirstOrDefaultAsync(c => c.UUID == cohortUuid);
                if (cohort == null)
                {
                    return null;
                }

                CohortMember member = new CohortMember()
                {
                    UUID = Guid.NewGuid(),
                    UserId = userId,
                    Cohort = cohort,
                    CohortUUID = cohort.UUID
                };
                _dataDbContext.CohortMember.Add(member);
                await _dataDbContext.SaveChangesAsync();
                return member;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<CohortMember> Create(CohortMember input)
        {
            try
            {
                _dataDbContext.CohortMember.Update(input);
                await _dataDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return input;
        }
        public async Task<List<CohortMember>> Create(List<CohortMember> memberList)
        {
            try
            {
                foreach (var i in memberList)
                {
                    await Create(i);
                }
                return memberList;

            }
            catch (Exception ex)
            {

                throw;
            }

        }

    }


}
