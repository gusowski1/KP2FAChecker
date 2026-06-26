// Shared KeeRadar infrastructure — synced from KPPasskeyChecker/src/Shared via sync-shared.ps1; do not edit here
using System;
using System.Drawing;
using System.Windows.Forms;
using KeePass.UI;
using KeePass.Util;

namespace KeeRadar.Shared.KeePassUi
{
    /// <summary>
    /// A plugin-agnostic, read-only detail window for a single directory entry. It renders an
    /// <see cref="EntryDetailModel"/> using KeePass' own UI infrastructure: a KeePass-style banner
    /// (plugin title plus the domain as its subtitle) on top, then one row per present field in a
    /// scrollable stack, and a gray attribution line pinned to the bottom. A plugin builds the model
    /// (filtering empty fields) and shows the form modally per double-click, so this form never
    /// references a plugin-specific entry type.
    /// </summary>
    public sealed class EntryDetailForm : Form
    {
        private const int OuterMargin = 12;
        private const int RowSpacing = 6;
        private const int LinkButtonWidth = 52;
        private const int NotesHeight = 65;

        // KeePass' standard banner height (BannerFactory.StdHeight). Hardcoded because that field
        // is internal; matches every native KeePass dialog (e.g. "Edit Entry").
        private const int BannerHeight = 60;

        private readonly EntryDetailModel _model;
        private PictureBox _banner;
        private Button _btnClose;
        private TableLayoutPanel _rows;

        // Tracks the width the current banner image was generated for, so we only regenerate when
        // the client width actually changes (the resizable form fires Resize frequently).
        private int _bannerWidth = -1;

        public EntryDetailForm(EntryDetailModel model)
        {
            if (model == null) throw new ArgumentNullException("model");
            _model = model;
            BuildLayout();

            Load += OnFormLoad;
            FormClosed += OnFormClosed;
            Resize += OnFormResize;
        }

        private void BuildLayout()
        {
            SuspendLayout();

            Font = SystemFonts.DialogFont;
            // The window title is the dialog's purpose (e.g. "Passkey Details" / "2FA Details"),
            // matching native KeePass dialogs like "Edit Entry" — not the domain (which the banner
            // already shows on its second line). Falls back to a neutral default.
            Text = BannerTitle();
            // A normal resizable window with the standard title bar (icon left, standard close X),
            // like KeePass' own dialogs — not the narrow SizableToolWindow tool strip. MinimizeBox
            // and MaximizeBox stay off, so only the standard close button remains.
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;

            // Title-bar icon: use the host's KeePass main-window icon when the plugin supplied it,
            // so the window reads as a native KeePass dialog. When it is absent, hide the icon
            // entirely (ShowIcon = false) rather than fall back to the generic WinForms default.
            if (_model.WindowIcon != null)
            {
                Icon = _model.WindowIcon;
                ShowIcon = true;
            }
            else
            {
                ShowIcon = false;
            }
            KeyPreview = true;
            MinimumSize = new Size(400, 300);
            ClientSize = new Size(460, 340);
            BackColor = SystemColors.Control;

            // KeePass-style banner across the top (filled with the real image on load/resize).
            _banner = new PictureBox();
            _banner.Dock = DockStyle.Top;
            _banner.Height = BannerHeight;
            _banner.SizeMode = PictureBoxSizeMode.Normal;
            _banner.TabStop = false;
            _banner.AccessibleName = BannerSubtitle();

            // Bottom: Close button (also the CancelButton, so Esc/X both close).
            _btnClose = new Button();
            _btnClose.Text = "Close";
            _btnClose.DialogResult = DialogResult.Cancel;
            _btnClose.Size = new Size(80, 26);
            _btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnClose.AccessibleName = "Close";
            _btnClose.Location = new Point(
                ClientSize.Width - OuterMargin - _btnClose.Width,
                ClientSize.Height - OuterMargin - _btnClose.Height);

            // Attribution line, gray, pinned to the bottom-left above the Close button.
            Label attribution = new Label();
            attribution.Text = _model.Attribution;
            attribution.AutoSize = false;
            attribution.ForeColor = SystemColors.GrayText;
            attribution.TextAlign = ContentAlignment.MiddleLeft;
            attribution.AccessibleName = _model.Attribution;
            attribution.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            attribution.Location = new Point(OuterMargin, _btnClose.Top + 4);
            attribution.Size = new Size(_btnClose.Left - OuterMargin - RowSpacing, _btnClose.Height - 4);

            // The scrollable row stack lives between the banner and the attribution line.
            _rows = new TableLayoutPanel();
            _rows.ColumnCount = 2;
            _rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _rows.AutoScroll = true;
            _rows.Padding = new Padding(0, 0, SystemInformation.VerticalScrollBarWidth, 0);
            _rows.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _rows.Location = new Point(OuterMargin, BannerHeight + RowSpacing);
            _rows.Size = new Size(
                ClientSize.Width - (2 * OuterMargin),
                attribution.Top - RowSpacing - (BannerHeight + RowSpacing));

            PopulateRows();

            Controls.Add(_rows);
            Controls.Add(attribution);
            Controls.Add(_btnClose);
            Controls.Add(_banner); // added last so Dock=Top sits above the others

            CancelButton = _btnClose;
            AcceptButton = _btnClose;
            ActiveControl = _btnClose;

            ResumeLayout(true);
        }

