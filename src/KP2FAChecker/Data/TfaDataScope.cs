namespace KP2FAChecker.Data
{
    /// <summary>
    /// Which slice of the 2FA Directory v4 data to fetch. Each scope maps to exactly one endpoint
    /// (see <see cref="TfaEndpoints"/>); only the selected endpoint is ever downloaded.
    /// </summary>
    public enum TfaDataScope
    {
        /// <summary>All sites with any documented 2FA method (v4/all.json).</summary>
        AnySupport,
        TotpOnly,
        U2fOnly,
        SmsOnly,
        EmailOnly
    }
}
