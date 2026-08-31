// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.UserModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.SharedServices;
using Febris.SharedServices.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Linq;

namespace Febris.UserNode.LogicLayer.Logic.HealthLogic
{
    /// <summary>
    /// Node health site: the ONE health registration entry point for both
    /// EndUser hosts, called at the END of <c>Startup.ConfigureServices</c> as
    /// <c>services.AddNodeHealthChecks(Configuration)</c> (after the DAL/storage/cache
    /// registrations -- it inspects the service collection to see what this host owns), plus the
    /// endpoint mapper <see cref="MapNodeHealthEndpoints"/> for <c>Startup.Configure</c>.
    /// <para>
    /// Registration is OWNERSHIP-DRIVEN, mirroring the conditional shape of
    /// <c>AddFebrisUserNodeDataAccess</c>: a check is added only when this host both
    /// registered the probed dependency in DI and (for databases/Redis) carries its connection
    /// string. A missing dependency means NO check -- absence of an optional subsystem never
    /// reads as unhealthy. The hub check is the one always-on check, because it is gate-aware
    /// internally (closed gate =&gt; Healthy "hub federation disabled").
    /// </para>
    /// <para>
    /// Built-in <c>Microsoft.Extensions.Diagnostics.HealthChecks</c> only (shared framework) --
    /// no new external packages. Lives in the EndUser BLL (which may reference the DAL and
    /// already carries the AspNetCore framework reference) so the two hosts share one
    /// implementation; SharedServices cannot host this (it must not reference the DAL).
    /// </para>
    /// </summary>
    public static class NodeHealthRegistration
    {
        /// <summary>Check name: the Identity/user database (Portal-owned).</summary>
        public const string DatabaseUserCheckName = "database-user";
        /// <summary>Check name: the tenant Data database.</summary>
        public const string DatabaseDataCheckName = "database-data";
        /// <summary>Check name: the tenant xAPI database.</summary>
        public const string DatabaseXApiCheckName = "database-xapi";
        /// <summary>Check name: the tenant Analytics database.</summary>
        public const string DatabaseAnalyticsCheckName = "database-analytics";

        /// <summary>Schema readiness for the three migration-managed databases (T11).</summary>
        public const string SchemaUserCheckName = "schema-user";
        public const string SchemaDataCheckName = "schema-data";
        public const string SchemaXApiCheckName = "schema-xapi";
        /// <summary>Check name: the artifact-store round-trip.</summary>
        public const string StorageCheckName = "storage";
        /// <summary>Check name: the hardware-token Redis cache.</summary>
        public const string RedisHardwareCheckName = "redis-hardware";
        /// <summary>Check name: the auth-ticket Redis cache.</summary>
        public const string RedisAuthCheckName = "redis-auth";
        /// <summary>Check name: the gate-aware hub reachability probe.</summary>
        public const string HubFederationCheckName = "hub-federation";
        /// <summary>Check name: the required Identity roles exist (account provisioning prerequisite).</summary>
        public const string IdentityRolesCheckName = "identity-roles";

