using System;
using System.IO;
using System.Windows.Forms;
using KeePass.Plugins;
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

            _columnProvider = new TfaColumnProvider();
            host.ColumnProviderPool.Add(_columnProvider);

            // A leading separator sets the plugin's entry apart in the Tools menu — the
            // convention other plugins follow to group their own items.
            ToolStripItemCollection toolsItems = host.MainWindow.ToolsMenu.DropDownItems;
            _menuSeparator = new ToolStripSeparator();
            toolsItems.Add(_menuSeparator);
            _menuItem = new ToolStripMenuItem("2FA Checker &Settings...");
            _menuItem.Click += OnSettingsMenuClick;
            toolsItems.Add(_menuItem);

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
    }
}
