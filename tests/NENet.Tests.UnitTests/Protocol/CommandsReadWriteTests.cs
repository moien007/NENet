
using ENetDotNet.Internal;
using ENetDotNet.Protocol;

using NUnit.Framework;

namespace ENetDotNet.Tests.UnitTests.Protocol
{
    [TestFixture]
    public class CommandsReadWriteTests
    {
        const byte TestByte = byte.MaxValue - 1;
        const ushort TestUShort = ushort.MaxValue - 1;
        const uint TestUInt = uint.MaxValue - 1;
        static readonly ENetProtocolCommandHeader TestHeader = new()
        {
            ChannelID = TestByte,
            Command = TestByte,
            ReliableSequenceNumber = TestUShort,
        };


        private ENetPacketWriter binaryWriter;

        [SetUp]
        public void Setup()
        {
            binaryWriter = new();
        }

        [TearDown]
        public void Teardown()
        {
            binaryWriter.Reset();

        }

        [TestCase]
        public void AcknowledgeTest()
        {
            ENetProtocolAcknowledge write = new()
            {
                Header = TestHeader,
                ReceivedReliableSequenceNumber = TestUShort,
                ReceivedSentTime = TestUShort,
            };
            write.WriteTo(binaryWriter);

            ENetPacketReader binaryReader = new(binaryWriter.GetWrittenBuffer().Span);
            ENetProtocolAcknowledge read = default;
            read.ReadFrom(ref binaryReader);

            AssertHeadersAreEqual(in write.Header, in read.Header);
            Assert.AreEqual(write.ReceivedReliableSequenceNumber, read.ReceivedReliableSequenceNumber);
            Assert.AreEqual(write.ReceivedSentTime, read.ReceivedSentTime);
        }

        [TestCase]
        public void BandwidthLimitTest()
        {
            ENetProtocolBandwidthLimit write = new()
            {
                Header = TestHeader,
                IncomingBandwidth = TestUShort,
                OutgoingBandwidth = TestUShort,
            };
            write.WriteTo(binaryWriter);

            ENetPacketReader binaryReader = new(binaryWriter.GetWrittenBuffer().Span);
            ENetProtocolBandwidthLimit read = default;
            read.ReadFrom(ref binaryReader);

            AssertHeadersAreEqual(in write.Header, in read.Header);
            Assert.AreEqual(write.IncomingBandwidth, read.IncomingBandwidth);
            Assert.AreEqual(write.OutgoingBandwidth, read.OutgoingBandwidth);
        }

        [TestCase]
        public void ConnectTest()
        {
            ENetProtocolConnect write = new()
            {
                Header = TestHeader,
                OutgoingPeerID = TestUShort,
                IncomingSessionID = TestByte,
                OutgoingSessionID = TestByte,
                MTU = TestUInt,
                WindowSize = TestUInt,
                ChannelCount = TestUInt,
                IncomingBandwidth = TestUInt,
                OutgoingBandwidth = TestUInt,
                PacketThrottleInterval = TestUInt,
                PacketThrottleAcceleration = TestUInt,
                PacketThrottleDeceleration = TestUInt,
                ConnectID = TestUInt,
                Data = TestUInt,
            };
            write.WriteTo(binaryWriter);

            ENetPacketReader binaryReader = new(binaryWriter.GetWrittenBuffer().Span);
            ENetProtocolConnect read = default;
            read.ReadFrom(ref binaryReader);

            AssertHeadersAreEqual(in write.Header, in read.Header);
            Assert.AreEqual(write.OutgoingPeerID, read.OutgoingPeerID);
            Assert.AreEqual(write.IncomingSessionID, read.IncomingSessionID);
            Assert.AreEqual(write.OutgoingSessionID, read.OutgoingSessionID);
            Assert.AreEqual(write.MTU, read.MTU);
            Assert.AreEqual(write.WindowSize, read.WindowSize);
            Assert.AreEqual(write.ChannelCount, read.ChannelCount);
            Assert.AreEqual(write.IncomingBandwidth, read.IncomingBandwidth);
            Assert.AreEqual(write.OutgoingBandwidth, read.OutgoingBandwidth);
            Assert.AreEqual(write.PacketThrottleInterval, read.PacketThrottleInterval);
            Assert.AreEqual(write.PacketThrottleAcceleration, read.PacketThrottleAcceleration);
            Assert.AreEqual(write.PacketThrottleDeceleration, read.PacketThrottleDeceleration);
            Assert.AreEqual(write.ConnectID, read.ConnectID);
            Assert.AreEqual(write.Data, read.Data);
        }

