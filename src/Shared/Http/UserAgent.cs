namespace KPPasskeyChecker.Shared.Http
{
    public static class UserAgent
    {
        public static string Build(string pluginName, string version, string repoUrl)
        {
            return pluginName + "/" + version + " (+" + repoUrl + ")";
        }
    }
}
