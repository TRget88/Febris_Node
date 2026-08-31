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
    public interface ILocationLinkedCohortQueries
    {
    }

    public class LocationLinkedCohortQueries: ILocationLinkedCohortQueries
    {
        private readonly DataDbContext _dataDbContext;

        public LocationLinkedCohortQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }
        public LocationLinkedCohortQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        public async Task<List<CohortLinkedLocation>> GetListByCohort(Cohort input)
        {
            List<CohortLinkedLocation> output = new List<CohortLinkedLocation>();

            output = await _dataDbContext.CohortLinkedLocation
                .AsNoTracking()
                .Include(p => p.Location)
                .Include(p => p.Cohort)
                .Where(i => i.Cohort.Id == input.Id)
                .OrderByDescending(i => i.TimeStamp).ToListAsync();

            return output;            
        }
    }        
}
