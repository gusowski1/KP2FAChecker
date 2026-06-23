namespace KP2FAChecker.Data
{
    /// <summary>
    /// The 2FA method tokens the 2FA Directory v4 API lists per domain (the "methods" array).
    /// Unknown/forward-compatible tokens are represented by <see cref="Unknown"/> and dropped by
    /// the parser, so a new method added upstream never throws — it is simply ignored until the
    /// plugin learns about it.
    /// </summary>
    public enum TfaMethod
    {
        Unknown = 0,
        Sms,
        Call,
        Email,
        Totp,
        U2f,
        CustomSoftware,
        CustomHardware
    }
}
