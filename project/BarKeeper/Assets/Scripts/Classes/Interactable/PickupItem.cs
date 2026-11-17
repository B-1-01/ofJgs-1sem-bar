using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PickupItem : Interactable
{
    private const ulong NoHolder = ulong.MaxValue;

    // Guarda o ID do jogador que tem o objeto (apenas o Servidor pode alterar)
    private readonly NetworkVariable<ulong> holderClientId = new(
        NoHolder,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Rigidbody rb;
    private Collider itemCollider;
    private NetworkTransform netTransform;
    private FPSPlayerController currentHolder;

    public bool IsHeld => holderClientId.Value != NoHolder;
    public bool IsHeldBy(ulong clientId) => holderClientId.Value == clientId;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
        itemCollider = GetComponent<Collider>();
        netTransform = GetComponent<NetworkTransform>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        holderClientId.OnValueChanged += OnHolderChanged;
        UpdatePhysicalState();

        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        holderClientId.OnValueChanged -= OnHolderChanged;

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void LateUpdate()
    {
        if (IsHeld && currentHolder != null)
        {
            Transform targetHold = currentHolder.HeldPosition;
            if (targetHold != null) transform.SetPositionAndRotation(targetHold.position, targetHold.rotation);
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer || !IsHeld || currentHolder == null) return;
        if (!currentHolder.IsSpawned || currentHolder.OwnerClientId != holderClientId.Value) ServerDrop(Vector3.zero);
    }

    /// <summary>Não permite focar/interagir se o objeto já estiver a ser segurado por alguém.</summary>
    public override bool CanInteract(FPSPlayerController player)
    {
        return IsSpawned && !IsHeld && !player.HasHeldItem;
    }

    public override void Interact(FPSPlayerController player)
    {
        if (!IsSpawned || IsHeld || player.HasHeldItem) return;
        AttachLocallyTo(player);
        RequestPickupRpc(player.NetworkObjectId);
    }

    [Rpc(SendTo.Server)]
    private void RequestPickupRpc(ulong playerNetworkObjectId, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (IsHeld)
            return;

        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out var netObj))
            return;

        FPSPlayerController player = netObj.GetComponent<FPSPlayerController>();
        if (player == null || player.OwnerClientId != senderId)
            return;

        if (player.HasHeldItem && player.CurrentlyHeldItem != this)
            return;

        float maxRange = player.InteractRange;
        float sqrDist = (player.transform.position - transform.position).sqrMagnitude;
        if (sqrDist > maxRange * maxRange + 2f)
            return;

        holderClientId.Value = senderId;
        AttachLocallyTo(player);
    }

    public void RequestDrop(Vector3 dropVelocity)
    {
        if (!IsSpawned) return;
        RequestDropRpc(dropVelocity);
    }

    public void RequestPlace(Vector3 position, Quaternion rotation)
    {
        if (!IsSpawned) return;
        RequestPlaceRpc(position, rotation);
    }

    [Rpc(SendTo.Server)]
    private void RequestDropRpc(Vector3 dropVelocity, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (!IsHeldBy(senderId)) return;

        ServerDrop(dropVelocity);
    }

    
    [Rpc(SendTo.Server)]
    private void RequestPlaceRpc(Vector3 position, Quaternion rotation, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (!IsHeldBy(senderId)) return;

        ServerPlaceAtRpc(position, rotation);
    }

    protected virtual void ServerDrop(Vector3 velocity)
    {
        holderClientId.Value = NoHolder;
        DetachLocally();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Vector3.zero;
        }
    }
    
    protected virtual void ServerPlaceAtRpc(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

        holderClientId.Value = NoHolder;
        DetachLocally();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void OnHolderChanged(ulong previous, ulong current)
    {
        if (previous != NoHolder)
        {
            FPSPlayerController prevPlayer = FindPlayerController(previous);
            if (prevPlayer != null)
                prevPlayer.ClearHeldItem();
        }

        if (current != NoHolder)
        {
            currentHolder = FindPlayerController(current);
            if (currentHolder != null)
            {
                AttachLocallyTo(currentHolder);
            }
        }
        else
        {
            DetachLocally();
        }
    }

    /// <summary>
    /// Configura o objeto para seguir o jogador localmente sem lag.
    /// Desativa o NetworkTransform para que a posição não seja sobrescrita por pacotes de rede.
    /// </summary>
    private void AttachLocallyTo(FPSPlayerController player)
    {
        currentHolder = player;
        player.SetHeldItem(this);
        
        itemCollider.enabled = false;

        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }

        if (netTransform != null) netTransform.enabled = false;

        Transform targetHold = player.HeldPosition;
        if (targetHold != null) transform.SetPositionAndRotation(targetHold.position, targetHold.rotation);
    }

    /// <summary>
    /// Desassocia o objeto do jogador e reativa o NetworkTransform e a física.
    /// </summary>
    private void DetachLocally()
    {
        if (currentHolder != null)
        {
            currentHolder.ClearHeldItem();
            currentHolder = null;
        }
        itemCollider.enabled = !IsHeld;

        if (netTransform != null) netTransform.enabled = true;

        UpdatePhysicalState();
    }

    private FPSPlayerController FindPlayerController(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return null;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null) return client.PlayerObject.GetComponent<FPSPlayerController>();

        FPSPlayerController[] players = FindObjectsByType<FPSPlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.IsSpawned && p.OwnerClientId == clientId)
                return p;
        }
        return null;
    }

    private void UpdatePhysicalState()
    {
        bool isHeld = IsHeld;
        if (itemCollider != null) itemCollider.enabled = !isHeld;
        if (rb != null)
        {
            if (!rb.isKinematic && isHeld)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = isHeld;
        }

        if (netTransform != null)
        {
            netTransform.enabled = !isHeld;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsHeldBy(clientId))
            ServerDrop(Vector3.zero);
    }

    /// <summary>
    /// Coloca o objeto numa posição específica pelo servidor (ex: na bancada ou estação),
    /// libertando-o do jogador e mantendo o estado de rede sincronizado.
    /// </summary>
    public virtual void ServerPlaceAt(Transform targetTransform, bool makeKinematic = true)
    {
        if (!IsServer) return;

        holderClientId.Value = NoHolder;
        DetachLocally();

        if (targetTransform != null)
        {
            transform.SetPositionAndRotation(targetTransform.position, targetTransform.rotation);
        }

        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = makeKinematic;
        }

        UpdatePhysicalState();
    }
}
