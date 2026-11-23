using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Outline))]
public class CocktailIngredientSource : MonoBehaviour
{
    [Header("Ingredient Settings")]
    [SerializeField] private IngredientType ingredient = IngredientType.Rum;
    [SerializeField] private bool continuousPour = true;
    [SerializeField] private float pourRatePerSecond = 5f;
    [SerializeField] private float amountPerClick = 1f;

    [Header("Visual Feedback")]
    [SerializeField] private Outline outline;

    public IngredientType Ingredient => ingredient;
    public bool ContinuousPour => continuousPour;
    public float PourRatePerSecond => pourRatePerSecond;
    public float AmountPerClick => amountPerClick;

    private void Awake()
    {
        if (outline == null)
            outline = GetComponent<Outline>();
    }

    private void Start()
    {
        if (outline != null) outline.enabled = false;
    }

    public void SetHighlighted(bool highlighted)
    {
        if (outline != null)
            outline.enabled = highlighted;
    }
}