using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using ENetDotNet.Internal;

namespace ENetDotNet.Protocol
{
    internal ref struct ENetPacketReader
    {
        private readonly ReadOnlySpan<byte> m_Buffer;
        private ReadOnlySpan<byte> m_Left;

        public ReadOnlySpan<byte> Buffer => m_Buffer;
        public ReadOnlySpan<byte> Left => m_Left;

        public ENetPacketReader(ReadOnlySpan<byte> span)
        {
            m_Buffer = m_Left = span;
        }

        public void EnsureAvailable(int count)
        {
            if (m_Left.Length < count)
                throw new InvalidOperationException("Ran out of bytes to read.");
        }

        public ReadOnlySpan<byte> ReadSpan(int count)
        {
            EnsureAvailable(count);

            var result = m_Left[..count];
            m_Left = m_Left[count..];
            return result;
        }

        public unsafe T ReadValue<T>() where T : unmanaged
        {
            EnsureAvailable(sizeof(T));

            var result = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(m_Left));
            m_Left = m_Left[sizeof(T)..];
            return result;
        }

        public unsafe T PeekValue<T>() where T : unmanaged
        {
            EnsureAvailable(sizeof(T));
            
            return Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(m_Left));
        }

        public byte ReadByte()
        {
            EnsureAvailable(1);
            var result = m_Left[0];
            m_Left = m_Left[1..];
            return result;
        }

        public short ReadNetInt16() => EndianHelper.NetworkSwapEndian(ReadValue<short>());
        public int ReadNetInt32() => EndianHelper.NetworkSwapEndian(ReadValue<int>());
        public long ReadNetInt64() => EndianHelper.NetworkSwapEndian(ReadValue<long>());
        public ushort ReadNetUInt16() => EndianHelper.NetworkSwapEndian(ReadValue<ushort>());
        public uint ReadNetUInt32() => EndianHelper.NetworkSwapEndian(ReadValue<uint>());
        public ulong ReadNetUInt64() => EndianHelper.NetworkSwapEndian(ReadValue<ulong>());
    }
}
