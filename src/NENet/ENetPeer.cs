using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using ENetDotNet.Internal;
using ENetDotNet.Protocol;

using static ENetDotNet.Internal.Constants;

namespace ENetDotNet
{
    internal sealed class ENetAcknowledge
    {
        public PooledLinkedList<ENetAcknowledge>.Node? AcknowledgementListNode;
        public uint SentTime;
        public ENetProtocolAcknowledge Command;

        public void Reset()
        {
            AcknowledgementListNode?.Remove();
            SentTime = default;
            Command = default;
        }
    }

    internal sealed class ENetIncomingCommand
    {
        public PooledLinkedList<ENetIncomingCommand>.Node? IncomingCommandListNode;
        public ushort reliableSequenceNumber;
        public ushort unreliableSequenceNumber;
        public ENetProtocol command;
        public readonly List<uint> fragments = new();
        public uint fragmentsRemaining;
        public ENetPacket? packet;

        public void Reset()
        {
            packet?.Dispose();
            packet = null;
            IncomingCommandListNode?.Remove();
            fragments.Clear();

            reliableSequenceNumber = default;
            unreliableSequenceNumber = default;
            fragmentsRemaining = default;
        }
    }

    internal sealed class ENetOutgoingCommand
    {
        public PooledLinkedList<ENetOutgoingCommand>.Node? OutgoingCommandListNode;
        public ushort reliableSequenceNumber;
        public ushort unreliableSequenceNumber;
        public uint sentTime;
        public uint roundTripTimeout;
        public uint roundTripTimeoutLimit;
        public uint fragmentOffset;
        public ushort fragmentLength;
        public ushort sendAttempts;
        public ENetProtocol command;
        public ENetPacket? packet;

        public void Reset()
        {
            packet?.Dispose();
            packet = null;
            OutgoingCommandListNode?.Remove();

            reliableSequenceNumber = default;
            unreliableSequenceNumber = default;
            sentTime = default;
            roundTripTimeout = default;
            roundTripTimeoutLimit = default;
            fragmentOffset = default;
            fragmentLength = default;
            sendAttempts = default;
        }
    }

    internal sealed class ENetChannel
    {
        public readonly int Id;

        public ushort outgoingReliableSequenceNumber;
        public ushort outgoingUnreliableSequenceNumber;
        public ushort usedReliableWindows;
        public readonly ushort[] reliableWindows = new ushort[ENET_PEER_RELIABLE_WINDOWS];
        public ushort incomingReliableSequenceNumber;
        public ushort incomingUnreliableSequenceNumber;
        public readonly PooledLinkedList<ENetIncomingCommand> incomingReliableCommands = new();
        public readonly PooledLinkedList<ENetIncomingCommand> incomingUnreliableCommands = new();

        public ENetChannel(int id)
        {
            Id = id;
        }
    }

    public sealed class ENetPeer
    {
        internal readonly PooledLinkedList<ENetAcknowledge> m_AcknowledgementList = new();
        internal readonly List<ENetChannel> m_ChannelList = new();
        internal readonly PooledLinkedList<ENetOutgoingCommand> m_OutgoingCommands = new();
        internal readonly PooledLinkedList<ENetOutgoingCommand> m_SentReliableCommands = new();
        internal ushort m_PeerId;
        internal ushort incomingPeerId;
        internal ushort outgoingPeerId = ENET_PROTOCOL_MAXIMUM_PEER_ID;
        internal byte incomingSessionId, outgoingSessionId;
        internal uint m_ConnectId;
        internal ulong m_IncomingDataTotal;
        internal ENetPeerState m_State = ENetPeerState.Disconnected;
        internal ulong lastRoundTripTime = ENET_PEER_DEFAULT_ROUND_TRIP_TIME;
        internal ulong lastRoundTripTimeVariance;
        internal ulong packetThrottleLimit = ENET_PEER_PACKET_THROTTLE_SCALE;
        internal ulong packetThrottle = ENET_PEER_DEFAULT_PACKET_THROTTLE;
        internal uint packetThrottleAcceleration = ENET_PEER_PACKET_THROTTLE_ACCELERATION;
        internal uint packetThrottleDeceleration = ENET_PEER_PACKET_THROTTLE_DECELERATION;
        internal uint packetThrottleInterval = ENET_PEER_PACKET_THROTTLE_INTERVAL;
        internal ulong lastReceiveTime, lastSendTime;
        internal ulong roundTripTime = ENET_PEER_DEFAULT_ROUND_TRIP_TIME;
        internal ulong highestRoundTripTimeVariance;
        internal ulong roundTripTimeVariance;
        internal ulong lowestRoundTripTime = ENET_PEER_DEFAULT_ROUND_TRIP_TIME;
        internal ulong packetThrottleEpoch;
        internal ulong reliableDataInTransit;
        internal ulong timeoutLimit = ENET_PEER_TIMEOUT_LIMIT,
                    timeoutMinimum = ENET_PEER_TIMEOUT_MINIMUM,
                    timeoutMaximum = ENET_PEER_TIMEOUT_MAXIMUM;
        internal ulong packetLossEpoch;
        internal uint packetsSent;
        internal uint packetsLost;
        internal uint packetLoss;
        internal uint packetLossVariance;
        internal uint incomingBandwidth, outgoingBandwidth;
        internal ulong pingInterval = ENET_PEER_PING_INTERVAL;
        internal ulong incomingBandwidthThrottleEpoch, outgoingBandwidthThrottleEpoch;
        internal int earliestTimeout;
        internal ulong incomingDataTotal, outgoingDataTotal;
        internal uint eventData;
        internal uint nextTimeout;
        internal uint windowSize = ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;
        internal readonly uint[] unsequencedWindow = new uint[ENET_PEER_UNSEQUENCED_WINDOW_SIZE / 32];
        internal uint mtu;
        internal int m_Flags;
        internal ushort incomingUnsequencedGroup, outgoingUnsequencedGroup, outgoingReliableSequenceNumber;
        internal readonly Queue<ENetOutgoingCommand> m_SendFragmentsBuffer = new();

        public ENetHost Host { get; }
        public IPEndPoint RemoteEndPoint { get; }
        public object? UserData { get; set; }
        public int ChannelCount { get; internal set; }
        public ENetPeerState State => m_State;

        internal ENetPeer(ENetHost host, IPEndPoint remoteEndPoint)
        {
            Host = host;
            RemoteEndPoint = remoteEndPoint;
        }

