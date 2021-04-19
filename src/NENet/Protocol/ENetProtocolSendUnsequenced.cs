using System.Runtime.InteropServices;

using ENetDotNet.Protocol;

namespace ENetDotNet.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ENetProtocolSendUnsequenced
    {
        public ENetProtocolCommandHeader Header;
        public ushort UnsequencedGroup;
        public ushort DataLength;

        public readonly void WriteTo(ENetPacketWriter writer)
        {
            Header.WriteTo(writer);
            writer.WriteNetEndian(UnsequencedGroup);
            writer.WriteNetEndian(DataLength);
        }

        public void ReadFrom(ref ENetPacketReader reader)
        {
            Header.ReadFrom(ref reader);
            UnsequencedGroup = reader.ReadNetUInt16();
            DataLength = reader.ReadNetUInt16();
        }
    }
}
