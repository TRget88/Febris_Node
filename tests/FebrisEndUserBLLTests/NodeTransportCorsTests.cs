// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.SharedServices;
using FluentAssertions;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// The node's operator-configured CORS origin predicate (<see cref="NodeTransport.IsOriginAllowed"/>),
    /// which replaces the febr.is hardcoding so a self-host node trusts its OWN frontend. Pins the
    /// security-critical matching: localhost is always allowed; an empty allow-list trusts no third party;
    /// exact-host and leading-dot (subdomain) entries match; and look-alikes / malformed origins are denied.
    /// </summary>
    public class NodeTransportCorsTests
    {
        private static readonly string[] None = new string[0];

        [Theory]
        [InlineData("http://localhost:5000")]
        [InlineData("https://localhost")]
        [InlineData("http://127.0.0.1:3000")]
        public void Localhost_IsAlwaysAllowed(string origin)
        {
            NodeTransport.IsOriginAllowed(origin, None).Should().BeTrue();
        }

        [Fact]
        public void EmptyAllowList_DeniesNonLocalhost()
        {
            NodeTransport.IsOriginAllowed("https://app.example.com", None).Should().BeFalse();
        }

        [Fact]
        public void ExactHost_MatchesOnlyThatHost()
        {
            var allow = new[] { "app.example.com" };
            NodeTransport.IsOriginAllowed("https://app.example.com", allow).Should().BeTrue();
            NodeTransport.IsOriginAllowed("https://other.example.com", allow).Should().BeFalse();
        }

        [Fact]
        public void LeadingDot_MatchesDomainAndSubdomains()
        {
            var allow = new[] { ".example.com" };
            NodeTransport.IsOriginAllowed("https://example.com", allow).Should().BeTrue();
            NodeTransport.IsOriginAllowed("https://app.example.com", allow).Should().BeTrue();
            NodeTransport.IsOriginAllowed("https://deep.app.example.com", allow).Should().BeTrue();
        }

        [Theory]
        [InlineData("https://evilexample.com")]
        [InlineData("https://example.com.evil.com")]
        [InlineData("https://notexample.com")]
        public void LeadingDot_RejectsLookAlikes(string origin)
        {
            NodeTransport.IsOriginAllowed(origin, new[] { ".example.com" }).Should().BeFalse();
        }

        [Fact]
        public void Host_MatchIsCaseInsensitive()
        {
            NodeTransport.IsOriginAllowed("https://APP.Example.COM", new[] { "app.example.com" }).Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-uri")]
        [InlineData("app.example.com")]   // no scheme -> not an absolute origin
        public void MalformedOrigin_IsDenied(string origin)
        {
            NodeTransport.IsOriginAllowed(origin, new[] { "app.example.com" }).Should().BeFalse();
        }

        [Fact]
        public void NullAllowList_DeniesNonLocalhost_ButAllowsLocalhost()
        {
            NodeTransport.IsOriginAllowed("https://app.example.com", null).Should().BeFalse();
            NodeTransport.IsOriginAllowed("http://localhost", null).Should().BeTrue();
        }

        [Fact]
        public void Options_Defaults_AreSafe()
        {
            var o = new NodeTransportOptions();

            o.Hsts.Enabled.Should().BeTrue();
            o.Hsts.MaxAgeDays.Should().Be(365);
            o.Hsts.IncludeSubdomains.Should().BeTrue();
            o.Hsts.Preload.Should().BeFalse();
            o.HttpsRedirection.Should().BeFalse();
            o.Cors.AllowedHosts.Should().BeEmpty();
            o.Cors.AllowCredentials.Should().BeTrue();
            o.SecurityHeaders.XContentTypeOptions.Should().BeTrue();
            o.SecurityHeaders.XXssProtection.Should().BeTrue();
            o.SecurityHeaders.XFrameOptions.Should().Be("SameOrigin");
        }
    }
}
