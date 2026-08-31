// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Febris.UserNode.Portal.LocalUtility
{
    /// <summary>
    /// Task #15 scaffolding (2026-05-20): config-driven external auth provider
    /// registration for the EndUserPortal. An institution that has its own SSO
    /// (Okta, Azure AD, Auth0, Google Workspace, custom OpenID Connect, etc.)
    /// can plug it in by populating <c>appsettings.json</c> -> <c>ExternalAuthProviders</c>
    /// rather than editing Startup.cs.
    /// <para>
    /// The EndUserPortal's Register page is intentionally closed (per the
    /// portal-policy directive); the only way for a new tenant-user account
    /// to be provisioned is either (a) admin invite (see invite-token flow)
    /// or (b) successful sign-in via an enabled external provider whose
    /// emitted identity matches a pre-provisioned user record (or whose
    /// configured policy allows just-in-time provisioning).
    /// </para>
    /// <para>
    /// This file is INFRASTRUCTURE only -- it reads config and binds it to
    /// a strongly-typed options object, and exposes one extension method
    /// (<see cref="ExternalAuthProviderRegistrationExtensions.AddExternalAuthProvidersFromConfig"/>)
    /// that Startup.cs can call to wire each enabled provider into the
    /// authentication pipeline. Per-provider registration calls (AddGoogle,
    /// AddOpenIdConnect, etc.) are gated behind explicit <c>Enabled = true</c>
    /// flags and require their respective NuGet packages to be added:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>Microsoft.AspNetCore.Authentication.Google</c> for Google Workspace</description></item>
    ///   <item><description><c>Microsoft.AspNetCore.Authentication.MicrosoftAccount</c> for Microsoft personal / Azure AD via the legacy v1 endpoints</description></item>
    ///   <item><description><c>Microsoft.AspNetCore.Authentication.OpenIdConnect</c> for Azure AD v2, Okta, Auth0, and generic OIDC providers</description></item>
    ///   <item><description><c>Sustainsys.Saml2.AspNetCore2</c> (or similar) for SAML 2.0 integrations -- not in the base package set</description></item>
    /// </list>
    /// <para>
    /// To enable a provider:
    /// </para>
    /// <list type="number">
    ///   <item><description>Add the NuGet package above to <c>Febris.UserNode.Portal.csproj</c>.</description></item>
    ///   <item><description>Populate the matching block under <c>ExternalAuthProviders</c> in <c>appsettings.json</c> with the institution's ClientId/ClientSecret/Authority/etc.</description></item>
    ///   <item><description>Set <c>"Enabled": true</c> for that block.</description></item>
    ///   <item><description>Uncomment the corresponding <c>builder.AddXxx(...)</c> call in <see cref="ExternalAuthProviderRegistrationExtensions.AddExternalAuthProvidersFromConfig"/>.</description></item>
    /// </list>
    /// </summary>
    public class ExternalAuthProvidersOptions
    {
        public const string SectionName = "ExternalAuthProviders";

        /// <summary>Google Workspace / Google personal account.</summary>
        public GoogleProviderOptions Google { get; set; } = new GoogleProviderOptions();

        /// <summary>Microsoft account (consumer) / Azure AD v1.</summary>
        public MicrosoftProviderOptions Microsoft { get; set; } = new MicrosoftProviderOptions();

        /// <summary>List of generic OpenID Connect providers (Azure AD v2, Okta, Auth0, custom).</summary>
        public List<OpenIdConnectProviderOptions> OpenIdConnect { get; set; } = new List<OpenIdConnectProviderOptions>();
    }

    public class GoogleProviderOptions
    {
        public bool Enabled { get; set; } = false;
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }

    public class MicrosoftProviderOptions
    {
        public bool Enabled { get; set; } = false;
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }

    /// <summary>
    /// Generic OpenID Connect provider config -- covers Azure AD (tenant-specific
    /// or common), Okta, Auth0, Keycloak, ADFS, and any other OIDC-compliant IdP.
    /// </summary>
    public class OpenIdConnectProviderOptions
    {
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Internal scheme name. Must be unique across all enabled providers in
        /// this list. Used as the value of the "provider" parameter on the
        /// ExternalLogin page (e.g., the button's POST sends provider=ad-acme).
        /// </summary>
        public string Scheme { get; set; }

        /// <summary>User-facing label rendered on the Login page button.</summary>
        public string DisplayName { get; set; }

        /// <summary>OIDC discovery base URL (e.g., https://login.microsoftonline.com/{tenant}/v2.0).</summary>
        public string Authority { get; set; }

        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }

    public static class ExternalAuthProviderRegistrationExtensions
    {
        /// <summary>
        /// Reads <c>appsettings.json</c> -> <c>ExternalAuthProviders</c> and
        /// registers each enabled provider with the authentication pipeline.
        /// Call this from Startup.ConfigureServices AFTER <c>AddIdentity(...)</c>
        /// and BEFORE the application cookie configuration.
        /// </summary>
        public static IServiceCollection AddExternalAuthProvidersFromConfig(
            this IServiceCollection services,
            IConfiguration configuration,
            ILogger logger = null)
        {
            var options = new ExternalAuthProvidersOptions();
            configuration.GetSection(ExternalAuthProvidersOptions.SectionName).Bind(options);
            services.AddSingleton(options);

            var builder = services.AddAuthentication();
            int enabledCount = 0;

            // -----------------------------------------------------------------
            // Google -- requires NuGet: Microsoft.AspNetCore.Authentication.Google
            // -----------------------------------------------------------------
            if (options.Google != null && options.Google.Enabled)
            {
                logger?.LogInformation("ExternalAuth: Google provider enabled");
                // UNCOMMENT after adding the Google NuGet package:
                //
                // builder.AddGoogle(google =>
                // {
                //     google.ClientId = options.Google.ClientId;
                //     google.ClientSecret = options.Google.ClientSecret;
                // });
                enabledCount++;
            }

            // -----------------------------------------------------------------
            // Microsoft account / Azure AD v1
            //   requires NuGet: Microsoft.AspNetCore.Authentication.MicrosoftAccount
            // -----------------------------------------------------------------
            if (options.Microsoft != null && options.Microsoft.Enabled)
            {
                logger?.LogInformation("ExternalAuth: Microsoft provider enabled");
                // UNCOMMENT after adding the MicrosoftAccount NuGet package:
                //
                // builder.AddMicrosoftAccount(ms =>
                // {
                //     ms.ClientId = options.Microsoft.ClientId;
                //     ms.ClientSecret = options.Microsoft.ClientSecret;
                // });
                enabledCount++;
            }

            // -----------------------------------------------------------------
            // OpenID Connect providers (Azure AD v2 / Okta / Auth0 / generic)
            //   requires NuGet: Microsoft.AspNetCore.Authentication.OpenIdConnect
            // -----------------------------------------------------------------
            if (options.OpenIdConnect != null)
            {
                foreach (var oidc in options.OpenIdConnect)
                {
                    if (!oidc.Enabled) continue;
                    if (string.IsNullOrWhiteSpace(oidc.Scheme))
                    {
                        logger?.LogWarning("ExternalAuth: OpenIdConnect entry skipped -- Scheme is required");
                        continue;
                    }

                    logger?.LogInformation("ExternalAuth: OpenIdConnect provider enabled (scheme={Scheme}, displayName={DisplayName})", oidc.Scheme, oidc.DisplayName);
                    // UNCOMMENT after adding the OpenIdConnect NuGet package:
                    //
                    // builder.AddOpenIdConnect(oidc.Scheme, oidc.DisplayName, opts =>
                    // {
                    //     opts.Authority = oidc.Authority;
                    //     opts.ClientId = oidc.ClientId;
                    //     opts.ClientSecret = oidc.ClientSecret;
                    //     opts.ResponseType = "code";
                    //     opts.SaveTokens = true;
                    //     opts.GetClaimsFromUserInfoEndpoint = true;
                    //     opts.Scope.Add("openid");
                    //     opts.Scope.Add("profile");
                    //     opts.Scope.Add("email");
                    // });
                    enabledCount++;
                }
            }

            if (enabledCount == 0)
            {
                logger?.LogInformation("ExternalAuth: no providers enabled (ExternalAuthProviders section missing or all .Enabled=false)");
            }

            return services;
        }
    }
}
