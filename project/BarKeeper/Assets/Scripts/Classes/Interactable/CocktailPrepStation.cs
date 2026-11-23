using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class CocktailPrepStation : Station
{
    [Header("Cocktail Station Setup")]
    [SerializeField] private Transform glassRestPoint;
    [SerializeField] private float detectionRadius = 0.35f;
    [SerializeField] private bool requireEmptyHands = false;
    [SerializeField] private LayerMask ingredientLayer = ~0;

    [Header("Mixing Settings")]
    [SerializeField] private float mixDurationSeconds = 1.5f;

    private readonly NetworkVariable<NetworkObjectReference> currentGlassRef = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private DrinkContainer currentGlass;
    private CocktailIngredientSource hoveredSource;
    private float mixProgress;

    private float pourTimer;

    public Transform GlassRestPoint => glassRestPoint;
    public bool HasGlass => currentGlass != null;
    public float MixProgress => mixProgress;
    public bool IsMixed => currentGlass != null && currentGlass.IsMixed;

    protected override bool MeetsStationRequirements(FPSPlayerController player)
    {
        if (requireEmptyHands && player.HasHeldItem)
            return false;

        CheckForGlassOnRestPoint();
        return HasGlass;
    }

    private void CheckForGlassOnRestPoint()
    {
        if (glassRestPoint == null) return;

        Collider[] hits = Physics.OverlapSphere(glassRestPoint.position, detectionRadius);
        foreach (var hit in hits)
        {
            DrinkContainer glass = hit.GetComponentInParent<DrinkContainer>();
            if (glass != null && !glass.IsHeld)
            {
                currentGlass = glass;
                if (IsServer)
                {
                    currentGlassRef.Value = new NetworkObjectReference(glass.NetworkObject);
                }
                return;
            }
        }

        currentGlass = null;
        if (IsServer)
            currentGlassRef.Value = default;
    }

    protected override void OnMinigameStarted()
    {
        base.OnMinigameStarted();

        if (currentGlass == null && currentGlassRef.Value.TryGet(out NetworkObject netObj))
            currentGlass = netObj.GetComponent<DrinkContainer>();

        mixProgress = currentGlass != null && currentGlass.IsMixed ? 1f : 0f;
        if (currentGlass != null)
            currentGlass.SetLocalMixPreview(mixProgress);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    protected override void OnMinigameExited()
    {
        base.OnMinigameExited();

        ClearHoveredSource();

        if (currentGlass != null)
            currentGlass.SetLocalMixPreview(currentGlass.IsMixed ? 1f : 0f);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    protected override void Update()
    {
        // base.Update() handles Q / E station exit
        base.Update();

        if (!isUsingStationLocally)
            return;

        if (currentGlass == null && currentGlassRef.Value.TryGet(out NetworkObject netObj))
            currentGlass = netObj.GetComponent<DrinkContainer>();

        if (currentGlass == null)
            return;

        HandleIngredientSelectionAndPouring();
        HandleMixingAndReset();
    }

    private void HandleIngredientSelectionAndPouring()
    {
        if (stationCamera == null || Mouse.current == null)
            return;

        Ray ray = stationCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        CocktailIngredientSource foundSource = null;
        float closestDist = float.MaxValue;

        RaycastHit[] hits = Physics.RaycastAll(ray, 5f, ingredientLayer, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            CocktailIngredientSource source = hit.collider.GetComponentInParent<CocktailIngredientSource>();
            if (source != null && hit.distance < closestDist)
            {
                closestDist = hit.distance;
                foundSource = source;
            }
        }

        if (hoveredSource != foundSource)
        {
            pourTimer = 0f;
            ClearHoveredSource();
            hoveredSource = foundSource;
            if (hoveredSource != null)
                hoveredSource.SetHighlighted(true);
        }

        if (hoveredSource == null || hoveredSource.Ingredient == IngredientType.None)
            return;

        if (hoveredSource.ContinuousPour)
        {
            if (Mouse.current.leftButton.isPressed)
            {
                pourTimer += Time.deltaTime;
                if (pourTimer >= 1f)
                {
                    pourTimer -= 1f;
                    mixProgress = 0f;
                    currentGlass.SetLocalMixPreview(0f);
                    RequestAddIngredientRpc(hoveredSource.Ingredient, hoveredSource.PourRatePerSecond);
                }
            }
            else
            {
                pourTimer = 0f;
            }
        }
        else
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                mixProgress = 0f;
                currentGlass.SetLocalMixPreview(0f);
                RequestAddIngredientRpc(hoveredSource.Ingredient, hoveredSource.AmountPerClick);
            }
        }
    }

    private void HandleMixingAndReset()
    {
        if (Keyboard.current == null)
            return;

        // Hold [SPACE] to mix the cocktail
        if (Keyboard.current.spaceKey.isPressed && !currentGlass.IsEmpty && !currentGlass.IsMixed)
        {
            mixProgress = Mathf.Clamp01(mixProgress + (Time.deltaTime / Mathf.Max(0.1f, mixDurationSeconds)));
            currentGlass.SetLocalMixPreview(mixProgress);

            if (mixProgress >= 1f)
            {
                RequestMixGlassRpc();
                Debug.Log("[CocktailStation] Cocktail mixed!");
            }
        }

        // Press [R] to empty the glass and start over
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            mixProgress = 0f;
            currentGlass.SetLocalMixPreview(0f);
            RequestEmptyGlassRpc();
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestAddIngredientRpc(IngredientType ingredient, float amountMl)
    {
        if (currentGlass == null && currentGlassRef.Value.TryGet(out NetworkObject netObj))
            currentGlass = netObj.GetComponent<DrinkContainer>();

        if (currentGlass == null || currentGlass.IsFull)
            return;

        float remainingCapacity = Mathf.Max(0f, currentGlass.MaxCapacityMl - currentGlass.TotalVolume);
        float clampedAmount = Mathf.Min(amountMl, remainingCapacity);

        if (clampedAmount > 0f)
            currentGlass.ServerAddIngredient(ingredient, clampedAmount);
    }

    [Rpc(SendTo.Server)]
    private void RequestMixGlassRpc()
    {
        if (currentGlass == null && currentGlassRef.Value.TryGet(out NetworkObject netObj))
            currentGlass = netObj.GetComponent<DrinkContainer>();

        if (currentGlass != null && !currentGlass.IsEmpty)
            currentGlass.ServerSetMixed(true);
    }

    [Rpc(SendTo.Server)]
    private void RequestEmptyGlassRpc()
    {
        if (currentGlass == null && currentGlassRef.Value.TryGet(out NetworkObject netObj))
            currentGlass = netObj.GetComponent<DrinkContainer>();

        if (currentGlass != null)
            currentGlass.ServerEmpty();
    }

    private void ClearHoveredSource()
    {
        if (hoveredSource != null)
        {
            hoveredSource.SetHighlighted(false);
            hoveredSource = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (glassRestPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(glassRestPoint.position, detectionRadius);
        }
    }
}