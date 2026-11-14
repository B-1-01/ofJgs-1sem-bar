using System;
using UnityEngine;

/// <summary>
/// Represents the possible states of a customer drink order.
/// </summary>
public enum OrderStatus
{
    Pending, // Waiting for a player to collect the order.
    InPreparation, // Collected and waiting for delivery.
    Completed, // Drink accepted and payment received.
    Rejected, // Delivered drink did not meet the order requirements.
    Cancelled // Customer left before the order was completed.
}

/// <summary>
/// Stores the data of a single customer order.
/// Each order corresponds to one drink recipe.
/// </summary>
[Serializable]
public class DrinkOrder
{
    [SerializeField] private int orderId;
    [SerializeField] private DrinkRecipeDefinition recipe;
    [SerializeField] private OrderStatus status;

    public int OrderId => orderId;
    public DrinkRecipeDefinition Recipe => recipe;
    public OrderStatus Status => status;

    /// <summary>
    /// Creates a new order using an ID provided by the order manager.
    /// Orders start in the Pending state.
    /// </summary>
    public DrinkOrder(int orderId, DrinkRecipeDefinition recipe)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));

        this.orderId = orderId;
        this.recipe = recipe;
        status = OrderStatus.Pending;
    }

    /// <summary>
    /// Updates the order status.
    /// Status transitions are controlled by the order management system.
    /// </summary>
    public void SetStatus(OrderStatus newStatus)
    {
        status = newStatus;
    }
}