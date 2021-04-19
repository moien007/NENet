using System.Linq;
using System.Net;

using NUnit.Framework;

namespace ENetDotNet.Tests.UnitTests
{
    [TestFixture]
    public class ENetPeerCollectionTests
    {
        static readonly ENetPeer FakePeer1 = new ENetPeer(null, new IPEndPoint(IPAddress.Loopback, 1000)) { m_PeerId = 1 };
        static readonly ENetPeer FakePeer2 = new ENetPeer(null, new IPEndPoint(IPAddress.Any, 2000)) { m_PeerId = 2 };
        static readonly ENetPeer FakePeer3 = new ENetPeer(null, new IPEndPoint(IPAddress.Broadcast, 3000)) { m_PeerId = 3 };

        internal ENetPeerCollection PeerCollection;


        [SetUp]
        public void Setup()
        {
            PeerCollection = new ENetPeerCollection();
        }

        [Test]
        public void AddPeer_IncreasesCount()
        {
            Assert.AreEqual(0, PeerCollection.Count);

            PeerCollection.AddPeer(FakePeer1);
            Assert.AreEqual(1, PeerCollection.Count);

            PeerCollection.AddPeer(FakePeer2);
            Assert.AreEqual(2, PeerCollection.Count);

            PeerCollection.AddPeer(FakePeer3);
            Assert.AreEqual(3, PeerCollection.Count);
        }

        [Test]
        public void TryRemovePeer_ReturnsTrue_WhenPeerExist()
        {
            PeerCollection.AddPeer(FakePeer1);
            Assert.IsTrue(PeerCollection.TryRemovePeer(FakePeer1));
        }

        [Test]
        public void TryRemovePeer_ReturnsFalse_WhenPeerDoesNotExist()
        {
            PeerCollection.AddPeer(FakePeer1);
            Assert.IsFalse(PeerCollection.TryRemovePeer(FakePeer2));
        }

        [Test]
        public void TryRemovePeer_DecreasesCount()
        {
            PeerCollection.AddPeer(FakePeer1);
            PeerCollection.AddPeer(FakePeer2);
            PeerCollection.AddPeer(FakePeer3);

            PeerCollection.TryRemovePeer(FakePeer1);
            Assert.AreEqual(2, PeerCollection.Count);

            PeerCollection.TryRemovePeer(FakePeer2);
            Assert.AreEqual(1, PeerCollection.Count);

            PeerCollection.TryRemovePeer(FakePeer3);
            Assert.AreEqual(0, PeerCollection.Count);
        }

        [Test]
        public void Enumerator_ReturnsAddedPeers()
        {
            PeerCollection.AddPeer(FakePeer1);
            Assert.IsTrue(PeerCollection.ToHashSet().SetEquals(new[] { FakePeer1 }));

            PeerCollection.AddPeer(FakePeer2);
            Assert.IsTrue(PeerCollection.ToHashSet().SetEquals(new[] { FakePeer1, FakePeer2 }));

            PeerCollection.AddPeer(FakePeer3);
            Assert.IsTrue(PeerCollection.ToHashSet().SetEquals(new[] { FakePeer1, FakePeer2, FakePeer3 }));
        }

        [Test]
        public void Enumerator_DoesNotReturnRemovedPeers()
        {
            PeerCollection.AddPeer(FakePeer1);
            PeerCollection.AddPeer(FakePeer2);
            PeerCollection.AddPeer(FakePeer3);

            PeerCollection.TryRemovePeer(FakePeer1);
            Assert.IsTrue(PeerCollection.ToHashSet().SetEquals(new[] { FakePeer2, FakePeer3 }));

            PeerCollection.TryRemovePeer(FakePeer2);
            Assert.IsTrue(PeerCollection.ToHashSet().SetEquals(new[] { FakePeer3 }));

            PeerCollection.TryRemovePeer(FakePeer3);
            Assert.AreEqual(0, PeerCollection.Count());
        }

        [Test]
        public void TryFindPeer_ReturnsPeer_SameEP_SameID()
        {
            PeerCollection.AddPeer(FakePeer1);
            Assert.AreEqual(FakePeer1, PeerCollection.TryFindPeer(FakePeer1.RemoteEndPoint, FakePeer1.m_PeerId));
        }

        [Test]
        public void TryFindPeer_ReturnsNull_SameEP_DifferentID()
        {
            PeerCollection.AddPeer(FakePeer1);
            Assert.IsNull(PeerCollection.TryFindPeer(FakePeer1.RemoteEndPoint, FakePeer2.m_PeerId));
        }

        [Test]
        public void TryFindPeer_ReturnsNull_DifferentEP_SameID()
        {
            PeerCollection.AddPeer(FakePeer1);
            Assert.IsNull(PeerCollection.TryFindPeer(FakePeer2.RemoteEndPoint, FakePeer1.m_PeerId));
        }
    }
}