        public unsafe void Send(ENetPacket packet)
        {
            var channel = GetOrAddChannelById(packet.Channel);
            var fragments = m_SendFragmentsBuffer;

            if (m_State != ENetPeerState.Connected ||
               packet.Channel >= this.ChannelCount ||
               packet.DataLength > this.Host.maximumPacketSize)
                throw new Exception(); //return -1;

            var fragmentLength = this.mtu - sizeof(ENetProtocolHeader) - sizeof(ENetProtocolSendFragment);
            if (this.Host.Checksum != null)
                fragmentLength -= 4;

            if (packet.DataLength > fragmentLength)
            {
                uint fragmentCount = (uint)((packet.DataLength + fragmentLength - 1) / fragmentLength),
                       fragmentNumber,
                       fragmentOffset;
                byte commandNumber;
                ushort startSequenceNumber;
                ENetOutgoingCommand fragment;

                if (fragmentCount > ENET_PROTOCOL_MAXIMUM_FRAGMENT_COUNT)
                    throw new Exception(); //return -1;

                if ((packet.Flags & (ENetPacketFlags.Reliable | ENetPacketFlags.UnreliableFragments)) == ENetPacketFlags.UnreliableFragments &&
                    channel.outgoingUnreliableSequenceNumber < 0xFFFF)
                {
                    commandNumber = ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE_FRAGMENT;
                    startSequenceNumber = (ushort)(channel.outgoingUnreliableSequenceNumber + 1);
                }
                else
                {
                    commandNumber = ENET_PROTOCOL_COMMAND_SEND_FRAGMENT | ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE;
                    startSequenceNumber = (ushort)(channel.outgoingReliableSequenceNumber + 1);
                }

                m_SendFragmentsBuffer.Clear();

                for (fragmentNumber = 0,
                       fragmentOffset = 0;
                     fragmentOffset < packet.DataLength;
                     ++fragmentNumber,
                       fragmentOffset = (uint)(fragmentOffset + fragmentLength))
                {
                    if (packet.DataLength - fragmentOffset < fragmentLength)
                        fragmentLength = packet.DataLength - fragmentOffset;

                    fragment = Host.m_OutgoingCommandPool.Rent();

                    fragment.fragmentOffset = fragmentOffset;
                    fragment.fragmentLength = (ushort)fragmentLength;
                    fragment.packet = packet;
                    fragment.command.Header.Command = commandNumber;
                    fragment.command.Header.ChannelID = (byte)packet.Channel;
                    fragment.command.SendFragment.StartSequenceNumber = startSequenceNumber;
                    fragment.command.SendFragment.DataLength = (ushort)fragmentLength;
                    fragment.command.SendFragment.FragmentCount = fragmentCount;
                    fragment.command.SendFragment.FragmentNumber = fragmentNumber;
                    fragment.command.SendFragment.TotalLength = (uint)packet.DataLength;
                    fragment.command.SendFragment.FragmentOffset = fragmentOffset;

                    fragments.Enqueue(fragment);
                }

                packet.IncrementRefCount();

                while (fragments.TryDequeue(out var fragmentCommand))
                {
                    SetupOutgoingCommand(fragmentCommand);
                }

                //return 0;
            }
        }

        public void Ping()
        {
            if (this.m_State != ENetPeerState.Connected)
                return;

            ENetProtocol command = default;

            command.Header.Command = ENET_PROTOCOL_COMMAND_PING | ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE;
            command.Header.ChannelID = 0xFF;

            QueueOutgoingCommand(in command, packet: null, offset: 0, length: 0);
        }

        public void PingInterval(TimeSpan interval)
        {
            this.pingInterval = interval != TimeSpan.Zero ? (ulong)interval.TotalMilliseconds : ENET_PEER_PING_INTERVAL;
        }

        public void Timeout(TimeSpan timeoutLimit, TimeSpan timeoutMinimum, TimeSpan timeoutMaximum)
        {
            this.timeoutLimit = timeoutLimit != TimeSpan.Zero ? (ulong)timeoutLimit.TotalMilliseconds : ENET_PEER_TIMEOUT_LIMIT;
            this.timeoutMinimum = timeoutMinimum != TimeSpan.Zero ? (ulong)timeoutMinimum.TotalMilliseconds : ENET_PEER_TIMEOUT_MINIMUM;
            this.timeoutMaximum = timeoutMaximum != TimeSpan.Zero ? (ulong)timeoutMaximum.TotalMilliseconds : ENET_PEER_TIMEOUT_MAXIMUM;
        }

        public void ThrottleConfigure(TimeSpan interval, uint acceleration, uint deceleration)
        {
            ENetProtocol command = default;

            this.packetThrottleInterval = (uint)interval.TotalMilliseconds;
            this.packetThrottleAcceleration = acceleration;
            this.packetThrottleDeceleration = deceleration;

            command.Header.Command = ENET_PROTOCOL_COMMAND_THROTTLE_CONFIGURE | ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE;
            command.Header.ChannelID = 0xFF;

            command.ThrottleConfigure.PacketThrottleInterval = (uint)interval.TotalMilliseconds;
            command.ThrottleConfigure.PacketThrottleAcceleration = acceleration;
            command.ThrottleConfigure.PacketThrottleDeceleration = deceleration;

            QueueOutgoingCommand(in command, packet: null, offset: 0, length: 0);
        }

        internal bool Unlist() => Host.m_Peers.TryRemovePeer(this);

        public void DisconnectLater(uint data)
        {
            if ((this.m_State == ENetPeerState.Connected || this.m_State == ENetPeerState.DisconnectLater) &&
                !(m_OutgoingCommands.IsEmpty &&
                   m_SentReliableCommands.IsEmpty))
            {
                this.m_State = ENetPeerState.DisconnectLater;
                this.eventData = data;
            }
            else
                Disconnect(data);
        }

        public void DisconnectNow(uint data)
        {
            if (this.m_State == ENetPeerState.Disconnected)
                return;

            if (this.m_State != ENetPeerState.Zombie &&
                this.m_State != ENetPeerState.Disconnecting)
            {
                ENetProtocol command = default;

                command.Header.Command = ENET_PROTOCOL_COMMAND_DISCONNECT | ENET_PROTOCOL_COMMAND_FLAG_UNSEQUENCED;
                command.Header.ChannelID = 0xFF;
                command.Disconnect.Data = data;

                QueueOutgoingCommand(in command, null, 0, 0);

                Host.Flush();
            }

            Unlist();
        }

