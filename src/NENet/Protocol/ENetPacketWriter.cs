using System;
using System.Runtime.CompilerServices;

using ENetDotNet.Internal;

namespace ENetDotNet.Protocol
{
    internal sealed class ENetPacketWriter
    {
        private byte[] m_Buffer;
        private int m_Offset;

        public int Written => m_Offset;
        public int Offset => m_Offset;

        public ENetPacketWriter()
        {
            m_Buffer = Array.Empty<byte>();
            m_Offset = 0;
        }

        public void EnsureCapacity(int len)
        {
            if (m_Buffer.Length - m_Offset < len)
            {
                var newCap = Math.Max(m_Buffer.Length * 2, m_Buffer.Length + len);
                Array.Resize(ref m_Buffer, newCap);
            }
        }

        public void Reset()
        {
            m_Offset = 0;
        }

        public void SetOffset(int offset)
        {
            var diff = Math.Abs(m_Offset - offset);
            if (m_Buffer.Length < diff)
            {
                Array.Resize(ref m_Buffer, m_Buffer.Length + diff);
            }
        }

        public Memory<byte> GetUnderlayingBuffer()
        {
            return m_Buffer.AsMemory();
        }

        public Memory<byte> GetWrittenBuffer()
        {
            return new Memory<byte>(m_Buffer, 0, m_Offset);
        }

        public unsafe void WriteValue<T>(T value) where T : unmanaged
        {
            EnsureCapacity(sizeof(T));
            Unsafe.WriteUnaligned(ref m_Buffer[m_Offset], value);
            m_Offset += sizeof(T);
        }

        public void WriteByte(byte value)
        {
            EnsureCapacity(1);
            m_Buffer[m_Offset++] = value;
        }

        public void WriteBytes(ReadOnlySpan<byte> span)
        {
            EnsureCapacity(span.Length);
            span.CopyTo(m_Buffer.AsSpan(m_Offset));
            m_Offset += span.Length;
        }

        public void WriteNetEndian(ushort value) => WriteValue(EndianHelper.NetworkSwapEndian(value));
        public void WriteNetEndian(uint value) => WriteValue(EndianHelper.NetworkSwapEndian(value));
        public void WriteNetEndian(ulong value) => WriteValue(EndianHelper.NetworkSwapEndian(value));
        public void WriteNetEndian(short value) => WriteValue(EndianHelper.NetworkSwapEndian(value));
        public void WriteNetEndian(int value) => WriteValue(EndianHelper.NetworkSwapEndian(value));
        public void WriteNetEndian(long value) => WriteValue(EndianHelper.NetworkSwapEndian(value));
    }
}
