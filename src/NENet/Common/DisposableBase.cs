using System;

namespace ENetDotNet.Common
{
    public abstract class DisposableBase : IDisposable
    {
        ~DisposableBase() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) { }
    }
}
