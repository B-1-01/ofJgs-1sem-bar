using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public enum InteractionType
{
    Pickup,
    Station
}

/// <summary>
/// Classe única para objetos interativos (Pickup ou Station).
/// </summary>
[RequireComponent(typeof(Outline))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkObject))]
public class InteractableObject : NetworkBehaviour
{
    private const ulong NoUser = ulong.MaxValue;

    [Header("Interaction Settings")]
    [SerializeField] private InteractionType interactionType = InteractionType.Pickup;
    [SerializeField] private string interactionName = "Objeto";

    [Header("Outline Settings (Local Only)")]
    [SerializeField] private bool useOutline = true;
    [SerializeField] private Outline outline;

    [Header("Station Minigame Events (Local Client)")]
    [SerializeField] private UnityEvent onStartMinigame;
    [SerializeField] private UnityEvent onStopMinigame;

    // Para Pickup: NetworkObjectId
    // Para Station: ClientId
    private NetworkVariable<ulong> currentUserId = new NetworkVariable<ulong>(NoUser);

    private Rigidbody rb;
    private Collider objCollider;
    private Transform currentHoldPoint;

    public InteractionType Type => interactionType;
    public string GetInteractionName() => interactionName;
    public bool IsInUse => currentUserId.Value != NoUser;

    protected virtual void Awake()
    {
        if (outline == null)
            outline = GetComponent<Outline>();

        rb = GetComponent<Rigidbody>();
        objCollider = GetComponent<Collider>();

        if (outline != null)
            outline.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        currentUserId.OnValueChanged += OnUserChanged;

        // Sincronizar
        if (currentUserId.Value != NoUser)
        {
            OnUserChanged(NoUser, currentUserId.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        currentUserId.OnValueChanged -= OnUserChanged;
    }

    private void LateUpdate()
    {
        if (interactionType == InteractionType.Pickup && currentHoldPoint != null)
        {
            transform.SetPositionAndRotation(currentHoldPoint.position, currentHoldPoint.rotation);
        }
    }

    /// <summary>Chamado APENAS localmente pelo raycast da câmara do jogador.</summary>
    public void SetFocus(bool isFocused)
    {
        if (outline == null)
            return;

        outline.enabled = useOutline && !IsInUse && isFocused;
    }

    public void SetUseOutline(bool canUse)
    {
        useOutline = canUse;

        if (!useOutline && outline != null)
            outline.enabled = false;
    }

    // Interaction

    public virtual void Interact(FPSPlayerController player)
    {
        if (interactionType == InteractionType.Pickup)
        {
            RequestPickupServerRpc(player.NetworkObjectId);
        }
        else if (interactionType == InteractionType.Station)
        {
            RequestUseStationServerRpc();
        }
    }

    // Pick-Up

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPickupServerRpc(ulong playerNetworkObjectId, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        // 1. Host verifica disponibilidade
        if (currentUserId.Value != NoUser)
        {
            Debug.LogWarning($"[Pickup Host] Recusado: {gameObject.name} já está a ser segurado!");
            return;
        }

        // 2. Host valida o jogador
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerNetObj))
            return;

        if (playerNetObj.OwnerClientId != senderClientId)
            return;

        FPSPlayerController player = playerNetObj.GetComponent<FPSPlayerController>();

        // 3. Host verifica mão do jogador
        if (player == null || player.HasItemInHand)
        {
            Debug.LogWarning($"[Pickup Host] Recusado: O jogador {senderClientId} já tem um item na mão!");
            return;
        }

        // 4. Host aprova
        player.ServerSetHeldItem(this);
        currentUserId.Value = playerNetworkObjectId;

        Debug.Log($"[Pickup Host] Sucesso: Jogador {senderClientId} apanhou {gameObject.name}!");
    }

    public void ServerDropItem()
    {
        if (!IsServer || interactionType != InteractionType.Pickup)
            return;

        Debug.Log($"[Pickup Host] {gameObject.name} foi largado!");
        currentUserId.Value = NoUser;
    }

    // Station

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestUseStationServerRpc(RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        // 1. Host verifica a station
        if (currentUserId.Value != NoUser)
        {
            Debug.Log($"[Station] {gameObject.name} já está ocupada!");
            return;
        }

        // 2. Host reserva a station
        currentUserId.Value = senderClientId;

        // 3. Host manda iniciar o minijogo APENAS no client
        StartMinigameClientRpc(RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void StartMinigameClientRpc(RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            FPSPlayerController localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<FPSPlayerController>();
            if (localPlayer != null)
            {
                localPlayer.EnterStationMinigame(this);
            }
        }

        Debug.Log($"[Minigame] Iniciado em {gameObject.name}! (Prime Q para sair)");
        onStartMinigame?.Invoke();
    }

    public void ExitStationLocal(FPSPlayerController localPlayer)
    {
        localPlayer.ExitStationMinigame();
        Debug.Log($"[Minigame] Terminado em {gameObject.name}.");
        onStopMinigame?.Invoke();
        ReleaseStationServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ReleaseStationServerRpc(RpcParams rpcParams = default)
    {
        if (currentUserId.Value == rpcParams.Receive.SenderClientId)
        {
            currentUserId.Value = NoUser;
        }
    }

    // Sync

    private void OnUserChanged(ulong previousUser, ulong newUser)
    {
    
        if (newUser != NoUser && outline != null)
        {
            outline.enabled = false;
        }

        if (interactionType == InteractionType.Pickup)
        {
            if (newUser != NoUser)
                ApplyPickupHeldState(newUser);
            else
                ApplyPickupDroppedState(previousUser);
        }
    }

    private void ApplyPickupHeldState(ulong playerNetId)
    {
        if (rb != null) rb.isKinematic = true;
        if (objCollider != null) objCollider.enabled = false;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetId, out NetworkObject playerNetObj))
        {
            FPSPlayerController player = playerNetObj.GetComponent<FPSPlayerController>();
            if (player != null)
            {
                player.SetHeldItemReference(this);
                currentHoldPoint = player.HandHoldPoint;
            }
        }
    }

    private void ApplyPickupDroppedState(ulong previousPlayerNetId)
    {
        currentHoldPoint = null;

        if (rb != null) rb.isKinematic = false;
        if (objCollider != null) objCollider.enabled = true;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(previousPlayerNetId, out NetworkObject playerNetObj))
        {
            FPSPlayerController player = playerNetObj.GetComponent<FPSPlayerController>();
            if (player != null)
            {
                player.SetHeldItemReference(null);
            }
        }
    }
}