        /// <summary>The plugin title line of the banner (falls back to a neutral default).</summary>
        private string BannerTitle()
        {
            return string.IsNullOrEmpty(_model.BannerTitle) ? "Entry details" : _model.BannerTitle;
        }

        /// <summary>The banner subtitle (second line): the domain, when present.</summary>
        private string BannerSubtitle()
        {
            return _model.Domain ?? string.Empty;
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            // Register with KeePass so the window participates in theming, visual styles and the
            // modal-window stack exactly like a native dialog.
            GlobalWindowManager.AddWindow(this);
            RefreshBanner();
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            GlobalWindowManager.RemoveWindow(this);

            // A WinForms PictureBox does not dispose an Image assigned directly to its Image
            // property, so the last banner bitmap would leak per window. Dispose it explicitly
            // (null-safe) and detach it.
            if (_banner != null && _banner.Image != null)
            {
                Image last = _banner.Image;
                _banner.Image = null;
                last.Dispose();
            }
        }

        private void OnFormResize(object sender, EventArgs e)
        {
            RefreshBanner();
        }

        /// <summary>
        /// Generates the banner image to exactly fill the current client width and stretch on resize,
        /// like KeePass' own resizable banner dialogs. Uses the non-cached overload so each width
        /// produces a correctly sized image without polluting the shared banner cache.
        /// </summary>
        private void RefreshBanner()
        {
            if (_banner == null) return;

            int width = ClientSize.Width;
            if (width <= 0 || width == _bannerWidth) return;

            Image previous = _banner.Image;
            _banner.Image = BannerFactory.CreateBanner(
                width, BannerHeight, BannerStyle.Default,
                _model.BannerIcon, BannerTitle(), BannerSubtitle(), true);
            _bannerWidth = width;

            if (previous != null) previous.Dispose();
        }

        private void PopulateRows()
        {
            int row = 0;

            if (_model.Rows == null || _model.Rows.Count == 0)
            {
                Label empty = new Label();
                empty.AutoSize = true;
                empty.MaximumSize = new Size(_rows.Width - 20, 0);
                empty.Text = string.IsNullOrEmpty(_model.EmptyMessage)
                    ? "No data found for this domain in the directory."
                    : _model.EmptyMessage;
                empty.AccessibleName = empty.Text;
                empty.Margin = new Padding(0, 0, 0, RowSpacing);
                _rows.Controls.Add(empty, 0, 0);
                _rows.SetColumnSpan(empty, 2);
                return;
            }

            foreach (EntryDetailRow detailRow in _model.Rows)
            {
                Label label = new Label();
                label.AutoSize = true;
                label.Font = FontUtil.CreateFont(Font, FontStyle.Bold);
                label.Text = detailRow.Label;
                label.Margin = new Padding(0, 3, RowSpacing, RowSpacing);
                label.Anchor = AnchorStyles.Left | AnchorStyles.Top;

                Control value = BuildValueControl(detailRow);

                _rows.Controls.Add(label, 0, row);
                _rows.Controls.Add(value, 1, row);
                row++;
            }
        }

        private Control BuildValueControl(EntryDetailRow detailRow)
        {
            TextDetailRow textRow = detailRow as TextDetailRow;
            if (textRow != null) return BuildTextValue(textRow);

            LinkDetailRow linkRow = detailRow as LinkDetailRow;
            if (linkRow != null) return BuildLinkValue(linkRow);

            NotesDetailRow notesRow = detailRow as NotesDetailRow;
            if (notesRow != null) return BuildNotesValue(notesRow);

            // Unknown row kind: render its accessible value as plain text rather than throwing.
            return BuildTextValue(new TextDetailRow(detailRow.Label, detailRow.AccessibleValue));
        }

        private Control BuildTextValue(TextDetailRow textRow)
        {
            Label value = new Label();
            value.AutoSize = true;
            // The value carries raw API data, so an '&' (e.g. in a documentation URL with query
            // parameters) must render literally, not be swallowed as a mnemonic.
            value.UseMnemonic = false;
            value.MaximumSize = new Size(ValueColumnWidth(), 0);
            value.Text = textRow.Value;
            value.Margin = new Padding(0, 3, 0, RowSpacing);
            value.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            value.AccessibleName = textRow.Label + ": " + textRow.AccessibleValue;
            return value;
        }

