using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Unity.Netcode.Components;

[Serializable]
public struct PouredIngredient : INetworkSerializable, IEquatable<PouredIngredient>
{
    public IngredientType Ingredient;
    public float AmountMl;

    public PouredIngredient(IngredientType ingredient, float amountMl)
    {
        Ingredient = ingredient;
        AmountMl = amountMl;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Ingredient);
        serializer.SerializeValue(ref AmountMl);
    }

    public bool Equals(PouredIngredient other)
    {
        return Ingredient == other.Ingredient && Mathf.Approximately(AmountMl, other.AmountMl);
    }
}

public abstract class DrinkContainer : PickupItem
{
    [Header("Container Specs")]
    [Tooltip("Maximum liquid capacity")]
    [HideInInspector]
    [SerializeField] protected float maxCapacityMl = 330f;

    [HideInInspector]
    [SerializeField] protected GlassType glassType = GlassType.None;

    [HideInInspector]
    [SerializeField] private Transform emptySpawnPoint;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    // List of all ingredients
    private readonly NetworkList<PouredIngredient> ingredients = new();

    private readonly NetworkVariable<float> foamAmount = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> isMixed = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float localMixPreview;

    public float LiquidAmount => CalculateTotalLiquid();
    public float FoamAmount => foamAmount.Value;
    public float TotalVolume => LiquidAmount + FoamAmount;
    public float MaxCapacityMl => maxCapacityMl;

    public GlassType GlassType => glassType;

    public bool IsEmpty => TotalVolume <= 0.01f;
    public bool IsFull => TotalVolume >= maxCapacityMl;
    public bool IsOverflowed => TotalVolume > maxCapacityMl;

    public NetworkList<PouredIngredient> Ingredients => ingredients;
    public bool IsMixed => isMixed.Value;
    public float EffectiveMixFactor => isMixed.Value ? 1f : Mathf.Clamp01(localMixPreview);

    protected virtual void Update() {}

    public void SetLocalMixPreview(float previewFactor)
    {
        localMixPreview = Mathf.Clamp01(previewFactor);
    }

    /// <summary>
    /// Stores the container's initial position on the server.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
            return;

        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    /// <summary>
    /// Empties the container and returns it to its original spawn point.
    /// The server handles the reset and synchronizes it with all clients.
    /// </summary>
    public void ServerReturnToEmptySpawn()
    {
        if (!IsServer || !IsSpawned)
            return;

        // Release the container from its current holder.
        ServerPlaceAt(null);

        // Remove all ingredients and foam.
        ServerEmpty();

        // Use the configured spawn point or the original position.
        Vector3 targetPosition = emptySpawnPoint != null
            ? emptySpawnPoint.position
            : initialPosition;

        Quaternion targetRotation = emptySpawnPoint != null
            ? emptySpawnPoint.rotation
            : initialRotation;

        // Teleport the container on all connected instances.
        GetComponent<NetworkTransform>().Teleport(
            targetPosition,
            targetRotation,
            transform.localScale
        );
    }

    public float GetIngredientAmount(IngredientType ingredient)
    {
        for (int i = 0; i < ingredients.Count; i++)
        {
            if (ingredients[i].Ingredient == ingredient)
                return ingredients[i].AmountMl;
        }
        return 0f;
    }

    private float CalculateTotalLiquid()
    {
        float total = 0f;
        for (int i = 0; i < ingredients.Count; i++)
        {
            total += ingredients[i].AmountMl;
        }
        return total;
    }

    public void ServerAddIngredient(IngredientType ingredient, float amountMl, float extraFoamMl = 0f)
    {
        if (!IsServer || (amountMl <= 0f && extraFoamMl <= 0f)) return;

        // Liquid can replace foam, but cannot exceed the container capacity.
        amountMl = Mathf.Clamp(amountMl, 0f, maxCapacityMl - LiquidAmount);

        if (amountMl > 0f)
        {
            bool found = false;
            for (int i = 0; i < ingredients.Count; i++)
            {
                if (ingredients[i].Ingredient == ingredient)
                {
                    PouredIngredient existing = ingredients[i];
                    existing.AmountMl += amountMl;
                    ingredients[i] = existing;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                ingredients.Add(new PouredIngredient(ingredient, amountMl));
                // Adding a new distinct ingredient unmixes the drink so layers appear
                if (ingredients.Count > 1)
                    isMixed.Value = false;
            }
        }

        // Remove excess foam when liquid or new foam exceeds the capacity.
        float maxFoamMl = Mathf.Max(0f, maxCapacityMl - LiquidAmount);

        foamAmount.Value = Mathf.Clamp(
            foamAmount.Value + extraFoamMl,
            0f,
            maxFoamMl
        );
    }

    public void ServerSetFoam(float foamMl)
    {
        if (!IsServer) return;
        // Foam cannot occupy more than the remaining container capacity.
        float maxFoamMl = Mathf.Max(0f, maxCapacityMl - LiquidAmount);
        foamAmount.Value = Mathf.Clamp(foamMl, 0f, maxFoamMl);
    }

    public void ServerSetMixed(bool mixed)
    {
        if (!IsServer) return;
        isMixed.Value = mixed;
    }

    public void ServerEmpty()
    {
        if (!IsServer) return;
        ingredients.Clear();
        foamAmount.Value = 0f;
        isMixed.Value = false;
        localMixPreview = 0f;
    }
}