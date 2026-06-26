using System.Reflection;
using System.Runtime.InteropServices;

// KeePass detects a DLL as a plugin via the file version information block.
// These attributes populate that block. The Product name MUST be exactly
// "KeePass Plugin" or KeePass silently ignores the DLL (no error shown).
// See https://keepass.info/help/v2_dev/plg_index.html
//
// Mapping (KeePass dialog field <- assembly attribute):
//   Title       <- AssemblyTitle        (full plugin name)
//   Description <- AssemblyDescription   (short description)
//   Author      <- AssemblyCompany       (author name)
//   Product     <- AssemblyProduct        (MUST be "KeePass Plugin")
//   Version     <- AssemblyVersion / AssemblyFileVersion (no asterisks!)

[assembly: AssemblyTitle("KP2FAChecker")]
[assembly: AssemblyDescription("Shows which two-factor methods the domain of an entry supports, using the 2FA Directory by 2factorauth.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Lars Gusowski")]
[assembly: AssemblyProduct("KeePass Plugin")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]
[assembly: Guid("4037c1ce-5b55-43ff-8f6a-e36c65ffd421")]

// Plugin version. Keep in sync with PluginVersion.Current ("0.3.0").
// Do NOT use asterisks here (KeePass requires a comparable, fixed version).
[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]
