using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ENetDotNet.Internal
{
    internal sealed class ObjectPool<T> where T : class
    {
        private readonly Stack<T> m_Pool;
        private readonly Func<T> m_Factory;

#if DEBUG
        public readonly HashSet<T> RentedObjects = new();
#endif

        public ObjectPool(Func<T> factory)
        {
            if (factory is null)
                throw new ArgumentNullException(nameof(factory));

            m_Pool = new();
            m_Factory = factory;
        }

        public RentedObject<T> SafeRent() => new(this, Rent());
        public T Rent()
        {
            T result;
            if (m_Pool.TryPop(out var item))
            {
                result = item;
            }
            else
            {
                result = m_Factory.Invoke();
            }

#if DEBUG
            RentedObjects.Add(result);
#endif

            return result;
        }

        public void Return(T item)
        {
#if DEBUG
            Trace.Assert(RentedObjects.Contains(item));
#endif
            
            m_Pool.Push(item);
        }
    }
}
