// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Microsoft.AspNetCore.Http;

namespace Febris.UserNode.LogicLayer.Logic.AnalyticsLogic
{
    /// <summary>
    /// Decides which requests are worth an analytics row.
    ///
    /// <para>
    /// T11. <c>LocalAnalyticsMiddleware</c> wrote one database row for EVERY HTTP request with no
    /// filtering whatsoever, on both hosts, into one shared table that has no retention. The two
    /// largest contributors were never analytics in any useful sense:
    /// </para>
    ///
    /// <list type="bullet">
    /// <item>
    /// <b>Container health probes.</b> docker-compose probes <c>/health/ready</c> on each node host
    /// every 15 seconds. Two hosts times four probes a minute is 11,520 rows per day on a node with
    /// ZERO users, forever, and every one of them records the container's own address asking whether
    /// it is alive.
    /// </item>
    /// <item>
    /// <b>Static assets.</b> One page view of the Portal drags in the whole vendored front-end
    /// bundle, so a single human visit wrote dozens of rows for stylesheets, scripts, fonts and
    /// favicons. Measured on the development database, the top paths by row count were
    /// <c>bootstrap.min.css</c>, <c>custom.min.css</c>, <c>favicon.ico</c> and <c>jquery.min.js</c>.
    /// </item>
    /// </list>
    ///
    /// <para>
    /// <b>This filters NOISE, not history.</b> Every request a person actually made is still
    /// recorded, including failures, redirects and unauthenticated attempts, because those are what
    /// the analytics screens exist to show. Nothing already stored is removed: this only stops new
    /// noise, so no existing evidence is lost.
    /// </para>
    /// </summary>
    public static class AnalyticsRequestFilter
    {
        /// <summary>
        /// Static asset extensions. Deliberately explicit rather than "anything with a dot": a real
        /// route can contain a dot (an email address in a path segment, a version string), and
        /// dropping those would lose genuine traffic.
        /// </summary>
        private static readonly string[] StaticAssetExtensions =
        {
            ".css", ".js", ".map",
            ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".webp", ".bmp",
            ".woff", ".woff2", ".ttf", ".eot", ".otf",
        };

        /// <summary>
        /// True when this request should produce an analytics row.
        ///
        /// <para>
        /// Health endpoints are matched by prefix because the node serves both <c>/health/live</c>
        /// and <c>/health/ready</c>, and any future sibling should be excluded by default rather
        /// than by being remembered.
        /// </para>
        /// </summary>
        public static bool ShouldRecord(PathString path)
        {
            if (!path.HasValue)
            {
                return true;
            }

            string value = path.Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (value.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (string extension in StaticAssetExtensions)
            {
                if (value.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
