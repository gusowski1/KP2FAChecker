using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using KeePass.UI;
using KeePassLib;
using KP2FAChecker.Data;
using KPPasskeyChecker.Shared.DomainMatching;
using KPPasskeyChecker.Shared.KeePassUi;

namespace KP2FAChecker.UI
{
    public sealed class TfaColumnProvider : ColumnProvider
    {
        // NOTE: intentionally NOT "2FA Support" — that title belongs to the separate third-party
        // plugin "KP2faChecker" (tiuub), which a user may run alongside this one. A distinct title
        // avoids two identical column headers.
        public const string ColumnName = "2FA Methods";

        // The KeePass main-window icon, shown in the entry-detail window's title bar so it looks
        // like a native KeePass dialog. Supplied by the plugin (which has the IPluginHost); may be
        // null, in which case the detail window hides its title-bar icon.
        private readonly Icon _windowIcon;

        public TfaColumnProvider(Icon windowIcon)
        {
            _windowIcon = windowIcon;
        }

        public override string[] ColumnNames
        {
            get { return new string[] { ColumnName }; }
        }

        public override string GetCellData(string strCol, PwEntry pe)
        {
            if (!TfaDirectoryService.IsAvailable) return string.Empty;

            TfaDirectory dir = TfaDirectoryService.Current.Directory;
            if (dir == null) return string.Empty;

            string host = ExtractHost(pe);
            if (host == null) return string.Empty;

            TfaEntry entry = Lookup(dir, host);
            return entry == null ? string.Empty : FormatEntry(entry);
        }

        /// <summary>
        /// KeePass calls <see cref="PerformCellAction"/> on double-click / Enter for this column
        /// when this returns true — and it does so even for an empty cell (the call is gated only on
        /// this flag, not on the cell text). So returning true here is enough to also reach the
        /// "no data" dialog for an unmatched domain.
        /// </summary>
        public override bool SupportsCellAction(string strColumnName)
        {
            return strColumnName == ColumnName;
        }

        public override void PerformCellAction(string strColumnName, PwEntry pe)
        {
            if (strColumnName != ColumnName || pe == null) return;
            ShowDetailDialog(pe);
        }

        /// <summary>
        /// Runs the same check-and-show flow that a double-click / Enter on the "2FA Methods" cell
        /// triggers, and opens the shared entry-detail dialog for the supplied entry. Exposed so the
        /// plugin can reuse the exact same flow from the entry context-menu action — no second
        /// dialog, no duplicated lookup logic.
        /// </summary>
        public void ShowDetailDialog(PwEntry pe)
        {
            if (pe == null) return;

            string host = ExtractHost(pe);
            string domain = host ?? string.Empty;

            EntryDetailModel model;

            if (!TfaDirectoryService.IsAvailable
                || TfaDirectoryService.Current.Directory == null)
            {
                model = TfaDetailModelBuilder.Build(domain, null);
                model.EmptyMessage =
                    "Directory data is not available yet. Open the 2FA Checker settings "
                    + "to check the cache status or refresh now.";
            }
            else
            {
                TfaDirectory dir = TfaDirectoryService.Current.Directory;
                TfaEntry entry = host == null ? null : Lookup(dir, host);

                if (entry != null)
                {
                    // On a match the banner subtitle shows the actually matched directory domain
                    // (entry.Domain), which for a subdomain match can differ from the user's stored
                    // host (e.g. host "mail.google.com" matching directory "google.com"). In the
                    // no-match branch below we deliberately show the user's host instead.
                    model = TfaDetailModelBuilder.Build(entry.Domain, entry);
                }
                else
                {
                    model = TfaDetailModelBuilder.Build(domain, null);
                    model.EmptyMessage = string.IsNullOrEmpty(domain)
                        ? "This entry has no website URL to look up."
                        : "No data found for this domain in the directory.";
                }
            }

            model.WindowIcon = _windowIcon;

            using (EntryDetailForm form = new EntryDetailForm(model))
                form.ShowDialog();
        }

        private static TfaEntry Lookup(TfaDirectory dir, string host)
        {
            foreach (string candidate in DomainCandidateGenerator.GetCandidates(host))
            {
                TfaEntry entry = dir.FindByDomain(candidate);
                if (entry != null) return entry;
            }
            return null;
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

        private static string ExtractHost(PwEntry pe)
        {
            string url = pe.Strings.ReadSafe(PwDefs.UrlField);
            if (string.IsNullOrEmpty(url) || url.Trim().Length == 0) return null;

            try
            {
                if (!url.Contains("://"))
                    url = "https://" + url;
                return new Uri(url).Host;
            }
            catch
            {
                return null;
            }
        }
    }
}
