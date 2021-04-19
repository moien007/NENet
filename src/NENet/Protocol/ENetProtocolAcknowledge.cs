using System.Runtime.InteropServices;

using ENetDotNet.Protocol;

namespace ENetDotNet.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ENetProtocolAcknowledge
    {
        public ENetProtocolCommandHeader Header;
        public ushort ReceivedReliableSequenceNumber;
        public ushort ReceivedSentTime;

        public readonly void WriteTo(ENetPacketWriter writer)
        {
            Header.WriteTo(writer);
            writer.WriteNetEndian(ReceivedReliableSequenceNumber);
            writer.WriteNetEndian(ReceivedSentTime);
        }

        public void ReadFrom(ref ENetPacketReader reader)
        {
            Header.ReadFrom(ref reader);
            ReceivedReliableSequenceNumber = reader.ReadNetUInt16();
            ReceivedSentTime = reader.ReadNetUInt16();
        }
    }
}
