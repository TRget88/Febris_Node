// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Febris.UserNode.Portal;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// The portal session store swaps by configuration. With no Redis/Valkey the
    /// node stores the login ticket in the encrypted cookie (zero external services, works over
    /// plain-HTTP localhost); with Redis configured it uses the server-side ticket store + HTTPS-strict
    /// cookie (multi-instance/HA). These pin the decision function AND the resulting DI wiring.
    /// </summary>
    public class NodeSessionPolicyTests
    {
        private const string Dummy = "Host=localhost;Database=x;Username=x;Password=x";

        // --- the pure decision ---

        [Fact]
        public void UsesRedisSessionStore_NullConfig_IsFalse()
            => NodeSessionPolicy.UsesRedisSessionStore(null).Should().BeFalse();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("{RedisAuthConnection}")]   // unsubstituted deploy placeholder -> treated as absent
        public void UsesRedisSessionStore_AbsentOrPlaceholder_IsFalse(string authConnection)
        {
            IConfiguration config = Config(authConnection);
            NodeSessionPolicy.UsesRedisSessionStore(config).Should().BeFalse();
        }

        [Theory]
        [InlineData("localhost:6379")]
        [InlineData("valkey:6379,password=x")]
        public void UsesRedisSessionStore_RealConnection_IsTrue(string authConnection)
        {
            IConfiguration config = Config(authConnection);
            NodeSessionPolicy.UsesRedisSessionStore(config).Should().BeTrue();
        }

        // --- the resulting DI wiring (Portal Startup) ---

        [Fact]
        public void Portal_WithNoRedis_RegistersNoTicketStore_AndCookieIsHttpFriendly()
        {
            var services = ConfigurePortal(redisAuthConnection: null);

            // No server-side ticket store when Redis is absent.
            services.Any(d => d.ServiceType == typeof(Microsoft.AspNetCore.Authentication.Cookies.ITicketStore))
                .Should().BeFalse("with no Redis the ticket lives in the cookie, not a server store");

            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            CookieAuthenticationOptions cookie = provider
                .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(IdentityConstants.ApplicationScheme);

            cookie.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.SameAsRequest, "login must work over plain-HTTP localhost");
            cookie.Cookie.SameSite.Should().Be(SameSiteMode.Lax, "a non-Secure cookie cannot be SameSite=None");
            cookie.SessionStore.Should().BeNull("the ticket is stored in the encrypted cookie, no server store");
        }

        [Fact]
        public void Portal_WithRedis_RegistersRedisTicketStore()
        {
            var services = ConfigurePortal(redisAuthConnection: "localhost:6379");

            services.Any(d =>
                d.ServiceType == typeof(Microsoft.AspNetCore.Authentication.Cookies.ITicketStore)
                && d.ImplementationType == typeof(Febris.SharedServices.RedisCacheTicketStore))
                .Should().BeTrue("Redis configured -> the server-side ticket store is wired for HA");
        }

        // --- helpers ---

        private static IConfiguration Config(string authConnection)
        {
            var settings = new Dictionary<string, string>();
            if (authConnection != null)
            {
                settings["RedisConnectionStrings:AuthConnection"] = authConnection;
            }
            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }

        private static IServiceCollection ConfigurePortal(string redisAuthConnection)
        {
            var settings = new Dictionary<string, string>
            {
                ["ConnectionStrings:DataDBConnection"] = Dummy,
                ["ConnectionStrings:XAPIDBConnection"] = Dummy,
                ["ConnectionStrings:AnalyticsDBConnection"] = Dummy,
                ["ConnectionStrings:UserDBConnection"] = Dummy,
                ["AppKeys:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "febris-session-keys-" + Guid.NewGuid().ToString("N")),
                ["Storage:Provider"] = "FileSystem",
                ["Storage:BasePath"] = Path.Combine(Path.GetTempPath(), "febris-session-store-" + Guid.NewGuid().ToString("N")),
                ["JwtSettings:Secret"] = "node-session-policy-test-signing-secret-0123456789abcdef0123456789abcdef",
                ["JwtSettings:Issuer"] = "https://node.local",
                ["JwtSettings:Audience"] = "https://node.local",
            };
            if (redisAuthConnection != null)
            {
                settings["RedisConnectionStrings:AuthConnection"] = redisAuthConnection;
            }
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            var startup = new Febris.UserNode.Portal.Startup(config);
            var services = new ServiceCollection();
            services.AddSingleton(config);
            services.AddLogging();
            startup.ConfigureServices(services);
            return services;
        }
    }
}
