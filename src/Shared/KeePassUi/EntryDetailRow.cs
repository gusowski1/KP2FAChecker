using System.Collections.Generic;

namespace KPPasskeyChecker.Shared.KeePassUi
{
    /// <summary>
    /// One typed row in the entry detail view. A plugin maps its own entry model to a list of these
    /// rows so the shared <see cref="EntryDetailForm"/> stays free of any plugin-specific type
    /// (which keeps the whole <c>Shared</c> tree byte-identical across plugins). Concrete row kinds
    /// are <see cref="TextDetailRow"/>, <see cref="LinkDetailRow"/> and <see cref="NotesDetailRow"/>.
    /// </summary>
    public abstract class EntryDetailRow
    {
        /// <summary>The bold field label shown to the left of the value (e.g. "Documentation").</summary>
        public string Label { get; private set; }

        /// <summary>
        /// An accessible, spelled-out description of the value for screen readers (e.g. regions and
        /// methods read as words, not raw tokens). Falls back to the label when not set.
        /// </summary>
        public string AccessibleValue { get; private set; }

        protected EntryDetailRow(string label, string accessibleValue)
        {
            Label = label ?? string.Empty;
            AccessibleValue = string.IsNullOrEmpty(accessibleValue) ? Label : accessibleValue;
        }
    }

    /// <summary>A label followed by a plain, read-only text value (e.g. "MFA → Allowed").</summary>
    public sealed class TextDetailRow : EntryDetailRow
    {
        public string Value { get; private set; }

        public TextDetailRow(string label, string value)
            : this(label, value, value)
        {
        }

        public TextDetailRow(string label, string value, string accessibleValue)
            : base(label, accessibleValue)
        {
            Value = value ?? string.Empty;
        }
    }

    /// <summary>
    /// A label followed by a clickable URL (rendered as a <c>LinkLabel</c>) plus small
    /// <c>Open</c> and <c>Copy</c> buttons.
    /// </summary>
    public sealed class LinkDetailRow : EntryDetailRow
    {
        public string Url { get; private set; }

        public LinkDetailRow(string label, string url)
            : base(label, url)
        {
            Url = url ?? string.Empty;
        }
    }

    /// <summary>A label followed by free text in a multiline, read-only, scrollable text box.</summary>
    public sealed class NotesDetailRow : EntryDetailRow
    {
        public string Text { get; private set; }

        public NotesDetailRow(string label, string text)
            : base(label, text)
        {
            Text = text ?? string.Empty;
        }
    }

    /// <summary>
    /// The full data the shared detail form renders: a KeePass-style banner (plugin-specific title
    /// plus the domain as its subtitle), the rows to show (already filtered to non-empty fields by
    /// the mapping plugin), an optional "no data" message, and the mandatory attribution line.
    /// </summary>
    public sealed class EntryDetailModel
    {
        /// <summary>The domain, shown as the banner subtitle (second line) and the window title.</summary>
        public string Domain { get; set; }

        /// <summary>
        /// The plugin-specific banner title (first line), e.g. "Passkey Details" or "2FA Details".
        /// Kept here rather than in <c>Shared</c> so the form stays plugin-agnostic; the plugin's
        /// model builder sets it. Empty falls back to a neutral default in the form.
        /// </summary>
        public string BannerTitle { get; set; }

        /// <summary>
        /// An optional icon for the banner (typed as <see cref="System.Drawing.Image"/> so
        /// <c>Shared</c> takes no KeePass-specific dependency). Null renders a text-only banner.
        /// </summary>
        public System.Drawing.Image BannerIcon { get; set; }

        /// <summary>
        /// An optional title-bar icon for the window, so it looks like a native KeePass dialog
        /// (icon left of the title). The hosting plugin supplies its KeePass main-window icon
        /// (<c>IPluginHost.MainWindow.Icon</c>); typed as <see cref="System.Drawing.Icon"/> (BCL)
        /// so <c>Shared</c> stays plugin-agnostic. Null hides the icon (<c>ShowIcon = false</c>)
        /// rather than showing the generic WinForms default.
        /// </summary>
        public System.Drawing.Icon WindowIcon { get; set; }

        /// <summary>The detail rows in display order. Empty when there is no directory match.</summary>
        public IList<EntryDetailRow> Rows { get; set; }

        /// <summary>
        /// A message shown instead of rows when the domain has no directory match or the cache is
        /// unavailable (e.g. "No data found for this domain in the directory."). Null when rows exist.
        /// </summary>
        public string EmptyMessage { get; set; }

        /// <summary>The mandatory data-source attribution line, shown in gray at the bottom.</summary>
        public string Attribution { get; set; }

        public EntryDetailModel()
        {
            Domain = string.Empty;
            BannerTitle = string.Empty;
            Rows = new List<EntryDetailRow>();
            Attribution = string.Empty;
        }
    }
}
