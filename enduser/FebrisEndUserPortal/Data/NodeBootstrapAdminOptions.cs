// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.SharedServices;
using Microsoft.Extensions.Configuration;

namespace Febris.UserNode.Portal.Data
{
    /// <summary>
    /// Self-host clone-and-run seed data: the operator-configurable identity of the bootstrap
    /// ITAdmin that <see cref="SeedData"/> creates on first boot. (Said "SuperAdmin" until
    /// 2026-08-21; the account has been seeded as ITAdmin since the 2026-08-01 owner ruling and
    /// this doc had not kept up.)
    ///
    /// <para>
    /// THIS IS NOW THE UNATTENDED DOOR, NOT THE ONLY ONE. An operator who configures nothing no
    /// longer gets a seeded account at all: the node issues a first-run setup token instead and
    /// prints it to stdout, and the operator claims the node at <c>/setup</c>. A compiled-in
    /// default is a reasonable shape for automation and a poor one for an open-source project.
    /// This section remains for IaC and unattended installs, where supplying a password up front
    /// is exactly what is wanted.
    /// </para>
    ///
    /// <para>
    /// Why this exists: the production seed policy (option A) creates the admin with NO
    /// password and relies on the Forgot Password email round-trip -- but a freshly cloned
    /// self-hosted node has no SMTP configured yet, so its operator could never log in. The
    /// <c>NodeBootstrap</c> configuration section closes that gap while PRESERVING option A's
    /// invariant ("Febris never sets or stores a production password"): a password is only ever
    /// applied when the OPERATOR supplied one (docker-compose forwards it from the operator's
    /// .env), and Febris ships no default.
    /// </para>
    ///
    /// <para>Configuration surface (all optional; behavior without the section is byte-for-byte
    /// the pre-existing seed behavior):</para>
    /// <code>
    /// "NodeBootstrap": {
    ///   "AdminEmail":    "admin@example.org",   // default: admin@example.com
    ///   "AdminPassword": "operator-chosen"      // default: none (option A email flow)
    /// }
    /// </code>
    ///
    /// <para>
    /// Placeholder hardening (MED-6 family, same posture as the federation gate): an
    /// unsubstituted deployment template such as <c>{AdminPassword}</c> resolves as UNSET, so a
    /// templated-but-never-filled config cannot become a literal credential.
    /// </para>
    /// </summary>
    public sealed class NodeBootstrapAdminOptions
    {
        /// <summary>The configuration section this type binds.</summary>
        public const string SectionName = "NodeBootstrap";

        /// <summary>Neutral placeholder identity for deployments that configure no
        /// <c>NodeBootstrap</c> section. Deliberately a reserved example.com address rather than a
        /// real mailbox: an operator who never sets AdminEmail must not end up with an account
        /// addressed to somebody else, and no password is seeded by default either, so the
        /// account is inert until the operator configures one.</summary>
        public const string DefaultAdminEmail = "admin@example.com";

        /// <summary>The email/username the bootstrap ITAdmin is looked up and created with.
        /// Never null; falls back to <see cref="DefaultAdminEmail"/>.</summary>
        public string AdminEmail { get; private set; }

        /// <summary>The operator-supplied initial password, or null when none was configured
        /// (option A: password set via the Forgot Password email flow). Never logged.</summary>
        public string AdminPassword { get; private set; }

        /// <summary>True when the operator supplied an initial password.</summary>
        public bool HasOperatorPassword
        {
            get { return !string.IsNullOrEmpty(AdminPassword); }
        }

        private NodeBootstrapAdminOptions() { }

        /// <summary>
        /// Resolve the bootstrap-admin identity from configuration. Whitespace-only values and
        /// unsubstituted <c>{Template}</c> placeholders read as unset (see class doc).
        /// </summary>
        public static NodeBootstrapAdminOptions Resolve(IConfiguration configuration)
        {
            var options = new NodeBootstrapAdminOptions();
            string email = ReadSetting(configuration, SectionName + ":AdminEmail");
            options.AdminEmail = email ?? DefaultAdminEmail;
            options.AdminPassword = ReadSetting(configuration, SectionName + ":AdminPassword");
            return options;
        }

        /// <summary>Read one setting: trims, and normalizes blank / unsubstituted-template
        /// values to null so "configured with garbage" behaves like "not configured".</summary>
        private static string ReadSetting(IConfiguration configuration, string key)
        {
            string value = configuration == null ? null : configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            value = value.Trim();
            if (JwtSigningKeyProvider.IsUnsubstitutedTemplate(value))
            {
                return null;
            }
            return value;
        }
    }
}
