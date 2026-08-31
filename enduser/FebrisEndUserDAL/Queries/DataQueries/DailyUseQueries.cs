// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    public class DailyUseQueries
    {
        public Task<List<DailyUse>> Get()
        {
            throw new NotImplementedException();
        }

        public Task<DailyUse> Get(Guid input)
        {
            throw new NotImplementedException();
        }

        public Task<DailyUse> Get(long input)
        {
            throw new NotImplementedException();
        }

        public Task<DailyUse> Create(DailyUse input)
        {
            throw new NotImplementedException();
        }

        public Task<DailyUse> Update(DailyUse input)
        {
            throw new NotImplementedException();
        }

        public Task<bool> Delete(DailyUse input)
        {
            throw new NotImplementedException();
        }

        public Task<List<DailyUse>> Get(DateTime startDate, DateTime endDate)
        {
            throw new NotImplementedException();
        }

        public Task<List<DailyUse>> Get(DateTime input)
        {
            throw new NotImplementedException();
        }
    }
}