        /// <summary>
        /// Register the node's readiness checks for whatever THIS host owns (see class doc).
        /// Call after every database/storage/cache registration in ConfigureServices.
        /// </summary>
        public static IServiceCollection AddNodeHealthChecks(this IServiceCollection services, IConfiguration configuration)
        {
            IHealthChecksBuilder builder = services.AddHealthChecks();

            // Databases: one check per DbContext the HOST registered. The DI descriptor is the
            // ownership signal (AddFebrisUserNodeDataAccess itself only registers a context
            // whose connection string is configured; the Identity context is host-wired), and the
            // connection string is double-checked so a context registered against a blank string
            // cannot produce a permanently-red check.
            AddDatabaseCheck<ApplicationDbContext>(builder, services, configuration, "UserDBConnection", DatabaseUserCheckName);
            AddDatabaseCheck<DataDbContext>(builder, services, configuration, "DataDBConnection", DatabaseDataCheckName);
            AddDatabaseCheck<XApiDbContext>(builder, services, configuration, "XAPIDBConnection", DatabaseXApiCheckName);
            AddDatabaseCheck<AnalyticsDbContext>(builder, services, configuration, "AnalyticsDBConnection", DatabaseAnalyticsCheckName);

            // T11. Connectivity is not readiness. CanConnectAsync answers "reachable" for a
            // connectable database with ZERO tables, so a node whose migrations failed reported
            // green, the compose healthcheck passed, and depends_on released the proxy onto a node
            // that could not serve a request. Pending migrations are the honest signal, and they
            // catch both the never-migrated and the half-migrated case.
            //
            // AnalyticsDbContext is deliberately EXCLUDED: it is provisioned with EnsureCreated(),
            // which writes no __EFMigrationsHistory, yet it owns a migration chain, so it would
            // report every migration pending forever and pin readiness red on a healthy node. That
            // mismatch is a real defect, recorded separately rather than papered over here.
            AddSchemaCheck<ApplicationDbContext>(builder, services, configuration, "UserDBConnection", SchemaUserCheckName);
            AddSchemaCheck<DataDbContext>(builder, services, configuration, "DataDBConnection", SchemaDataCheckName);
            AddSchemaCheck<XApiDbContext>(builder, services, configuration, "XAPIDBConnection", SchemaXApiCheckName);

            // Storage: both EndUser hosts call AddFebrisStorage, but stay descriptor-gated so a
            // host composition without the seam simply has no storage check.
            if (IsRegistered(services, typeof(IStorageProvider)))
            {
                builder.AddCheck<StorageProviderHealthCheck>(StorageCheckName, failureStatus: HealthStatus.Unhealthy);
            }

            // Redis: OPTIONAL. A check exists only for a configured
            // connection whose cache abstraction this host registered -- probed through that
            // same abstraction, never a raw connection.
            IConfigurationSection redisSection = configuration.GetSection("RedisConnectionStrings");
            if (!string.IsNullOrWhiteSpace(redisSection.GetValue<string>("HardwareConnection"))
                && IsRegistered(services, typeof(IDistributedHardwareCache)))
            {
                builder.AddCheck<DistributedCacheHealthCheck<IDistributedHardwareCache>>(
                    RedisHardwareCheckName, failureStatus: HealthStatus.Unhealthy);
            }
            if (!string.IsNullOrWhiteSpace(redisSection.GetValue<string>("AuthConnection"))
                && IsRegistered(services, typeof(IDistributedUserCache)))
            {
                builder.AddCheck<DistributedCacheHealthCheck<IDistributedUserCache>>(
                    RedisAuthCheckName, failureStatus: HealthStatus.Unhealthy);
            }

            // Identity roles: a hard account-provisioning prerequisite (UserManager.AddToRoleAsync THROWS
            // when a role is absent). Ownership-gated on RoleManager -- registered only by the host that
            // wired Identity (the Portal via AddIdentity), never the API. Boot seeding fails startup when
            // roles are missing; this probe covers the runtime window (a role dropped after boot).
            if (IsRegistered(services, typeof(RoleManager<ApplicationRole>)))
            {
                builder.AddCheck<IdentityRolesHealthCheck>(IdentityRolesCheckName, failureStatus: HealthStatus.Unhealthy);
            }

            // Hub: ALWAYS registered -- the check consults the federation gate itself, and a
            // closed gate reports Healthy ("hub federation disabled"), so a standalone node is
            // never degraded by having no hub. AddHttpClient is the built-in factory registration
            // (idempotent) for the probe's pooled handler.
            services.AddHttpClient();
            builder.AddCheck<HubFederationHealthCheck>(HubFederationCheckName, failureStatus: HealthStatus.Degraded);

            return services;
        }

