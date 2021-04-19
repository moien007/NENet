using System;
using System.Net.Sockets;

namespace ENetDotNet.Sockets
{
    public class ENetSocketException : Exception
    {
        public SocketError SocketError { get; set; }

        public ENetSocketException() { }
        public ENetSocketException(string message) : base(message) { }
        public ENetSocketException(string message, Exception innerException) : base(message, innerException) { }
    }
}
