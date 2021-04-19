using System;

namespace ENetDotNet.Internal
{
    public static class ThrowHelper
    {
        public static void ThrowIfArgumentNull(object arg, string argName)
        {
            if (arg is null)
                throw new ArgumentNullException(argName);
        }
    }
}