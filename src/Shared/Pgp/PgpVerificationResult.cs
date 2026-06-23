namespace KPPasskeyChecker.Shared.Pgp
{
    /// <summary>
    /// Outcome of verifying an OpenPGP signed message. On success <see cref="SignedContent"/>
    /// holds the exact bytes that were signed (the literal data); on failure <see cref="Error"/>
    /// explains why and <see cref="SignedContent"/> is null.
    /// </summary>
    public sealed class PgpVerificationResult
    {
        public bool IsValid { get; private set; }
        public byte[] SignedContent { get; private set; }
        public string IssuerKeyId { get; private set; }
        public string Error { get; private set; }

        public static PgpVerificationResult Valid(byte[] content, string issuerKeyId)
        {
            return new PgpVerificationResult
            {
                IsValid = true,
                SignedContent = content,
                IssuerKeyId = issuerKeyId
            };
        }

        public static PgpVerificationResult Invalid(string error)
        {
            return new PgpVerificationResult
            {
                IsValid = false,
                Error = error
            };
        }
    }
}
