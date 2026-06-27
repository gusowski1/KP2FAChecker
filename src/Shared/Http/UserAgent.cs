// Shared KeeRadar infrastructure — synced from KPPasskeyChecker/src/Shared via sync-shared.ps1; do not edit here
namespace KeeRadar.Shared.Http
{
    public static class UserAgent
    {
        public static string Build(string pluginName, string version, string repoUrl)
        {
            return pluginName + "/" + version + " (+" + repoUrl + ")";
        }
    }
}
