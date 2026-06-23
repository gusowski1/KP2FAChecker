using System;

namespace KPPasskeyChecker.Shared.Pgp
{
    /// <summary>
    /// Minimal OpenPGP packet framing reader (RFC 4880 4.2). Supports old- and new-format
    /// headers with definite lengths, plus the old-format indeterminate length used by the
    /// outermost compressed-data packet. Partial body lengths are not supported.
    /// </summary>
    internal static class PgpPacketReader
    {
        /// <summary>
        /// Read a packet header starting at <paramref name="pos"/>. Advances <paramref name="pos"/>
        /// past the header to the first body byte, reports where the body starts and how long it is,
        /// and returns the packet tag.
        /// </summary>
        public static int ReadHeader(byte[] buf, ref int pos, out int bodyStart, out int bodyLen)
        {
            if (pos >= buf.Length)
                throw new ArgumentException("Unexpected end of packet stream.");

            byte b0 = buf[pos];
            pos += 1;
            if ((b0 & 0x80) == 0)
                throw new ArgumentException("Invalid OpenPGP packet tag byte 0x" + b0.ToString("X2") + ".");

            int tag;
            if ((b0 & 0x40) != 0)
            {
                // New format.
                tag = b0 & 0x3F;
                byte l0 = buf[pos];
                if (l0 < 192)
                {
                    bodyLen = l0;
                    pos += 1;
                }
                else if (l0 < 224)
                {
                    bodyLen = ((l0 - 192) << 8) + buf[pos + 1] + 192;
                    pos += 2;
                }
                else if (l0 == 255)
                {
                    bodyLen = (buf[pos + 1] << 24) | (buf[pos + 2] << 16) | (buf[pos + 3] << 8) | buf[pos + 4];
                    pos += 5;
                }
                else
                {
                    throw new NotSupportedException("Partial body lengths are not supported.");
                }
            }
            else
            {
                // Old format.
                tag = (b0 & 0x3C) >> 2;
                int lengthType = b0 & 0x03;
                switch (lengthType)
                {
                    case 0:
                        bodyLen = buf[pos];
                        pos += 1;
                        break;
                    case 1:
                        bodyLen = (buf[pos] << 8) | buf[pos + 1];
                        pos += 2;
                        break;
                    case 2:
                        bodyLen = (buf[pos] << 24) | (buf[pos + 1] << 16) | (buf[pos + 2] << 8) | buf[pos + 3];
                        pos += 4;
                        break;
                    default:
                        // Indeterminate length: body runs to the end of the buffer.
                        bodyLen = buf.Length - pos;
                        break;
                }
            }

            bodyStart = pos;
            return tag;
        }
    }
}
