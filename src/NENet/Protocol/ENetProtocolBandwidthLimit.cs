using System.Runtime.InteropServices;

using ENetDotNet.Protocol;

namespace ENetDotNet.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ENetProtocolBandwidthLimit
    {
        public ENetProtocolCommandHeader Header;
        public uint IncomingBandwidth;
        public uint OutgoingBandwidth;

        public readonly void WriteTo(ENetPacketWriter writer)
        {
            Header.WriteTo(writer);
            writer.WriteNetEndian(IncomingBandwidth);
            writer.WriteNetEndian(OutgoingBandwidth);
        }

        public void ReadFrom(ref ENetPacketReader reader)
        {
            Header.ReadFrom(ref reader);
            IncomingBandwidth = reader.ReadNetUInt32();
            OutgoingBandwidth = reader.ReadNetUInt32();
        }
    }
}
