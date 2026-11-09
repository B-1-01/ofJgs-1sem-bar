using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using PlayEveryWare.EpicOnlineServices;
using PlayEveryWare.EpicOnlineServices.Samples.Network;

public class EOSNetworkBridge : MonoBehaviour
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Disconnecting
    }

    [Header("References")]
    [SerializeField] private EOSLoginManager loginManager;
    [SerializeField] private EOSTransport eosTransport;

    [Header("Settings")]
    [SerializeField] private float connectionTimeout = 30f;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public string Status { get; private set; } = "Disconnected";

    public event Action<ConnectionState, string> StateChanged;
    public event Action<string> ConnectionFailed;

    private NetworkManager networkManager;
    private Coroutine connectionTimeoutRoutine;
    private bool eventsRegistered;
    private bool failureReported;

    private void Start()
    {
        InitializeReferences();
    }

    private void InitializeReferences()
    {
        if (loginManager == null)
            loginManager = GetComponent<EOSLoginManager>();

        if (eosTransport == null)
            eosTransport = FindFirstObjectByType<EOSTransport>();

        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null || eventsRegistered)
            return;

        networkManager.OnConnectionEvent += OnConnectionEvent;
        networkManager.OnTransportFailure += OnTransportFailure;
        networkManager.OnClientStopped += OnClientStopped;
        networkManager.OnServerStopped += OnServerStopped;

        eventsRegistered = true;
    }

    public bool StartHost()
    {
        if (!CanStart())
            return false;

        failureReported = false;
        SetState(ConnectionState.Connecting, "Starting NGO Host...");

        if (!networkManager.StartHost())
            return StartFailed("Failed to start NGO Host.");

        SetState(ConnectionState.Connected, "NGO Host running.");
        Debug.Log("[NGO] Host started successfully.");

        return true;
    }

    public bool StartClient(string lobbyId)
    {
        if (!CanStart())
            return false;

        if (string.IsNullOrWhiteSpace(lobbyId))
            return StartFailed("Invalid Lobby ID.");

        var lobbyInterface = EOSManager.Instance.GetEOSPlatformInterface().GetLobbyInterface();

        var options = new CopyLobbyDetailsHandleOptions
        {
            LobbyId = lobbyId,
            LocalUserId = loginManager.LocalUserId
        };

        Result result = lobbyInterface.CopyLobbyDetailsHandle(
            ref options, out LobbyDetails details);

        if (result != Result.Success || details == null)
            return StartFailed($"Failed to get lobby details: {result}");

        ProductUserId hostId;

        try
        {
            var ownerOptions = new LobbyDetailsGetLobbyOwnerOptions();
            hostId = details.GetLobbyOwner(ref ownerOptions);
        }
        finally
        {
            details.Release();
        }

        if (hostId == null || !hostId.IsValid())
            return StartFailed("Lobby Host PUID is invalid.");

        if (hostId == loginManager.LocalUserId)
            return StartFailed("Cannot connect to your own lobby as a Client.");

        eosTransport.ServerUserIdToConnectTo = hostId;

        failureReported = false;
        SetState(ConnectionState.Connecting, "Connecting to NGO Host...");

        Debug.Log($"[NGO] Connecting to Host PUID: {hostId}");

        if (!networkManager.StartClient())
            return StartFailed("Failed to start NGO Client.");

        if (State == ConnectionState.Connecting)
            connectionTimeoutRoutine = StartCoroutine(ConnectionTimeout());

        return true;
    }

    private IEnumerator ConnectionTimeout()
    {
        yield return new WaitForSecondsRealtime(connectionTimeout);

        connectionTimeoutRoutine = null;

        if (State == ConnectionState.Connecting)
            FailConnection("NGO connection timed out.");
    }

    private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
    {
        if (manager != networkManager)
            return;

        if (manager.IsServer)
        {
            if (data.EventType == ConnectionEvent.ClientConnected)
                Debug.Log($"[NGO] Player {data.ClientId} connected.");

            if (data.EventType == ConnectionEvent.ClientDisconnected)
                Debug.Log($"[NGO] Player {data.ClientId} disconnected.");

            return;
        }

        if (data.ClientId != manager.LocalClientId)
            return;

        if (data.EventType == ConnectionEvent.ClientConnected &&
            State == ConnectionState.Connecting)
        {
            StopConnectionTimeout();
            SetState(ConnectionState.Connected, "Connected to NGO Host.");

            Debug.Log("[NGO] Client connection confirmed.");
        }
        else if (data.EventType == ConnectionEvent.ClientDisconnected &&
                 (State == ConnectionState.Connecting ||
                  State == ConnectionState.Connected))
        {
            string reason = string.IsNullOrEmpty(manager.DisconnectReason)
                ? "Disconnected from NGO Host."
                : $"Disconnected: {manager.DisconnectReason}";

            FailConnection(reason);
        }
    }

    private void OnTransportFailure()
    {
        FailConnection("EOS network transport failure.");
    }

    private void OnClientStopped(bool wasHost)
    {
        HandleNetworkStopped();
    }

    private void OnServerStopped(bool wasHost)
    {
        HandleNetworkStopped();
    }

    private void HandleNetworkStopped()
    {
        if (State == ConnectionState.Disconnecting)
        {
            SetState(ConnectionState.Disconnected, "Disconnected.");
        }
        else if (State == ConnectionState.Connecting ||
                 State == ConnectionState.Connected)
        {
            FailConnection("NGO session stopped unexpectedly.");
        }
    }

    private void FailConnection(string reason)
    {
        if (State != ConnectionState.Connecting &&
            State != ConnectionState.Connected)
            return;

        Debug.LogError($"[NGO] {reason}");

        StopConnectionTimeout();
        SetState(ConnectionState.Disconnecting, reason);

        if (networkManager != null && networkManager.IsListening)
            networkManager.Shutdown();

        if (!failureReported)
        {
            failureReported = true;
            ConnectionFailed?.Invoke(reason);
        }

        if (networkManager == null || !networkManager.IsListening)
            SetState(ConnectionState.Disconnected, reason);
    }

    private bool StartFailed(string reason)
    {
        Debug.LogError($"[NGO] {reason}");

        StopConnectionTimeout();

        if (networkManager != null && networkManager.IsListening)
        {
            SetState(ConnectionState.Disconnecting, reason);
            networkManager.Shutdown();
        }
        else
        {
            SetState(ConnectionState.Disconnected, reason);
        }

        return false;
    }

    public void Shutdown()
    {
        StopConnectionTimeout();

        if (State == ConnectionState.Disconnected ||
            State == ConnectionState.Disconnecting)
            return;

        SetState(ConnectionState.Disconnecting, "Disconnecting...");

        if (networkManager != null && networkManager.IsListening)
            networkManager.Shutdown();
        else
            SetState(ConnectionState.Disconnected, "Disconnected.");
    }

    private bool CanStart()
    {
        InitializeReferences();

        if (loginManager == null || !loginManager.IsLoggedIn)
            return StartFailed("EOS login is not ready.");

        if (networkManager == null || eosTransport == null)
            return StartFailed("NetworkManager or EOSTransport is missing.");

        if (networkManager.NetworkConfig.NetworkTransport != eosTransport)
            return StartFailed("EOSTransport is not assigned to NetworkManager.");

        if (State != ConnectionState.Disconnected || networkManager.IsListening)
        {
            Debug.LogWarning("[NGO] Another network session is still active.");
            return false;
        }

        if (EOSManager.Instance == null ||
            EOSManager.Instance.GetProductUserId()?.IsValid() != true)
        {
            return StartFailed("EOS Product User ID is not available.");
        }

        return true;
    }

    private void SetState(ConnectionState state, string message)
    {
        State = state;
        Status = message;
        StateChanged?.Invoke(state, message);
    }

    private void StopConnectionTimeout()
    {
        if (connectionTimeoutRoutine == null)
            return;

        StopCoroutine(connectionTimeoutRoutine);
        connectionTimeoutRoutine = null;
    }

    private void OnDestroy()
    {
        StopConnectionTimeout();

        if (networkManager == null || !eventsRegistered)
            return;

        networkManager.OnConnectionEvent -= OnConnectionEvent;
        networkManager.OnTransportFailure -= OnTransportFailure;
        networkManager.OnClientStopped -= OnClientStopped;
        networkManager.OnServerStopped -= OnServerStopped;
    }
}