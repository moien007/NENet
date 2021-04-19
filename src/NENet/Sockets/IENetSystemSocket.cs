using System.Net;
using System.Net.Sockets;

namespace ENetDotNet.Sockets
{
    public interface IENetSystemSocket
    {
        IPEndPoint EndPoint { get; }
        Socket Socket { get; }
    }
}
