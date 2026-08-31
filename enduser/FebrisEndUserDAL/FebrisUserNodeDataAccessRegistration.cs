// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using System.Reflection;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Febris.UserNode.DataAccessLayer
{
    /// <summary>
    /// DI refactor: the EndUser ("primary tenant") counterpart of the shared
    /// <c>AddFebrisDataAccess</c>. Intended to be called once per EndUser host as
    /// <c>services.AddFebrisUserNodeDataAccess(Configuration)</c>. It registers the three
    /// per-tenant <see cref="DbContext"/> types (Data, XApi, Analytics) as scoped from this
    /// deployment's IConfiguration connection strings, and auto-registers every
    /// <c>IXxxQueries</c> to its <c>XxxQueries</c> implementation as scoped by naming convention
    /// so the strangler DI constructors on the query classes resolve.
    /// <para>
    /// This is NOT a blind mirror of <c>AddFebrisDataAccess</c>. The EndUser DAL queries split into:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <b>Local</b> queries are backed by the per-tenant DataDbContext / XApiDbContext /
    /// AnalyticsDbContext registered here, so their strangler DI ctor injects the scoped context.
    /// </item>
    /// <item>
    /// <b>Remote</b> queries make HTTP calls to the central Febris API through APIRequestFactory and
    /// have NO DbContext. They keep resolving through their existing constructor (which still news
    /// APIRequestFactory) until the IHttpClientFactory work (SSO-B7 / SCBA-B9) injects a typed
    /// client. That is an additive follow-up, not a precondition for this registration.
    /// </item>
    /// </list>
    /// <para>
    /// Auth-island boundary: this registers ONLY the tenant's own databases (Local) plus the
    /// reflection map. It never registers a direct central-database context. Central data is reached
    /// only via the Remote queries' HTTP path. The Identity <c>ApplicationDbContext</c> is owned by
    /// the host's <c>AddIdentity</c> wiring and is intentionally NOT registered here.
    /// </para>
    /// <para>
    /// Activation is gated. This helper is additive and safe to build and unit-test, but a host only
    /// benefits once its <c>Startup.ConfigureServices</c> calls it AND a per-host DI-resolution test
    /// confirms startup. Wiring it into the EndUser hosts is the runtime-verified step that follows.
    ///
    /// </para>
    /// </summary>
    public static class FebrisUserNodeDataAccessRegistration
    {
        public static IServiceCollection AddFebrisUserNodeDataAccess(this IServiceCollection services, IConfiguration config)
        {
            // The ONE hub-federation gate (): resolution is now
            // DB-FIRST -- a stored HubFederationConfig row (the portal's Hub Federation page)
            // governs when present; otherwise the configuration resolution ("HubFederation"
            // section, legacy ApiUrlPath+LicenseKey back-compat) governs unchanged. The resolver
            // is a singleton with a short-TTL cached snapshot (choice documented on the class:
            // 27 query classes consult the gate per request, and several consumers are
            // singletons, so scoped resolution would be both hot and a captive-dependency trap).
            // Unsubstituted {Token} deploy placeholders now resolve the gate DISABLED (MED-6
            // family). Default CLOSED as ever: no row + no config = local-only, zero hub
            // credentials. TryAdd keeps an explicit host/test registration in charge.
            services.TryAddSingleton<HubFederationSettingsResolver>(provider =>
                new HubFederationSettingsResolver(provider.GetRequiredService<IServiceScopeFactory>(), config));
            services.TryAddSingleton<IHubFederationSettings>(provider =>
                provider.GetRequiredService<HubFederationSettingsResolver>());
            services.TryAddSingleton<IHubFederationSettingsCache>(provider =>
                provider.GetRequiredService<HubFederationSettingsResolver>());

            // Per-tenant DbContexts: scoped, connection string from this deployment's IConfiguration.
            // Only registered when the key is present, so a deployment missing a database key keeps
            // resolving the affected Local queries through the static ops fallback (strangler-safe).
            AddContext<DataDbContext>(services, config, "DataDBConnection");
            AddContext<XApiDbContext>(services, config, "XAPIDBConnection");
            AddContext<AnalyticsDbContext>(services, config, "AnalyticsDBConnection");

            // Auto-register IXxxQueries -> XxxQueries (scoped) by naming convention over the EndUser
            // DAL assembly, so new query classes are picked up without editing this file. The
            // strangler DI constructor on each Local query injects its per-tenant DbContext; Remote
            // queries resolve through their existing HTTP constructor. Until a query class is
            // converted it still resolves through its existing parameterless constructor, so this is
            // additive and safe to call before the conversion lands. TryAddScoped leaves any explicit
            // host registration in place.
            var dalAssembly = typeof(DataDbContext).Assembly;
            Type[] dalTypes;
            try
            {
                dalTypes = dalAssembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Some EndUser DAL types reference MVC/view dependencies that may not fully load in
                // every host. Register the types that did load and skip the rest rather than throwing.
                dalTypes = ex.Types.Where(t => t != null).ToArray();
            }
            foreach (var implementation in dalTypes)
            {
                if (!implementation.IsClass || implementation.IsAbstract)
                {
                    continue;
                }
                if (!implementation.Name.EndsWith("Queries", StringComparison.Ordinal))
                {
                    continue;
                }
                var contract = implementation.GetInterface("I" + implementation.Name);
                if (contract != null)
                {
                    services.TryAddScoped(contract, implementation);
                }
            }

            return services;
        }

        private static void AddContext<TContext>(IServiceCollection services, IConfiguration config, string connectionKey)
            where TContext : DbContext
        {
            var connectionString = config.GetConnectionString(connectionKey);
            if (!string.IsNullOrEmpty(connectionString))
            {
                services.AddDbContext<TContext>(options => options.UseNpgsql(connectionString));
            }
        }
    }
}
