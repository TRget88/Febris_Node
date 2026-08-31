// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.SharedServices;
using Microsoft.Extensions.Configuration;

namespace Febris.UserNode.Portal
{
    /// <summary>
    /// Decides how the node's portal stores its login session: Redis is OPTIONAL for a node, so the
    /// cache posture is chosen by configuration (previously the Redis-backed store + HTTPS-strict
    /// cookie were hard-wired).
    ///
    /// <para>
    /// When a real Redis/Valkey <c>AuthConnection</c> is configured, the heavy
    /// <c>AuthenticationTicket</c> lives server-side in <c>RedisCacheTicketStore</c> so sessions are
    /// shared across instances (multi-instance / HA), and the auth cookie is HTTPS-strict. When it is
    /// NOT configured, the node needs neither Redis nor a TLS terminator: the ticket is carried in the
    /// DataProtection-encrypted cookie itself and the cookie is relaxed so login works over plain-HTTP
    /// localhost -- the "clone -> run -> log in with only a database" path. The store swaps purely by
    /// configuration, exactly like the database and storage providers.
    /// </para>
    /// </summary>
    public static class NodeSessionPolicy
    {
        /// <summary>
        /// True iff a usable Redis/Valkey <c>RedisConnectionStrings:AuthConnection</c> is configured --
        /// non-empty and not an unsubstituted <c>{Token}</c> deploy placeholder. False selects the
        /// zero-dependency in-cookie session default.
        /// </summary>
        public static bool UsesRedisSessionStore(IConfiguration config)
        {
            if (config == null)
            {
                return false;
            }

            string authConnection = config.GetSection("RedisConnectionStrings").GetValue<string>("AuthConnection");
            return !string.IsNullOrWhiteSpace(authConnection)
                && !JwtSigningKeyProvider.IsUnsubstitutedTemplate(authConnection);
        }
    }
}
