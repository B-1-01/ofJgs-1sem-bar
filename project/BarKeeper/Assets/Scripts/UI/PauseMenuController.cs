
using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject menuPanel;

    [Header("Settings")]
    [SerializeField] private bool startWithMenuOpen = true;

    private FPSPlayerController playerController;

    public bool IsOpen { get; private set; }

    private void Start()
    {
        SetMenuOpen(startWithMenuOpen);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        bool togglePressed = Keyboard.current.escapeKey.wasPressedThisFrame;

#if UNITY_EDITOR
        togglePressed |= Keyboard.current.pKey.wasPressedThisFrame;
#endif

        if (togglePressed)
            ToggleMenu();
    }

    public void RegisterLocalPlayer(FPSPlayerController player)
    {
        playerController = player;
        playerController.SetGameplayInputEnabled(!IsOpen);
    }

    public void UnregisterLocalPlayer(FPSPlayerController player)
    {
        if (playerController == player)
            playerController = null;
    }

    public void ToggleMenu()
    {
        SetMenuOpen(!IsOpen);
    }

    public void SetMenuOpen(bool open)
    {
        IsOpen = open;

        if (menuPanel != null)
            menuPanel.SetActive(open);

        if (playerController != null)
        {
            playerController.SetGameplayInputEnabled(!open);
        }
        else
        {
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }
    }
}
