using System.Runtime.InteropServices;

using ENetDotNet.Protocol;

namespace ENetDotNet.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ENetProtocolDisconnect
    {
        public ENetProtocolCommandHeader Header;
        public uint Data;

        public readonly void WriteTo(ENetPacketWriter writer)
        {
            Header.WriteTo(writer);
            writer.WriteNetEndian(Data);
        }

        public void ReadFrom(ref ENetPacketReader reader)
        {
            Header.ReadFrom(ref reader);
            Data = reader.ReadNetUInt32();
        }
    }
}