        [TestCase]
        public void DisconnectTest()
        {
            ENetProtocolDisconnect write = new()
            {
                Header = TestHeader,
                Data = TestUInt,
            };
            write.WriteTo(binaryWriter);

            ENetPacketReader binaryReader = new(binaryWriter.GetWrittenBuffer().Span);
            ENetProtocolDisconnect read = default;
            read.ReadFrom(ref binaryReader);

            AssertHeadersAreEqual(in write.Header, in read.Header);
            Assert.AreEqual(write.Data, read.Data);
        }

        [TestCase]
        public void PingTest()
        {
            ENetProtocolPing write = new()
            {
                Header = TestHeader,
            };
            write.WriteTo(binaryWriter);

            ENetPacketReader binaryReader = new(binaryWriter.GetWrittenBuffer().Span);
            ENetProtocolPing read = default;
            read.ReadFrom(ref binaryReader);

            AssertHeadersAreEqual(in write.Header, in read.Header);
        }

        [TestCase]
        public void SendFragmentTest()
        {
            ENetProtocolSendFragment write = new()
            {
                Header = TestHeader,
                StartSequenceNumber = TestUShort,
                DataLength = TestUShort,
                FragmentCount = TestUInt,
                FragmentNumber = TestUInt,
                TotalLength = TestUInt,
                FragmentOffset = TestUInt,
            };
            write.WriteTo(binaryWriter);

            ENetPacketReader binaryReader = new(binaryWriter.GetWrittenBuffer().Span);
            ENetProtocolSendFragment read = default;
            read.ReadFrom(ref binaryReader);

            AssertHeadersAreEqual(in write.Header, in read.Header);
            Assert.AreEqual(write.StartSequenceNumber, read.StartSequenceNumber);
            Assert.AreEqual(write.DataLength, read.DataLength);
            Assert.AreEqual(write.FragmentCount, read.FragmentCount);
            Assert.AreEqual(write.FragmentNumber, read.FragmentNumber);
            Assert.AreEqual(write.TotalLength, read.TotalLength);
            Assert.AreEqual(write.FragmentOffset, read.FragmentOffset);
        }

        [TestCase]
        public void SendReliableTest()
        {
            ENetProtocolSendReliable write = new()
            {
                Header = TestHeader,
                DataLength = TestUShort,
            };
            write.WriteTo(binaryWriter);

            ENetPacketReader binaryReader = new(binaryWriter.GetWrittenBuffer().Span);
            ENetProtocolSendReliable read = default;
            read.ReadFrom(ref binaryReader);

            AssertHeadersAreEqual(in write.Header, in read.Header);
            Assert.AreEqual(write.DataLength, read.DataLength);
        }

        [TestCase]
        public void SendUnreliableTest()
        {
            ENetProtocolSendUnreliable write = new()
            {
                Header = TestHeader,
                UnreliableSequenceNumber = TestUShort,
                DataLength = TestUShort,
            };
            write.WriteTo(binaryWriter);

            ENetPacketReader binaryReader = new(binaryWriter.GetWrittenBuffer().Span);
            ENetProtocolSendUnreliable read = default;
            read.ReadFrom(ref binaryReader);

            AssertHeadersAreEqual(in write.Header, in read.Header);
            Assert.AreEqual(write.UnreliableSequenceNumber, read.UnreliableSequenceNumber);
            Assert.AreEqual(write.DataLength, read.DataLength);
        }

