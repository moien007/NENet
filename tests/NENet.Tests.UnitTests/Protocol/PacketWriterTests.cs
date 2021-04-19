using System;
using System.Linq;
using System.Text;

using ENetDotNet.Protocol;

using NUnit.Framework;

namespace ENetDotNet.Tests.UnitTests
{
    [TestFixture]
    public class PacketWriterTests
    {
        private ENetPacketWriter binaryWriter;

        [SetUp]
        public void Setup()
        {
            binaryWriter = new();
        }

        [TestCase]
        public void WriteBytesTest()
        {
            var bytes = Encoding.UTF8.GetBytes("Lazy cat jumps over sleepy dog.");

            binaryWriter.WriteBytes(bytes);
            var writtenBytes = binaryWriter.GetWrittenBuffer().ToArray();
            Assert.IsTrue(writtenBytes.SequenceEqual(bytes));
        }

        [TestCase]
        public void WriteIntValueTest()
        {
            const int testValue = 2020;

            Assert.AreEqual(0, binaryWriter.Written);
            Assert.AreEqual(0, binaryWriter.GetWrittenBuffer().Length);

            binaryWriter.WriteValue(testValue);
            Assert.AreEqual(sizeof(int), binaryWriter.Written);
            Assert.AreEqual(sizeof(int), binaryWriter.GetWrittenBuffer().Length);
            Assert.IsTrue(BitConverter.GetBytes(testValue).AsSpan().SequenceEqual(binaryWriter.GetWrittenBuffer().Span));
        }

        [TestCase]
        public void ResetTest()
        {
            Assert.AreEqual(0, binaryWriter.Written);

            binaryWriter.WriteValue(10);
            binaryWriter.WriteValue(10);
            Assert.AreEqual(8, binaryWriter.Written);

            binaryWriter.Reset();
            Assert.AreEqual(0, binaryWriter.Written);
            Assert.IsTrue(binaryWriter.GetWrittenBuffer().IsEmpty);
        }

        [TestCase]
        public void WriteNetIntTest()
        {
            byte[] netBytes = { 0x00, 0x00, 0x00, 0x01 };
            binaryWriter.WriteNetEndian(0x00_00_00_01);
            Assert.IsTrue(binaryWriter.GetWrittenBuffer().Span.SequenceEqual(netBytes));
        } 
    }
}