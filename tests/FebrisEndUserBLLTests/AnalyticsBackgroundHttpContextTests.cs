// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.AnalyticsModels;
using Febris.UserNode.DataAccessLayer.Queries.AnalyticsQueries;
using Febris.UserNode.LogicLayer.Logic.AnalyticsLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// The analytics logic must never touch HttpContext off the request thread.
    ///
    /// <para>
    /// WHY. <c>LocalAnalyticsMiddleware</c> fire-and-forgets its write with <c>Task.Run</c> and
    /// resolves <c>ILocalAnalyticsLogic</c> from a fresh DI scope INSIDE that task. AsyncLocal flows
    /// into <c>Task.Run</c>, so <c>IHttpContextAccessor</c> there still points at the request that is
    /// at that moment still travelling through <c>await _next(context)</c>. The constructor used to
    /// do <c>User = _httpContextAccessor?.HttpContext?.User</c>, which read a LIVE HttpContext from a
    /// second thread.
    /// </para>
    ///
    /// <para>
    /// That is a data race, not just untidiness. <c>HttpContext.User</c> and
    /// <c>HttpContext.RequestAborted</c> both resolve through <c>FeatureReferences.Fetch</c>, which
    /// WRITES to a shared struct and zeroes the entire feature cache when it flushes. One thread can
    /// therefore null the other thread's <c>ref</c> slot between the assignment and the return, at
    /// which point Fetch hands back null and the caller dereferences it.
    /// </para>
    ///
    /// <para>
    /// Observed once in the wild: a login POST returned 500 instead of 429 with
    /// <c>NullReferenceException at DefaultHttpContext.get_RequestAborted()</c> inside the rate
    /// limiter. A NullReferenceException is the tell -- a context that has merely been recycled
    /// throws ObjectDisposedException, and a context with the lifetime feature absent synthesizes a
    /// new one rather than throwing. Concurrent mutation is the only path that yields NRE.
    /// </para>
    ///
    /// <para>
    /// These tests pin the CONSTRUCTOR and the background write path at zero HttpContext reads,
    /// while proving the request-thread callers still resolve the principal.
    /// </para>
    /// </summary>
    public class AnalyticsBackgroundHttpContextTests
    {
        /// <summary>
        /// Counts every read of <see cref="HttpContext"/>. The count is the whole point: the
        /// background path must be ZERO, because any non-zero value is a second thread touching a
        /// live request's context.
        /// </summary>
        private sealed class CountingHttpContextAccessor : IHttpContextAccessor
        {
            private HttpContext _context;

            public CountingHttpContextAccessor(HttpContext context)
            {
                _context = context;
            }

            public int Reads { get; private set; }

            public HttpContext HttpContext
            {
                get
                {
                    Reads++;
                    return _context;
                }
                set { _context = value; }
            }
        }

        private static HttpContext ContextWithUser()
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "probe") }, "test"));
            return context;
        }

        private static IConfiguration EmptyConfig()
        {
            return new ConfigurationBuilder().Build();
        }

        [Fact]
        public void LocalAnalyticsLogic_Constructor_ReadsHttpContextZeroTimes()
        {
            CountingHttpContextAccessor accessor = new CountingHttpContextAccessor(ContextWithUser());

            _ = new LocalAnalyticsLogic(accessor, EmptyConfig(), Mock.Of<ILocalAnalyticsQueries>());

            accessor.Reads.Should().Be(0,
                "the constructor runs on a background thread inside the middleware's Task.Run, while the request is still in the pipeline");
        }

        [Fact]
        public async Task LocalAnalyticsLogic_Create_ReadsHttpContextZeroTimes()
        {
            CountingHttpContextAccessor accessor = new CountingHttpContextAccessor(ContextWithUser());
            Mock<ILocalAnalyticsQueries> queries = new Mock<ILocalAnalyticsQueries>();
            queries.Setup(q => q.Create(It.IsAny<LocalAnalytics>())).ReturnsAsync(true);

            LocalAnalyticsLogic logic = new LocalAnalyticsLogic(accessor, EmptyConfig(), queries.Object);
            await logic.Create(new LocalAnalytics { IPAddress = "203.0.113.9", Path = "/probe" });

            accessor.Reads.Should().Be(0, "Create is the fire-and-forget background write and must not touch the request's context");
        }

        [Fact]
        public void UserAnalyticsLogic_Constructor_ReadsHttpContextZeroTimes()
        {
            // Dead today -- UseUserAnalytics has no caller -- but it carries the identical defect
            // and would become live the moment anyone wires the middleware.
            CountingHttpContextAccessor accessor = new CountingHttpContextAccessor(ContextWithUser());

            _ = new UserAnalyticsLogic(accessor, EmptyConfig(), Mock.Of<IUserAnalyticsQueries>());

            accessor.Reads.Should().Be(0);
        }

        [Fact]
        public void User_StillResolves_WhenReadOnTheRequestThread()
        {
            // The other half of the contract. Making User lazy fixes the background race, but the
            // traffic chart reads User.IsFebrisUser() on the REQUEST thread and must still see the
            // principal. Reflection because User is private -- the point is the behaviour, and there
            // is no public surface that exposes it without a database.
            HttpContext context = ContextWithUser();
            CountingHttpContextAccessor accessor = new CountingHttpContextAccessor(context);
            LocalAnalyticsLogic logic = new LocalAnalyticsLogic(accessor, EmptyConfig(), Mock.Of<ILocalAnalyticsQueries>());

            PropertyInfo user = typeof(LocalAnalyticsLogic)
                .GetProperty("User", BindingFlags.NonPublic | BindingFlags.Instance);

            user.Should().NotBeNull("User must be a lazily evaluated property, not a field captured in the constructor");

            ClaimsPrincipal resolved = (ClaimsPrincipal)user.GetValue(logic);

            resolved.Should().BeSameAs(context.User);
            accessor.Reads.Should().Be(1, "the read happens on demand, at the moment the caller asks");
        }
    }
}
