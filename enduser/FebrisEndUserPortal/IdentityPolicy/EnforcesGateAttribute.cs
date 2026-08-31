// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;

namespace Febris.UserNode.Portal.IdentityPolicy
{
    /// <summary>
    /// Marks the method (or class) that ENFORCES an identity-policy gate -- a leaf property path on
    /// <see cref="IdentityPolicyOptions"/>, written "Section.Property" (e.g. "Login.AllowLocalPassword",
    /// "Password.RequiredLength").
    /// <para>
    /// The <c>IdentityGateCoverageTests</c> ratchet reflects over every leaf gate on
    /// <see cref="IdentityPolicyOptions"/> and asserts each one is EITHER marked here OR listed in
    /// <see cref="DeferredGates"/> with a written reason. A gate in neither fails the build (a knob that
    /// would silently lie to the operator); a gate in both also fails the build.
    /// </para>
    /// <para>
    /// SEMANTICS: applying this attribute is a claim that the gate is HONORED on every reachable path,
    /// not merely read once. Do NOT mark a gate that a known sink bypasses -- defer it with the reason
    /// describing the gap. The marker travels with the code through refactors, unlike a file:line citation.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class EnforcesGateAttribute : Attribute
    {
        /// <param name="gatePath">The leaf gate path, "Section.Property".</param>
        public EnforcesGateAttribute(string gatePath)
        {
            GatePath = gatePath;
        }

        /// <summary>The leaf gate path this member enforces, e.g. "Login.AllowLocalPassword".</summary>
        public string GatePath { get; }
    }
}
