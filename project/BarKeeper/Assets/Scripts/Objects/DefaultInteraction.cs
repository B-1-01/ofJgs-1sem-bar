using UnityEngine;

public class DefaultInteraction : InteractableObject
{
    public override void Interact(FPSPlayerController player)
    {
        // 1. Faz o print de debug local quando primes E no cubo:
        Debug.Log($"[Debug Cube] {player.OwnerClientId} interacted with {gameObject.name} (Name: {GetInteractionName()}, Mode: {Type})!");

        // 2. Chama o Interact da classe base (InteractableObject) para executar o Pickup ou Station no Host!
        base.Interact(player);
    }
}