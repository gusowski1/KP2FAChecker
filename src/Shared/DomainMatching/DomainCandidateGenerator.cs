// Shared KeeRadar infrastructure — synced from KPPasskeyChecker/src/Shared via sync-shared.ps1; do not edit here
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace KeeRadar.Shared.DomainMatching
{
    /// <summary>
    /// Walks from a full hostname down to the registrable domain (eTLD+1),
    /// yielding candidates most-specific first.
    /// </summary>
    public static class DomainCandidateGenerator
    {
        private const string PslUrl     = "https://publicsuffix.org/list/public_suffix_list.dat";
        private static readonly TimeSpan PslCacheTtl = TimeSpan.FromDays(7);

        // Written once by the background loader, read on the UI thread for every column cell.
        // A volatile reference suffices (atomic publish + visibility) and avoids per-cell locking.
        private static volatile PublicSuffixList _psl;

        /// <summary>
        /// Call once on a background thread during plugin startup.
        /// Downloads and caches the PSL; falls back to 2-label minimum on failure.
        /// </summary>
        public static void InitializeAsync(string cacheDirectory)
        {
            Task.Run(async () =>
            {
                try
                {
                    string cacheFile = Path.Combine(cacheDirectory, "public_suffix_list.dat");
                    string content   = await LoadPslAsync(cacheFile).ConfigureAwait(false);
                    _psl = PublicSuffixList.Parse(content);
                }
                catch { /* Use 2-label fallback */ }
            });
        }

        private static async Task<string> LoadPslAsync(string cacheFile)
        {
            if (File.Exists(cacheFile) &&
                (DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile)) < PslCacheTtl)
            {
                return File.ReadAllText(cacheFile, System.Text.Encoding.UTF8);
            }

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 16L * 1024 * 1024 })
            {
                string content = await http.GetStringAsync(PslUrl).ConfigureAwait(false);
                string tmp = cacheFile + ".tmp";
                File.WriteAllText(tmp, content, System.Text.Encoding.UTF8);
                // Guarded swap: on .NET 4.8 File.Move throws IOException when the destination
                // already exists, which would make every refresh after the 7-day TTL fail. Mirror
                // the atomic pattern in FileSystemJsonCache.AtomicWrite.
                if (File.Exists(cacheFile))
                    File.Replace(tmp, cacheFile, null);  // atomic swap on the same volume
                else
                    File.Move(tmp, cacheFile);
                return content;
            }
        }

        public static IEnumerable<string> GetCandidates(string rawHost)
        {
            if (string.IsNullOrWhiteSpace(rawHost))
                yield break;

            string host = NormalizeHost(rawHost);
            if (string.IsNullOrEmpty(host))
                yield break;

            string[] labels = host.Split('.');
            if (labels.Length < 2)
                yield break;

            // eTLD+1 is the stopping point for candidate generation
            string registrable = null;
            PublicSuffixList psl = _psl;
            if (psl != null)
                registrable = psl.GetRegistrableDomain(host);

            for (int i = 0; i < labels.Length - 1; i++)
            {
                string candidate = string.Join(".", labels, i, labels.Length - i);
                yield return candidate;

                if (registrable != null && candidate == registrable)
                    yield break;

                // Fallback: stop when only one label would remain
                if (labels.Length - i == 2)
                    yield break;
            }
        }

        private static string NormalizeHost(string host)
        {
            host = host.Trim().ToLowerInvariant();
            if (host.StartsWith("www.", StringComparison.Ordinal))
                host = host.Substring(4);
            return host;
        }
    }
}
