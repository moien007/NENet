// 
// Following codes are ported from https://github.com/lsalzman/enet/blob/master/compress.c
//

using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace ENetDotNet.Compressions
{
    public unsafe sealed class ENetRangeCoder : ENetCompression
    {
        private readonly Symbol* m_Symbols;
        private readonly Context* m_CompressContext;
        private Memory<byte> m_CompressOutput;
        private MemoryHandle m_CompressOutputHandle;

        public ENetRangeCoder()
        {
            m_Symbols = (Symbol*)Marshal.AllocHGlobal(sizeof(Symbol) * 4096);
            m_CompressContext = (Context*)Marshal.AllocHGlobal(sizeof(Context));
        }

        public override void ResetCompressor()
        {
            base.ResetCompressor();

            if (m_CompressOutput.IsEmpty == false)
            {
                *m_CompressContext = default;
                m_CompressOutput = default;
                m_CompressOutputHandle.Dispose();
            }
        }

        public override unsafe void StartCompressor(Memory<byte> output)
        {
            if (output.IsEmpty)
                return;

            m_CompressOutput = output;
            m_CompressOutputHandle = output.Pin();

            var outData = (byte*)m_CompressOutputHandle.Pointer;
            var outLimit = output.Length;

            *m_CompressContext = new()
            {
                symbols = m_Symbols,
                outStart = outData,
                outEnd = &outData[outLimit],
                outData = outData,
                range = unchecked((uint)~0),
            };

            m_CompressContext->Create(out m_CompressContext->root, ENET_CONTEXT_ESCAPE_MINIMUM, ENET_CONTEXT_SYMBOL_MINIMUM);
        }

        public override unsafe void CompressChunk(ReadOnlySpan<byte> chunk)
        {
            if (m_CompressOutput.IsEmpty)
                return;

            if (chunk.IsEmpty)
                return;

            fixed (byte* pChunk = chunk)
            {
                m_CompressContext->inData = pChunk;
                m_CompressContext->inEnd = &pChunk[chunk.Length];

                while (true)
                {
                    Symbol* subcontext, symbol = null;
                    byte value;
                    ushort under = 0, total = 0, count = 0;
                    ushort* parent = &m_CompressContext->predicted;

                    if (m_CompressContext->inData >= m_CompressContext->inEnd)
                        break;

                    value = *m_CompressContext->inData++;

                    for (subcontext = &m_CompressContext->symbols[m_CompressContext->predicted];
                         subcontext != m_CompressContext->root;
                         subcontext = &m_CompressContext->symbols[subcontext->parent])
                    {
                        m_CompressContext->Encode(subcontext, ref symbol, value, ref under, ref count, ENET_SUBCONTEXT_SYMBOL_DELTA, 0);

                        *parent = (ushort)(symbol - m_CompressContext->symbols);
                        parent = &symbol->parent;
                        total = subcontext->total;

                        if (count > 0)
                        {
                            m_CompressContext->RangeCoderEncode((ushort)(subcontext->escapes + under), count, total);
                        }
                        else
                        {
                            if (subcontext->escapes > 0 && subcontext->escapes < total)
                                m_CompressContext->RangeCoderEncode(0, subcontext->escapes, total);
                            subcontext->escapes += ENET_SUBCONTEXT_ESCAPE_DELTA;
                            subcontext->total += ENET_SUBCONTEXT_ESCAPE_DELTA;
                        }
                        subcontext->total += ENET_SUBCONTEXT_SYMBOL_DELTA;
                        if (count > 0xFF - 2 * ENET_SUBCONTEXT_SYMBOL_DELTA || subcontext->total > ENET_RANGE_CODER_BOTTOM - 0x100)
                            m_CompressContext->Rescale(subcontext, 0);
                        if (count > 0) goto nextInput;
                    }

                    m_CompressContext->Encode(m_CompressContext->root, ref symbol, value, ref under, ref count, ENET_CONTEXT_SYMBOL_DELTA, ENET_CONTEXT_SYMBOL_MINIMUM);
                    *parent = (ushort)(symbol - m_CompressContext->symbols);
                    parent = &symbol->parent;
                    total = m_CompressContext->root->total;

                    m_CompressContext->RangeCoderEncode((ushort)(m_CompressContext->root->escapes + under), count, total);
                    m_CompressContext->root->total += ENET_CONTEXT_SYMBOL_DELTA;
                    if (count > 0xFF - 2 * ENET_CONTEXT_SYMBOL_DELTA + ENET_CONTEXT_SYMBOL_MINIMUM || m_CompressContext->root->total > ENET_RANGE_CODER_BOTTOM - 0x100)
                        m_CompressContext->Rescale(m_CompressContext->root, ENET_CONTEXT_SYMBOL_MINIMUM);

                    nextInput:
                    if (m_CompressContext->order >= ENET_SUBCONTEXT_ORDER)
                        m_CompressContext->predicted = m_CompressContext->symbols[m_CompressContext->predicted].parent;
                    else
                        m_CompressContext->order++;
                    m_CompressContext->RangeCoderFreeSymbols();
                }
            }
        }

        public override int EndCompressor()
        {
            if (m_CompressOutput.IsEmpty)
                throw new Exception();

            m_CompressContext->RangeCoderFlush();
            return (int)(m_CompressContext->outData - m_CompressContext->outStart);
        }

        public override int Decompress(ReadOnlySpan<byte> input, Span<byte> output)
        {
            if (input.IsEmpty)
                return 0;

            if (output.Length < input.Length)
                throw new ArgumentException($"{nameof(output)} is smaller than {nameof(input)}.");

            uint bytesOut;

            fixed (byte* pInput = input)
            fixed (byte* pOutput = output)
            {
                unchecked
                {
                    bytesOut = RangeCoderDecompress(m_Symbols, pInput, (uint)input.Length, pOutput, (uint)output.Length);
                }
            }

            return checked((int)bytesOut);
        }

        protected override void Dispose(bool disposing)
        {
            Marshal.FreeHGlobal(new IntPtr(m_Symbols));
            Marshal.FreeHGlobal(new IntPtr(m_CompressContext));
        }

        /* Don't remove this, we may need it. */
        /*static uint RangeCoderCompress(Symbol* context, byte* inBuffer, uint inBufferLen, byte* outData, uint outLimit)
        {
            if (context == null || inBuffer == null || outData == null)
                throw new ENetCompressionException("Bad compressor arguments.");

            unchecked
            {
                Context ctx = new()
                {
                    symbols = context,
                    outStart = outData,
                    outEnd = &outData[outLimit],
                    inData = inBuffer,
                    outData = outData,
                    inEnd = &inBuffer[inBufferLen],
                    low = 0,
                    range = (uint)~0,
                    root = null,
                    predicted = 0,
                    order = 0,
                    nextSymbol = 0,
                };

                ctx.Create(out ctx.root, ENET_CONTEXT_ESCAPE_MINIMUM, ENET_CONTEXT_SYMBOL_MINIMUM);

                while (true)
                {
                    Symbol* subcontext, symbol = null;
                    byte value;
                    ushort under = 0, total = 0, count = 0;
                    ushort* parent = &ctx.predicted;
                    if (ctx.inData >= ctx.inEnd)
                        break;

                    value = *ctx.inData++;

                    for (subcontext = &ctx.symbols[ctx.predicted];
                         subcontext != ctx.root;
                         subcontext = &ctx.symbols[subcontext->parent])
                    {
                        ctx.Encode(subcontext, ref symbol, value, ref under, ref count, ENET_SUBCONTEXT_SYMBOL_DELTA, 0);

                        *parent = (ushort)(symbol - ctx.symbols);
                        parent = &symbol->parent;
                        total = subcontext->total;

                        if (count > 0)
                        {
                            ctx.RangeCoderEncode((ushort)(subcontext->escapes + under), count, total);
                        }
                        else
                        {
                            if (subcontext->escapes > 0 && subcontext->escapes < total)
                                ctx.RangeCoderEncode(0, subcontext->escapes, total);
                            subcontext->escapes += ENET_SUBCONTEXT_ESCAPE_DELTA;
                            subcontext->total += ENET_SUBCONTEXT_ESCAPE_DELTA;
                        }
                        subcontext->total += ENET_SUBCONTEXT_SYMBOL_DELTA;
                        if (count > 0xFF - 2 * ENET_SUBCONTEXT_SYMBOL_DELTA || subcontext->total > ENET_RANGE_CODER_BOTTOM - 0x100)
                            ctx.Rescale(subcontext, 0);
                        if (count > 0) goto nextInput;
                    }

                    ctx.Encode(ctx.root, ref symbol, value, ref under, ref count, ENET_CONTEXT_SYMBOL_DELTA, ENET_CONTEXT_SYMBOL_MINIMUM);
                    *parent = (ushort)(symbol - ctx.symbols);
                    parent = &symbol->parent;
                    total = ctx.root->total;

                    ctx.RangeCoderEncode((ushort)(ctx.root->escapes + under), count, total);
                    ctx.root->total += ENET_CONTEXT_SYMBOL_DELTA;
                    if (count > 0xFF - 2 * ENET_CONTEXT_SYMBOL_DELTA + ENET_CONTEXT_SYMBOL_MINIMUM || ctx.root->total > ENET_RANGE_CODER_BOTTOM - 0x100)
                        ctx.Rescale(ctx.root, ENET_CONTEXT_SYMBOL_MINIMUM);

                    nextInput:
                    if (ctx.order >= ENET_SUBCONTEXT_ORDER)
                        ctx.predicted = ctx.symbols[ctx.predicted].parent;
                    else
                        ctx.order++;
                    ctx.RangeCoderFreeSymbols();
                }

                ctx.RangeCoderFlush();

                return (uint)(ctx.outData - ctx.outStart);
            }
        }*/

        static uint RangeCoderDecompress(Symbol* context, byte* inData, uint inLimit, byte* outData, uint outLimit)
        {
            if (context == null || inLimit <= 0)
                throw new ENetCompressionException("Bad decompressor arguments.");

            unchecked
            {
                Context ctx = new()
                {
                    symbols = context,
                    outStart = outData,
                    outData = outData,
                    outEnd = &outData[outLimit],
                    inEnd = &inData[inLimit],
                    inData = inData,
                    low = 0,
                    code = 0,
                    range = (uint)~0,
                    root = null,
                    predicted = 0,
                    order = 0,
                    nextSymbol = 0,
                };

                ctx.Create(out ctx.root, ENET_CONTEXT_ESCAPE_MINIMUM, ENET_CONTEXT_SYMBOL_MINIMUM);

                ctx.RangeCoderSeed();

                while (true)
                {
                    Symbol* subcontext = null, symbol = null, patch = null;
                    byte value = 0;
                    ushort code = 0, under = 0, count = 0, bottom = 0, total = 0;
                    ushort* parent = &ctx.predicted;

                    for (subcontext = &ctx.symbols[ctx.predicted];
                         subcontext != ctx.root;
                         subcontext = &ctx.symbols[subcontext->parent])
                    {
                        if (subcontext->escapes <= 0)
                            continue;

                        total = subcontext->total;

                        if (subcontext->escapes >= total)
                            continue;
                        code = ctx.RangeCoderRead(total);
                        if (code < subcontext->escapes)
                        {
                            ctx.RangeCoderDecode(0, subcontext->escapes, total);
                            continue;
                        }
                        code -= subcontext->escapes;
                        {
                            ctx.TryDecode(subcontext, ref symbol, ref code, ref value, ref under, ref count, ENET_SUBCONTEXT_SYMBOL_DELTA, 0);
                        }
                        bottom = (ushort)(symbol - ctx.symbols);
                        ctx.RangeCoderDecode((ushort)(subcontext->escapes + under), count, total);
                        subcontext->total += ENET_SUBCONTEXT_SYMBOL_DELTA;
                        if (count > 0xFF - 2 * ENET_SUBCONTEXT_SYMBOL_DELTA || subcontext->total > ENET_RANGE_CODER_BOTTOM - 0x100)
                            ctx.Rescale(subcontext, 0);
                        goto patchContexts;
                    }

                    total = ctx.root->total;

                    code = ctx.RangeCoderRead(total);
                    if (code < ctx.root->escapes)
                    {
                        ctx.RangeCoderDecode(0, ctx.root->escapes, total);
                        break;
                    }
                    code -= ctx.root->escapes;
                    {
                        ctx.RootDecode(ctx.root, ref symbol, ref code, ref value, ref under, ref count, ENET_CONTEXT_SYMBOL_DELTA, ENET_CONTEXT_SYMBOL_MINIMUM);
                    }
                    bottom = (ushort)(symbol - ctx.symbols);
                    ctx.RangeCoderDecode((ushort)(ctx.root->escapes + under), count, total);
                    ctx.root->total += ENET_CONTEXT_SYMBOL_DELTA;
                    if (count > 0xFF - 2 * ENET_CONTEXT_SYMBOL_DELTA + ENET_CONTEXT_SYMBOL_MINIMUM || ctx.root->total > ENET_RANGE_CODER_BOTTOM - 0x100)
                        ctx.Rescale(ctx.root, ENET_CONTEXT_SYMBOL_MINIMUM);

                    patchContexts:
                    for (patch = &ctx.symbols[ctx.predicted];
                         patch != subcontext;
                         patch = &ctx.symbols[patch->parent])
                    {
                        ctx.Encode(patch, ref symbol, value, ref under, ref count, ENET_SUBCONTEXT_SYMBOL_DELTA, 0);
                        *parent = (ushort)(symbol - ctx.symbols);
                        parent = &symbol->parent;
                        if (count <= 0)
                        {
                            patch->escapes += ENET_SUBCONTEXT_ESCAPE_DELTA;
                            patch->total += ENET_SUBCONTEXT_ESCAPE_DELTA;
                        }
                        patch->total += ENET_SUBCONTEXT_SYMBOL_DELTA;
                        if (count > 0xFF - 2 * ENET_SUBCONTEXT_SYMBOL_DELTA || patch->total > ENET_RANGE_CODER_BOTTOM - 0x100)
                            ctx.Rescale(patch, 0);
                    }
                    *parent = bottom;

                    ctx.RangeCoderOutput(value);

                    if (ctx.order >= ENET_SUBCONTEXT_ORDER)
                        ctx.predicted = ctx.symbols[ctx.predicted].parent;
                    else
                        ctx.order++;

                    ctx.RangeCoderFreeSymbols();
                }

                return (uint)(ctx.outData - ctx.outStart);
            }
        }

        struct Context
        {
            public byte* outData;
            public Symbol* symbols;
            public byte* outStart, outEnd;
            public byte* inData, inEnd;
            public uint low, code, range;
            public Symbol* root;
            public ushort predicted;
            public uint order, nextSymbol;

            public void Create(out Symbol* context, ushort escapes, ushort minimum)
            {
                SymbolCreate(out context, 0, 0);
                context->escapes = escapes;
                context->total = unchecked((ushort)(escapes + 256 * minimum));
                context->symbols = 0;
            }

            public void SymbolCreate(out Symbol* symbol, byte value, byte count)
            {
                symbol = &symbols[nextSymbol++];
                symbol->value = value;
                symbol->count = count;
                symbol->under = count;
                symbol->left = 0;
                symbol->right = 0;
                symbol->symbols = 0;
                symbol->escapes = 0;
                symbol->total = 0;
                symbol->parent = 0;
            }

            public static void Rescale(Symbol* context, ushort minimum)
            {
                unchecked
                {
                    context->total = context->symbols != 0 ? Symbol.Rescale(context + context->symbols) : 0;
                    context->escapes -= (ushort)(context->escapes >> 1);
                    context->total += (ushort)(context->escapes + 256 * minimum);
                }
            }

            public void RangeCoderOutput(byte value)
            {
                if (outData >= outEnd)
                    throw new ENetCompressionException("Output limit exceeded.");

                *outData++ = value;
            }

            public void RangeCoderEncode(ushort under, ushort count, ushort total)
            {
                unchecked
                {
                    range /= total;
                    low += under * range;
                    range *= count;
                    while (true)
                    {
                        if ((low ^ low + range) >= ENET_RANGE_CODER_TOP)
                        {
                            if (range >= ENET_RANGE_CODER_BOTTOM) break;
                            range = (uint)(-low & ENET_RANGE_CODER_BOTTOM - 1);
                        }
                        RangeCoderOutput((byte)(low >> 24));
                        range <<= 8;
                        low <<= 8;
                    }
                }
            }

            public void RangeCoderFlush()
            {
                while (low != 0)
                {
                    RangeCoderOutput(unchecked((byte)(low >> 24)));
                    low <<= 8;
                }
            }

            public void RangeCoderFreeSymbols()
            {
                if (nextSymbol >= sizeof(Symbol) * 4096 / sizeof(Symbol) - ENET_SUBCONTEXT_ORDER)
                {
                    nextSymbol = 0;
                    Create(out root, ENET_CONTEXT_ESCAPE_MINIMUM, ENET_CONTEXT_SYMBOL_MINIMUM);
                    predicted = 0;
                    order = 0;
                }
            }

            public void RangeCoderSeed()
            {
                unchecked
                {
                    if (inData < inEnd) code = unchecked((uint)(code | (long)*inData++ << 24));
                    if (inData < inEnd) code = unchecked((uint)(code | (long)*inData++ << 16));
                    if (inData < inEnd) code = unchecked((uint)(code | (long)*inData++ << 8));
                    if (inData < inEnd) code = code | *inData++;
                }
            }

            public ushort RangeCoderRead(ushort total)
            {
                unchecked
                {
                    return (ushort)((code - low) / (range /= total));
                }
            }

            public void RangeCoderDecode(ushort under, ushort count, ushort total)
            {
                low += under * range;
                range *= count;
                for (; ; )
                {
                    if ((low ^ low + range) >= ENET_RANGE_CODER_TOP)
                    {
                        if (range >= ENET_RANGE_CODER_BOTTOM) break;
                        range = (ushort)(-low & ENET_RANGE_CODER_BOTTOM - 1);
                    }
                    code <<= 8;
                    if (inData < inEnd)
                        code |= *inData++;
                    range <<= 8;
                    low <<= 8;
                }
            }

            public void Rescale(Symbol* context, byte minimum)
            {
                context->total = context->symbols != 0 ? Symbol.Rescale(context + context->symbols) : 0;
                context->escapes -= (ushort)(context->escapes >> 1);
                context->total += (ushort)(context->escapes + 256 * minimum);
            }

            public void Encode(Symbol* context, ref Symbol* symbol, byte value, ref ushort under, ref ushort count, byte update, byte minimum)
            {
                unchecked
                {
                    under = (byte)(value * minimum);
                    count = minimum;
                    if (context->symbols == 0)
                    {
                        SymbolCreate(out symbol, value, update);
                        context->symbols = (ushort)(symbol - context);
                    }
                    else
                    {
                        Symbol* node = context + context->symbols;
                        while (true)
                        {
                            if (value < node->value)
                            {
                                node->under += update;
                                if (node->left != 0) { node += node->left; continue; }
                                SymbolCreate(out symbol, value, update);
                                node->left = (ushort)(symbol - node);
                            }
                            else
                            if (value > node->value)
                            {
                                under += node->under;
                                if (node->right != 0) { node += node->right; continue; }
                                SymbolCreate(out symbol, value, update);
                                node->right = (ushort)(symbol - node);
                            }
                            else
                            {
                                count += node->count;
                                under += (ushort)(node->under - node->count);
                                node->under += update;
                                node->count += update;
                                symbol = node;
                            }
                            break;
                        }
                    }
                }
            }

            public void TryDecode(Symbol* context, ref Symbol* symbol, ref ushort code, ref byte value, ref ushort under, ref ushort count, byte update, byte minimum)
            {
                unchecked
                {
                    under = 0;
                    count = minimum;
                    if (context->symbols == 0)
                    {
                        throw new ENetCompressionException();
                    }
                    else
                    {
                        Symbol* node = context + context->symbols;
                        while (true)
                        {
                            ushort after = (ushort)(under + node->under + (node->value + 1) * minimum), before = (ushort)(node->count + minimum);
                            if (code >= after)
                            {
                                under += node->under;
                                if (node->right != 0) { node += node->right; continue; }
                                throw new ENetCompressionException();
                            }
                            else
                            if (code < after - before)
                            {
                                node->under += update;
                                if (node->left != 0) { node += node->left; continue; }
                                throw new ENetCompressionException();
                            }
                            else
                            {
                                value = node->value;
                                count += node->count;
                                under = (ushort)(after - before);
                                node->under += update;
                                node->count += update;
                                symbol = node;
                            }
                            break;
                        }
                    }
                }
            }

            public void RootDecode(Symbol* context, ref Symbol* symbol_, ref ushort code, ref byte value_, ref ushort under_, ref ushort count_, byte update, byte minimum)
            {
                unchecked
                {
                    under_ = 0;
                    count_ = minimum;
                    if (context->symbols == 0)
                    {
                        {
                            value_ = (byte)(code / minimum);
                            under_ = (byte)(code - code % minimum);
                            SymbolCreate(out symbol_, value_, update);
                            context->symbols = (ushort)(symbol_ - context);
                        }
                    }
                    else
                    {
                        Symbol* node = context + context->symbols;
                        for (; ; )
                        {
                            ushort after = (ushort)(under_ + node->under + (node->value + 1) * minimum), before = (ushort)(node->count + minimum);

                            if (code >= after)
                            {
                                under_ += node->under;
                                if (node->right != 0) { node += node->right; continue; }
                                {
                                    value_ = (byte)(node->value + 1 + (code - after) / minimum);
                                    under_ = (byte)(code - (code - after) % minimum);
                                    SymbolCreate(out symbol_, value_, update);
                                    node->right = (ushort)(symbol_ - node);
                                }
                            }
                            else
                            if (code < after - before)
                            {
                                node->under += update;
                                if (node->left != 0) { node += node->left; continue; }
                                {
                                    value_ = (byte)(node->value - 1 - (after - before - code - 1) / minimum);
                                    under_ = (ushort)(code - (after - before - code - 1) % minimum);
                                    SymbolCreate(out symbol_, value_, update);
                                    node->left = (byte)(symbol_ - node);
                                }
                            }
                            else
                            {
                                value_ = node->value;
                                count_ += node->count;
                                under_ = (ushort)(after - before);
                                node->under += update;
                                node->count += update;
                                symbol_ = node;
                            }
                            break;
                        }
                    }
                }
            }
        }

        struct Symbol
        {
            public byte value;
            public byte count;
            public ushort under;
            public ushort left, right;

            public ushort symbols;
            public ushort escapes;
            public ushort total;
            public ushort parent;

            public static ushort Rescale(Symbol* symbol)
            {
                ushort total = 0;
                while (true)
                {
                    unchecked
                    {
                        symbol->count -= (byte)(symbol->count >> 1);
                        symbol->under = symbol->count;
                        if (symbol->left != 0)
                            symbol->under += Rescale(symbol + symbol->left);
                        total += symbol->under;
                        if (symbol->right == 0) break;
                        symbol += symbol->right;
                    }
                }
                return total;
            }
        }

        const int
            ENET_RANGE_CODER_TOP = 1 << 24,
            ENET_RANGE_CODER_BOTTOM = 1 << 16,
            ENET_CONTEXT_SYMBOL_DELTA = 3,
            ENET_CONTEXT_SYMBOL_MINIMUM = 1,
            ENET_CONTEXT_ESCAPE_MINIMUM = 1,
            ENET_SUBCONTEXT_ORDER = 2,
            ENET_SUBCONTEXT_SYMBOL_DELTA = 2,
            ENET_SUBCONTEXT_ESCAPE_DELTA = 5;
    }
}