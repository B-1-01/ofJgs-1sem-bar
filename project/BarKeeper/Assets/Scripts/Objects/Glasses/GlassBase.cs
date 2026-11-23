using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct IngredientColorEntry
{
    public IngredientType Ingredient;
    public Color Color;

    public IngredientColorEntry(IngredientType ingredient, Color color)
    {
        Ingredient = ingredient;
        Color = color;
    }
}

/// <summary>
/// Template script for all glasses in the game (Beer, Shot, Highball, Collins, Rocks, Coupe, Martini, etc.).
/// Manages glass identity, capacity in mL, layered vs. mixed liquid shader colors, and live cocktail identification.
/// </summary>
public class GlassBase : DrinkContainer
{
    private const int MaxShaderLayers = 8;

    [Header("Glass Configuration")]
    [Tooltip("Tipo/identidade do copo (Shot, Highball, Collins, Rocks, Coupe, Martini, CopperMug, Margarita, Hurricane, Zombie, Beer, etc.)")]
    [SerializeField] private GlassType glassIdentity = GlassType.Beer;

    [Tooltip("Capacidade máxima do copo em mililitros (mL)")]
    [Min(1)]
    [SerializeField] private int glassSizeMl = 200;

    [Header("Visual Representation")]
    [Tooltip("Arrasta aqui o MeshRenderer do cilindro interior 'Drink'")]
    [SerializeField] private MeshRenderer drinkRenderer;

    [Tooltip("Cores atribuídas a cada ingrediente (usadas nas camadas e na mistura)")]
    [SerializeField] private List<IngredientColorEntry> ingredientColors = new();

    [Header("Live Readouts (Inspector Debug)")]
    [SerializeField] private string identifiedCocktail = "None";
    [SerializeField] private bool debugIsMixed;
    [SerializeField] private float debugTotalVolumeMl;
    [SerializeField] private float debugLiquidAmountMl;
    [SerializeField] private float debugFoamAmountMl;
    [SerializeField] private float debugFoamRatio;
    [SerializeField] private List<PouredIngredient> debugIngredients = new();

    // Shader property IDs cached for performance
    private static readonly int LiquidPropId = Shader.PropertyToID("_LiquidAmount");
    private static readonly int FoamPropId = Shader.PropertyToID("_FoamAmount");
    private static readonly int FoamColorPropId = Shader.PropertyToID("_FoamColor");
    private static readonly int LiquidColorPropId = Shader.PropertyToID("_LiquidColor");
    private static readonly int MixFactorPropId = Shader.PropertyToID("_MixFactor");
    private static readonly int LayerCountPropId = Shader.PropertyToID("_LayerCount");
    private static readonly int LayerTops0PropId = Shader.PropertyToID("_LayerTops0");
    private static readonly int LayerTops1PropId = Shader.PropertyToID("_LayerTops1");
    private static readonly int[] LayerColorPropIds =
    {
        Shader.PropertyToID("_LayerColor0"),
        Shader.PropertyToID("_LayerColor1"),
        Shader.PropertyToID("_LayerColor2"),
        Shader.PropertyToID("_LayerColor3"),
        Shader.PropertyToID("_LayerColor4"),
        Shader.PropertyToID("_LayerColor5"),
        Shader.PropertyToID("_LayerColor6"),
        Shader.PropertyToID("_LayerColor7")
    };

    private MaterialPropertyBlock propBlock;
    private DrinkRecipeManager cachedRecipeManager;
    private DrinkRecipeDefinition[] fallbackRecipes;

    public GlassType GlassIdentity => glassIdentity;
    public int GlassSizeMl => glassSizeMl;
    public string IdentifiedCocktail => identifiedCocktail;

    public float BeerAmount => GetIngredientAmount(IngredientType.Beer);
    public float MixerAmount => LiquidAmount - BeerAmount;
    public float FoamFactor => TotalVolume > 0f ? (FoamAmount / TotalVolume) : 0f;

    protected override void Awake()
    {
        base.Awake();

        SyncBaseContainerSpecs();
        EnsureDefaultIngredientColors();
        propBlock = new MaterialPropertyBlock();
        CacheRecipes();
    }

    private void OnValidate()
    {
        glassSizeMl = Mathf.Max(1, glassSizeMl);
        SyncBaseContainerSpecs();
        EnsureDefaultIngredientColors();
        propBlock ??= new MaterialPropertyBlock();
        UpdateVisuals();
    }

    private void SyncBaseContainerSpecs()
    {
        glassType = glassIdentity;
        maxCapacityMl = glassSizeMl;
    }

