using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ENetDotNet.Internal;

using NUnit.Framework;

namespace ENetDotNet.Tests.UnitTests.Internal
{
    [TestFixture]
    class PooledLinkedListTests
    {
        PooledLinkedList<int> PooledLinkedList;

        [SetUp]
        public void Setup()
        {
            PooledLinkedList = new();
        }

        [TestCase]
        public void IterateBackward_ReturnsFalseWhenEmpty()
        {
            PooledLinkedList<int>.Node current = null;
            Assert.False(PooledLinkedList.IterateBackward(ref current));
        }

        [TestCase]
        public void IterateBackward_BasicTest()
        {
            PooledLinkedList.AddLast(1);
            PooledLinkedList.AddLast(2);
            PooledLinkedList.AddLast(3);

            var iteratedItems = new List<int>();
            PooledLinkedList<int>.Node current = null;
            
            while (PooledLinkedList.IterateBackward(ref current))
            {
                iteratedItems.Add(current.Value);
            }

            CollectionAssert.AreEqual(new int[] { 3, 2, 1 }, iteratedItems);
        }
        
        [TestCase]
        public void Count_KeepsUpWithAdd()
        {
            Assert.AreEqual(0, PooledLinkedList.Count);
            
            PooledLinkedList.AddFirst(1);
            Assert.AreEqual(1, PooledLinkedList.Count);
            
            PooledLinkedList.AddFirst(2);
            Assert.AreEqual(2, PooledLinkedList.Count);
            
            PooledLinkedList.AddFirst(3);
            Assert.AreEqual(3, PooledLinkedList.Count);
        }

        [TestCase]
        public void Count_KeepsUpWithRemove()
        {
            var node1 = PooledLinkedList.AddFirst(1);
            var node2 = PooledLinkedList.AddFirst(2);
            var node3 = PooledLinkedList.AddFirst(3);

            Assert.AreEqual(3, PooledLinkedList.Count);
            node3.Remove();

            Assert.AreEqual(2, PooledLinkedList.Count);
            node2.Remove();

            Assert.AreEqual(1, PooledLinkedList.Count);
            node1.Remove();
            
            Assert.AreEqual(0, PooledLinkedList.Count);
        }

        [TestCase]
        public void AddLast_AppendsList()
        {
            PooledLinkedList.AddLast(1);
            CollectionAssert.AreEqual(new int[] { 1 }, PooledLinkedList);

            PooledLinkedList.AddLast(2);
            CollectionAssert.AreEqual(new int[] { 1, 2 }, PooledLinkedList);

            PooledLinkedList.AddLast(3);
            CollectionAssert.AreEqual(new int[] { 1, 2, 3 }, PooledLinkedList);
        }

        [TestCase]
        public void AddFirst_PrependsList()
        {
            PooledLinkedList.AddFirst(1);
            CollectionAssert.AreEqual(new int[] { 1 }, PooledLinkedList);

            PooledLinkedList.AddFirst(2);
            CollectionAssert.AreEqual(new int[] { 2, 1 }, PooledLinkedList);

            PooledLinkedList.AddFirst(3);
            CollectionAssert.AreEqual(new int[] { 3, 2, 1 }, PooledLinkedList);
        }

        [TestCase]
        public void AddFirstAndAddLast()
        {
            PooledLinkedList.AddFirst(2);
            PooledLinkedList.AddFirst(1);
            PooledLinkedList.AddLast(3);

            CollectionAssert.AreEqual(new int[] { 1, 2, 3 }, PooledLinkedList);
        }
    }
}
