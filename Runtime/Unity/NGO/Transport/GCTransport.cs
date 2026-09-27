#if GC_UNITY_NETCODE_GAMEOBJECTS
using System;
using Unity.Netcode;
using UnityEngine;

namespace DSB.GC.Unity.NGO.Transport
{
    public class GCTransport : NetworkTransport
    {
        private GCServer GCServer = null;
        private GCClient GCClient = null;
        private bool IsStarted = false;

        public override ulong ServerClientId => 0;

        private void OnValidate()
        {
            var gamingCouch = FindFirstObjectByType<GamingCouch>();

            if (!gamingCouch)
            {
                throw new InvalidOperationException("GCTransport requires GamingCouch to be present in the scene");
            }

            if (!gamingCouch.OnlineMultiplayerSupport)
            {
                throw new InvalidOperationException("GCTransport is an unsupported temporary internal migration surface. It requires GC_ENABLE_UNSUPPORTED_MULTIPLAYER and legacy Online Multiplayer Support to be enabled.");
            }
        }

        private void ReceiveMessage(string base64String)
        {
            byte[] combined = System.Convert.FromBase64String(base64String);
            uint gcClientId = System.BitConverter.ToUInt32(combined, 0);

            byte[] data = new byte[combined.Length - 4];
            Buffer.BlockCopy(combined, 4, data, 0, data.Length);

            var clientId = gcClientId;
            if (NetworkManager.Singleton.IsServer)
            {
                GCServer.OnMessage(clientId, new ArraySegment<byte>(data));
            }
            else
            {
                GCClient.OnMessage(new ArraySegment<byte>(data));
            }
        }

        public override void DisconnectLocalClient()
        {
            // Not required for GamingCouch as the platform handles the disconnection
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            // Not required for GamingCouch as the platform handles the disconnection
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            return 0; // TODO: If this is useful or required, the platform should calculate this
        }

        public override void Initialize(NetworkManager networkManager = null)
        {
        }

        public GCEvent GetNextEvent()
        {
            if (GCClient != null)
            {
                return GCClient.Poll();
            }

            return GCServer.Poll();
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            var e = GetNextEvent();

            clientId = e.ClientId;
            receiveTime = Time.realtimeSinceStartup;

            if (e.Payload != null)
            {
                payload = new ArraySegment<byte>(e.Payload);
            }
            else
            {
                payload = new ArraySegment<byte>();
            }

            return e.Type;
        }

        public override void Send(ulong clientId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
            bool isReliable;

            // It could be possible to achieve all of these with multiple data channels on WebRTC side,
            // but should be sufficient now to either reliable or unreliable.
            switch (delivery)
            {
                case NetworkDelivery.Reliable:
                case NetworkDelivery.ReliableSequenced:
                case NetworkDelivery.ReliableFragmentedSequenced:
                    isReliable = true;
                    break;
                case NetworkDelivery.Unreliable:
                case NetworkDelivery.UnreliableSequenced:
                    isReliable = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(delivery), delivery, "Unhandled NetworkDelivery mode");
            }

            if (clientId == ServerClientId)
            {
                GCClient.Send(data, isReliable);
            }
            else
            {
                GCServer.Send(clientId, data, isReliable);
            }
        }

        public override void Shutdown()
        {
        }

        public override bool StartClient()
        {
            if (IsStarted)
            {
                throw new InvalidOperationException("Client already started");
            }

            GCClient = new GCClient();
            GCClient.HandshakeWithServer();

            IsStarted = true;

            return true;
        }

        public override bool StartServer()
        {
            if (IsStarted)
            {
                throw new InvalidOperationException("Server already started");
            }

            GCServer = new GCServer();
            IsStarted = true;

            return true;
        }
    }
}
#endif
