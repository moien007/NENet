using System.Runtime.InteropServices;

using ENetDotNet.Protocol;

namespace ENetDotNet.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ENetProtocolPing
    {
        public ENetProtocolCommandHeader Header;

        public readonly void WriteTo(ENetPacketWriter writer)
        {
            Header.WriteTo(writer);
        }

        public void ReadFrom(ref ENetPacketReader reader)
        {
            Header.ReadFrom(ref reader);
        }
    }
}
