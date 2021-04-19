using System;

using ENetDotNet.Common;

namespace ENetDotNet.Compressions
{
    public abstract class ENetCompression : DisposableBase
    {
        public abstract void StartCompressor(Memory<byte> output);
        public abstract void CompressChunk(ReadOnlySpan<byte> chunk);
        public abstract int EndCompressor();
        public virtual void ResetCompressor() { }

        public abstract int Decompress(ReadOnlySpan<byte> input, Span<byte> output);
    }
}