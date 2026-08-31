// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.Extensions.Options;
using System;

namespace Febris.UserNode.Portal.IdentityPolicy
{
    /// <summary>
    /// Read-only view over the configured registration policy. Encapsulates the
    /// self-service registration rules so callers never branch on <see cref="RegistrationMode"/>
    /// directly.
    /// </summary>
    public interface IRegistrationPolicy
    {
        /// <summary>The configured registration mode.</summary>
        RegistrationMode Mode { get; }

        /// <summary>
        /// True only when unauthenticated users may register themselves, i.e. when
        /// <see cref="Mode"/> is <see cref="RegistrationMode.Open"/> or
        /// <see cref="RegistrationMode.DomainAllowlist"/>.
        /// </summary>
        bool SelfRegistrationEnabled { get; }

        /// <summary>True when a newly registered account must be approved by an admin before use.</summary>
        bool RequiresAdminApproval { get; }

        /// <summary>Whether a first-login unknown external-IdP user is auto-provisioned a local account (JIT).
        /// False = closed SSO (unknown users are turned away; they must be pre-provisioned by an admin).</summary>
        bool AutoProvisionJitEnabled { get; }

        /// <summary>
        /// Whether the given email address is permitted to register under the current policy.
        /// Open => any well-formed address; DomainAllowlist => the address' domain must appear in
        /// the configured allowlist; AdminOnly/Invite => always false. Null, blank, or malformed
        /// addresses are never allowed.
        /// </summary>
        bool IsEmailAllowed(string email);
    }

    /// <summary>
    /// Default <see cref="IRegistrationPolicy"/> backed by <see cref="IdentityPolicyOptions"/>.
    /// </summary>
    public sealed class RegistrationPolicy : IRegistrationPolicy
    {
        private readonly RegistrationOptions _registration;

        public RegistrationPolicy(IOptions<IdentityPolicyOptions> options)
        {
            // Tolerate a null IOptions or an absent/null Registration section by falling back to defaults.
            _registration = options?.Value?.Registration ?? new RegistrationOptions();
        }

        public RegistrationMode Mode
        {
            get { return _registration.Mode; }
        }

        public bool SelfRegistrationEnabled
        {
            get
            {
                return _registration.Mode == RegistrationMode.Open
                    || _registration.Mode == RegistrationMode.DomainAllowlist;
            }
        }

        public bool RequiresAdminApproval
        {
            get { return _registration.RequireAdminApproval; }
        }

        public bool AutoProvisionJitEnabled
        {
            get { return _registration.AutoProvisionJit; }
        }

        public bool IsEmailAllowed(string email)
        {
            string domain;
            if (!TryGetDomain(email, out domain))
            {
                // Null, blank, or malformed address.
                return false;
            }

            switch (_registration.Mode)
            {
                case RegistrationMode.Open:
                    return true;
                case RegistrationMode.DomainAllowlist:
                    return IsDomainAllowlisted(domain);
                default: // AdminOnly, Invite
                    return false;
            }
        }

        private bool IsDomainAllowlisted(string domain)
        {
            string[] allowed = _registration.AllowedEmailDomains;
            if (allowed == null)
            {
                return false;
            }

            foreach (string entry in allowed)
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                // Allowlist entries may be stored as "example.com" or "@example.com".
                string normalized = entry.Trim();
                if (normalized.Length > 0 && normalized[0] == '@')
                {
                    normalized = normalized.Substring(1);
                }

                if (string.Equals(normalized, domain, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts the domain part of an email address, requiring exactly one '@' with a
        /// non-empty local part and a non-empty domain part. Returns false for null/blank/malformed input.
        /// </summary>
        private static bool TryGetDomain(string email, out string domain)
        {
            domain = null;
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            string trimmed = email.Trim();
            int at = trimmed.IndexOf('@');

            // Require a single '@', a non-empty local part (at > 0), and a non-empty domain part.
            if (at <= 0 || at != trimmed.LastIndexOf('@') || at == trimmed.Length - 1)
            {
                return false;
            }

            domain = trimmed.Substring(at + 1);
            return true;
        }
    }
}
