using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;

using ENetDotNet.Checksums;
using ENetDotNet.Compressions;
using ENetDotNet.Internal;
using ENetDotNet.Protocol;
using ENetDotNet.Sockets;

using static ENetDotNet.Internal.Constants;

namespace ENetDotNet
{
    public sealed class ENetHost
    {
        internal readonly ObjectPool<ENetAcknowledge> m_AcknowledgePool = new(factory: () => new());
        internal readonly ObjectPool<ENetOutgoingCommand> m_OutgoingCommandPool = new(factory: () => new());
        internal readonly ObjectPool<ENetIncomingCommand> m_IncomingCommandPool = new(factory: () => new());
        internal readonly ObjectPool<ENetPacketWriter> m_PacketWriterPool = new(factory: () => new());
        internal readonly ENetPeerCollection m_Peers = new();
        internal readonly ENetProtocol m_ReceiveProtocol = new();
        internal readonly Random m_Random = new();
        internal ulong m_ServiceTime;
        internal ulong bandwidthThrottleEpoch;
        internal uint incomingBandwidth, outgoingBandwidth;
        internal bool recalculateBandwidthLimits;
        internal int bandwidthLimitedPeers;
        internal uint maximumPacketSize = ENET_HOST_DEFAULT_MAXIMUM_PACKET_SIZE;
        internal byte channelLimit;
        internal uint mtu = ENET_HOST_DEFAULT_MTU;
        internal uint duplicatePeers = ENET_PROTOCOL_MAXIMUM_PEER_ID;
        internal uint maximumPollTime;
        internal bool continueSending;
        internal bool headerIncludeSentTime;
        internal uint packetSize;
        internal ulong totalSentPackets, totalSentData;
        internal readonly Queue<ENetPacketWriter> m_PacketWriterQueue = new(32);

        [ThreadStatic]
        private static List<ReadOnlyMemory<byte>>? ts_SendBuffers;

        public event ENetEventHandler? OnEvent;

        public ENetCompression? Compression { get; set; }
        public ENetChecksum? Checksum { get; set; }
        public MemoryPool<byte> MemoryPool { get; set; } = MemoryPool<byte>.Shared;
        public ENetSocket Socket { get; }
        public int PeerCount { get; }
        public object? UserData { get; set; }

        public ENetHost(ENetSocket socket, int peerCount, int channelLimit, long incomingBandwidth = 0, long outgoingBandwidth = 0) => throw new NotImplementedException();

        public void Service(TimeSpan timeout)
        {
            m_ServiceTime = Utilities.GetMillisecondsSinceEpoch();

            ulong timeoutDue;
            if (timeout == Timeout.InfiniteTimeSpan)
                timeoutDue = ulong.MaxValue;
            else
                timeoutDue = (ulong)(m_ServiceTime + timeout.TotalMilliseconds);

            do
            {
                if (Utilities.Difference(m_ServiceTime, bandwidthThrottleEpoch) >= ENET_HOST_BANDWIDTH_THROTTLE_INTERVAL)
                    BandwidthThrottle();

                SendOutgoingCommands(checkForTimeouts: true);

                ReceiveIncomingCommands(timeout: TimeSpan.Zero, maxReceives: int.MaxValue);

                SendOutgoingCommands(checkForTimeouts: true);

                if (timeout == TimeSpan.Zero || m_ServiceTime >= timeoutDue)
                    return;

                m_ServiceTime = Utilities.GetMillisecondsSinceEpoch();

                var receiveTimeout = Utilities.Difference(m_ServiceTime, timeoutDue);
                receiveTimeout = Math.Min(maximumPollTime, receiveTimeout);
                
                ReceiveIncomingCommands(timeout: TimeSpan.FromMilliseconds(receiveTimeout), maxReceives: 1);
            }
            while (true);
        }

