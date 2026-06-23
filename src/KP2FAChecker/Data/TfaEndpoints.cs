namespace KP2FAChecker.Data
{
    internal static class TfaEndpoints
    {
        private const string BaseUrl = "https://api.2fa.directory/v4/";

        public static string ForScope(TfaDataScope scope)
        {
            switch (scope)
            {
                case TfaDataScope.TotpOnly:  return BaseUrl + "totp.json";
                case TfaDataScope.U2fOnly:   return BaseUrl + "u2f.json";
                case TfaDataScope.SmsOnly:   return BaseUrl + "sms.json";
                case TfaDataScope.EmailOnly: return BaseUrl + "email.json";
                default:                     return BaseUrl + "all.json";
            }
        }

        /// <summary>The inline OpenPGP signed message for a scope (the ".json.sig" sibling).</summary>
        public static string SignatureForScope(TfaDataScope scope)
        {
            return ForScope(scope) + ".sig";
        }

        public static string CacheKey(TfaDataScope scope)
        {
            return "tfa_" + scope.ToString();
        }

        /// <summary>
        /// Cache key for PGP-verified data, kept distinct from the unverified key so that toggling
        /// verification never lets unverified cached JSON be served as if it had been verified.
        /// </summary>
        public static string SignedCacheKey(TfaDataScope scope)
        {
            return "tfa_" + scope.ToString() + "_signed";
        }
    }
}
