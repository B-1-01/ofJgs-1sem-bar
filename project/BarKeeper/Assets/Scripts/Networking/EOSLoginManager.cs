using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using PlayEveryWare.EpicOnlineServices;

public class EOSLoginManager : MonoBehaviour
{
    public ProductUserId LocalUserId { get; private set; }
    public bool IsLoggedIn => LocalUserId != null && LocalUserId.IsValid();

    private void Start()
    {
        if (EOSManager.Instance == null)
        {
            Debug.LogError("EOSManager not found!");
            return;
        }

        CreateDeviceId();
    }

    private void CreateDeviceId()
    {
        var options = new CreateDeviceIdOptions
        {
            DeviceModel = SystemInfo.deviceModel
        };

        EOSManager.Instance.GetEOSConnectInterface().CreateDeviceId(
            ref options, null, OnDeviceIdCreated);
    }

    private void OnDeviceIdCreated(ref CreateDeviceIdCallbackInfo data)
    {
        if (data.ResultCode != Result.Success &&
            data.ResultCode != Result.DuplicateNotAllowed)
        {
            Debug.LogError($"EOS Device ID failed: {data.ResultCode}");
            return;
        }

        Login();
    }

    private void Login()
    {
        EOSManager.Instance.StartConnectLoginWithOptions(
            ExternalCredentialType.DeviceidAccessToken,
            null,
            "BarkeeperPlayer",
            OnLoginCompleted);
    }

    private void OnLoginCompleted(LoginCallbackInfo data)
    {
        if (data.ResultCode == Result.Success)
        {
            LocalUserId = data.LocalUserId;
            Debug.Log($"EOS Login successful! PUID: {LocalUserId}");
            return;
        }

        if (data.ResultCode == Result.InvalidUser)
        {
            EOSManager.Instance.CreateConnectUserWithContinuanceToken(
                data.ContinuanceToken,
                (CreateUserCallbackInfo result) =>
                {
                    if (result.ResultCode == Result.Success)
                        Login();
                    else
                        Debug.LogError($"EOS Create User failed: {result.ResultCode}");
                });

            return;
        }

        Debug.LogError($"EOS Login failed: {data.ResultCode}");
    }
}
