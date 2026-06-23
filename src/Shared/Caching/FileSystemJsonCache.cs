using System;
using System.IO;

namespace KPPasskeyChecker.Shared.Caching
{
    public sealed class FileSystemJsonCache : ILocalCache
    {
        private readonly string _directory;

        public FileSystemJsonCache(string directory)
        {
            _directory = directory;
            Directory.CreateDirectory(directory);
        }

        public CacheEntry Read(string key)
        {
            string contentPath = ContentPath(key);
            string metaPath    = MetaPath(key);

            if (!File.Exists(contentPath) || !File.Exists(metaPath))
                return null;

            try
            {
                string content  = File.ReadAllText(contentPath, System.Text.Encoding.UTF8);
                string metaText = File.ReadAllText(metaPath,    System.Text.Encoding.UTF8);

                string etag      = ReadMetaField(metaText, "etag");
                string fetchedAt = ReadMetaField(metaText, "fetchedat");

                DateTimeOffset fetched = DateTimeOffset.MinValue;
                if (!string.IsNullOrEmpty(fetchedAt))
                    DateTimeOffset.TryParse(fetchedAt, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out fetched);

                return new CacheEntry
                {
                    Content   = content,
                    ETag      = etag,
                    FetchedAt = fetched
                };
            }
            catch
            {
                return null;
            }
        }

        public void Write(string key, CacheEntry entry)
        {
            string etag      = entry.ETag ?? string.Empty;
            string fetchedAt = entry.FetchedAt.ToString("O");

            string metaText = "etag=" + EscapeValue(etag) + "\n"
                            + "fetchedat=" + EscapeValue(fetchedAt) + "\n";

            AtomicWrite(ContentPath(key), entry.Content);
            AtomicWrite(MetaPath(key),    metaText);
        }

        public void Invalidate(string key)
        {
            TryDelete(ContentPath(key));
            TryDelete(MetaPath(key));
        }

        private string ContentPath(string key)
        {
            return Path.Combine(_directory, SanitizeKey(key) + ".json");
        }

        private string MetaPath(string key)
        {
            return Path.Combine(_directory, SanitizeKey(key) + ".meta.txt");
        }

        private static string SanitizeKey(string key)
        {
            return string.Concat(key.Split(Path.GetInvalidFileNameChars()));
        }

        private static void AtomicWrite(string path, string content)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content, System.Text.Encoding.UTF8);
            if (File.Exists(path))
                File.Replace(tmp, path, null);  // atomic swap on the same volume
            else
                File.Move(tmp, path);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private static string EscapeValue(string v)
        {
            return v.Replace("\\", "\\\\").Replace("\n", "\\n");
        }

        private static string UnescapeValue(string v)
        {
            return v.Replace("\\n", "\n").Replace("\\\\", "\\");
        }

        private static string ReadMetaField(string text, string fieldName)
        {
            string prefix = fieldName + "=";
            string[] lines = text.Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.TrimEnd('\r');
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return UnescapeValue(trimmed.Substring(prefix.Length));
            }
            return null;
        }
    }
}
