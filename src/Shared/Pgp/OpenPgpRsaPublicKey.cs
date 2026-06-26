// Shared KeeRadar infrastructure — synced from KPPasskeyChecker/src/Shared via sync-shared.ps1; do not edit here
using System;
using System.Security.Cryptography;
using System.Text;

namespace KeeRadar.Shared.Pgp
{
    /// <summary>
    /// An RSA public key parsed from an OpenPGP version-4 public-key packet (RFC 4880).
    /// Carries the RSA parameters plus the computed key fingerprint / key ID so callers
    /// can pin a specific publisher key.
    /// </summary>
    public sealed class OpenPgpRsaPublicKey
    {
        public byte[] Modulus { get; private set; }
        public byte[] Exponent { get; private set; }

        /// <summary>40 uppercase hex chars (SHA-1 of the v4 key packet).</summary>
        public string Fingerprint { get; private set; }

        /// <summary>Low 64 bits of the fingerprint, 16 uppercase hex chars.</summary>
        public string KeyId { get; private set; }

        private OpenPgpRsaPublicKey() { }

        /// <summary>
        /// Parse from a DNS CERT record's RDATA (RFC 4398). Expects certificate type 3 (PGP),
        /// then skips the 5-byte CERT header and parses the embedded public-key packet.
        /// </summary>
        public static OpenPgpRsaPublicKey FromCertRecord(byte[] rdata)
        {
            if (rdata == null || rdata.Length < 6)
                throw new ArgumentException("CERT RDATA too short.");
            int certType = (rdata[0] << 8) | rdata[1];
            if (certType != 3)
                throw new ArgumentException("CERT record is not of type PGP (3).");
            // type(2) + key tag(2) + algorithm(1) = 5-byte header
            byte[] packet = new byte[rdata.Length - 5];
            Buffer.BlockCopy(rdata, 5, packet, 0, packet.Length);
            return FromPublicKeyPacket(packet);
        }

        /// <summary>
        /// Parse an OpenPGP public-key packet (tag 6), including its packet header.
        /// </summary>
        public static OpenPgpRsaPublicKey FromPublicKeyPacket(byte[] packet)
        {
            if (packet == null || packet.Length < 3)
                throw new ArgumentException("Public-key packet too short.");

            int pos = 0;
            int bodyStart;
            int bodyLen;
            int tag = PgpPacketReader.ReadHeader(packet, ref pos, out bodyStart, out bodyLen);
            if (tag != 6)
                throw new ArgumentException("Not a public-key packet (tag " + tag + ").");
            if (bodyStart + bodyLen > packet.Length)
                throw new ArgumentException("Public-key packet length exceeds buffer.");

            byte[] body = new byte[bodyLen];
            Buffer.BlockCopy(packet, bodyStart, body, 0, bodyLen);

            var key = new OpenPgpRsaPublicKey();
            key.ParseBody(body);
            return key;
        }

        private void ParseBody(byte[] body)
        {
            int p = 0;
            byte version = body[p]; p += 1;
            if (version != 4)
                throw new NotSupportedException("Only version-4 public keys are supported (got " + version + ").");
            p += 4; // creation time
            byte algo = body[p]; p += 1;
            if (algo != 1 && algo != 2 && algo != 3) // RSA / RSA-encrypt / RSA-sign
                throw new NotSupportedException("Only RSA public keys are supported (algorithm " + algo + ").");

            Modulus = ReadMpi(body, ref p);
            Exponent = ReadMpi(body, ref p);

            ComputeFingerprint(body);
        }

        private void ComputeFingerprint(byte[] body)
        {
            // RFC 4880 12.2: v4 fingerprint = SHA-1( 0x99 || 2-byte body length || body ).
            byte[] input = new byte[3 + body.Length];
            input[0] = 0x99;
            input[1] = (byte)((body.Length >> 8) & 0xFF);
            input[2] = (byte)(body.Length & 0xFF);
            Buffer.BlockCopy(body, 0, input, 3, body.Length);

            byte[] fp;
            using (var sha1 = SHA1.Create())
                fp = sha1.ComputeHash(input);

            Fingerprint = ToHex(fp, 0, fp.Length);
            KeyId = ToHex(fp, fp.Length - 8, 8);
        }

        private static byte[] ReadMpi(byte[] buf, ref int pos)
        {
            if (pos + 2 > buf.Length)
                throw new ArgumentException("Truncated MPI length.");
            int bits = (buf[pos] << 8) | buf[pos + 1];
            pos += 2;
            int len = (bits + 7) / 8;
            if (pos + len > buf.Length)
                throw new ArgumentException("Truncated MPI value.");
            byte[] value = new byte[len];
            Buffer.BlockCopy(buf, pos, value, 0, len);
            pos += len;
            return value;
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
