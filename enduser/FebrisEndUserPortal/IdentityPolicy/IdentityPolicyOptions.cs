// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
namespace Febris.UserNode.Portal.IdentityPolicy
{
    /// <summary>
    /// Root configuration for the tenant portal's identity policy, bound from the
    /// "<see cref="SectionName"/>" configuration section.
    /// </summary>
    public class IdentityPolicyOptions
    {
        /// <summary>The configuration section name these options bind from.</summary>
        public const string SectionName = "Identity";

        /// <summary>Account registration and sign-up policy.</summary>
        public RegistrationOptions Registration { get; set; } = new();

        /// <summary>Password strength and composition policy.</summary>
        public PasswordPolicyOptions Password { get; set; } = new();

        /// <summary>Failed-login lockout policy.</summary>
        public LockoutOptions Lockout { get; set; } = new();

        /// <summary>Two-factor authentication policy.</summary>
        public TwoFactorOptions TwoFactor { get; set; } = new();

        /// <summary>Login and self-service credential policy.</summary>
        public LoginOptions Login { get; set; } = new();

        /// <summary>Session lifetime policy.</summary>
        public SessionOptions Session { get; set; } = new();

        /// <summary>Account lifecycle, deletion, and data-portability policy.</summary>
        public AccountLifecycleOptions AccountLifecycle { get; set; } = new();
    }

    /// <summary>
    /// Controls how end-user accounts may be registered.
    /// </summary>
    public class RegistrationOptions
    {
        /// <summary>Determines who may create new accounts.</summary>
        public RegistrationMode Mode { get; set; } = RegistrationMode.AdminOnly;

        /// <summary>
        /// Email domains permitted to self-register when <see cref="Mode"/> is
        /// <see cref="RegistrationMode.DomainAllowlist"/>.
        /// </summary>
        public string[] AllowedEmailDomains { get; set; } = System.Array.Empty<string>();

        /// <summary>Whether a confirmed email address is required before an account is usable.</summary>
        public bool RequireConfirmedEmail { get; set; } = true;

        /// <summary>Whether an administrator must approve an account before it is activated.</summary>
        public bool RequireAdminApproval { get; set; } = false;

        /// <summary>
        /// Whether an unknown user authenticated by an external IdP is auto-provisioned a local account on
        /// first login (JIT). When false ("closed SSO"), a first-login unknown user is turned away and must be
        /// pre-provisioned by an admin. Still subject to <see cref="Mode"/> / domain admission when true.
        /// Defaults to closed: an operator who wires up an IdP has to opt in to auto-provisioning rather
        /// than discover that everyone the IdP will authenticate already has an account here.
        /// </summary>
        public bool AutoProvisionJit { get; set; } = false;
    }

    /// <summary>
    /// Password strength and composition requirements.
    /// </summary>
    public class PasswordPolicyOptions
    {
        /// <summary>Minimum required password length.</summary>
        public int RequiredLength { get; set; } = 8;

        /// <summary>Whether at least one digit (0-9) is required.</summary>
        public bool RequireDigit { get; set; } = true;

        /// <summary>Whether at least one uppercase letter is required.</summary>
        public bool RequireUppercase { get; set; } = true;

        /// <summary>Whether at least one lowercase letter is required.</summary>
        public bool RequireLowercase { get; set; } = true;

        /// <summary>Whether at least one non-alphanumeric character is required.</summary>
        public bool RequireNonAlphanumeric { get; set; } = false;

        /// <summary>Minimum number of distinct characters a password must contain.</summary>
        public int RequiredUniqueChars { get; set; } = 1;
    }

    /// <summary>
    /// Failed-login lockout policy.
    /// </summary>
    public class LockoutOptions
    {
        /// <summary>Number of failed login attempts before an account is locked out.</summary>
        public int MaxFailedAttempts { get; set; } = 5;

        /// <summary>Duration, in minutes, that an account remains locked out.</summary>
        public int LockoutMinutes { get; set; } = 15;

        /// <summary>Whether lockout is enabled for newly created users.</summary>
        public bool EnabledForNewUsers { get; set; } = true;
    }

    /// <summary>
    /// Two-factor authentication policy.
    /// </summary>
    public class TwoFactorOptions
    {
        /// <summary>The extent to which two-factor authentication is enforced.</summary>
        public TwoFactorEnforcement Enforcement { get; set; } = TwoFactorEnforcement.Off;
    }

    /// <summary>
    /// Login and self-service credential policy.
    /// </summary>
    public class LoginOptions
    {
        /// <summary>Whether local username/password sign-in is permitted.</summary>
        public bool AllowLocalPassword { get; set; } = true;

        /// <summary>Whether users may reset their own password via self-service.</summary>
        public bool AllowSelfServiceReset { get; set; } = true;
    }

    /// <summary>
    /// Session lifetime policy.
    /// </summary>
    public class SessionOptions
    {
        /// <summary>Session lifetime, in minutes.</summary>
        public int LifetimeMinutes { get; set; } = 60;

        /// <summary>Whether the session lifetime slides (renews) on activity.</summary>
        public bool Sliding { get; set; } = true;

        /// <summary>
        /// Optional absolute session timeout, in minutes, after which a session expires
        /// regardless of activity. <c>null</c> disables the absolute timeout.
        /// </summary>
        public int? AbsoluteTimeoutMinutes { get; set; } = null;
    }

    /// <summary>
    /// Account lifecycle, deletion, and data-portability policy.
    /// </summary>
    public class AccountLifecycleOptions
    {
        /// <summary>Whether account deletion is soft (marked deleted) rather than hard (removed).</summary>
        public bool SoftDeleteOnly { get; set; } = true;

        /// <summary>Whether users may delete their own account via self-service.</summary>
        public bool AllowSelfServiceDeletion { get; set; } = false;

        /// <summary>Whether users may export their own personal data.</summary>
        public bool AllowPersonalDataExport { get; set; } = true;

        /// <summary>
        /// Optional number of days after soft deletion before an account is permanently purged.
        /// <c>null</c> disables automatic purging.
        /// </summary>
        public int? PurgeAfterDays { get; set; } = null;
    }
}
