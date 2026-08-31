// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.LogicLayer.Attributes;
using Febris.UserNode.LogicLayer.Logic.AuthorizationLogic;
using Febris.SharedServices;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the API's authorization posture after ROADMAP 16: exactly ONE scheme. The
    /// NodeAdmin token (auth severance slice 3) was deleted when the admin-only writes it
    /// existed to reach moved into the Portal behind cookie auth, so the middleware attaches
    /// only a signature-valid Hardware claim and the composed filter authorizes only that.
    /// This file replaced NodeAdminCredentialTests, which pinned the two-scheme composition
    /// end to end while the credential existed.
    /// <list type="bullet">
    /// <item>a hardware token flows through the REAL JwtHardwareMiddleware and authorizes;</item>
    /// <item>no token, and a token signed with the wrong key, are refused (401);</item>
    /// <item>a locked-out hardware identity is refused (401), the A-02 defense in depth;</item>
    /// <item>REGRESSION PIN: a token shaped like the deleted credential (a "NodeAdmin" claim
    /// signed with the node's own key) attaches nothing and is refused. If the second scheme
    /// grows back, this is the test that goes red.</item>
    /// </list>
    /// </summary>
    public class HardwareOnlyAuthorizationTests
    {
        private const string Secret = "node-admin-credential-test-secret-0123456789abcdef0123456789abcdef";

        private static IConfiguration BuildConfig(string secret = Secret)
        {
            return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["JwtSettings:Secret"] = secret,
            }).Build();
        }

        /// <summary>Non-Development provider with no RSA configured: deterministic legacy-HMAC
        /// posture, the same fallback the hardware issuer uses.</summary>
        private static JwtSigningKeyProvider BuildKeyProvider(string secret = Secret)
        {
            return new JwtSigningKeyProvider(BuildConfig(secret), isDevelopment: false);
        }

        /// <summary>Run the REAL middleware over a bearer token and return the resulting
        /// HttpContext (whose Items carry whatever the token proved).</summary>
        private static async Task<HttpContext> RunMiddleware(string bearerToken, JwtSigningKeyProvider validatorKeys)
        {
            var context = new DefaultHttpContext();
            if (bearerToken != null)
            {
                context.Request.Headers["Authorization"] = "Bearer " + bearerToken;
            }
            var middleware = new JwtHardwareMiddleware(_ => Task.CompletedTask, BuildConfig(), validatorKeys);
            await middleware.Invoke(context, new Mock<IHardwareKeyAuthorization>().Object, new Mock<IHardwareRevocationList>().Object);
            return context;
        }

        private static AuthorizationFilterContext FilterContextFor(HttpContext httpContext)
        {
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }

        /// <summary>A token signed with the node's key carrying one arbitrary JSON claim --
        /// used both for the hardware wire shape and for the deleted credential's shape.</summary>
        private static string MintToken(JwtSigningKeyProvider provider, string claimType, object claimValue)
        {
            var handler = new JwtSecurityTokenHandler();
            var descriptor = new SecurityTokenDescriptor
            {
                Claims = new Dictionary<string, object>
                {
                    // JSON-string claim value, matching the issuer's wire shape (net8
                    // IdentityModel refuses raw POCO claim values).
                    [claimType] = System.Text.Json.JsonSerializer.Serialize(claimValue)
                },
                Expires = DateTime.UtcNow.AddMinutes(15),
                SigningCredentials = new SigningCredentials(provider.GetSigningKey(), SecurityAlgorithms.HmacSha256Signature)
            };
            return handler.WriteToken(handler.CreateToken(descriptor));
        }

        private static string MintHardwareToken(JwtSigningKeyProvider provider, bool lockedOut = false)
        {
            return MintToken(provider, "Hardware", new Hardware() { Id = 7, UUID = Guid.NewGuid(), IsLockedOut = lockedOut });
        }

        [Fact]
        public async Task HardwareToken_FlowsThroughMiddleware_AndAuthorizes()
        {
            JwtSigningKeyProvider provider = BuildKeyProvider();
            HttpContext context = await RunMiddleware(MintHardwareToken(provider), provider);

            context.Items["Hardware"].Should().BeOfType<Hardware>();

            AuthorizationFilterContext filterContext = FilterContextFor(context);
            new AuthorizeAttribute().OnAuthorization(filterContext);
            filterContext.Result.Should().BeNull();
        }

        [Fact]
        public async Task NoToken_IsRefused()
        {
            HttpContext context = await RunMiddleware(null, BuildKeyProvider());

            AuthorizationFilterContext filterContext = FilterContextFor(context);
            new AuthorizeAttribute().OnAuthorization(filterContext);
            filterContext.Result.Should().BeOfType<JsonResult>()
                .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        [Fact]
        public async Task TokenSignedWithTheWrongKey_AttachesNothing_AndIsRefused()
        {
            // Minted against a DIFFERENT secret than the node validates with.
            var foreignProvider = BuildKeyProvider("some-other-node-entirely-9876543210fedcba9876543210fedcba9876");
            string token = MintHardwareToken(foreignProvider);

            HttpContext context = await RunMiddleware(token, BuildKeyProvider());

            context.Items["Hardware"].Should().BeNull("signature validation must gate the attach");

            AuthorizationFilterContext filterContext = FilterContextFor(context);
            new AuthorizeAttribute().OnAuthorization(filterContext);
            filterContext.Result.Should().BeOfType<JsonResult>()
                .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        [Fact]
        public async Task LockedOutHardware_IsRefused()
        {
            // A-02 Stage 1: the filter re-checks the lockout flag even though the middleware
            // attached the identity. Defense in depth against a token minted before the lock.
            JwtSigningKeyProvider provider = BuildKeyProvider();
            HttpContext context = await RunMiddleware(MintHardwareToken(provider, lockedOut: true), provider);

            context.Items["Hardware"].Should().BeOfType<Hardware>();

            AuthorizationFilterContext filterContext = FilterContextFor(context);
            new AuthorizeAttribute().OnAuthorization(filterContext);
            filterContext.Result.Should().BeOfType<JsonResult>()
                .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        [Fact]
        public async Task A_token_shaped_like_the_deleted_admin_credential_attaches_nothing_and_is_refused()
        {
            // The deleted NodeAdmin credential's exact wire shape: a JWT signed with the node's
            // OWN key whose only claim is "NodeAdmin" carrying a serialized admin payload. While
            // the credential existed this authenticated the admin-only API writes. After ROADMAP
            // 16 the middleware must attach NOTHING for it -- not a Hardware item, not any other
            // item -- and the filter must refuse it like any other tokenless request. If a
            // second scheme ever grows back on the API, this test goes red first.
            JwtSigningKeyProvider provider = BuildKeyProvider();
            string legacyAdminToken = MintToken(provider, "NodeAdmin",
                new { UserUUID = Guid.NewGuid(), UserName = "portal-admin", MintedAtUtc = DateTime.UtcNow });

            HttpContext context = await RunMiddleware(legacyAdminToken, provider);

            context.Items["Hardware"].Should().BeNull("an admin-shaped token must never masquerade as a device");
            context.Items.ContainsKey("NodeAdmin").Should().BeFalse("the deleted scheme must not attach anything");

            AuthorizationFilterContext filterContext = FilterContextFor(context);
            new AuthorizeAttribute().OnAuthorization(filterContext);
            filterContext.Result.Should().BeOfType<JsonResult>()
                .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
    }
}
