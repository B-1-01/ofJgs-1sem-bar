using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Represents a networked customer NPC, managing its unique identification, spawn point assignment, and server-authoritative despawn.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class CustomerNPC : NetworkBehaviour
{
    private int customerId;
    private int spawnPointIndex = -1;
    private CustomerSpawner spawner;

    public int CustomerId => customerId;
    public int SpawnPointIndex => spawnPointIndex;

    /// <summary>
    /// Initializes the customer with a unique ID and spawn point.
    /// Must be called by the server before spawning the NetworkObject.
    /// </summary>
    public void Initialize(CustomerSpawner customerSpawner, int id, int pointIndex)
    {
        if (IsSpawned)
        {
            Debug.LogError("[CustomerNPC] Cannot initialize an already spawned NPC.", this);
            return;
        }

        spawner = customerSpawner;
        customerId = id;
        spawnPointIndex = pointIndex;
    }

    /// <summary>
    /// Synchronizes the customer's initial data with connected clients, including players who join after the customer has spawned.
    /// </summary>
    protected override void OnSynchronize<T>(
        ref BufferSerializer<T> serializer)
    {
        serializer.SerializeValue(ref customerId);
        serializer.SerializeValue(ref spawnPointIndex);

        base.OnSynchronize(ref serializer);
    }

    /// <summary>
    /// Called when the customer is spawned on the network.
    /// Logs its identification and spawn information for debugging.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        Debug.Log(
            $"[CustomerNPC] Spawned | " +
            $"CustomerId: {CustomerId} | " +
            $"NetworkObjectId: {NetworkObjectId} | " +
            $"SpawnPoint: {SpawnPointIndex} | " +
            $"Position: {transform.position}",
            this);
    }

    /// <summary>
    /// Notifies the spawner when the customer is despawned, allowing the server to free the occupied spawn point.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        if (IsServer && spawner != null)
        {
            spawner.OnCustomerDespawned(this, spawnPointIndex);
        }
    }

    /// <summary>
    /// Despawns the customer across the network.
    /// Only the server can execute this operation.
    /// </summary>
    [ContextMenu("Despawn Customer (Server Only)")]
    public void DespawnCustomer()
    {
        if (!IsServer || !IsSpawned)
            return;

        NetworkObject.Despawn();
    }

    /// <summary>
    /// Requests the server to despawn this customer.
    /// Can be called by any connected player.
    /// </summary>
    public void RequestDespawn()
    {
        if (!IsSpawned)
            return;

        RequestDespawnRpc();
    }

    /// <summary>
    /// Handles despawn requests on the server ensuring that customers are only removed by the server.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void RequestDespawnRpc()
    {
        DespawnCustomer();
    }

}