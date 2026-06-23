using System;
using System.Windows.Forms;
using KP2FAChecker.Data;

namespace KP2FAChecker.Settings
{
    public partial class TfaSettingsForm : Form
    {
        private readonly TfaSettingsStore _store;

        public TfaSettingsForm(TfaSettingsStore store)
        {
            _store = store;
            InitializeComponent();
            PopulateScopes();
            LoadSettings();
            UpdateCacheStatus();
        }

        // Pairs a user-facing label with the scope it selects. The ComboBox shows the labels and
        // returns the matching TfaDataScope through SelectedItem.
        private sealed class ScopeOption
        {
            public TfaDataScope Scope { get; private set; }
            private readonly string _label;
            public ScopeOption(TfaDataScope scope, string label) { Scope = scope; _label = label; }
            public override string ToString() { return _label; }
        }

        private void PopulateScopes()
        {
            _cboScope.Items.Clear();
            _cboScope.Items.Add(new ScopeOption(TfaDataScope.AnySupport, "Any 2FA support  (all.json)"));
            _cboScope.Items.Add(new ScopeOption(TfaDataScope.TotpOnly,   "TOTP only  (totp.json)"));
            _cboScope.Items.Add(new ScopeOption(TfaDataScope.U2fOnly,    "Security key / U2F only  (u2f.json)"));
            _cboScope.Items.Add(new ScopeOption(TfaDataScope.SmsOnly,    "SMS only  (sms.json)"));
            _cboScope.Items.Add(new ScopeOption(TfaDataScope.EmailOnly,  "Email only  (email.json)"));
        }

        private void LoadSettings()
        {
            SelectScope(_store.Scope);
            _nudInterval.Value = _store.RefreshIntervalHours;
            _chkPgp.Checked    = _store.VerifyPgpSignature;
        }

        private void SelectScope(TfaDataScope scope)
        {
            for (int i = 0; i < _cboScope.Items.Count; i++)
            {
                ScopeOption option = _cboScope.Items[i] as ScopeOption;
                if (option != null && option.Scope == scope)
                {
                    _cboScope.SelectedIndex = i;
                    return;
                }
            }
            _cboScope.SelectedIndex = 0;
        }

        private TfaDataScope SelectedScope()
        {
            ScopeOption option = _cboScope.SelectedItem as ScopeOption;
            return option != null ? option.Scope : TfaDataScope.AnySupport;
        }

        private void UpdateCacheStatus()
        {
            if (!TfaDirectoryService.IsAvailable)
            {
                _lblCacheStatus.Text = "Service not running.";
                return;
            }

            var svc = TfaDirectoryService.Current;

            if (svc.Directory == null)
            {
                _lblCacheStatus.Text = svc.LastError != null
                    ? "Not loaded. Error: " + svc.LastError
                    : "Loading...";
                return;
            }

            string age = svc.LastRefreshed.HasValue
                ? FormatAge(svc.LastRefreshed.Value)
                : "unknown";

            string status = svc.IsStale
                ? "Stale fallback — last fetched " + age + "\r\nError: " + svc.LastError
                : "Up to date — last fetched " + age + "\r\n" + svc.Directory.Count + " domains indexed";

            _lblCacheStatus.Text = status;
        }

        private static string FormatAge(DateTimeOffset fetchedAt)
        {
            TimeSpan age = DateTimeOffset.UtcNow - fetchedAt;
            if (age.TotalMinutes < 1) return "just now";
            if (age.TotalHours < 1)   return (int)age.TotalMinutes + " min ago";
            if (age.TotalDays < 1)    return (int)age.TotalHours + " h ago";
            return (int)age.TotalDays + " day(s) ago";
        }

        private async void OnRefreshNowClick(object sender, EventArgs e)
        {
            _btnRefreshNow.Enabled = false;
            _lblCacheStatus.Text   = "Refreshing...";

            try
            {
                await TfaDirectoryService.Current.RefreshAsync(true).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _lblCacheStatus.Text   = "Refresh failed: " + ex.Message;
                _btnRefreshNow.Enabled = true;
                return;
            }

            UpdateCacheStatus();
            _btnRefreshNow.Enabled = true;
        }

        private void OnOkClick(object sender, EventArgs e)
        {
            _store.Scope                = SelectedScope();
            _store.RefreshIntervalHours = (int)_nudInterval.Value;
            _store.VerifyPgpSignature   = _chkPgp.Checked;
        }
    }
}
