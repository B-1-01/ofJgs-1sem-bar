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

    [Header("Order System")]
    [SerializeField] private DrinkOrderManager orderManager;

    [Header("Spawn Points")]
    [SerializeField] private List<Transform> spawnPoints = new();

    [Header("Counter Points")]
    [SerializeField] private List<Transform> counterPoints = new();

    [Header("Spawn Timing")]
    [SerializeField, Min(0f)]
    private float minSpawnInterval = 5f;

    [SerializeField, Min(0f)]
    private float maxSpawnInterval = 10f;

    private NetworkManager networkManager;
    private CustomerNPC[] occupiedCounterPoints;

    private readonly List<int> freeCounterPoints = new();

    private readonly List<int> freeSpawnPoints = new();

    private Coroutine pendingSpawn;
    private int nextCustomerId = 1;

    private bool spawningEnabled = false;

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
        if (orderManager == null)
            orderManager = FindFirstObjectByType<DrinkOrderManager>();

        if (orderManager != null)
            orderManager.EligibleRecipesChanged += OnRecipesChanged;

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
            Debug.LogError("[CustomerSpawner] NetworkManager not found.", this);
        }

        if (orderManager == null)
        {
            Debug.LogError("[CustomerSpawner] DrinkOrderManager not found.", this);
        }
    }

    /// <summary>
    /// Stops pending spawns and unregisters network event callbacks.
    /// </summary>
    private void OnDisable()
    {
        CancelPendingSpawn();

        if (orderManager != null)
            orderManager.EligibleRecipesChanged -= OnRecipesChanged;

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

        if (occupiedCounterPoints == null || occupiedCounterPoints.Length != counterPoints.Count)
        {
            occupiedCounterPoints = new CustomerNPC[counterPoints.Count];
        }

        ScheduleNextSpawn();
    }

    /// <summary>
    /// Stops the spawning process and resets occupancy tracking and customer identification when the server shuts down.
    /// </summary>
    private void HandleServerStopped(bool wasHost)
    {
        CancelPendingSpawn();

        if (occupiedCounterPoints != null)
        {
            System.Array.Clear(occupiedCounterPoints, 0, occupiedCounterPoints.Length);
        }

        nextCustomerId = 1;
    }

    /// <summary>
    /// Starts or stops customer spawning when recipe availability changes.
    /// </summary>
    private void OnRecipesChanged()
    {
        if (!CanSpawn)
            return;

        if (!spawningEnabled)
        {
            CancelPendingSpawn();
            return;
        }

        if (orderManager != null && orderManager.HasEligibleRecipes())
        {
            ScheduleNextSpawn();
        }
        else
        {
            CancelPendingSpawn();
        }
    }

    /// <summary>
    /// Schedules a new customer spawn if the server is active, no spawn is pending, and at least one point is available.
    /// </summary>
    private void ScheduleNextSpawn()
    {
        if (!CanSpawn || !isActiveAndEnabled || !spawningEnabled)
            return;

        if (pendingSpawn != null)
            return;

        if (orderManager == null || !orderManager.HasEligibleRecipes())
            return;

        if (!HasFreeCounterPoint())
            return;

        if (!HasValidSpawnPoint())
            return;

        pendingSpawn = StartCoroutine(SpawnAfterDelay());
    }

    /// <summary>
    /// Turning ON and OFF spawning.
    /// </summary>
    public void SetSpawningEnabled(bool enabled)
    {
        if (!CanSpawn)
        {
            return;
        }

        spawningEnabled = enabled;

        if (!spawningEnabled)
        {
            CancelPendingSpawn();

            Debug.Log($"[CustomerSpawner] Spawning disabled.", this);
            return;
        }

        Debug.Log($"[CustomerSpawner] Spawning enabled.", this);

        ScheduleNextSpawn();
    }

    /// <summary>
    /// Sending away the remaining customers.
    /// </summary>
    public void DismissRemainingCustomers()
    {
        if (!CanSpawn)
        {
            return;
        }

        SetSpawningEnabled(false);

        if(occupiedCounterPoints == null)
        {
            return;
        }

        for(int i = 0; i < occupiedCounterPoints.Length; i++)
        {
            CustomerNPC customer = occupiedCounterPoints[i];

            if(customer != null && customer.IsSpawned)
            {
                customer.DespawnCustomer();
            }
        }

        Debug.Log($"[CustomerSpawner] Remaining customers dismissed.", this);
    }

    /// <summary>
    /// Waits for a random interval before spawning a customer at a randomly selected available point.
    /// </summary>
    private IEnumerator SpawnAfterDelay()
    {
        float delay = Random.Range(minSpawnInterval, maxSpawnInterval);

        yield return new WaitForSeconds(delay);

        pendingSpawn = null;

        if (!CanSpawn || !spawningEnabled)
            yield break;

        int counterIndex = GetRandomFreeCounterPoint();
        int spawnIndex = GetRandomSpawnPoint();

        if (counterIndex == -1 || spawnIndex == -1)
        {
            yield break;
        }    

        SpawnCustomer(spawnIndex, counterIndex);

        ScheduleNextSpawn();

    }

    /// <summary>
    /// Checks whether at least one valid counter point is unoccupied.
    /// </summary>
    private bool HasFreeCounterPoint()
    {
        if (occupiedCounterPoints == null)
            return false;

        for (int i = 0; i < counterPoints.Count; i++)
        {
            if (counterPoints[i] != null && occupiedCounterPoints[i] == null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the index of a randomly selected free counter point.
    /// Returns -1 if no valid points are available.
    /// </summary>
    private int GetRandomFreeCounterPoint()
    {
        freeCounterPoints.Clear();

        for (int i = 0; i < counterPoints.Count; i++)
        {
            if (counterPoints[i] != null && occupiedCounterPoints[i] == null)
            {
                freeCounterPoints.Add(i);
            }
        }

        if (freeCounterPoints.Count == 0)
            return -1;

        int randomIndex = Random.Range(0, freeCounterPoints.Count);

        return freeCounterPoints[randomIndex];
    }

    /// <summary>
    /// Returns the index of a randomly selected spawn point.
    /// Returns -1 if no valid points are available.
    /// </summary>
    private int GetRandomSpawnPoint()
    {
        freeSpawnPoints.Clear();

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i] != null)
            {
                freeSpawnPoints.Add(i);
            }
        }

        if (freeSpawnPoints.Count == 0)
            return -1;

        int randomIndex = Random.Range(0, freeSpawnPoints.Count);

        return freeSpawnPoints[randomIndex];
    }

    /// <summary>
    /// Checks if there is at least one valid spawn point.
    /// </summary>
    private bool HasValidSpawnPoint()
    {
        foreach (Transform point in spawnPoints)
        {
            if(point != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a customer at the specified spawn point, assigns a unique ID, and spawns it across the network.
    /// </summary>
    private void SpawnCustomer(int spawnIndex, int counterIndex)
    {
        if (!CanSpawn || !spawningEnabled)
            return;
        
        if(spawnIndex < 0 || spawnIndex >= spawnPoints.Count)
            return;
        
        if(counterIndex < 0 || counterIndex >= counterPoints.Count)
            return;

        Transform spawnPoint = spawnPoints[spawnIndex];
        Transform counterPoint = counterPoints[counterIndex];

        if(spawnPoint == null || counterPoint == null)
            return;
        
        if(occupiedCounterPoints[counterIndex] != null)
            return;
        
        // Create the order before spawning the customer.
        if (orderManager == null || !orderManager.TryCreateOrder(out DrinkOrder order))
            return;

        CustomerNPC customer = Instantiate(customerPrefab, spawnPoint.position, spawnPoint.rotation);

        int customerId = nextCustomerId++;

        customer.Initialize(this, orderManager, customerId, counterIndex, spawnPoint, counterPoint, order);

        occupiedCounterPoints[counterIndex] = customer;

        customer.NetworkObject.Spawn(true);

        Debug.Log($"[CustomerSpawner] Created customer {customerId} " + $"at {spawnPoint.name}. " + $"NetworkObjectId: {customer.NetworkObjectId}", this);
    }

    /// <summary>
    /// Releases the spawn point occupied by a despawned customer.
    /// Schedules a new spawn if the server is still active.
    /// </summary>
    public void OnCustomerDespawned(CustomerNPC customer, int counterIndex)
    {
        if (occupiedCounterPoints == null)
            return;

        if (counterIndex < 0 || counterIndex >= occupiedCounterPoints.Length)
            return;

        if (occupiedCounterPoints[counterIndex] != customer)
            return;

        occupiedCounterPoints[counterIndex] = null;

        Debug.Log($"[CustomerSpawner] Customer {customer.CustomerId} " + $"left counter point {counterIndex}.", this);

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