// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;

namespace Febris.UserNode.Portal.IdentityPolicy
{
    /// <summary>
    /// Identity-policy gates that are DECLARED on <see cref="IdentityPolicyOptions"/> but not yet
    /// enforced, each paired with a written justification. This is the ONLY sanctioned way for a gate
    /// to exist without an <see cref="EnforcesGateAttribute"/>: the <c>IdentityGateCoverageTests</c>
    /// ratchet fails the build for any leaf gate that is neither marked enforced nor listed here.
    /// <para>
    /// A deferral is a REVIEWED TODO, not a silent suppression -- keep each reason specific: name the
    /// unguarded path, the fix, and its fail direction. When a gate is genuinely enforced, delete its
    /// entry here and mark the enforcing member with <see cref="EnforcesGateAttribute"/>.
    /// </para>
    /// </summary>
    public static class DeferredGates
    {
        /// <summary>
        /// Gate path -> why it is not yet enforced (and its fail direction). EMPTY: all 23 identity gates
        /// are currently enforced. Add an entry here ONLY with a real
        /// justification when a new gate is declared but not yet wired.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Reasons = new Dictionary<string, string>();
    }
}
