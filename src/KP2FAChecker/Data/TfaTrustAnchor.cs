using System;
using KeeRadar.Shared.Pgp;

namespace KP2FAChecker.Data
{
    /// <summary>
    /// The pinned 2FA Directory code-signing key used to verify downloaded ".sig" files.
    ///
    /// The key is published by 2factorauth as a CERT record on <c>security.2fa.directory</c>
    /// (retrievable with <c>gpg --auto-key-locate cert --locate-keys security@2fa.directory</c>).
    /// We pin it at build time rather than fetching it live: pinning ties verification to a known
    /// publisher key and needs no extra (DNSSEC-validated) network lookup. If 2factorauth ever
    /// rotates this key, the plugin must be updated — the verifier will refuse to validate against
    /// any other key, which is the intended fail-closed behaviour.
    ///
    /// This is the SAME signing key 2factorauth uses across the directory (the passkeys data uses
    /// it too); the pinned RDATA hex and fingerprint are identical.
    ///
    /// UID:        2FactorAuth (Code signing key) &lt;security@2fa.directory&gt;
    /// Algorithm:  RSA-4096
    /// </summary>
    internal static class TfaTrustAnchor
    {
        /// <summary>Expected v4 fingerprint of <see cref="CertRecordHex"/>; asserted at load time.</summary>
        public const string ExpectedFingerprint = "0D504141CE290061BD4F95A4AD8483C1CBABC36D";

        /// <summary>
        /// The raw RDATA of the <c>security.2fa.directory</c> CERT record (RFC 4398): a 5-byte
        /// header (type 3 = PGP) followed by the OpenPGP public-key packet.
        /// </summary>
        internal const string CertRecordHex =
            "000300000099020d04604596b2011000d5291dc2ac2b30ffb2930604f90405214fd010630c5a03b9bddcee7af66a66640b703f38ab3ca1960898897f7ecc7bf7d6e65178e80642ffaf6f7cc85d1ec2cb0018ae9d4d898dead5b51ce4e0629d0fe2ce3d435bc33ffcc09a41874e08e867741d2181235450969f195c072fb933776cc3263a21438da92b240e74f26eb4bac5d4059f83eab007ce7d681233b9d36db0cbe98bf6a8d5fd91ad813651897f6f2ea2b35c071c898ccb3f900c70ba052c6708cd148dbde3000bc729eb4bb6e8b195545a81bd511e4cb6bcd734fcee73cecdd664b5c7559c66c637c333392a6969d6246faca4f5732151f3c05f25f66f6d0cd5867664c4b7366aa37a6c69bf8bd53e59615dc89a0a8953337af25d6c229ca1cdcff6418f07f5eb76da7dc867bbf4995fd4897e5e2030002e57503125c4681be608babde9cfcaa9c837c4ed1ec904bd5590de941d8c9c2c8c3903ed15aed08704eec0045137422017d3c6e25823cbd22f55e2fa7780348ddbf5205a55fb8f489c59c31047491f8b2f11ec4d31945739b98dad05493a3ba7659f43ff666088022981a0b1d99068a7345349355cb64a3b98a33b883fddc858ea159dc4205ce4591ec3359b0155efd597710d7eb2e5d0ebefb53c4753cd1f6fcf2f2f4a9da381986a056fe30efb91557709de01221a9459d97c25183ce0c80bdf0e4fa507649cff0739a170d95b8793491048604f00070011010001";

        private static readonly object _gate = new object();
        private static OpenPgpRsaPublicKey _key;

        public static OpenPgpRsaPublicKey Key
        {
            get
            {
                if (_key == null)
                {
                    lock (_gate)
                    {
                        if (_key == null)
                        {
                            OpenPgpRsaPublicKey key = OpenPgpRsaPublicKey.FromCertRecord(HexToBytes(CertRecordHex));
                            if (!string.Equals(key.Fingerprint, ExpectedFingerprint, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException(
                                    "Pinned signing key fingerprint mismatch: expected " + ExpectedFingerprint +
                                    ", got " + key.Fingerprint + ".");
                            _key = key;
                        }
                    }
                }
                return _key;
            }
        }

        public static OpenPgpSignatureVerifier CreateVerifier()
        {
            return new OpenPgpSignatureVerifier(Key);
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }
}
