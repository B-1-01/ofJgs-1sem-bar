using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public class Station : Interactable
{
    private const ulong NoUser = ulong.MaxValue;

    [Header("Station Perspective")]
    [SerializeField] private Camera stationCamera;

    [Header("In-Use Feedback")]
    [SerializeField] private GameObject inUseIndicator;

    private readonly NetworkVariable<ulong> currentUserId = new(
        NoUser,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool isUsingStationLocally = false;
    private FPSPlayerController activeLocalPlayer;

    public bool IsInUse => currentUserId.Value != NoUser;
    public bool IsUsedBy(ulong clientId) => currentUserId.Value == clientId;

    protected override void Awake()
    {
        base.Awake();

        if (stationCamera != null) stationCamera.enabled = false;
        if (inUseIndicator != null) inUseIndicator.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        currentUserId.OnValueChanged += OnStationUserChanged;
        UpdateVisualFeedback();

        if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentUserId.OnValueChanged -= OnStationUserChanged;

        if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        if (isUsingStationLocally) RestoreLocalPlayerControls();
    }

    public override bool CanInteract(FPSPlayerController player)
    {
        return IsSpawned && !IsInUse && MeetsStationRequirements(player);
    }

    protected virtual bool MeetsStationRequirements(FPSPlayerController player)
    {
        return !player.HasHeldItem;
    }

    public override void Interact(FPSPlayerController player)
    {
        if (!IsSpawned || IsInUse || !MeetsStationRequirements(player))
            return;

        RequestEnterStationRpc(player.NetworkObjectId);
    }

    [Rpc(SendTo.Server)]
    private void RequestEnterStationRpc(ulong playerNetworkObjectId, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (IsInUse) return;
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out var netObj)) return;

        FPSPlayerController player = netObj.GetComponent<FPSPlayerController>();
        if (player == null || player.OwnerClientId != senderId)
            return;

        float maxRange = player.InteractRange;
        float sqrDist = (player.transform.position - transform.position).sqrMagnitude;
        if (sqrDist > (maxRange * maxRange) + 2f)
            return;

        if (!MeetsStationRequirements(player))
            return;

        currentUserId.Value = senderId;
    }

    private void Update()
    {
        if (!isUsingStationLocally) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)
        {
            ExitStation();
        }
    }

    public void ExitStation()
    {
        if (isUsingStationLocally)
        {
            RequestExitStationRpc();
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestExitStationRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!IsUsedBy(senderId)) return;
        currentUserId.Value = NoUser;
    }

    private void OnStationUserChanged(ulong previousUser, ulong newUser)
    {
        UpdateVisualFeedback();

        ulong myClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;

        if (newUser == myClientId) EnterLocalStationMode();
        else if (previousUser == myClientId) RestoreLocalPlayerControls();
    }

    private void EnterLocalStationMode()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null) return;

        activeLocalPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<FPSPlayerController>();
        if (activeLocalPlayer == null) return;

        isUsingStationLocally = true;

        activeLocalPlayer.SetPlayerCameraActive(false);

        if (stationCamera != null) stationCamera.enabled = true;

        activeLocalPlayer.SetGameplayInputEnabled(false);

        OnMinigameStarted();
    }

    private void RestoreLocalPlayerControls()
    {
        OnMinigameExited();

        isUsingStationLocally = false;

        if (stationCamera != null) stationCamera.enabled = false;

        if (activeLocalPlayer != null)
        {
            activeLocalPlayer.SetPlayerCameraActive(true);
            activeLocalPlayer.SetGameplayInputEnabled(true);
            activeLocalPlayer = null;
        }
    }

    protected virtual void OnMinigameStarted() { }

    protected virtual void OnMinigameExited() { }

    private void UpdateVisualFeedback()
    {
        if (inUseIndicator != null) inUseIndicator.SetActive(IsInUse);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsUsedBy(clientId)) currentUserId.Value = NoUser;
    }
}