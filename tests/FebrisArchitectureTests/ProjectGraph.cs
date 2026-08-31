// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Febris.ArchitectureTests
{
    /// <summary>
    /// Shared filesystem helpers for the architecture fitness tests. Parses
    /// the repository's .csproj files on disk to reason about the project
    /// reference graph. Like the test project itself, this depends on nothing,
    /// so the guards can never drag a project across a boundary they police.
    /// </summary>
    internal static class ProjectGraph
    {
        /// <summary>
        /// Walk up from the test assembly to the repo root, identified as the
        /// directory that contains enduser/.
        ///
        /// central/ is deliberately NOT part of the marker. It exists only in the
        /// full workshop tree, so requiring it made every guard throw in a repo cut
        /// that contains just the node -- exactly the tree the guards most need to
        /// run in.
        ///
        /// shared/ was removed from the marker on 2026-08-28 for the SAME reason,
        /// one tree over. The shared kernel now lives in its own repository (see
        /// docs/decisions/TRIAD_OWNERSHIP.md), so a node export contains no shared/
        /// at all and requiring it here would throw before a single guard ran. The
        /// lesson the central/ note already records is that a root marker must name
        /// something every tree carrying these tests actually has, and adding a
        /// second directory to the condition looks free in the workshop precisely
        /// because the workshop has everything.
        /// </summary>
        public static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
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

        /// <summary>Every .csproj under the given repo-relative roots, excluding build output.</summary>
        public static IEnumerable<string> EnumerateProjectFilesUnder(string repoRoot, params string[] roots)
        {
            foreach (var root in roots)
            {
                var dir = Path.Combine(repoRoot, root);
                if (!Directory.Exists(dir))
                {
                    continue;
                }
                foreach (var f in Directory.EnumerateFiles(dir, "*.csproj", SearchOption.AllDirectories))
                {
                    if (IsInBuildOutput(f))
                    {
                        continue;
                    }
                    yield return Path.GetFullPath(f);
                }
            }
        }

        /// <summary>Absolute paths of the projects this csproj references directly (one hop).</summary>
        public static IEnumerable<string> DirectProjectReferences(string csprojPath)
        {
            if (!File.Exists(csprojPath))
            {
                yield break;
            }
            XDocument doc;
            try
            {
                doc = XDocument.Load(csprojPath);
            }
            catch
            {
                yield break;
            }
            var dir = Path.GetDirectoryName(csprojPath);
            foreach (var pr in doc.Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
            {
                var include = pr.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(include))
                {
                    continue;
                }
                var normalized = include.Replace('\\', Path.DirectorySeparatorChar);
                yield return Path.GetFullPath(Path.Combine(dir, normalized));
            }
        }

        /// <summary>The full transitive closure of project references from this csproj.</summary>
        public static HashSet<string> TransitiveProjectReferences(string csprojPath)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(csprojPath) };
            var stack = new Stack<string>();
            stack.Push(Path.GetFullPath(csprojPath));
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                foreach (var refPath in DirectProjectReferences(current))
                {
                    if (seen.Add(refPath))
                    {
                        result.Add(refPath);
                        stack.Push(refPath);
                    }
                }
            }
            return result;
        }

        public static string FindProjectFile(string repoRoot, string projectName)
        {
            return Directory
                .EnumerateFiles(repoRoot, projectName + ".csproj", SearchOption.AllDirectories)
                .FirstOrDefault(f => !IsInBuildOutput(f));
        }

        public static bool IsInBuildOutput(string path)
        {
            var sep = Path.DirectorySeparatorChar;
            return path.Contains($"{sep}bin{sep}") || path.Contains($"{sep}obj{sep}");
        }

        public static string ProjectName(string csprojPath) => Path.GetFileNameWithoutExtension(csprojPath);

        public static string Rel(string root, string path) => Path.GetRelativePath(root, path);

        public static bool IsUnderAnyRoot(string path, string repoRoot, params string[] roots)
        {
            var rel = Rel(repoRoot, path).Replace('\\', '/');
            return roots.Any(r => rel.StartsWith(r + "/", StringComparison.OrdinalIgnoreCase));
        }
    }
}
