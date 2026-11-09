
using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using PlayEveryWare.EpicOnlineServices;

public class EOSLobbyManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EOSLoginManager loginManager;
    [SerializeField] private EOSNetworkBridge networkBridge;
    [SerializeField] private NetworkPlayerRoster playerRoster;
    [SerializeField] private PauseMenuController pauseMenu;

    private LobbyInterface lobbyInterface;

    private string lobbyId = "";
    private string joinLobbyId = "";
    private string status = "Waiting for EOS login...";

    private bool isHost;
    private bool isBusy;
    private bool forceMenuVisible;

    public string LobbyId => lobbyId;
    public bool IsInLobby => !string.IsNullOrEmpty(lobbyId);
    public bool IsHost => isHost;

    private void Start()
    {
        if (loginManager == null)
            loginManager = GetComponent<EOSLoginManager>();

        if (networkBridge == null)
            networkBridge = GetComponent<EOSNetworkBridge>();

        if (playerRoster == null)
            playerRoster = GetComponent<NetworkPlayerRoster>();

        if (pauseMenu == null)
            pauseMenu = FindFirstObjectByType<PauseMenuController>();

        if (networkBridge != null)
        {
            networkBridge.StateChanged += OnNetworkStateChanged;
            networkBridge.ConnectionFailed += OnNetworkFailed;
        }
    }

    private bool IsReady()
    {
        if (loginManager == null || !loginManager.IsLoggedIn)
        {
            status = "EOS login not ready.";
            return false;
        }

        if (EOSManager.Instance == null)
        {
            status = "EOSManager not found.";
            return false;
        }

        if (lobbyInterface == null)
        {
            var platform = EOSManager.Instance.GetEOSPlatformInterface();

            if (platform == null)
            {
                status = "EOS platform not initialized.";
                return false;
            }

            lobbyInterface = platform.GetLobbyInterface();
        }

        return lobbyInterface != null;
    }

    public void CreateLobby()
    {
        if (!IsReady() || IsInLobby || isBusy || networkBridge == null ||
            networkBridge.State != EOSNetworkBridge.ConnectionState.Disconnected)
            return;

        isBusy = true;
        forceMenuVisible = false;
        status = "Creating EOS lobby...";

        var options = new CreateLobbyOptions
        {
            LocalUserId = loginManager.LocalUserId,
            MaxLobbyMembers = 4,
            PermissionLevel = LobbyPermissionLevel.Publicadvertised,
            BucketId = "Barkeeper_Test",
            PresenceEnabled = false,
            AllowInvites = false,
            EnableRTCRoom = false,
            EnableJoinById = true
        };

        lobbyInterface.CreateLobby(ref options, null,
            (ref CreateLobbyCallbackInfo data) =>
            {
                isBusy = false;

                if (data.ResultCode != Result.Success)
                {
                    status = $"Create lobby failed: {data.ResultCode}";
                    Debug.LogError(status);
                    return;
                }

                lobbyId = data.LobbyId;
                isHost = true;

                status = "Lobby created. Starting NGO Host...";
                Debug.Log($"[EOS] Lobby created: {lobbyId}");

                if (!networkBridge.StartHost())
                    BeginLeavingLobby("NGO Host failed to start.");
            });
    }

    public void JoinLobby(string targetLobbyId)
    {
        if (!IsReady() || IsInLobby || isBusy || networkBridge == null ||
            networkBridge.State != EOSNetworkBridge.ConnectionState.Disconnected)
            return;

        if (string.IsNullOrWhiteSpace(targetLobbyId))
        {
            status = "Please enter a Lobby ID.";
            return;
        }

        isBusy = true;
        forceMenuVisible = false;
        status = "Joining EOS lobby...";

        var options = new JoinLobbyByIdOptions
        {
            LocalUserId = loginManager.LocalUserId,
            LobbyId = targetLobbyId.Trim(),
            PresenceEnabled = false
        };

        lobbyInterface.JoinLobbyById(ref options, null,
            (ref JoinLobbyByIdCallbackInfo data) =>
            {
                isBusy = false;

                if (data.ResultCode != Result.Success)
                {
                    status = $"Join lobby failed: {data.ResultCode}";
                    Debug.LogError(status);
                    return;
                }

                lobbyId = data.LobbyId;
                isHost = false;

                status = "Lobby joined. Connecting to NGO Host...";
                Debug.Log($"[EOS] Joined lobby: {lobbyId}");

                if (!networkBridge.StartClient(lobbyId))
                    BeginLeavingLobby("Failed to start NGO Client.");
            });
    }

    private void OnNetworkStateChanged(
        EOSNetworkBridge.ConnectionState state, string message)
    {
        if (isBusy || !IsInLobby)
            return;

        if (state == EOSNetworkBridge.ConnectionState.Connected)
        {
            forceMenuVisible = false;

            status = isHost
                ? "Host running. Waiting for players."
                : "Connected to Host successfully!";
        }
        else if (state == EOSNetworkBridge.ConnectionState.Connecting ||
                 state == EOSNetworkBridge.ConnectionState.Disconnecting)
        {
            status = message;
        }
    }

    private void OnNetworkFailed(string reason)
    {
        Debug.LogError($"[Barkeeper] Network failure: {reason}");

        forceMenuVisible = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (IsInLobby && !isBusy)
        {
            BeginLeavingLobby($"Connection failed: {reason}");
        }
        else
        {
            status = $"Connection failed: {reason}";
        }
    }

    public void LeaveLobby()
    {
        BeginLeavingLobby("Left lobby successfully.");
    }

    private void BeginLeavingLobby(string completedStatus)
    {
        if (!IsInLobby || isBusy)
            return;

        isBusy = true;
        status = "Leaving lobby...";

        networkBridge?.Shutdown();

        if (!IsReady())
        {
            ResetLocalLobby($"{completedStatus} (EOS unavailable)");
            return;
        }

        if (isHost)
        {
            var options = new DestroyLobbyOptions
            {
                LocalUserId = loginManager.LocalUserId,
                LobbyId = lobbyId
            };

            lobbyInterface.DestroyLobby(ref options, null,
                (ref DestroyLobbyCallbackInfo data) =>
                {
                    HandleLeaveResult(data.ResultCode, completedStatus);
                });
        }
        else
        {
            var options = new LeaveLobbyOptions
            {
                LocalUserId = loginManager.LocalUserId,
                LobbyId = lobbyId
            };

            lobbyInterface.LeaveLobby(ref options, null,
                (ref LeaveLobbyCallbackInfo data) =>
                {
                    HandleLeaveResult(data.ResultCode, completedStatus);
                });
        }
    }

    private void HandleLeaveResult(Result result, string completedStatus)
    {
        isBusy = false;

        if (result != Result.Success && result != Result.NotFound)
        {
            status = $"Leave lobby failed: {result}. Try again.";
            Debug.LogError(status);
            return;
        }

        ResetLocalLobby(completedStatus);
    }

    private void ResetLocalLobby(string message)
    {
        isBusy = false;
        lobbyId = "";
        isHost = false;

        status = message;
        Debug.Log($"[Barkeeper] {message}");
    }

    private void OnGUI()
    {
        if (pauseMenu != null && !pauseMenu.IsOpen && !forceMenuVisible)
            return;

        float scaleX = Screen.width / 1920f;
        float scaleY = Screen.height / 1080f;
        float uiScale = Mathf.Clamp(Mathf.Min(scaleX, scaleY), 0.75f, 2f);

        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(
            new Vector3(uiScale, uiScale, 1f)) * previousMatrix;

        GUILayout.BeginArea(new Rect(15, 15, 510, 385), GUI.skin.box);

        GUILayout.Label("BARKEEPER - EOS MULTIPLAYER");
        GUILayout.Space(10);

        bool loggedIn = loginManager != null && loginManager.IsLoggedIn;

        GUILayout.Label($"EOS: {(loggedIn ? "Connected" : "Not connected")}");
        GUILayout.Label($"Status: {status}", GUILayout.MinHeight(45));

        if (networkBridge != null)
            GUILayout.Label($"NGO: {networkBridge.State}");

        if (networkBridge != null &&
            networkBridge.State == EOSNetworkBridge.ConnectionState.Connected &&
            playerRoster != null)
        {
            GUILayout.Label($"Players: {playerRoster.PlayerCount}/{playerRoster.MaxPlayers}");
            GUILayout.Label($"Last Event: {playerRoster.LastEvent}");
        }

        GUILayout.Space(10);

        bool previousEnabled = GUI.enabled;

        if (!IsInLobby)
        {
            GUI.enabled = loggedIn && !isBusy &&
                networkBridge != null &&
                networkBridge.State == EOSNetworkBridge.ConnectionState.Disconnected;

            if (GUILayout.Button("Create Lobby", GUILayout.Height(30)))
                CreateLobby();

            GUILayout.Label("Lobby ID:");
            joinLobbyId = GUILayout.TextField(joinLobbyId);

            if (GUILayout.Button("Join Lobby", GUILayout.Height(30)))
                JoinLobby(joinLobbyId);
        }
        else
        {
            GUILayout.Label(isHost ? "Role: HOST" : "Role: CLIENT");
            GUILayout.Label("Current Lobby ID:");

            GUILayout.TextField(lobbyId);

            GUI.enabled = !isBusy;

            if (GUILayout.Button("Leave Lobby", GUILayout.Height(30)))
                LeaveLobby();
        }

        GUI.enabled = previousEnabled;

        GUILayout.EndArea();
        GUI.matrix = previousMatrix;
    }

    private void OnDestroy()
    {
        if (networkBridge == null)
            return;

        networkBridge.StateChanged -= OnNetworkStateChanged;
        networkBridge.ConnectionFailed -= OnNetworkFailed;
    }
}
