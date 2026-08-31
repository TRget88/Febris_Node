// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.HealthLogic;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.FederationLogic
{
    /// <summary>
    /// Admin surface behind the portal's Hub Federation page (owner-ratified
    /// 2026-07-17: the OPERATOR owns federation -- the license key is a marketplace membership
    /// credential, never an operate requirement, and opt-in happens here, on the node portal).
    /// Read, save, and probe the node's stored federation settings.
    /// </summary>
    public interface IHubFederationSettingsLogic
    {
        /// <summary>The page model: stored settings (license key MASKED -- last four characters
        /// only, never the full key) plus the effective gate state the resolver currently serves.</summary>
        Task<HubFederationSettingsViewModel> GetSettings();

        /// <summary>
        /// Persist the operator's settings. Write-only key semantics: blank keeps the stored
        /// key, a value replaces it, <c>ClearLicenseKey</c> removes it. Invalidates the gate
        /// resolver's cached snapshot so THIS host applies the save immediately (the sibling
        /// host converges within the resolver TTL through the shared DataDb). Returns the
        /// refreshed page model.
        /// </summary>
        Task<HubFederationSettingsViewModel> SaveSettings(HubFederationSettingsInputModel input);

        /// <summary>Run the gate-aware hub reachability probe (<see cref="HubFederationHealthCheck"/>)
        /// against the settings the resolver currently serves -- i.e. the SAVED settings.</summary>
        Task<HubProbeResultViewModel> TestConnection();
    }

    /// <summary>
    /// DI-only implementation of <see cref="IHubFederationSettingsLogic"/>:
    /// greenfield node code, deliberately NO legacy self-newing constructor. The probe is the
    /// SAME <see cref="HubFederationHealthCheck"/> the readiness endpoint runs, composed here
    /// from the injected gate + client factory (the health system creates its own instances, so
    /// the check type is not a DI service).
    /// </summary>
    public class HubFederationSettingsLogic : IHubFederationSettingsLogic
    {
        private readonly IHubFederationConfigQueries _configContext;
        private readonly IHubFederationSettings _federation;
        private readonly IHubFederationSettingsCache _federationCache;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>DI constructor (the only one).</summary>
        public HubFederationSettingsLogic(
            IHubFederationConfigQueries configContext,
            IHubFederationSettings federation,
            IHubFederationSettingsCache federationCache,
            IHttpClientFactory httpClientFactory)
        {
            _configContext = configContext;
            _federation = federation;
            _federationCache = federationCache;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public async Task<HubFederationSettingsViewModel> GetSettings()
        {
            HubFederationConfig row = await _configContext.Get();
            return ToViewModel(row);
        }

        /// <inheritdoc />
        public async Task<HubFederationSettingsViewModel> SaveSettings(HubFederationSettingsInputModel input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            HubFederationConfig existing = await _configContext.Get();

            // Write-only key merge: the form never round-trips the stored key, so a blank field
            // means KEEP (the operator edited endpoints, not the credential); an explicit clear
            // beats everything.
            string licenseKey;
            if (input.ClearLicenseKey)
            {
                licenseKey = null;
            }
            else if (!string.IsNullOrWhiteSpace(input.LicenseKey))
            {
                licenseKey = input.LicenseKey.Trim();
            }
            else
            {
                licenseKey = existing?.LicenseKey;
            }

            HubFederationConfig saved = await _configContext.Save(new HubFederationConfig()
            {
                Enabled = input.Enabled,
                DataApi = NormalizeUrl(input.DataApi),
                AuthenticationApi = NormalizeUrl(input.AuthenticationApi),
                LicenseKey = licenseKey
            });

            // The stored row now governs; drop the resolver's snapshot so the very next gate
            // consultation on this host sees it.
            _federationCache?.Invalidate();

            return ToViewModel(saved);
        }

        /// <inheritdoc />
        public async Task<HubProbeResultViewModel> TestConnection()
        {
            HubFederationHealthCheck probe = new HubFederationHealthCheck(_federation, _httpClientFactory);
            HealthCheckResult result = await probe.CheckHealthAsync(new HealthCheckContext());
            return new HubProbeResultViewModel()
            {
                Status = result.Status.ToString(),
                Description = result.Description,
                ProbedAtUtc = DateTime.UtcNow
            };
        }

        /// <summary>Map a stored row (or its absence) onto the page model. The key is reduced to
        /// its masked display form HERE, at the seam -- the full key never leaves the logic layer.</summary>
        private HubFederationSettingsViewModel ToViewModel(HubFederationConfig row)
        {
            return new HubFederationSettingsViewModel()
            {
                HasStoredSettings = row != null,
                Enabled = row?.Enabled ?? false,
                DataApi = row?.DataApi,
                AuthenticationApi = row?.AuthenticationApi,
                LicenseKeyMasked = MaskLicenseKey(row?.LicenseKey),
                HasLicenseKey = !string.IsNullOrWhiteSpace(row?.LicenseKey),
                UpdatedAtUtc = row?.UpdatedAt,
                EffectiveEnabled = _federation?.Enabled ?? false
            };
        }

        /// <summary>Masked display form: the LAST FOUR characters behind a fixed-width prefix
        /// ("****abcd"). The fixed prefix leaks nothing about the key's length; keys of four
        /// characters or fewer mask completely.</summary>
        public static string MaskLicenseKey(string plaintextKey)
        {
            if (string.IsNullOrWhiteSpace(plaintextKey))
            {
                return null;
            }
            if (plaintextKey.Length <= 4)
            {
                return "****";
            }
            return "****" + plaintextKey.Substring(plaintextKey.Length - 4);
        }

        /// <summary>Trim; empty becomes null (absent), matching the gate's whitespace checks.</summary>
        private static string NormalizeUrl(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
