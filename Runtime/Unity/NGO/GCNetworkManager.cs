#if GC_UNITY_NETCODE_GAMEOBJECTS
using UnityEngine;
using Unity.Netcode;
using DSB.GC.Unity.NGO.Transport;
using System;

namespace DSB.GC.Unity.NGO
{
  [RequireComponent(typeof(NetworkManager))]
  [RequireComponent(typeof(GCTransport))]
  public class GCNetworkManager : MonoBehaviour
  {
    [Serializable]
    private class ClientConnectionData
    {
      public uint GCClientId;
    }

    public Action OnServerStarted;
    public Action OnClientStarted;
    public Action<ulong> OnClientConnectedCallback;

    private NetworkManager networkManager;

    private static GCNetworkManager instance;
    public static GCNetworkManager Instance => instance;

    private bool startServerCalled = false;
    private bool startClientCalled = false;

    private void Awake()
    {
      if (instance != null)
      {
        Destroy(gameObject);
        Debug.LogWarning("Multiple GCNetworkManager instances detected. Only one GCNetworkManager instance is allowed.");
        return;
      }

      instance = this;

      networkManager = GetComponent<NetworkManager>();

      networkManager.OnServerStarted += () =>
      {
        if (!startServerCalled)
        {
          throw new InvalidOperationException("You should call StartServer() via GCNetworkManager. Maybe you called via the NetworkManager directly?");
        }

        OnServerStarted?.Invoke();
      };

      networkManager.OnClientStarted += () =>
      {
        if (!startClientCalled)
        {
          throw new InvalidOperationException("You should call StartClient() via GCNetworkManager. Maybe you called via the NetworkManager directly?");
        }

        OnClientStarted?.Invoke();
      };

      networkManager.OnClientConnectedCallback += (ulong clientId) =>
      {
        OnClientConnectedCallback?.Invoke(clientId);
      };
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
      response.Approved = true;
      response.CreatePlayerObject = false;
      response.PlayerPrefabHash = null;
      response.Pending = false;
    }

    private void SetNetworkConfigs()
    {
      NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
      NetworkManager.Singleton.NetworkConfig.ClientConnectionBufferTimeout = 9999;
    }

    public void StartServer()
    {
      if (!GamingCouch.Instance.IsServer)
      {
        throw new InvalidOperationException("StartServer can only be called on the server.");
      }

      if (startServerCalled)
      {
        throw new InvalidOperationException("StartServer has already been called.");
      }

      startServerCalled = true;

      SetNetworkConfigs();
      NetworkManager.Singleton.ConnectionApprovalCallback = ApproveConnection;
      NetworkManager.Singleton.StartServer();
    }

    public void StartClient()
    {
      if (GamingCouch.Instance.IsServer)
      {
        throw new InvalidOperationException("StartClient can only be called on the client.");
      }

      if (startClientCalled)
      {
        throw new InvalidOperationException("StartClient has already been called.");
      }

      startClientCalled = true;
      SetNetworkConfigs();
      NetworkManager.Singleton.StartClient();
    }

    public static byte[] SerializeToBytes<T>(T obj)
    {
      string json = JsonUtility.ToJson(obj);
      return System.Text.Encoding.UTF8.GetBytes(json);
    }

    public static T DeserializeFromBytes<T>(byte[] data)
    {
      string json = System.Text.Encoding.UTF8.GetString(data);
      return JsonUtility.FromJson<T>(json);
    }
  }
}
#endif