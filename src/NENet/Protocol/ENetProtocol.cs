using System;
using System.Runtime.InteropServices;
using ENetDotNet.Internal;

using static ENetDotNet.Internal.Constants;

namespace ENetDotNet.Protocol
{
    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct ENetProtocol
    {
        private delegate void CommandHandlerDelegate(in ENetProtocol union, ENetPeer peer, ref ENetPacketReader dataReader);
        private delegate void CommandReaderDelegate(ref ENetProtocol union, ref ENetPacketReader reader);
        private delegate void CommandWriterDelegate(in ENetProtocol union, ENetPacketWriter writer);
        
        private static readonly int[] s_CommandSizes =
        {
            0,
            sizeof(ENetProtocolAcknowledge),
            sizeof(ENetProtocolConnect),
            sizeof(ENetProtocolVerifyConnect),
            sizeof(ENetProtocolDisconnect),
            sizeof(ENetProtocolPing),
            sizeof(ENetProtocolSendReliable),
            sizeof(ENetProtocolSendUnreliable),
            sizeof(ENetProtocolSendFragment),
            sizeof(ENetProtocolSendUnsequenced),
            sizeof(ENetProtocolBandwidthLimit),
            sizeof(ENetProtocolThrottleConfigure),
            sizeof(ENetProtocolSendFragment), // repeated
        };

        private static readonly CommandHandlerDelegate[] s_CommandHandlers =
        {
            (in ENetProtocol _, ENetPeer _, ref ENetPacketReader _) => throw new InvalidOperationException(),
            (in ENetProtocol u, ENetPeer p, ref ENetPacketReader dr) => p.HandleAcknowledgeCommand(in u.Acknowledge, ref dr),
            (in ENetProtocol u, ENetPeer p, ref ENetPacketReader dr) => p.HandleConnectCommand(in u.Connect, ref dr),
            (in ENetProtocol u, ENetPeer p, ref ENetPacketReader dr) => p.HandleVerifyConnectCommand(in u.VerifyConnect, ref dr),
            (in ENetProtocol u, ENetPeer p, ref ENetPacketReader dr) => p.HandleDisconnectCommand(in u.Disconnect, ref dr),
            (in ENetProtocol u, ENetPeer p, ref ENetPacketReader dr) => p.HandlePingCommand(in u.Ping, ref dr),
            (in ENetProtocol u, ENetPeer p, ref ENetPacketReader dr) => p.HandleSendReliableCommand(in u, ref dr),
            (in ENetProtocol u, ENetPeer p, ref ENetPacketReader dr) => p.HandleSendUnreliableCommand(in u, ref dr),
            (in ENetProtocol u, ENetPeer p, ref ENetPacketReader dr) => p.HandleSendFragmentCommand(in u, ref dr),
            (in ENetProtocol u, ENetPeer p, ref ENetPacketReader dr) => p.HandleSendUnsequencedCommand(in u, ref dr),
            (in ENetProtocol u, ENetPeer p, ref ENetPacketReader dr) => p.HandleBandwidthLimitCommand(in u.BandwidthLimit, ref dr),
            (in ENetProtocol u, ENetPeer p, ref ENetPacketReader dr) => p.HandleThrottleConfigureCommand(in u.ThrottleConfigure, ref dr),
            (in ENetProtocol u, ENetPeer p, ref ENetPacketReader dr) => p.HandleSendUnreliableFragmentCommand(in u, ref dr),
        };

        private static readonly CommandReaderDelegate[] s_CommandReaders =
        {
            (ref ENetProtocol _, ref ENetPacketReader _) => throw new InvalidOperationException(),
            (ref ENetProtocol u, ref ENetPacketReader br) => u.Acknowledge.ReadFrom(ref br),
            (ref ENetProtocol u, ref ENetPacketReader br) => u.Connect.ReadFrom(ref br),
            (ref ENetProtocol u, ref ENetPacketReader br) => u.VerifyConnect.ReadFrom(ref br),
            (ref ENetProtocol u, ref ENetPacketReader br) => u.Disconnect.ReadFrom(ref br),
            (ref ENetProtocol u, ref ENetPacketReader br) => u.Ping.ReadFrom(ref br),
            (ref ENetProtocol u, ref ENetPacketReader br) => u.SendReliable.ReadFrom(ref br),
            (ref ENetProtocol u, ref ENetPacketReader br) => u.SendUnreliable.ReadFrom(ref br),
            (ref ENetProtocol u, ref ENetPacketReader br) => u.SendFragment.ReadFrom(ref br),
            (ref ENetProtocol u, ref ENetPacketReader br) => u.SendUnsequenced.ReadFrom(ref br),
            (ref ENetProtocol u, ref ENetPacketReader br) => u.BandwidthLimit.ReadFrom(ref br),
            (ref ENetProtocol u, ref ENetPacketReader br) => u.ThrottleConfigure.ReadFrom(ref br),
            (ref ENetProtocol u, ref ENetPacketReader br) => u.SendFragment.ReadFrom(ref br),
        };

        static readonly CommandWriterDelegate[] s_CommandWriters =
        {
            (in ENetProtocol _, ENetPacketWriter _) => throw new InvalidOperationException(),
            (in ENetProtocol u, ENetPacketWriter bw) => u.Acknowledge.WriteTo(bw),
            (in ENetProtocol u, ENetPacketWriter bw) => u.Connect.WriteTo(bw),
            (in ENetProtocol u, ENetPacketWriter bw) => u.VerifyConnect.WriteTo(bw),
            (in ENetProtocol u, ENetPacketWriter bw) => u.Disconnect.WriteTo(bw),
            (in ENetProtocol u, ENetPacketWriter bw) => u.Ping.WriteTo(bw),
            (in ENetProtocol u, ENetPacketWriter bw) => u.SendReliable.WriteTo(bw),
            (in ENetProtocol u, ENetPacketWriter bw) => u.SendUnreliable.WriteTo(bw),
            (in ENetProtocol u, ENetPacketWriter bw) => u.SendFragment.WriteTo(bw),
            (in ENetProtocol u, ENetPacketWriter bw) => u.SendUnsequenced.WriteTo(bw),
            (in ENetProtocol u, ENetPacketWriter bw) => u.BandwidthLimit.WriteTo(bw),
            (in ENetProtocol u, ENetPacketWriter bw) => u.ThrottleConfigure.WriteTo(bw),
            (in ENetProtocol u, ENetPacketWriter bw) => u.SendFragment.WriteTo(bw),
        };

        [FieldOffset(0)] public ENetProtocolCommandHeader Header;
        [FieldOffset(0)] public ENetProtocolAcknowledge Acknowledge;
        [FieldOffset(0)] public ENetProtocolConnect Connect;
        [FieldOffset(0)] public ENetProtocolVerifyConnect VerifyConnect;
        [FieldOffset(0)] public ENetProtocolDisconnect Disconnect;
        [FieldOffset(0)] public ENetProtocolPing Ping;
        [FieldOffset(0)] public ENetProtocolSendReliable SendReliable;
        [FieldOffset(0)] public ENetProtocolSendUnreliable SendUnreliable;
        [FieldOffset(0)] public ENetProtocolSendUnsequenced SendUnsequenced;
        [FieldOffset(0)] public ENetProtocolSendFragment SendFragment;
        [FieldOffset(0)] public ENetProtocolBandwidthLimit BandwidthLimit;
        [FieldOffset(0)] public ENetProtocolThrottleConfigure ThrottleConfigure;
        
        public readonly void Handle(ENetPeer peer, ref ENetPacketReader dataReader, byte commandNumber)
        {
            var handler = s_CommandHandlers[commandNumber & ENET_PROTOCOL_COMMAND_MASK];
            handler.Invoke(in this, peer, ref dataReader);
        }

        public readonly void WriteTo(ENetPacketWriter byteWriter, byte commandNumber)
        {
            var writer = s_CommandWriters[commandNumber & ENET_PROTOCOL_COMMAND_MASK];
            writer.Invoke(in this, byteWriter);
        }

        public void ReadFrom(ref ENetPacketReader byteReader, byte commandNumber)
        {
            var reader = s_CommandReaders[commandNumber & ENET_PROTOCOL_COMMAND_MASK];
            reader.Invoke(ref this, ref byteReader);
        }

        public static int CommandSize(byte commandNumber)
        {
            return s_CommandSizes[commandNumber & ENET_PROTOCOL_COMMAND_MASK];
        }
    }
}
