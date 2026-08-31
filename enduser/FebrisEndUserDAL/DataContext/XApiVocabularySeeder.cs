// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Febris.UserNode.DataAccessLayer.DataContext
{
    /// <summary>
    /// Node-local xAPI vocabulary seed (local-first vocabulary).
    ///
    /// <para>
    /// The node owns its Verb/Version stores, so a fresh deployment must be able to resolve the
    /// standard vocabulary with NO central configured. Mirrors the central
    /// <c>DataBaseSeedDataInitalizer.CreateVerbs()/CreateVersion()</c> seed exactly: the standard
    /// <see cref="VerbEnums"/> verbs (IRIs via <see cref="VerbIRIResolver.ResolveVerbIRI"/>) and a
    /// single xAPI Version ("2.0"). Idempotent -- each set seeds only when its table is empty, so
    /// re-running at every startup is safe and a hub-synced or locally-extended vocabulary is
    /// never overwritten.
    /// </para>
    ///
    /// <para>
    /// Invoked by <see cref="EndUserDatabaseProvisioner"/> right after the XApi migration step,
    /// inside its per-database try/catch (a failed seed logs and skips rather than crashing host
    /// startup). Takes the context as a parameter -- no static ops fallback.
    /// </para>
    /// </summary>
    public static class XApiVocabularySeeder
    {
        /// <summary>
        /// Seed the standard verbs and the default Version into an EMPTY vocabulary store.
        /// Safe to call on every startup; non-empty tables are left untouched.
        /// </summary>
        public static void Seed(XApiDbContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (!context.Verb.Any())
            {
                // Same standard set central seeds; content-authored verbs beyond these resolve
                // through StatementFactor's transient-verb-on-miss fallback until added/synced.
                VerbEnums[] verbNames =
                {
                    VerbEnums.Attempted,
                    VerbEnums.Completed,
                    VerbEnums.Initialized,
                    VerbEnums.Terminated,
                    VerbEnums.Pass,
                    VerbEnums.Not_Pass,
                    VerbEnums.Voided
                };

                List<ModelLibrary.Models.XApiModels.Verb> verbs = new List<ModelLibrary.Models.XApiModels.Verb>();
                foreach (VerbEnums verbName in verbNames)
                {
                    verbs.Add(new ModelLibrary.Models.XApiModels.Verb()
                    {
                        Display = new Dictionary<string, string> { ["en"] = verbName.ToString() },
                        Id = new Uri(VerbIRIResolver.ResolveVerbIRI(verbName))
                    });
                }
                context.Verb.AddRange(verbs);
                context.SaveChanges();
            }

            if (!context.Version.Any())
            {
                context.Version.Add(new ModelLibrary.Models.XApiModels.Version()
                {
                    VersionNumber = "2.0"
                });
                context.SaveChanges();
            }
        }
    }
}
