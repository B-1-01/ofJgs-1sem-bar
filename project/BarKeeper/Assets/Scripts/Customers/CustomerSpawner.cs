using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Manages server-authoritative customer spawning at predefined points.
/// Customers spawn at random intervals and occupy one point each.
/// Spawning stops when all available points are occupied.
/// </summary>
public class CustomerSpawner : MonoBehaviour
{
    [Header("Customer Prefab")]
    [SerializeField] private CustomerNPC customerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private List<Transform> spawnPoints = new();

    [Header("Spawn Timing")]
    [SerializeField, Min(0f)]
    private float minSpawnInterval = 5f;

    [SerializeField, Min(0f)]
    private float maxSpawnInterval = 10f;

    private NetworkManager networkManager;
    private CustomerNPC[] occupiedPoints;

    private readonly List<int> freePoints = new();

    private Coroutine pendingSpawn;
    private int nextCustomerId = 1;

    // Ensures that spawning is only performed by an active server.
    private bool CanSpawn =>
        networkManager != null &&
        networkManager.IsServer &&
        !networkManager.ShutdownInProgress;

    private void OnValidate()
    {
        minSpawnInterval = Mathf.Max(0f, minSpawnInterval);
        maxSpawnInterval = Mathf.Max(minSpawnInterval, maxSpawnInterval);
    }

    private void OnEnable()
    {
        RegisterNetworkManager();
    }

    /// <summary>
    /// Ensures that the NetworkManager is registered regardless of the initialization order of scene objects.
    /// </summary>
    private void Start()
    {
        RegisterNetworkManager();

        if (networkManager == null)
        {
            Debug.LogError(
                "[CustomerSpawner] NetworkManager not found.",
                this);
        }
    }

    /// <summary>
    /// Stops pending spawns and unregisters network event callbacks.
    /// </summary>
    private void OnDisable()
    {
        CancelPendingSpawn();

        if (networkManager == null)
            return;

        networkManager.OnServerStarted -= HandleServerStarted;
        networkManager.OnServerStopped -= HandleServerStopped;

        networkManager = null;
    }

    /// <summary>
    /// Finds the NetworkManager and subscribes to server lifecycle events.
    /// </summary>
    private void RegisterNetworkManager()
    {
        if (networkManager != null)
            return;

        networkManager = NetworkManager.Singleton;

        if (networkManager == null)
            return;

        networkManager.OnServerStarted += HandleServerStarted;
        networkManager.OnServerStopped += HandleServerStopped;

        if (CanSpawn)
            HandleServerStarted();
    }

    /// <summary>
    /// Initializes the spawn point occupancy tracking and starts the spawning process when the server becomes active.
    /// </summary>
    private void HandleServerStarted()
    {
        if (!CanSpawn)
            return;

        if (customerPrefab == null)
        {
            Debug.LogError("[CustomerSpawner] Customer prefab is missing.", this);
            return;
        }

        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning("[CustomerSpawner] No spawn points configured.", this);
            return;
        }

        if (occupiedPoints == null || occupiedPoints.Length != spawnPoints.Count)
        {
            occupiedPoints = new CustomerNPC[spawnPoints.Count];
        }

        ScheduleNextSpawn();
    }

    /// <summary>
    /// Stops the spawning process and resets occupancy tracking and customer identification when the server shuts down.
    /// </summary>
    private void HandleServerStopped(bool wasHost)
    {
        CancelPendingSpawn();

        if (occupiedPoints != null)
        {
            System.Array.Clear(occupiedPoints, 0, occupiedPoints.Length);
        }

        nextCustomerId = 1;
    }

    /// <summary>
    /// Schedules a new customer spawn if the server is active, no spawn is pending, and at least one point is available.
    /// </summary>
    private void ScheduleNextSpawn()
    {
        if (!CanSpawn || !isActiveAndEnabled)
            return;

        if (pendingSpawn != null)
            return;

        if (!HasFreeSpawnPoint())
            return;

        pendingSpawn = StartCoroutine(SpawnAfterDelay());
    }

    /// <summary>
    /// Waits for a random interval before spawning a customer at a randomly selected available point.
    /// </summary>
    private IEnumerator SpawnAfterDelay()
    {
        float delay = Random.Range(minSpawnInterval, maxSpawnInterval);

        yield return new WaitForSeconds(delay);

        pendingSpawn = null;

        if (!CanSpawn)
            yield break;

        int pointIndex = GetRandomFreeSpawnPoint();

        if (pointIndex == -1)
            yield break;

        SpawnCustomer(pointIndex);

        ScheduleNextSpawn();
    }

    /// <summary>
    /// Checks whether at least one valid spawn point is unoccupied.
    /// </summary>
    private bool HasFreeSpawnPoint()
    {
        if (occupiedPoints == null)
            return false;

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i] != null && occupiedPoints[i] == null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the index of a randomly selected free spawn point.
    /// Returns -1 if no valid points are available.
    /// </summary>
    private int GetRandomFreeSpawnPoint()
    {
        freePoints.Clear();

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i] != null && occupiedPoints[i] == null)
            {
                freePoints.Add(i);
            }
        }

        if (freePoints.Count == 0)
            return -1;

        int randomIndex = Random.Range(0, freePoints.Count);

        return freePoints[randomIndex];
    }

    /// <summary>
    /// Creates a customer at the specified spawn point, assigns a unique ID, and spawns it across the network.
    /// </summary>
    private void SpawnCustomer(int pointIndex)
    {
        if (!CanSpawn)
            return;

        Transform spawnPoint = spawnPoints[pointIndex];

        CustomerNPC customer = Instantiate(customerPrefab, spawnPoint.position, spawnPoint.rotation);

        int customerId = nextCustomerId++;

        customer.Initialize(this, customerId, pointIndex);

        occupiedPoints[pointIndex] = customer;

        customer.NetworkObject.Spawn(true);

        Debug.Log($"[CustomerSpawner] Created customer {customerId} " + $"at {spawnPoint.name}. " + $"NetworkObjectId: {customer.NetworkObjectId}", this);
    }

    /// <summary>
    /// Releases the spawn point occupied by a despawned customer.
    /// Schedules a new spawn if the server is still active.
    /// </summary>
    public void OnCustomerDespawned(CustomerNPC customer, int pointIndex)
    {
        if (occupiedPoints == null)
            return;

        if (pointIndex < 0 || pointIndex >= occupiedPoints.Length)
            return;

        if (occupiedPoints[pointIndex] != customer)
            return;

        occupiedPoints[pointIndex] = null;

        Debug.Log($"[CustomerSpawner] Customer {customer.CustomerId} " + $"left spawn point {pointIndex}.", this);

        ScheduleNextSpawn();
    }

    /// <summary>
    /// Cancels any pending customer spawn timer.
    /// </summary>
    private void CancelPendingSpawn()
    {
        if (pendingSpawn == null)
            return;

        StopCoroutine(pendingSpawn);
        pendingSpawn = null;
    }
}