using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

using ENetDotNet.Common;
using ENetDotNet.Internal;

namespace ENetDotNet.Sockets
{
    public abstract class ENetSocket : DisposableBase
    {
        public readonly struct ReceiveResult
        {
            public readonly IPEndPoint RemoteEndPoint;
            public readonly IMemoryOwner<byte> PacketMemoryOwner;
            public readonly int PacketLength;

            public ReceiveResult(IPEndPoint remoteEndPoint, IMemoryOwner<byte> packetMemoryOwner, int packetLength)
            {
                RemoteEndPoint = remoteEndPoint;
                PacketMemoryOwner = packetMemoryOwner;
                PacketLength = packetLength;
            }

            internal void CheckValues()
            {
                if (RemoteEndPoint is null)
                    throw new NullReferenceException($"Bad {nameof(ReceiveResult)} ({nameof(RemoteEndPoint)} is null).");

                if (PacketMemoryOwner is null)
                    throw new NullReferenceException($"Bad {nameof(ReceiveResult)} ({nameof(PacketMemoryOwner)} is null).");

                if (PacketLength < 0 || PacketLength > PacketMemoryOwner.Memory.Length)
                    throw new NullReferenceException($"Bad {nameof(ReceiveResult)} ({nameof(PacketLength)} is out of range).");
            }
        }

        public MemoryPool<byte> MemoryPool { get; set; } = MemoryPool<byte>.Shared;

        public abstract bool Receive(int suggestedBufferLength, TimeSpan timeout, out ReceiveResult receiveResult);
        public abstract int Send(ReadOnlyMemory<byte> buffer, IPEndPoint remoteEndPoint);
        public virtual int Send(IReadOnlyList<ReadOnlyMemory<byte>> scatteredBuffers, IPEndPoint remoteEndPoint)
        {
            ThrowHelper.ThrowIfArgumentNull(scatteredBuffers, nameof(scatteredBuffers));
            ThrowHelper.ThrowIfArgumentNull(remoteEndPoint, nameof(remoteEndPoint));
            
            var totalBuffersLen = scatteredBuffers.Sum(x => x.Length);
            
            using (var gatherMemoryOwner = MemoryPool.Rent())
            {
                var gatherMemory = gatherMemoryOwner.Memory;
                var remainingGatherSpan = gatherMemory.Span;

                foreach (var bufferMemory in scatteredBuffers)
                {
                    var bufferSpan = bufferMemory.Span;
                    bufferSpan.CopyTo(remainingGatherSpan);
                    remainingGatherSpan = remainingGatherSpan[bufferSpan.Length..];
                }

                return Send(gatherMemory[..totalBuffersLen], remoteEndPoint);
            }
        }
    }
}
