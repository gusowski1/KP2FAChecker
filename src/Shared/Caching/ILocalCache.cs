namespace KPPasskeyChecker.Shared.Caching
{
    public interface ILocalCache
    {
        CacheEntry Read(string key);
        void Write(string key, CacheEntry entry);
        void Invalidate(string key);
    }
}
