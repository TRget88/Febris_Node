// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.TicketModels;
using Febris.ModelLibrary.ViewModels;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.AuthorizationLogic;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Readiness blocker 3: REGENERATING A DEVICE CREDENTIAL REVOKED NOTHING.
    ///
    /// <para>
    /// Regenerating rewrites the stored hash, which stops a thief authenticating AGAIN. It did not
    /// stop one who had already authenticated. The refresh path re-read the device row but tested
    /// only <c>IsLockedOut</c>, never the credential, and refresh tokens rotate on every call with an
    /// eight-hour life. So the stolen chain renewed itself indefinitely while the honest device, the
    /// only party that has to re-enter anything, was the only one actually locked out. The method's
    /// own docstring claimed it "deliberately breaks the device". It broke the wrong one.
    /// </para>
    ///
    /// <para>
    /// This mattered more than a lost-credential edge case, because minting a credential is not
    /// restricted to physical possession: <c>HardwareController</c> exposes RegenerateCredential to
    /// Educators and org admins and hands the plaintext straight back. It is the documented incident
    /// response, and following it left the intruder connected.
    /// </para>
    ///
    /// <para>
    /// CLOSED IN TWO LAYERS, mirroring how A-02 Stage 2 handles locking. The DURABLE half is
    /// <c>CredentialRegeneratedAt</c> on the row: refresh refuses any token minted before it, it
    /// survives a cache outage, and it needs no TTL guess. The IMMEDIATE half is the revocation list,
    /// which stops the access token the thief holds right now instead of waiting for it to expire.
    /// Neither alone is sufficient, and there is a test below for each direction.
    /// </para>
    /// </summary>
    public class DeviceCredentialRevocationTests
    {
        private const string DeviceLicense = "REVOCATION-PROBE-LICENSE";

        /// <summary>
        /// Minimal in-memory cache. Same shape as the one in <c>DeviceRefreshRotationTests</c>, which
        /// is the established pattern in this project (see also <c>InMemoryHardwareCache</c> in the
        /// shared-services suite) rather than a shared fixture.
        /// </summary>
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

        private static IConfiguration Config()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "JwtSettings:Secret", "revocation-probe-secret-key-that-is-long-enough-for-hmac-sha256" },
                { "JwtSettings:RefreshTokenHours", "8" }
            }).Build();
        }

        /// <summary>
        /// The queries mock returns the SAME device instance every time, so mutating the instance
        /// between authenticate and refresh is a faithful simulation of an administrator regenerating
        /// the credential mid-session: the refresh path genuinely re-reads the row and sees the change.
        /// </summary>
        private static HardwareKeyAuthorization Build(FakeCache cache, LocalHardware device)
        {
            DefaultHttpContext http = new DefaultHttpContext();
            http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.11");

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
                Id = 77,
                UUID = Guid.NewGuid(),
                DescriptiveName = "Revocation probe headset",
                PhysicalLicense = DeviceLicense,
                IsLockedOut = false
            };
        }

        // ------------------------------------------------------------------
        // The durable half: the refresh path honours CredentialRegeneratedAt.
        // ------------------------------------------------------------------

        [Fact]
        public async Task ATokenMintedBeforeTheCredentialWasRegeneratedIsRefused()
        {
            // THE regression test. Before the fix this refresh SUCCEEDED, which is precisely how a
            // stolen device kept its session across the recovery action taken to stop it.
            LocalHardware device = Device();
            HardwareKeyAuthorization auth = Build(new FakeCache(), device);

            HardwareAuthenticationResponse session = await auth.HardwareAuthentication(
                new HardwareAuthenticationRequest { LicenseKey = DeviceLicense });
            session.Should().NotBeNull("the thief authenticated while the old credential was live");

            // The administrator regenerates. The row now says the old credential died just now.
            device.CredentialRegeneratedAt = DateTime.UtcNow.AddSeconds(1);

            HardwareAuthenticationResponse refreshed = await auth.RefreshHardwareToken(session.RefreshToken);

            refreshed.Should().BeNull(
                "a token minted under the replaced credential must not renew itself");
        }

        [Fact]
        public async Task ATokenMintedAfterTheRegenerationStillWorks()
        {
            // The honest device re-authenticates with its new credential and must be unaffected.
            // Without this, the fix would simply brick every device it was meant to rescue.
            LocalHardware device = Device();
            device.CredentialRegeneratedAt = DateTime.UtcNow.AddSeconds(-30);

            HardwareKeyAuthorization auth = Build(new FakeCache(), device);

            HardwareAuthenticationResponse session = await auth.HardwareAuthentication(
                new HardwareAuthenticationRequest { LicenseKey = DeviceLicense });
            session.Should().NotBeNull();

            HardwareAuthenticationResponse refreshed = await auth.RefreshHardwareToken(session.RefreshToken);

            refreshed.Should().NotBeNull("this token was minted AFTER the regeneration, so it is the new session");
            refreshed.RefreshToken.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ADeviceThatNeverRegeneratedIsUnaffected()
        {
            // The null case is the overwhelming majority of devices. If this check had been written
            // to treat null as "regenerated at the epoch" it would refuse every refresh on the node.
            LocalHardware device = Device();
            device.CredentialRegeneratedAt = null;

            HardwareKeyAuthorization auth = Build(new FakeCache(), device);

            HardwareAuthenticationResponse session = await auth.HardwareAuthentication(
                new HardwareAuthenticationRequest { LicenseKey = DeviceLicense });

            HardwareAuthenticationResponse refreshed = await auth.RefreshHardwareToken(session.RefreshToken);

            refreshed.Should().NotBeNull("a device whose credential was never regenerated must refresh normally");
        }

        [Fact]
        public async Task TheRefusalSurvivesRepeatedAttempts()
        {
            // A thief retrying is the expected behaviour, not an edge case. The refusal must be a
            // property of the row rather than of anything consumed on first use.
            LocalHardware device = Device();
            HardwareKeyAuthorization auth = Build(new FakeCache(), device);

            HardwareAuthenticationResponse session = await auth.HardwareAuthentication(
                new HardwareAuthenticationRequest { LicenseKey = DeviceLicense });

            device.CredentialRegeneratedAt = DateTime.UtcNow.AddSeconds(1);

            (await auth.RefreshHardwareToken(session.RefreshToken)).Should().BeNull();
            (await auth.RefreshHardwareToken(session.RefreshToken)).Should().BeNull();
            (await auth.RefreshHardwareToken(session.RefreshToken)).Should().BeNull();
        }

        // ------------------------------------------------------------------
        // The write side: RegenerateCredential stamps and publishes.
        // ------------------------------------------------------------------

        private static DataDbContext BuildContext(string dbName)
        {
            return new DataDbContext(new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName).Options);
        }

        private static HardwareLogic BuildLogic(
            DataDbContext context, Mock<IHardwareRevocationList> revocations, params string[] roles)
        {
            ClaimsIdentity identity = new ClaimsIdentity(
                roles.Select(r => new Claim(ClaimTypes.Role, r)), "test");
            DefaultHttpContext http = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(http);

            return new HardwareLogic(accessor.Object, new HardwareQueries(context), revocations.Object, Config());
        }

        private static LocalHardwareCreationViewModel NewDevice()
        {
            return new LocalHardwareCreationViewModel
            {
                Hardware = new LocalHardware
                {
                    HardwareKind = HardwareKind.MobileServer,
                    DescriptiveName = "Bay 2 headset"
                }
            };
        }

        [Fact]
        public async Task RegeneratingStampsTheMomentTheOldCredentialDied()
        {
            using DataDbContext context = BuildContext(nameof(RegeneratingStampsTheMomentTheOldCredentialDied));
            Mock<IHardwareRevocationList> revocations = new Mock<IHardwareRevocationList>();
            HardwareLogic logic = BuildLogic(context, revocations, InstitutionUserAccountType.Admin.ToString());

            (LocalHardware created, _) = await logic.Create(NewDevice());
            context.Hardware.Single().CredentialRegeneratedAt.Should().BeNull(
                "a freshly registered device has never had a credential replaced");

            DateTime before = DateTime.UtcNow;
            await logic.RegenerateCredential(created.Id);

            DateTime? stamped = context.Hardware.Single().CredentialRegeneratedAt;
            stamped.Should().NotBeNull("without this the refresh check has nothing to compare against");
            stamped.Value.Should().BeOnOrAfter(before.AddSeconds(-1));
        }

        [Fact]
        public async Task TheStampIsUtcNotLocalTime()
        {
            // It is compared against RefreshHardwareToken.Created, which is UtcNow. Writing local
            // time would shift every comparison by the host's offset, which on this machine would
            // either strand honest devices or leave stolen ones running. A silent, timezone-shaped
            // bug that no functional test would catch on a UTC build agent.
            using DataDbContext context = BuildContext(nameof(TheStampIsUtcNotLocalTime));
            Mock<IHardwareRevocationList> revocations = new Mock<IHardwareRevocationList>();
            HardwareLogic logic = BuildLogic(context, revocations, InstitutionUserAccountType.Admin.ToString());

            (LocalHardware created, _) = await logic.Create(NewDevice());
            await logic.RegenerateCredential(created.Id);

            DateTime stamped = context.Hardware.Single().CredentialRegeneratedAt.Value;

            stamped.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1),
                "the stamp must be UTC, which is what the refresh comparison assumes");
        }

        [Fact]
        public async Task RegeneratingPublishesARevocationForTheDevice()
        {
            // The immediate half. CredentialRegeneratedAt cannot reach an access token that has
            // already been issued, because an access token never touches the refresh path. Without
            // this call the thief keeps working until the token expires on its own.
            using DataDbContext context = BuildContext(nameof(RegeneratingPublishesARevocationForTheDevice));
            Mock<IHardwareRevocationList> revocations = new Mock<IHardwareRevocationList>();
            HardwareLogic logic = BuildLogic(context, revocations, InstitutionUserAccountType.Admin.ToString());

            (LocalHardware created, _) = await logic.Create(NewDevice());
            Guid uuid = context.Hardware.Single().UUID;

            await logic.RegenerateCredential(created.Id);

            revocations.Verify(r => r.RevokeAsync(uuid, It.IsAny<TimeSpan>()), Times.Once,
                "the token the device already holds must be revoked, not merely superseded");
        }

        [Fact]
        public async Task TheRevocationWindowCoversTheConfiguredAccessTokenLifetime()
        {
            // Pins the window to the SETTING rather than a literal. An operator who lengthens the
            // access token would otherwise get a revocation that lapses before the token it revokes.
            using DataDbContext context = BuildContext(nameof(TheRevocationWindowCoversTheConfiguredAccessTokenLifetime));
            Mock<IHardwareRevocationList> revocations = new Mock<IHardwareRevocationList>();
            HardwareLogic logic = BuildLogic(context, revocations, InstitutionUserAccountType.Admin.ToString());

            (LocalHardware created, _) = await logic.Create(NewDevice());
            await logic.RegenerateCredential(created.Id);

            TimeSpan expected = JwtLifetimeSettings.AccessTokenLifetime(Config());
            revocations.Verify(r => r.RevokeAsync(It.IsAny<Guid>(), expected), Times.Once);
        }

        [Fact]
        public async Task ACallerWhoIsNotPermittedNeitherStampsNorRevokes()
        {
            // The authorization filter runs before any of this. If it did not, an unprivileged caller
            // could sign every device on the node out by hammering the endpoint.
            using DataDbContext context = BuildContext(nameof(ACallerWhoIsNotPermittedNeitherStampsNorRevokes));
            Mock<IHardwareRevocationList> revocations = new Mock<IHardwareRevocationList>();

            HardwareLogic admin = BuildLogic(context, revocations, InstitutionUserAccountType.Admin.ToString());
            (LocalHardware created, _) = await admin.Create(NewDevice());

            // User is the least-privileged real role on the node. Educator IS permitted here, which
            // is itself the reason blocker 4 rates the exploit precondition as an ordinary account
            // rather than physical possession of a headset.
            HardwareLogic outsider = BuildLogic(context, revocations, InstitutionUserAccountType.User.ToString());
            string result = await outsider.RegenerateCredential(created.Id);

            result.Should().BeNull("an unprivileged caller gets no credential");
            context.Hardware.Single().CredentialRegeneratedAt.Should().BeNull(
                "and must not have signed the device out as a side effect");
            revocations.Verify(r => r.RevokeAsync(It.IsAny<Guid>(), It.IsAny<TimeSpan>()), Times.Never);
        }
    }
}
