// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.SharedServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;

namespace Febris.UserNode.DataAccessLayer
{
    /// <summary>
    /// DB-first resolution for the ONE hub-federation gate (owner-ratified 2026-07-17
    /// "the operator owns federation"). Replaces the boot-time
    /// <c>HubFederationSettings.Resolve(config)</c> singleton VALUE with a singleton RESOLVER:
    ///
    /// <list type="number">
    /// <item>When the tenant DataDb carries a <see cref="HubFederationConfig"/> row (the portal's
    /// Hub Federation page was saved at least once), that row GOVERNS -- the operator's stored
    /// answer beats whatever the deployment's config files say.</item>
    /// <item>Otherwise the existing configuration resolution governs unchanged
    /// (<c>HubFederation</c> section, legacy <c>ApiUrlPath</c>+<c>LicenseKey</c> back-compat) --
    /// deployments that never touch the portal page behave exactly as before.</item>
    /// </list>
    ///
    /// <para>
    /// CACHING (the documented choice): a short-TTL cached snapshot
    /// (<see cref="CacheTtl"/> = 15s) on a singleton, NOT a per-request scoped resolver. The gate
    /// is consulted by 27 remote query classes plus TokenQueries on hot request paths, and several
    /// consumers (the health check, this resolver's own registration) are singletons -- a scoped
    /// registration would be a captive-dependency trap there, while the TTL turns the cost into
    /// at most one single-row read per host per 15s. The portal save path calls
    /// <see cref="Invalidate"/> (via <see cref="IHubFederationSettingsCache"/>) so its own host
    /// applies a save immediately; the OTHER EndUser host converges within the TTL through the
    /// shared tenant DataDb.
    /// </para>
    ///
    /// <para>
    /// Placeholder hardening (MED-6 family): BOTH sources pass through
    /// <see cref="JwtSigningKeyProvider.IsUnsubstitutedTemplate"/> -- an unsubstituted
    /// <c>{Token}</c> endpoint/key resolves the gate DISABLED instead of open-against-garbage.
    /// </para>
    ///
    /// <para>
    /// Failure posture: ANY database problem (no DataDb registered, store not yet migrated,
    /// connection refused) quietly falls back to the configuration resolution -- the gate must
    /// never take a node down. DB reads happen in a fresh DI scope per refresh (a singleton
    /// cannot hold the scoped DbContext); no static state.
    /// </para>
    /// </summary>
    public sealed class HubFederationSettingsResolver : IHubFederationSettings, IHubFederationSettingsCache
    {
        /// <summary>How long a resolved snapshot is served before the store is consulted again.
        /// Also the cross-host convergence bound for portal saves.</summary>
        public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(15);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly object _refreshLock = new object();

        // One immutable (snapshot, expiry) pair swapped atomically; readers never lock.
        private volatile CachedSnapshot _cached;

        /// <summary>DI constructor (the only one).</summary>
        public HubFederationSettingsResolver(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _configuration = configuration;
        }

        /// <inheritdoc />
        public bool Enabled => Current.Enabled;

        /// <inheritdoc />
        public string DataApi => Current.DataApi;

        /// <inheritdoc />
        public string AuthenticationApi => Current.AuthenticationApi;

        /// <inheritdoc />
        public string LicenseKey => Current.LicenseKey;

        /// <inheritdoc />
        public bool CanReachDataApi => Current.CanReachDataApi;

        /// <inheritdoc />
        public bool CanReachAuthenticationApi => Current.CanReachAuthenticationApi;

        /// <inheritdoc />
        public bool HasLicenseKey => Current.HasLicenseKey;

        /// <inheritdoc />
        public void Invalidate()
        {
            _cached = null;
        }

        /// <summary>The current snapshot: cached while fresh, re-resolved (DB-first) otherwise.</summary>
        private HubFederationSettings Current
        {
            get
            {
                CachedSnapshot cached = _cached;
                if (cached != null && Stopwatch.GetTimestamp() < cached.ExpiresAtTimestamp)
                {
                    return cached.Settings;
                }

                lock (_refreshLock)
                {
                    cached = _cached;
                    if (cached != null && Stopwatch.GetTimestamp() < cached.ExpiresAtTimestamp)
                    {
                        return cached.Settings;
                    }

                    HubFederationSettings resolved = ResolveNow();
                    _cached = new CachedSnapshot(resolved);
                    return resolved;
                }
            }
        }

        /// <summary>One full DB-first resolution pass (see class doc for the rules).</summary>
        private HubFederationSettings ResolveNow()
        {
            HubFederationConfig row = TryReadRow();
            if (row != null)
            {
                return FromRow(row);
            }
            return HubFederationSettings.Resolve(_configuration, JwtSigningKeyProvider.IsUnsubstitutedTemplate);
        }

        /// <summary>The stored row (license key already decrypted by the queries), or null on any
        /// miss or failure -- an unreachable/unmigrated store must degrade to config, not throw.</summary>
        private HubFederationConfig TryReadRow()
        {
            try
            {
                using (IServiceScope scope = _scopeFactory.CreateScope())
                {
                    Queries.DataQueries.IHubFederationConfigQueries queries =
                        scope.ServiceProvider.GetService<Queries.DataQueries.IHubFederationConfigQueries>();
                    if (queries == null)
                    {
                        return null;
                    }
                    // Sync-over-async is confined to this per-TTL refresh (a single-row read in a
                    // fresh scope, no ambient SynchronizationContext in either host).
                    return queries.Get().GetAwaiter().GetResult();
                }
            }
            catch
            {
                // Deliberately quiet (mirrors the gate-off discipline): a hub-less or
                // not-yet-migrated node must not log an error per TTL window.
                return null;
            }
        }

        /// <summary>Map the stored row onto gate settings, with the same placeholder scrub the
        /// config path gets (a `{Token}` pasted into the portal must fail closed too).</summary>
        private static HubFederationSettings FromRow(HubFederationConfig row)
        {
            HubFederationSettings settings = new HubFederationSettings()
            {
                Enabled = row.Enabled,
                DataApi = Scrub(row.DataApi),
                AuthenticationApi = Scrub(row.AuthenticationApi),
                LicenseKey = Scrub(row.LicenseKey)
            };
            if (settings.Enabled
                && string.IsNullOrWhiteSpace(settings.DataApi)
                && string.IsNullOrWhiteSpace(settings.AuthenticationApi))
            {
                settings.Enabled = false;
            }
            return settings;
        }

        private static string Scrub(string value)
        {
            return JwtSigningKeyProvider.IsUnsubstitutedTemplate(value) ? null : value;
        }

        /// <summary>Immutable cache entry (settings + monotonic expiry).</summary>
        private sealed class CachedSnapshot
        {
            public CachedSnapshot(HubFederationSettings settings)
            {
                Settings = settings;
                ExpiresAtTimestamp = Stopwatch.GetTimestamp()
                    + (long)(CacheTtl.TotalSeconds * Stopwatch.Frequency);
            }

            public HubFederationSettings Settings { get; }
            public long ExpiresAtTimestamp { get; }
        }
    }
}
