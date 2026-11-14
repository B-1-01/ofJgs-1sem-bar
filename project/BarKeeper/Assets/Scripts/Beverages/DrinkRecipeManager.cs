using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the game's drink recipes and determines which
/// recipes are currently available based on player unlocks.
/// </summary>
public class DrinkRecipeManager : MonoBehaviour
{
    // Complete list of recipes, configured in the Inspector.
    [Header("All Recipes")]
    [SerializeField]
    private List<DrinkRecipeDefinition> allRecipes = new();

    // Runtime list containing only unlocked recipes.
    [SerializeField]
    private List<DrinkRecipeDefinition> availableRecipes = new();

    [Header("Unlocked Ingredients")]
    [SerializeField]
    private List<IngredientType> unlockedIngredients = new();

    [Header("Unlocked Stations")]
    [SerializeField]
    private List<PreparationStation> unlockedStations = new();

    public IReadOnlyList<DrinkRecipeDefinition> AllRecipes => allRecipes;
    public IReadOnlyList<DrinkRecipeDefinition> AvailableRecipes => availableRecipes;
    public IReadOnlyList<IngredientType> UnlockedIngredients => unlockedIngredients;
    public IReadOnlyList<PreparationStation> UnlockedStations => unlockedStations;

    // Notifies other systems when recipe availability is updated.
    public event Action AvailableRecipesChanged;

    private void Awake()
    {
        RefreshAvailableRecipes();
    }

    public void UnlockIngredient(IngredientType ingredient)
    {
        if (ingredient == IngredientType.None ||
            unlockedIngredients.Contains(ingredient))
            return;

        unlockedIngredients.Add(ingredient);
        RefreshAvailableRecipes();
    }

    public void UnlockStation(PreparationStation station)
    {
        if (unlockedStations.Contains(station))
            return;

        unlockedStations.Add(station);
        RefreshAvailableRecipes();
    }

    /// <summary>
    /// Rebuilds the available recipe list using the ingredients
    /// and preparation stations currently unlocked by the players.
    /// Ingredient stock quantities are not considered.
    /// </summary>
    public void RefreshAvailableRecipes()
    {
        HashSet<IngredientType> ingredients = new(unlockedIngredients);
        HashSet<PreparationStation> stations = new(unlockedStations);

        availableRecipes.Clear();

        foreach (DrinkRecipeDefinition recipe in allRecipes)
        {
            if (recipe == null)
                continue;

            if (availableRecipes.Contains(recipe))
                continue;

            if (!stations.Contains(recipe.Station))
                continue;

            if (recipe.Ingredients.Count == 0)
                continue;

            bool canPrepare = true;

            foreach (RecipeIngredient requirement in recipe.Ingredients)
            {
                if (requirement == null ||
                    requirement.Ingredient == IngredientType.None ||
                    requirement.Quantity <= 0f ||
                    !ingredients.Contains(requirement.Ingredient))
                {
                    canPrepare = false;
                    break;
                }
            }

            if (canPrepare)
                availableRecipes.Add(recipe);
        }

        AvailableRecipesChanged?.Invoke();
    }

    /// <summary>
    /// Checks whether a recipe is currently available to players.
    /// </summary>
    public bool IsRecipeAvailable(DrinkRecipeDefinition recipe)
    {
        return recipe != null && availableRecipes.Contains(recipe);
    }
}