        public void Disconnect(uint data)
        {
            if (m_State == ENetPeerState.Disconnecting ||
                m_State == ENetPeerState.Disconnected ||
                m_State == ENetPeerState.AcknowledgingDisconnect ||
                m_State == ENetPeerState.Zombie)
                return;

            ENetProtocol command = default;

            command.Header.Command = ENET_PROTOCOL_COMMAND_DISCONNECT;
            command.Header.ChannelID = 0xFF;
            command.Disconnect.Data = data;

            if (this.m_State == ENetPeerState.Connected || this.m_State == ENetPeerState.DisconnectLater)
                command.Header.Command |= ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE;
            else
                command.Header.Command |= ENET_PROTOCOL_COMMAND_FLAG_UNSEQUENCED;

            QueueOutgoingCommand(in command, packet: null, offset: 0, length: 0);

            if (this.m_State == ENetPeerState.Connected || this.m_State == ENetPeerState.DisconnectLater)
            {
                OnDisconnect();
                m_State = ENetPeerState.Disconnecting;
            }
            else
            {
                Host.Flush();
                Unlist();
            }
        }

        internal ENetChannel GetOrAddChannelById(int channelId)
        {
            for (int i = 0; i < m_ChannelList.Count; i++)
            {
                if (m_ChannelList[i].Id == channelId)
                    return m_ChannelList[i];
            }

            var channel = new ENetChannel(channelId);
            m_ChannelList.Add(channel);
            return channel;
        }

        internal ENetAcknowledge? QueueAcknowledgement(in ENetProtocolAcknowledge command, ushort sentTime)
        {
            if (command.Header.ChannelID < ChannelCount)
            {
                var channel = GetOrAddChannelById(command.Header.ChannelID);
                var reliableWindow = command.Header.ReliableSequenceNumber / ENET_PEER_RELIABLE_WINDOW_SIZE;
                var currentWindow = channel.incomingReliableSequenceNumber / ENET_PEER_RELIABLE_WINDOW_SIZE;

                if (command.Header.ReliableSequenceNumber < channel.incomingReliableSequenceNumber)
                    reliableWindow += ENET_PEER_RELIABLE_WINDOWS;

                if (reliableWindow >= currentWindow + ENET_PEER_FREE_RELIABLE_WINDOWS - 1 && reliableWindow <= currentWindow + ENET_PEER_FREE_RELIABLE_WINDOWS)
                    return null;
            }

            unsafe
            {
                this.outgoingDataTotal += (ulong)sizeof(ENetProtocolAcknowledge);
            }

            var ack = Host.m_AcknowledgePool.Rent();
            ack.SentTime = sentTime;
            ack.Command = command;
            ack.AcknowledgementListNode = m_AcknowledgementList.AddLast(ack);

            return ack;
        }

        internal void Throttle(ulong rtt)
        {
            if (lastRoundTripTime <= lastRoundTripTimeVariance)
            {
                packetThrottle = packetThrottleLimit;
            }
            else if (rtt <= lastRoundTripTime)
            {
                packetThrottle += packetThrottleAcceleration;

                if (packetThrottle > packetThrottleLimit)
                    packetThrottle = packetThrottleLimit;

                return; //return 1;
            }
            else if (rtt > lastRoundTripTime + 2 * lastRoundTripTimeVariance)
            {
                if (packetThrottle > packetThrottleDeceleration)
                    packetThrottle -= packetThrottleDeceleration;
                else
                    packetThrottle = 0;

                return; //return -1;
            }

            //return 0;
        }

        internal int RemoveSentReliableCommand(uint reliableSequenceNumber, int channelID)
        {
            var wasSent = true;
            ENetOutgoingCommand? outgoingCommand = null;

            foreach (var currentOutgoingCommand in m_SentReliableCommands)
            {
                if (currentOutgoingCommand.reliableSequenceNumber == reliableSequenceNumber &&
                    currentOutgoingCommand.command.Header.ChannelID == channelID)
                {
                    outgoingCommand = currentOutgoingCommand;
                    break;
                }
            }

            if (outgoingCommand == null)
            {
                foreach (var currentOutgoingCommand in m_OutgoingCommands)
                {
                    if ((currentOutgoingCommand.command.Header.Command & ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE) == 0)
                        continue;

                    if (currentOutgoingCommand.sendAttempts < 1)
                        return ENET_PROTOCOL_COMMAND_NONE;

                    if (currentOutgoingCommand.reliableSequenceNumber == reliableSequenceNumber &&
                        currentOutgoingCommand.command.Header.ChannelID == channelID)
                    {
                        outgoingCommand = currentOutgoingCommand;
                        break;
                    }
                }

                if (outgoingCommand == null)
                    return ENET_PROTOCOL_COMMAND_NONE;

                wasSent = false;
            }

            if (channelID < ChannelCount)
            {
                var channel = GetOrAddChannelById(channelID);
                var reliableWindow = reliableSequenceNumber / ENET_PEER_RELIABLE_WINDOW_SIZE;
                if (channel.reliableWindows[reliableWindow] > 0)
                {
                    --channel.reliableWindows[reliableWindow];
                    if (channel.reliableWindows[reliableWindow] == 0)
                        channel.usedReliableWindows &= unchecked((ushort)~(1 << (int)reliableWindow));
                }
            }

            var commandNumber = outgoingCommand.command.Header.Command & ENET_PROTOCOL_COMMAND_MASK;

            outgoingCommand.OutgoingCommandListNode!.Remove();
            outgoingCommand.OutgoingCommandListNode = null;

            if (outgoingCommand.packet != null)
            {
                if (wasSent)
                    reliableDataInTransit -= outgoingCommand.fragmentLength;

                outgoingCommand.packet.Dispose();
                outgoingCommand.packet = null;
            }

            Host.m_OutgoingCommandPool.Return(outgoingCommand);

            if (!m_SentReliableCommands.IsEmpty)
            {
                outgoingCommand = m_SentReliableCommands.FirstNode.Value;
                nextTimeout = outgoingCommand.sentTime + outgoingCommand.roundTripTimeout;
            }

            return commandNumber;
        }

