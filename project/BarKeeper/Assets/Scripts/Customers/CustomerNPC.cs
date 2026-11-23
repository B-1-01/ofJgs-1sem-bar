using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Unity.VisualScripting;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;

/// <summary>
/// Represents a networked customer NPC, managing its unique identification, spawn point assignment, and server-authoritative despawn.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NavMeshAgent))]
public class CustomerNPC : NetworkBehaviour
{
    private int customerId;
    private int counterPointIndex = -1;
    private CustomerSpawner spawner;

    [Header("Customer Movement")]
    [SerializeField, Min(0.1f)]
    private float movementSpeed = 4f;
    [SerializeField, Min(0.1f)]
    private float stoppingDistance = 0.5f;
    private NavMeshAgent agent;
    private Transform targetCounterPoint;
    private Transform spawnPoint;
    private Coroutine movementCoroutine;

    [Header("Customer Timeouts")]
    [SerializeField, Min(1f)]
    private float serviceTimeout = 60f;
    [SerializeField, Min(1f)]
    private float orderWaitTimeout = 60f;

    private Coroutine timeoutCoroutine;

    [Header("Customer Consumption")]
    [SerializeField, Min(0f)]
    private float consumptionTime = 10f;

    [Header("Customer Departure")]
    [SerializeField, Min(0.1f)]
    private float departureDelay = 3f;

    private int orderId = -1;
    private string recipeId = "";

    private DrinkOrderManager orderManager;
    private Coroutine departureCoroutine;

    private readonly NetworkVariable<OrderStatus> orderStatus = new(
        OrderStatus.Pending,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int CustomerId => customerId;
    public int CounterPointIndex => counterPointIndex;
    public int OrderId => orderId;
    public string RecipeId => recipeId;
    public OrderStatus CurrentOrderStatus => orderStatus.Value;

    /// <summary>
    /// Returns the recipe associated with this customer's order.
    /// </summary>
    public DrinkRecipeDefinition GetRecipe()
    {
        if (orderManager == null)
            return null;

        return orderManager.FindRecipeById(recipeId);
    }

    /// <summary>
    /// Initializes the customer with a unique ID and spawn point.
    /// Must be called by the server before spawning the NetworkObject.
    /// </summary>
    public void Initialize(CustomerSpawner customerSpawner, DrinkOrderManager manager, int id, int counterIndex, Transform spawnPoint,Transform counterPoint, DrinkOrder order)
    {
        if (IsSpawned)
        {
            Debug.LogError("[CustomerNPC] Cannot initialize an already spawned NPC.", this);
            return;
        }

        if (manager == null || order == null || order.Recipe == null || counterPoint == null || spawnPoint == null)
        {
            Debug.LogError("[CustomerNPC] Invalid order or initialization data.", this);
            return;
        }

        spawner = customerSpawner;
        customerId = id;
        counterPointIndex = counterIndex;
        this.spawnPoint = spawnPoint;
        targetCounterPoint = counterPoint;
        orderManager = manager;
        orderId = order.OrderId;
        recipeId = order.Recipe.RecipeId;
    }

    /// <summary>
    /// Synchronizes the customer's initial data with connected clients, including players who join after the customer has spawned.
    /// </summary>
    protected override void OnSynchronize<T>(
        ref BufferSerializer<T> serializer)
    {
        serializer.SerializeValue(ref customerId);
        serializer.SerializeValue(ref counterPointIndex);
        serializer.SerializeValue(ref orderId);
        serializer.SerializeValue(ref recipeId);

        base.OnSynchronize(ref serializer);
    }

    /// <summary>
    /// Called when the customer is spawned on the network.
    /// Logs its identification and spawn information for debugging.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (orderManager == null)
        {
            orderManager = FindFirstObjectByType<DrinkOrderManager>();
        }

        agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.speed = movementSpeed;
            agent.stoppingDistance = stoppingDistance;

            if (!IsServer)
            {
                agent.enabled = false;
            }
        }

        orderStatus.OnValueChanged += OnOrderStatusChanged;

        Debug.Log(
            $"[CustomerNPC] Spawned | " +
            $"CustomerId: {CustomerId} | " +
            $"NetworkObjectId: {NetworkObjectId} | " +
            $"CounterPoint: {CounterPointIndex} | " +
            $"Position: {transform.position}",
            this);

        if (orderId <= 0 || string.IsNullOrEmpty(recipeId))
        {
            Debug.LogError("[CustomerNPC] Customer has no valid order.", this);
            return;
        }

        if (IsServer)
        {
            StartMovement();
        }

