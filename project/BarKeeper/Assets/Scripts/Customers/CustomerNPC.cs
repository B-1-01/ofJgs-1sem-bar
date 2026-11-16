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
    public int SpawnPointIndex => spawnPointIndex;
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

        // Schedule departure if the order has ended.
        if (newStatus == OrderStatus.Completed ||
            newStatus == OrderStatus.Cancelled)
        {
            departureCoroutine = StartCoroutine(LeaveAfterDelay());
        }
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