        internal void OnDisconnect()
        {
            if (m_State == ENetPeerState.Connected || m_State == ENetPeerState.DisconnectLater)
            {
                if (incomingBandwidth != 0)
                    --Host.bandwidthLimitedPeers;
            }
        }

        internal void OnConnect()
        {
            if (m_State != ENetPeerState.Connected && m_State != ENetPeerState.DisconnectLater)
            {
                if (incomingBandwidth != 0)
                    ++Host.bandwidthLimitedPeers;
            }
        }

        internal void ChangeState(ENetPeerState state)
        {
            if (state == ENetPeerState.Connected || state == ENetPeerState.DisconnectLater)
                OnConnect();
            else
                OnDisconnect();

            m_State = state;
        }

        internal void NotifyConnect() => NotifyConnect(eventData);
        internal void NotifyConnect(uint data)
        {
            Host.recalculateBandwidthLimits = true;

            ChangeState(m_State == ENetPeerState.Connecting ? ENetPeerState.ConnectionSucceeded : ENetPeerState.ConnectionPending);

            Host.InvokeOnEvent(ENetEvent.CreateConnect(this, data));
        }

        internal void NotifyDisconnect(uint data = 0)
        {
            if (m_State >= ENetPeerState.ConnectionPending)
                Host.recalculateBandwidthLimits = true;

            ChangeState(ENetPeerState.Disconnected);
            Host.InvokeOnEvent(ENetEvent.CreateDisconnect(this, data));
            Unlist();
        }

        internal void SetupOutgoingCommand(ENetOutgoingCommand outgoingCommand)
        {
            var channel = GetOrAddChannelById(outgoingCommand.command.Header.ChannelID);

            if (outgoingCommand.command.Header.ChannelID == 0xFF)
            {
                ++this.outgoingReliableSequenceNumber;

                outgoingCommand.reliableSequenceNumber = this.outgoingReliableSequenceNumber;
                outgoingCommand.unreliableSequenceNumber = 0;
            }
            else if ((outgoingCommand.command.Header.Command & ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE) != 0)
            {
                ++channel.outgoingReliableSequenceNumber;
                channel.outgoingUnreliableSequenceNumber = 0;

                outgoingCommand.reliableSequenceNumber = channel.outgoingReliableSequenceNumber;
                outgoingCommand.unreliableSequenceNumber = 0;
            }
            else if ((outgoingCommand.command.Header.Command & ENET_PROTOCOL_COMMAND_FLAG_UNSEQUENCED) != 0)
            {
                ++this.outgoingUnsequencedGroup;

                outgoingCommand.reliableSequenceNumber = 0;
                outgoingCommand.unreliableSequenceNumber = 0;
            }
            else
            {
                if (outgoingCommand.fragmentOffset == 0)
                    ++channel.outgoingUnreliableSequenceNumber;

                outgoingCommand.reliableSequenceNumber = channel.outgoingReliableSequenceNumber;
                outgoingCommand.unreliableSequenceNumber = channel.outgoingUnreliableSequenceNumber;
            }

            outgoingCommand.sendAttempts = 0;
            outgoingCommand.sentTime = 0;
            outgoingCommand.roundTripTimeout = 0;
            outgoingCommand.roundTripTimeoutLimit = 0;
            outgoingCommand.command.Header.ReliableSequenceNumber = outgoingCommand.reliableSequenceNumber;

            switch (outgoingCommand.command.Header.Command & ENET_PROTOCOL_COMMAND_MASK)
            {
                case ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE:
                    outgoingCommand.command.SendUnreliable.UnreliableSequenceNumber =
                        outgoingCommand.unreliableSequenceNumber;
                    break;

                case ENET_PROTOCOL_COMMAND_SEND_UNSEQUENCED:
                    outgoingCommand.command.SendUnsequenced.UnsequencedGroup = this.outgoingUnsequencedGroup;
                    break;

                default:
                    break;
            }

            outgoingCommand.OutgoingCommandListNode = m_OutgoingCommands.AddLast(outgoingCommand);
        }

        internal ENetOutgoingCommand QueueOutgoingCommand(in ENetProtocol command, ENetPacket? packet, uint offset, ushort length)
        {
            var outgoingCommand = Host.m_OutgoingCommandPool.Rent();

            outgoingCommand.command = command; // unlike the original code, we are moving it
            outgoingCommand.fragmentOffset = offset;
            outgoingCommand.fragmentLength = length;
            outgoingCommand.packet = packet;

            if (packet != null)
            {
                packet.IncrementRefCount();
            }

            SetupOutgoingCommand(outgoingCommand);

            return outgoingCommand;
        }

        internal ENetIncomingCommand? QueueIncomingCommand(in ENetProtocol command, ReadOnlySpan<byte> data, int dataLength, ENetPacketFlags flags, uint fragmentCount)
        {
            throw new NotImplementedException();
        }

        internal void DispatchIncomingReliableCommands(ENetChannel channel, ENetIncomingCommand? queuedCommand)
        {
            throw new NotImplementedException();
        }

        internal void DispatchIncomingUnreliableCommands(ENetChannel channel, ENetIncomingCommand? queuedCommand)
        {
            throw new NotImplementedException();
        }

        internal void SendAcknowledgements()
        {
            throw new NotImplementedException();
        }

        internal void RemoveSentUnreliableCommands()
        {
            throw new NotImplementedException();
        }

        internal void CheckTimeouts()
        {
            throw new NotImplementedException();
        }

        internal bool CheckOutgoingCommands()
        {
            throw new NotImplementedException();
        }

        internal void Reset()
        {
            ResetQueues();
            Unlist();
        }

