#if GC_UNITY_NETCODE_GAMEOBJECTS
using System.Runtime.InteropServices;

namespace DSB.GC.Unity.NGO.Transport
{
    public struct GCPeer
    {
        [DllImport("__Internal")]
        internal static extern void _GCGameMessageToJS(byte[] data, int offset, int count, uint gcClientId, bool isReliable);

        public ulong ClientId;

        public GCPeer(ulong clientId)
        {
            ClientId = clientId;
        }

        internal void Send(byte[] data, int offset, int count, bool isReliable)
        {
            _GCGameMessageToJS(data, offset, count, (uint)ClientId, isReliable);
        }

        internal ulong Ping
        {
            get
            {
                return 0;
            }
        }
    }
}
#endif