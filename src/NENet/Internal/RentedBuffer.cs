using System;
using System.Buffers;

namespace ENetDotNet.Internal
{
    internal readonly struct RentedBuffer : IDisposable
    {
        public readonly IMemoryOwner<byte> MemoryOwner;
        public readonly int Length;

        public Memory<byte> Memory
        {
            get
            {
                if (MemoryOwner is null)
                {
                    return Memory<byte>.Empty;
                }
                else
                {
                    return MemoryOwner.Memory.Slice(0, Length);
                }
            }
        }

        public RentedBuffer(IMemoryOwner<byte> memoryOwner, int length)
        {
            MemoryOwner = memoryOwner ?? throw new ArgumentNullException(nameof(memoryOwner));

            if (length < 0 || length > memoryOwner.Memory.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            Length = length;
        }

        public void Dispose()
        {
            if (MemoryOwner is not null)
            {
                MemoryOwner.Dispose();
            }
        }
    }
}
