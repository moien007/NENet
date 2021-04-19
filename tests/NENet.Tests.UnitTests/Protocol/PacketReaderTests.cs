using System;
using System.Linq;
using System.Text;

using ENetDotNet.Protocol;

using NUnit.Framework;

namespace ENetDotNet.Tests.UnitTests.Protocol
{
    [TestFixture]
    public class PacketReaderTests
    {
        [TestCase]
        public void ReadIntValueTest()
        {
            const int intValue = 0xF0F0FF;
            var intBytes = BitConverter.GetBytes(intValue);
            ENetPacketReader packetReader = new(intBytes);

            Assert.AreEqual(intBytes.Length, packetReader.Left.Length);
            Assert.AreEqual(intValue, packetReader.ReadValue<int>());
            Assert.AreEqual(0, packetReader.Left.Length);
        }

        [TestCase]
        public void ReadNetIntTest()
        {
            byte[] intBytes = { 0x00, 0x00, 0x00, 0x01 };
            ENetPacketReader packetReader = new(intBytes);
            Assert.AreEqual(1, packetReader.ReadNetInt32());
            Assert.AreEqual(0, packetReader.Left.Length);
        }

        [TestCase]
        public void ReadSpanTest()
        {
            var bytes = Encoding.UTF8.GetBytes("Lazy man jumps over fence!");
            ENetPacketReader packetReader = new(bytes);
            var readSpan = packetReader.ReadSpan(bytes.Length);
            Assert.IsTrue(bytes.AsSpan().SequenceEqual(readSpan));
            Assert.AreEqual(0, packetReader.Left.Length);
        }
    }
}