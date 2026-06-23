using System;

namespace KPPasskeyChecker.Shared.Caching
{
    public sealed class CacheEntry
    {
        public string Content { get; set; }
        public string ETag { get; set; }
        public DateTimeOffset FetchedAt { get; set; }

        public CacheEntry()
        {
            Content = string.Empty;
        }
    }
}
