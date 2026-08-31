// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Locate the repository root from the test binaries, for the source-scanning guards in this
    /// project.
    ///
    /// <para>
    /// EXPORT-SAFE MARKER (2026-08-25). Three tests each carried their own copy of this walk, and
    /// all three looked for <c>Febris.sln</c>. That file does not survive the public cut:
    /// <c>release/export/cut-node.sh</c> deletes it and generates <c>febris-node.sln</c> over the
    /// projects that remain, because the workshop solution lists 30 projects and the export ships
    /// 11. So every guard using that marker walked to the filesystem root in the export, dereferenced
    /// a null <c>DirectoryInfo</c>, and failed -- and the first public CI run would have been red
    /// from the cut itself rather than from any defect in the product.
    /// </para>
    /// <para>
    /// The marker is now a pair of DIRECTORIES that the export exists to ship, matching
    /// <c>ProjectGraph.FindRepoRoot</c> in the architecture test project as it stands today. That
    /// one required enduser/ AND shared/ AND central/ until commit 4a18fee dropped central/ on
    /// 2026-07-30, for exactly this reason -- the export has no central/, so every guard threw at
    /// extraction. This is the second time the same marker bug has been fixed, not a
    /// long-standing precedent being followed. A marker
    /// that the release process removes on purpose was never the right choice: it made the guards
    /// depend on workshop-only scaffolding.
    /// </para>
    /// </summary>
    internal static class TestRepoRoot
    {
        /// <summary>
        /// Walk up from the test binaries to the repository root, identified by carrying
        /// <c>enduser/</c>.
        ///
        /// <para>
        /// THIRD OCCURRENCE, 2026-08-28. The note above records this bug being fixed twice and
        /// concludes that "a marker that the release process removes on purpose was never the right
        /// choice". The fix that wrote that sentence still required TWO directories. When the shared
        /// kernel moved to its own repository (docs/decisions/TRIAD_OWNERSHIP.md) and the node export
        /// stopped shipping <c>shared/</c>, all thirteen source-scanning guards in this project threw
        /// in the export again, exactly as before.
        /// </para>
        /// <para>
        /// The marker is now ONE directory. Every extra directory in the condition is another thing a
        /// future cut can legitimately remove, and the cost of adding one is invisible in the
        /// workshop precisely because the workshop has everything.
        /// </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown rather than returning null, so a guard that cannot find the tree FAILS instead of
        /// silently scanning nothing. A source guard with no source to scan is the vacuous-pass
        /// shape this suite has been bitten by before.
        /// </exception>
        public static string Find()
        {
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "enduser")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException(
                "Could not locate the Febris repo root (a directory containing enduser/).");
        }
    }
}
