// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
namespace Febris.UserNode.Portal.IdentityPolicy
{
    /// <summary>
    /// Controls who may create new end-user accounts in the tenant portal.
    /// </summary>
    public enum RegistrationMode
    {
        /// <summary>Only administrators may create accounts; there is no self-service sign-up.</summary>
        AdminOnly,

        /// <summary>Accounts may only be created via an explicit invitation.</summary>
        Invite,

        /// <summary>Anyone may self-register an account.</summary>
        Open,

        /// <summary>Self-registration is permitted only for email addresses on an allowed-domain list.</summary>
        DomainAllowlist
    }

    /// <summary>
    /// Controls the extent to which two-factor authentication is required.
    /// </summary>
    public enum TwoFactorEnforcement
    {
        /// <summary>Two-factor authentication is not enforced for any user.</summary>
        Off,

        /// <summary>Two-factor authentication is required for administrators only.</summary>
        AdminsRequired,

        /// <summary>Two-factor authentication is required for all users.</summary>
        AllRequired
    }
}
