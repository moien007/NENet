using System;
using System.Net.Sockets;
using System.Threading;

namespace ENetDotNet.Internal
{
    internal sealed class WaitableSAEA : SocketAsyncEventArgs
    {
        private readonly ManualResetEventSlim m_ResetEvent;

        public bool IsSet => m_ResetEvent.IsSet;

        public WaitableSAEA()
        {
            m_ResetEvent = new(initialState: false);
        }

        public void Wait(CancellationToken cancellationToken) => m_ResetEvent.Wait(cancellationToken);
        public bool Wait(TimeSpan timeout) => m_ResetEvent.Wait(timeout);

        public void Reset()
        {
            BufferList = null;
            SetBuffer(buffer: null, offset: 0, count: 0);
            m_ResetEvent.Reset();
        }

        protected override void OnCompleted(SocketAsyncEventArgs e)
        {
            base.OnCompleted(e);

            m_ResetEvent.Set();
        }
    }
}

