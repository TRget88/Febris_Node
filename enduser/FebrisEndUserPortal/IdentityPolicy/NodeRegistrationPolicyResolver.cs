// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.LogicLayer.Logic.IdentityLogic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;

namespace Febris.UserNode.Portal.IdentityPolicy
{
    /// <summary>Lets the admin save path drop this host's cached snapshot so its own save applies
    /// immediately rather than at the end of the TTL.</summary>
    public interface IRegistrationPolicyCache
    {
        /// <summary>Discard the cached snapshot; the next policy consultation re-resolves.</summary>
        void Invalidate();
    }

    /// <summary>
    /// DB-first resolution of the node's registration policy (node initialization design
    /// 2026-08-18). Replaces the boot-time, configuration-only <see cref="RegistrationPolicy"/>
    /// registration with a resolver, so that <c>Identity:Registration:Mode</c> stops being a
    /// setting that requires editing a JSON file inside a container and restarting the host.
    ///
    /// <list type="number">
    /// <item>When the tenant DataDb carries a stored policy row (the portal's Registration page was
    /// saved at least once), that row GOVERNS.</item>
    /// <item>Otherwise the configured <c>Identity:Registration</c> section governs unchanged, so a
    /// deployment that never touches the page behaves exactly as it did before.</item>
    /// <item>If the store cannot be READ, the policy is <see cref="RegistrationMode.AdminOnly"/>.</item>
    /// </list>
    ///
    /// <para>
    /// THE ASYMMETRY IN RULE 3 IS THE WHOLE POINT, and it is where this resolver deliberately
    /// departs from its sibling <c>HubFederationSettingsResolver</c>, which falls back to
    /// configuration on any database problem. Doing that here would mean a node whose stored policy
    /// says AdminOnly, but whose configuration file still says Open, RE-OPENS to the public every
    /// time the database blips. "Not configured" and "could not read" must not resolve the same
    /// way, which is why the logic layer throws on a read failure instead of returning an
    /// empty snapshot, and why this class treats a missing collaborator as a fault rather than as
    /// an absent row.
    /// </para>
    ///
    /// <para>
    /// This class decides WHICH <see cref="RegistrationOptions"/> govern and then delegates every
    /// actual decision to <see cref="RegistrationPolicy"/>. The admission rules (mode to
    /// self-registration, domain allowlist matching, malformed-address rejection) are not
    /// reimplemented here, so the stored path and the configured path cannot drift apart and the
    /// existing tests over <see cref="RegistrationPolicy"/> keep covering both.
    /// </para>
    ///
    /// <para>
    /// CACHING: a short-TTL snapshot (<see cref="CacheTtl"/>) on a singleton, mirroring the
    /// federation resolver. <see cref="IRegistrationPolicy"/> is consumed synchronously, including
    /// from a Razor <c>@inject</c> on the login page, so a per-request async read is not available
    /// without sync-over-async on the request path. The save path calls <see cref="Invalidate"/> so
    /// the acting host applies a change immediately; any sibling host converges within the TTL.
    /// </para>
    /// </summary>
    public sealed class NodeRegistrationPolicyResolver : IRegistrationPolicy, IRegistrationPolicyCache
    {
        /// <summary>How long a resolved snapshot is served before the store is consulted again.
        /// Also the cross-host convergence bound for saves.</summary>
        public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(15);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<IdentityPolicyOptions> _options;
        private readonly object _refreshLock = new object();

        // One immutable (policy, expiry) pair swapped atomically; readers never lock.
        private volatile CachedRegistrationPolicy _cached;

        /// <summary>DI constructor (the only one).</summary>
        public NodeRegistrationPolicyResolver(
            IServiceScopeFactory scopeFactory, IOptions<IdentityPolicyOptions> options)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _options = options;
        }

        /// <inheritdoc />
        public RegistrationMode Mode => Current.Mode;

        /// <inheritdoc />
        public bool SelfRegistrationEnabled => Current.SelfRegistrationEnabled;

