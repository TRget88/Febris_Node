// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
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
    public interface ICohortLinkedCurriculumQueries
    {
        /// <summary>
        /// The cohort's curriculum entitlements, Curriculum navigation included. This is the
        /// node-local replacement for the old purchase-derived access list: a cohort's access
        /// is what curricula it is linked to, not what its members bought.
        /// </summary>
        Task<List<CohortLinkedCurriculum>> GetListByCohort(Cohort input);
    }

    public class CohortLinkedCurriculumQueries: ICohortLinkedCurriculumQueries
    {
        private readonly DataDbContext _dataDbContext;

        public CohortLinkedCurriculumQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }
        public CohortLinkedCurriculumQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }


        public async Task<List<CohortLinkedCurriculum>> GetListByCohort(Cohort input)
        {
            List<CohortLinkedCurriculum> output = new List<CohortLinkedCurriculum>();

            output = await _dataDbContext.CohortLinkedCurriculum
                .AsNoTracking()
                .Include(p => p.Curriculum)
                .Include(p => p.Cohort)
                .Where(i => i.Cohort.Id == input.Id)
                .OrderByDescending(i => i.TimeStamp)
                .ToListAsync();

            return output;
        }
    }

    
}
