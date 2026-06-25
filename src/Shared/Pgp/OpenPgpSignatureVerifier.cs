using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace KPPasskeyChecker.Shared.Pgp
{
    /// <summary>
    /// Verifies an OpenPGP signed message (as produced by <c>gpg --sign</c>) against a single
    /// pinned RSA public key. The 2FA Directory ".sig" files are ZIP-compressed messages that
    /// embed a one-pass signature, the literal JSON data, and an RSA/SHA-512 signature packet.
    ///
    /// Security model: verification is performed exclusively with the injected trusted key. No
    /// key material from the message itself is ever trusted, and on success the caller is handed
    /// back the exact bytes that were signed (the embedded literal data), so the data used is the
    /// data that was verified.
    /// </summary>
    public sealed class OpenPgpSignatureVerifier
    {
        private const int TagSignature = 2;
        private const int TagOnePassSignature = 4;
        private const int TagCompressedData = 8;
        private const int TagLiteralData = 11;

        private const int PubAlgoRsa = 1;
        private const byte SigTypeBinaryDocument = 0x00;

        // Upper bound on the decompressed signed message, to defend against a decompression bomb:
        // a tiny .sig that inflates to gigabytes would otherwise exhaust memory before the signature
        // is ever checked. The real signed payloads are well under 1 MB.
        private const int MaxDecompressedBytes = 16 * 1024 * 1024;

        private readonly OpenPgpRsaPublicKey _trustedKey;

        public OpenPgpSignatureVerifier(OpenPgpRsaPublicKey trustedKey)
        {
            if (trustedKey == null) throw new ArgumentNullException("trustedKey");
            _trustedKey = trustedKey;
        }

        public PgpVerificationResult Verify(byte[] signedMessage)
        {
            if (signedMessage == null || signedMessage.Length == 0)
                return PgpVerificationResult.Invalid("Empty signature file.");

            try
            {
                byte[] packets = Decompress(signedMessage);

                byte[] content = null;
                byte[] signatureBody = null;
                string onePassKeyId = null;

                int pos = 0;
                while (pos < packets.Length)
                {
                    int bodyStart, bodyLen;
                    int tag = PgpPacketReader.ReadHeader(packets, ref pos, out bodyStart, out bodyLen);
                    if (bodyStart + bodyLen > packets.Length)
                        return PgpVerificationResult.Invalid("Packet length exceeds message (tag " + tag + ").");

                    if (tag == TagOnePassSignature)
                        onePassKeyId = ReadOnePassKeyId(packets, bodyStart, bodyLen);
                    else if (tag == TagLiteralData)
                        content = ReadLiteralContent(packets, bodyStart, bodyLen);
                    else if (tag == TagSignature)
                        signatureBody = Slice(packets, bodyStart, bodyLen);

                    pos = bodyStart + bodyLen;
                }

                if (content == null)
                    return PgpVerificationResult.Invalid("No literal data packet found.");
                if (signatureBody == null)
                    return PgpVerificationResult.Invalid("No signature packet found.");

                return VerifySignature(content, signatureBody, onePassKeyId);
            }
            catch (Exception ex)
            {
                return PgpVerificationResult.Invalid("Malformed signed message: " + ex.Message);
            }
        }

        private PgpVerificationResult VerifySignature(byte[] content, byte[] sig, string onePassKeyId)
        {
            if (sig.Length < 6)
                return PgpVerificationResult.Invalid("Signature packet too short.");

            byte version = sig[0];
            if (version != 4)
                return PgpVerificationResult.Invalid("Unsupported signature version " + version + " (expected 4).");

            byte sigType = sig[1];
            byte pubAlgo = sig[2];
            byte hashAlgo = sig[3];

            if (sigType != SigTypeBinaryDocument)
                return PgpVerificationResult.Invalid("Unexpected signature type 0x" + sigType.ToString("X2") + ".");
            if (pubAlgo != PubAlgoRsa)
                return PgpVerificationResult.Invalid("Unsupported public-key algorithm " + pubAlgo + " (expected RSA).");

            HashAlgorithmName hashName;
            if (!TryMapHash(hashAlgo, out hashName))
                return PgpVerificationResult.Invalid("Unsupported hash algorithm " + hashAlgo + ".");

            int hashedLen = (sig[4] << 8) | sig[5];
            int hashedRegionLen = 6 + hashedLen;
            if (hashedRegionLen > sig.Length)
                return PgpVerificationResult.Invalid("Truncated hashed subpackets.");

            // Issuer check (defence in depth — the real gate is the RSA check below).
            string issuer = onePassKeyId != null ? onePassKeyId : FindIssuerKeyId(sig, hashedRegionLen);
            if (issuer != null && !string.Equals(issuer, _trustedKey.KeyId, StringComparison.OrdinalIgnoreCase))
                return PgpVerificationResult.Invalid(
                    "Signature issuer " + issuer + " does not match trusted key " + _trustedKey.KeyId + ".");

            byte[] signatureValue = ReadSignatureMpi(sig, hashedRegionLen);

            byte[] digest = ComputeDigest(content, sig, hashedRegionLen, hashName);

            using (RSA rsa = RSA.Create())
            {
                rsa.ImportParameters(new RSAParameters
                {
                    Modulus = _trustedKey.Modulus,
                    Exponent = _trustedKey.Exponent
                });

                bool ok = rsa.VerifyHash(digest, signatureValue, hashName, RSASignaturePadding.Pkcs1);
                if (!ok)
                    return PgpVerificationResult.Invalid("RSA signature does not match the trusted key.");
            }

            return PgpVerificationResult.Valid(content, _trustedKey.KeyId);
        }

        private static byte[] ComputeDigest(byte[] content, byte[] sig, int hashedRegionLen, HashAlgorithmName hashName)
        {
            // RFC 4880 5.2.4: hash over the document data, then the signature's hashed fields,
            // then the v4 trailer (0x04, 0xFF, 4-byte big-endian length of the hashed fields).
            byte[] trailer = new byte[]
            {
                0x04, 0xFF,
                (byte)((hashedRegionLen >> 24) & 0xFF),
                (byte)((hashedRegionLen >> 16) & 0xFF),
                (byte)((hashedRegionLen >> 8) & 0xFF),
                (byte)(hashedRegionLen & 0xFF)
            };

            using (HashAlgorithm hash = CreateHash(hashName))
            {
                hash.TransformBlock(content, 0, content.Length, null, 0);
                hash.TransformBlock(sig, 0, hashedRegionLen, null, 0);
                hash.TransformFinalBlock(trailer, 0, trailer.Length);
                return hash.Hash;
            }
        }

        private static byte[] ReadSignatureMpi(byte[] sig, int hashedRegionLen)
        {
            int q = hashedRegionLen;
            if (q + 1 >= sig.Length)
                throw new ArgumentException("Truncated signature MPI.");
            int unhashedLen = (sig[q] << 8) | sig[q + 1];
            q += 2 + unhashedLen;          // skip unhashed subpackets
            if (q + 1 >= sig.Length)
                throw new ArgumentException("Truncated signature MPI.");
            q += 2;                         // skip the left 16 bits of the hash
            if (q + 1 >= sig.Length)
                throw new ArgumentException("Truncated signature MPI.");
            int bits = (sig[q] << 8) | sig[q + 1];
            q += 2;
            int len = (bits + 7) / 8;
            if (q + len > sig.Length)
                throw new ArgumentException("Truncated signature MPI.");
            return Slice(sig, q, len);
        }

        private static string FindIssuerKeyId(byte[] sig, int hashedRegionLen)
        {
            // Scan the hashed subpackets (6 .. hashedRegionLen) and the unhashed subpackets.
            string id = ScanSubpacketsForIssuer(sig, 6, hashedRegionLen - 6);
            if (id != null) return id;

            int q = hashedRegionLen;
            int unhashedLen = (sig[q] << 8) | sig[q + 1];
            return ScanSubpacketsForIssuer(sig, q + 2, unhashedLen);
        }

        private static string ScanSubpacketsForIssuer(byte[] sig, int offset, int length)
        {
            int end = offset + length;
            int p = offset;
            while (p < end)
            {
                int spLen;
                byte first = sig[p];
                if (first < 192) { spLen = first; p += 1; }
                else if (first < 255)
                {
                    if (p + 1 >= end) break;
                    spLen = ((first - 192) << 8) + sig[p + 1] + 192; p += 2;
                }
                else
                {
                    if (p + 4 >= end) break;
                    spLen = (sig[p + 1] << 24) | (sig[p + 2] << 16) | (sig[p + 3] << 8) | sig[p + 4]; p += 5;
                }
                if (spLen <= 0 || p + spLen > end) break;

                int type = sig[p] & 0x7F;     // strip the critical bit
                int dataStart = p + 1;
                int dataLen = spLen - 1;

                if (type == 16 && dataLen == 8)               // Issuer Key ID
                    return ToHex(sig, dataStart, 8);
                if (type == 33 && dataLen == 21)              // Issuer Fingerprint (v4): version + 20 bytes
                    return ToHex(sig, dataStart + 1 + 12, 8); // low 8 bytes of the 20-byte fingerprint

                p += spLen;
            }
            return null;
        }

        private static string ReadOnePassKeyId(byte[] buf, int bodyStart, int bodyLen)
        {
            // One-Pass Signature packet (v3): version(1) type(1) hash(1) pubAlgo(1) keyId(8) nested(1).
            if (bodyLen < 13) return null;
            return ToHex(buf, bodyStart + 4, 8);
        }

        private static byte[] ReadLiteralContent(byte[] buf, int bodyStart, int bodyLen)
        {
            // Literal Data packet: format(1) fileNameLen(1) fileName(n) date(4) content...
            if (bodyLen < 2) throw new ArgumentException("Literal data packet too short.");
            int fileNameLen = buf[bodyStart + 1];
            int headerLen = 1 + 1 + fileNameLen + 4;
            int contentStart = bodyStart + headerLen;
            int contentLen = bodyLen - headerLen;
            if (contentLen < 0) throw new ArgumentException("Malformed literal data packet.");
            return Slice(buf, contentStart, contentLen);
        }

        private static byte[] Decompress(byte[] message)
        {
            int pos = 0;
            int bodyStart, bodyLen;
            int tag = PgpPacketReader.ReadHeader(message, ref pos, out bodyStart, out bodyLen);
            if (tag != TagCompressedData)
                return message; // already an uncompressed packet stream

            byte algorithm = message[bodyStart];
            int dataStart = bodyStart + 1;
            int dataLen = bodyLen - 1;

            if (algorithm == 0)                 // uncompressed
                return Slice(message, dataStart, dataLen);

            if (algorithm == 1 || algorithm == 2) // ZIP (raw DEFLATE) or ZLIB
            {
                int deflateStart = dataStart;
                int deflateLen = dataLen;
                if (algorithm == 2)             // ZLIB: skip the 2-byte zlib header
                {
                    deflateStart += 2;
                    deflateLen -= 2;
                }
                using (var input = new MemoryStream(message, deflateStart, deflateLen))
                using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    byte[] buffer = new byte[81920];
                    int total = 0;
                    int read;
                    while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += read;
                        if (total > MaxDecompressedBytes)
                            throw new InvalidDataException("Signed message expands beyond the allowed size.");
                        output.Write(buffer, 0, read);
                    }
                    return output.ToArray();
                }
            }

            throw new NotSupportedException("Unsupported compression algorithm " + algorithm + ".");
        }

        private static bool TryMapHash(byte hashAlgo, out HashAlgorithmName name)
        {
            switch (hashAlgo)
            {
                case 8: name = HashAlgorithmName.SHA256; return true;
                case 9: name = HashAlgorithmName.SHA384; return true;
                case 10: name = HashAlgorithmName.SHA512; return true;
                default: name = default(HashAlgorithmName); return false;
            }
        }

        private static HashAlgorithm CreateHash(HashAlgorithmName name)
        {
            if (name == HashAlgorithmName.SHA256) return SHA256.Create();
            if (name == HashAlgorithmName.SHA384) return SHA384.Create();
            return SHA512.Create();
        }

        private static byte[] Slice(byte[] src, int offset, int len)
        {
            byte[] r = new byte[len];
            Buffer.BlockCopy(src, offset, r, 0, len);
            return r;
        }

        private static string ToHex(byte[] b, int offset, int count)
        {
            var sb = new StringBuilder(count * 2);
            for (int i = 0; i < count; i++)
                sb.Append(b[offset + i].ToString("X2"));
            return sb.ToString();
        }
    }
}
