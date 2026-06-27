// Shared KeeRadar infrastructure — synced from KPPasskeyChecker/src/Shared via sync-shared.ps1; do not edit here
namespace KeeRadar.Shared.Caching
{
    public interface ILocalCache
    {
        CacheEntry Read(string key);
        void Write(string key, CacheEntry entry);
        void Invalidate(string key);
    }
}