        internal void ResetQueues()
        {
            for (var current = m_AcknowledgementList.FirstNodeOrNull; current != null;)
            {
                var acknowledge = current.Value;
                var next = current.Next;
                current = next;

                acknowledge.Reset();
                Host.m_AcknowledgePool.Return(acknowledge);
            }

            for (var current = m_SentReliableCommands.FirstNodeOrNull; current != null;)
            {
                var outgoingCommand = current.Value;
                var next = current.Next;
                current = next;

                outgoingCommand.Reset();
                Host.m_OutgoingCommandPool.Return(outgoingCommand);
            }

            for (var current = m_OutgoingCommands.FirstNodeOrNull; current != null;)
            {
                var outgoingCommand = current.Value;
                var next = current.Next;
                current = next;

                outgoingCommand.Reset();
                Host.m_OutgoingCommandPool.Return(outgoingCommand);
            }

            foreach (var outgoingCommand in m_SendFragmentsBuffer)
            {
                outgoingCommand.Reset();
                Host.m_OutgoingCommandPool.Return(outgoingCommand);
            }
            m_SendFragmentsBuffer.Clear();

            foreach (var channel in m_ChannelList)
            {
                for (var current = channel.incomingReliableCommands.FirstNodeOrNull; current != null;)
                {
                    var incomingCommand = current.Value;
                    var next = current.Next;
                    current = next;

                    incomingCommand.Reset();
                    Host.m_IncomingCommandPool.Return(incomingCommand);
                }

                for (var current = channel.incomingUnreliableCommands.FirstNodeOrNull; current != null;)
                {
                    var incomingCommand = current.Value;
                    var next = current.Next;
                    current = next;

                    incomingCommand.Reset();
                    Host.m_IncomingCommandPool.Return(incomingCommand);
                }
            }
        }

        internal void HandleConnectCommand(in ENetProtocolConnect connect, ref ENetPacketReader dataReader)
        {
            if (m_State != ENetPeerState.Disconnected)
                return;

            if (connect.ChannelCount < ENET_PROTOCOL_MINIMUM_CHANNEL_COUNT ||
                connect.ChannelCount > ENET_PROTOCOL_MAXIMUM_CHANNEL_COUNT)
                return;

            var duplicatePeers = 0;

            foreach (var listedPeer in Host.m_Peers)
            {
                if (listedPeer.RemoteEndPoint.Address.Equals(RemoteEndPoint.Address))
                {
                    duplicatePeers++;
                }
            }

            if (duplicatePeers >= Host.duplicatePeers)
                return;

            this.ChannelCount = (int)connect.ChannelCount;
            this.m_State = ENetPeerState.AcknowledgingConnect;
            this.m_ConnectId = connect.ConnectID;
            this.outgoingPeerId = connect.OutgoingPeerID;
            this.incomingBandwidth = connect.IncomingBandwidth;
            this.outgoingBandwidth = connect.OutgoingBandwidth;
            this.packetThrottleInterval = connect.PacketThrottleInterval;
            this.packetThrottleAcceleration = connect.PacketThrottleAcceleration;
            this.packetThrottleDeceleration = connect.PacketThrottleDeceleration;
            this.eventData = connect.Data;

            unchecked
            {
                outgoingSessionId = Host.m_Random.NextByte();
                incomingSessionId = Host.m_Random.NextByte();
                if (incomingSessionId == outgoingSessionId)
                    incomingSessionId++;
            }

            mtu = Math.Clamp(connect.MTU, ENET_PROTOCOL_MINIMUM_MTU, ENET_PROTOCOL_MAXIMUM_MTU);

            if (Host.outgoingBandwidth == 0 && this.incomingBandwidth == 0)
                this.windowSize = ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;
            else if (Host.outgoingBandwidth == 0 || this.incomingBandwidth == 0)
                this.windowSize = Math.Max(Host.outgoingBandwidth, this.incomingBandwidth) /
                                              ENET_PEER_WINDOW_SIZE_SCALE *
                                                ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;
            else
                this.windowSize = Math.Min(Host.outgoingBandwidth, this.incomingBandwidth) /
                                              ENET_PEER_WINDOW_SIZE_SCALE *
                                                ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;

            this.windowSize = Math.Clamp(this.windowSize,
                                         ENET_PROTOCOL_MINIMUM_WINDOW_SIZE,
                                         ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE);

            var incomingWindowSize = 0u;
            if (Host.incomingBandwidth == 0)
                incomingWindowSize = ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;
            else
                incomingWindowSize = Host.incomingBandwidth / ENET_PEER_WINDOW_SIZE_SCALE *
                               ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;

            if (incomingWindowSize > connect.WindowSize)
                incomingWindowSize = connect.WindowSize;

            incomingWindowSize = Math.Clamp(incomingWindowSize,
                                            ENET_PROTOCOL_MINIMUM_WINDOW_SIZE,
                                            ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE);

            ENetProtocol verifyCommand = default;

            verifyCommand.Header.Command = ENET_PROTOCOL_COMMAND_VERIFY_CONNECT | ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE;
            verifyCommand.Header.ChannelID = 0xFF;
            verifyCommand.VerifyConnect.OutgoingPeerID = this.incomingPeerId;
            verifyCommand.VerifyConnect.IncomingSessionID = incomingSessionId;
            verifyCommand.VerifyConnect.OutgoingSessionID = outgoingSessionId;
            verifyCommand.VerifyConnect.MTU = this.mtu;
            verifyCommand.VerifyConnect.WindowSize = windowSize;
            verifyCommand.VerifyConnect.ChannelCount = unchecked((byte)ChannelCount);
            verifyCommand.VerifyConnect.IncomingBandwidth = Host.incomingBandwidth;
            verifyCommand.VerifyConnect.OutgoingBandwidth = Host.outgoingBandwidth;
            verifyCommand.VerifyConnect.PacketThrottleInterval = this.packetThrottleInterval;
            verifyCommand.VerifyConnect.PacketThrottleAcceleration = this.packetThrottleAcceleration;
            verifyCommand.VerifyConnect.PacketThrottleDeceleration = this.packetThrottleDeceleration;
            verifyCommand.VerifyConnect.ConnectID = this.m_ConnectId;

            QueueOutgoingCommand(in verifyCommand, packet: null, offset: 0, length: 0);
        }