        [TestCase]
        public void SendUnsequencedTest()
        {
            ENetProtocolSendUnsequenced write = new()
            {
                Header = TestHeader,
                UnsequencedGroup = TestUShort,
                DataLength = TestUShort,
            };
            write.WriteTo(binaryWriter);

            ENetPacketReader binaryReader = new(binaryWriter.GetWrittenBuffer().Span);
            ENetProtocolSendUnsequenced read = default;
            read.ReadFrom(ref binaryReader);

            AssertHeadersAreEqual(in write.Header, in read.Header);
            Assert.AreEqual(write.UnsequencedGroup, read.UnsequencedGroup);
            Assert.AreEqual(write.DataLength, read.DataLength);
        }

        [TestCase]
        public void ThrottleConfigureTest()
        {
            ENetProtocolThrottleConfigure write = new()
            {
                Header = TestHeader,
                PacketThrottleInterval = TestUInt,
                PacketThrottleAcceleration = TestUInt,
                PacketThrottleDeceleration = TestUInt,
            };
            write.WriteTo(binaryWriter);

            ENetPacketReader binaryReader = new(binaryWriter.GetWrittenBuffer().Span);
            ENetProtocolThrottleConfigure read = default;
            read.ReadFrom(ref binaryReader);

            AssertHeadersAreEqual(in write.Header, in read.Header);
            Assert.AreEqual(write.PacketThrottleInterval, read.PacketThrottleInterval);
            Assert.AreEqual(write.PacketThrottleAcceleration, read.PacketThrottleAcceleration);
            Assert.AreEqual(write.PacketThrottleDeceleration, read.PacketThrottleDeceleration);
        }

        [TestCase]
        public void VerifyConnectTest()
        {
            ENetProtocolVerifyConnect write = new()
            {
                Header = TestHeader,
                OutgoingPeerID = TestUShort,
                IncomingSessionID = TestByte,
                OutgoingSessionID = TestByte,
                MTU = TestUInt,
                WindowSize = TestUInt,
                ChannelCount = TestUInt,
                IncomingBandwidth = TestUInt,
                OutgoingBandwidth = TestUInt,
                PacketThrottleInterval = TestUInt,
                PacketThrottleAcceleration = TestUInt,
                PacketThrottleDeceleration = TestUInt,
                ConnectID = TestUInt,
            };

            write.WriteTo(binaryWriter);

            ENetPacketReader binaryReader = new(binaryWriter.GetWrittenBuffer().Span);
            ENetProtocolVerifyConnect read = default;
            read.ReadFrom(ref binaryReader);

            AssertHeadersAreEqual(in write.Header, in read.Header);
            Assert.AreEqual(write.OutgoingPeerID, read.OutgoingPeerID);
            Assert.AreEqual(write.IncomingSessionID, read.IncomingSessionID);
            Assert.AreEqual(write.OutgoingSessionID, read.OutgoingSessionID);
            Assert.AreEqual(write.MTU, read.MTU);
            Assert.AreEqual(write.WindowSize, read.WindowSize);
            Assert.AreEqual(write.ChannelCount, read.ChannelCount);
            Assert.AreEqual(write.IncomingBandwidth, read.IncomingBandwidth);
            Assert.AreEqual(write.OutgoingBandwidth, read.OutgoingBandwidth);
            Assert.AreEqual(write.PacketThrottleInterval, read.PacketThrottleInterval);
            Assert.AreEqual(write.PacketThrottleAcceleration, read.PacketThrottleAcceleration);
            Assert.AreEqual(write.PacketThrottleDeceleration, read.PacketThrottleDeceleration);
            Assert.AreEqual(write.ConnectID, read.ConnectID);
        }

        static void AssertHeadersAreEqual(in ENetProtocolCommandHeader expected, in ENetProtocolCommandHeader actual)
        {
            Assert.AreEqual(expected.ChannelID, actual.ChannelID);
            Assert.AreEqual(expected.Command, actual.Command);
            Assert.AreEqual(expected.ReliableSequenceNumber, actual.ReliableSequenceNumber);
        }
    }
}
