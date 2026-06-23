using System;
using System.Collections.Generic;

namespace KP2FAChecker.Data
{
    public sealed class TfaDirectory
    {
        private readonly Dictionary<string, TfaEntry> _index;

        public int Count { get; private set; }

        private TfaDirectory(Dictionary<string, TfaEntry> index, int count)
        {
            _index = index;
            Count  = count;
        }

        public TfaEntry FindByDomain(string domain)
        {
            TfaEntry entry;
            return _index.TryGetValue(domain.ToLowerInvariant(), out entry) ? entry : null;
        }

        internal static TfaDirectory Build(Dictionary<string, object> raw)
        {
            var index = new Dictionary<string, TfaEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in raw)
            {
                Dictionary<string, object> data = kvp.Value as Dictionary<string, object>;
                if (data == null) continue;

                TfaEntry entry = TfaEntryMapper.Map(kvp.Key, data);

                // v4 marks disabled sites with an empty object ("domain": {}) — and an entry whose
                // "methods" is absent/empty means "no documented 2FA". Skip both so they never show
                // as supported. (The v4 API does NOT expose "additional-domains"; each alias is its
                // own top-level key, so no alias expansion is needed.)
                if (!entry.SupportsAny) continue;

                index[kvp.Key.ToLowerInvariant()] = entry;
            }

            return new TfaDirectory(index, index.Count);
        }
    }
}
