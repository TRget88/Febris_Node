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
    public interface ITestUserQueries
    {
        Task<TestUser> Create(TestUser input);
        Task<bool> Delete(TestUser temp);
        Task<List<TestUser>> Get();
        Task<TestUser> Get(long? id);
        Task<TestUser> Update(TestUser input);
        Task<TestUser> Get(Guid? id);
    }

    public class TestUserQueries : ITestUserQueries
    {
        private readonly DataDbContext _dataDbContext;

        public TestUserQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }

        public TestUserQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<TestUser> Get(Guid input)
        {
            TestUser output = new TestUser();
            try
            {
                output = await _dataDbContext.TestUser.AsNoTracking().Where(i => i.UUID == input).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;

        }

        public async Task<TestUser> Get(Guid? input)
        {
            TestUser output = new TestUser();
            try
            {
                output = await _dataDbContext.TestUser.AsNoTracking().Where(i => i.UUID == input).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;

        }


        public async Task<List<TestUser>> Get()
        {
            List<TestUser> output = new List<TestUser>();
            try
            {
                output = await _dataDbContext.TestUser.AsNoTracking().OrderByDescending(i => i.TimeStamp).ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<TestUser> Get(long input)
        {
            TestUser output = new TestUser();
            try
            {
                output = await _dataDbContext.TestUser.FindAsync(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }
        public async Task<TestUser> Get(long? input)
        {
            TestUser output = new TestUser();
            try
            {
                output = await _dataDbContext.TestUser
                    .AsNoTracking()
                    //.Include(i => i.TestUserSettings)
                    .Where(i => i.Id == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }
        #endregion
        #region Create
        public async Task<TestUser> Create(TestUser input)
        {
            try
            {
                _dataDbContext.TestUser.Update(input);
                await _dataDbContext.SaveChangesAsync();
                //await _dataDbContext.TestUser.AddAsync(input);
                //_dataDbContext.SaveChanges();

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return input;
        }
        #endregion
        #region Update
        public async Task<TestUser> Update(TestUser input)
        {
            try
            {
                _dataDbContext.TestUser.Update(input);
                await _dataDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return input;
        }
        #endregion
        #region Delete
        public async Task<bool> Delete(TestUser input)
        {
            bool output = false;
            try
            {
                _dataDbContext.TestUser.Remove(input);
                await _dataDbContext.SaveChangesAsync();
                output = true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }




        #endregion

        //public async Task<bool> LockOut(long input)
        //{
        //    bool output = false;
        //    try
        //    {
        //        TestUser temp = await _dataDbContext.TestUser.FindAsync(input);

        //        await _dataDbContext.SaveChangesAsync();
        //        output = true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //    }
        //    return output;
        //}
    }

}
