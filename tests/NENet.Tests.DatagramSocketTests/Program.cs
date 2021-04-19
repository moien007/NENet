using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using ENetDotNet.Sockets;

namespace ENetDotNet.Tests.DatagramSocketTests
{
    class Program
    {
        static void Main(string[] args)
        {
            var socket1 = ENetDatagramSocket.BindUdp(IPEndPoint.Parse("127.0.0.1:32888"));

            Trace.Assert(socket1.EndPoint.Equals(socket1.Socket.LocalEndPoint) == true);
            
            Trace.Assert(
                socket1.Receive(suggestedBufferLength: -1, timeout: TimeSpan.Zero, receiveResult: out _) == false);
            
            var socket2 = ENetDatagramSocket.BindUdp(IPEndPoint.Parse("127.0.0.1:0"));
            var sendPayload = Encoding.UTF8.GetBytes("HelloWorld");

            socket1.Send(sendPayload, socket2.EndPoint);

            var didReceive = 
                socket2.Receive(-1, TimeSpan.FromMilliseconds(500), out var receiveResult);

            Trace.Assert(didReceive == true);
            Trace.Assert(receiveResult.PacketLength == sendPayload.Length);
            Trace.Assert(receiveResult.RemoteEndPoint.Equals(socket1.EndPoint) == true);

            var payloadsWereEqual = 
                receiveResult.PacketMemoryOwner.Memory.Slice(0, sendPayload.Length).Span.SequenceEqual(sendPayload);
            
            Trace.Assert(payloadsWereEqual == true);

            ReadOnlyMemory<byte> sendPayloadFirstHalf = sendPayload[..5];
            ReadOnlyMemory<byte> sendPayloadSecondHalf = sendPayload[5..];
            var scatteredPayload = new[] {sendPayloadFirstHalf, sendPayloadSecondHalf};
            
            socket1.Send(scatteredPayload, socket2.EndPoint);
            
            didReceive = 
                socket2.Receive(-1, TimeSpan.FromMilliseconds(500), out receiveResult);

            Trace.Assert(didReceive == true);
            Trace.Assert(receiveResult.PacketLength == sendPayload.Length);
            Trace.Assert(receiveResult.RemoteEndPoint.Equals(socket1.EndPoint) == true);
            
            socket1.Dispose();
            socket2.Dispose();
        }
    }
}