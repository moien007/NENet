using System;

namespace ENetDotNet.Internal
{
    internal sealed class Utilities
    {
        public static ulong GetMillisecondsSinceEpoch()
        {
            return (ulong)(DateTime.UtcNow - DateTimeOffset.UnixEpoch).TotalMilliseconds;
        }

        public static ulong Difference(ulong a, ulong b)
        {
            var max = Math.Max(a, b);
            var min = Math.Min(a, b);
            return max - min;
        }
    }
}