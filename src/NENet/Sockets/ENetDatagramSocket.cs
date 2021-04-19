using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

using ENetDotNet.Internal;

namespace ENetDotNet.Sockets
{
    public class ENetDatagramSocket : ENetSocket, IENetSystemSocket
    {
        private static readonly EndPoint s_AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        public static ENetDatagramSocket BindUdp(IPEndPoint bindEndPoint)
        {
            if (bindEndPoint == null)
                throw new ArgumentNullException(nameof(bindEndPoint));

            Socket socket = new(bindEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            
            var fault = true;
            try
            {
                socket.Bind(bindEndPoint);
                fault = false;
            }
            finally
            {
                if (fault)
                {
                    socket.Dispose();
                }
            }

            return new ENetDatagramSocket(socket);
        }

        private readonly List<ArraySegment<byte>> m_SendSegments;
        private readonly WaitableSAEA m_RecvSAEA, m_SendSAEA;
        private IMemoryOwner<byte>? m_RecvMemoryOwner, m_SendMemoryOwner;

        public Socket Socket { get; }
        public IPEndPoint EndPoint { get; }

        public ENetDatagramSocket(Socket socket)
        {
            Socket = socket ?? throw new ArgumentNullException(nameof(socket));

            if (socket.SocketType != SocketType.Dgram)
                throw new ArgumentException($"Socket must be of type {SocketType.Dgram}.");

            if (socket.IsBound == false)
                throw new ArgumentException($"Socket must be bound.");

            EndPoint = socket.LocalEndPoint as IPEndPoint ??
                        throw new InvalidOperationException("Socket must be bound to an IP.");

            m_RecvSAEA = new();
            m_SendSAEA = new();
            m_SendSegments = new();
        }

        public override bool Receive(int suggestedBufferLength, TimeSpan timeout, out ReceiveResult receiveResult)
        {
            if (m_RecvMemoryOwner == null)
            {
                m_RecvMemoryOwner = MemoryPool.Rent(suggestedBufferLength);
                m_RecvSAEA.SetBuffer(m_RecvMemoryOwner.Memory);
                m_RecvSAEA.RemoteEndPoint = s_AnyEndPoint;

                var pending = false;
                try
                {
                    pending = Socket.ReceiveFromAsync(m_RecvSAEA);
                }
                catch (SocketException ex)
                {
                    WrapAndThrowSocketException(ex);
                }

                if (pending && !m_RecvSAEA.Wait(timeout))
                {
                    receiveResult = default;
                    return false;
                }
            }
            else
            {
                if (!m_RecvSAEA.Wait(timeout) == false)
                {
                    receiveResult = default;
                    return false;
                }
            }

            try
            {
                CheckSocketError(m_RecvSAEA.SocketError);
                var remoteEndPoint = m_RecvSAEA.RemoteEndPoint as IPEndPoint ?? throw new InvalidOperationException();
                receiveResult = new(remoteEndPoint,
                                    packetMemoryOwner: m_RecvMemoryOwner,
                                    packetLength: m_RecvSAEA.BytesTransferred);

                return true;
            }
            finally
            {
                m_RecvMemoryOwner = null;
                m_RecvSAEA.Reset();
            }
        }

        public override int Send(IReadOnlyList<ReadOnlyMemory<byte>> scatteredBuffers, IPEndPoint remoteEndPoint)
        {
            if (SendSAEA_TryPrepareByBufferList(scatteredBuffers, remoteEndPoint))
            {
                return SendSAEA_DoSend();
            }
            else
            {
                return base.Send(scatteredBuffers, remoteEndPoint);
            }
        }

        public override int Send(ReadOnlyMemory<byte> buffer, IPEndPoint remoteEndPoint)
        {
            m_SendSAEA.SetBuffer(MemoryMarshal.AsMemory(buffer));
            m_SendSAEA.RemoteEndPoint = remoteEndPoint;

            return SendSAEA_DoSend();
        }

        private int SendSAEA_DoSend()
        {
            try
            {
                var pending = false;
                try
                {
                    pending = Socket.SendToAsync(m_SendSAEA);
                }
                catch (SocketException ex)
                {
                    WrapAndThrowSocketException(ex);
                }

                if (pending)
                {
                    m_SendSAEA.Wait(cancellationToken: default);
                }

                CheckSocketError(m_SendSAEA.SocketError);
                return m_SendSAEA.BytesTransferred;
            }
            finally
            {
                m_SendMemoryOwner?.Dispose();
                m_SendMemoryOwner = null;
                m_SendSAEA.Reset();
                m_SendSegments.Clear();
            }
        }

        private bool SendSAEA_TryPrepareByBufferList(IReadOnlyList<ReadOnlyMemory<byte>> buffers, IPEndPoint remoteEndPoint)
        {
            // use 'for' loop to avoid allocating the enumerator
            for (int i = 0; i < buffers.Count; i++)
            {
                if (MemoryMarshal.TryGetArray(buffers[i], out var segment))
                {
                    m_SendSegments.Add(segment);
                }
                else
                {
                    m_SendSegments.Clear();
                    return false;
                }
            }

            m_SendSAEA.BufferList = m_SendSegments;
            m_SendSAEA.RemoteEndPoint = remoteEndPoint;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Socket.Dispose();
                m_SendSAEA.Dispose();
                m_RecvSAEA.Dispose();
            }
        }

        private static void CheckSocketError(SocketError error)
        {
            if (error != SocketError.Success)
            {
                ThrowSocketError(error);
            }
        }

        [DoesNotReturn]
        private static void ThrowSocketError(SocketError error)
        {
            throw new ENetSocketException($"Got socket error {error}")
            {
                SocketError = error
            };
        }

        [DoesNotReturn]
        private static void WrapAndThrowSocketException(SocketException ex)
        {
            throw new ENetSocketException("Socket thrown exception.", ex);
        }
    }
}
