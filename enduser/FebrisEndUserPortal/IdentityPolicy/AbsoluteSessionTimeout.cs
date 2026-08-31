// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Globalization;
using Microsoft.AspNetCore.Authentication;

namespace Febris.UserNode.Portal.IdentityPolicy
{
    /// <summary>
    /// Enforces <c>Session.AbsoluteTimeoutMinutes</c>: a HARD cap on session age that
    /// <c>ExpireTimeSpan</c> + <c>SlidingExpiration</c> cannot provide (with sliding on, an active session
    /// renews forever). A deadline is stamped ONCE at real sign-in (the cookie's <c>OnSigningIn</c> event)
    /// into <see cref="AuthenticationProperties"/>.Items -- which survive sliding renewals, unlike
    /// <c>IssuedUtc</c>, which the cookie handler resets on each slide -- and <c>OnValidatePrincipal</c>
    /// rejects the principal once it passes. A null or non-positive timeout disables the cap (the default).
    /// </summary>
    /// <remarks>
    /// Transition note: the deadline is written only at sign-in, so a session already active when an
    /// operator FIRST enables (or lowers) the cap on a running node carries no stamp and stays uncapped
    /// (bounded only by the idle <c>ExpireTimeSpan</c>) until it goes idle or the user re-authenticates. To
    /// bind pre-existing sessions immediately, pair enabling/lowering the cap with a global security-stamp
    /// rotation ("sign out everywhere"), which <c>SecurityStampValidator</c> already honors. Sessions created
    /// after the cap is configured -- the normal case, set in appsettings before boot -- are always capped.
    /// </remarks>
    public static class AbsoluteSessionTimeout
    {
        /// <summary>AuthenticationProperties.Items key holding the absolute session deadline (round-trip UTC).</summary>
        public const string DeadlineKey = "febris.session.abs_deadline";

        /// <summary>
        /// Stamp the absolute deadline ONCE, at real sign-in. Idempotent: a re-entrant sign-in keeps the
        /// original deadline, and sliding renewals never re-fire OnSigningIn, so the cap anchors to the
        /// FIRST sign-in, not the last activity. No-op when the timeout is null or non-positive.
        /// </summary>
        public static void Stamp(AuthenticationProperties properties, int? absoluteTimeoutMinutes, DateTimeOffset nowUtc)
        {
            if (properties == null || !absoluteTimeoutMinutes.HasValue || absoluteTimeoutMinutes.Value <= 0)
            {
                return;
            }
            if (properties.Items.ContainsKey(DeadlineKey))
            {
                return;
            }
            DateTimeOffset deadline = nowUtc.AddMinutes(absoluteTimeoutMinutes.Value);
            properties.Items[DeadlineKey] = deadline.ToString("o", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// True only when a well-formed deadline was stamped AND it has passed. Fails OPEN on a missing or
        /// unparseable stamp -- never lock a user out over a malformed cookie value; the framework's own
        /// <c>ExpireTimeSpan</c> still bounds such a session.
        /// </summary>
        public static bool IsExpired(AuthenticationProperties properties, DateTimeOffset nowUtc)
        {
            if (properties == null
                || !properties.Items.TryGetValue(DeadlineKey, out string raw)
                || string.IsNullOrEmpty(raw))
            {
                return false;
            }
            if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset deadline))
            {
                return false;
            }
            return nowUtc >= deadline;
        }
    }
}
