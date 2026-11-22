using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class CocktailPrepStation : Station
{
    [Header("Cocktail Station Setup")]
    [SerializeField] private Transform glassRestPoint;
    [SerializeField] private bool requireEmptyHands = false;

    public Transform GlassRestPoint => glassRestPoint;

    protected override bool MeetsStationRequirements(FPSPlayerController player)
    {
        if (requireEmptyHands && player.HasHeldItem)
            return false;

        return base.MeetsStationRequirements(player);
    }

    protected override void OnMinigameStarted()
    {
        base.OnMinigameStarted();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[CocktailStation] Player entered cocktail station.");
    }

    protected override void OnMinigameExited()
    {
        base.OnMinigameExited();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("[CocktailStation] Player exited cocktail station.");
    }

    protected override void Update()
    {
        // base.Update() trata da saída da estação através das teclas Q / E
        base.Update();

        if (!isUsingStationLocally) return;

        // TODO: A logica toda
    }

    private void OnDrawGizmosSelected()
    {
        if (glassRestPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(glassRestPoint.position, 0.15f);
        }
    }
}
