using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using PlayEveryWare.EpicOnlineServices;

public class EOSLoginManager : MonoBehaviour
{
    public ProductUserId LocalUserId { get; private set; }
    public bool IsLoggedIn => LocalUserId != null && LocalUserId.IsValid();

    /// <summary>Inicia o processo de autenticação no EOS através de uma identidade associada ao dispositivo.</summary>
    private void Start()
    {
        if (EOSManager.Instance == null)
        {
            Debug.LogError("EOSManager not found!");
            return;
        }

        CreateDeviceId();
    }

    /// <summary>Solicita ao EOS a criação de um identificador de dispositivo para permitir autenticação sem uma conta Epic.</summary>
    private void CreateDeviceId()
    {
        var options = new CreateDeviceIdOptions
        {
            DeviceModel = SystemInfo.deviceModel
        };

        EOSManager.Instance.GetEOSConnectInterface().CreateDeviceId(
            ref options, null, OnDeviceIdCreated);
    }

    /// <summary>Verifica o resultado da criação do Device ID e inicia o login caso tenha sido criado ou já exista.</summary>
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

    /// <summary>Inicia a autenticação no EOS Connect utilizando as credenciais associadas ao dispositivo.</summary>
    private void Login()
    {
        EOSManager.Instance.StartConnectLoginWithOptions(
            ExternalCredentialType.DeviceidAccessToken,
            null,
            "BarkeeperPlayer",
            OnLoginCompleted);
    }

    /// <summary>Processa o resultado da autenticação, guardando o Product User ID ou criando um novo utilizador quando necessário.</summary>
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
