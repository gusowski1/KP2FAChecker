using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using KeePass.Plugins;
using KeePassLib;
using KP2FAChecker.Data;
using KP2FAChecker.Settings;
using KP2FAChecker.UI;

namespace KP2FAChecker
{
    public sealed class KP2FACheckerExt : Plugin
    {
        private IPluginHost _host;
        private TfaSettingsStore _settings;
        private TfaColumnProvider _columnProvider;
        private ToolStripMenuItem _menuItem;
        private ToolStripSeparator _menuSeparator;
        private ToolStripMenuItem _entryMenuItem;
        // One shared 16x16 icon instance for both menu entries; disposed in Terminate.
        private Image _menuIcon;
        private Icon _pluginIcon;

        /// <summary>
        /// Lets KeePass check whether a newer plugin version is available. KeePass downloads
        /// this file and compares the "KP2FAChecker:&lt;version&gt;" line against the installed
        /// AssemblyFileVersion. See https://keepass.info/help/v2_dev/plg_index.html#upd
        /// </summary>
        public override string UpdateUrl
        {
            get { return PluginVersion.UpdateUrl; }
        }

        public override bool Initialize(IPluginHost host)
        {
            if (host == null) return false;
            _host = host;

            _settings = new TfaSettingsStore(host);

            string cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KeePassPluginCache", "KP2FAChecker");

            TfaDirectoryService.Initialize(_settings, cacheDir);

            // Build the plugin icon once; reuse as both menu Image and dialog title-bar Icon.
            _menuIcon = PluginIcon.Create16(host.MainWindow.ClientIcons.Images[0]);
            _pluginIcon = Icon.FromHandle(((Bitmap)_menuIcon).GetHicon());

            _columnProvider = new TfaColumnProvider(_pluginIcon);
            host.ColumnProviderPool.Add(_columnProvider);

            // A leading separator sets the plugin's entry apart in the Tools menu — the
            // convention other plugins follow to group their own items.

            ToolStripItemCollection toolsItems = host.MainWindow.ToolsMenu.DropDownItems;
            _menuSeparator = new ToolStripSeparator();
            toolsItems.Add(_menuSeparator);
            _menuItem = new ToolStripMenuItem("2FA Checker &Settings...");
            _menuItem.Image = _menuIcon;
            _menuItem.Click += OnSettingsMenuClick;
            toolsItems.Add(_menuItem);

            // Per-entry action: a right-click on an entry opens the same detail dialog the
            // "2FA Methods" column shows on double-click.
            _entryMenuItem = new ToolStripMenuItem("Check 2FA Support");
            _entryMenuItem.Image = _menuIcon;
            _entryMenuItem.Click += OnEntryMenuClick;
            host.MainWindow.EntryContextMenu.Items.Add(_entryMenuItem);

            return true;
        }

        public override void Terminate()
        {
            if (_host == null) return;

            if (_columnProvider != null)
            {
                _host.ColumnProviderPool.Remove(_columnProvider);
                _columnProvider = null;
            }

            if (_entryMenuItem != null)
            {
                _host.MainWindow.EntryContextMenu.Items.Remove(_entryMenuItem);
                _entryMenuItem.Click -= OnEntryMenuClick;
                _entryMenuItem.Dispose();
                _entryMenuItem = null;
            }

            if (_menuItem != null)
            {
                _host.MainWindow.ToolsMenu.DropDownItems.Remove(_menuItem);
                _menuItem.Click -= OnSettingsMenuClick;
                _menuItem.Dispose();
                _menuItem = null;
            }

            if (_menuSeparator != null)
            {
                _host.MainWindow.ToolsMenu.DropDownItems.Remove(_menuSeparator);
                _menuSeparator.Dispose();
                _menuSeparator = null;
            }

            // Both menu items are removed/disposed above; now release the shared icons.
            if (_pluginIcon != null)
            {
                _pluginIcon.Dispose();
                _pluginIcon = null;
            }

            if (_menuIcon != null)
            {
                _menuIcon.Dispose();
                _menuIcon = null;
            }

            TfaDirectoryService.Shutdown();
            _host = null;
        }

        private async void OnSettingsMenuClick(object sender, EventArgs e)
        {
            if (_settings == null || _host == null) return;

            DialogResult result;
            using (var form = new TfaSettingsForm(_settings))
                result = form.ShowDialog(_host.MainWindow as IWin32Window);

            if (result != DialogResult.OK) return;

            // Settings may have changed the scope or verification mode. Refresh now and only
            // repaint the entry list once the new data has actually arrived.
            try
            {
                await TfaDirectoryService.Current.RefreshAsync(true).ConfigureAwait(true);
            }
            catch
            {
                // Refresh failures are reflected in the service's cache status; never let them
                // surface as an unhandled exception out of this async void event handler.
            }

            if (_host != null)
                _host.MainWindow.RefreshEntriesList();
        }

        private void OnEntryMenuClick(object sender, EventArgs e)
        {
            if (_host == null || _columnProvider == null) return;

            PwEntry[] selected = _host.MainWindow.GetSelectedEntries();
            if (selected == null || selected.Length == 0)
            {
                MessageBox.Show(_host.MainWindow as IWin32Window,
                    "Please select an entry first.", "2FA Checker",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // The detail dialog is per-entry; act on the first selected entry. The column's
            // double-click flow is reused verbatim, including its own no-URL / no-data handling.
            _columnProvider.ShowDetailDialog(selected[0]);
        }
    }
}
