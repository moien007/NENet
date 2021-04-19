using System;

namespace ENetDotNet.Checksums
{
    public abstract class ENetChecksum
    {
        public abstract void Begin();
        public abstract void Sum(ReadOnlySpan<byte> buffer);
        public abstract uint End();
        public virtual void Reset() { }
    }
}