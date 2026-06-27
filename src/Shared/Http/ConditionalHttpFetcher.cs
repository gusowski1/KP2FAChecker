// Shared KeeRadar infrastructure — synced from KPPasskeyChecker/src/Shared via sync-shared.ps1; do not edit here
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using KeeRadar.Shared.Caching;

namespace KeeRadar.Shared.Http
{
    public sealed class ConditionalHttpFetcher
    {
        // Cap buffered response size so a compromised/hostile endpoint cannot exhaust memory by
        // returning a huge body. The largest real payload is well under 1 MB.
        private const long MaxResponseBytes = 16L * 1024 * 1024;

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
            MaxResponseContentBufferSize = MaxResponseBytes
        };

        private readonly string _userAgent;

        public ConditionalHttpFetcher(string userAgent)
        {
            _userAgent = userAgent;
        }

        public Task<FetchResult> FetchAsync(string url, CacheEntry cached)
        {
            return FetchAsync(url, cached, false);
        }

        /// <summary>
        /// Conditional GET. When <paramref name="binary"/> is true the body is returned as raw
        /// bytes (<see cref="FetchResult.ContentBytes"/>) instead of a decoded string — required
        /// for binary payloads such as OpenPGP ".sig" files, where string decoding would corrupt
        /// the data.
        /// </summary>
        public async Task<FetchResult> FetchAsync(string url, CacheEntry cached, bool binary)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", _userAgent);

                if (cached != null && cached.ETag != null)
                    request.Headers.TryAddWithoutValidation("If-None-Match", cached.ETag);

                HttpResponseMessage response = await _http.SendAsync(request).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotModified)
                    return FetchResult.NotModified();

                if (!response.IsSuccessStatusCode)
                    return FetchResult.Failed(
                        "HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase);

                string etag = null;
                if (response.Headers.ETag != null)
                    etag = response.Headers.ETag.ToString();

                if (binary)
                {
                    byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    return FetchResult.Success(bytes, etag);
                }

                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return FetchResult.Success(content, etag);
            }
            catch (Exception ex)
            {
                return FetchResult.Failed(ex.Message);
            }
        }
    }
}
