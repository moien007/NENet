using System.Runtime.InteropServices;

using ENetDotNet.Protocol;

namespace ENetDotNet.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ENetProtocolVerifyConnect
    {
        public ENetProtocolCommandHeader Header;
        public ushort OutgoingPeerID;
        public byte IncomingSessionID;
        public byte OutgoingSessionID;
        public uint MTU;
        public uint WindowSize;
        public uint ChannelCount;
        public uint IncomingBandwidth;
        public uint OutgoingBandwidth;
        public uint PacketThrottleInterval;
        public uint PacketThrottleAcceleration;
        public uint PacketThrottleDeceleration;
        public uint ConnectID;

        public readonly void WriteTo(ENetPacketWriter writer)
        {
            Header.WriteTo(writer);
            writer.WriteNetEndian(OutgoingPeerID);
            writer.WriteByte(IncomingSessionID);
            writer.WriteByte(OutgoingSessionID);
            writer.WriteNetEndian(MTU);
            writer.WriteNetEndian(WindowSize);
            writer.WriteNetEndian(ChannelCount);
            writer.WriteNetEndian(IncomingBandwidth);
            writer.WriteNetEndian(OutgoingBandwidth);
            writer.WriteNetEndian(PacketThrottleInterval);
            writer.WriteNetEndian(PacketThrottleAcceleration);
            writer.WriteNetEndian(PacketThrottleDeceleration);
            writer.WriteNetEndian(ConnectID);
        }

        public void ReadFrom(ref ENetPacketReader reader)
        {
            Header.ReadFrom(ref reader);
            OutgoingPeerID = reader.ReadNetUInt16();
            IncomingSessionID = reader.ReadByte();
            OutgoingSessionID = reader.ReadByte();
            MTU = reader.ReadNetUInt32();
            WindowSize = reader.ReadNetUInt32();
            ChannelCount = reader.ReadNetUInt32();
            IncomingBandwidth = reader.ReadNetUInt32();
            OutgoingBandwidth = reader.ReadNetUInt32();
            PacketThrottleInterval = reader.ReadNetUInt32();
            PacketThrottleAcceleration = reader.ReadNetUInt32();
            PacketThrottleDeceleration = reader.ReadNetUInt32();
            ConnectID = reader.ReadNetUInt32();
        }
    }
}