        /// <summary>
        /// Configuration key controlling whether the probe bodies enumerate the individual checks.
        /// Unset means "detailed on a Development host, terse everywhere else", which is the safe
        /// default for a deployment nobody has configured.
        /// </summary>
        public const string DetailedResponseKey = "HealthChecks:DetailedResponse";

        /// <summary>
        /// Resolve the probe detail level for a host: the explicit
        /// <see cref="DetailedResponseKey"/> setting when the operator has expressed one, otherwise
        /// <paramref name="isDevelopment"/>.
        /// <para>
        /// Deliberately a nullable read rather than <c>GetValue&lt;bool&gt;</c>. The latter cannot
        /// distinguish "operator set false" from "operator set nothing", which would make the
        /// Development default unreachable and silently degrade the dev experience the owner
        /// actually uses.
        /// </para>
        /// </summary>
        public static bool ResolveDetailedResponse(IConfiguration configuration, bool isDevelopment)
        {
            bool? configured = configuration?.GetValue<bool?>(DetailedResponseKey);
            return configured ?? isDevelopment;
        }

        /// <summary>
        /// Map the node's machine probe endpoints (Docker/K8s):
        /// <c>/health/live</c> -- process liveness, NO checks (a wedged dependency must not make
        /// the orchestrator restart a serving process), and <c>/health/ready</c> -- every
        /// registered check. Both anonymous (probes cannot authenticate) and both emitting the
        /// secret-free JSON of <see cref="NodeHealthResponseWriter"/>.
        /// <para>
        /// <paramref name="detailedResponse"/> false (the default off a Development host) omits the
        /// per-check array, so an anonymous caller cannot enumerate this node's dependencies. See
        /// <see cref="NodeHealthResponseWriter"/> for why that gate belongs here rather than in the
        /// bundled proxy config.
        /// </para>
        /// </summary>
        public static IEndpointRouteBuilder MapNodeHealthEndpoints(
            this IEndpointRouteBuilder endpoints,
            bool detailedResponse)
        {
            endpoints.MapHealthChecks("/health/live", new HealthCheckOptions()
            {
                Predicate = _ => false,   // liveness = the process answers; no dependency checks
                ResponseWriter = (context, report) =>
                    NodeHealthResponseWriter.WriteAsync(context, report, detailedResponse)
            }).AllowAnonymous();

            endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions()
            {
                ResponseWriter = (context, report) =>
                    NodeHealthResponseWriter.WriteAsync(context, report, detailedResponse)
            }).AllowAnonymous();

            return endpoints;
        }

        /// <summary>Add the connectivity check for <typeparamref name="TContext"/> when this host
        /// owns it: DI registration present AND the connection string configured.</summary>
        private static void AddDatabaseCheck<TContext>(
            IHealthChecksBuilder builder,
            IServiceCollection services,
            IConfiguration configuration,
            string connectionKey,
            string checkName)
            where TContext : DbContext
        {
            if (!IsRegistered(services, typeof(TContext)))
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(connectionKey)))
            {
                return;
            }

            builder.AddCheck<DbContextHealthCheck<TContext>>(checkName, failureStatus: HealthStatus.Unhealthy);
        }

        /// <summary>
        /// Same ownership gate as <c>AddDatabaseCheck</c>, for the schema probe. Only ever called for
        /// migration-managed contexts.
        /// </summary>
        private static void AddSchemaCheck<TContext>(
            IHealthChecksBuilder builder,
            IServiceCollection services,
            IConfiguration configuration,
            string connectionKey,
            string checkName)
            where TContext : DbContext
        {
            if (!IsRegistered(services, typeof(TContext)))
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(connectionKey)))
            {
                return;
            }

            builder.AddCheck<DbContextSchemaHealthCheck<TContext>>(checkName, failureStatus: HealthStatus.Unhealthy);
        }

        /// <summary>True when the collection carries a registration for <paramref name="serviceType"/>.</summary>
        private static bool IsRegistered(IServiceCollection services, Type serviceType)
        {
            return services.Any(descriptor => descriptor.ServiceType == serviceType);
        }
    }
}