        internal void HandleVerifyConnectCommand(in ENetProtocolVerifyConnect verifyConnect, ref ENetPacketReader dataReader)
        {
            if (m_State != ENetPeerState.Connecting)
                return;

            var channelCount = unchecked((byte)verifyConnect.ChannelCount);

            if (channelCount < ENET_PROTOCOL_MINIMUM_CHANNEL_COUNT || channelCount > ENET_PROTOCOL_MAXIMUM_CHANNEL_COUNT ||
                verifyConnect.PacketThrottleInterval != this.packetThrottleInterval ||
                verifyConnect.PacketThrottleAcceleration != this.packetThrottleAcceleration ||
                verifyConnect.PacketThrottleDeceleration != this.packetThrottleDeceleration ||
                verifyConnect.ConnectID != this.m_ConnectId)
            {
                NotifyDisconnect();
                return;
            }

            RemoveSentReliableCommand(1, 0xFF);

            if (channelCount < ChannelCount)
                ChannelCount = channelCount;

            this.outgoingPeerId = verifyConnect.OutgoingPeerID;
            this.incomingSessionId = verifyConnect.IncomingSessionID;
            this.outgoingSessionId = verifyConnect.OutgoingSessionID;

            var connectMTU = Math.Clamp(verifyConnect.MTU,
                                        ENET_PROTOCOL_MINIMUM_MTU,
                                        ENET_PROTOCOL_MAXIMUM_MTU);

            if (connectMTU < this.mtu)
                this.mtu = connectMTU;

            var outgoingWindowSize = Math.Clamp(verifyConnect.WindowSize,
                                                ENET_PROTOCOL_MINIMUM_WINDOW_SIZE,
                                                ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE);

            if (outgoingWindowSize < this.windowSize)
                this.windowSize = outgoingWindowSize;

            this.incomingBandwidth = verifyConnect.IncomingBandwidth;
            this.outgoingBandwidth = verifyConnect.OutgoingBandwidth;

            NotifyConnect();
        }

        internal void HandleThrottleConfigureCommand(in ENetProtocolThrottleConfigure throttleConfigure, ref ENetPacketReader dataReader)
        {
            if (m_State != ENetPeerState.Connected && m_State != ENetPeerState.DisconnectLater)
                return;

            packetThrottleInterval = throttleConfigure.PacketThrottleInterval;
            packetThrottleAcceleration = throttleConfigure.PacketThrottleAcceleration;
            packetThrottleDeceleration = throttleConfigure.PacketThrottleDeceleration;
        }

        internal void HandleDisconnectCommand(in ENetProtocolDisconnect disconnect, ref ENetPacketReader dataReader)
        {
            if (this.m_State == ENetPeerState.Disconnected || this.m_State == ENetPeerState.Zombie || this.m_State == ENetPeerState.AcknowledgingDisconnect)
                return;

            ResetQueues();

            if (this.m_State == ENetPeerState.ConnectionSucceeded || this.m_State == ENetPeerState.Disconnecting || this.m_State == ENetPeerState.Connecting)
                NotifyDisconnect(disconnect.Data);
            else
            if (this.m_State != ENetPeerState.Connected && this.m_State != ENetPeerState.DisconnectLater)
            {
                if (this.m_State == ENetPeerState.ConnectionPending) Host.recalculateBandwidthLimits = true;

                Reset();
            }
            else
            if ((disconnect.Header.Command & ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE) != 0)
                ChangeState(ENetPeerState.AcknowledgingDisconnect);
            else
                NotifyDisconnect(disconnect.Data);

            if (this.m_State != ENetPeerState.Disconnected)
                this.eventData = disconnect.Data;
        }

        internal void HandleBandwidthLimitCommand(in ENetProtocolBandwidthLimit bandwidthLimit, ref ENetPacketReader dataReader)
        {
            if (this.incomingBandwidth != 0)
                --Host.bandwidthLimitedPeers;

            this.incomingBandwidth = bandwidthLimit.IncomingBandwidth;
            this.outgoingBandwidth = bandwidthLimit.OutgoingBandwidth;

            if (this.incomingBandwidth != 0)
                ++Host.bandwidthLimitedPeers;

            if (this.incomingBandwidth == 0 && Host.outgoingBandwidth == 0)
                this.windowSize = ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;
            else
            if (this.incomingBandwidth == 0 || Host.outgoingBandwidth == 0)
                this.windowSize = Math.Max(this.incomingBandwidth, Host.outgoingBandwidth) /
                                       ENET_PEER_WINDOW_SIZE_SCALE * ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;
            else
                this.windowSize = Math.Min(this.incomingBandwidth, Host.outgoingBandwidth) /
                                       ENET_PEER_WINDOW_SIZE_SCALE * ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;

            if (this.windowSize < ENET_PROTOCOL_MINIMUM_WINDOW_SIZE)
                this.windowSize = ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;
            else
            if (this.windowSize > ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE)
                this.windowSize = ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;
        }

        internal void HandlePingCommand(in ENetProtocolPing ping, ref ENetPacketReader dataReader)
        {
            /* no-op */
        }

        internal void HandleAcknowledgeCommand(in ENetProtocolAcknowledge acknowledge, ref ENetPacketReader dataReader)
        {
            if (m_State == ENetPeerState.Disconnected || m_State == ENetPeerState.Zombie)
                return; //return 0;

            ulong receivedSentTime = acknowledge.ReceivedSentTime;
            receivedSentTime |= Host.m_ServiceTime & 0x7FFF_FFFF_FFFF_0000;

            if ((receivedSentTime & 0x8000) > (Host.m_ServiceTime & 0x8000))
                receivedSentTime -= 0x10000;

            if (Host.m_ServiceTime > receivedSentTime)
                return; // return 0;

            ulong roundTripTime = Host.m_ServiceTime - receivedSentTime;
            roundTripTime = Math.Max(roundTripTime, 1);

            if (lastReceiveTime > 0)
            {
                Throttle(roundTripTime);

                roundTripTimeVariance -= roundTripTimeVariance / 4;

                if (roundTripTime >= this.roundTripTime)
                {
                    var diff = roundTripTime - this.roundTripTime;
                    roundTripTimeVariance += diff / 4;
                    this.roundTripTime += diff / 8;
                }
                else
                {
                    var diff = this.roundTripTime - roundTripTime;
                    roundTripTimeVariance += diff / 4;
                    this.roundTripTime -= diff / 8;
                }
            }
            else
            {
                this.roundTripTime = roundTripTime;
                roundTripTimeVariance = (roundTripTime + 1) / 2;
            }

            if (this.roundTripTime < lowestRoundTripTime)
                lowestRoundTripTime = this.roundTripTime;

            if (roundTripTimeVariance > highestRoundTripTimeVariance)
                highestRoundTripTimeVariance = roundTripTimeVariance;

            if (packetThrottleEpoch == 0 ||
                Host.m_ServiceTime - packetThrottleEpoch >= packetThrottleInterval)
            {
                lastRoundTripTime = lowestRoundTripTime;
                lastRoundTripTimeVariance = Math.Max(highestRoundTripTimeVariance, 1);
                lowestRoundTripTime = this.roundTripTime;
                highestRoundTripTimeVariance = roundTripTimeVariance;
                packetThrottleEpoch = Host.m_ServiceTime;
            }

            lastReceiveTime = Math.Max(Host.m_ServiceTime, 1);
            earliestTimeout = 0;

            uint receivedReliableSequenceNumber = acknowledge.ReceivedReliableSequenceNumber;

            var commandNumber = RemoveSentReliableCommand(receivedReliableSequenceNumber, acknowledge.Header.ChannelID);

            switch (m_State)
            {
                case ENetPeerState.AcknowledgingConnect:
                    if (commandNumber != ENET_PROTOCOL_COMMAND_VERIFY_CONNECT)
                        return; // return -1;

                    NotifyConnect();
                    break;

                case ENetPeerState.Disconnecting:
                    if (commandNumber != ENET_PROTOCOL_COMMAND_DISCONNECT)
                        return; // return -1;

                    NotifyDisconnect();
                    break;

                case ENetPeerState.DisconnectLater:
                    if (m_OutgoingCommands.IsEmpty && m_SentReliableCommands.IsEmpty)
                        Disconnect(eventData);
                    break;

                default:
                    break;
            }

            return; // return 0;
        }

