using System.Runtime.InteropServices;

using ENetDotNet.Protocol;

namespace ENetDotNet.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ENetProtocolSendReliable
    {
        public ENetProtocolCommandHeader Header;
        public ushort DataLength;

        public readonly void WriteTo(ENetPacketWriter writer)
        {
            Header.WriteTo(writer);
            writer.WriteNetEndian(DataLength);
        }

        public void ReadFrom(ref ENetPacketReader reader)
        {
            Header.ReadFrom(ref reader);
            DataLength = reader.ReadNetUInt16();
        }
    }
}
