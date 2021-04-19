using System.Runtime.InteropServices;

using ENetDotNet.Protocol;

namespace ENetDotNet.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ENetProtocolSendUnreliable
    {
        public ENetProtocolCommandHeader Header;
        public ushort UnreliableSequenceNumber;
        public ushort DataLength;

        public readonly void WriteTo(ENetPacketWriter writer)
        {
            Header.WriteTo(writer);
            writer.WriteNetEndian(UnreliableSequenceNumber);
            writer.WriteNetEndian(DataLength);
        }

        public void ReadFrom(ref ENetPacketReader reader)
        {
            Header.ReadFrom(ref reader);
            UnreliableSequenceNumber = reader.ReadNetUInt16();
            DataLength = reader.ReadNetUInt16();
        }
    }
}