        /// <inheritdoc />
        public bool RequiresAdminApproval => Current.RequiresAdminApproval;

        /// <inheritdoc />
        public bool AutoProvisionJitEnabled => Current.AutoProvisionJitEnabled;

        /// <inheritdoc />
        public bool IsEmailAllowed(string email)
        {
            return Current.IsEmailAllowed(email);
        }

        /// <inheritdoc />
        public void Invalidate()
        {
            _cached = null;
        }

        /// <summary>The configured registration options, i.e. what governs when nothing is stored.
        /// Exposed so the admin page can show the operator what clearing the stored row falls back
        /// to, without re-reading configuration itself.</summary>
        public RegistrationOptions ConfiguredRegistration
        {
            get { return _options?.Value?.Registration ?? new RegistrationOptions(); }
        }

        /// <summary>The current policy: cached while fresh, re-resolved (DB-first) otherwise.</summary>
        private IRegistrationPolicy Current
        {
            get
            {
                CachedRegistrationPolicy cached = _cached;
                if (cached != null && Stopwatch.GetTimestamp() < cached.ExpiresAtTimestamp)
                {
                    return cached.Policy;
                }

                lock (_refreshLock)
                {
                    cached = _cached;
                    if (cached != null && Stopwatch.GetTimestamp() < cached.ExpiresAtTimestamp)
                    {
                        return cached.Policy;
                    }

                    IRegistrationPolicy resolved = new RegistrationPolicy(
                        Options.Create(new IdentityPolicyOptions() { Registration = ResolveNow() }));
                    _cached = new CachedRegistrationPolicy(resolved);
                    return resolved;
                }
            }
        }

