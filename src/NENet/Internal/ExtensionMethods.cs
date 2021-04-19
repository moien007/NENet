using System;

namespace ENetDotNet.Internal
{
    internal static class ExtensionMethods
    {
        public static byte NextByte(this Random random)
        {
            unchecked
            {
                return (byte)random.Next(byte.MinValue, byte.MaxValue);
            }
        }
    }
}
