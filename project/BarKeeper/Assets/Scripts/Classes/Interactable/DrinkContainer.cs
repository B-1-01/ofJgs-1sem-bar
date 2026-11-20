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

    protected virtual void Update() {}

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