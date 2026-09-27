#if GC_UNITY_NETCODE_GAMEOBJECTS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Netcode;

namespace DSB.GC.Unity.NGO.Transport
{
    public class GCServer
    {
        private static Dictionary<ulong, GCPeer> Clients = new Dictionary<ulong, GCPeer>();
        private static readonly object ConnectionLock = new object();
        private static Queue<GCEvent> EventQueue = new Queue<GCEvent>();
        private static GCEvent NothingEvent = new GCEvent(NetworkEvent.Nothing, 0, null);

        public static ulong Ping(ulong clientId)
        {
            lock (ConnectionLock)
            {
                if (Clients.ContainsKey(clientId))
                {
                    return Clients[clientId].Ping;
                }
            }

            return 0;
        }

        public static void Send(ulong clientId, ArraySegment<byte> data, bool isReliable)
        {
            lock (ConnectionLock)
            {
                if (Clients.ContainsKey(clientId))
                {
                    if (data.Count < data.Array.Length || data.Offset > 0)
                    {
                        byte[] slimPayload = new byte[data.Count];

                        Buffer.BlockCopy(data.Array, data.Offset, slimPayload, 0, data.Count);
                        Clients[clientId].Send(slimPayload, 0, slimPayload.Length, isReliable);
                    }
                    else
                    {
                        Clients[clientId].Send(data.Array, data.Offset, data.Count, isReliable);
                    }
                }
            }
        }

        public static GCEvent Poll()
        {
            lock (ConnectionLock)
            {
                if (EventQueue.Count > 0)
                {
                    UnityEngine.Debug.Log("GCServer - Dequeue");
                    return EventQueue.Dequeue();
                }
                else
                {
                    return NothingEvent;
                }
            }
        }

        public void OnMessage(ulong clientId, ArraySegment<byte> data)
        {
            lock (ConnectionLock)
            {
                if (!Clients.ContainsKey(clientId))
                {
                    OnClientHandshake(clientId);
                }

                EventQueue.Enqueue(new GCEvent(NetworkEvent.Data, clientId, data.Array));
            }
        }

        public void OnClientHandshake(ulong clientId)
        {
            Clients[clientId] = new GCPeer(clientId);
            EventQueue.Enqueue(new GCEvent(NetworkEvent.Connect, clientId, null));
        }
    }
}
#endif