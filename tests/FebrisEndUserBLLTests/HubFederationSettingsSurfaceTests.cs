// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.FederationLogic;
using Febris.SharedServices;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the operator-owned federation settings surface (sub-slice 1; owner-ratified
    /// 2026-07-17 "the operator owns federation"):
    /// <list type="bullet">
    /// <item>settings round-trip with REAL encryption -- the stored LicenseKey column carries an
    /// IDataProtection payload (dedicated purpose), never plaintext, and reads decrypt it back;</item>
    /// <item>unresolved deploy placeholders ({Token}) resolve the gate DISABLED, config and DB
    /// alike (MED-6 family, fail-closed);</item>
    /// <item>DB-first precedence -- a stored HubFederationConfig row GOVERNS over config, while a
    /// row-less node keeps the legacy configuration resolution byte-for-byte;</item>
    /// <item>masked display -- the admin page model never carries the full key.</item>
    /// </list>
    /// Uses the EF InMemory provider plus an ephemeral (but real) DataProtection provider.
    /// </summary>
    public class HubFederationSettingsSurfaceTests
    {
        private static DbContextOptions<DataDbContext> InMemoryOptions(string dbName)
        {
            return new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }

        private static IDataProtectionProvider RealProtectionProvider()
        {
            // Ephemeral = keys live and die with the test process, but Protect/Unprotect are the
            // REAL DataProtection pipeline -- exactly what the hosts wire (file-system ring,
            // SetApplicationName("Febris.UserAuth")) minus the persistence.
            return new EphemeralDataProtectionProvider();
        }

        private static IConfiguration Config(Dictionary<string, string> settings)
        {
            return new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string>()).Build();
        }

        private static Dictionary<string, string> LegacyHubConfig()
        {
            return new Dictionary<string, string>
            {
                ["ApiUrlPath:DataApi"] = "https://legacy.example/api/",
                ["ApiUrlPath:AuthenticationApi"] = "https://legacy.example/auth/",
                ["LicenseKey"] = "8b4bffe1-7d05-4c0f-9d7c-9a2b3c4d5e6f",
            };
        }

        /// <summary>A resolver over a DI graph carrying an InMemory DataDb + real protection --
        /// the exact composition AddFebrisUserNodeDataAccess produces at a host.</summary>
        private static (HubFederationSettingsResolver Resolver, ServiceProvider Provider) BuildResolver(
            string dbName, Dictionary<string, string> configSettings)
        {
            IConfiguration config = Config(configSettings);
            ServiceProvider provider = new ServiceCollection()
                .AddSingleton(RealProtectionProvider())
                .AddDbContext<DataDbContext>(o => o.UseInMemoryDatabase(dbName))
                .AddScoped<IHubFederationConfigQueries, HubFederationConfigQueries>()
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var resolver = new HubFederationSettingsResolver(
                provider.GetRequiredService<IServiceScopeFactory>(), config);
            return (resolver, provider);
        }

        #region Round-trip with encryption at rest

        [Fact]
        public async Task Save_EncryptsLicenseKeyAtRest_AndGetDecryptsIt()
        {
            const string plaintext = "8b4bffe1-7d05-4c0f-9d7c-9a2b3c4d5e6f";
            var options = InMemoryOptions(nameof(Save_EncryptsLicenseKeyAtRest_AndGetDecryptsIt));
            IDataProtectionProvider protection = RealProtectionProvider();

            using (DataDbContext context = new DataDbContext(options))
            {
                var queries = new HubFederationConfigQueries(context, protection);
                HubFederationConfig saved = await queries.Save(new HubFederationConfig()
                {
                    Enabled = true,
                    DataApi = "https://hub.example/api/",
                    AuthenticationApi = "https://hub.example/auth/",
                    LicenseKey = plaintext
                });
                saved.LicenseKey.Should().Be(plaintext, "the seam deals in plaintext");
            }

            // The COLUMN must not carry the plaintext: read the raw row, no queries in between.
            using (DataDbContext context = new DataDbContext(options))
            {
                HubFederationConfig raw = context.HubFederationConfig.AsNoTracking().Single();
                raw.LicenseKey.Should().NotBeNullOrWhiteSpace();
                raw.LicenseKey.Should().NotBe(plaintext, "the license key must be encrypted at rest");
                raw.LicenseKey.Should().NotContain(plaintext);
                raw.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            }

            // And a fresh reader over the same ring decrypts it back.
            using (DataDbContext context = new DataDbContext(options))
            {
                var queries = new HubFederationConfigQueries(context, protection);
                (await queries.Get()).LicenseKey.Should().Be(plaintext);
            }
        }

        [Fact]
        public async Task Get_UnreadableStoredKey_DegradesToNullInsteadOfThrowing()
        {
            // A payload from a FOREIGN key ring (rotated/hand-edited) must not take the page or
            // the resolver down -- endpoints and the switch still govern.
            var options = InMemoryOptions(nameof(Get_UnreadableStoredKey_DegradesToNullInsteadOfThrowing));
            using (DataDbContext context = new DataDbContext(options))
            {
                var writer = new HubFederationConfigQueries(context, RealProtectionProvider());
                await writer.Save(new HubFederationConfig() { Enabled = true, DataApi = "https://hub.example/api/", LicenseKey = "some-key" });
            }

            using (DataDbContext context = new DataDbContext(options))
            {
                var foreignReader = new HubFederationConfigQueries(context, RealProtectionProvider());
                HubFederationConfig row = await foreignReader.Get();
                row.Should().NotBeNull();
                row.LicenseKey.Should().BeNull("an unprotectable payload degrades to an absent key");
                row.DataApi.Should().Be("https://hub.example/api/");
            }
        }

        [Fact]
        public async Task Save_IsSingleRow_SecondSaveUpdatesInPlace()
        {
            var options = InMemoryOptions(nameof(Save_IsSingleRow_SecondSaveUpdatesInPlace));
            IDataProtectionProvider protection = RealProtectionProvider();
            using DataDbContext context = new DataDbContext(options);
            var queries = new HubFederationConfigQueries(context, protection);

            await queries.Save(new HubFederationConfig() { Enabled = false, DataApi = "https://one.example/" });
            await queries.Save(new HubFederationConfig() { Enabled = true, DataApi = "https://two.example/" });

            context.HubFederationConfig.AsNoTracking().Should().HaveCount(1, "single-row semantics like NodeIdentity");
            HubFederationConfig row = await queries.Get();
            row.Enabled.Should().BeTrue();
            row.DataApi.Should().Be("https://two.example/");
        }

        #endregion

        #region Placeholders resolve the gate DISABLED (config + DB)

        [Fact]
        public void Resolve_LegacyPlaceholders_GateStaysClosed()
        {
            // Pre-fix, an unsubstituted deploy template ({DataApiUrl} + {LicenseKey}) counted as
            // "configured" and OPENED the legacy gate against garbage endpoints.
            HubFederationSettings gate = HubFederationSettings.Resolve(Config(new Dictionary<string, string>
            {
                ["ApiUrlPath:DataApi"] = "{DataApiUrl}",
                ["ApiUrlPath:AuthenticationApi"] = "{AuthenticationApiUrl}",
                ["LicenseKey"] = "{LicenseKey}",
            }), JwtSigningKeyProvider.IsUnsubstitutedTemplate);

            gate.Enabled.Should().BeFalse("unsubstituted placeholders must fail CLOSED");
            gate.CanReachDataApi.Should().BeFalse();
            gate.CanReachAuthenticationApi.Should().BeFalse();
            gate.HasLicenseKey.Should().BeFalse();
        }

        [Fact]
        public void Resolve_SectionEnabledButAllEndpointsArePlaceholders_GateStaysClosed()
        {
            HubFederationSettings gate = HubFederationSettings.Resolve(Config(new Dictionary<string, string>
            {
                ["HubFederation:Enabled"] = "true",
                ["HubFederation:DataApi"] = "{DataApiUrl}",
                ["HubFederation:AuthenticationApi"] = "{AuthenticationApiUrl}",
                ["HubFederation:LicenseKey"] = "{LicenseKey}",
            }), JwtSigningKeyProvider.IsUnsubstitutedTemplate);

            gate.Enabled.Should().BeFalse("Enabled=true over nothing but placeholders is a deploy failure, not an open gate");
        }

        [Fact]
        public void Resolve_SectionWithOneRealEndpoint_KeepsTheSurvivor()
        {
            // Half-substituted config: the real endpoint survives (the health check reports the
            // gap); the placeholder one is treated as absent.
            HubFederationSettings gate = HubFederationSettings.Resolve(Config(new Dictionary<string, string>
            {
                ["HubFederation:Enabled"] = "true",
                ["HubFederation:DataApi"] = "https://hub.example/api/",
                ["HubFederation:AuthenticationApi"] = "{AuthenticationApiUrl}",
            }), JwtSigningKeyProvider.IsUnsubstitutedTemplate);

            gate.Enabled.Should().BeTrue();
            gate.CanReachDataApi.Should().BeTrue();
            gate.CanReachAuthenticationApi.Should().BeFalse("the placeholder endpoint is absent");
        }

        [Fact]
        public void Resolve_WithoutPredicate_BehaviorUnchanged_LegacyBackCompat()
        {
            // The 1-arg overload (and a null predicate) keeps the historical semantics exactly.
            HubFederationSettings gate = HubFederationSettings.Resolve(Config(new Dictionary<string, string>
            {
                ["ApiUrlPath:DataApi"] = "{DataApiUrl}",
                ["LicenseKey"] = "{LicenseKey}",
            }));

            gate.Enabled.Should().BeTrue("no predicate = the pre-fix behavior, unchanged for the pure-function callers");
        }

        [Fact]
        public async Task Resolver_DbRowWithPlaceholderEndpoints_GateDisabled()
        {
            string dbName = nameof(Resolver_DbRowWithPlaceholderEndpoints_GateDisabled);
            var (resolver, provider) = BuildResolver(dbName, null);
            using (provider)
            {
                using (IServiceScope scope = provider.CreateScope())
                {
                    await scope.ServiceProvider.GetRequiredService<IHubFederationConfigQueries>()
                        .Save(new HubFederationConfig() { Enabled = true, DataApi = "{DataApiUrl}", AuthenticationApi = "{AuthenticationApiUrl}" });
                }

                resolver.Enabled.Should().BeFalse("a {Token} pasted into the portal must fail closed too");
                resolver.CanReachDataApi.Should().BeFalse();
            }
        }

        #endregion

        #region DB-first precedence + legacy back-compat

        [Fact]
        public void Resolver_NoDbRow_LegacyConfigResolutionGovernsUnchanged()
        {
            var (resolver, provider) = BuildResolver(
                nameof(Resolver_NoDbRow_LegacyConfigResolutionGovernsUnchanged), LegacyHubConfig());
            using (provider)
            {
                resolver.Enabled.Should().BeTrue("a row-less node keeps the existing config resolution");
                resolver.DataApi.Should().Be("https://legacy.example/api/");
                resolver.AuthenticationApi.Should().Be("https://legacy.example/auth/");
                resolver.HasLicenseKey.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Resolver_DbRowGoverns_OverEnabledLegacyConfig()
        {
            // The operator's stored answer beats the deployment's config files: a saved
            // Enabled=false row CLOSES a gate the legacy keys would have opened.
            string dbName = nameof(Resolver_DbRowGoverns_OverEnabledLegacyConfig);
            var (resolver, provider) = BuildResolver(dbName, LegacyHubConfig());
            using (provider)
            {
                using (IServiceScope scope = provider.CreateScope())
                {
                    await scope.ServiceProvider.GetRequiredService<IHubFederationConfigQueries>()
                        .Save(new HubFederationConfig() { Enabled = false, DataApi = "https://operator.example/api/" });
                }

                resolver.Enabled.Should().BeFalse("the DB row governs");
                resolver.DataApi.Should().Be("https://operator.example/api/", "endpoints come from the row, not ApiUrlPath");
            }
        }

        [Fact]
        public async Task Resolver_DbRowOpensGate_OverEmptyConfig_AndInvalidateAppliesImmediately()
        {
            string dbName = nameof(Resolver_DbRowOpensGate_OverEmptyConfig_AndInvalidateAppliesImmediately);
            var (resolver, provider) = BuildResolver(dbName, null);
            using (provider)
            {
                // First consultation caches the row-less answer: closed.
                resolver.Enabled.Should().BeFalse();

                using (IServiceScope scope = provider.CreateScope())
                {
                    await scope.ServiceProvider.GetRequiredService<IHubFederationConfigQueries>()
                        .Save(new HubFederationConfig()
                        {
                            Enabled = true,
                            DataApi = "https://operator.example/api/",
                            AuthenticationApi = "https://operator.example/auth/",
                            LicenseKey = "8b4bffe1-7d05-4c0f-9d7c-9a2b3c4d5e6f"
                        });
                }

                // Within the TTL the cached snapshot still serves...
                resolver.Enabled.Should().BeFalse("the snapshot is cached for the TTL");

                // ...until the save path invalidates, after which the row governs immediately,
                // license key DECRYPTED so the scheme-B bootstrap can parse it.
                ((IHubFederationSettingsCache)resolver).Invalidate();
                resolver.Enabled.Should().BeTrue();
                resolver.CanReachDataApi.Should().BeTrue();
                resolver.CanReachAuthenticationApi.Should().BeTrue();
                resolver.HasLicenseKey.Should().BeTrue("the resolver must serve the DECRYPTED key");
            }
        }

        [Fact]
        public void AddFebrisUserNodeDataAccess_RegistersResolverForGateAndCache()
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddFebrisUserNodeDataAccess(Config(null))
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

            IHubFederationSettings gate = provider.GetRequiredService<IHubFederationSettings>();
            IHubFederationSettingsCache cache = provider.GetRequiredService<IHubFederationSettingsCache>();

            gate.Should().BeOfType<HubFederationSettingsResolver>();
            cache.Should().BeSameAs(gate, "one resolver instance serves both contracts");
            gate.Enabled.Should().BeFalse("no row + no config = closed, as ever");
        }

        #endregion

        #region Admin page logic -- masked display + write-only key semantics

        private static HubFederationSettingsLogic BuildLogic(DataDbContext context, IDataProtectionProvider protection,
            IHubFederationSettings federation = null, IHubFederationSettingsCache cache = null)
        {
            var queries = new HubFederationConfigQueries(context, protection);
            ServiceProvider provider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
            return new HubFederationSettingsLogic(
                queries,
                federation ?? HubFederationSettings.Disabled(),
                cache,
                provider.GetRequiredService<IHttpClientFactory>());
        }

        [Fact]
        public async Task GetSettings_MaskedDisplay_NeverReturnsTheFullKey()
        {
            const string plaintext = "8b4bffe1-7d05-4c0f-9d7c-9a2b3c4d5e6f";
            using DataDbContext context = new DataDbContext(InMemoryOptions(nameof(GetSettings_MaskedDisplay_NeverReturnsTheFullKey)));
            IDataProtectionProvider protection = RealProtectionProvider();
            HubFederationSettingsLogic logic = BuildLogic(context, protection);

            await logic.SaveSettings(new HubFederationSettingsInputModel()
            {
                Enabled = true,
                DataApi = "https://hub.example/api/",
                LicenseKey = plaintext
            });
            HubFederationSettingsViewModel model = await logic.GetSettings();

            model.HasLicenseKey.Should().BeTrue();
            model.LicenseKeyMasked.Should().Be("****5e6f", "last four characters only");
            model.LicenseKeyMasked.Should().NotBe(plaintext);
            model.LicenseKeyMasked.Should().NotContain(plaintext.Substring(0, 8), "no full-key prefix may leak");
        }

        [Fact]
        public void MaskLicenseKey_ShortAndEmptyKeys_MaskCompletely()
        {
            HubFederationSettingsLogic.MaskLicenseKey(null).Should().BeNull();
            HubFederationSettingsLogic.MaskLicenseKey("").Should().BeNull();
            HubFederationSettingsLogic.MaskLicenseKey("abcd").Should().Be("****", "a short key must not echo back in full");
            HubFederationSettingsLogic.MaskLicenseKey("abcdef").Should().Be("****cdef");
        }

        [Fact]
        public async Task SaveSettings_BlankKeyKeepsStoredKey_ClearRemovesIt()
        {
            const string plaintext = "8b4bffe1-7d05-4c0f-9d7c-9a2b3c4d5e6f";
            using DataDbContext context = new DataDbContext(InMemoryOptions(nameof(SaveSettings_BlankKeyKeepsStoredKey_ClearRemovesIt)));
            IDataProtectionProvider protection = RealProtectionProvider();
            HubFederationSettingsLogic logic = BuildLogic(context, protection);

            await logic.SaveSettings(new HubFederationSettingsInputModel() { Enabled = true, DataApi = "https://hub.example/api/", LicenseKey = plaintext });

            // Editing the endpoints with a BLANK key field keeps the stored credential (the form
            // never round-trips it, so blank means "unchanged").
            HubFederationSettingsViewModel afterEdit = await logic.SaveSettings(new HubFederationSettingsInputModel()
            {
                Enabled = true,
                DataApi = "https://hub.example/api/v2/"
            });
            afterEdit.HasLicenseKey.Should().BeTrue("blank keeps the stored key");
            afterEdit.LicenseKeyMasked.Should().Be("****5e6f");

            // The explicit clear removes it even when a value is also supplied.
            HubFederationSettingsViewModel afterClear = await logic.SaveSettings(new HubFederationSettingsInputModel()
            {
                Enabled = true,
                DataApi = "https://hub.example/api/v2/",
                LicenseKey = "should-be-ignored",
                ClearLicenseKey = true
            });
            afterClear.HasLicenseKey.Should().BeFalse("clear beats everything");
            afterClear.LicenseKeyMasked.Should().BeNull();
        }

        [Fact]
        public async Task SaveSettings_InvalidatesTheResolverCache()
        {
            // The full loop the portal Save drives: resolver caches "closed", the operator saves
            // an enabled row through the LOGIC, and the resolver answers "open" immediately.
            string dbName = nameof(SaveSettings_InvalidatesTheResolverCache);
            var (resolver, provider) = BuildResolver(dbName, null);
            using (provider)
            {
                resolver.Enabled.Should().BeFalse();

                using DataDbContext context = new DataDbContext(InMemoryOptions(dbName));
                HubFederationSettingsLogic logic = BuildLogic(
                    context,
                    provider.GetRequiredService<IDataProtectionProvider>(),
                    federation: resolver,
                    cache: resolver);
                await logic.SaveSettings(new HubFederationSettingsInputModel() { Enabled = true, DataApi = "https://hub.example/api/" });

                resolver.Enabled.Should().BeTrue("the save path invalidates the snapshot; no TTL wait on the saving host");
            }
        }

        [Fact]
        public async Task TestConnection_GateClosed_ReportsHealthyDisabled()
        {
            // The probe is the SAME gate-aware check the readiness endpoint runs: a standalone
            // node is a supported shape, so a closed gate probes Healthy/"disabled", not an error.
            using DataDbContext context = new DataDbContext(InMemoryOptions(nameof(TestConnection_GateClosed_ReportsHealthyDisabled)));
            HubFederationSettingsLogic logic = BuildLogic(context, RealProtectionProvider());

            HubProbeResultViewModel probe = await logic.TestConnection();

            probe.Status.Should().Be("Healthy");
            probe.Description.Should().Be(Logic.HealthLogic.HubFederationHealthCheck.DisabledDescription);
        }

        #endregion
    }
}
