using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ENetDotNet.Internal
{
    internal sealed class PooledLinkedList<T> : IReadOnlyCollection<T>
    {
        public sealed class Node
        {
            public PooledLinkedList<T> List { get; }
            public Node? Prev { get; set; }
            public Node? Next { get; set; }
            public T Value { get; set; }

            public Node(PooledLinkedList<T> list)
            {
                List = list;
                Value = default!;
            }

            public void Remove()
            {
                if (List.m_First == this)
                {
                    List.m_First = Next;
                }

                if (List.m_Last == this)
                {
                    List.m_Last = Prev;
                }

                if (Prev != null)
                {
                    Prev.Next = Next;
                }

                if (Next != null)
                {
                    Next.Prev = Prev;
                }

                Next = Prev = null;
                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                {
                    Value = default!;
                }

                unchecked { List.m_Version++; }
                List.Count--;
                List.m_NodePool.Return(this);
            }
        }

        private readonly ObjectPool<Node> m_NodePool;
        private Node? m_First, m_Last;
        private uint m_Version;

        public int Count { get; private set; }
        public bool IsEmpty => Count == 0;

        public Node FirstNode
        {
            get
            {
                ThrowIfEmpty();
                return m_First!;
            }
        }

        public Node LastNode
        {
            get
            {
                ThrowIfEmpty();
                return m_Last!;
            }
        }

        public PooledLinkedList()
        {
            m_NodePool = new(factory: () => new Node(list: this));
        }

        public Node AddFirst(T item)
        {
            var node = m_NodePool.Rent();
            node.Value = item;

            if (IsEmpty)
            {
                m_First = m_Last = node;
            }
            else
            {
                node.Next = m_First;
                m_First!.Prev = node;
                m_First = node;
            }

            Count++;
            unchecked { m_Version++; }

            return node;
        }

        public Node AddLast(T item)
        {
            var node = m_NodePool.Rent();
            node.Value = item;

            if (IsEmpty)
            {
                m_First = m_Last = node;
            }
            else
            {
                node.Prev = m_Last;
                m_Last!.Next = node;
                m_Last = node;
            }

            Count++;
            unchecked { m_Version++; }

            return node;
        }


        public bool IterateBackward(ref Node? currentNode)
        {
            if (currentNode == null)
                currentNode = m_Last;
            else
                currentNode = currentNode.Prev;

            return currentNode != null;
        }

        private void ThrowIfEmpty()
        {
            if (IsEmpty)
                throw new InvalidOperationException("Linked list is emptry.");
        }

        public IEnumerator<T> GetEnumerator() => new Enumerator(this, m_Version);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this, m_Version);

        public struct Enumerator : IEnumerator<T>
        {
            private readonly uint m_ListVersion;
            private readonly PooledLinkedList<T>? m_List;
            private Node? m_CurrentNode;

            public T Current
            {
                get
                {
                    if (m_CurrentNode == null)
                        throw new InvalidOperationException($"You must call {nameof(MoveNext)} first.");

                    return m_CurrentNode.Value;
                }
            }

            object IEnumerator.Current => Current!;

            public Enumerator(PooledLinkedList<T> list, uint version)
            {
                m_List = list;
                m_ListVersion = version;
                m_CurrentNode = null;
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                if (m_List is null)
                    return false;

                if (m_List.m_Version != m_ListVersion)
                    throw new InvalidOperationException("The list is modified.");

                if (m_CurrentNode == null)
                {
                    m_CurrentNode = m_List.FirstNode;
                    return true;
                }
                else if (m_CurrentNode.Next == null)
                {
                    return false;
                }
                else
                {
                    m_CurrentNode = m_CurrentNode.Next;
                    return true;
                }
            }

            public void Reset()
            {
                m_CurrentNode = null;
            }
        }
    }
}
