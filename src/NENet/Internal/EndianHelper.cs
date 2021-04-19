using System;

using System.Buffers.Binary;

namespace ENetDotNet.Internal
{
    internal static class EndianHelper
    {
        public static short NetworkSwapEndian(short value) => BitConverter.IsLittleEndian ?
                BinaryPrimitives.ReverseEndianness(value) : value;

        public static int NetworkSwapEndian(int value) => BitConverter.IsLittleEndian ?
                BinaryPrimitives.ReverseEndianness(value) : value;

        public static long NetworkSwapEndian(long value) => BitConverter.IsLittleEndian ?
                BinaryPrimitives.ReverseEndianness(value) : value;

        public static ushort NetworkSwapEndian(ushort value) => BitConverter.IsLittleEndian ?
                BinaryPrimitives.ReverseEndianness(value) : value;

        public static uint NetworkSwapEndian(uint value) => BitConverter.IsLittleEndian ?
             BinaryPrimitives.ReverseEndianness(value) : value;

        public static ulong NetworkSwapEndian(ulong value) => BitConverter.IsLittleEndian ?
                BinaryPrimitives.ReverseEndianness(value) : value;
    }
}
