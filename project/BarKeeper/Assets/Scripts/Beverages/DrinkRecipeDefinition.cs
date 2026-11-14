using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the station where a drink must be prepared.
/// </summary>
public enum PreparationStation
{
    BeerTap,
    Cocktails
}

/// <summary>
/// Defines how a drink is prepared at its station.
/// </summary>
public enum PreparationMethod
{
    DirectPour,
    Shaker,
    TapPour
}

/// <summary>
/// Represents an ingredient and the quantity required by a recipe.
/// </summary>
[Serializable]
public class RecipeIngredient
{
    [SerializeField] private IngredientType ingredient;

    [Min(0f)]
    [SerializeField] private float quantity;

    public IngredientType Ingredient => ingredient;
    public float Quantity => quantity;
}


/// <summary>
/// Stores the configuration of a drink recipe.
/// Recipes are created as ScriptableObjects and shared between
/// the recipe book, order system, and drink evaluation system.
/// </summary>
[CreateAssetMenu(
    fileName = "Recipe_",
    menuName = "Barkeeper/Beverages/Drink Recipe")]
public class DrinkRecipeDefinition : ScriptableObject
{
    [Header("Identification")]
    [SerializeField] private string recipeId;
    [SerializeField] private string displayName;

    [Header("Ingredients")]
    [SerializeField]
    private List<RecipeIngredient> ingredients = new();

    [Header("Preparation")]
    [SerializeField] private PreparationStation station;
    [SerializeField] private PreparationMethod method;

    public string RecipeId => recipeId;
    public string DisplayName => displayName;

    public IReadOnlyList<RecipeIngredient> Ingredients => ingredients;

    public PreparationStation Station => station;
    public PreparationMethod Method => method;
}