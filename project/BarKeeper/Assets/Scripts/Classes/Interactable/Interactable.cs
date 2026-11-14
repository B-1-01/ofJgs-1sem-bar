using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Outline))]
public abstract class Interactable : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private string objectName = "Objeto";

    [Header("Visual Feedback")]
    [SerializeField] private Outline outline;

    public virtual string ObjectName => objectName;

    private void Reset()
    {
        outline = GetComponent<Outline>();
        if (outline != null)
        {
            outline.OutlineMode = Outline.Mode.OutlineAll;
            outline.OutlineColor = Color.white;
            outline.OutlineWidth = 4f;
            outline.enabled = false;
        }
    }

    protected virtual void Awake()
    {
        if (outline == null)
            outline = GetComponent<Outline>();

        if (outline != null)
            outline.enabled = false;
    }

    /// <summary>
    /// Ação principal executada pelo jogador.
    /// Futuramente, Stations abrirão minigames e Pickups farão o pickup.
    /// </summary>
    public abstract void Interact(FPSPlayerController player);

    /// <summary>Ativado quando a mira da câmara aponta para o objeto dentro do alcance.</summary>
    public virtual void OnFocusEnter()
    {
        if (outline != null)
            outline.enabled = true;
    }

    /// <summary>Ativado quando o jogador afasta a mira ou sai do alcance.</summary>
    public virtual void OnFocusExit()
    {
        if (outline != null)
            outline.enabled = false;
    }
}