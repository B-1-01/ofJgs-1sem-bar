using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class NetworkTestBall : NetworkBehaviour
{
    private const ulong NoHolder = ulong.MaxValue;

    private readonly NetworkVariable<ulong> holderClientId = new(
        NoHolder,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Rigidbody rb;
    private SphereCollider ballCollider;
    private PlayerBallInteractor holder;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    public bool IsHeld => holderClientId.Value != NoHolder;

    public bool IsHeldBy(ulong clientId)
    {
        return holderClientId.Value == clientId;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ballCollider = GetComponent<SphereCollider>();

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        rb.isKinematic = true;
    }

    /// <summary>Inicializa o estado da bola, ativa a simulação física no servidor e regista a deteção de desconexões.</summary>
    public override void OnNetworkSpawn()
    {
        holderClientId.OnValueChanged += OnHolderChanged;
        UpdateCollider();

        if (!IsServer)
            return;

        holder = null;
        holderClientId.Value = NoHolder;

        rb.isKinematic = false;
        rb.position = initialPosition;
        rb.rotation = initialRotation;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void FixedUpdate()
    {
        if (!IsSpawned || !IsServer || !IsHeld)
            return;

        if (holder == null ||
            !holder.IsSpawned ||
            holder.OwnerClientId != holderClientId.Value)
        {
            ServerDrop(Vector3.zero);
            return;
        }

        rb.MovePosition(holder.HoldPosition);
    }

    /// <summary>Verifica se a bola está livre e dentro do alcance do jogador, permitindo ao servidor autorizar a interação.</summary>
    public bool ServerTryPickup(PlayerBallInteractor requester, float maxDistance)
    {
        if (!IsSpawned || !IsServer || IsHeld ||
            requester == null || !requester.IsSpawned)
            return false;

        float distanceSqr =
            (requester.transform.position - rb.position).sqrMagnitude;

        if (distanceSqr > maxDistance * maxDistance)
            return false;

        holder = requester;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        holderClientId.Value = requester.OwnerClientId;
        rb.position = requester.HoldPosition;

        Debug.Log($"[Ball] Player {requester.OwnerClientId} picked up the ball.");

        return true;
    }

    /// <summary>Liberta a bola e devolve-a à simulação física, aplicando uma velocidade inicial caso esteja a ser lançada.</summary>
    public void ServerDrop(Vector3 launchVelocity)
    {
        if (!IsSpawned || !IsServer || !IsHeld)
            return;

        ulong previousHolder = holderClientId.Value;

        holder = null;
        holderClientId.Value = NoHolder;

        rb.isKinematic = false;
        rb.linearVelocity = launchVelocity;
        rb.angularVelocity = Vector3.zero;

        Debug.Log($"[Ball] Player {previousHolder} released the ball.");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsHeldBy(clientId))
            ServerDrop(Vector3.zero);
    }

    private void OnHolderChanged(ulong previous, ulong current)
    {
        UpdateCollider();
    }

    private void UpdateCollider()
    {
        if (ballCollider != null)
            ballCollider.isTrigger = IsHeld;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            holder = null;
        }

        holderClientId.OnValueChanged -= OnHolderChanged;

        rb.isKinematic = true;
        ballCollider.isTrigger = false;

        transform.SetPositionAndRotation(initialPosition, initialRotation);
    }
}