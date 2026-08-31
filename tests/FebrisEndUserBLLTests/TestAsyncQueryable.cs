// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Query;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// An in-memory <see cref="IQueryable{T}"/> that EF Core's async operators will accept.
    ///
    /// <para>
    /// Needed because logic under test calls <c>ToListAsync()</c> on <c>UserManager.Users</c>. A
    /// plain <c>List&lt;T&gt;.AsQueryable()</c> throws "The source 'IQueryable' doesn't implement
    /// 'IAsyncEnumerable&lt;T&gt;'", so any test that stubs <c>Users</c> with a plain list fails on
    /// the first async query rather than on the behaviour it meant to check.
    /// </para>
    ///
    /// <para>
    /// This is the standard EF Core test double for that problem. It lives in its own file because
    /// it is not specific to any one suite: anything mocking a <c>DbSet</c> or <c>UserManager.Users</c>
    /// will need it.
    /// </para>
    /// </summary>
    internal static class TestAsyncQueryable
    {
        public static IQueryable<T> From<T>(IEnumerable<T> source) => new TestAsyncEnumerable<T>(source);
    }

    internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(Expression expression) : base(expression) { }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
        }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

        public T Current => _inner.Current;

        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(_inner.MoveNext());

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return new ValueTask();
        }
    }

    internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        internal TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

        public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
            new TestAsyncEnumerable<TElement>(expression);

        public object Execute(Expression expression) => _inner.Execute(expression);

        public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

        /// <summary>
        /// EF wraps the result in a Task of the element type, so the Task has to be built
        /// reflectively from the closed generic rather than cast.
        /// </summary>
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            Type resultType = typeof(TResult).GetGenericArguments()[0];
            object executed = typeof(IQueryProvider)
                .GetMethods()
                .First(m => m.Name == nameof(IQueryProvider.Execute) && m.IsGenericMethod)
                .MakeGenericMethod(resultType)
                .Invoke(_inner, new object[] { expression });

            return (TResult)typeof(Task)
                .GetMethod(nameof(Task.FromResult))
                .MakeGenericMethod(resultType)
                .Invoke(null, new[] { executed });
        }
    }
}
