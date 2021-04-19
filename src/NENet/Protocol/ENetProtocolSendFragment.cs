using System.Runtime.InteropServices;

using ENetDotNet.Protocol;

namespace ENetDotNet.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ENetProtocolSendFragment
    {
        public ENetProtocolCommandHeader Header;
        public ushort StartSequenceNumber;
        public ushort DataLength;
        public uint FragmentCount;
        public uint FragmentNumber;
        public uint TotalLength;
        public uint FragmentOffset;

        public readonly void WriteTo(ENetPacketWriter writer)
        {
            Header.WriteTo(writer);
            writer.WriteNetEndian(StartSequenceNumber);
            writer.WriteNetEndian(DataLength);
            writer.WriteNetEndian(FragmentCount);
            writer.WriteNetEndian(FragmentNumber);
            writer.WriteNetEndian(TotalLength);
            writer.WriteNetEndian(FragmentOffset);
        }

        public void ReadFrom(ref ENetPacketReader reader)
        {
            Header.ReadFrom(ref reader);
            StartSequenceNumber = reader.ReadNetUInt16();
            DataLength = reader.ReadNetUInt16();
            FragmentCount = reader.ReadNetUInt32();
            FragmentNumber = reader.ReadNetUInt32();
            TotalLength = reader.ReadNetUInt32();
            FragmentOffset = reader.ReadNetUInt32();
        }
    }
}
