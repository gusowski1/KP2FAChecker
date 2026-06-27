namespace KP2FAChecker
{
    internal static class PluginVersion
    {
        public const string Current = "0.4.0";
        public const string RepoUrl = "https://github.com/gusowski1/KP2FAChecker";

        // KeePass downloads this file and compares "KP2FAChecker:<version>" (the AssemblyTitle
        // and AssemblyFileVersion) to decide whether a newer version exists. The file lives at
        // the repo root on the default branch; bump its version line on every release.
        // See https://keepass.info/help/v2_dev/plg_index.html#upd
        public const string UpdateUrl = "https://raw.githubusercontent.com/gusowski1/KP2FAChecker/main/VersionInfo.txt";
    }
}
