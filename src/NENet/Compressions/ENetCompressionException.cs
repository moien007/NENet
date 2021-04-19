// 
// Following codes are ported from https://github.com/lsalzman/enet/blob/master/compress.c
//

using System;

namespace ENetDotNet.Compressions
{
    public class ENetCompressionException : Exception
    {
        public ENetCompressionException() { }
        public ENetCompressionException(string message) : base(message) { }
        public ENetCompressionException(string message, Exception innerException) : base(message, innerException) { }
    }
}