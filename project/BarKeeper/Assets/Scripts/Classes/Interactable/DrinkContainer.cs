using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public struct PouredIngredient : INetworkSerializable, IEquatable<PouredIngredient>
{
    public IngredientType Ingredient;
    public float AmountCl;

    public PouredIngredient(IngredientType ingredient, float amountCl)
    {
        Ingredient = ingredient;
        AmountCl = amountCl;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Ingredient);
        serializer.SerializeValue(ref AmountCl);
    }

    public bool Equals(PouredIngredient other)
    {
        return Ingredient == other.Ingredient && Mathf.Approximately(AmountCl, other.AmountCl);
    }
}

public abstract class DrinkContainer : PickupItem
{
    [Header("Container Specs")]
    [Tooltip("Maximum liquid capacity in centiliters (cl)")]
    [SerializeField] protected float maxCapacityCl = 33f;
    [SerializeField] protected GlassType glassType = GlassType.None;

    [Range(0f, 1f)]
    [SerializeField] protected float foamToBeerRatio = 0.10f;

    [SerializeField] protected float foamThresholdCl = 10f;
    [SerializeField] protected float decayRateAboveThreshold = 0.15f;
    [SerializeField] protected float decayRateBelowThreshold = 0.03f;

    // List of all ingredients
    private readonly NetworkList<PouredIngredient> ingredients = new();

    private readonly NetworkVariable<float> foamAmount = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public float LiquidAmount => CalculateTotalLiquid();
    public float FoamAmount => foamAmount.Value;
    public float TotalVolume => LiquidAmount + FoamAmount;
    public float MaxCapacityCl => maxCapacityCl;

    public GlassType GlassType => glassType;

    public bool IsEmpty => TotalVolume <= 0.01f;
    public bool IsFull => TotalVolume >= maxCapacityCl;
    public bool IsOverflowed => TotalVolume > maxCapacityCl;

    public NetworkList<PouredIngredient> Ingredients => ingredients;

    protected virtual void Update()
    {
        ApplyFoamDecay();
    }

    private void ApplyFoamDecay()
    {
        float currentFoam = foamAmount.Value;
        if (currentFoam <= 0f) return;
        float activeDecayRate = currentFoam > foamThresholdCl 
            ? decayRateAboveThreshold 
            : decayRateBelowThreshold;

        float decay = currentFoam * activeDecayRate * Time.deltaTime;
        foamAmount.Value = currentFoam - decay < 0.01f ? 0f : currentFoam - decay;

        if (foamToBeerRatio > 0f && decay > 0f)
        {
            float liquidFromFoam = decay * foamToBeerRatio;
            ServerAddIngredient(IngredientType.Beer, liquidFromFoam, 0f);
        }
    }

    public float GetIngredientAmount(IngredientType ingredient)
    {
        for (int i = 0; i < ingredients.Count; i++)
        {
            if (ingredients[i].Ingredient == ingredient)
                return ingredients[i].AmountCl;
        }
        return 0f;
    }

    private float CalculateTotalLiquid()
    {
        float total = 0f;
        for (int i = 0; i < ingredients.Count; i++)
        {
            total += ingredients[i].AmountCl;
        }
        return total;
    }

    public void ServerAddIngredient(IngredientType ingredient, float amountCl, float extraFoamCl = 0f)
    {
        if (!IsServer || (amountCl <= 0f && extraFoamCl <= 0f)) return;

        if (amountCl > 0f)
        {
            bool found = false;
            for (int i = 0; i < ingredients.Count; i++)
            {
                if (ingredients[i].Ingredient == ingredient)
                {
                    PouredIngredient existing = ingredients[i];
                    existing.AmountCl += amountCl;
                    ingredients[i] = existing;
                    found = true;
                    break;
                }
            }
            if (!found) ingredients.Add(new PouredIngredient(ingredient, amountCl));
        }

        if (extraFoamCl > 0f)
        {
            foamAmount.Value = Mathf.Max(0f, foamAmount.Value + extraFoamCl);
        }
    }

    public void ServerSetFoam(float foamCl)
    {
        if (!IsServer) return;
        foamAmount.Value = Mathf.Max(0f, foamCl);
    }

    public void ServerEmpty()
    {
        if (!IsServer) return;
        ingredients.Clear();
        foamAmount.Value = 0f;
    }
}