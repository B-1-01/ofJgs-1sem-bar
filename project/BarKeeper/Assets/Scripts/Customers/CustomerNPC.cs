using System;
using Unity.Netcode;
using UnityEngine;
using System.Collections;

/// <summary>
/// Represents a networked customer NPC, managing its unique identification, spawn point assignment, and server-authoritative despawn.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class CustomerNPC : NetworkBehaviour
{
    private int customerId;
    private int spawnPointIndex = -1;
    private CustomerSpawner spawner;

    [Header("Customer Departure")]
    [SerializeField, Min(0.1f)]
    private float departureDelay = 3f;

    [Header("Order Patience (Seconds)")]
    [SerializeField, Min(0.1f)]
    private float pickupPatience = 60f;

    [SerializeField, Min(0.1f)]
    private float deliveryPatience = 60f;

    private int orderId = -1;
    private string recipeId = "";

    private DrinkOrderManager orderManager;
    private Coroutine departureCoroutine;

    private readonly NetworkVariable<OrderStatus> orderStatus = new(
        OrderStatus.Pending,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Stores the synchronized state of a customer's active patience timer.
    /// </summary>
    private struct PatienceTimerState : INetworkSerializable, IEquatable<PatienceTimerState>
    {
        public bool IsActive;
        public OrderStatus Phase;
        public double StartTime;
        public float Duration;

        public double EndTime => StartTime + Duration;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsActive);
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref StartTime);
            serializer.SerializeValue(ref Duration);
        }

        public bool Equals(PatienceTimerState other)
        {
            return IsActive == other.IsActive &&
                   Phase == other.Phase &&
                   StartTime.Equals(other.StartTime) &&
                   Duration.Equals(other.Duration);
        }
    }

    // Synchronizes the active timer's phase, start time, and duration.
    private readonly NetworkVariable<PatienceTimerState> patienceTimer = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Tracks which patience milestones have already been logged locally.
    private bool timerStartLogged;
    private bool timerHalfLogged;
    private bool timerTenPercentLogged;
    private PatienceTimerState lastLoggedTimer;

    // Notifies local listeners when the customer's order status changes.
    public event Action<CustomerNPC, OrderStatus, OrderStatus> OrderStatusChanged;

    public int CustomerId => customerId;
    public int SpawnPointIndex => spawnPointIndex;
    public int OrderId => orderId;
    public string RecipeId => recipeId;
    public OrderStatus CurrentOrderStatus => orderStatus.Value;

    /// <summary>
    /// Returns the remaining patience time using the synchronized server clock.
    /// </summary>
    public float PatienceRemaining
    {
        get
        {
            PatienceTimerState timer = patienceTimer.Value;

            if (!IsSpawned || !timer.IsActive || timer.Phase != orderStatus.Value || NetworkManager == null)
                return 0f;

            return (float)Math.Max(0d, timer.EndTime - NetworkManager.ServerTime.Time
            );
        }
    }

    public float PatienceDuration => patienceTimer.Value.IsActive ? patienceTimer.Value.Duration : 0f;

    private void OnValidate()
    {
        pickupPatience = Mathf.Max(0.1f, pickupPatience);
        deliveryPatience = Mathf.Max(0.1f, deliveryPatience);
    }

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
    public void Initialize(CustomerSpawner customerSpawner, DrinkOrderManager manager, int id, int pointIndex, DrinkOrder order)
    {
        if (IsSpawned)
        {
            Debug.LogError("[CustomerNPC] Cannot initialize an already spawned NPC.", this);
            return;
        }

        if (manager == null || order == null || order.Recipe == null)
        {
            Debug.LogError("[CustomerNPC] Invalid order or order manager.", this);
            return;
        }

        spawner = customerSpawner;
        customerId = id;
        spawnPointIndex = pointIndex;
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
        serializer.SerializeValue(ref spawnPointIndex);
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

        orderStatus.OnValueChanged += OnOrderStatusChanged;

        Debug.Log(
            $"[CustomerNPC] Spawned | " +
            $"CustomerId: {CustomerId} | " +
            $"NetworkObjectId: {NetworkObjectId} | " +
            $"SpawnPoint: {SpawnPointIndex} | " +
            $"Position: {transform.position}",
            this);

        if (orderId <= 0 || string.IsNullOrEmpty(recipeId))
        {
            Debug.LogError("[CustomerNPC] Customer has no valid order.", this);
            return;
        }

        PrintOrder();

        // Start the pickup timer when the customer receives its order.
        if (IsServer)
            StartPatienceTimer(OrderStatus.Pending);
    }

    /// <summary>
    /// Monitors patience milestones on each instance.
    /// Only the server can expire an order.
    /// </summary>
    private void Update()
    {
        if (!IsSpawned || NetworkManager == null)
            return;

        PatienceTimerState timer = patienceTimer.Value;

        if (!timer.IsActive || timer.Phase != orderStatus.Value)
            return;

        double remaining = Math.Max(0d, timer.EndTime - NetworkManager.ServerTime.Time);

        // Reset local log milestones whenever a new timer begins.
        if (!timerStartLogged || !timer.Equals(lastLoggedTimer))
        {
            lastLoggedTimer = timer;
            timerStartLogged = true;
            timerHalfLogged = false;
            timerTenPercentLogged = false;

            LogPatience(timer, $"Started ({timer.Duration:0.##}s) | Remaining: {remaining:0.##}s");
        }

        if (!timerHalfLogged && remaining <= timer.Duration * 0.5d)
        {
            timerHalfLogged = true;

            LogPatience(timer, $"50% | Remaining: {remaining:0.##}s");
        }

        if (!timerTenPercentLogged && remaining <= timer.Duration * 0.1d)
        {
            timerTenPercentLogged = true;

            LogPatience(timer, $"10% | Remaining: {remaining:0.##}s");
        }

        // Only the server can cancel an expired order.
        if (IsServer && remaining <= 0d)
            ExpireOrder();
    }

    /// <summary>
    /// Starts the patience timer for the specified order phase.
    /// </summary>
    private void StartPatienceTimer(OrderStatus phase)
    {
        if (!IsServer || NetworkManager == null)
            return;

        float duration = phase == OrderStatus.Pending
            ? pickupPatience
            : deliveryPatience;

        patienceTimer.Value = new PatienceTimerState
        {
            IsActive = true,
            Phase = phase,
            StartTime = NetworkManager.ServerTime.Time,
            Duration = duration
        };
    }

    /// <summary>
    /// Stops the currently active patience timer.
    /// </summary>
    private void StopPatienceTimer()
    {
        if (IsServer)
            patienceTimer.Value = default;
    }

    /// <summary>
    /// Cancels the customer's order when its patience expires.
    /// </summary>
    private void ExpireOrder()
    {
        if (!IsServer || orderManager == null)
            return;

        OrderStatus currentStatus = orderStatus.Value;
        PatienceTimerState timer = patienceTimer.Value;

        if (!timer.IsActive || timer.Phase != currentStatus || NetworkManager.ServerTime.Time < timer.EndTime)
            return;

        if (!orderManager.TryChangeOrderStatus(orderId, currentStatus, OrderStatus.Cancelled))
            return;

        LogPatience(timer, "Expired | Status: Cancelled");

        FinishOrder(OrderStatus.Cancelled);
    }

    /// <summary>
    /// Applies an accepted order status change and handles its next phase.
    /// </summary>
    private void FinishOrder(OrderStatus newStatus)
    {
        orderStatus.Value = newStatus;

        if (newStatus == OrderStatus.InPreparation)
        {
            // Collection completed. Start a fresh delivery timer.
            StartPatienceTimer(OrderStatus.InPreparation);
        }
        else if (newStatus == OrderStatus.Completed || newStatus == OrderStatus.Cancelled)
        {
            StopPatienceTimer();

            if (departureCoroutine == null)
                departureCoroutine = StartCoroutine(LeaveAfterDelay());
        }
    }

    /// <summary>
    /// Logs a patience milestone with customer, order, and phase identification.
    /// </summary>
    private void LogPatience(PatienceTimerState timer, string message)
    {
        string phase = timer.Phase == OrderStatus.Pending ? "Pickup" : "Delivery";

        Debug.Log($"[Patience] Customer {customerId} | " + $"Order {orderId} | {phase} | {message}", this);
    }

    /// <summary>
    /// Notifies the spawner when the customer is despawned, allowing the server to free the occupied spawn point.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        orderStatus.OnValueChanged -= OnOrderStatusChanged;

        if (departureCoroutine != null)
        {
            StopCoroutine(departureCoroutine);
            departureCoroutine = null;
        }

        Debug.Log("[CustomerNPC] Customer " + customerId + " despawned.", this);

        if (IsServer && orderManager != null)
        {
            orderManager.RemoveOrder(orderId);
        }

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

        // Reject interactions received after the patience deadline.
        // Expiration takes priority even if the server has not updated this frame.
        PatienceTimerState timer = patienceTimer.Value;

        if (timer.IsActive && timer.Phase == expectedStatus && NetworkManager.ServerTime.Time >= timer.EndTime)
        {
            ExpireOrder();
            return;
        }

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

        // Apply the status change and manage the corresponding patience phase.
        FinishOrder(newStatus);
    }

    /// <summary>
    /// Removes the customer after a configurable delay.
    /// </summary>
    private IEnumerator LeaveAfterDelay()
    {
        yield return new WaitForSeconds(departureDelay);

        departureCoroutine = null;

        DespawnCustomer();
    }

    /// <summary>
    /// Called when the synchronized order status changes.
    /// </summary>
    private void OnOrderStatusChanged(OrderStatus previousStatus, OrderStatus newStatus)
    {
        PrintOrder();

        OrderStatusChanged?.Invoke(this, previousStatus, newStatus);
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