        public ENetPeer Connect(IPEndPoint remoteEndPoint, int channelCount, uint data)
        {
            if (channelCount < ENET_PROTOCOL_MINIMUM_CHANNEL_COUNT)
                channelCount = ENET_PROTOCOL_MINIMUM_CHANNEL_COUNT;
            else if (channelCount > ENET_PROTOCOL_MAXIMUM_CHANNEL_COUNT)
                channelCount = ENET_PROTOCOL_MAXIMUM_CHANNEL_COUNT;

            var currentPeer = new ENetPeer(this, remoteEndPoint);

            currentPeer.ChannelCount = channelCount;
            currentPeer.m_State = ENetPeerState.Connecting;
            currentPeer.m_ConnectId = unchecked((uint)m_Random.Next(int.MinValue, int.MaxValue));
            currentPeer.incomingSessionId = currentPeer.outgoingSessionId = 0;

            ushort incomingPeerId = 0;
            ushort findingProperIncomingIdTries = 0;
            while (true)
            {
                if (++findingProperIncomingIdTries == ushort.MaxValue)
                    throw new InvalidOperationException($"All possible incoming peer identifiers are being used for {remoteEndPoint}.");

                incomingPeerId = unchecked((ushort)m_Random.Next(ushort.MinValue, ushort.MaxValue));

                foreach (var peer in m_Peers)
                {
                    if (peer.incomingPeerId == incomingPeerId)
                        goto tryAgain;
                }

                break;

            tryAgain: continue;
            }

            currentPeer.incomingPeerId = incomingPeerId;

            if (this.outgoingBandwidth == 0)
                currentPeer.windowSize = ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;
            else
                currentPeer.windowSize = (this.outgoingBandwidth /
                                              ENET_PEER_WINDOW_SIZE_SCALE) *
                                                ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;

            if (currentPeer.windowSize < ENET_PROTOCOL_MINIMUM_WINDOW_SIZE)
                currentPeer.windowSize = ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;
            else
            if (currentPeer.windowSize > ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE)
                currentPeer.windowSize = ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;

            ENetProtocol command = default;

            command.Header.Command = ENET_PROTOCOL_COMMAND_CONNECT | ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE;
            command.Header.ChannelID = 0xFF;
            command.Connect.OutgoingPeerID = (currentPeer.incomingPeerId);
            command.Connect.IncomingSessionID = currentPeer.incomingSessionId;
            command.Connect.OutgoingSessionID = currentPeer.outgoingSessionId;
            command.Connect.MTU = (currentPeer.mtu);
            command.Connect.WindowSize = (currentPeer.windowSize);
            command.Connect.ChannelCount = unchecked((byte)(channelCount));
            command.Connect.IncomingBandwidth = (this.incomingBandwidth);
            command.Connect.OutgoingBandwidth = (this.outgoingBandwidth);
            command.Connect.PacketThrottleInterval = (currentPeer.packetThrottleInterval);
            command.Connect.PacketThrottleAcceleration = (currentPeer.packetThrottleAcceleration);
            command.Connect.PacketThrottleDeceleration = (currentPeer.packetThrottleDeceleration);
            command.Connect.ConnectID = currentPeer.m_ConnectId;
            command.Connect.Data = (data);

            currentPeer.QueueOutgoingCommand(in command, packet: null, offset: 0, length: 0);

            return currentPeer;
        }

        public void Broadcast(ENetPacket packet, ENetPeer? except = null)
        {
            foreach (var peer in m_Peers)
            {
                if (peer == except)
                    continue;

                peer.Send(packet);
            }
        }

        public void ChannelLimit(int limit)
        {
            if (limit == 0 || limit > ENET_PROTOCOL_MAXIMUM_CHANNEL_COUNT)
                limit = ENET_PROTOCOL_MAXIMUM_CHANNEL_COUNT;
            else if (limit < ENET_PROTOCOL_MINIMUM_CHANNEL_COUNT)
                limit = ENET_PROTOCOL_MINIMUM_CHANNEL_COUNT;

            this.channelLimit = unchecked((byte)limit);
        }

        public void BandwidthLimit(long incoming, long outgoing)
        {
            this.incomingBandwidth = (uint)incoming;
            this.outgoingBandwidth = (uint)outgoing;
            this.recalculateBandwidthLimits = true;
        }

        public void Flush()
        {
            m_ServiceTime = Utilities.GetMillisecondsSinceEpoch();

            SendOutgoingCommands(checkForTimeouts: false);
        }

        internal void InvokeOnEvent(in ENetEvent @event) => OnEvent?.Invoke(this, in @event);

        internal int ReceiveIncomingCommands(TimeSpan timeout, int maxReceives)
        {
            int numReceives = 0;

            for (; numReceives < maxReceives; numReceives++)
            {
                if (Socket.Receive(suggestedBufferLength: ENET_PROTOCOL_MAXIMUM_MTU,
                                   timeout: timeout,
                                   out var receiveResult))
                {
                    HandlePacketReceiveResult(in receiveResult);
                }
                else
                {
                    break;
                }
            }

            return numReceives;
        }

