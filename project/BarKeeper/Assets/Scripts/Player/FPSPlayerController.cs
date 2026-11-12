using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(NetworkObject))]
public class FPSPlayerController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Transform handHoldPoint;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float acceleration = 40f;

    [Header("Camera")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float maxLookAngle = 85f;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 5f;
    [SerializeField] private LayerMask interactableLayer = ~0;

    [Header("Debug")]
    [SerializeField] private bool showLocalBody = false;

    private Rigidbody rb;
    private AudioListener audioListener;
    private PauseMenuController pauseMenu;

    private Vector2 moveInput;
    private float cameraPitch;
    private float pendingYaw;
    private bool gameplayInputEnabled = true;

    private InteractableObject currInteractable;
    private InteractableObject heldItem;
    private InteractableObject activeStation;

    public InteractableObject CurrentInteractable => currInteractable;
    public Transform HandHoldPoint => handHoldPoint != null ? handHoldPoint : playerCamera.transform;
    public bool HasItemInHand => heldItem != null;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (bodyRenderer == null)
            bodyRenderer = GetComponent<Renderer>();

        if (playerCamera != null)
        {
            audioListener = playerCamera.GetComponent<AudioListener>();
            playerCamera.enabled = false;
        }

        if (audioListener != null)
            audioListener.enabled = false;

        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    /// <summary>Configura o jogador após o spawn, permitindo apenas ao proprietário controlar a câmara e os movimentos.</summary>
    public override void OnNetworkSpawn()
    {
        if (playerCamera != null)
            playerCamera.enabled = IsOwner;

        if (audioListener != null)
            audioListener.enabled = IsOwner;

        UpdateBodyVisibility();

        if (!IsOwner)
            return;

        pauseMenu = FindFirstObjectByType<PauseMenuController>();

        if (pauseMenu != null)
            pauseMenu.RegisterLocalPlayer(this);
        else
            SetGameplayInputEnabled(true);
    }

    /// <summary>Desassocia o jogador do menu de pausa, desativa a câmara e limpa os valores de movimento pendentes.</summary>
    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            if (pauseMenu != null)
                pauseMenu.UnregisterLocalPlayer(this);

            SetCursorLocked(false);
        }

        if (playerCamera != null)
            playerCamera.enabled = false;

        if (audioListener != null)
            audioListener.enabled = false;

        moveInput = Vector2.zero;
        pendingYaw = 0f;
        ClearInteractable();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        HandleDebugInput();

        // Se estiver numa statoion, bloqueia o movimento normal e permite sair com Q
        // TODO: Verificar tecla!
        if (activeStation != null)
        {
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            {
                activeStation.ExitStationLocal(this);
            }
            return;
        }

        if (!gameplayInputEnabled)
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            moveInput = Vector2.zero;
            pendingYaw = 0f;
            ClearInteractable();
            return;
        }

        ReadMovementInput();
        HandleLook();

        HandleInteractionDetection();
        HandleInteractionInput();
    }

    /// <summary>Aplica movimento e rotação através do Rigidbody, utilizando aceleração para atingir a velocidade pretendida.</summary>
    private void FixedUpdate()
    {
        if (!IsSpawned || !IsOwner || rb.isKinematic)
            return;

        Quaternion targetRotation = rb.rotation * Quaternion.Euler(0f, pendingYaw, 0f);
        rb.MoveRotation(targetRotation);
        pendingYaw = 0f;

        Vector3 localMovement = new Vector3(moveInput.x, 0f, moveInput.y);
        Vector3 targetVelocity = targetRotation * localMovement * moveSpeed;

        Vector3 currentVelocity = rb.linearVelocity;
        currentVelocity.y = 0f;

        Vector3 velocityDifference = targetVelocity - currentVelocity;
        Vector3 requiredAcceleration = velocityDifference / Time.fixedDeltaTime;
        requiredAcceleration = Vector3.ClampMagnitude(requiredAcceleration, acceleration);

        rb.AddForce(requiredAcceleration, ForceMode.Acceleration);
    }

    /// <summary>Lê o movimento através das teclas WASD e normaliza o vetor para evitar maior velocidade na diagonal.</summary>
    private void ReadMovementInput()
    {
        if (Keyboard.current == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.wKey.isPressed) vertical += 1f;
        if (Keyboard.current.sKey.isPressed) vertical -= 1f;
        if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.dKey.isPressed) horizontal += 1f;

        moveInput = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
    }

    /// <summary>Utiliza o movimento do rato para rodar horizontalmente a cápsula e controlar a inclinação vertical da câmara.</summary>
    private void HandleLook()
    {
        if (Mouse.current == null || playerCamera == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        pendingYaw += mouseDelta.x * mouseSensitivity;

        cameraPitch -= mouseDelta.y * mouseSensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch, -maxLookAngle, maxLookAngle);

        playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void HandleDebugInput()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            showLocalBody = !showLocalBody;
            UpdateBodyVisibility();
        }
    }

    private void UpdateBodyVisibility()
    {
        if (bodyRenderer == null)
            return;

        bodyRenderer.enabled = true;

        if (IsOwner && !showLocalBody)
        {
            bodyRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
        else
        {
            bodyRenderer.shadowCastingMode = ShadowCastingMode.On;
        }
    }

    public void SetGameplayInputEnabled(bool enabled)
    {
        gameplayInputEnabled = enabled;

        if (!enabled)
        {
            moveInput = Vector2.zero;
            pendingYaw = 0f;
            ClearInteractable();
        }

        if (IsSpawned && IsOwner)
            SetCursorLocked(enabled);
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnDisable()
    {
        if (IsOwner)
            SetCursorLocked(false);
    }

    // Interaction

    private void HandleInteractionDetection()
    {
        if (playerCamera == null)
        {
            ClearInteractable();
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer, QueryTriggerInteraction.Ignore))
        {
            InteractableObject interactable = hit.collider.GetComponentInParent<InteractableObject>();

            if (interactable != currInteractable)
            {
                if (currInteractable != null)
                    currInteractable.SetFocus(false);

                currInteractable = interactable;

                if (currInteractable != null)
                    currInteractable.SetFocus(true);
            }
        }
        else
        {
            ClearInteractable();
        }
    }

    private void HandleInteractionInput()
    {
        if (Keyboard.current == null)
            return;

        // TODO: Teclas
        // Tecla E: Interagir
        if (Keyboard.current.eKey.wasPressedThisFrame && currInteractable != null)
        {
            currInteractable.Interact(this);
        }

        // Tecla G: Droppar
        if (Keyboard.current.gKey.wasPressedThisFrame && heldItem != null)
        {
            RequestDropHeldItemServerRpc();
        }
    }

    private void ClearInteractable()
    {
        if (currInteractable != null)
        {
            currInteractable.SetFocus(false);
            currInteractable = null;
        }
    }

    // Helpers (Interaction)

    public void ServerSetHeldItem(InteractableObject item)
    {
        heldItem = item;
    }

    public void SetHeldItemReference(InteractableObject item)
    {
        heldItem = item;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestDropHeldItemServerRpc()
    {
        if (heldItem != null)
        {
            heldItem.ServerDropItem();
            heldItem = null;
        }
    }

    public void EnterStationMinigame(InteractableObject station)
    {
        activeStation = station;
        SetGameplayInputEnabled(false);
    }

    public void ExitStationMinigame()
    {
        activeStation = null;
        SetGameplayInputEnabled(true);
    }
}