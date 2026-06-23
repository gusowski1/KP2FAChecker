using System.Collections.Generic;
using System.Text;
using KeePass.UI;
using KeePassLib;
using KP2FAChecker.Data;
using KPPasskeyChecker.Shared.DomainMatching;

namespace KP2FAChecker.UI
{
    public sealed class TfaColumnProvider : ColumnProvider
    {
        // NOTE: intentionally NOT "2FA Support" — that title belongs to the separate third-party
        // plugin "KP2faChecker" (tiuub), which a user may run alongside this one. A distinct title
        // avoids two identical column headers.
        public const string ColumnName = "2FA Methods";

        public override string[] ColumnNames
        {
            get { return new string[] { ColumnName }; }
        }

        public override string GetCellData(string strCol, PwEntry pe)
        {
            if (!TfaDirectoryService.IsAvailable) return string.Empty;

            TfaDirectory dir = TfaDirectoryService.Current.Directory;
            if (dir == null) return string.Empty;

            string url = pe.Strings.ReadSafe(KeePassLib.PwDefs.UrlField);
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            string host = ExtractHost(url);
            if (host == null) return string.Empty;

            foreach (string candidate in DomainCandidateGenerator.GetCandidates(host))
            {
                TfaEntry entry = dir.FindByDomain(candidate);
                if (entry == null) continue;
                return FormatEntry(entry);
            }

            return string.Empty;
        }

        /// <summary>
        /// A concise, human-readable summary of an entry's documented 2FA methods,
        /// e.g. "TOTP, Security Key, SMS". Custom software/hardware product names are appended
        /// in parentheses when present (e.g. "Hardware (YubiKey)"). Blank when none.
        /// </summary>
        internal static string FormatEntry(TfaEntry entry)
        {
            if (entry == null || !entry.SupportsAny) return string.Empty;

            var parts = new List<string>(entry.Methods.Count);
            foreach (TfaMethod method in entry.Methods)
            {
                string label = TfaMethods.Label(method);
                if (label.Length == 0) continue;

                if (method == TfaMethod.CustomSoftware)
                    label = AppendNames(label, entry.CustomSoftware);
                else if (method == TfaMethod.CustomHardware)
                    label = AppendNames(label, entry.CustomHardware);

                parts.Add(label);
            }

            return string.Join(", ", parts.ToArray());
        }

        private static string AppendNames(string label, IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0) return label;

            var sb = new StringBuilder(label);
            sb.Append(" (");
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(names[i]);
            }
            sb.Append(")");
            return sb.ToString();
        }

        private static string ExtractHost(string url)
        {
            try
            {
                if (!url.Contains("://"))
                    url = "https://" + url;
                return new System.Uri(url).Host;
            }
            catch
            {
                return null;
            }
        }
    }
}