        internal void HandleSendReliableCommand(in ENetProtocol command, ref ENetPacketReader dataReader)
        {
            if (command.Header.ChannelID >= ChannelCount ||
                (m_State != ENetPeerState.Connected && m_State != ENetPeerState.DisconnectLater))
                return;

            var dataLength = command.SendReliable.DataLength;
            if (command.SendReliable.DataLength > Host.maximumPacketSize)
                return;

            var data = dataReader.ReadSpan(dataLength);
            QueueIncomingCommand(in command, data, data.Length, ENetPacketFlags.Reliable, fragmentCount: 0);
        }

        internal void HandleSendUnreliableCommand(in ENetProtocol command, ref ENetPacketReader dataReader)
        {
            if (command.Header.ChannelID >= ChannelCount ||
                (m_State != ENetPeerState.Connected && m_State != ENetPeerState.DisconnectLater))
                return;

            var dataLength = command.SendUnreliable.DataLength;
            if (command.SendUnreliable.DataLength > Host.maximumPacketSize)
                return;

            var data = dataReader.ReadSpan(dataLength);
            QueueIncomingCommand(in command, data, data.Length, ENetPacketFlags.None, fragmentCount: 0);
        }

        internal void HandleSendUnsequencedCommand(in ENetProtocol command, ref ENetPacketReader dataReader)
        {
            if (command.Header.ChannelID >= ChannelCount ||
                (m_State != ENetPeerState.Connected && m_State != ENetPeerState.DisconnectLater))
                return;

            var dataLength = command.SendUnsequenced.DataLength;
            if (command.SendUnsequenced.DataLength > Host.maximumPacketSize)
                return;

            var data = dataReader.ReadSpan(dataLength);

            uint unsequencedGroup = command.SendUnsequenced.UnsequencedGroup;
            var index = unsequencedGroup % ENET_PEER_UNSEQUENCED_WINDOW_SIZE;

            if (unsequencedGroup < this.incomingUnsequencedGroup)
                unsequencedGroup += 0x10000;

            if (unsequencedGroup >= this.incomingUnsequencedGroup + ENET_PEER_FREE_UNSEQUENCED_WINDOWS * ENET_PEER_UNSEQUENCED_WINDOW_SIZE)
                return;

            unsequencedGroup &= 0xFFFF;

            if (unsequencedGroup - index != this.incomingUnsequencedGroup)
            {
                this.incomingUnsequencedGroup = unchecked((ushort)(unsequencedGroup - index));

                unsequencedWindow.AsSpan().Fill(0);
            }
            else
            if ((this.unsequencedWindow[index / 32] & (1 << unchecked((int)(index % 32)))) != 0)
                return;

            if (QueueIncomingCommand(in command, data, data.Length, ENetPacketFlags.Unsequenced, fragmentCount: 0) == null)
                return;

            this.unsequencedWindow[index / 32] |= unchecked((uint)(1 << (int)(index % 32)));
        }

        internal void HandleSendFragmentCommand(in ENetProtocol command, ref ENetPacketReader dataReader)
        {
            if (command.Header.ChannelID >= this.ChannelCount ||
                (this.m_State != ENetPeerState.Connected && this.m_State != ENetPeerState.DisconnectLater))
                return;

            var fragmentLength = command.SendFragment.DataLength;
            if (fragmentLength > Host.maximumPacketSize ||
                dataReader.Left.Length < fragmentLength)
                return;

            var channel = GetOrAddChannelById(command.Header.ChannelID);
            var startSequenceNumber = command.SendFragment.StartSequenceNumber;
            var startWindow = startSequenceNumber / ENET_PEER_RELIABLE_WINDOW_SIZE;
            var currentWindow = channel.incomingReliableSequenceNumber / ENET_PEER_RELIABLE_WINDOW_SIZE;

            if (startSequenceNumber < channel.incomingReliableSequenceNumber)
                startWindow += ENET_PEER_RELIABLE_WINDOWS;

            if (startWindow < currentWindow || startWindow >= currentWindow + ENET_PEER_FREE_RELIABLE_WINDOWS - 1)
                return;

            var fragmentNumber = command.SendFragment.FragmentNumber;
            var fragmentCount = command.SendFragment.FragmentCount;
            var fragmentOffset = command.SendFragment.FragmentOffset;
            var totalLength = command.SendFragment.TotalLength;

            if (fragmentCount > ENET_PROTOCOL_MAXIMUM_FRAGMENT_COUNT ||
                fragmentNumber >= fragmentCount ||
                totalLength > Host.maximumPacketSize ||
                fragmentOffset >= totalLength ||
                fragmentLength > totalLength - fragmentOffset)
                return;

            var startCommand = default(ENetIncomingCommand);
            var currentCommandNode = default(PooledLinkedList<ENetIncomingCommand>.Node);
            while (channel.incomingReliableCommands.IterateBackward(ref currentCommandNode))
            {
                var incomingCommand = currentCommandNode!.Value;

                if (startSequenceNumber >= channel.incomingReliableSequenceNumber)
                {
                    if (incomingCommand.reliableSequenceNumber < channel.incomingReliableSequenceNumber)
                        continue;
                }
                else
                if (incomingCommand.reliableSequenceNumber >= channel.incomingReliableSequenceNumber)
                    break;

                if (incomingCommand.reliableSequenceNumber <= startSequenceNumber)
                {
                    if (incomingCommand.reliableSequenceNumber < startSequenceNumber)
                        break;

                    if ((incomingCommand.command.Header.Command & ENET_PROTOCOL_COMMAND_MASK) != ENET_PROTOCOL_COMMAND_SEND_FRAGMENT ||
                        totalLength != incomingCommand.packet!.DataLength ||
                        fragmentCount != incomingCommand.fragments.Count)
                        return;

                    startCommand = incomingCommand;
                    break;
                }
            }

            if (startCommand == null)
            {
                ENetProtocol hostCommand = command;

                hostCommand.Header.ReliableSequenceNumber = startSequenceNumber;

                startCommand = QueueIncomingCommand(in hostCommand,
                                                    data: ReadOnlySpan<byte>.Empty,
                                                    dataLength: (int)totalLength,
                                                    flags: ENetPacketFlags.Reliable,
                                                    fragmentCount: fragmentCount);

                if (startCommand == null)
                    return;
            }

            unchecked
            {
                if ((startCommand.fragments[(int)(fragmentNumber / 32)] & ((1 << ((int)fragmentNumber % 32)))) == 0)
                {
                    --startCommand.fragmentsRemaining;

                    startCommand.fragments[(int)(fragmentNumber / 32)] |= (uint)(1 << ((int)(fragmentNumber % 32)));

                    if (fragmentOffset + fragmentLength > startCommand.packet!.DataLength)
                        fragmentLength = (ushort)(startCommand.packet.DataLength - fragmentOffset);

                    var data = dataReader.ReadSpan(fragmentLength);
                    data.CopyTo(startCommand.packet.Data.Span[(int)fragmentOffset..]);

                    if (startCommand.fragmentsRemaining <= 0)
                        DispatchIncomingReliableCommands(channel, queuedCommand: null);
                }
            }
        }