        internal void HandlePacketReceiveResult(in ENetSocket.ReceiveResult receiveResult)
        {
            receiveResult.CheckValues();

            using IMemoryOwner<byte> packetMemoryOwner = receiveResult.PacketMemoryOwner;
            using IMemoryOwner<byte>? decompressedPacketMemoryOwner =
                Compression != null ? MemoryPool.Rent(ENET_PROTOCOL_MAXIMUM_MTU) : null;

            if (receiveResult.PacketLength < ENetProtocolHeader.MinSize)
                return;

            Span<byte> receivedPacket = receiveResult.PacketMemoryOwner.Memory.Span[..receiveResult.PacketLength];
            ENetPacketReader packetReader = new(receivedPacket);

            if (!ENetProtocolHeader.TryReadFrom(ref packetReader, out var header))
                return;

            var headerLength = receivedPacket.Length - packetReader.Left.Length;
            if (Checksum != null)
            {
                headerLength += sizeof(uint);
                packetReader.ReadNetUInt32();
            }

            var headerBytes = receivedPacket[..headerLength];

            if ((header.Flags & ENET_PROTOCOL_HEADER_FLAG_COMPRESSED) != 0)
            {
                if (Compression == null)
                    return;

                var decompressBuffer = decompressedPacketMemoryOwner!.Memory.Span;
                int decompressedLen = 0;

                try
                {
                    decompressedLen = Compression.Decompress(packetReader.Left, decompressBuffer);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    return;
                }

                if (decompressedLen > packetReader.Left.Length)
                    return;

                packetReader = new(decompressBuffer);
            }

            ENetPeer? peer = m_Peers.TryFindPeer(receiveResult.RemoteEndPoint, header.PeerId);

            if (Checksum != null)
            {
                if (receivedPacket.Length < headerLength + sizeof(uint))
                    return;

                unsafe
                {
                    fixed (byte* pHeader = headerBytes)
                    {
                        void* pChecksum = pHeader + headerLength - sizeof(uint);
                        var desiredChecksum = Unsafe.ReadUnaligned<uint>(pChecksum);

                        Unsafe.WriteUnaligned<uint>(pChecksum, peer != null ? peer.m_ConnectId : 0);

                        Checksum.Begin();
                        Checksum.Sum(headerBytes);
                        Checksum.Sum(packetReader.Left);
                        var actualChecksum = Checksum.End();

                        if (actualChecksum != desiredChecksum)
                            return;
                    }
                }
            }

            if (peer != null)
            {
                unchecked
                {
                    peer.m_IncomingDataTotal += (uint)receivedPacket.Length;
                }
            }

            while (true)
            {
                var commandHeaderSize = Unsafe.SizeOf<ENetProtocolCommandHeader>();
                if (packetReader.Left.Length < commandHeaderSize)
                    return;

                var commandNumber = packetReader.PeekValue<byte>();
                if ((commandNumber & ENET_PROTOCOL_COMMAND_MASK) >= ENET_PROTOCOL_COMMAND_COUNT)
                    return;

                var commandSize = ENetProtocol.CommandSize(commandNumber);
                if (packetReader.Left.Length - commandHeaderSize < commandSize)
                    return;

                if (peer == null && (commandNumber & ENET_PROTOCOL_COMMAND_MASK) != ENET_PROTOCOL_COMMAND_CONNECT)
                    return;

                try
                {
                    m_ReceiveProtocol.ReadFrom(ref packetReader, commandNumber);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    return;
                }

                if (peer == null)
                {
                    peer = new ENetPeer(this, receiveResult.RemoteEndPoint);
                }

                try
                {
                    m_ReceiveProtocol.Handle(peer, ref packetReader, commandNumber);
                }
                catch (Exception ex)
                {
                    Debug.Write(ex);
                    return;
                }

                if ((m_ReceiveProtocol.Header.Command & ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE) != 0)
                {
                    if ((header.Flags & ENET_PROTOCOL_HEADER_FLAG_SENT_TIME) == 0)
                        return;

                    switch (peer.m_State)
                    {
                        case ENetPeerState.Disconnecting:
                        case ENetPeerState.AcknowledgingConnect:
                        case ENetPeerState.Disconnected:
                        case ENetPeerState.Zombie:
                            break;

                        case ENetPeerState.AcknowledgingDisconnect:
                            if ((m_ReceiveProtocol.Header.Command & ENET_PROTOCOL_COMMAND_MASK) == ENET_PROTOCOL_COMMAND_DISCONNECT)
                                peer.QueueAcknowledgement(in m_ReceiveProtocol.Acknowledge, header.SentTime);
                            break;

                        default:
                            peer.QueueAcknowledgement(in m_ReceiveProtocol.Acknowledge, header.SentTime);
                            break;
                    }
                }
            }
        }

