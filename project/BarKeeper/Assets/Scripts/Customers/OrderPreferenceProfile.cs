using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the selection weight of one drink recipe.
/// </summary>
[Serializable]
public class RecipePreference
{
    [SerializeField] private DrinkRecipeDefinition recipe;

    [SerializeField, Min(0)]
    private int weight = 1;

    public DrinkRecipeDefinition Recipe
    {
        get { return recipe; }
    }

    public int Weight
    {
        get { return weight; }
    }
}

/// <summary>
/// Defines which drinks a customer prefers.
/// Different personalities can use different profiles.
/// </summary>
[CreateAssetMenu(
    fileName = "OrderPreferences_",
    menuName = "Barkeeper/Orders/Preference Profile")]
public class OrderPreferenceProfile : ScriptableObject
{
    [Header("Default Weight")]
    [SerializeField, Min(0)]
    private int defaultWeight = 1;

    [Header("Recipe Preferences")]
    [SerializeField]
    private List<RecipePreference> preferences = new List<RecipePreference>();

    /// <summary>
    /// Returns the weight of a recipe.
    /// Uses the default weight if the recipe is not listed.
    /// </summary>
    public int GetWeight(DrinkRecipeDefinition recipe)
    {
        if (recipe == null)
        {
            return 0;
        }

        foreach (RecipePreference preference in preferences)
        {
            if (preference == null)
            {
                continue;
            }

            if (preference.Recipe == recipe)
            {
                return Mathf.Max(0, preference.Weight);
            }
        }

        return Mathf.Max(0, defaultWeight);
    }
}
