// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;

namespace Febris.UserNode.DataAccessLayer.DataContext
{
    /// <summary>
    /// Node-local identity seed (auth severance): gives a fresh deployment its
    /// LOCAL single-tenant identity so license-claim-derived reads have a no-hub answer.
    ///
    /// <para>
    /// Mirrors <see cref="XApiVocabularySeeder"/>'s idempotent pattern exactly: seeds ONLY when
    /// the table is empty, so re-running at every startup is safe and the provisioned
    /// <c>InstitutionUUID</c> is generated once and never regenerated (it is the node's stable
    /// identity -- a hub can later map it, so it must not drift). The display name comes from
    /// config (<c>NodeIdentity:Name</c>) when supplied at provision time; renames afterwards are
    /// an admin-surface concern, not a seeder concern (the seeder never overwrites).
    /// </para>
    ///
    /// <para>
    /// Invoked by <see cref="EndUserDatabaseProvisioner"/> right after the DataDb migration step,
    /// inside its per-database try/catch (a failed seed logs and skips rather than crashing host
    /// startup). Takes the context as a parameter -- no static ops fallback.
    /// </para>
    /// </summary>
    public static class NodeIdentitySeeder
    {
        /// <summary>Fallback display name when config supplies none.</summary>
        public const string DefaultName = "Febris Node";

        /// <summary>
        /// Seed the single NodeIdentity row into an EMPTY store. Safe to call on every startup;
        /// a non-empty table is left untouched (provision-once semantics).
        /// </summary>
        public static void Seed(DataDbContext context, IConfiguration configuration = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (!context.NodeIdentity.Any())
            {
                string configuredName = configuration?["NodeIdentity:Name"];
                context.NodeIdentity.Add(new NodeIdentity()
                {
                    // Explicit UUIDs (rather than the column defaults) so the generated identity
                    // is available to the caller immediately and provider-neutral (InMemory test
                    // stores have no uuid_generate_v4()).
                    UUID = Guid.NewGuid(),
                    InstitutionUUID = Guid.NewGuid(),
                    Name = string.IsNullOrWhiteSpace(configuredName) ? DefaultName : configuredName
                });
                context.SaveChanges();
            }
        }
    }
}
