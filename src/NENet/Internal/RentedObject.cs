using System;

namespace ENetDotNet.Internal
{
    internal readonly struct RentedObject<T> : IDisposable
        where T : class
    {
        private readonly ObjectPool<T>? m_Pool;
        
        public readonly T? Object;

        public RentedObject(ObjectPool<T> pool, T? @object)
        {
            m_Pool = pool;
            Object = @object;
        }

        public void Dispose()
        {
            if (m_Pool != null && Object != null)
            {
                m_Pool.Return(Object);
            }
        }
    }
}