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
    // DAL for the parent/guardian to student links that back FERPA read-only
    // access. Mirrors the CohortMemberQueries shape (DataDbContext, DI plus a
    // parameterless ops-based ctor). UUID and TimeStamp are set by the BLL before
    // Link is called, the same convention the other Create methods follow.
    public interface IParentLinkedStudentQueries
    {
        Task<ParentLinkedStudent> Link(ParentLinkedStudent input);
        Task<bool> Unlink(Guid parentUserId, Guid studentActorId);
        Task<List<ParentLinkedStudent>> GetByParent(Guid parentUserId);
        Task<List<Guid>> GetStudentActorIdsForParent(Guid parentUserId);
        Task<List<ParentLinkedStudent>> GetParentsForStudent(Guid studentActorId);
        Task<bool> Exists(Guid parentUserId, Guid studentActorId);
    }

    public class ParentLinkedStudentQueries : IParentLinkedStudentQueries
    {
        private readonly DataDbContext _dataDbContext;

        public ParentLinkedStudentQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }
        public ParentLinkedStudentQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        public async Task<ParentLinkedStudent> Link(ParentLinkedStudent input)
        {
            try
            {
                _dataDbContext.ParentLinkedStudent.Update(input);
                await _dataDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return input;
        }

        public async Task<bool> Unlink(Guid parentUserId, Guid studentActorId)
        {
            bool output = false;
            try
            {
                List<ParentLinkedStudent> rows = await _dataDbContext.ParentLinkedStudent
                    .Where(i => i.ParentUserId == parentUserId && i.StudentActorId == studentActorId)
                    .ToListAsync();
                if (rows.Any())
                {
                    _dataDbContext.ParentLinkedStudent.RemoveRange(rows);
                    await _dataDbContext.SaveChangesAsync();
                    output = true;
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<ParentLinkedStudent>> GetByParent(Guid parentUserId)
        {
            List<ParentLinkedStudent> output = new List<ParentLinkedStudent>();
            try
            {
                output = await _dataDbContext.ParentLinkedStudent
                    .AsNoTracking()
                    .Where(i => i.ParentUserId == parentUserId)
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

        // The set used by the FERPA access-scoping helper: the actor ids a parent
        // is permitted to read. Distinct so a duplicate link cannot widen anything.
        public async Task<List<Guid>> GetStudentActorIdsForParent(Guid parentUserId)
        {
            List<Guid> output = new List<Guid>();
            try
            {
                output = await _dataDbContext.ParentLinkedStudent
                    .AsNoTracking()
                    .Where(i => i.ParentUserId == parentUserId)
                    .Select(i => i.StudentActorId)
                    .Distinct()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<ParentLinkedStudent>> GetParentsForStudent(Guid studentActorId)
        {
            List<ParentLinkedStudent> output = new List<ParentLinkedStudent>();
            try
            {
                output = await _dataDbContext.ParentLinkedStudent
                    .AsNoTracking()
                    .Where(i => i.StudentActorId == studentActorId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<bool> Exists(Guid parentUserId, Guid studentActorId)
        {
            bool output = false;
            try
            {
                output = await _dataDbContext.ParentLinkedStudent
                    .AnyAsync(i => i.ParentUserId == parentUserId && i.StudentActorId == studentActorId);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
    }
}
