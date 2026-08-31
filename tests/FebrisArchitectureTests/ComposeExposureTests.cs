// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Febris.ArchitectureTests
{
    /// <summary>
    /// Build-time guard on what the bundled compose stack exposes to the network.
    ///
    /// <para>
    /// AUDIT H-56, a cut line A publication blocker. <c>docker-compose.yml</c> published the node
    /// API on plain HTTP to every interface, and <c>SELF_HOSTING.md</c> named that as the FIRST
    /// place to point the PC launcher and mobile Server. The API is what carries device JWTs, the
    /// <c>Hardware.PhysicalLicense</c> credential and xAPI learner records, so all of it crossed the
    /// LAN in cleartext. Owner ruling 2026-08-09: bind it to loopback by default and make LAN
    /// exposure an explicit, documented opt-in via <c>NODE_API_HTTP_BIND</c>.
    /// </para>
    ///
    /// <para>
    /// This is a one-character regression away at all times -- deleting <c>127.0.0.1</c> from a port
    /// mapping looks like tidying and silently re-opens the hole -- and compose is not covered by
    /// any other test. Parsing the file is how this project's guards already work (the csproj notes
    /// it deliberately holds no ProjectReference items).
    /// </para>
    /// </summary>
    public class ComposeExposureTests
    {
        private static string ComposePath()
        {
            return Path.Combine(ProjectGraph.FindRepoRoot(), "docker-compose.yml");
        }

        /// <summary>
        /// Published port mappings, comments and blanks stripped. A compose ports entry looks like
        /// <c>- "HOST:CONTAINER"</c> or <c>- "BIND:HOST:CONTAINER"</c>.
        /// </summary>
        private static List<string> PublishedPorts()
        {
            List<string> ports = new List<string>();
            bool inPorts = false;

            foreach (string raw in File.ReadAllLines(ComposePath()))
            {
                string line = raw.Trim();

                if (line.StartsWith("#", StringComparison.Ordinal) || line.Length == 0)
                {
                    continue;
                }

                if (line == "ports:")
                {
                    inPorts = true;
                    continue;
                }

                if (inPorts)
                {
                    if (line.StartsWith("- ", StringComparison.Ordinal))
                    {
                        ports.Add(line.Substring(2).Trim().Trim('"', '\''));
                        continue;
                    }

                    inPorts = false;
                }
            }

            return ports;
        }

        /// <summary>
        /// Replaces every <c>${...}</c> with a single placeholder character, so structural colons
        /// in a mapping can be counted without the ones inside a compose default value.
        /// </summary>
        private static string StripVariableExpansions(string mapping)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(mapping.Length);
            int depth = 0;

            for (int i = 0; i < mapping.Length; i++)
            {
                if (i + 1 < mapping.Length && mapping[i] == '$' && mapping[i + 1] == '{')
                {
                    if (depth == 0)
                    {
                        builder.Append('V');
                    }

                    depth++;
                    i++;
                    continue;
                }

                if (depth > 0)
                {
                    if (mapping[i] == '}')
                    {
                        depth--;
                    }

                    continue;
                }

                builder.Append(mapping[i]);
            }

            return builder.ToString();
        }

        [Fact]
        public void StripVariableExpansions_RemovesTheColonInsideAComposeDefault()
        {
            // Pins the helper above, because the guard that uses it silently under-reports without it.
            Assert.Equal("V:80", StripVariableExpansions("${NODE_API_HTTP_PORT:-8081}:80"));
            Assert.Equal("V:V:80", StripVariableExpansions("${A:-1}:${B:-2}:80"));
            Assert.Equal("127.0.0.1:8081:80", StripVariableExpansions("127.0.0.1:8081:80"));
        }

        [Fact]
        public void ComposeFileIsPresentAndPublishesSomething()
        {
            // Guards the parser itself: every assertion below passes vacuously on an empty list.
            Assert.True(File.Exists(ComposePath()), "docker-compose.yml not found at " + ComposePath());
            Assert.True(PublishedPorts().Count > 0, "parsed no published ports, so the guards below prove nothing");
        }

        [Fact]
        public void ThePlainHttpApiPortIsBoundToLoopbackByDefault()
        {
            List<string> apiPorts = PublishedPorts()
                .Where(p => p.Contains("NODE_API_HTTP_PORT", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.True(apiPorts.Count == 1,
                "expected exactly one published mapping for the plain-HTTP API port, found " + apiPorts.Count);

            string mapping = apiPorts[0];

            Assert.True(
                mapping.Contains("NODE_API_HTTP_BIND", StringComparison.OrdinalIgnoreCase),
                "the plain-HTTP API port must bind through NODE_API_HTTP_BIND so operators can opt in explicitly, but the mapping was: " + mapping);

            Assert.True(
                mapping.Contains(":-127.0.0.1", StringComparison.Ordinal),
                "NODE_API_HTTP_BIND must DEFAULT to 127.0.0.1 -- the API carries device tokens, the hardware credential and learner records over plain HTTP. Mapping was: " + mapping);
        }

        [Fact]
        public void NoServicePublishesPlainHttpToEveryInterface()
        {
            // A mapping with no bind part, or an explicit 0.0.0.0, reaches the whole LAN. The HTTPS
            // proxy port is the one legitimate exception: TLS is the entire point of it.
            List<string> offenders = new List<string>();

            foreach (string mapping in PublishedPorts())
            {
                if (mapping.Contains("NODE_HTTPS_PORT", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Count colons OUTSIDE ${...}. A compose default like ${NODE_API_HTTP_PORT:-8081}
                // contains its own colon, and counting it made this guard think a bind part was
                // present when it was not -- caught by mutation-testing the guard rather than by
                // reading it.
                string skeleton = StripVariableExpansions(mapping);

                bool explicitlyAllInterfaces = skeleton.StartsWith("0.0.0.0:", StringComparison.Ordinal);
                bool hasBindPart = skeleton.Count(c => c == ':') >= 2;

                if (explicitlyAllInterfaces || !hasBindPart)
                {
                    offenders.Add(mapping);
                }
            }

            Assert.True(offenders.Count == 0,
                "these compose mappings publish plain HTTP to every interface:\n  " + string.Join("\n  ", offenders));
        }
    }
}
