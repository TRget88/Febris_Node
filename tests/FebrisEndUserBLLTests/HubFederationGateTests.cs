// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.TicketModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer;
using Febris.UserNode.DataAccessLayer.Queries;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the ONE hub-federation gate (auth severance, slice 1):
    /// <list type="bullet">
    /// <item>the gate's config shape -- "HubFederation" section governs, default CLOSED, with
    /// legacy ApiUrlPath+LicenseKey back-compat so existing deployments stay federated;</item>
    /// <item>gate-off short-circuit -- a Remote query class and the scheme-B TokenQueries
    /// bootstrap construct cleanly with NO hub config anywhere (null passed-back config, no
    /// ApiUrlPath, no LicenseKey) and return quiet local-only defaults with zero HTTP;</item>
    /// <item>DI -- AddFebrisUserNodeDataAccess registers the gate and TokenQueries
    /// resolves through the greedy ctor that receives it (marker-instance proof).</item>
    /// </list>
    /// </summary>
    public class HubFederationGateTests
    {
        private static IConfiguration Build(Dictionary<string, string> settings)
        {
            return new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string>()).Build();
        }

        #region Resolve -- the gate's config shape

        [Fact]
        public void Resolve_NullConfiguration_IsClosed()
        {
            HubFederationSettings gate = HubFederationSettings.Resolve(null);

            gate.Enabled.Should().BeFalse();
            gate.CanReachDataApi.Should().BeFalse();
            gate.CanReachAuthenticationApi.Should().BeFalse();
            gate.HasLicenseKey.Should().BeFalse();
        }

        [Fact]
        public void Resolve_EmptyConfiguration_IsClosed_DefaultOff()
        {
            HubFederationSettings gate = HubFederationSettings.Resolve(Build(null));

            gate.Enabled.Should().BeFalse("a node with no hub configured must never federate");
        }

        [Fact]
        public void Resolve_HubFederationSection_Governs()
        {
            HubFederationSettings gate = HubFederationSettings.Resolve(Build(new Dictionary<string, string>
            {
                ["HubFederation:Enabled"] = "true",
                ["HubFederation:DataApi"] = "https://hub.example/api/",
                ["HubFederation:AuthenticationApi"] = "https://hub.example/auth/",
                ["HubFederation:LicenseKey"] = "8b4bffe1-7d05-4c0f-9d7c-9a2b3c4d5e6f",
            }));

            gate.Enabled.Should().BeTrue();
            gate.CanReachDataApi.Should().BeTrue();
            gate.CanReachAuthenticationApi.Should().BeTrue();
            gate.HasLicenseKey.Should().BeTrue();
        }

        [Fact]
        public void Resolve_HubFederationSectionPresent_EnabledDefaultsFalse_AndLegacyKeysAreIgnored()
        {
            // Once a deployment adopts the section, the section is the single truth: an explicit
            // section with no Enabled=true stays closed even if stale legacy keys linger.
            HubFederationSettings gate = HubFederationSettings.Resolve(Build(new Dictionary<string, string>
            {
                ["HubFederation:DataApi"] = "https://hub.example/api/",
                ["ApiUrlPath:DataApi"] = "https://legacy.example/api/",
                ["ApiUrlPath:AuthenticationApi"] = "https://legacy.example/auth/",
                ["LicenseKey"] = "8b4bffe1-7d05-4c0f-9d7c-9a2b3c4d5e6f",
            }));

            gate.Enabled.Should().BeFalse("HubFederation:Enabled defaults false and the section governs");
            gate.DataApi.Should().Be("https://hub.example/api/", "endpoints come from the section, not ApiUrlPath");
        }

        [Fact]
        public void Resolve_LegacyApiUrlPathPlusLicenseKey_TreatedAsEnabled_BackCompat()
        {
            // Existing deployments (no HubFederation section, ApiUrlPath + LicenseKey configured)
            // keep federating unchanged -- no config migration required.
            HubFederationSettings gate = HubFederationSettings.Resolve(Build(new Dictionary<string, string>
            {
                ["ApiUrlPath:DataApi"] = "https://legacy.example/api/",
                ["ApiUrlPath:AuthenticationApi"] = "https://legacy.example/auth/",
                ["LicenseKey"] = "8b4bffe1-7d05-4c0f-9d7c-9a2b3c4d5e6f",
            }));

            gate.Enabled.Should().BeTrue();
            gate.DataApi.Should().Be("https://legacy.example/api/");
            gate.AuthenticationApi.Should().Be("https://legacy.example/auth/");
            gate.HasLicenseKey.Should().BeTrue();
        }

        [Fact]
        public void Resolve_LegacyEndpointsWithoutLicenseKey_StaysClosed()
        {
            HubFederationSettings gate = HubFederationSettings.Resolve(Build(new Dictionary<string, string>
            {
                ["ApiUrlPath:DataApi"] = "https://legacy.example/api/",
            }));

            gate.Enabled.Should().BeFalse("the scheme-B credential is what made the legacy coupling live");
        }

        [Fact]
        public void Resolve_LegacyLicenseKeyWithoutEndpoints_StaysClosed()
        {
            HubFederationSettings gate = HubFederationSettings.Resolve(Build(new Dictionary<string, string>
            {
                ["LicenseKey"] = "8b4bffe1-7d05-4c0f-9d7c-9a2b3c4d5e6f",
            }));

            gate.Enabled.Should().BeFalse();
        }

        #endregion

        #region Gate off -- fail fast and quietly into local-only behavior

        [Fact]
        public async Task RemoteQuery_GateOff_ConstructsAndShortCircuits_NoHttp_NoThrow()
        {
            // StaticDetails.PassedBackConfig is null in the test process: the exact "no hub
            // credentials anywhere" state. Pre-gate, the ctor NRE'd on the unconditional
            // PassedBackConfig dereference; now it resolves a CLOSED gate.
            var queries = new InstitutionSettingsQueries();

            Stopwatch sw = Stopwatch.StartNew();
            List<InstitutionSettings> list = await queries.Get();
            InstitutionSettings single = await queries.Get(Guid.NewGuid());
            sw.Stop();

            // Quiet local-only defaults: the SAME values an unreachable hub already produced at
            // the BLL (empty list / empty settings object), with zero HTTP attempted.
            list.Should().NotBeNull().And.BeEmpty();
            single.Should().NotBeNull("gate-off must not surface nulls the legacy error path never produced");
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2),
                "a closed gate must fail FAST -- no connection attempts, no retries");
        }

        [Fact]
        public async Task TokenQueries_GateOff_LicenseBootstrapIsInert()
        {
            // The scheme-B license bootstrap demoted to an opt-in hub credential: with no
            // AuthenticationApi and no LicenseKey the class constructs, and every operation is a
            // quiet no-op (null/false) -- including RenewToken, the retry hook every DataApi
            // Remote query fires on a non-OK response.
            var queries = new TokenQueries();

            (await queries.RenewToken()).Should().BeFalse();
            (await queries.Authenticate()).Should().BeNull();
            (await queries.Get(new LicenseAuthenticationRequest() { LicenseKey = Guid.NewGuid() })).Should().BeNull();
            (await queries.Refresh("some-refresh-token")).Should().BeNull();
        }

        #endregion

        #region DI -- the gate flows through the container

        [Fact]
        public void AddFebrisUserNodeDataAccess_RegistersTheGate_ClosedByDefault()
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddFebrisUserNodeDataAccess(Build(null))
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

            IHubFederationSettings gate = provider.GetRequiredService<IHubFederationSettings>();
            gate.Enabled.Should().BeFalse();
        }

        [Fact]
        public void TokenQueries_ResolvesThroughTheGreedyDiCtor_ReceivingTheRegisteredGate()
        {
            // Marker proof (mirrors the StatementLogic greedy-ctor test): the container carries a
            // distinct gate instance; only the DI ctor receives it (the legacy ctors resolve from
            // the passed-back static config), so the private field must be the marker.
            var marker = new HubFederationSettings() { Enabled = false };
            var accessor = new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new Microsoft.AspNetCore.Http.DefaultHttpContext());

            using ServiceProvider provider = new ServiceCollection()
                .AddFebrisUserNodeDataAccess(Build(null))
                .AddSingleton(accessor.Object)
                .AddSingleton<IHubFederationSettings>(marker)
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using IServiceScope scope = provider.CreateScope();

            ITokenQueries queries = scope.ServiceProvider.GetRequiredService<ITokenQueries>();

            var field = typeof(TokenQueries).GetField("_federation",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.GetValue(queries).Should().BeSameAs(marker,
                "the greedy DI ctor must be selected wherever the gate is registered");
        }

        #endregion
    }
}
