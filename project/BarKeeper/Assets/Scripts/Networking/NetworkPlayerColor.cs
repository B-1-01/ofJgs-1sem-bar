using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerColor : NetworkBehaviour
{
    [SerializeField] private Renderer bodyRenderer;

    private readonly NetworkVariable<Color> playerColor = new(
        Color.white,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponent<Renderer>();
    }

    public override void OnNetworkSpawn()
    {
        playerColor.OnValueChanged += OnColorChanged;

        if (IsServer)
            playerColor.Value = Color.HSVToRGB(Random.value, 0.8f, 1f);

        ApplyColor(playerColor.Value);
    }

    public override void OnNetworkDespawn()
    {
        playerColor.OnValueChanged -= OnColorChanged;
    }

    private void OnColorChanged(Color previous, Color current)
    {
        ApplyColor(current);
    }

    private void ApplyColor(Color color)
    {
        if (bodyRenderer != null)
            bodyRenderer.material.color = color;
    }
}