// Shared KeeRadar infrastructure — synced from KPPasskeyChecker/src/Shared via sync-shared.ps1; do not edit here
using System;
using System.Collections.Generic;

namespace KeeRadar.Shared.DomainMatching
{
    /// <summary>
    /// Minimal Public Suffix List implementation.
    /// Supports exact rules, wildcard rules (*.foo) and exception rules (!bar.foo).
    /// </summary>
    public sealed class PublicSuffixList
    {
        private readonly HashSet<string> _exact;      // e.g. "co.uk"
        private readonly HashSet<string> _wildcards;  // root of *.root: "kawasaki.jp" for "*.kawasaki.jp"
        private readonly HashSet<string> _exceptions; // without "!": "metro.tokyo.jp"

        private PublicSuffixList(
            HashSet<string> exact,
            HashSet<string> wildcards,
            HashSet<string> exceptions)
        {
            _exact      = exact;
            _wildcards  = wildcards;
            _exceptions = exceptions;
        }

        public static PublicSuffixList Parse(string content)
        {
            var exact      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var wildcards  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var exceptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawLine in content.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("!", StringComparison.Ordinal))
                    exceptions.Add(line.Substring(1).ToLowerInvariant());
                else if (line.StartsWith("*.", StringComparison.Ordinal))
                    wildcards.Add(line.Substring(2).ToLowerInvariant());
                else
                    exact.Add(line.ToLowerInvariant());
            }

            return new PublicSuffixList(exact, wildcards, exceptions);
        }

        /// <summary>
        /// Returns the registrable domain (eTLD+1) for a hostname,
        /// or null if the hostname is itself a public suffix.
        /// </summary>
        public string GetRegistrableDomain(string hostname)
        {
            if (string.IsNullOrEmpty(hostname)) return null;
            hostname = hostname.ToLowerInvariant();

            string[] labels = hostname.Split('.');
            if (labels.Length < 2) return null;

            int etldLen = FindEtldLength(labels);
            int regLen  = etldLen + 1;

            if (regLen > labels.Length) return null; // hostname is itself a public suffix

            return string.Join(".", labels, labels.Length - regLen, regLen);
        }

        // Returns the number of labels forming the effective TLD for the given label array.
        private int FindEtldLength(string[] labels)
        {
            for (int start = 0; start < labels.Length; start++)
            {
                int    suffixLen = labels.Length - start;
                string suffix    = string.Join(".", labels, start, suffixLen);

                // Exception rule: !suffix overrides a wildcard, eTLD = one label shorter
                if (_exceptions.Contains(suffix))
                    return suffixLen - 1;

                // Wildcard rule: *.parent matches this suffix
                if (start + 1 < labels.Length)
                {
                    string parent = string.Join(".", labels, start + 1, labels.Length - start - 1);
                    if (_wildcards.Contains(parent))
                        return suffixLen;
                }

                // Exact rule
                if (_exact.Contains(suffix))
                    return suffixLen;
            }

            return 1; // default: single rightmost label is the TLD
        }
    }
}
