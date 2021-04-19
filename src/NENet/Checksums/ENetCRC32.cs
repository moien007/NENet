using System;

using ENetDotNet.Internal;

namespace ENetDotNet.Checksums
{
    public sealed class ENetCRC32 : ENetChecksum
    {
        private static readonly uint[] m_Table;

        static ENetCRC32()
        {
            m_Table = new uint[256];

            static uint reflect(uint val, int bits)
            {
                int result = 0, bit;

                unchecked
                {
                    for (bit = 0; bit < bits; bit++)
                    {
                        if ((val & 1) != 0) result |= 1 << bits - 1 - bit;
                        val >>= 1;
                    }

                    return (uint)result;
                }
            }

            unchecked
            {
                for (int @byte = 0; @byte < 256; ++@byte)
                {
                    uint crc = reflect(unchecked((uint)@byte), 8) << 24;
                    int offset;

                    for (offset = 0; offset < 8; ++offset)
                    {
                        if ((crc & 0x80000000) != 0)
                            crc = crc << 1 ^ 0x04c11db7;
                        else
                            crc <<= 1;
                    }

                    m_Table[@byte] = reflect(crc, 32);
                }
            }
        }


        private uint m_Crc;

        public override void Begin()
        {
            m_Crc = uint.MaxValue;
        }

        public override void Sum(ReadOnlySpan<byte> buffer)
        {
            unchecked
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    m_Crc = m_Crc >> 8 ^ m_Table[m_Crc & 0xFF ^ buffer[i]];
                }
            }
        }

        public override uint End()
        {
            return EndianHelper.NetworkSwapEndian(~m_Crc);
        }
    }
}