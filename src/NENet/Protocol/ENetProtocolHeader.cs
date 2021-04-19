
using ENetDotNet.Protocol;

using static ENetDotNet.Internal.Constants;

namespace ENetDotNet.Internal
{
    internal unsafe struct ENetProtocolHeader
    {
        public const int MinSize = 2;
        public const int SizeWithSentTime = 4;

        public ushort PeerId;
        public ushort SentTime;
        public ushort Flags;
        public byte SessionId;

        public static bool TryReadFrom(ref ENetPacketReader packetReader, out ENetProtocolHeader header)
        {
            if (packetReader.Left.Length < MinSize)
            {
                header = default;
                return false;
            }

            var peerID = packetReader.ReadNetUInt16();
            var sessionID = unchecked((byte)((peerID & ENET_PROTOCOL_HEADER_SESSION_MASK) >> ENET_PROTOCOL_HEADER_SESSION_SHIFT));
            var flags = unchecked((ushort)(peerID & ENET_PROTOCOL_HEADER_FLAG_MASK));
            peerID = unchecked((ushort)(peerID & ~(ENET_PROTOCOL_HEADER_FLAG_MASK | ENET_PROTOCOL_HEADER_SESSION_MASK)));
            ushort sentTime = 0;

            if ((flags & ENET_PROTOCOL_HEADER_FLAG_SENT_TIME) != 0)
            {
                if (packetReader.Left.Length < SizeWithSentTime - sizeof(ushort))
                {
                    header = default;
                    return false;
                }

                sentTime = packetReader.ReadNetUInt16();
            }
            else
            {
                sentTime = 0;
            }

            header = new()
            {
                PeerId = peerID,
                SentTime = sentTime,
                Flags = flags,
                SessionId = sessionID,
            };

            return true;
        }

        public readonly void WriteTo(ENetPacketWriter packetWriter)
        {
            int peerId = PeerId & ~(ENET_PROTOCOL_HEADER_FLAG_MASK | ENET_PROTOCOL_HEADER_SESSION_MASK);
            peerId |= (SessionId << ENET_PROTOCOL_HEADER_SESSION_SHIFT) & ENET_PROTOCOL_HEADER_SESSION_MASK;
            peerId |= Flags & ENET_PROTOCOL_HEADER_FLAG_MASK;

            packetWriter.WriteNetEndian(unchecked((ushort)peerId));
            
            if ((Flags & ENET_PROTOCOL_HEADER_FLAG_SENT_TIME) != 0)
            {
                packetWriter.WriteNetEndian(SentTime);
            }
        }
    }
}