        PrintOrder();
    }

    /// <summary>
    /// It will start the movement of the customer to the target point.
    /// </summary>
    private void StartMovement()
    {
        if(!IsServer || agent == null || !agent.enabled)
            return;
        
        if(targetCounterPoint == null)
        {
            Debug.LogError($"[CustomerNPC] Customer {customerId} has no target CounterPoint.", this);
            return;
        }

        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }

        movementCoroutine = StartCoroutine(MoveToCounterPoint());
    }

    /// <summary>
    /// It will make the customer move to the target point.
    /// </summary>
    private IEnumerator MoveToCounterPoint()
    {
        yield return null;

        if(!IsServer || agent == null || !agent.enabled)
        {
            movementCoroutine = null;
            yield break;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError($"[CustomerNPC] Customer {customerId} is not on the NavMesh.", this);
            movementCoroutine = null;
            yield break;
        }

        agent.isStopped = false;

        if (!agent.SetDestination(targetCounterPoint.position))
        {
            Debug.LogWarning($"[CustomerNPC] Could not set destination for customer.", this);
            movementCoroutine = null;
            yield break;
        }

        while (agent.pathPending)
        {
            yield return null;
        }

        if(agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            Debug.LogWarning($"[CustomerNPC] Customer {customerId} doesn't have a complete path to counter.", this);
            movementCoroutine = null;
            yield break;
        }

        while(agent.enabled && agent.isOnNavMesh && agent.remainingDistance > agent.stoppingDistance)
        {
            yield return null;
        }

        if(agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        movementCoroutine = null;

        Debug.Log($"[CustomerNPC] Customer {customerId} arrived at the counter.", this);

        if (orderStatus.Value == OrderStatus.Pending)
        {
            timeoutCoroutine = StartCoroutine(WaitForServiceTimeout());
        }
    }

    /// <summary>
    /// Customer waiting for service goes away.
    /// </summary>
    private IEnumerator WaitForServiceTimeout()
    {
        Debug.Log($"[CustomerNPC] Customer {customerId} is waiting to be served.", this);

        yield return new WaitForSeconds(serviceTimeout);

        timeoutCoroutine = null;

        if(IsServer && orderStatus.Value == OrderStatus.Pending)
        {
            Debug.Log($"[CustomerNPC] Customer {customerId} left because nobody served them.", this);

            orderStatus.Value = OrderStatus.Cancelled;

            departureCoroutine = StartCoroutine(ReturnToSpawn());
        }
    }

    /// <summary>
    /// Customer waiting for order goes away.
    /// </summary>
    private IEnumerator WaitForOrderTimeout()
    {
        Debug.Log($"[CustomerNPC] Customer {customerId} is waiting for the order.", this);

        yield return new WaitForSeconds(orderWaitTimeout);

        timeoutCoroutine = null;

        if(IsServer && orderStatus.Value == OrderStatus.InPreparation)
        {
            Debug.Log($"[CustomerNPC] Customer {customerId} left because the order took too long.", this);

            orderStatus.Value = OrderStatus.Cancelled;

            departureCoroutine = StartCoroutine(ReturnToSpawn());
        }
    }

    private void StopTimeoutCoroutine()
    {
        if(timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }
    }

    /// <summary>
    /// Notifies the spawner when the customer is despawned, allowing the server to free the occupied spawn point.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        orderStatus.OnValueChanged -= OnOrderStatusChanged;

        StopTimeoutCoroutine();

        if (departureCoroutine != null)
        {
            StopCoroutine(departureCoroutine);
            departureCoroutine = null;
        }

        if(movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }

        if(agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        Debug.Log("[CustomerNPC] Customer " + customerId + " despawned.", this);

        if (IsServer && orderManager != null)
        {
            orderManager.RemoveOrder(orderId);
        }

        if (IsServer && spawner != null)
        {
            spawner.OnCustomerDespawned(this, counterPointIndex);
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
    /// Requests the next order status when the player left-clicks.
    /// </summary>
    public void RequestAdvanceOrder(FPSPlayerController player)
    {
        RequestOrderAction(player, false);
    }

    /// <summary>
    /// Requests order cancellation when the player right-clicks.
    /// </summary>
    public void RequestCancelOrder(FPSPlayerController player)
    {
        RequestOrderAction(player, true);
    }

    /// <summary>
    /// Sends the player's order action to the server.
    /// </summary>
    private void RequestOrderAction(FPSPlayerController player, bool cancelOrder)
    {
        if (!IsSpawned || player == null)
            return;

        if (!player.IsSpawned || !player.IsOwner)
            return;

        OrderStatus currentStatus = orderStatus.Value;

        if (currentStatus != OrderStatus.Pending &&
            currentStatus != OrderStatus.InPreparation)
            return;

        RequestOrderActionRpc(
            player.NetworkObjectId,
            orderId,
            currentStatus,
            cancelOrder);
    }

    /// <summary>
    /// Validates the player and updates the order on the server.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void RequestOrderActionRpc(
        ulong playerObjectId,
        int requestedOrderId,
        OrderStatus expectedStatus,
        bool cancelOrder,
        RpcParams rpcParams = default)
    {
        if (!IsServer || !IsSpawned || orderManager == null)
            return;

        // Check that the order has not changed.
        if (requestedOrderId != orderId ||
            expectedStatus != orderStatus.Value)
            return;

        // Find the player that sent the request.
        NetworkObject playerObject;

        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
            playerObjectId, out playerObject))
            return;

        FPSPlayerController player =
            playerObject.GetComponent<FPSPlayerController>();

        if (player == null || !player.IsSpawned)
            return;

        // Make sure the player owns the requesting object.
        if (player.OwnerClientId != rpcParams.Receive.SenderClientId)
            return;

        // Check interaction distance.
        float maxDistance = player.InteractRange;

        float distanceSquared =
            (player.transform.position - transform.position).sqrMagnitude;

        if (distanceSquared > maxDistance * maxDistance + 2f)
            return;

        // Determine the next order status.
        OrderStatus newStatus;

        if (cancelOrder)
        {
            newStatus = OrderStatus.Cancelled;
        }
        else if (expectedStatus == OrderStatus.Pending)
        {
            newStatus = OrderStatus.InPreparation;
        }
        else if (expectedStatus == OrderStatus.InPreparation)
        {
            newStatus = OrderStatus.Completed;
        }
        else
        {
            return;
        }

        // Ask the order manager to validate the change.
        bool changed = orderManager.TryChangeOrderStatus(
            orderId,
            expectedStatus,
            newStatus);

        if (!changed)
            return;

        // Synchronize the new status.
        orderStatus.Value = newStatus;

        StopTimeoutCoroutine();

        if(newStatus == OrderStatus.InPreparation)
        {
            timeoutCoroutine = StartCoroutine(WaitForOrderTimeout());
        }
        else if (newStatus == OrderStatus.Completed)
        {
            departureCoroutine = StartCoroutine(ConsumeAndLeave());
        }
        else if(newStatus == OrderStatus.Cancelled)
        {
            departureCoroutine = StartCoroutine(LeaveAfterDelay());
        }
    }

    /// <summary>
    /// Customer returns to spawn after a configurable delay.
    /// </summary>
    private IEnumerator LeaveAfterDelay()
    {
        yield return new WaitForSeconds(departureDelay);

        departureCoroutine = null;

        yield return StartCoroutine(ReturnToSpawn());
    }

    /// <summary>
    /// Customer returns to spawn after consuming the order with a configurable timeframe.
    /// </summary>
    private IEnumerator ConsumeAndLeave()
    {
        Debug.Log($"[CustomerNPC] Customer {customerId} is consuming the order for {consumptionTime} seconds.", this);

        yield return new WaitForSeconds(consumptionTime);

        departureCoroutine = null;

        yield return StartCoroutine(ReturnToSpawn());
    }

    private IEnumerator ReturnToSpawn()
    {
        if(!IsServer || agent == null || !agent.enabled)
        {
            DespawnCustomer();
            yield break;
        }

        if(spawnPoint == null)
        {
            Debug.LogWarning($"[CustomerNPC] Customer {customerId} has no spawnpoint to return to.", this);
            DespawnCustomer();
            yield break;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError($"[CustomerNPC] Customer {customerId} is not on the NavMesh.", this);
            DespawnCustomer();
            yield break;
        }

        Debug.Log($"[CustomerNPC] Customer {customerId} is returning to spawn.", this);

        agent.isStopped = false;

        if (!agent.SetDestination(spawnPoint.position))
        {
            Debug.LogWarning($"[CustomerNPC] Could not set return destination for customer.", this);
            DespawnCustomer();
            yield break;
        }

        while (agent.pathPending)
        {
            yield return null;
        }

        if(agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            Debug.LogWarning($"[CustomerNPC] Customer {customerId} doesn't have a complete path to spawn.", this);
            DespawnCustomer();
            yield break;
        }

        while(agent.enabled && agent.isOnNavMesh && agent.remainingDistance > agent.stoppingDistance)
        {
            yield return null;
        }

        if(agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        Debug.Log($"[CustomerNPC] Customer {customerId} arrived at spawn. Despawning.", this);

        DespawnCustomer();

    }

    /// <summary>
    /// Called when the synchronized order status changes.
    /// </summary>
    private void OnOrderStatusChanged(OrderStatus previousStatus, OrderStatus newStatus)
    {
        PrintOrder();
    }

    /// <summary>
    /// Prints the customer's order in the console.
    /// </summary>
    private void PrintOrder()
    {
        string recipeName = recipeId;

        DrinkRecipeDefinition recipe = GetRecipe();

        if (recipe != null)
        {
            recipeName = recipe.DisplayName;
        }

        Debug.Log(
            "[Order] Customer: " + customerId +
            " | Order: " + orderId +
            " | Recipe: " + recipeName +
            " (" + recipeId + ")" +
            " | Status: " + orderStatus.Value,
            this);
    }

}