    protected override void Update()
    {
        SyncBaseContainerSpecs();
        UpdateDebugReadouts();
        UpdateVisuals();
    }

    private void UpdateDebugReadouts()
    {
        debugIsMixed = IsMixed;
        debugLiquidAmountMl = LiquidAmount;
        debugFoamAmountMl = FoamAmount;
        debugTotalVolumeMl = TotalVolume;
        debugFoamRatio = FoamFactor;

        debugIngredients.Clear();
        if (Ingredients != null)
        {
            for (int i = 0; i < Ingredients.Count; i++)
            {
                debugIngredients.Add(Ingredients[i]);
            }
        }

        identifiedCocktail = IdentifyCurrentCocktail();
    }

    private void CacheRecipes()
    {
        if (cachedRecipeManager == null)
            cachedRecipeManager = FindFirstObjectByType<DrinkRecipeManager>();

        if (fallbackRecipes == null || fallbackRecipes.Length == 0)
            fallbackRecipes = Resources.FindObjectsOfTypeAll<DrinkRecipeDefinition>();
    }

    private string IdentifyCurrentCocktail()
    {
        if (IsEmpty)
            return "None";

        CacheRecipes();

        IReadOnlyList<DrinkRecipeDefinition> recipes =
            cachedRecipeManager != null && cachedRecipeManager.AllRecipes.Count > 0
                ? cachedRecipeManager.AllRecipes
                : fallbackRecipes;

        if (recipes == null || recipes.Count == 0)
            return "Unidentified Mix";

        string wrongGlassMatch = null;
        string wrongProportionMatch = null;

        for (int i = 0; i < recipes.Count; i++)
        {
            DrinkRecipeDefinition recipe = recipes[i];
            if (recipe == null || recipe.Ingredients == null || recipe.Ingredients.Count == 0)
                continue;

            string name = !string.IsNullOrWhiteSpace(recipe.DisplayName)
                ? recipe.DisplayName
                : recipe.RecipeId;

            if (DrinkDeliveryValidator.MatchesRecipe(this, recipe, recipe.IngredientTolerancePercent, out _))
                return name;

            if (MatchesIngredientsOnly(recipe, recipe.IngredientTolerancePercent))
            {
                if (recipe.PreferredGlass == GlassType.None)
                    return name;

                wrongGlassMatch ??= $"{name} (Wrong Glass: needs {recipe.PreferredGlass})";
            }
            else if (HasSameIngredientTypes(recipe))
            {
                wrongProportionMatch ??= $"{name} (In Progress / Proportions)";
            }
        }

        if (wrongGlassMatch != null)
            return wrongGlassMatch;

        if (wrongProportionMatch != null)
            return wrongProportionMatch;

        return "Unidentified Mix";
    }

    private bool MatchesIngredientsOnly(DrinkRecipeDefinition recipe, float tolerancePercent)
    {
        float tolerance = Mathf.Clamp(tolerancePercent, 0f, 100f) / 100f;
        int requiredCount = recipe.Ingredients.Count;
        int actualCount = (Ingredients?.Count ?? 0) + (FoamAmount > 0.01f ? 1 : 0);

        if (requiredCount != actualCount)
            return false;

        for (int i = 0; i < recipe.Ingredients.Count; i++)
        {
            RecipeIngredient req = recipe.Ingredients[i];
            if (req == null || req.Ingredient == IngredientType.None)
                return false;

            float actual = req.Ingredient == IngredientType.BeerFoam
                ? FoamAmount
                : GetIngredientAmount(req.Ingredient);

            float allowedDiff = req.Quantity * tolerance;
            if (Mathf.Abs(actual - req.Quantity) > allowedDiff + 0.0001f)
                return false;
        }

        return true;
    }

    private bool HasSameIngredientTypes(DrinkRecipeDefinition recipe)
    {
        int requiredCount = recipe.Ingredients.Count;
        int actualCount = (Ingredients?.Count ?? 0) + (FoamAmount > 0.01f ? 1 : 0);

        if (requiredCount != actualCount)
            return false;

        for (int i = 0; i < recipe.Ingredients.Count; i++)
        {
            RecipeIngredient req = recipe.Ingredients[i];
            if (req == null || req.Ingredient == IngredientType.None)
                return false;

            float actual = req.Ingredient == IngredientType.BeerFoam
                ? FoamAmount
                : GetIngredientAmount(req.Ingredient);

            if (actual <= 0.001f)
                return false;
        }

        return true;
    }

