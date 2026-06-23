using System;

namespace KP2FAChecker.Data
{
    /// <summary>
    /// Maps between the 2FA Directory v4 method tokens (e.g. "totp", "u2f", "custom-software")
    /// and the <see cref="TfaMethod"/> enum, plus short human-readable labels for the column.
    /// </summary>
    public static class TfaMethods
    {
        /// <summary>
        /// Parse a single method token. Unknown tokens map to <see cref="TfaMethod.Unknown"/>
        /// (forward-compatibility) — callers drop those rather than treating them as an error.
        /// </summary>
        public static TfaMethod Parse(string token)
        {
            if (string.IsNullOrEmpty(token))
                return TfaMethod.Unknown;

            switch (token.Trim().ToLowerInvariant())
            {
                case "sms":             return TfaMethod.Sms;
                case "call":            return TfaMethod.Call;
                case "email":           return TfaMethod.Email;
                case "totp":            return TfaMethod.Totp;
                case "u2f":             return TfaMethod.U2f;
                case "custom-software": return TfaMethod.CustomSoftware;
                case "custom-hardware": return TfaMethod.CustomHardware;
                default:                return TfaMethod.Unknown;
            }
        }

        /// <summary>A short, human-readable label for the entry-list column.</summary>
        public static string Label(TfaMethod method)
        {
            switch (method)
            {
                case TfaMethod.Sms:            return "SMS";
                case TfaMethod.Call:           return "Phone Call";
                case TfaMethod.Email:          return "Email";
                case TfaMethod.Totp:           return "TOTP";
                case TfaMethod.U2f:            return "Security Key";
                case TfaMethod.CustomSoftware: return "Software";
                case TfaMethod.CustomHardware: return "Hardware";
                default:                       return string.Empty;
            }
        }
    }
}
