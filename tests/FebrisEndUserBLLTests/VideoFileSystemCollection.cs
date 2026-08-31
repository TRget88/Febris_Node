// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Serialises the two video suites against each other.
    ///
    /// <para>
    /// Both <see cref="VideoMergeLifecycleTests"/> and <see cref="VideoQuotaTests"/> redirect the
    /// PROCESS-WIDE <c>StaticDetails.SplitVideoFileSystemPath</c> and
    /// <c>RecordingsFileSystemPath</c> at a temp directory, because the production code concatenates
    /// those statics directly and they are the only seam. xUnit parallelises across test CLASSES by
    /// default, so without this one class's <c>Dispose</c> restored the statics while the other was
    /// still running, and its uploads landed in a directory the assertions were not looking at.
    /// </para>
    ///
    /// <para>
    /// It only failed in a FULL run, never in a filtered one, which is the kind of flake that gets
    /// re-run and dismissed. Pinning both suites into one non-parallel collection is the fix.
    /// </para>
    /// </summary>
    [CollectionDefinition("VideoFileSystem", DisableParallelization = true)]
    public class VideoFileSystemCollection { }
}