        private Control BuildLinkValue(LinkDetailRow linkRow)
        {
            FlowLayoutPanel panel = new FlowLayoutPanel();
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.FlowDirection = FlowDirection.LeftToRight;
            panel.WrapContents = true;
            panel.Margin = new Padding(0, 0, 0, RowSpacing);
            panel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            panel.MaximumSize = new Size(ValueColumnWidth(), 0);

            LinkLabel link = new LinkLabel();
            link.AutoSize = true;
            // The link text is a raw API URL, so an '&' (query parameters) must render literally
            // rather than be swallowed as a mnemonic.
            link.UseMnemonic = false;
            link.Text = linkRow.Url;
            link.MaximumSize = new Size(ValueColumnWidth() - (2 * LinkButtonWidth) - 16, 0);
            link.Margin = new Padding(0, 3, RowSpacing, 0);
            link.AccessibleName = linkRow.Label + " link: " + linkRow.Url;
            link.Tag = linkRow.Url;
            link.LinkClicked += OnLinkClicked;

            Button open = new Button();
            open.Text = "Open";
            open.Size = new Size(LinkButtonWidth, 23);
            open.Margin = new Padding(0, 1, 4, 0);
            open.Tag = linkRow.Url;
            open.AccessibleName = "Open " + linkRow.Label + " link";
            open.Click += OnOpenClicked;

            Button copy = new Button();
            copy.Text = "Copy";
            copy.Size = new Size(LinkButtonWidth, 23);
            copy.Margin = new Padding(0, 1, 0, 0);
            copy.Tag = linkRow.Url;
            copy.AccessibleName = "Copy " + linkRow.Label + " link";
            copy.Click += OnCopyClicked;

            panel.Controls.Add(link);
            panel.Controls.Add(open);
            panel.Controls.Add(copy);
            return panel;
        }

        private Control BuildNotesValue(NotesDetailRow notesRow)
        {
            // Read-only, static-looking notes: control-colored background (not the white "Window"
            // color), borderless and not a tab stop, so it reads as a label rather than an editable
            // field — while staying scrollable and selectable/copyable. The default arrow cursor
            // (rather than the text I-beam) reinforces that it is not an input.
            TextBox box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.WordWrap = true;
            box.BorderStyle = BorderStyle.None;
            box.BackColor = SystemColors.Control;
            box.TabStop = false;
            box.Cursor = Cursors.Default;
            box.Text = notesRow.Text;
            box.Height = NotesHeight;
            box.Margin = new Padding(0, 3, 0, RowSpacing);
            box.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            box.Width = ValueColumnWidth();
            box.AccessibleName = notesRow.Label + ": " + notesRow.Text;
            return box;
        }

        private int ValueColumnWidth()
        {
            // A conservative working width for the value column, leaving room for the label column
            // and the vertical scrollbar. Controls anchor Left|Right so they still track on resize.
            int w = _rows.ClientSize.Width - 130;
            return w < 120 ? 120 : w;
        }

        private void OnLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            LinkLabel link = sender as LinkLabel;
            if (link == null) return;
            link.LinkVisited = true;
            OpenUrl(link.Tag as string);
        }

        private void OnOpenClicked(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;
            OpenUrl(button.Tag as string);
        }

        private void OnCopyClicked(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            // Apply the same http/https guard as OpenUrl: never copy an arbitrary (e.g. file://
            // or javascript:) string to the clipboard. Non-http/https → no-op.
            Uri uri = TryParseHttpUrl(button.Tag as string);
            if (uri == null) return;

            try { Clipboard.SetText(uri.AbsoluteUri); }
            catch { /* Clipboard can be transiently locked; ignore. */ }
        }

        private static void OpenUrl(string url)
        {
            // Only follow absolute http/https links; never hand an arbitrary string to the opener.
            Uri uri = TryParseHttpUrl(url);
            if (uri == null) return;

            try
            {
                // Route through KeePass so the user's configured URL-override / browser is honored,
                // exactly like clicking an entry URL in KeePass. No PwEntry data source here.
                WinUtil.OpenUrl(uri.AbsoluteUri, null);
            }
            catch
            {
                // A missing/locked-down default browser must not crash KeePass.
            }
        }

        /// <summary>
        /// Returns the parsed <see cref="Uri"/> only for an absolute http/https URL; otherwise null.
        /// Shared by Open and Copy so both apply the identical scheme guard (no logic duplication).
        /// </summary>
        private static Uri TryParseHttpUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
            return uri;
        }
    }
}
