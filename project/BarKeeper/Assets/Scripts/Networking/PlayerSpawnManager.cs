using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public class PlayerSpawnManager : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints = new Transform[4];

    private NetworkManager networkManager;

    private readonly Dictionary<ulong, int> assignedSpawns = new();
    private readonly HashSet<int> occupiedSpawns = new();

    public int MaxPlayers => spawnPoints.Length;

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();

        networkManager.ConnectionApprovalCallback = ApproveConnection;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        networkManager.OnServerStopped += OnServerStopped;
    }

    private void ApproveConnection(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        int availableIndex = -1;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null && !occupiedSpawns.Contains(i))
            {
                availableIndex = i;
                break;
            }
        }

        if (availableIndex == -1)
        {
            response.Approved = false;
            response.Reason = "Lobby is full.";
            return;
        }

        assignedSpawns[request.ClientNetworkId] = availableIndex;
        occupiedSpawns.Add(availableIndex);

        Transform spawn = spawnPoints[availableIndex];

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Position = spawn.position;
        response.Rotation = spawn.rotation;

        Debug.Log($"Player {request.ClientNetworkId} assigned to Spawn {availableIndex + 1}");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!assignedSpawns.TryGetValue(clientId, out int index))
            return;

        occupiedSpawns.Remove(index);
        assignedSpawns.Remove(clientId);

        Debug.Log($"Spawn {index + 1} is now available.");
    }

    private void OnServerStopped(bool wasHost)
    {
        assignedSpawns.Clear();
        occupiedSpawns.Clear();
    }

    private void OnDestroy()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        networkManager.OnServerStopped -= OnServerStopped;

        if (networkManager.ConnectionApprovalCallback == ApproveConnection)
            networkManager.ConnectionApprovalCallback = null;
    }
}