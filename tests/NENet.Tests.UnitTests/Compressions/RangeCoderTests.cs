using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ENetDotNet.Compressions;

using NUnit.Framework;

namespace ENetDotNet.Tests.UnitTests.Compressions
{
    [TestFixture]
    public class RangeCoderTests
    {
        public ENetRangeCoder rangeCoder;

        [SetUp]
        public void Setup()
        {
            rangeCoder = new();
        }

        [TearDown]
        public void Teardown()
        {
            rangeCoder.Dispose();
        }

        [TestCase("Hello World!", "491EB21F3D7045CFFC766F3FB493")]
        public void CompressDecompressTest(string uncompressedText, string compressedHex)
        {
            var uncompressed = Encoding.UTF8.GetBytes(uncompressedText);
            var compressed = new byte[uncompressed.Length * 4];

            rangeCoder.StartCompressor(compressed);
            rangeCoder.CompressChunk(uncompressed);
            var compressedLen = rangeCoder.EndCompressor();
            rangeCoder.ResetCompressor();

            Assert.AreEqual(14, compressedLen);

            var decompressed = new byte[uncompressed.Length * 4];
            var decompressedLen = rangeCoder.Decompress(compressed[..compressedLen], decompressed);

            Assert.AreEqual(uncompressed.Length, decompressedLen);

            var decompressedText = Encoding.UTF8.GetString(decompressed, 0, decompressedLen);

            Assert.AreEqual(uncompressedText, decompressedText);

            Assert.AreEqual(compressedHex, hexify(compressed.Take(compressedLen)));

            static string hexify(IEnumerable<byte> bytesEnum) => BitConverter.ToString(bytesEnum.ToArray()).Replace("-", "");
        }
    }
}