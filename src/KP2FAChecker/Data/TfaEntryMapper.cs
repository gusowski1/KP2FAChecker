using System.Collections;
using System.Collections.Generic;

namespace KP2FAChecker.Data
{
    internal static class TfaEntryMapper
    {
        public static TfaEntry Map(string domain, Dictionary<string, object> data)
        {
            return new TfaEntry
            {
                Domain           = domain,
                Methods          = ParseMethods(GetStringList(data, "methods")),
                CustomSoftware   = GetStringList(data, "custom-software"),
                CustomHardware   = GetStringList(data, "custom-hardware"),
                DocumentationUrl = GetString(data, "documentation"),
                RecoveryUrl      = GetString(data, "recovery"),
                Notes            = GetString(data, "notes")
            };
        }

        private static IReadOnlyList<TfaMethod> ParseMethods(IReadOnlyList<string> tokens)
        {
            var methods = new List<TfaMethod>(tokens.Count);
            foreach (string token in tokens)
            {
                TfaMethod method = TfaMethods.Parse(token);
                // Drop unknown tokens (forward-compat) and de-duplicate.
                if (method != TfaMethod.Unknown && !methods.Contains(method))
                    methods.Add(method);
            }
            return methods;
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            object val;
            if (d.TryGetValue(key, out val))
                return val as string;
            return null;
        }

        private static IReadOnlyList<string> GetStringList(Dictionary<string, object> d, string key)
        {
            object val;
            if (!d.TryGetValue(key, out val)) return new string[0];
            ArrayList list = val as ArrayList;
            if (list == null) return new string[0];

            var result = new List<string>(list.Count);
            foreach (var item in list)
            {
                string s = item as string;
                if (!string.IsNullOrEmpty(s)) result.Add(s);
            }
            return result;
        }
    }
}
