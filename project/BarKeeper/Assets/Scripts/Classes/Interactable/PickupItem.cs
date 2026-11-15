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
    private FPSPlayerController currentHolder;

    public bool IsHeld => holderClientId.Value != NoHolder;
    public bool IsHeldBy(ulong clientId) => holderClientId.Value == clientId;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
        itemCollider = GetComponent<Collider>();
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

    private void FixedUpdate()
    {
        // Apenas o servidor move fisicamente o objeto para o ponto do jogador
        if (!IsServer || !IsHeld || currentHolder == null)
            return;

        // Se o jogador perdeu a ligação ou despawnou, larga o objeto
        if (!currentHolder.IsSpawned || currentHolder.OwnerClientId != holderClientId.Value)
        {
            ServerDrop(Vector3.zero);
            return;
        }

        // Segue suavemente a posição e rotação de transporte do jogador
        Transform targetHold = currentHolder.HeldPosition;
        if (targetHold != null)
        {
            rb.position = targetHold.position;
            rb.rotation = targetHold.rotation;
            transform.SetPositionAndRotation(targetHold.position, targetHold.rotation);
        }
    }

    /// <summary>Não permite focar/interagir se o objeto já estiver a ser segurado por alguém.</summary>
    public override bool CanInteract(FPSPlayerController player)
    {
        return IsSpawned && !IsHeld && !player.HasHeldItem;
    }

    public override void Interact(FPSPlayerController player)
    {
        if (!IsSpawned || IsHeld || player.HasHeldItem)
            return;

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

        if (player.HasHeldItem)
            return;

        float maxRange = player.InteractRange;
        float sqrDist = (player.transform.position - transform.position).sqrMagnitude;
        if (sqrDist > maxRange * maxRange + 2f)
            return;

        currentHolder = player;
        holderClientId.Value = senderId;
        player.SetHeldItem(this);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    public void RequestDrop(Vector3 dropVelocity)
    {
        if (!IsSpawned) return;
        RequestDropRpc(dropVelocity);
    }

    [Rpc(SendTo.Server)]
    private void RequestDropRpc(Vector3 dropVelocity, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (!IsHeldBy(senderId)) return;

        ServerDrop(dropVelocity);
    }

    private void ServerDrop(Vector3 velocity)
    {
        if (currentHolder != null)
        {
            currentHolder.ClearHeldItem();
            currentHolder = null;
        }

        holderClientId.Value = NoHolder;

        rb.isKinematic = false;
        rb.linearVelocity = velocity;
        rb.angularVelocity = Vector3.zero;
    }

    private void OnHolderChanged(ulong previous, ulong current)
    {
        UpdatePhysicalState();

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
                currentHolder.SetHeldItem(this);
        }
        else
        {
            currentHolder = null;
        }
    }

    private FPSPlayerController FindPlayerController(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return null;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
        {
            return client.PlayerObject.GetComponent<FPSPlayerController>();
        }

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
        if (itemCollider != null) itemCollider.isTrigger = IsHeld;
        if (!IsServer) rb.isKinematic = IsHeld;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsHeldBy(clientId))
            ServerDrop(Vector3.zero);
    }
}