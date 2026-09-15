#if GC_UNITY_NETCODE_GAMEOBJECTS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Unity.Netcode;

namespace DSB.GC.Unity.NGO.Transport
{
    public class GCClient
    {
        public Queue<GCEvent> EventQueue { get; } = new Queue<GCEvent>();

        [DllImport("__Internal")]
        internal static extern void _GCGameMessageToJS(byte[] data, int offset, int count, uint gcClientId, bool isReliable);

        private static GCEvent NothingEvent = new GCEvent(NetworkEvent.Nothing, 0, null);

        public ulong WaitTime => 0;

        public void OnMessage(ArraySegment<byte> data)
        {
            EventQueue.Enqueue(new GCEvent(NetworkEvent.Data, 0, data.Array));
        }

        public void HandshakeWithServer()
        {
            // initiate the handshake, the server should then respond with NetworkEvent.Connect
            EventQueue.Enqueue(new GCEvent(NetworkEvent.Connect, 0, null));
        }

        public void Send(ArraySegment<byte> data, bool isReliable)
        {
            _GCGameMessageToJS(data.Array, data.Offset, data.Count, GamingCouch.Instance.ClientId, isReliable);
        }

        public GCEvent Poll()
        {
            if (EventQueue.Count > 0)
            {
                return EventQueue.Dequeue();
            }
            else
            {
                return NothingEvent;
            }
        }
    }
}
#endif