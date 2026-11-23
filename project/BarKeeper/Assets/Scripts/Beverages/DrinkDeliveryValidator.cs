using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Validates a drink against a recipe, including its glass type, liquid ingredients, foam, and quantity tolerances.
/// </summary>
public static class DrinkDeliveryValidator
{
    /// <summary>
    /// Checks that the container matches every requirement of the recipe.
    /// All quantities are expressed in milliliters.
    /// </summary>
    public static bool MatchesRecipe(
        DrinkContainer drink,
        DrinkRecipeDefinition recipe,
        float tolerancePercent,
        out string failureReason)
    {
        failureReason = "";

        if (drink == null || recipe == null)
        {
            failureReason = "Missing drink or recipe.";
            return false;
        }

        // The container must be the type required by the recipe.
        if (drink.GlassType != recipe.PreferredGlass)
        {
            failureReason = "Incorrect glass type.";
            return false;
        }

        if (drink.TotalVolume > drink.MaxCapacityMl + 0.01f)
        {
            failureReason = "Container exceeds its capacity.";
            return false;
        }

        Dictionary<IngredientType, float> requiredIngredients = new();

        // Build the requirements from the recipe.
        foreach (RecipeIngredient requirement in recipe.Ingredients)
        {
            if (requirement == null ||
                requirement.Ingredient == IngredientType.None ||
                requirement.Quantity <= 0f ||
                float.IsNaN(requirement.Quantity) ||
                float.IsInfinity(requirement.Quantity))
            {
                failureReason = "Invalid recipe ingredient.";
                return false;
            }

            if (requiredIngredients.ContainsKey(requirement.Ingredient))
            {
                failureReason = "Duplicate ingredient in recipe.";
                return false;
            }

            requiredIngredients.Add(
                requirement.Ingredient,
                requirement.Quantity);
        }

        if (requiredIngredients.Count == 0)
        {
            failureReason = "Recipe has no ingredients.";
            return false;
        }

        Dictionary<IngredientType, float> actualIngredients = new();

        // Validate every liquid currently stored in the container.
        foreach (PouredIngredient poured in drink.Ingredients)
        {
            if (poured.Ingredient == IngredientType.None ||
                poured.Ingredient == IngredientType.BeerFoam ||
                poured.AmountMl <= 0f ||
                float.IsNaN(poured.AmountMl) ||
                float.IsInfinity(poured.AmountMl))
            {
                failureReason = "Invalid liquid ingredient.";
                return false;
            }

            // Extra ingredients invalidate the entire drink.
            if (!requiredIngredients.ContainsKey(poured.Ingredient))
            {
                failureReason = $"Unexpected ingredient: {poured.Ingredient}.";
                return false;
            }

            if (actualIngredients.TryGetValue(
                poured.Ingredient, out float existingAmount))
            {
                actualIngredients[poured.Ingredient] =
                    existingAmount + poured.AmountMl;
            }
            else
            {
                actualIngredients.Add(
                    poured.Ingredient,
                    poured.AmountMl);
            }
        }

        // Foam is stored separately from the liquid ingredient list.
        float foamAmount = drink.FoamAmount;

        if (float.IsNaN(foamAmount) ||
            float.IsInfinity(foamAmount) ||
            foamAmount < 0f)
        {
            failureReason = "Invalid foam amount.";
            return false;
        }

        if (foamAmount > 0f)
        {
            if (!requiredIngredients.ContainsKey(IngredientType.BeerFoam))
            {
                failureReason = "Unexpected beer foam.";
                return false;
            }

            actualIngredients.Add(IngredientType.BeerFoam, foamAmount);
        }

        // Apply the percentage tolerance separately to every ingredient.
        float tolerance = Mathf.Clamp(tolerancePercent, 0f, 100f) / 100f;

        foreach (KeyValuePair<IngredientType, float> requirement
            in requiredIngredients)
        {
            if (!actualIngredients.TryGetValue(
                requirement.Key, out float actualAmount))
            {
                failureReason = $"Missing ingredient: {requirement.Key}.";
                return false;
            }

            float expectedAmount = requirement.Value;
            float allowedDifference = expectedAmount * tolerance;

            if (float.IsNaN(actualAmount) ||
                float.IsInfinity(actualAmount) ||
                Mathf.Abs(actualAmount - expectedAmount) >
                    allowedDifference + 0.0001f)
            {
                failureReason =
                    $"Incorrect {requirement.Key} amount: " +
                    $"{actualAmount:0.##} ml, expected " +
                    $"{expectedAmount:0.##} ml.";
                return false;
            }
        }

        return true;
    }
}