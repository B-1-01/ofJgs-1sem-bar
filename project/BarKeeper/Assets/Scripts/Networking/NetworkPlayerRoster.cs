using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerRoster : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private PlayerSpawnManager spawnManager;

    public int PlayerCount
    {
        get
        {
            if (networkManager == null || !networkManager.IsListening)
                return 0;

            return networkManager.ConnectedClientsIds.Count;
        }
    }

    public int MaxPlayers => spawnManager != null ? spawnManager.MaxPlayers : 4;

    public string LastEvent { get; private set; } = "Waiting for players.";

    private void Start()
    {
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("[Roster] NetworkManager not found.");
            return;
        }

        if (spawnManager == null)
            spawnManager = networkManager.GetComponent<PlayerSpawnManager>();

        networkManager.OnConnectionEvent += OnConnectionEvent;
        networkManager.OnClientStopped += OnClientStopped;
        networkManager.OnServerStopped += OnServerStopped;
    }

    private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
    {
        if (manager != networkManager)
            return;

        switch (data.EventType)
        {
            case ConnectionEvent.ClientConnected:
                if (manager.IsServer)
                    Report($"Player {data.ClientId} joined.");
                else
                    Report("Connected to Host.");
                break;

            case ConnectionEvent.PeerConnected:
                if (!manager.IsServer)
                    Report($"Player {data.ClientId} joined.");
                break;

            case ConnectionEvent.ClientDisconnected:
                if (manager.IsServer)
                    Report($"Player {data.ClientId} left.");
                else
                    Report("Disconnected from Host.");
                break;

            case ConnectionEvent.PeerDisconnected:
                if (!manager.IsServer)
                    Report($"Player {data.ClientId} left.");
                break;
        }
    }

    private void Report(string message)
    {
        LastEvent = message;
        Debug.Log($"[Roster] {message}");
    }

    private void OnClientStopped(bool wasHost)
    {
        LastEvent = "Session ended.";
    }

    private void OnServerStopped(bool wasHost)
    {
        LastEvent = "Session ended.";
    }

    private void OnDestroy()
    {
        if (networkManager == null)
            return;

        networkManager.OnConnectionEvent -= OnConnectionEvent;
        networkManager.OnClientStopped -= OnClientStopped;
        networkManager.OnServerStopped -= OnServerStopped;
    }
}