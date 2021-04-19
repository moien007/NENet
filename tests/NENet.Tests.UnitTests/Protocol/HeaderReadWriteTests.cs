
using ENetDotNet.Internal;
using ENetDotNet.Protocol;

using NUnit.Framework;

using static ENetDotNet.Internal.Constants;

namespace ENetDotNet.Tests.UnitTests.Protocol
{
    [TestFixture]
    public class HeaderReadWriteTests
    {
        [TestCase]
        public void BasicReadWriteTest()
        {
            var writeHeader = new ENetProtocolHeader()
            {
                Flags = ENET_PROTOCOL_HEADER_FLAG_SENT_TIME | ENET_PROTOCOL_HEADER_FLAG_COMPRESSED,
                PeerId = 0b101010_010101, // 12bit
                SentTime = ushort.MaxValue, // 2byte
                SessionId = 0b11, // 2bit
            };

            var packetWriter = new ENetPacketWriter();
            writeHeader.WriteTo(packetWriter);

            var packetReader = new ENetPacketReader(packetWriter.GetWrittenBuffer().Span);
            Assert.IsTrue(ENetProtocolHeader.TryReadFrom(ref packetReader, out var readHeader));
            Assert.AreEqual(writeHeader.Flags, readHeader.Flags);
            Assert.AreEqual(writeHeader.PeerId, readHeader.PeerId);
            Assert.AreEqual(writeHeader.SentTime, readHeader.SentTime);
            Assert.AreEqual(writeHeader.SessionId, readHeader.SessionId);
        }

        [TestCase]
        public void TryReadFrom_ReadsSentTimeWhenFlagIsSet()
        {
            const ushort sentTime = 0b1100_1100_0011_1010;

            var packetWriter = new ENetPacketWriter();
            packetWriter.WriteNetEndian((ushort)ENET_PROTOCOL_HEADER_FLAG_SENT_TIME);
            packetWriter.WriteNetEndian((ushort)sentTime);

            var packetReader = new ENetPacketReader(packetWriter.GetWrittenBuffer().Span);

            Assert.IsTrue(ENetProtocolHeader.TryReadFrom(ref packetReader, out var header));
            Assert.AreEqual(sentTime, header.SentTime);
        }

        [TestCase]
        public void TryReadFrom_DoesNotReadSentTimeWhenFlagIsNotSet()
        {
            var packetWriter = new ENetPacketWriter();
            packetWriter.WriteValue<ushort>(0);
            packetWriter.WriteValue<ushort>(0);

            var packetReader = new ENetPacketReader(packetWriter.GetWrittenBuffer().Span);

            Assert.IsTrue(ENetProtocolHeader.TryReadFrom(ref packetReader, out var header));
            Assert.AreEqual(2, packetReader.Left.Length);
        }

        [TestCase]
        public void TryReadFrom_ReturnsFalseWhenBufferIsSmall()
        {
            var packetReader = new ENetPacketReader(new byte[1]);

            Assert.IsFalse(ENetProtocolHeader.TryReadFrom(ref packetReader, out _));
        }

        [TestCase]
        public void TryReadFrom_ReturnsFalseWhenSentTimeIsNotPresent()
        {
            var packetWriter = new ENetPacketWriter();
            packetWriter.WriteNetEndian((ushort)ENET_PROTOCOL_HEADER_FLAG_SENT_TIME);

            var packetReader = new ENetPacketReader(packetWriter.GetWrittenBuffer().Span);

            Assert.IsFalse(ENetProtocolHeader.TryReadFrom(ref packetReader, out var _));
        }

        [TestCase]
        public void WriteTo_WritesSentTimeWhenFlagIsSet()
        {
            var writeHeader = new ENetProtocolHeader()
            {
                Flags = ENET_PROTOCOL_HEADER_FLAG_SENT_TIME,
                SentTime = 123,
            };

            var packetWriter = new ENetPacketWriter();
            writeHeader.WriteTo(packetWriter);

            var packetReader = new ENetPacketReader(packetWriter.GetWrittenBuffer().Span);
            Assert.True(ENetProtocolHeader.TryReadFrom(ref packetReader, out var readHeader));

            Assert.AreEqual(writeHeader.Flags, readHeader.Flags);
            Assert.AreEqual(writeHeader.SentTime, readHeader.SentTime);
        }

        [TestCase]
        public void WriteTo_DoesNotWriteSentWhenFlagIsNotSet()
        {
            var writeHeader = new ENetProtocolHeader()
            {
                Flags = 0,
                SentTime = 12031,
            };

            var packetWriter = new ENetPacketWriter();
            writeHeader.WriteTo(packetWriter);

            Assert.AreEqual(2, packetWriter.GetWrittenBuffer().Length);
        }
    }
}