        internal void BandwidthThrottle()
        {
            ulong timeCurrent = Utilities.GetMillisecondsSinceEpoch();
            ulong elapsedTime = timeCurrent - this.bandwidthThrottleEpoch;
            ulong dataTotal = unchecked((uint)~0);
            ulong bandwidth = unchecked((uint)~0);
            ulong throttle = 0;
            ulong bandwidthLimit = 0;
            var peersRemaining = m_Peers.Count;
            int needsAdjustment = this.bandwidthLimitedPeers > 0 ? 1 : 0;

            if (elapsedTime < ENET_HOST_BANDWIDTH_THROTTLE_INTERVAL)
                return;

            this.bandwidthThrottleEpoch = timeCurrent;

            if (m_Peers.Count == 0)
                return;

            if (this.outgoingBandwidth != 0)
            {
                dataTotal = 0;
                bandwidth = (this.outgoingBandwidth * elapsedTime) / 1000;

                foreach (var peer in m_Peers)
                {
                    if (peer.m_State != ENetPeerState.Connected &&
                        peer.m_State != ENetPeerState.DisconnectLater)
                        continue;

                    dataTotal += peer.outgoingDataTotal;
                }
            }

            while (peersRemaining > 0 && needsAdjustment != 0)
            {
                needsAdjustment = 0;

                if (dataTotal <= bandwidth)
                    throttle = ENET_PEER_PACKET_THROTTLE_SCALE;
                else
                    throttle = (bandwidth * ENET_PEER_PACKET_THROTTLE_SCALE) / dataTotal;

                foreach (var peer in m_Peers)
                {
                    ulong peerBandwidth;

                    if ((peer.m_State != ENetPeerState.Connected && peer.m_State != ENetPeerState.DisconnectLater) ||
                        peer.incomingBandwidth == 0 ||
                        peer.outgoingBandwidthThrottleEpoch == timeCurrent)
                        continue;

                    peerBandwidth = (peer.incomingBandwidth * elapsedTime) / 1000;
                    if ((throttle * peer.outgoingDataTotal) / ENET_PEER_PACKET_THROTTLE_SCALE <= peerBandwidth)
                        continue;

                    peer.packetThrottleLimit = (peerBandwidth *
                                                    ENET_PEER_PACKET_THROTTLE_SCALE) / peer.outgoingDataTotal;

                    if (peer.packetThrottleLimit == 0)
                        peer.packetThrottleLimit = 1;

                    if (peer.packetThrottle > peer.packetThrottleLimit)
                        peer.packetThrottle = peer.packetThrottleLimit;

                    peer.outgoingBandwidthThrottleEpoch = timeCurrent;

                    peer.incomingDataTotal = 0;
                    peer.outgoingDataTotal = 0;

                    needsAdjustment = 1;
                    --peersRemaining;
                    bandwidth -= peerBandwidth;
                    dataTotal -= peerBandwidth;
                }
            }

            if (peersRemaining > 0)
            {
                if (dataTotal <= bandwidth)
                    throttle = ENET_PEER_PACKET_THROTTLE_SCALE;
                else
                    throttle = (bandwidth * ENET_PEER_PACKET_THROTTLE_SCALE) / dataTotal;

                foreach (var peer in m_Peers)
                {
                    if ((peer.m_State != ENetPeerState.Connected && peer.m_State != ENetPeerState.DisconnectLater) ||
                        peer.outgoingBandwidthThrottleEpoch == timeCurrent)
                        continue;

                    peer.packetThrottleLimit = throttle;

                    if (peer.packetThrottle > peer.packetThrottleLimit)
                        peer.packetThrottle = peer.packetThrottleLimit;

                    peer.incomingDataTotal = 0;
                    peer.outgoingDataTotal = 0;
                }
            }

            if (this.recalculateBandwidthLimits)
            {
                this.recalculateBandwidthLimits = false;

                peersRemaining = m_Peers.Count;
                bandwidth = this.incomingBandwidth;
                needsAdjustment = 1;

                if (bandwidth == 0)
                    bandwidthLimit = 0;
                else
                    while (peersRemaining > 0 && needsAdjustment != 0)
                    {
                        needsAdjustment = 0;
                        bandwidthLimit = bandwidth / unchecked((uint)peersRemaining);

                        foreach (var peer in m_Peers)
                        {
                            if ((peer.m_State != ENetPeerState.Connected && peer.m_State != ENetPeerState.DisconnectLater) ||
                                peer.incomingBandwidthThrottleEpoch == timeCurrent)
                                continue;

                            if (peer.outgoingBandwidth > 0 &&
                                peer.outgoingBandwidth >= bandwidthLimit)
                                continue;

                            peer.incomingBandwidthThrottleEpoch = timeCurrent;

                            needsAdjustment = 1;
                            --peersRemaining;
                            bandwidth -= peer.outgoingBandwidth;
                        }
                    }

                ENetProtocol command = default;

                foreach (var peer in m_Peers)
                {
                    if (peer.m_State != ENetPeerState.Connected && peer.m_State != ENetPeerState.DisconnectLater)
                        continue;

                    command.Header.Command = ENET_PROTOCOL_COMMAND_BANDWIDTH_LIMIT | ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE;
                    command.Header.ChannelID = 0xFF;
                    command.BandwidthLimit.OutgoingBandwidth = (this.outgoingBandwidth);

                    if (peer.incomingBandwidthThrottleEpoch == timeCurrent)
                        command.BandwidthLimit.IncomingBandwidth = (peer.outgoingBandwidth);
                    else
                        command.BandwidthLimit.IncomingBandwidth = (uint)(bandwidthLimit);

                    peer.QueueOutgoingCommand(in command, packet: null, offset: 0, length: 0);
                }
            }
        }

