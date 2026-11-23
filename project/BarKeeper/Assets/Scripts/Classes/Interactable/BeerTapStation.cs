using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class BeerTapStation : Station
{
    [Header("Beer Tap Setup")]
    [SerializeField] private Transform tapNozzlePoint;
    [SerializeField] private Transform glassRestPoint;
    [SerializeField] private float detectionRadius = 0.35f;

    [Header("Minigame Tuning")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float pourRateMlPerSecond = 40f;
    [SerializeField] private float maxTurbulenceFactor = 0.75f;

    // Referência em rede ao copo atualmente na estação
    private readonly NetworkVariable<NetworkObjectReference> currentGlassRef = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private GlassBase currentGlass;
    private float currentHeightRatio = 0f;

    public bool HasGlass => currentGlass != null;

    protected override bool MeetsStationRequirements(FPSPlayerController player)
    {
        CheckForGlassOnRestPoint();
        return base.MeetsStationRequirements(player) && HasGlass;
    }

    private void CheckForGlassOnRestPoint()
    {
        if (glassRestPoint == null) return;

        Collider[] hits = Physics.OverlapSphere(glassRestPoint.position, detectionRadius);
        foreach (var hit in hits)
        {
            GlassBase glass = hit.GetComponentInParent<GlassBase>();
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
        if (IsServer) currentGlassRef.Value = default;
    }

    protected override void OnMinigameStarted()
    {
        base.OnMinigameStarted();

        // Garante que temos a referência local do copo
        if (currentGlass == null && currentGlassRef.Value.TryGet(out NetworkObject netObj)) currentGlass = netObj.GetComponent<GlassBase>();

        currentHeightRatio = 0f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    protected override void OnMinigameExited()
    {
        base.OnMinigameExited();

        if (currentGlass != null && glassRestPoint != null)
        {
            currentGlass.transform.SetPositionAndRotation(glassRestPoint.position, glassRestPoint.rotation);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (!isUsingStationLocally || currentGlass == null) return;

        HandleGlassMovement();
        HandlePouring();
    }

    private void HandleGlassMovement()
    {
        if (Mouse.current == null || tapNozzlePoint == null || glassRestPoint == null) return;

        // Lê movimento vertical do rato
        float deltaY = Mouse.current.delta.y.ReadValue() * mouseSensitivity * 0.01f;
        currentHeightRatio = Mathf.Clamp01(currentHeightRatio + deltaY);

        // Interpola a posição do copo entre o tabuleiro (0) e a torneira (1)
        Vector3 targetPos = Vector3.Lerp(glassRestPoint.position, tapNozzlePoint.position, currentHeightRatio);
        currentGlass.transform.position = targetPos;
    }

    private void HandlePouring()
    {
        if (Keyboard.current == null) return;

        // Segurar [ESPAÇO] ativa a torneira
        if (Keyboard.current.spaceKey.isPressed)
        {
            // Espuma por distancia
            float turbulence = Mathf.Lerp(maxTurbulenceFactor, 0.08f, currentHeightRatio);

            float totalPourThisFrame = pourRateMlPerSecond * Time.deltaTime;
            float foamMl = totalPourThisFrame * turbulence;
            float liquidMl = totalPourThisFrame * (1f - turbulence);

            RequestPourRpc(liquidMl, foamMl);
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestPourRpc(float liquidMl, float foamMl)
    {
        if (currentGlass == null)
        {
            if (currentGlassRef.Value.TryGet(out NetworkObject netObj))
                currentGlass = netObj.GetComponent<GlassBase>();
        }

        if (currentGlass != null) currentGlass.ServerAddIngredient(IngredientType.Beer, liquidMl, foamMl);
    }

    private void OnDrawGizmosSelected()
    {
        if (glassRestPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(glassRestPoint.position, detectionRadius);
        }

        if (tapNozzlePoint != null && glassRestPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(glassRestPoint.position, tapNozzlePoint.position);
        }
    }
}