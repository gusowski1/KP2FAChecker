using System.Collections.Generic;

namespace KP2FAChecker.Data
{
    /// <summary>
    /// One domain's 2FA support as described by the 2FA Directory v4 API. An entry with no
    /// recognised methods means "no documented 2FA" and is rendered as a blank cell.
    /// </summary>
    public sealed class TfaEntry
    {
        public string Domain { get; set; }

        /// <summary>The recognised 2FA methods (unknown/forward-compat tokens are dropped).</summary>
        public IReadOnlyList<TfaMethod> Methods { get; set; }

        /// <summary>Product names for the "custom-software" token (e.g. "Authy"). May be empty.</summary>
        public IReadOnlyList<string> CustomSoftware { get; set; }

        /// <summary>Product names for the "custom-hardware" token (e.g. "YubiKey"). May be empty.</summary>
        public IReadOnlyList<string> CustomHardware { get; set; }

        public string DocumentationUrl { get; set; }
        public string RecoveryUrl { get; set; }
        public string Notes { get; set; }

        /// <summary>True when at least one recognised 2FA method is documented for this domain.</summary>
        public bool SupportsAny
        {
            get { return Methods != null && Methods.Count > 0; }
        }

        public TfaEntry()
        {
            Domain         = string.Empty;
            Methods        = new TfaMethod[0];
            CustomSoftware = new string[0];
            CustomHardware = new string[0];
        }
    }
}
