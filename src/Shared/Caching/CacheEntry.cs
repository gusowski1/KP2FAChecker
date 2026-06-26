// Shared KeeRadar infrastructure — synced from KPPasskeyChecker/src/Shared via sync-shared.ps1; do not edit here
using System;

namespace KeeRadar.Shared.Caching
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
