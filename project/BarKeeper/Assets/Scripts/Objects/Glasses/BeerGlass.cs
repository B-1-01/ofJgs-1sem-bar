using UnityEngine;

public enum BeerSize
{
    Small = 20,       // 20 cl
    Medium = 33,      // 33 cl
    Large = 50,       // 50 cl
    ExtraLarge = 100  // 100 cl (1 Litro)
}

public enum FoamLevel
{
    NoFoam,       // < 5% (sem espuma / choca)
    LowFoam,      // 5% - 15% (pouca espuma)
    IdealFoam,    // 15% - 25% (perfeito: 2 dedos de colarinho)
    HighFoam,     // 25% - 40% (muita espuma)
    ExcessiveFoam // > 40% (quase só espuma)
}

public class BeerGlass : DrinkContainer
{
    [Header("Beer Configuration")]
    [SerializeField] private BeerSize beerSize = BeerSize.Medium;

    [Header("Visual Representation")]
    [Tooltip("Arrasta aqui o MeshRenderer do cilindro interior 'Drink'")]
    [SerializeField] private MeshRenderer drinkRenderer;

    [Header("Scene View / Inspector Preview")]
    [Tooltip("Ativa para testar valores diretamente no Scene View / Inspector")]
    [SerializeField] private bool useDebugOverride = false;
    [Range(0f, 100f)]
    [SerializeField] private float debugSetBeerCl = 0f;
    [Range(0f, 100f)]
    [SerializeField] private float debugSetFoamCl = 0f;

    [Header("Live Readouts (Inspector Debug)")]
    [SerializeField] private float debugBeerCl;
    [SerializeField] private float debugMixerCl;
    [SerializeField] private float debugFoamCl;
    [SerializeField] private float debugFoamRatio;
    [SerializeField] private FoamLevel debugFoamLevel;

    // Cache dos IDs de propriedades do shader para performance máxima
    private static readonly int LiquidPropId = Shader.PropertyToID("_LiquidAmount");
    private static readonly int FoamPropId = Shader.PropertyToID("_FoamAmount");
    private MaterialPropertyBlock propBlock;

    public BeerSize Size => beerSize;

    // Acesso direto aos líquidos presentes no copo
    public float BeerAmount => GetIngredientAmount(IngredientType.Beer);
    public float MixerAmount => LiquidAmount - BeerAmount; // ex: 7Up, Cola, etc.

    /// <summary>
    /// Rácio de espuma em relação ao volume total no copo (0.0 a 1.0).
    /// </summary>
    public float FoamFactor => TotalVolume > 0f ? (FoamAmount / TotalVolume) : 0f;

    /// <summary>
    /// Nível qualitativo da espuma para validação de pedidos dos clientes.
    /// </summary>
    public FoamLevel FoamLevel => EvaluateFoamLevel();

    protected override void Awake()
    {
        base.Awake();
        // A capacidade máxima do copo é definida pelo tamanho selecionado
        maxCapacityCl = (float)beerSize;
        propBlock = new MaterialPropertyBlock();
    }

    private void OnValidate()
    {
        maxCapacityCl = (float)beerSize;
        propBlock ??= new MaterialPropertyBlock();
        UpdateVisuals();
    }

    protected override void Update()
    {
        base.Update();

        // 1. Atualiza as variáveis de debug visíveis no Inspector
        debugBeerCl = BeerAmount;
        debugMixerCl = MixerAmount;
        debugFoamCl = FoamAmount;
        debugFoamRatio = FoamFactor;
        debugFoamLevel = FoamLevel;

        // 2. Atualiza os níveis visuais no shader do cilindro interior
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (drinkRenderer == null) return;

        float capacity = MaxCapacityCl;
        float beer = useDebugOverride ? debugSetBeerCl : BeerAmount;
        float foam = useDebugOverride ? debugSetFoamCl : FoamAmount;

        float liquidNorm = capacity > 0f ? Mathf.Clamp01(beer / capacity) : 0f;
        float foamNorm = capacity > 0f ? Mathf.Clamp01(foam / capacity) : 0f;

        propBlock ??= new MaterialPropertyBlock();
        drinkRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(LiquidPropId, liquidNorm);
        propBlock.SetFloat(FoamPropId, foamNorm);
        drinkRenderer.SetPropertyBlock(propBlock);
    }

    private FoamLevel EvaluateFoamLevel()
    {
        float ratio = FoamFactor;
        if (FoamAmount < 0.2f || ratio < 0.05f) return FoamLevel.NoFoam;
        if (ratio < 0.15f) return FoamLevel.LowFoam;
        if (ratio <= 0.25f) return FoamLevel.IdealFoam;
        if (ratio <= 0.40f) return FoamLevel.HighFoam;
        return FoamLevel.ExcessiveFoam;
    }
}