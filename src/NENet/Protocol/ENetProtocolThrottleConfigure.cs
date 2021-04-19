using System.Runtime.InteropServices;

using ENetDotNet.Protocol;

namespace ENetDotNet.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ENetProtocolThrottleConfigure
    {
        public ENetProtocolCommandHeader Header;
        public uint PacketThrottleInterval;
        public uint PacketThrottleAcceleration;
        public uint PacketThrottleDeceleration;

        public readonly void WriteTo(ENetPacketWriter writer)
        {
            Header.WriteTo(writer);
            writer.WriteNetEndian(PacketThrottleInterval);
            writer.WriteNetEndian(PacketThrottleAcceleration);
            writer.WriteNetEndian(PacketThrottleDeceleration);
        }

        public void ReadFrom(ref ENetPacketReader reader)
        {
            Header.ReadFrom(ref reader);
            PacketThrottleInterval = reader.ReadNetUInt32();
            PacketThrottleAcceleration = reader.ReadNetUInt32();
            PacketThrottleDeceleration = reader.ReadNetUInt32();
        }
    }
}
