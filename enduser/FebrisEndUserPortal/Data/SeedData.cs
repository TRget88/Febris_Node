// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.UserModels;
using Febris.UserNode.LogicLayer.Logic.HealthLogic;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.Portal.Data
{
    /// <summary>
    /// One-time startup seed for this tenant portal: ensures the Identity roles exist and that the
    /// single bootstrap admin (the configured NODE_ADMIN_EMAIL) is present. That account is seeded as
    /// ITAdmin, the node's top LOCAL role -- SuperAdmin is a FebrisUserType vendor role and the node
    /// stopped minting it on the 2026-08-01 owner ruling. Idempotent -- the account
    /// is created only when absent, so every later boot is a cheap existence check that no-ops.
    /// That existence check IS the "run once" guarantee (and it self-heals if the row is ever lost),
    /// so no migration or separate "has it run" flag is needed.
    ///
    /// Invoked from Program.Main, awaited, BEFORE the host serves traffic, so seeding completes and
    /// any failure surfaces in the log rather than running fire-and-forget mid-pipeline.
    ///
    /// Production password (option A): the bootstrap admin is created with NO password. The operator
    /// sets the initial password through the portal's Forgot Password page -- the account is
    /// email-confirmed, so a reset link is issued immediately and Febris never sets or stores a
    /// production password. DEBUG/STAGING keep a known dev password for local convenience.
    ///
    /// Self-host clone-and-run: the bootstrap identity is now
    /// operator-configurable through the "NodeBootstrap" section (see
    /// <see cref="NodeBootstrapAdminOptions"/>) -- a self-hosted node has no SMTP on first boot,
    /// so the operator may supply the admin email and an OPERATOR-CHOSEN initial password via
    /// the environment (docker-compose forwards .env). Option A's invariant holds: Febris still
    /// never ships or invents a production password; one is only set when the operator provided
    /// it. With no NodeBootstrap section configured, behavior is unchanged.
    /// </summary>
    public class SeedData
    {

        /// <summary>
        /// Runs the idempotent seed. Call once from Program.Main (awaited) before host.Run().
        /// </summary>
        public static async Task SeedAllDataAsync(IServiceProvider services)
        {
            using (var scope = services.CreateScope())
            {
                IServiceProvider provider = scope.ServiceProvider;
                // Operator-configurable bootstrap identity (clone-and-run); resolves to the
                // historical defaults when no NodeBootstrap section is configured.
                NodeBootstrapAdminOptions bootstrap =
                    NodeBootstrapAdminOptions.Resolve(provider.GetService<IConfiguration>());
                await SeedRolesAsync(provider);
                await SeedFirstAdminAsync(provider, bootstrap);
                await IssueSetupTokenIfUnclaimedAsync(provider);
            }
        }

        /// <summary>
        /// First-run claim (2026-08-21). When the node has NO ITAdmin, mint a one-time setup token
        /// and print it so the operator can claim the node at <c>/setup</c>.
        ///
        /// <para>
        /// The predicate is "no ITAdmin exists", not "no users exist" and not a run-once flag. That
        /// makes it self-healing: a node whose admin accounts were all removed can be re-claimed by
        /// whoever can read its console, instead of being permanently bricked. It also means the
        /// ordinary case costs one indexed role query per boot and does nothing.
        /// </para>
        ///
        /// <para>
        /// WRITTEN WITH <c>Console.WriteLine</c>, NOT Serilog, and that is the entire security
        /// design rather than a style choice (owner decision 2026-08-21). Serilog fans out to the
        /// file sink and to any configured shipper, so a token logged through it would land on disk
        /// and in whatever aggregator the operator runs. Console output reaches
        /// <c>docker compose logs</c> and stops there. The trust boundary for claiming an unclaimed
        /// node is therefore "can read the node's stdout", which is the operator by definition.
        /// <c>NodeSetupTokenTests.TheToken_IsPrintedToStdoutOnly_NeverThroughSerilog</c> pins this.
        /// </para>
        /// </summary>
        private static async Task IssueSetupTokenIfUnclaimedAsync(IServiceProvider provider)
        {
            try
            {
                var userManager = provider.GetRequiredService<UserManager<LocalApplicationUser>>();
                var admins = await userManager.GetUsersInRoleAsync(
                    InstitutionUserAccountType.ITAdmin.ToString());

                // A SOFT-DELETED admin is not an admin (2026-08-25). Deleting an account sets
                // IsDeleted and locks it with LockoutEnd = MaxValue, but it deliberately RETAINS the
                // row for xAPI history and FERPA -- and it does not strip roles. So the sole ITAdmin
                // of a node deleting their own account left this query still returning them, the node
                // still believing it was claimed, no token issued, and /setup answering 404. That is
                // the permanent brick the self-healing predicate documented above exists to prevent,
                // reachable on a FRESH node by one supported user action with no way back short of
                // direct SQL.
                //
                // The claim surface does not widen: /setup still needs the one-time token, and that
                // token still goes to stdout only. The trust boundary is unchanged.
                // Precedent for the filter is UserLogic's own reads (`.Where(u => !u.IsDeleted)`).
                if (admins != null && admins.Any(admin => !admin.IsDeleted))
                {
                    return;
                }

                var setup = provider.GetService<Febris.UserNode.LogicLayer.Logic.IdentityLogic.INodeSetupLogic>();
                if (setup == null)
                {
                    Log.Error("Seed: the node has no ITAdmin and INodeSetupLogic is not registered, "
                        + "so no setup token can be issued and the node cannot be claimed.");
                    return;
                }

                string token = await setup.IssueToken();

                // Serilog is told an EVENT happened. It is never told the token.
                Log.Warning("Seed: this node has no ITAdmin. A first-run setup token was issued and "
                    + "printed to stdout. Claim the node at /setup.");

                Console.WriteLine();
                Console.WriteLine("========================================================================");
                Console.WriteLine(" FEBRIS NODE IS UNCLAIMED");
                Console.WriteLine();
                Console.WriteLine(" Open  /setup  and enter this token to create the first administrator:");
                Console.WriteLine();
                Console.WriteLine("     " + token);
                Console.WriteLine();
                Console.WriteLine(" It is valid for "
                    + (int)Febris.UserNode.LogicLayer.Logic.IdentityLogic.NodeSetupLogic.TokenLifetime.TotalMinutes
                    + " minutes and can be used once. Restarting the node issues a new one.");
                Console.WriteLine(" This token is printed here ONLY. It is not written to the log files.");
                Console.WriteLine("========================================================================");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                // Never fatal. A node that cannot issue a token is still a running node, and the
                // operator has the NodeBootstrap environment variables as the other door.
                Log.Error(ex, "Seed: failed issuing the first-run setup token.");
            }
        }

        // internal (not private) so the fail-fast behavior can be unit-tested directly against a mocked
        // RoleManager -- see SeedRolesFailFastTests. InternalsVisibleTo is declared in AssemblyInfo.cs.
        internal static async Task SeedRolesAsync(IServiceProvider provider)
        {
            // Roles are a HARD prerequisite: without them UserManager.AddToRoleAsync THROWS, so account
            // creation (self-registration, SSO JIT) and the bootstrap admin seed below all fail. This used to
            // SWALLOW every error, letting the node boot mis-seeded and 500 every registration silently.
            // Now any failure is logged Fatal and RE-THROWN so Program.Main aborts startup before
            // host.Run() -- the node refuses to serve rather than booting broken. The seed is idempotent,
            // so a restart retries cleanly once the underlying cause (unreachable DB, transient) is fixed.
            try
            {
                var roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();

                foreach (string roleName in NodeIdentityRoles.Required)
                {
                    if (!await roleManager.RoleExistsAsync(roleName))
                    {
                        IdentityResult createResult = await roleManager.CreateAsync(new ApplicationRole(roleName));
                        if (!createResult.Succeeded)
                        {
                            // CreateAsync REPORTS failure (vs throwing), e.g. a constraint violation --
                            // previously the result was ignored, silently leaving the role absent.
                            throw new InvalidOperationException(
                                "Seed: failed creating Identity role '" + roleName + "': " +
                                string.Join(", ", createResult.Errors.Select(e => e.Description)));
                        }
                    }
                }

                // Verify the full set actually exists now -- catches a create that neither threw nor
                // reported failure, and confirms the prerequisite before the host serves traffic.
                var stillMissing = new List<string>();
                foreach (string roleName in NodeIdentityRoles.Required)
                {
                    if (!await roleManager.RoleExistsAsync(roleName))
                    {
                        stillMissing.Add(roleName);
                    }
                }
                if (stillMissing.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Seed: Identity roles still missing after seeding: " + string.Join(", ", stillMissing));
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Seed: Identity role seeding FAILED. The node cannot provision accounts without " +
                    "its roles; aborting startup so it does not serve mis-seeded.");
                throw;
            }
        }

        private static async Task SeedFirstAdminAsync(IServiceProvider provider, NodeBootstrapAdminOptions bootstrap)
        {
            try
            {
                var userManager = provider.GetRequiredService<UserManager<LocalApplicationUser>>();
                string adminEmail = bootstrap.AdminEmail;

                // Run-once guard: key off the bootstrap admin itself (not "any user exists"), so the
                // seed is correct regardless of other accounts and re-creates the admin only if it
                // is genuinely missing. Keyed on the CONFIGURED email: changing NodeBootstrap:AdminEmail
                // on an already-seeded node seeds the new identity alongside the old one (an explicit,
                // visible operator action -- never a silent rename of an existing account).
                if (await userManager.FindByEmailAsync(adminEmail) != null)
                {
                    return;
                }

                // NOTHING CONFIGURED means DO NOT SEED (2026-08-21). Until now this branch created
                // an account at the compiled-in default address with no password, which in Release
                // was unreachable by construction: example.com is a reserved domain that cannot
                // receive the password-reset mail the flow depended on. The node now issues a
                // first-run setup token instead (see IssueSetupTokenIfUnclaimedAsync), which needs
                // no SMTP and puts no password on disk.
                //
                // Deliberately narrow. An operator who CONFIGURED an email but no password still
                // gets the old password-less account and the Forgot Password route, because that is
                // a real choice on a node with real mail, not the accidental default.
#if !(DEBUG || STAGING)
                // Declared inside the fence so a DEBUG build does not warn on an unused local.
                bool nothingConfigured = !bootstrap.HasOperatorPassword
                    && string.Equals(adminEmail, NodeBootstrapAdminOptions.DefaultAdminEmail, StringComparison.OrdinalIgnoreCase);
                if (nothingConfigured)
                {
                    Log.Information("Seed: no NodeBootstrap identity configured, so no default admin is "
                        + "seeded. A first-run setup token will be printed to stdout instead.");
                    return;
                }
#endif

                var admin = new LocalApplicationUser
                {
                    Email = adminEmail,
                    UserName = adminEmail,
                    EmailConfirmed = true,
                    LockoutEnabled = true,
                };

                IdentityResult createResult;
                if (bootstrap.HasOperatorPassword)
                {
                    // Clone-and-run: the OPERATOR supplied the initial password (NodeBootstrap:
                    // AdminPassword, forwarded from the deployment's .env) -- valid in every
                    // configuration because it is the operator's own choice, not a Febris default,
                    // so option A's "Febris never sets a production password" invariant holds.
                    createResult = await userManager.CreateAsync(admin, bootstrap.AdminPassword);
                }
                else
                {
#if DEBUG || STAGING
                    // Local/staging convenience: a known password so the seeded admin can sign in
                    // directly without the email round-trip.
                    createResult = await userManager.CreateAsync(admin, "Password123!");
#else
                    // Production (option A): NO password. The operator sets the initial password via the
                    // portal's Forgot Password page (the account is email-confirmed). Febris never sets
                    // or stores a production password.
                    createResult = await userManager.CreateAsync(admin);
#endif
                }
                if (!createResult.Succeeded)
                {
                    Log.Error("Seed: failed creating bootstrap admin {Email}: {Errors}",
                        adminEmail, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    return;
                }

                // ITAdmin, not SuperAdmin (owner ruling 2026-08-01). SuperAdmin is a FebrisUserType
                // -- a vendor staff role from when hosted support was offered. A self-hosted node
                // has no vendor, so its top account is the top LOCAL role. ITAdmin satisfies every
                // gate the bootstrap admin needs: IsLocalAdmin() is true for Admin OR ITAdmin, and
                // EndUserAll / EndUserNoParent / OrgAdmins / EducatorAndOrgAdmins all include it.
                IdentityResult roleResult = await userManager.AddToRoleAsync(
                    admin, InstitutionUserAccountType.ITAdmin.ToString());
                if (!roleResult.Succeeded)
                {
                    Log.Error("Seed: created bootstrap admin {Email} but failed adding the ITAdmin role: {Errors}",
                        adminEmail, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    return;
                }

                if (bootstrap.HasOperatorPassword)
                {
                    // Never log the password itself.
                    Log.Information("Seed: created bootstrap ITAdmin {Email} with the operator-supplied " +
                        "NodeBootstrap:AdminPassword.", adminEmail);
                }
                else
                {
#if DEBUG || STAGING
                    Log.Information("Seed: created bootstrap ITAdmin {Email} with the dev password.", adminEmail);
#else
                    Log.Information("Seed: created bootstrap ITAdmin {Email} with NO password. Set the " +
                        "initial password via the portal Forgot Password page (the account is email-confirmed), " +
                        "or configure NodeBootstrap:AdminPassword before first boot.",
                        adminEmail);
#endif
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Seed: failed seeding the bootstrap admin.");
            }
        }
    }
}