        internal void HandleSendUnreliableFragmentCommand(in ENetProtocol command, ref ENetPacketReader dataReader)
        {
            if (command.Header.ChannelID >= this.ChannelCount ||
                (this.m_State != ENetPeerState.Connected && this.m_State != ENetPeerState.DisconnectLater))
                return;

            var fragmentLength = (command.SendFragment.DataLength);
            if (fragmentLength > Host.maximumPacketSize ||
                dataReader.Left.Length < fragmentLength)
                return;

            var channel = GetOrAddChannelById(command.Header.ChannelID);
            var reliableSequenceNumber = command.Header.ReliableSequenceNumber;
            var startSequenceNumber = command.SendFragment.StartSequenceNumber;

            var reliableWindow = reliableSequenceNumber / ENET_PEER_RELIABLE_WINDOW_SIZE;
            var currentWindow = channel.incomingReliableSequenceNumber / ENET_PEER_RELIABLE_WINDOW_SIZE;

            if (reliableSequenceNumber < channel.incomingReliableSequenceNumber)
                reliableWindow += ENET_PEER_RELIABLE_WINDOWS;

            if (reliableWindow < currentWindow || reliableWindow >= currentWindow + ENET_PEER_FREE_RELIABLE_WINDOWS - 1)
                return;

            if (reliableSequenceNumber == channel.incomingReliableSequenceNumber &&
                startSequenceNumber <= channel.incomingUnreliableSequenceNumber)
                return;

            var fragmentNumber = (command.SendFragment.FragmentNumber);
            var fragmentCount = (command.SendFragment.FragmentCount);
            var fragmentOffset = (command.SendFragment.FragmentOffset);
            var totalLength = (command.SendFragment.TotalLength);

            if (fragmentCount > ENET_PROTOCOL_MAXIMUM_FRAGMENT_COUNT ||
                fragmentNumber >= fragmentCount ||
                totalLength > Host.maximumPacketSize ||
                fragmentOffset >= totalLength ||
                fragmentLength > totalLength - fragmentOffset)
                return;

            var startCommand = default(ENetIncomingCommand);
            var currentCommandNode = default(PooledLinkedList<ENetIncomingCommand>.Node);
            while (channel.incomingReliableCommands.IterateBackward(ref currentCommandNode))
            {
                var incomingCommand = currentCommandNode!.Value;

                if (reliableSequenceNumber >= channel.incomingReliableSequenceNumber)
                {
                    if (incomingCommand.reliableSequenceNumber < channel.incomingReliableSequenceNumber)
                        continue;
                }
                else
                if (incomingCommand.reliableSequenceNumber >= channel.incomingReliableSequenceNumber)
                    break;

                if (incomingCommand.reliableSequenceNumber < reliableSequenceNumber)
                    break;

                if (incomingCommand.reliableSequenceNumber > reliableSequenceNumber)
                    continue;

                if (incomingCommand.unreliableSequenceNumber <= startSequenceNumber)
                {
                    if (incomingCommand.unreliableSequenceNumber < startSequenceNumber)
                        break;

                    if ((incomingCommand.command.Header.Command & ENET_PROTOCOL_COMMAND_MASK) != ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE_FRAGMENT ||
                        totalLength != incomingCommand.packet!.DataLength ||
                        fragmentCount != incomingCommand.fragments.Count)
                        return;

                    startCommand = incomingCommand;
                    break;
                }
            }

            if (startCommand == null)
            {
                startCommand = QueueIncomingCommand(in command,
                                                    data: ReadOnlySpan<byte>.Empty,
                                                    dataLength: (int)totalLength,
                                                    flags: ENetPacketFlags.UnreliableFragments,
                                                    fragmentCount: fragmentCount);

                if (startCommand == null)
                    return;
            }

            unchecked
            {
                if ((startCommand.fragments[(int)(fragmentNumber / 32)] & (1 << (int)(fragmentNumber % 32))) == 0)
                {
                    --startCommand.fragmentsRemaining;

                    startCommand.fragments[(int)(fragmentNumber / 32)] |= (uint)(1 << (int)(fragmentNumber % 32));

                    if (fragmentOffset + fragmentLength > startCommand.packet!.DataLength)
                        fragmentLength = (ushort)(startCommand.packet.DataLength - fragmentOffset);

                    var data = dataReader.ReadSpan(fragmentLength);
                    data.CopyTo(startCommand.packet.Data.Span[(int)fragmentOffset..]);

                    if (startCommand.fragmentsRemaining <= 0)
                        DispatchIncomingUnreliableCommands(channel, queuedCommand: null);
                }
            }

            return;
        }
    }
}