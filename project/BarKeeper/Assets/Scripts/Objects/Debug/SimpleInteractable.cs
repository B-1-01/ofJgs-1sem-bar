using UnityEngine;

public class SimpleInteractable : Interactable
{

    [Header("Day Control")]
    [SerializeField] private bool isDayControl;
    [SerializeField] private BarDayUI barDayUI;

    public override void Interact(FPSPlayerController player)
    {

        if (isDayControl)
        {
            if (barDayUI == null)
            {
                Debug.LogError("[SimpleInteractable] BarDayUI não está atribuída!", this);
                return;
            }

            barDayUI.OpenDayControlPanel(player);
            return;
        }

        Debug.Log($"[Sucesso] Interagiu com {gameObject.name} à distância válida!");
    }
}