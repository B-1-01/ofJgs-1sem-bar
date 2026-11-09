using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject))]
public class PlayerBallInteractor : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;

    [Header("Interaction")]
    [SerializeField] private float pickupDistance = 2.5f;
    [SerializeField] private float holdDistance = 1.15f;
    [SerializeField] private float holdHeightOffset = 0.2f;
    [SerializeField] private float throwSpeed = 9f;

    public Vector3 HoldPosition =>
        transform.position +
        Vector3.up * holdHeightOffset +
        transform.forward * holdDistance;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner ||
            Cursor.lockState != CursorLockMode.Locked)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            TogglePickupRpc();
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            playerCamera != null)
        {
            ThrowBallRpc(playerCamera.transform.forward);
        }
    }

    [Rpc(SendTo.Server)]
    private void TogglePickupRpc(RpcParams rpcParams = default)
    {
        if (!IsServer ||
            rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        NetworkTestBall[] balls =
            FindObjectsByType<NetworkTestBall>(FindObjectsSortMode.None);

        foreach (NetworkTestBall ball in balls)
        {
            if (ball.IsSpawned && ball.IsHeldBy(OwnerClientId))
            {
                ball.ServerDrop(Vector3.zero);
                return;
            }
        }

        NetworkTestBall closest = null;
        float closestSqrDistance = pickupDistance * pickupDistance;

        foreach (NetworkTestBall ball in balls)
        {
            if (!ball.IsSpawned || ball.IsHeld)
                continue;

            float distanceSqr =
                (ball.transform.position - transform.position).sqrMagnitude;

            if (distanceSqr < closestSqrDistance)
            {
                closestSqrDistance = distanceSqr;
                closest = ball;
            }
        }

        if (closest != null)
            closest.ServerTryPickup(this, pickupDistance);
    }

    [Rpc(SendTo.Server)]
    private void ThrowBallRpc(Vector3 direction, RpcParams rpcParams = default)
    {
        if (!IsServer ||
            rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (direction.sqrMagnitude < 0.01f ||
            float.IsNaN(direction.x) ||
            float.IsNaN(direction.y) ||
            float.IsNaN(direction.z) ||
            float.IsInfinity(direction.x) ||
            float.IsInfinity(direction.y) ||
            float.IsInfinity(direction.z))
            return;

        NetworkTestBall[] balls =
            FindObjectsByType<NetworkTestBall>(FindObjectsSortMode.None);

        foreach (NetworkTestBall ball in balls)
        {
            if (ball.IsSpawned && ball.IsHeldBy(OwnerClientId))
            {
                ball.ServerDrop(direction.normalized * throwSpeed);
                return;
            }
        }
    }
}