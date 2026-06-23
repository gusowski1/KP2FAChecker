using KeePass.Plugins;

namespace KPPasskeyChecker.Shared.KeePassUi
{
    public abstract class PluginSettingsStoreBase
    {
        private readonly IPluginHost _host;

        protected PluginSettingsStoreBase(IPluginHost host)
        {
            _host = host;
        }

        protected string GetString(string key, string defaultValue)
        {
            return _host.CustomConfig.GetString(key, defaultValue);
        }

        protected void SetString(string key, string value)
        {
            _host.CustomConfig.SetString(key, value);
        }

        protected long GetLong(string key, long defaultValue)
        {
            return _host.CustomConfig.GetLong(key, defaultValue);
        }

        protected void SetLong(string key, long value)
        {
            _host.CustomConfig.SetLong(key, value);
        }

        protected bool GetBool(string key, bool defaultValue)
        {
            return _host.CustomConfig.GetBool(key, defaultValue);
        }

        protected void SetBool(string key, bool value)
        {
            _host.CustomConfig.SetBool(key, value);
        }
    }
}