        internal unsafe void SendOutgoingCommands(bool checkForTimeouts)
        {
            do
            {
                this.continueSending = false;

                foreach (var currentPeer in m_Peers)
                {
                    if (currentPeer.m_State == ENetPeerState.Zombie ||
                        currentPeer.m_State == ENetPeerState.Zombie)
                        continue;

                    this.headerIncludeSentTime = false;
                    this.packetSize = (uint)sizeof(ENetProtocolHeader);

                    if (!currentPeer.m_AcknowledgementList.IsEmpty)
                        currentPeer.SendAcknowledgements();

                    if (checkForTimeouts &&
                        !currentPeer.m_SentReliableCommands.IsEmpty &&
                        this.m_ServiceTime >= currentPeer.nextTimeout)
                    {
                        currentPeer.CheckTimeouts();
                    }

                    if ((currentPeer.m_OutgoingCommands.IsEmpty ||
                          currentPeer.CheckOutgoingCommands()) &&
                        currentPeer.m_SentReliableCommands.IsEmpty &&
                         this.m_ServiceTime - currentPeer.lastReceiveTime >= currentPeer.pingInterval &&
                        currentPeer.mtu - this.packetSize >= sizeof(ENetProtocolPing))
                    {
                        currentPeer.Ping();
                        currentPeer.CheckOutgoingCommands();
                    }

                    if (m_PacketWriterQueue.Count == 0)
                        continue;

                    if (currentPeer.packetLossEpoch == 0)
                        currentPeer.packetLossEpoch = this.m_ServiceTime;
                    else
                    if (this.m_ServiceTime - currentPeer.packetLossEpoch >= ENET_PEER_PACKET_LOSS_INTERVAL &&
                        currentPeer.packetsSent > 0)
                    {
                        uint packetLoss = currentPeer.packetsLost * ENET_PEER_PACKET_LOSS_SCALE / currentPeer.packetsSent;

                        currentPeer.packetLossVariance = (currentPeer.packetLossVariance * 3 + (uint)Math.Abs(packetLoss - currentPeer.packetLoss)) / 4;
                        currentPeer.packetLoss = (currentPeer.packetLoss * 7 + packetLoss) / 8;

                        currentPeer.packetLossEpoch = this.m_ServiceTime;
                        currentPeer.packetsSent = 0;
                        currentPeer.packetsLost = 0;
                    }

                    ENetProtocolHeader header = default;
                    header.PeerId = currentPeer.outgoingPeerId;

                    if (headerIncludeSentTime)
                    {
                        header.Flags |= ENET_PROTOCOL_HEADER_FLAG_SENT_TIME;
                        header.SentTime = unchecked((ushort)(this.m_ServiceTime & 0xFFFF));
                    }

                    if (this.Compression != null)
                    {
                        var originalSize = (int)(this.packetSize - sizeof(ENetProtocolHeader));

                        var compressionWriter = m_PacketWriterPool.Rent();
                        compressionWriter.EnsureCapacity(originalSize);

                        int compressedSize = 0;
                        var compressionFault = true;
                        try
                        {
                            this.Compression.StartCompressor(compressionWriter.GetUnderlayingBuffer());

                            foreach (var packetWriter in m_PacketWriterQueue)
                            {
                                var packetBuffer = packetWriter.GetWrittenBuffer();
                                Compression.CompressChunk(packetBuffer.Span);
                            }

                            compressedSize = this.Compression.EndCompressor();

                            if (compressedSize <= 0)
                                throw new ENetCompressionException($"{nameof(Compression.EndCompressor)}() returned <= 0 value of {compressedSize}.");

                            compressionFault = false;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                        finally
                        {
                            this.Compression.ResetCompressor();
                        }

                        if (!compressionFault && compressedSize < originalSize)
                        {
                            while (m_PacketWriterQueue.TryDequeue(out var packetWriter))
                            {
                                packetWriter.SetOffset(0);
                                m_PacketWriterPool.Return(packetWriter);
                            }

                            compressionWriter.SetOffset(compressedSize);
                            m_PacketWriterQueue.Enqueue(compressionWriter);

                            header.Flags |= ENET_PROTOCOL_HEADER_FLAG_COMPRESSED;
                        }
                        else
                        {
                            compressionWriter.SetOffset(0);
                            m_PacketWriterPool.Return(compressionWriter);
                        }
                    }

                    if (currentPeer.outgoingPeerId < ENET_PROTOCOL_MAXIMUM_PEER_ID)
                        header.SessionId = currentPeer.outgoingSessionId;

                    var headerWriter = m_PacketWriterPool.Rent();

                    header.WriteTo(headerWriter);


                    if (this.Checksum != null)
                    {
                        var checksumOffset = headerWriter.Offset;
                        uint checksum = currentPeer.m_PeerId < ENET_PROTOCOL_MAXIMUM_PEER_ID ? currentPeer.m_ConnectId : 0;
                        headerWriter.WriteValue(checksum);

                        try
                        {
                            Checksum.Begin();
                            Checksum.Sum(headerWriter.GetWrittenBuffer().Span);

                            foreach (var packetWriter in m_PacketWriterQueue)
                            {
                                Checksum.Sum(packetWriter.GetWrittenBuffer().Span);
                            }

                            checksum = Checksum.End();
                        }
                        finally
                        {
                            Checksum.Reset();
                        }

                        headerWriter.SetOffset(checksumOffset);
                        headerWriter.WriteValue(checksum);
                    }


                    currentPeer.lastSendTime = this.m_ServiceTime;

                    if (ts_SendBuffers == null)
                    {
                        ts_SendBuffers = new(capacity: 8);
                    }

                    ts_SendBuffers.Add(headerWriter.GetWrittenBuffer());

                    while (m_PacketWriterQueue.TryDequeue(out var packetWriter))
                    {
                        ts_SendBuffers.Add(packetWriter.GetWrittenBuffer());
                        m_PacketWriterPool.Return(packetWriter);
                    }
                    m_PacketWriterPool.Return(headerWriter);

                    int sentLength;
                    try
                    {
                        sentLength = Socket.Send(ts_SendBuffers, currentPeer.RemoteEndPoint);
                    }
                    finally
                    {
                        ts_SendBuffers.Clear();
                    }

                    currentPeer.RemoveSentUnreliableCommands();

                    this.totalSentData += (uint)sentLength;
                    this.totalSentPackets++;
                }
            }
            while (this.continueSending);

        }

        public static ENetHost Create(IPEndPoint endPoint, int peerCount, int channelLimit, long incomingBandwidth = 0, long outgoingBandwidth = 0)
        {
            var socket = ENetDatagramSocket.BindUdp(endPoint);
            return new ENetHost(socket, peerCount, channelLimit, incomingBandwidth, outgoingBandwidth);
        }
    }
}
