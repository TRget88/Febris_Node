// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.TicketModels;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.AuthorizationLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Audit T9: the device refresh token was rotated ONLY inside the last 24 hours of an EIGHT DAY
    /// life. The guard read
    /// <c>if (refreshToken.Expires &lt;= DateTime.UtcNow.AddDays(1))</c>, so every refresh before the
    /// final day handed back the SAME token. A stolen refresh token was usable for about a week and
    /// nothing noticed.
    ///
    /// <para>
    /// Owner ruling 2026-08-10: eight hours, rotated on EVERY refresh. Safe to rotate every time
    /// precisely because the token is short and a device can always re-authenticate from scratch
    /// with its <c>PhysicalLicense</c> -- the worst case for a lost response is one extra auth
    /// round-trip, not a stranded headset.
    /// </para>
    ///
    /// <para>
    /// These tests drive the REAL code path: authenticate a device, then refresh, through
    /// <c>HardwareKeyAuthorization</c> with an in-memory cache.
    /// </para>
    ///
    /// <para>
    /// WHAT THEY ACTUALLY PIN, measured rather than assumed. Reinstating the old 24-hour rotation
    /// guard ALONE leaves them green, because an eight-HOUR token always satisfies
    /// <c>Expires &lt;= UtcNow.AddDays(1)</c> -- so with the short lifetime in place that guard would
    /// rotate every time anyway. Reinstating BOTH the old guard and the old eight-DAY lifetime turns
    /// four of the five red, with the exact production symptom: <c>RefreshToken</c> comes back as
    /// the empty string and the original token stays usable.
    /// </para>
    ///
    /// <para>
    /// So the LIFETIME is the load-bearing fix and unconditional rotation is belt-and-braces that
    /// keeps the cadence correct if the lifetime is ever configured longer again. Worth knowing
    /// before anyone "simplifies" either half on the grounds that the tests still pass.
    /// </para>
    /// </summary>
    public class DeviceRefreshRotationTests
    {
        /// <summary>Minimal in-memory IDistributedCache. Only the async paths are exercised.</summary>
        private sealed class FakeCache : IDistributedHardwareCache
        {
            public readonly Dictionary<string, byte[]> Store = new Dictionary<string, byte[]>();

            public byte[] Get(string key) => Store.TryGetValue(key, out byte[] v) ? v : null;

            public Task<byte[]> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

            public void Refresh(string key) { }

            public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

            public void Remove(string key) => Store.Remove(key);

            public Task RemoveAsync(string key, CancellationToken token = default)
            {
                Store.Remove(key);
                return Task.CompletedTask;
            }

            public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Store[key] = value;

            public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            {
                Store[key] = value;
                return Task.CompletedTask;
            }
        }

        private const string DeviceLicense = "ROTATION-PROBE-LICENSE";

        private static IConfiguration Config()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                // Long enough for HMAC-SHA256.
                { "JwtSettings:Secret", "rotation-probe-secret-key-that-is-long-enough-for-hmac-sha256" },
                { "JwtSettings:RefreshTokenHours", "8" }
            }).Build();
        }

        private static HardwareKeyAuthorization Build(FakeCache cache, LocalHardware device)
        {
            DefaultHttpContext http = new DefaultHttpContext();
            http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.10");

            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(http);

            Mock<IHardwareQueries> queries = new Mock<IHardwareQueries>();
            queries.Setup(q => q.GetByKey(It.IsAny<string>())).ReturnsAsync(device);
            queries.Setup(q => q.Get(It.IsAny<long>())).ReturnsAsync(device);

            Mock<IJwtSigningKeyProvider> keys = new Mock<IJwtSigningKeyProvider>();
            keys.SetupGet(k => k.HasAsymmetricKey).Returns(false);

            return new HardwareKeyAuthorization(accessor.Object, cache, Config(), keys.Object, queries.Object);
        }

        private static LocalHardware Device()
        {
            return new LocalHardware
            {
                Id = 42,
                UUID = Guid.NewGuid(),
                DescriptiveName = "Rotation probe headset",
                PhysicalLicense = DeviceLicense,
                IsLockedOut = false
            };
        }

        private static async Task<(HardwareKeyAuthorization Auth, FakeCache Cache, string RefreshToken)> Authenticate()
        {
            FakeCache cache = new FakeCache();
            HardwareKeyAuthorization auth = Build(cache, Device());

            HardwareAuthenticationResponse response = await auth.HardwareAuthentication(
                new HardwareAuthenticationRequest { LicenseKey = DeviceLicense });

            response.Should().NotBeNull("the device must authenticate before rotation can be exercised");
            return (auth, cache, response.RefreshToken);
        }

        [Fact]
        public async Task EveryRefreshIssuesANewToken()
        {
            // THE regression test. The issued token expires in 8 hours, far outside the old 24-hour
            // rotation window, so the pre-fix code returned an EMPTY refresh token here and left the
            // original one active and reusable.
            (HardwareKeyAuthorization auth, _, string first) = await Authenticate();

            HardwareAuthenticationResponse refreshed = await auth.RefreshHardwareToken(first);

            refreshed.Should().NotBeNull();
            refreshed.RefreshToken.Should().NotBeNullOrEmpty("a refresh must hand back a NEW token every time");
            refreshed.RefreshToken.Should().NotBe(first, "the token must actually rotate, not be handed back unchanged");
        }

        [Fact]
        public async Task TheRotatedOutTokenIsRevokedAndCannotBeUsedAgain()
        {
            // Rotation is only worth anything if the old token dies. This is what makes a stolen
            // token stop working at the device's next refresh rather than at its expiry.
            (HardwareKeyAuthorization auth, _, string first) = await Authenticate();

            HardwareAuthenticationResponse refreshed = await auth.RefreshHardwareToken(first);
            refreshed.RefreshToken.Should().NotBe(first);

            HardwareAuthenticationResponse replay = await auth.RefreshHardwareToken(first);

            replay.Should().BeNull("the rotated-out token is revoked, so presenting it again must be refused");
        }

        [Fact]
        public async Task RotationChainsAcrossSuccessiveRefreshes()
        {
            (HardwareKeyAuthorization auth, _, string first) = await Authenticate();

            HardwareAuthenticationResponse second = await auth.RefreshHardwareToken(first);
            HardwareAuthenticationResponse third = await auth.RefreshHardwareToken(second.RefreshToken);

            third.Should().NotBeNull("the newly issued token must itself be usable");
            third.RefreshToken.Should().NotBe(second.RefreshToken);
            third.RefreshToken.Should().NotBe(first);
        }

        [Fact]
        public async Task TheIssuedRefreshTokenExpiresInHoursNotDays()
        {
            // Pins the ruling at the point the token is MINTED, not just in the settings helper.
            FakeCache cache = new FakeCache();
            HardwareKeyAuthorization auth = Build(cache, Device());

            HardwareAuthenticationResponse response = await auth.HardwareAuthentication(
                new HardwareAuthenticationRequest { LicenseKey = DeviceLicense });

            RefreshHardwareToken stored = await cache.GetRecord<RefreshHardwareToken>(
                "FebrisHardwareToken-" + response.RefreshToken);

            stored.Should().NotBeNull("the refresh token record must be cached under its own key");
            stored.Expires.Should().BeCloseTo(DateTime.UtcNow.AddHours(8), TimeSpan.FromMinutes(5));
            stored.Expires.Should().BeBefore(DateTime.UtcNow.AddDays(1), "eight DAYS was the defect");
        }

        [Fact]
        public void TheAccessTokenDoesNotCarryTheDeviceCredential()
        {
            // PhysicalLicense is the device AUTHENTICATION CREDENTIAL and a JWT is base64, not
            // encrypted. Carrying it in the Hardware claim put the credential in every access token,
            // in anything that logged a token, and on the wire -- including, until the H-56 fix, a
            // plain-HTTP LAN endpoint.
            //
            // Nothing ever read it back: ExtractHardwareData uses only Id before re-reading the live
            // row, and the claim's other consumer hands it to the API controllers, where
            // PhysicalLicense appears nowhere.
            Febris.ModelLibrary.Models.DataModels.Hardware claim =
                HardwareKeyAuthorization.ToHardwareClaim(Device());

            claim.Should().NotBeNull();
            claim.PhysicalLicense.Should().BeNullOrEmpty("the credential must never travel inside a token");

            // The claim must still carry what its consumers actually use.
            claim.Id.Should().Be(42);
            claim.UUID.Should().NotBeEmpty();
        }

        [Fact]
        public async Task AnUnknownTokenIsRefused()
        {
            // Sits directly alongside the replay path. Reuse detection now branches on
            // Revoked != null BEFORE the general IsActive check, so this pins that a token the cache
            // has never seen still takes the plain refusal and does not fall into the replay branch
            // (which would dereference a token that is null).
            FakeCache cache = new FakeCache();
            HardwareKeyAuthorization auth = Build(cache, Device());

            HardwareAuthenticationResponse refreshed = await auth.RefreshHardwareToken("a-token-this-node-never-issued");

            refreshed.Should().BeNull();
        }

        [Fact]
        public async Task ALockedOutDeviceCannotRefresh()
        {
            // Unchanged behaviour, pinned because rotation now runs unconditionally and must not
            // become a way around the lockout.
            FakeCache cache = new FakeCache();
            LocalHardware device = Device();
            HardwareKeyAuthorization auth = Build(cache, device);

            HardwareAuthenticationResponse response = await auth.HardwareAuthentication(
                new HardwareAuthenticationRequest { LicenseKey = DeviceLicense });

            device.IsLockedOut = true;

            HardwareAuthenticationResponse refreshed = await auth.RefreshHardwareToken(response.RefreshToken);

            refreshed.Should().BeNull("a locked-out device must not be able to refresh its way back in");
        }
    }
}
