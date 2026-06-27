using System.Collections.Generic;
using System.Text;
using KP2FAChecker.Data;
using KeeRadar.Shared.KeePassUi;

namespace KP2FAChecker.UI
{
    /// <summary>
    /// Maps a <see cref="TfaEntry"/> (or the absence of one) to a plugin-agnostic
    /// <see cref="EntryDetailModel"/> for the shared detail window. A row is added only when its
    /// field is actually present, so missing fields are omitted entirely (no placeholders).
    /// </summary>
    internal static class TfaDetailModelBuilder
    {
        private const string Attribution =
            "Data sourced from 2FA Directory by 2factorauth. (MIT)";

        /// <summary>
        /// Security-priority display order for the methods. <see cref="TfaMethod.CustomSoftware"/>
        /// and <see cref="TfaMethod.CustomHardware"/> are only listed when their product list is
        /// non-empty (handled in <see cref="BuildMethodsText"/>).
        /// </summary>
        private static readonly TfaMethod[] DisplayOrder =
        {
            TfaMethod.Totp,
            TfaMethod.U2f,
            TfaMethod.Email,
            TfaMethod.Sms,
            TfaMethod.Call,
            TfaMethod.CustomSoftware,
            TfaMethod.CustomHardware
        };

        public static EntryDetailModel Build(string domain, TfaEntry entry)
        {
            EntryDetailModel model = new EntryDetailModel();
            model.Domain = domain;
            model.BannerTitle = "2FA Details";
            model.Attribution = Attribution;

            if (entry == null)
                return model; // EmptyMessage left null → form shows the default "no data" text.

            List<EntryDetailRow> rows = new List<EntryDetailRow>();

            string methods = BuildMethodsText(entry);
            if (methods.Length > 0)
                rows.Add(new TextDetailRow("2FA methods", methods));

            if (!string.IsNullOrEmpty(entry.DocumentationUrl))
                rows.Add(new LinkDetailRow("Documentation", entry.DocumentationUrl));

            if (!string.IsNullOrEmpty(entry.RecoveryUrl))
                rows.Add(new LinkDetailRow("Recovery", entry.RecoveryUrl));

            if (!string.IsNullOrEmpty(entry.Notes))
                rows.Add(new NotesDetailRow("Notes", entry.Notes));

            model.Rows = rows;
            return model;
        }

        /// <summary>
        /// Builds the comma-joined method list in security-priority order. Custom software/hardware
        /// product names are appended in parentheses; those two methods are listed only when their
        /// product list is non-empty.
        /// </summary>
        internal static string BuildMethodsText(TfaEntry entry)
        {
            if (entry == null || entry.Methods == null) return string.Empty;

            HashSet<TfaMethod> present = new HashSet<TfaMethod>(entry.Methods);
            List<string> parts = new List<string>();

            foreach (TfaMethod method in DisplayOrder)
            {
                if (!present.Contains(method)) continue;

                if (method == TfaMethod.CustomSoftware)
                {
                    if (IsEmpty(entry.CustomSoftware)) continue;
                    parts.Add(AppendNames(TfaMethods.Label(method), entry.CustomSoftware));
                }
                else if (method == TfaMethod.CustomHardware)
                {
                    if (IsEmpty(entry.CustomHardware)) continue;
                    parts.Add(AppendNames(TfaMethods.Label(method), entry.CustomHardware));
                }
                else
                {
                    string label = TfaMethods.Label(method);
                    if (label.Length > 0) parts.Add(label);
                }
            }

            return string.Join(", ", parts.ToArray());
        }

        private static bool IsEmpty(IReadOnlyList<string> names)
        {
            return names == null || names.Count == 0;
        }

        private static string AppendNames(string label, IReadOnlyList<string> names)
        {
            StringBuilder sb = new StringBuilder(label);
            sb.Append(" (");
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(names[i]);
            }
            sb.Append(")");
            return sb.ToString();
        }
    }
}
