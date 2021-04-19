using System;
using System.Buffers;

namespace ENetDotNet
{
    public sealed class ENetPacket : IDisposable
    {
        private readonly IMemoryOwner<byte> m_DataMemoryOwner;
        private readonly int m_DataLength;
        private uint m_RefCount;
        
        public ENetPacketFlags Flags { get; }
        public int Channel { get; }

        public bool Disposed => m_RefCount == 0;
        public Memory<byte> Data
        {
            get
            {
                ThrowIfDisposed();

                return m_DataMemoryOwner.Memory[..m_DataLength];
            }
        }

        public int DataLength => m_DataLength;

        public ENetPacket(IMemoryOwner<byte> dataMemoryOwner, int dataLength, ENetPacketFlags flags, int channel)
        {
            m_DataMemoryOwner = dataMemoryOwner ?? throw new ArgumentNullException(nameof(dataMemoryOwner));
            m_DataLength = dataLength;
            Flags = flags;
            Channel = channel;
            m_RefCount = 1;
        }

        internal void IncrementRefCount()
        {
            ThrowIfDisposed();
            
            m_RefCount++;
        }
        
        public void Dispose()
        {
            if (m_RefCount == 0 || --m_RefCount > 0)
                return;

            m_DataMemoryOwner.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (Disposed)
                throw new ObjectDisposedException(nameof(ENetPacket));
        }
    }
}
