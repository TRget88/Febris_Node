// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Febris.UserNode.Portal.IdentityPolicy;
using FluentAssertions;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// The identity-gate COVERAGE ratchet. Every leaf policy knob on <see cref="IdentityPolicyOptions"/>
    /// must be either
    /// <list type="bullet">
    ///   <item>(a) ENFORCED -- the member that honors it is marked with <see cref="EnforcesGateAttribute"/>, or</item>
    ///   <item>(b) DEFERRED -- listed in <see cref="DeferredGates"/> with a written reason.</item>
    /// </list>
    /// A knob in NEITHER is a dead toggle that silently lies to the operator, and this build fails until it
    /// is enforced or deferred. A knob in BOTH is contradictory and also fails. This makes "a security or
    /// privacy gate declared without enforcement" un-mergeable -- the exact class of defect an identity-gate
    /// audit found five instances of. It reflects over the compiled Portal assembly (this test
    /// project already references it), so a new knob cannot be added without appearing in the leaf set.
    /// </summary>
    public class IdentityGateCoverageTests
    {
        private static readonly Assembly PortalAssembly = typeof(IdentityPolicyOptions).Assembly;

        // ---- the three sets ---------------------------------------------------------------------

        /// <summary>Every leaf gate path ("Section.Property"), reflected from the options tree -- the source of truth.</summary>
        private static ISet<string> AllLeafGates()
        {
            var leaves = new SortedSet<string>(StringComparer.Ordinal);
            CollectLeaves(typeof(IdentityPolicyOptions), prefix: null, leaves, new HashSet<Type>());
            return leaves;
        }

        private static void CollectLeaves(Type type, string prefix, ISet<string> sink, ISet<Type> visited)
        {
            if (!visited.Add(type)) return; // cycle guard (the shape is acyclic today)
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var path = prefix == null ? prop.Name : $"{prefix}.{prop.Name}";
                if (IsPolicyContainer(prop.PropertyType))
                    CollectLeaves(prop.PropertyType, path, sink, visited);
                else
                    sink.Add(path);
            }
            visited.Remove(type);
        }

        // A nested options class (RegistrationOptions/PasswordPolicyOptions/...) is a container to recurse;
        // a bool/int/int?/enum/string[] is a leaf to record. "Container" == a class declared alongside
        // IdentityPolicyOptions (same namespace), which excludes string and the framework value types.
        private static bool IsPolicyContainer(Type t) =>
            t.IsClass && t != typeof(string) && t.Namespace == typeof(IdentityPolicyOptions).Namespace;

        /// <summary>Every gate path claimed enforced by an <see cref="EnforcesGateAttribute"/> in the Portal assembly.</summary>
        private static ISet<string> MarkedEnforcedGates()
        {
            var marked = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var type in SafeGetTypes(PortalAssembly))
            {
                try
                {
                    foreach (var a in type.GetCustomAttributes<EnforcesGateAttribute>(inherit: false))
                        marked.Add(a.GatePath);

                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                  BindingFlags.Instance | BindingFlags.Static |
                                                  BindingFlags.DeclaredOnly);
                    foreach (var m in methods)
                        foreach (var a in m.GetCustomAttributes<EnforcesGateAttribute>(inherit: false))
                            marked.Add(a.GatePath);
                }
                catch (Exception)
                {
                    // A type whose metadata can't be fully loaded contributes no markers; skip it. If it
                    // WERE one of ours, the gate surfaces as an orphan below -- a loud failure, never silent.
                }
            }
            return marked;
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
        }

        private static ISet<string> DeferredGateKeys() =>
            new SortedSet<string>(DeferredGates.Reasons.Keys, StringComparer.Ordinal);

        // ---- the invariants ---------------------------------------------------------------------

        [Fact]
        public void EveryLeafGate_IsEitherEnforcedOrDeferred()
        {
            var covered = MarkedEnforcedGates();
            covered.UnionWith(DeferredGateKeys());

            var orphans = AllLeafGates().Where(g => !covered.Contains(g)).ToList();

            orphans.Should().BeEmpty(
                "every IdentityPolicyOptions knob must be enforced ([EnforcesGate]) or deferred " +
                "(DeferredGates) so no toggle silently lies to the operator. Orphaned: {0}",
                string.Join(", ", orphans));
        }

        [Fact]
        public void NoGate_IsBothEnforcedAndDeferred()
        {
            var both = MarkedEnforcedGates().Intersect(DeferredGateKeys()).ToList();

            both.Should().BeEmpty(
                "a gate cannot be both enforced and deferred -- resolve it to one bucket. Contradictory: {0}",
                string.Join(", ", both));
        }

        [Fact]
        public void EnforcedMarkers_PointAtRealLeafGates()
        {
            var leaves = AllLeafGates();
            var stale = MarkedEnforcedGates().Where(g => !leaves.Contains(g)).ToList();

            stale.Should().BeEmpty(
                "[EnforcesGate] must name a current IdentityPolicyOptions leaf (catches a renamed/removed " +
                "knob leaving a dangling marker). Stale markers: {0}", string.Join(", ", stale));
        }

        [Fact]
        public void DeferredEntries_PointAtRealLeafGates()
        {
            var leaves = AllLeafGates();
            var stale = DeferredGateKeys().Where(g => !leaves.Contains(g)).ToList();

            stale.Should().BeEmpty(
                "DeferredGates keys must name a current IdentityPolicyOptions leaf. Stale deferrals: {0}",
                string.Join(", ", stale));
        }

        [Fact]
        public void DeferredEntries_HaveSubstantiveReasons()
        {
            var thin = DeferredGates.Reasons
                .Where(kv => string.IsNullOrWhiteSpace(kv.Value) || kv.Value.Trim().Length < 30)
                .Select(kv => kv.Key)
                .ToList();

            thin.Should().BeEmpty(
                "a deferral is a reviewed TODO -- each needs a real justification (>= 30 chars), never a " +
                "blank suppression. Thin reasons: {0}", string.Join(", ", thin));
        }

        [Fact]
        public void ReflectionWalker_IsWired()
        {
            // Guards the ratchet itself: if the walker suddenly reads 0 leaves or 0 markers, the reflection
            // or the Portal reference broke and every other test above would pass vacuously.
            AllLeafGates().Should().NotBeEmpty("the options tree must yield leaf gates");
            MarkedEnforcedGates().Should().NotBeEmpty("the Portal assembly must contain [EnforcesGate] markers");
        }
    }
}
