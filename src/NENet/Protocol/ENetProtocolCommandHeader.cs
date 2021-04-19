using System.Runtime.InteropServices;

using ENetDotNet.Protocol;

namespace ENetDotNet.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ENetProtocolCommandHeader
    {
        public const int Size = 1 + 1 + 2;

        public byte Command;
        public byte ChannelID;
        public ushort ReliableSequenceNumber;

        public readonly void WriteTo(ENetPacketWriter writer)
        {
            writer.WriteByte(Command);
            writer.WriteByte(ChannelID);
            writer.WriteNetEndian(ReliableSequenceNumber);
        }

        public void ReadFrom(ref ENetPacketReader reader)
        {
            Command = reader.ReadByte();
            ChannelID = reader.ReadByte();
            ReliableSequenceNumber = reader.ReadNetUInt16();
        }
    }
}
