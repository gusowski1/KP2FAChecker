using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using KPPasskeyChecker.Shared.Caching;
using KPPasskeyChecker.Shared.Http;
using KPPasskeyChecker.Shared.Pgp;

namespace KP2FAChecker.Data
{
    internal sealed class TfaApiClient
    {
        private readonly ConditionalHttpFetcher _fetcher;
        private OpenPgpSignatureVerifier _verifier;

        public TfaApiClient(string userAgent)
        {
            _fetcher = new ConditionalHttpFetcher(userAgent);
        }

        public Task<TfaDataResult> FetchAsync(
            TfaDataScope scope,
            ILocalCache cache,
            bool force = false)
        {
            return FetchAsync(scope, false, cache, force);
        }

        public Task<TfaDataResult> FetchAsync(
            TfaDataScope scope,
            bool verify,
            ILocalCache cache,
            bool force = false)
        {
            return verify
                ? FetchVerifiedAsync(scope, cache, force)
                : FetchPlainAsync(scope, cache, force);
        }

        private async Task<TfaDataResult> FetchPlainAsync(
            TfaDataScope scope,
            ILocalCache cache,
            bool force)
        {
            string cacheKey = TfaEndpoints.CacheKey(scope);
            string url      = TfaEndpoints.ForScope(scope);

            CacheEntry cached = cache.Read(cacheKey);

            FetchResult result = await _fetcher
                .FetchAsync(url, force ? null : cached)
                .ConfigureAwait(false);

            switch (result.Outcome)
            {
                case FetchOutcome.Success:
                {
                    string parseError;
                    TfaDirectory dir = TryBuildDirectory(result.Content, out parseError);
                    if (dir == null)
                        return FallbackOrError(cached, "Failed to parse API response: " + parseError);

                    cache.Write(cacheKey, NewEntry(result.Content, result.ETag));
                    return Fresh(dir);
                }

                case FetchOutcome.NotModified:
                    return FromCacheAfterNotModified(cache, cacheKey, cached);

                default:
                    return FallbackOrError(cached, result.ErrorMessage ?? "Unknown error.");
            }
        }

        private async Task<TfaDataResult> FetchVerifiedAsync(
            TfaDataScope scope,
            ILocalCache cache,
            bool force)
        {
            string cacheKey = TfaEndpoints.SignedCacheKey(scope);
            string url      = TfaEndpoints.SignatureForScope(scope);

            CacheEntry cached = cache.Read(cacheKey);

            OpenPgpSignatureVerifier verifier;
            try
            {
                verifier = GetVerifier();
            }
            catch (Exception ex)
            {
                return FallbackOrError(cached, "Signature verifier unavailable: " + ex.Message);
            }

            FetchResult result = await _fetcher
                .FetchAsync(url, force ? null : cached, true)
                .ConfigureAwait(false);

            switch (result.Outcome)
            {
                case FetchOutcome.Success:
                {
                    PgpVerificationResult verification = verifier.Verify(result.ContentBytes);
                    if (!verification.IsValid)
                        return FallbackOrError(cached, "PGP verification failed: " + verification.Error);

                    string json = Encoding.UTF8.GetString(verification.SignedContent);
                    string parseError;
                    TfaDirectory dir = TryBuildDirectory(json, out parseError);
                    if (dir == null)
                        return FallbackOrError(cached, "Failed to parse verified API response: " + parseError);

                    // Only verified JSON is ever written under the signed cache key.
                    cache.Write(cacheKey, NewEntry(json, result.ETag));
                    return Fresh(dir);
                }

                case FetchOutcome.NotModified:
                    // Cached content under the signed key was verified when it was written.
                    return FromCacheAfterNotModified(cache, cacheKey, cached);

                default:
                    return FallbackOrError(cached, result.ErrorMessage ?? "Unknown error.");
            }
        }

        private OpenPgpSignatureVerifier GetVerifier()
        {
            if (_verifier == null)
                _verifier = TfaTrustAnchor.CreateVerifier();
            return _verifier;
        }

        private static TfaDataResult FromCacheAfterNotModified(ILocalCache cache, string cacheKey, CacheEntry cached)
        {
            if (cached != null)
            {
                CacheEntry updated = NewEntry(cached.Content, cached.ETag);
                cache.Write(cacheKey, updated);
                cached = updated;
            }

            string parseError;
            TfaDirectory dir = cached != null ? TryBuildDirectory(cached.Content, out parseError) : null;
            return dir != null
                ? new TfaDataResult { Directory = dir, IsFromCache = true, FetchedAt = cached.FetchedAt }
                : new TfaDataResult { ErrorMessage = "Cache missing after 304." };
        }

        private static CacheEntry NewEntry(string content, string etag)
        {
            return new CacheEntry
            {
                Content   = content,
                ETag      = etag,
                FetchedAt = DateTimeOffset.UtcNow
            };
        }

        private static TfaDataResult Fresh(TfaDirectory dir)
        {
            return new TfaDataResult
            {
                Directory   = dir,
                IsFromCache = false,
                FetchedAt   = DateTimeOffset.UtcNow
            };
        }

        private static TfaDataResult FallbackOrError(CacheEntry cached, string error)
        {
            if (cached == null)
                return new TfaDataResult { ErrorMessage = error };

            string parseError;
            TfaDirectory dir = TryBuildDirectory(cached.Content, out parseError);
            if (dir == null)
                return new TfaDataResult { ErrorMessage = error };

            return new TfaDataResult
            {
                Directory    = dir,
                IsFromCache  = true,
                IsStale      = true,
                FetchedAt    = cached.FetchedAt,
                ErrorMessage = error
            };
        }

        private static TfaDirectory TryBuildDirectory(string json, out string error)
        {
            if (string.IsNullOrEmpty(json))
            {
                error = "empty response";
                return null;
            }
            try
            {
                var jss = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
                var raw = jss.Deserialize<Dictionary<string, object>>(json);
                if (raw == null)
                {
                    error = "response did not deserialize to an object";
                    return null;
                }
                error = null;
                return TfaDirectory.Build(raw);
            }
            catch (Exception ex)
            {
                // Surface the cause (e.g. malformed JSON or an oversized payload) instead of
                // discarding it silently. The fail-soft cache fallback is unchanged: callers still
                // fall back to cached data and only attach this message for diagnostics.
                error = ex.Message;
                return null;
            }
        }
    }
}
