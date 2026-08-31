// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.UserModels;
using Febris.UserNode.Portal.IdentityPolicy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// <see cref="TwoFactorEnrollmentGateMiddleware"/> (TwoFactor.Enforcement): Off passes through;
    /// AllRequired/AdminsRequired redirect an authenticated-but-unenrolled (applicable) user to the
    /// authenticator-setup page and block everything else, while always letting the enrollment/logout
    /// paths through so the gate cannot trap the user.
    /// </summary>
    public class TwoFactorEnrollmentGateTests
    {
        private const string Enrollment = "/Identity/Account/Manage/EnableAuthenticator";

        private static IOptions<IdentityPolicyOptions> Options(TwoFactorEnforcement enforcement) =>
            Microsoft.Extensions.Options.Options.Create(new IdentityPolicyOptions
            {
                TwoFactor = new TwoFactorOptions { Enforcement = enforcement }
            });

        private static ClaimsPrincipal Principal(bool authenticated, params string[] roles)
        {
            var claims = new List<Claim>();
            foreach (string r in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, r));
            }
            // A non-null authenticationType makes Identity.IsAuthenticated true.
            ClaimsIdentity identity = authenticated ? new ClaimsIdentity(claims, "TestAuth") : new ClaimsIdentity(claims);
            return new ClaimsPrincipal(identity);
        }

        private static UserManager<LocalApplicationUser> UserManagerReturning(bool twoFactorEnabled)
        {
            var user = new LocalApplicationUser();
            var store = new Mock<IUserStore<LocalApplicationUser>>();
            var mgr = new Mock<UserManager<LocalApplicationUser>>(
                store.Object, null, null, null, null, null, null, null, null);
            mgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
            mgr.Setup(m => m.GetTwoFactorEnabledAsync(It.IsAny<LocalApplicationUser>())).ReturnsAsync(twoFactorEnabled);
            return mgr.Object;
        }

        private static async Task<(bool nextCalled, DefaultHttpContext ctx)> Run(
            TwoFactorEnforcement enforcement, ClaimsPrincipal principal, bool enrolled, string path, bool ajax = false)
        {
            bool nextCalled = false;
            RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
            var middleware = new TwoFactorEnrollmentGateMiddleware(next, Options(enforcement), new MemoryCache(new MemoryCacheOptions()));
            var ctx = new DefaultHttpContext { User = principal };
            ctx.Request.Path = path;
            if (ajax)
            {
                ctx.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            }

            await middleware.Invoke(ctx, UserManagerReturning(enrolled));

            return (nextCalled, ctx);
        }

        [Fact]
        public async Task Off_PassesThrough()
        {
            var (next, ctx) = await Run(TwoFactorEnforcement.Off, Principal(true), enrolled: false, "/Home");

            next.Should().BeTrue();
            ctx.Response.StatusCode.Should().NotBe(302);
        }

        [Fact]
        public async Task Unauthenticated_PassesThrough()
        {
            var (next, _) = await Run(TwoFactorEnforcement.AllRequired, Principal(false), enrolled: false, "/Home");

            next.Should().BeTrue();
        }

        [Fact]
        public async Task AllRequired_Unenrolled_RedirectsToEnrollment()
        {
            var (next, ctx) = await Run(TwoFactorEnforcement.AllRequired, Principal(true), enrolled: false, "/Home");

            next.Should().BeFalse();
            ctx.Response.StatusCode.Should().Be(302);
            ctx.Response.Headers.Location.ToString().Should().Be(Enrollment);
        }

        [Fact]
        public async Task AllRequired_Enrolled_PassesThrough()
        {
            var (next, ctx) = await Run(TwoFactorEnforcement.AllRequired, Principal(true), enrolled: true, "/Home");

            next.Should().BeTrue();
            ctx.Response.StatusCode.Should().NotBe(302);
        }

        [Fact]
        public async Task AllRequired_OnEnrollmentPath_PassesThrough_NoLoop()
        {
            var (next, ctx) = await Run(TwoFactorEnforcement.AllRequired, Principal(true), enrolled: false, Enrollment);

            next.Should().BeTrue();
            ctx.Response.StatusCode.Should().NotBe(302);
        }

        [Fact]
        public async Task AllRequired_LogoutAllowed()
        {
            var (next, _) = await Run(TwoFactorEnforcement.AllRequired, Principal(true), enrolled: false, "/Identity/Account/Logout");

            next.Should().BeTrue();
        }

        [Fact]
        public async Task AdminsRequired_NonAdmin_PassesThrough()
        {
            var (next, _) = await Run(TwoFactorEnforcement.AdminsRequired, Principal(true, "User"), enrolled: false, "/Home");

            next.Should().BeTrue();
        }

        [Fact]
        public async Task AllRequired_Unenrolled_AjaxRequest_Returns403WithHintHeader_NotRedirect()
        {
            var (next, ctx) = await Run(TwoFactorEnforcement.AllRequired, Principal(true), enrolled: false, "/Widget/IndexPartial", ajax: true);

            next.Should().BeFalse();
            ctx.Response.StatusCode.Should().Be(403);
            ctx.Response.Headers["X-Mfa-Enrollment-Required"].ToString().Should().Be(Enrollment);
        }

        [Fact]
        public async Task EnrolledUser_SecondRequest_ServedFromCache_SkipsUserLookup()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var userId = Guid.NewGuid();
            var user = new LocalApplicationUser { Id = userId };
            var store = new Mock<IUserStore<LocalApplicationUser>>();
            var um = new Mock<UserManager<LocalApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
            um.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(userId.ToString());
            um.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
            um.Setup(m => m.GetTwoFactorEnabledAsync(It.IsAny<LocalApplicationUser>())).ReturnsAsync(true);
            ClaimsPrincipal principal = Principal(true);

            async Task<bool> Once()
            {
                bool nextCalled = false;
                RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
                var mw = new TwoFactorEnrollmentGateMiddleware(next, Options(TwoFactorEnforcement.AllRequired), cache);
                var ctx = new DefaultHttpContext { User = principal };
                ctx.Request.Path = "/Home";
                await mw.Invoke(ctx, um.Object);
                return nextCalled;
            }

            (await Once()).Should().BeTrue();   // live check, caches enrolled=true
            (await Once()).Should().BeTrue();   // served from cache

            um.Verify(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Once,
                "the second request must be served from the enrolled-cache without a user lookup");
        }

        [Fact]
        public async Task UnenrolledUser_NeverCached_RecheckedLiveEveryRequest_NoBypass()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var userId = Guid.NewGuid();
            var user = new LocalApplicationUser { Id = userId };
            var store = new Mock<IUserStore<LocalApplicationUser>>();
            var um = new Mock<UserManager<LocalApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
            um.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(userId.ToString());
            um.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
            um.Setup(m => m.GetTwoFactorEnabledAsync(It.IsAny<LocalApplicationUser>())).ReturnsAsync(false);
            ClaimsPrincipal principal = Principal(true);

            async Task<int> Once()
            {
                RequestDelegate next = _ => Task.CompletedTask;
                var mw = new TwoFactorEnrollmentGateMiddleware(next, Options(TwoFactorEnforcement.AllRequired), cache);
                var ctx = new DefaultHttpContext { User = principal };
                ctx.Request.Path = "/Home";
                await mw.Invoke(ctx, um.Object);
                return ctx.Response.StatusCode;
            }

            (await Once()).Should().Be(302);
            (await Once()).Should().Be(302);

            um.Verify(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Exactly(2),
                "an unenrolled user must never be cached -- re-checked live on every request (no bypass)");
        }

        [Fact]
        public async Task AdminsRequired_AdminUnenrolled_RedirectsToEnrollment()
        {
            var (next, ctx) = await Run(TwoFactorEnforcement.AdminsRequired, Principal(true, "Admin"), enrolled: false, "/Home");

            next.Should().BeFalse();
            ctx.Response.StatusCode.Should().Be(302);
            ctx.Response.Headers.Location.ToString().Should().Be(Enrollment);
        }
    }
}
