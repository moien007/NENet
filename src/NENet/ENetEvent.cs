using System.Runtime.CompilerServices;

namespace ENetDotNet
{
    public readonly struct ENetEvent
    {
        public ENetPeer Peer { get; }
        public ENetPacket Packet { get; }
        public uint Data { get; }
        public ENetEventType Type { get; }

        private ENetEvent(ENetPeer peer, ENetPacket? packet, uint data, ENetEventType type)
        {
            Peer = peer;
            Packet = packet!;
            Data = data;
            Type = type;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ENetEvent CreateNone() => new(peer: null!, packet: null!, data: 0, type: ENetEventType.None);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ENetEvent CreateConnect(ENetPeer peer, uint data) => new(peer: peer, packet: null, data: data, type: ENetEventType.Connect);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ENetEvent CreateDisconnect(ENetPeer peer, uint data) => new(peer: peer, packet: null, data: data, type: ENetEventType.Disconnect);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ENetEvent CreateReceive(ENetPeer peer, ENetPacket packet) => new(peer: peer, packet: packet, data: default, type: ENetEventType.Receive);
    }
}
