using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Creates and manages customer orders.
/// Only the server can create or modify orders.
/// </summary>
public class DrinkOrderManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DrinkRecipeManager recipeManager;

    [Header("Default Preferences")]
    [SerializeField] private OrderPreferenceProfile defaultPreferenceProfile;

    private Dictionary<int, DrinkOrder> activeOrders =
        new Dictionary<int, DrinkOrder>();

    private int nextOrderId = 1;

    private NetworkManager networkManager;

    // Notifies the customer spawner when available recipes change.
    public event Action EligibleRecipesChanged;

    // Notifies server-side systems when an order changes status.
    public event Action<int, OrderStatus, OrderStatus> OrderStatusChanged;

    private void OnEnable()
    {
        if (recipeManager == null)
        {
            recipeManager = FindFirstObjectByType<DrinkRecipeManager>();
        }

        if (recipeManager != null)
        {
            recipeManager.AvailableRecipesChanged += OnRecipesChanged;
        }
    }

    private void Start()
    {
        networkManager = NetworkManager.Singleton;

        if (networkManager != null)
        {
            networkManager.OnServerStopped += OnServerStopped;
        }

        if (recipeManager == null)
        {
            Debug.LogError("[DrinkOrderManager] Recipe manager missing.");
        }

        if (defaultPreferenceProfile == null)
        {
            Debug.LogError("[DrinkOrderManager] Default preference profile missing.");
        }
    }

    private void OnDisable()
    {
        if (recipeManager != null)
        {
            recipeManager.AvailableRecipesChanged -= OnRecipesChanged;
        }
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnServerStopped -= OnServerStopped;
        }
    }

    /// <summary>
    /// Informs other systems that recipe availability changed.
    /// </summary>
    private void OnRecipesChanged()
    {
        if (EligibleRecipesChanged != null)
        {
            EligibleRecipesChanged();
        }
    }

    /// <summary>
    /// Checks whether the server is running.
    /// </summary>
    private bool IsServerActive()
    {
        if (NetworkManager.Singleton == null)
        {
            return false;
        }

        return NetworkManager.Singleton.IsServer;
    }

    /// <summary>
    /// Checks whether a recipe has a valid and unique ID.
    /// </summary>
    private bool IsRecipeValid(DrinkRecipeDefinition recipe)
    {
        if (recipe == null || recipeManager == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(recipe.RecipeId))
        {
            return false;
        }

        // Different recipes cannot have the same ID.
        foreach (DrinkRecipeDefinition other in recipeManager.AllRecipes)
        {
            if (other == null || other == recipe)
            {
                continue;
            }

            if (other.RecipeId == recipe.RecipeId)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if there is at least one recipe that can be ordered.
    /// </summary>
    public bool HasEligibleRecipes(OrderPreferenceProfile profile = null)
    {
        if (profile == null)
        {
            profile = defaultPreferenceProfile;
        }

        if (recipeManager == null || profile == null)
        {
            return false;
        }

        foreach (DrinkRecipeDefinition recipe in recipeManager.AvailableRecipes)
        {
            if (!IsRecipeValid(recipe))
            {
                continue;
            }

            if (profile.GetWeight(recipe) > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Selects a random recipe based on its selection weight.
    /// Returns null if no valid recipe can be selected.
    /// </summary>
    private DrinkRecipeDefinition SelectRandomRecipe(OrderPreferenceProfile profile)
    {
        if (recipeManager == null || profile == null)
        {
            return null;
        }

        int totalWeight = 0;

        // Step 1: Add the weights of all valid recipes.
        foreach (DrinkRecipeDefinition recipe in recipeManager.AvailableRecipes)
        {
            if (!IsRecipeValid(recipe))
            {
                continue;
            }

            int weight = profile.GetWeight(recipe);

            if (weight > 0)
            {
                totalWeight += weight;
            }
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        // Step 2: Pick a random number inside the total weight.
        int randomNumber = UnityEngine.Random.Range(0, totalWeight);

        // Step 3: Find the recipe corresponding to that number.
        foreach (DrinkRecipeDefinition recipe in recipeManager.AvailableRecipes)
        {
            if (!IsRecipeValid(recipe))
            {
                continue;
            }

            int weight = profile.GetWeight(recipe);

            if (weight <= 0)
            {
                continue;
            }

            if (randomNumber < weight)
            {
                return recipe;
            }

            randomNumber -= weight;
        }

        return null;
    }

    /// <summary>
    /// Creates a new order with a unique ID and a selected recipe.
    /// </summary>
    public bool TryCreateOrder(
        out DrinkOrder order,
        OrderPreferenceProfile profile = null)
    {
        order = null;

        if (!IsServerActive())
        {
            return false;
        }

        if (profile == null)
        {
            profile = defaultPreferenceProfile;
        }

        DrinkRecipeDefinition recipe = SelectRandomRecipe(profile);

        if (recipe == null)
        {
            return false;
        }

        int orderId = nextOrderId;
        nextOrderId++;

        order = new DrinkOrder(orderId, recipe);

        activeOrders.Add(orderId, order);

        return true;
    }

    /// <summary>
    /// Checks whether a status transition is allowed.
    /// </summary>
    private bool IsValidStatusChange(
        OrderStatus oldStatus,
        OrderStatus newStatus)
    {
        if (oldStatus == OrderStatus.Pending)
        {
            if (newStatus == OrderStatus.InPreparation ||
                newStatus == OrderStatus.Cancelled)
            {
                return true;
            }
        }

        if (oldStatus == OrderStatus.InPreparation)
        {
            if (newStatus == OrderStatus.Completed ||
                newStatus == OrderStatus.Rejected ||
                newStatus == OrderStatus.Cancelled)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Changes an order's status if the requested transition is valid.
    /// </summary>
    public bool TryChangeOrderStatus(
        int orderId,
        OrderStatus expectedStatus,
        OrderStatus newStatus)
    {
        if (!IsServerActive())
        {
            return false;
        }

        DrinkOrder order;

        if (!activeOrders.TryGetValue(orderId, out order))
        {
            return false;
        }

        // The order must still have the status the player saw.
        if (order.Status != expectedStatus)
        {
            return false;
        }

        if (!IsValidStatusChange(expectedStatus, newStatus))
        {
            return false;
        }

        order.SetStatus(newStatus);

        // Notify interested systems of the accepted status change.
        OrderStatusChanged?.Invoke(orderId, expectedStatus, newStatus);

        return true;
    }

    /// <summary>
    /// Finds an active order using its ID.
    /// </summary>
    public bool TryGetOrder(int orderId, out DrinkOrder order)
    {
        return activeOrders.TryGetValue(orderId, out order);
    }

    /// <summary>
    /// Finds a recipe using its ID.
    /// Returns null if different recipes share that ID.
    /// </summary>
    public DrinkRecipeDefinition FindRecipeById(string id)
    {
        if (recipeManager == null || string.IsNullOrEmpty(id))
        {
            return null;
        }

        DrinkRecipeDefinition foundRecipe = null;

        foreach (DrinkRecipeDefinition recipe in recipeManager.AllRecipes)
        {
            if (recipe == null || recipe.RecipeId != id)
            {
                continue;
            }

            if (foundRecipe != null && foundRecipe != recipe)
            {
                return null;
            }

            foundRecipe = recipe;
        }

        return foundRecipe;
    }

    /// <summary>
    /// Removes an order after its customer leaves.
    /// </summary>
    public void RemoveOrder(int orderId)
    {
        if (!IsServerActive())
        {
            return;
        }

        activeOrders.Remove(orderId);
    }

    /// <summary>
    /// Clears the order data when the server shuts down.
    /// </summary>
    private void OnServerStopped(bool wasHost)
    {
        activeOrders.Clear();
        nextOrderId = 1;
    }
}