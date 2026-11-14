using UnityEngine;

public class SimpleInteractable : Interactable
{
    public override void Interact(FPSPlayerController player)
    {
        Debug.Log($"[Sucesso] Interagiu com {gameObject.name} à distância válida!");
    }
}