        /// <summary>One full DB-first resolution pass (see the class doc for the three rules).</summary>
        private RegistrationOptions ResolveNow()
        {
            StoredRegistrationPolicy stored;
            try
            {
                using (IServiceScope scope = _scopeFactory.CreateScope())
                {
                    INodeRegistrationSettingsLogic settings =
                        scope.ServiceProvider.GetService<INodeRegistrationSettingsLogic>();
                    if (settings == null)
                    {
                        // A missing collaborator is a WIRING FAULT, not an absent row. Falling back
                        // to configuration here would be the fail-open this class exists to avoid.
                        Febris.SharedServices.FebrisLog.Warn(
                            "[registration-policy] INodeRegistrationSettingsLogic is not registered; resolving AdminOnly");
                        return Closed();
                    }

                    // Sync-over-async is confined to this per-TTL refresh (a single-row read in a
                    // fresh scope, no ambient SynchronizationContext in either host) -- the same
                    // containment the federation resolver documents.
                    stored = settings.GetStored().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                // Deliberately NOT quiet, unlike the federation resolver: there, an unreadable
                // store is the ordinary state of a hub-less node. Here it is a fault, and it has
                // just closed registration, so it should be visible in the log.
                Febris.SharedServices.FebrisLog.Error(ex,
                    "[registration-policy] stored policy unreadable; resolving AdminOnly (fail closed)");
                return Closed();
            }

            if (stored == null || !stored.HasStoredSettings)
            {
                // Never saved: configuration governs, exactly as before this feature existed.
                return ConfiguredRegistration;
            }

            return FromStored(stored);
        }

        /// <summary>Map a stored snapshot onto registration options, applying both fail-closed
        /// reductions: an unparseable mode name, and an elapsed open window.</summary>
        private RegistrationOptions FromStored(StoredRegistrationPolicy stored)
        {
            RegistrationMode mode;
            if (!TryParseModeName(stored.Mode, out mode))
            {
                Febris.SharedServices.FebrisLog.Warn(string.Format(
                    "[registration-policy] stored mode \"{0}\" is not a recognized mode name; resolving AdminOnly",
                    stored.Mode));
                return Closed();
            }

            // An expiry only means something for a mode that actually admits self-registration.
            // Applying it to AdminOnly or Invite would silently rewrite a policy the operator did
            // not ask to have rewritten, so those modes ignore it.
            if (IsSelfRegistering(mode)
                && stored.OpenUntilUtc.HasValue
                && stored.OpenUntilUtc.Value <= DateTime.UtcNow)
            {
                return Closed();
            }

            return new RegistrationOptions()
            {
                Mode = mode,
                AllowedEmailDomains = SplitDomains(stored.AllowedEmailDomains),
                RequireAdminApproval = stored.RequireAdminApproval,
                AutoProvisionJit = stored.AutoProvisionJit,

                // Not stored, and not editable from the admin page: it is also applied to ASP.NET
                // Identity's SignIn.RequireConfirmedEmail at startup, so only configuration can
                // move it without the two halves disagreeing.
                RequireConfirmedEmail = ConfiguredRegistration.RequireConfirmedEmail
            };
        }

        /// <summary>
        /// Parse a registration mode from its NAME only, case-insensitively. Numeric input is
        /// REJECTED.
        ///
        /// <para>
        /// This is not the same as <c>Enum.TryParse</c> plus <c>Enum.IsDefined</c>, and the
        /// difference is the reason this helper exists. <c>Enum.TryParse("2", out mode)</c>
        /// succeeds and yields <see cref="RegistrationMode.Open"/>, and <c>IsDefined</c> then says
        /// yes, because 2 genuinely IS a defined value -- so the pair accepts an ordinal. That
        /// defeats the whole reason the mode is stored as a name: an ordinal only means what it
        /// means until someone inserts a member into the enum, at which point a stored "2" silently
        /// becomes a different policy. IsDefined still earns its place for the undefined-number
        /// case ("17"), but on its own it is not enough. A test asserting a stored "2" resolves
        /// AdminOnly is what surfaced this.
        /// </para>
        /// </summary>
        public static bool TryParseModeName(string name, out RegistrationMode mode)
        {
            mode = RegistrationMode.AdminOnly;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string trimmed = name.Trim();
            foreach (string candidate in Enum.GetNames(typeof(RegistrationMode)))
            {
                if (string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    mode = (RegistrationMode)Enum.Parse(typeof(RegistrationMode), candidate);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a mode admits unauthenticated self-registration. Mirrors
        /// <see cref="RegistrationPolicy.SelfRegistrationEnabled"/>; kept here only to decide
        /// whether an expiry is meaningful, never to make an admission decision.</summary>
        public static bool IsSelfRegistering(RegistrationMode mode)
        {
            return mode == RegistrationMode.Open || mode == RegistrationMode.DomainAllowlist;
        }

        /// <summary>The fail-closed answer: admin-only, no domains, no JIT provisioning. Admin
        /// approval is irrelevant under AdminOnly (nothing self-registers) and is left at its
        /// default rather than inherited from a value we may have failed to read.</summary>
        private static RegistrationOptions Closed()
        {
            return new RegistrationOptions() { Mode = RegistrationMode.AdminOnly };
        }

        /// <summary>Expand the stored comma-separated domain list back into the array shape
        /// <see cref="RegistrationOptions.AllowedEmailDomains"/> expects.</summary>
        public static string[] SplitDomains(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
            {
                return Array.Empty<string>();
            }
            return stored.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Immutable cache entry (resolved policy + monotonic expiry). Named
        /// <c>CachedRegistrationPolicy</c> rather than the obvious <c>CachedSnapshot</c> because
        /// <c>HubFederationSettingsResolver</c> already has a private nested <c>CachedSnapshot</c>,
        /// and the duplicate-type ratchet counts nested types across projects -- it caught this.
        /// Renaming is the right fix; the baseline is a list that may only shrink.
        /// </summary>
        private sealed class CachedRegistrationPolicy
        {
            public CachedRegistrationPolicy(IRegistrationPolicy policy)
            {
                Policy = policy;
                ExpiresAtTimestamp = Stopwatch.GetTimestamp()
                    + (long)(CacheTtl.TotalSeconds * Stopwatch.Frequency);
            }

            public IRegistrationPolicy Policy { get; }
            public long ExpiresAtTimestamp { get; }
        }
    }
}
