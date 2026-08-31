// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.ViewModels;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    /// <summary>
    /// Read-only fetch of a PUBLIC client-software distribution feed (the
    /// "optional hub pull" pointed at a static feed instead of a hub).
    /// <para>
    /// This is the only outbound-HTTP query in the EndUser DAL that does NOT go through
    /// <c>APIRequestFactory</c>, and the deviation is deliberate. Three reasons, in order of weight:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>It masks every error.</b> <c>APIRequestFactory.MakeStringRequest</c> lets
    /// <c>WebRequest.GetResponse()</c> throw on any 4xx or 5xx and returns
    /// <c>(string.Empty, HttpStatusCode.InternalServerError)</c> from its catch, so no caller can tell
    /// a 404 from a 401 from a network failure. A sync that cannot tell "the
    /// feed does not exist" from "the feed is temporarily unreachable" cannot report anything useful.
    /// </item>
    /// <item>
    /// <b>It buffers whole responses into strings.</b> Artifacts here are tens of megabytes, and this
    /// runs in a server process serving other requests.
    /// </item>
    /// <item>
    /// <b>None of its auth machinery applies.</b> The feed is anonymous and unauthenticated by
    /// design, so there is no Bearer token, no license header and no token-renewal retry to reuse.
    /// </item>
    /// </list>
    /// <para>
    /// <b>Auth-island boundary.</b> This adds outbound HTTP to the EndUser tier, so it is worth being
    /// explicit that it does not couple the node to the central SSO or to central data. The target is
    /// a PUBLIC static document at a URL the node's own admin supplies per call, with no Febris
    /// credential, no license key and no hub identity involved. It moves in the direction the
    /// invariant wants: a node with NO hub configured can still obtain its client software, and a feed
    /// that is unreachable degrades to a reported failure while the node keeps operating. Nothing here
    /// may grow a dependency on a Febris-operated endpoint.
    /// </para>
    /// <para>
    /// One process-wide <see cref="HttpClient"/> over one handler, matching the reasoning already
    /// recorded on <c>APIRequestFactory</c>'s shared handler (HIGH-7): a per-call client with its own
    /// handler risks socket exhaustion. When the <c>IHttpClientFactory</c> migration (SCBA-M4) lands,
    /// this becomes a typed client and the static goes away.
    /// </para>
    /// </summary>
    public interface IPackageFeedQueries
    {
        /// <summary>
        /// Fetch and deserialize a feed manifest. Returns null when the feed cannot be read or is not
        /// parseable, having logged why. Never throws for an unreachable or malformed feed, because a
        /// bad feed is an expected operational condition rather than a server fault.
        /// </summary>
        Task<PackageFeedManifest> GetManifest(string manifestUrl);

        /// <summary>
        /// Open an artifact for reading. The caller owns the stream and must dispose it. Returns null
        /// when the artifact cannot be fetched.
        /// <para>
        /// Streamed, not buffered: the response is returned as soon as the headers arrive, so a 60 MB
        /// APK never lands in memory in one piece.
        /// </para>
        /// </summary>
        Task<Stream> OpenArtifact(string artifactUrl);
    }

    /// <inheritdoc />
    public class PackageFeedQueries : IPackageFeedQueries
    {
        /// <summary>
        /// A manifest is an index, not a payload. Anything larger than this is not a manifest, and
        /// reading it into a string would be the vulnerability rather than the parse that follows.
        /// </summary>
        private const int MaxManifestBytes = 4 * 1024 * 1024;

        private static readonly HttpClient _client = CreateClient();

        private static HttpClient CreateClient()
        {
            // Redirects are followed because GitHub Releases redirects downloads to a CDN host.
            // AllowAutoRedirect will not follow an https-to-http downgrade, which is the property that
            // matters: a feed served over https must not be silently demoted mid-fetch.
            HttpClientHandler handler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };

            HttpClient client = new HttpClient(handler, disposeHandler: false)
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Febris-Node-PackageFeedSync/1.0");
            return client;
        }

        /// <summary>
        /// Reject anything that is not absolute https up front. Plain http would let whoever is on the
        /// path rewrite BOTH the artifact and the checksum that is supposed to detect a rewritten
        /// artifact, which makes the integrity check theatre rather than protection.
        /// </summary>
        private static bool IsAcceptableUrl(string url, out Uri parsed)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }
            if (!Uri.TryCreate(url, UriKind.Absolute, out parsed))
            {
                return false;
            }
            return parsed.Scheme == Uri.UriSchemeHttps;
        }

        /// <inheritdoc />
        public async Task<PackageFeedManifest> GetManifest(string manifestUrl)
        {
            try
            {
                if (!IsAcceptableUrl(manifestUrl, out Uri uri))
                {
                    Febris.SharedServices.FebrisLog.Warn(
                        "Package feed sync: refusing manifest URL '" + manifestUrl +
                        "'. An absolute https URL is required.");
                    return null;
                }

                using (HttpResponseMessage response =
                    await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Febris.SharedServices.FebrisLog.Warn(
                            "Package feed sync: manifest fetch returned " + (int)response.StatusCode +
                            " from " + uri);
                        return null;
                    }

                    if (response.Content.Headers.ContentLength > MaxManifestBytes)
                    {
                        Febris.SharedServices.FebrisLog.Warn(
                            "Package feed sync: manifest at " + uri + " declares " +
                            response.Content.Headers.ContentLength + " bytes, above the " +
                            MaxManifestBytes + " byte cap. Refusing.");
                        return null;
                    }

                    string json;
                    using (Stream content = await response.Content.ReadAsStreamAsync())
                    {
                        // Cap the read itself as well as the declared length, because a server is free
                        // to omit Content-Length or lie about it.
                        json = await ReadCappedText(content, MaxManifestBytes);
                    }

                    if (json == null)
                    {
                        Febris.SharedServices.FebrisLog.Warn(
                            "Package feed sync: manifest at " + uri + " exceeded the " +
                            MaxManifestBytes + " byte cap while reading. Refusing.");
                        return null;
                    }

                    return JsonConvert.DeserializeObject<PackageFeedManifest>(json);
                }
            }
            catch (Exception ex)
            {
                // An unreachable or malformed feed is an operational condition, not a server fault, so
                // this is logged and reported rather than thrown.
                Febris.SharedServices.FebrisLog.Error(ex);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<Stream> OpenArtifact(string artifactUrl)
        {
            try
            {
                if (!IsAcceptableUrl(artifactUrl, out Uri uri))
                {
                    Febris.SharedServices.FebrisLog.Warn(
                        "Package feed sync: refusing artifact URL '" + artifactUrl +
                        "'. An absolute https URL is required.");
                    return null;
                }

                // Deliberately NOT wrapped in `using`: the response owns the stream, so disposing it
                // here would close the stream the caller is about to read. HttpResponseMessage is
                // disposed when the returned stream is disposed.
                HttpResponseMessage response =
                    await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    Febris.SharedServices.FebrisLog.Warn(
                        "Package feed sync: artifact fetch returned " + (int)response.StatusCode +
                        " from " + uri);
                    response.Dispose();
                    return null;
                }

                return await response.Content.ReadAsStreamAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                return null;
            }
        }

        /// <summary>
        /// Read at most <paramref name="cap"/> bytes as UTF-8 text. Returns null when the stream is
        /// longer, rather than a truncated document that would then fail to parse for a misleading
        /// reason.
        /// </summary>
        private static async Task<string> ReadCappedText(Stream source, int cap)
        {
            byte[] buffer = new byte[8192];
            using (MemoryStream accumulated = new MemoryStream())
            {
                int read;
                while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    if (accumulated.Length + read > cap)
                    {
                        return null;
                    }
                    accumulated.Write(buffer, 0, read);
                }
                return System.Text.Encoding.UTF8.GetString(accumulated.ToArray());
            }
        }
    }
}
