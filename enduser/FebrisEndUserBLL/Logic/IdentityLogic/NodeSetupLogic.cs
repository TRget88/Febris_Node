// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using System;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.IdentityLogic
{
    /// <summary>
    /// First-run claim: issue and redeem the one-time token that creates a node's first ITAdmin
    /// (2026-08-21).
    ///
    /// <para>
    /// REPLACES A COMPILED-IN SEEDED ADMIN, which is a reasonable shape for unattended automation
    /// and a poor one for an open-source project. The old default put an admin password in a file on
    /// disk, required editing configuration before the first boot, and in Release with nothing
    /// configured produced an account at a reserved example.com address with no password. That
    /// account could never be signed in to, because the domain cannot receive the password-reset
    /// mail the flow depended on.
    /// </para>
    ///
    /// <para>
    /// The env-var seed is NOT removed. It stays as the unattended door, and this is the interactive
    /// one. A node where the operator configured a password never issues a token at all.
    /// </para>
    /// </summary>
    public interface INodeSetupLogic
    {
        /// <summary>
        /// Mint a token and return it IN PLAINTEXT, once. The caller is the startup path, whose only
        /// job with it is to write it to stdout. Nothing else may keep, log or persist it.
        /// </summary>
        Task<string> IssueToken();

        /// <summary>Whether a token is currently live (unconsumed, unexpired).</summary>
        Task<bool> HasLiveToken();

        /// <summary>
        /// Look a presented token up and classify it. Returns the row only when it is claimable, so
        /// a caller cannot act on a spent or lapsed one.
        /// </summary>
        Task<NodeSetupTokenState> Validate(string rawToken);

        /// <summary>The claimable token's row handle, or null when it is absent, spent or lapsed.
        /// On the interface rather than the concrete class so the setup page never has to cast, and
        /// so a substituted implementation stays substitutable.</summary>
        Task<Guid?> ClaimableUuid(string rawToken);

        /// <summary>Atomically claim the token. False when another request won the race.</summary>
        Task<bool> Consume(Guid uuid, Guid consumedByUserId, string consumedByEmail);
    }

    /// <summary>What a presented setup token is.</summary>
    public enum NodeSetupTokenState
    {
        /// <summary>No such token. Also the answer for a blank or malformed one.</summary>
        NotFound,

        /// <summary>Live and claimable.</summary>
        Claimable,

        /// <summary>Lapsed. Restarting the node mints a fresh one.</summary>
        Expired,

        /// <summary>Already used. The node has been claimed.</summary>
        AlreadyClaimed
    }

    /// <summary>
    /// DI-only implementation of <see cref="INodeSetupLogic"/>. Greenfield node logic, deliberately
    /// NO legacy self-newing constructor.
    /// </summary>
    public class NodeSetupLogic : INodeSetupLogic
    {
        /// <summary>
        /// How long a freshly minted token stays claimable.
        ///
        /// <para>
        /// Short on purpose, and the short direction is the SAFE one rather than the inconvenient
        /// one: once the window lapses the setup page refuses everything, so an unclaimed node left
        /// running overnight becomes less claimable, not more. The cost of being wrong is a restart,
        /// which mints a fresh token and prints it again.
        /// </para>
        /// </summary>
        public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(60);

        private readonly INodeSetupTokenQueries _tokenContext;

        /// <summary>DI constructor (the only one).</summary>
        public NodeSetupLogic(INodeSetupTokenQueries tokenContext)
        {
            _tokenContext = tokenContext;
        }

        /// <inheritdoc />
        public async Task<string> IssueToken()
        {
            // Same primitive as the invitation token and the device credential: 256 bits from a
            // CSPRNG, stored only as a lowercase-hex SHA-256. Shared rather than reimplemented,
            // because every line of that reasoning transfers.
            string rawToken = DeviceCredential.Generate();
            await _tokenContext.Issue(DeviceCredential.Hash(rawToken), DateTime.UtcNow.Add(TokenLifetime));

            // Returned, never logged here. THE CALLER WRITES IT TO STDOUT AND NOWHERE ELSE. This
            // method deliberately does not touch FebrisLog: Serilog fans out to the file sink and to
            // any configured shipper, and the whole security argument for this flow is that the
            // claim secret reaches the operator's console and no durable medium.
            return rawToken;
        }

        /// <inheritdoc />
        public async Task<bool> HasLiveToken()
        {
            return await _tokenContext.HasLiveToken();
        }

        /// <inheritdoc />
        public async Task<NodeSetupTokenState> Validate(string rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
            {
                return NodeSetupTokenState.NotFound;
            }

            NodeSetupToken token = await _tokenContext.GetByTokenHash(DeviceCredential.Hash(rawToken.Trim()));
            if (token == null)
            {
                return NodeSetupTokenState.NotFound;
            }
            if (token.ConsumedAt.HasValue)
            {
                return NodeSetupTokenState.AlreadyClaimed;
            }
            if (token.ExpiresAt <= DateTime.UtcNow)
            {
                return NodeSetupTokenState.Expired;
            }
            return NodeSetupTokenState.Claimable;
        }

        /// <inheritdoc />
        public async Task<Guid?> ClaimableUuid(string rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
            {
                return null;
            }
            NodeSetupToken token = await _tokenContext.GetByTokenHash(DeviceCredential.Hash(rawToken.Trim()));
            if (token == null || token.ConsumedAt.HasValue || token.ExpiresAt <= DateTime.UtcNow)
            {
                return null;
            }
            return token.UUID;
        }

        /// <inheritdoc />
        public async Task<bool> Consume(Guid uuid, Guid consumedByUserId, string consumedByEmail)
        {
            bool claimed = await _tokenContext.MarkConsumed(
                uuid, consumedByUserId, consumedByEmail, DateTime.UtcNow);
            if (claimed)
            {
                // The CLAIM is logged through the normal path. The TOKEN never is.
                FebrisLog.Warn("[node-setup] node claimed; first ITAdmin created for "
                    + (consumedByEmail ?? "(unrecorded)"));
            }
            return claimed;
        }
    }
}
