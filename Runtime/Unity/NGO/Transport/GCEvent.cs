#if GC_UNITY_NETCODE_GAMEOBJECTS
using Unity.Netcode;

namespace DSB.GC.Unity.NGO.Transport
{
    public readonly struct GCEvent
    {
        public GCEvent(NetworkEvent type, ulong clientId, byte[] payload)
        {
            Type = type;
            ClientId = clientId;
            Payload = payload;
        }

        public readonly NetworkEvent Type;
        public readonly ulong ClientId;
        public readonly byte[] Payload;
    }
}
#endif