    private void UpdateVisuals()
    {
        if (drinkRenderer == null)
            return;

        float capacity = Mathf.Max(1f, glassSizeMl);
        float liquidNorm = Mathf.Clamp01(LiquidAmount / capacity);
        float foamNorm = Mathf.Clamp01(FoamAmount / capacity);

        propBlock ??= new MaterialPropertyBlock();
        drinkRenderer.GetPropertyBlock(propBlock);

        propBlock.SetFloat(LiquidPropId, liquidNorm);
        propBlock.SetFloat(FoamPropId, foamNorm);
        propBlock.SetColor(FoamColorPropId, GetColorForIngredient(IngredientType.BeerFoam));

        int layerCount = 0;
        Vector4 tops0 = Vector4.zero;
        Vector4 tops1 = Vector4.zero;
        Color weightedColorSum = Color.clear;
        float totalColorWeight = 0f;
        float cumulativeAmount = 0f;

        if (Ingredients != null && Ingredients.Count > 0)
        {
            int count = Mathf.Min(Ingredients.Count, MaxShaderLayers);
            for (int i = 0; i < count; i++)
            {
                PouredIngredient poured = Ingredients[i];
                if (poured.AmountMl <= 0.0001f)
                    continue;

                Color layerColor = GetColorForIngredient(poured.Ingredient);
                cumulativeAmount += poured.AmountMl;
                float topNorm = Mathf.Clamp01(cumulativeAmount / capacity);

                propBlock.SetColor(LayerColorPropIds[layerCount], layerColor);
                if (layerCount < 4)
                    tops0[layerCount] = topNorm;
                else
                    tops1[layerCount - 4] = topNorm;

                weightedColorSum += layerColor * poured.AmountMl;
                totalColorWeight += poured.AmountMl;
                layerCount++;
            }
        }

        Color mixedColor = totalColorWeight > 0.0001f
            ? (weightedColorSum / totalColorWeight)
            : GetDefaultIngredientColor(IngredientType.Beer);

        propBlock.SetColor(LiquidColorPropId, mixedColor);
        propBlock.SetFloat(LayerCountPropId, layerCount);
        propBlock.SetVector(LayerTops0PropId, tops0);
        propBlock.SetVector(LayerTops1PropId, tops1);
        propBlock.SetFloat(MixFactorPropId, EffectiveMixFactor);

        drinkRenderer.SetPropertyBlock(propBlock);
    }

    public Color GetColorForIngredient(IngredientType ingredient)
    {
        for (int i = 0; i < ingredientColors.Count; i++)
        {
            if (ingredientColors[i].Ingredient == ingredient)
                return ingredientColors[i].Color;
        }

        return GetDefaultIngredientColor(ingredient);
    }

    public static Color GetDefaultIngredientColor(IngredientType ingredient)
    {
        return ingredient switch
        {
            IngredientType.Beer     => new Color(0.95f, 0.65f, 0.12f, 0.88f), // Amber beer
            IngredientType.Rum      => new Color(0.95f, 0.55f, 0.15f, 0.88f), // Light orange
            IngredientType.Coke     => new Color(0.16f, 0.06f, 0.02f, 0.95f), // Dark brown
            IngredientType.Vodka    => new Color(0.88f, 0.94f, 0.98f, 0.45f), // Clear / frosty white
            IngredientType.Gin      => new Color(0.84f, 0.96f, 0.92f, 0.48f), // Clear botanical tint
            IngredientType.Tequilla => new Color(0.96f, 0.84f, 0.38f, 0.78f), // Pale gold
            IngredientType.Lime     => new Color(0.45f, 0.88f, 0.18f, 0.82f), // Lime green
            IngredientType.Sugar    => new Color(0.95f, 0.92f, 0.80f, 0.65f), // Syrup white-gold
            IngredientType.Ice      => new Color(0.78f, 0.93f, 1.00f, 0.55f), // Frosty ice blue
            IngredientType.BeerFoam => new Color(0.98f, 0.98f, 0.92f, 1.00f), // Foam white
            _                       => new Color(0.90f, 0.90f, 0.90f, 0.70f)
        };
    }

    private void EnsureDefaultIngredientColors()
    {
        ingredientColors ??= new List<IngredientColorEntry>();

        HashSet<IngredientType> existing = new();
        for (int i = ingredientColors.Count - 1; i >= 0; i--)
        {
            IngredientType type = ingredientColors[i].Ingredient;
            if (type == IngredientType.None || !existing.Add(type))
                ingredientColors.RemoveAt(i);
        }

        foreach (IngredientType type in Enum.GetValues(typeof(IngredientType)))
        {
            if (type == IngredientType.None) //|| type == IngredientType.BeerFoam
                continue;

            if (existing.Add(type))
                ingredientColors.Add(new IngredientColorEntry(type, GetDefaultIngredientColor(type)));
        }
    }
}