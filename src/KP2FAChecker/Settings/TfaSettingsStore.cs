using KeePass.Plugins;
using KP2FAChecker.Data;
using KPPasskeyChecker.Shared.KeePassUi;

namespace KP2FAChecker.Settings
{
    public sealed class TfaSettingsStore : PluginSettingsStoreBase
    {
        private const string KeyScope           = "KP2FAChecker.Scope";
        private const string KeyRefreshInterval = "KP2FAChecker.RefreshIntervalHours";
        private const string KeyVerifyPgp       = "KP2FAChecker.VerifyPgpSignature";

        public TfaSettingsStore(IPluginHost host) : base(host) { }

        public TfaDataScope Scope
        {
            get
            {
                string raw = GetString(KeyScope, "AnySupport");
                TfaDataScope scope;
                if (System.Enum.TryParse<TfaDataScope>(raw, out scope))
                    return scope;
                return TfaDataScope.AnySupport;
            }
            set
            {
                SetString(KeyScope, value.ToString());
            }
        }

        public int RefreshIntervalHours
        {
            get
            {
                return (int)GetLong(KeyRefreshInterval, 24);
            }
            set
            {
                SetLong(KeyRefreshInterval, value < 1 ? 1 : value);
            }
        }

        public bool VerifyPgpSignature
        {
            get
            {
                // On by default: downloaded data is verified against the pinned signing key
                // unless the user explicitly turns it off.
                return GetBool(KeyVerifyPgp, true);
            }
            set
            {
                SetBool(KeyVerifyPgp, value);
            }
        }
    }
}
