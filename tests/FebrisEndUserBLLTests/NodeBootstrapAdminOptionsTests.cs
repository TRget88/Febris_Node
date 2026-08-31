// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using Febris.UserNode.Portal.Data;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Self-host clone-and-run (bootstrap-admin slice): pins the
    /// resolution contract of <see cref="NodeBootstrapAdminOptions"/> -- the operator-facing
    /// "NodeBootstrap" section that lets a freshly cloned node log in without SMTP.
    ///
    /// The load-bearing guarantees:
    ///  1. NO section configured => the neutral placeholder identity, no password.
    ///  2. Operator values are honored (trimmed) when present.
    ///  3. "Configured with garbage" == "not configured": blank/whitespace values and
    ///     unsubstituted {Template} placeholders never become a login identity or a literal
    ///     password (MED-6 placeholder-hardening posture, same as the federation gate).
    /// </summary>
    public class NodeBootstrapAdminOptionsTests
    {
        private static IConfiguration Build(Dictionary<string, string> settings = null)
        {
            var builder = new ConfigurationBuilder();
            if (settings != null)
            {
                builder.AddInMemoryCollection(settings);
            }
            return builder.Build();
        }

        [Fact]
        public void Resolve_WithNoSection_UsesHistoricalDefaults()
        {
            NodeBootstrapAdminOptions options = NodeBootstrapAdminOptions.Resolve(Build());

            options.AdminEmail.Should().Be(NodeBootstrapAdminOptions.DefaultAdminEmail);
            options.AdminEmail.Should().Be("admin@example.com");
            options.AdminPassword.Should().BeNull();
            options.HasOperatorPassword.Should().BeFalse();
        }

        [Fact]
        public void Resolve_WithNullConfiguration_UsesHistoricalDefaults()
        {
            NodeBootstrapAdminOptions options = NodeBootstrapAdminOptions.Resolve(null);

            options.AdminEmail.Should().Be(NodeBootstrapAdminOptions.DefaultAdminEmail);
            options.HasOperatorPassword.Should().BeFalse();
        }

        [Fact]
        public void Resolve_WithOperatorValues_HonorsAndTrimsThem()
        {
            NodeBootstrapAdminOptions options = NodeBootstrapAdminOptions.Resolve(Build(
                new Dictionary<string, string>
                {
                    ["NodeBootstrap:AdminEmail"] = "  admin@example.org  ",
                    ["NodeBootstrap:AdminPassword"] = "  operator-chosen-secret!1  ",
                }));

            options.AdminEmail.Should().Be("admin@example.org");
            options.AdminPassword.Should().Be("operator-chosen-secret!1");
            options.HasOperatorPassword.Should().BeTrue();
        }

        [Fact]
        public void Resolve_WithPasswordOnly_KeepsDefaultEmail()
        {
            NodeBootstrapAdminOptions options = NodeBootstrapAdminOptions.Resolve(Build(
                new Dictionary<string, string>
                {
                    ["NodeBootstrap:AdminPassword"] = "operator-chosen-secret!1",
                }));

            options.AdminEmail.Should().Be(NodeBootstrapAdminOptions.DefaultAdminEmail);
            options.HasOperatorPassword.Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("{AdminPassword}")]
        [InlineData("{NodeAdminPassword}")]
        public void Resolve_TreatsBlankOrUnsubstitutedPasswordAsUnset(string configured)
        {
            NodeBootstrapAdminOptions options = NodeBootstrapAdminOptions.Resolve(Build(
                new Dictionary<string, string>
                {
                    ["NodeBootstrap:AdminPassword"] = configured,
                }));

            options.AdminPassword.Should().BeNull();
            options.HasOperatorPassword.Should().BeFalse();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("{AdminEmail}")]
        public void Resolve_TreatsBlankOrUnsubstitutedEmailAsUnset_FallingBackToDefault(string configured)
        {
            NodeBootstrapAdminOptions options = NodeBootstrapAdminOptions.Resolve(Build(
                new Dictionary<string, string>
                {
                    ["NodeBootstrap:AdminEmail"] = configured,
                }));

            options.AdminEmail.Should().Be(NodeBootstrapAdminOptions.DefaultAdminEmail);
        }
    }
}
