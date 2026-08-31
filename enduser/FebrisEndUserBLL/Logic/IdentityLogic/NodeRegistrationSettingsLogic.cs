// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using System;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.IdentityLogic
{
    /// <summary>
    /// Admin surface behind the portal's Registration page (node initialization design
    /// 2026-08-18). Reads and writes the node's STORED registration policy -- the runtime-turnable
    /// version of <c>Identity:Registration</c>, which until now could only be changed by editing a
    /// JSON file and restarting the host.
    ///
    /// <para>
    /// Deals in NAMES, not the registration-mode enum: that enum lives in the portal assembly and
    /// the logic layer cannot reference it. Validating a name against the enum is therefore the
    /// PORTAL's job, done before it calls <see cref="Save"/>. This layer's contract is narrower
    /// and honest about it: persist what it is given, and never invent a policy of its own.
    /// </para>
    /// </summary>
    public interface INodeRegistrationSettingsLogic
    {
        /// <summary>
        /// The stored policy. Returns a snapshot with <c>HasStoredSettings = false</c> when the
        /// operator never saved (a normal state that hands governance back to configuration).
        /// THROWS on a store failure rather than returning the never-saved shape, because callers
        /// must be able to tell "not configured" from "could not read" -- conflating them is how a
        /// fail-closed gate turns into a fail-open one.
        /// </summary>
        Task<StoredRegistrationPolicy> GetStored();

        /// <summary>
        /// Persist the operator's policy and return the refreshed snapshot.
        /// <paramref name="modeName"/> must already be a valid mode name (the portal validates it
        /// against the enum first). <paramref name="actorEmail"/> is recorded on the row and
        /// written to the log, because opening registration on a node holding learner records is
        /// an audit-worthy event.
        /// </summary>
        Task<StoredRegistrationPolicy> Save(RegistrationSettingsInputModel input, string modeName, string actorEmail);
    }

    /// <summary>
    /// DI-only implementation of <see cref="INodeRegistrationSettingsLogic"/>: greenfield node
    /// logic, deliberately NO legacy self-newing constructor. Non-<c>*Queries</c>, so the DAL
    /// convention sweep does not cover it and the host registers it explicitly.
    /// </summary>
    public class NodeRegistrationSettingsLogic : INodeRegistrationSettingsLogic
    {
        /// <summary>Upper bound on an auto-close window: 30 days in hours. A window longer than a
        /// month is indistinguishable in practice from "open forever" and should be expressed as
        /// such (no expiry) rather than hidden behind a number nobody will revisit.</summary>
        public const int MaxOpenForHours = 24 * 30;

        private readonly INodeRegistrationConfigQueries _configContext;

        /// <summary>DI constructor (the only one).</summary>
        public NodeRegistrationSettingsLogic(INodeRegistrationConfigQueries configContext)
        {
            _configContext = configContext;
        }

        /// <inheritdoc />
        public async Task<StoredRegistrationPolicy> GetStored()
        {
            NodeRegistrationConfig row = await _configContext.Get();
            return ToSnapshot(row);
        }

        /// <inheritdoc />
        public async Task<StoredRegistrationPolicy> Save(
            RegistrationSettingsInputModel input, string modeName, string actorEmail)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            if (string.IsNullOrWhiteSpace(modeName))
            {
                throw new ArgumentException("A registration mode name is required.", nameof(modeName));
            }

            NodeRegistrationConfig saved = await _configContext.Save(new NodeRegistrationConfig()
            {
                Mode = modeName.Trim(),
                AllowedEmailDomains = NormalizeDomains(input.AllowedEmailDomains),
                RequireAdminApproval = input.RequireAdminApproval,
                AutoProvisionJit = input.AutoProvisionJit,
                OpenUntilUtc = ResolveOpenUntil(input.OpenForHours),
                UpdatedByEmail = string.IsNullOrWhiteSpace(actorEmail) ? null : actorEmail.Trim()
            });

            // The node has no audit table (see ParentLinkLogic for the same note), so this records
            // through the existing logging path. Warn rather than Info deliberately: a registration
            // policy change is the kind of event someone reads the log looking for.
            Febris.SharedServices.FebrisLog.Warn(string.Format(
                "[registration-policy] mode set to {0} by {1}; adminApproval={2} autoProvisionJit={3} openUntilUtc={4}",
                saved.Mode,
                saved.UpdatedByEmail ?? "(unrecorded)",
                saved.RequireAdminApproval,
                saved.AutoProvisionJit,
                saved.OpenUntilUtc.HasValue ? saved.OpenUntilUtc.Value.ToString("u") : "(none)"));

            return ToSnapshot(saved);
        }

        /// <summary>
        /// Turn the operator's "close automatically after N hours" into an absolute UTC moment.
        /// Null, zero, or negative means open-ended; anything above <see cref="MaxOpenForHours"/>
        /// is clamped rather than rejected, so a fat-fingered 99999 yields a bounded window
        /// instead of a validation error that tempts the operator into choosing no window at all.
        /// </summary>
        private static DateTime? ResolveOpenUntil(int? openForHours)
        {
            if (!openForHours.HasValue || openForHours.Value <= 0)
            {
                return null;
            }
            int hours = openForHours.Value > MaxOpenForHours ? MaxOpenForHours : openForHours.Value;
            return DateTime.UtcNow.AddHours(hours);
        }

        /// <summary>
        /// Tidy the comma-separated domain list: trim each entry, drop blanks, drop a leading "@"
        /// (the allowlist comparison tolerates both forms, but storing one form keeps the field
        /// readable), lowercase, and de-duplicate while preserving order. An empty result is stored
        /// as null so "no domains" reads the same whether the operator cleared the box or never
        /// filled it.
        /// </summary>
        public static string NormalizeDomains(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var kept = new System.Collections.Generic.List<string>();

            // Split on commas, semicolons, whitespace and newlines: the field is a textarea and
            // operators paste lists in every one of those shapes.
            string[] parts = raw.Split(
                new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string entry = part.Trim();
                if (entry.Length > 0 && entry[0] == '@')
                {
                    entry = entry.Substring(1);
                }
                if (entry.Length == 0)
                {
                    continue;
                }
                entry = entry.ToLowerInvariant();
                if (seen.Add(entry))
                {
                    kept.Add(entry);
                }
            }

            return kept.Count == 0 ? null : string.Join(",", kept);
        }

        /// <summary>Map a stored row (or its absence) onto the neutral snapshot.</summary>
        private static StoredRegistrationPolicy ToSnapshot(NodeRegistrationConfig row)
        {
            if (row == null)
            {
                return new StoredRegistrationPolicy() { HasStoredSettings = false };
            }

            return new StoredRegistrationPolicy()
            {
                HasStoredSettings = true,
                Mode = row.Mode,
                AllowedEmailDomains = row.AllowedEmailDomains,
                RequireAdminApproval = row.RequireAdminApproval,
                AutoProvisionJit = row.AutoProvisionJit,
                OpenUntilUtc = row.OpenUntilUtc,
                UpdatedAtUtc = row.UpdatedAt,
                UpdatedByEmail = row.UpdatedByEmail
            };
        }
    }
}
