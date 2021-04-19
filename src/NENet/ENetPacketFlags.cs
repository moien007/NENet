using System;

namespace ENetDotNet
{
    [Flags]
    public enum ENetPacketFlags
    {
        None = 0,
        Reliable = 1 << 0,
        Unsequenced = 1 << 1,
        UnreliableFragments = 1 << 3,
    }
}
