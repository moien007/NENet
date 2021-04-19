using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace ENetDotNet
{
    internal sealed class ENetPeerCollection : IEnumerable<ENetPeer>
    {
        private ENetPeer?[] m_Peers;
        private int[] m_Hashes;
        private int m_Count, m_Capacity;

        public int Count => m_Count;

        public ENetPeerCollection()
        {
            m_Peers = Array.Empty<ENetPeer?>();
            m_Hashes = Array.Empty<int>();
            m_Count = m_Capacity = 0;
        }

        public void AddPeer(ENetPeer peer)
        {
            if (m_Count == m_Capacity)
            {
                var newCapacity = Math.Max(16, m_Capacity * 2);
                Array.Resize(ref m_Peers, newCapacity);
                Array.Resize(ref m_Hashes, newCapacity);
                m_Capacity = newCapacity;
            }

            var hash = HashCode.Combine(peer.RemoteEndPoint, peer.m_PeerId);
            var index = m_Count++;
            m_Peers[index] = peer;
            m_Hashes[index] = hash;
        }

        public bool TryRemovePeer(ENetPeer peer)
        {
            var index = -1;
            for (int i = 0; i < m_Count; i++)
            {
                if (m_Peers[i] == peer)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
                return false;

            var lastIndex = Math.Max(0, m_Count - 1);
            if (index == lastIndex)
            {
                m_Peers[index] = null;
            }
            else
            {
                m_Hashes[index] = m_Hashes[lastIndex];
                m_Peers[index] = m_Peers[lastIndex];
                m_Peers[lastIndex] = null;
            }

            m_Count--;
            return true;
        }

        public ENetPeer? TryFindPeer(IPEndPoint endPoint, ushort id)
        {
            var hash = HashCode.Combine(endPoint, id);

            for (var i = 0; i < m_Count; i++)
            {
                if (m_Hashes[i] == hash)
                {
                    var peer = m_Peers[i];
                    Debug.Assert(peer != null, "Peer is not expected to be null.");
                    if (peer!.m_PeerId == id && peer.RemoteEndPoint.Equals(endPoint))
                        return peer;
                }
            }

            return null;
        }

        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        IEnumerator<ENetPeer> IEnumerable<ENetPeer>.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<ENetPeer>
        {
            public readonly ENetPeerCollection? Owner;
            public int m_Index;

            public ENetPeer Current => Owner!.m_Peers[m_Index]!;
            object IEnumerator.Current => Current;

            public Enumerator(ENetPeerCollection owner)
            {
                Owner = owner;
                m_Index = -1;
            }

            public bool MoveNext() => ++m_Index < Owner!.m_Count;
            public void Reset() => throw new NotSupportedException();
            public void Dispose() { }
        }
    }
}