using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aplica el estilo "Neo-Kemet" a una carta según su rareza:
///   • Common    — marco pizarra, sin efectos.
///   • Rare      — marco turquesa + aurora fría.
///   • Epic      — marco violeta + aurora + foil de interferencia + barrido dorado.
///   • Legendary — marco de circuito dorado (pulsos turquesa) + todo lo anterior
///                 + relieve circuito-jeroglífico (pseudo normal map) + chispas.
///
/// Los colores/intensidades del estilo viven en <see cref="CardStyleKemet"/>
/// (código, no prefab). Este componente solo decide QUÉ capas enciende.
/// </summary>
[RequireComponent(typeof(CardDisplay))]
public class CardHoloEffect : MonoBehaviour
{
    [Header("Material único (usa _RarityMode para diferenciar)")]
    [SerializeField] private Material matHolo;

    [Header("Marco de la carta")]
    [SerializeField] private Image frameBorder;

    [Header("Config")]
    [SerializeField] private float smoothSpeed = 6f;

    [Header("Borde Legendary (circuito Neo-Kemet)")]
    [SerializeField] private Material matRainbowBorder;      // shader YGO/RainbowBorderShader en modo duotono
    [SerializeField] private float borderPulseSpeed = 0.55f; // velocidad del pulso que recorre el marco
    [SerializeField] private float borderPulseCount = 2f;    // pulsos simultáneos alrededor del marco
    [SerializeField] private float borderBrightness = 1f;

    private Material _instanceMat;
    private Material _instanceBorderMat;
    private Image _targetImage;
    private CardRarity _currentRarity;

    // Escala global de la intensidad de los efectos para ESTA carta (1 = pleno).
    // Se baja en cartas grandes (visor del modal, mesa de duelo) para que el
    // efecto no se sienta excesivo por cubrir tanta área; el grid queda en 1.
    private float _intensityScale = 1f;

    private static readonly int PropRarityMode     = Shader.PropertyToID("_RarityMode");
    private static readonly int PropAuroraStrength = Shader.PropertyToID("_AuroraStrength");
    private static readonly int PropAuroraColorA   = Shader.PropertyToID("_AuroraColorA");
    private static readonly int PropAuroraColorB   = Shader.PropertyToID("_AuroraColorB");
    private static readonly int PropAuroraTint     = Shader.PropertyToID("_AuroraTintAmount");
    private static readonly int PropAuroraIntens   = Shader.PropertyToID("_AuroraIntensity");
    private static readonly int PropGlareStrength  = Shader.PropertyToID("_GlareStrength");
    private static readonly int PropGlareIntens    = Shader.PropertyToID("_GlareIntensity");
    private static readonly int PropGlareTint      = Shader.PropertyToID("_GlareTint");
    private static readonly int PropFoilStrength   = Shader.PropertyToID("_FoilStrength");
    private static readonly int PropFoilIntens     = Shader.PropertyToID("_FoilIntensity");
    private static readonly int PropFoilScale      = Shader.PropertyToID("_FoilStripeScale");
    private static readonly int PropFoilDuoA       = Shader.PropertyToID("_FoilDuoA");
    private static readonly int PropFoilDuoB       = Shader.PropertyToID("_FoilDuoB");
    private static readonly int PropReliefStrength = Shader.PropertyToID("_ReliefStrength");
    private static readonly int PropReliefIntens   = Shader.PropertyToID("_ReliefIntensity");
    private static readonly int PropReliefScale    = Shader.PropertyToID("_ReliefScale");
    private static readonly int PropSparkleStrength = Shader.PropertyToID("_SparkleStrength");
    private static readonly int PropSparkleColor    = Shader.PropertyToID("_SparkleColor");
    private static readonly int PropParallax       = Shader.PropertyToID("_ParallaxStrength");
    private static readonly int PropMetal          = Shader.PropertyToID("_MetalStrength");
    private static readonly int PropScan           = Shader.PropertyToID("_ScanStrength");
    private static readonly int PropRays           = Shader.PropertyToID("_RayStrength");
    private static readonly int PropGlitch         = Shader.PropertyToID("_GlitchStrength");
    private static readonly int PropTiltX          = Shader.PropertyToID("_TiltX");
    private static readonly int PropTiltY          = Shader.PropertyToID("_TiltY");

    private static readonly int PropBorderSpeed      = Shader.PropertyToID("_Speed");
    private static readonly int PropBorderBandCount  = Shader.PropertyToID("_BandCount");
    private static readonly int PropBorderBrightness = Shader.PropertyToID("_Brightness");
    private static readonly int PropBorderDuotone    = Shader.PropertyToID("_Duotone");
    private static readonly int PropBorderDuoA       = Shader.PropertyToID("_DuoColorA");
    private static readonly int PropBorderDuoB       = Shader.PropertyToID("_DuoColorB");

    void Awake()
    {
        if (frameBorder == null)
        {
            Transform f = transform.Find("Frame");
            if (f != null) frameBorder = f.GetComponent<Image>();
        }
    }

    public void Apply(CardRarity rarity, Image targetImage)
    {
        _currentRarity = rarity;
        _targetImage = targetImage;

        if (_targetImage == null) return;

        ApplyFrame(rarity);

        // Common no usa material holo
        if (rarity == CardRarity.Common || matHolo == null)
        {
            _targetImage.material = null;
            if (_instanceMat != null) Destroy(_instanceMat);
            _instanceMat = null;
            return;
        }

        if (_instanceMat != null) Destroy(_instanceMat);
        _instanceMat = new Material(matHolo);

        int rarityMode = rarity switch
        {
            CardRarity.Rare => 1,
            CardRarity.Epic => 2,
            CardRarity.Legendary => 3,
            _ => 0
        };
        _instanceMat.SetFloat(PropRarityMode, rarityMode);

        // Colores de aurora según rareza (paleta Neo-Kemet)
        Color auroraA, auroraB;
        switch (rarity)
        {
            case CardRarity.Rare:
                auroraA = CardStyleKemet.AuroraARare; auroraB = CardStyleKemet.AuroraBRare;
                break;
            case CardRarity.Epic:
                auroraA = CardStyleKemet.AuroraAEpic; auroraB = CardStyleKemet.AuroraBEpic;
                break;
            case CardRarity.Legendary:
                auroraA = CardStyleKemet.AuroraALegendary; auroraB = CardStyleKemet.AuroraBLegendary;
                break;
            default:
                auroraA = Color.white; auroraB = Color.white;
                break;
        }
        _instanceMat.SetColor(PropAuroraColorA, auroraA);
        _instanceMat.SetColor(PropAuroraColorB, auroraB);

        // Tuning del estilo fijado por código: el material asset conserva
        // valores serializados de la versión anterior del shader y no deben
        // arrastrarse a las instancias.
        _instanceMat.SetFloat(PropAuroraTint,   CardStyleKemet.AuroraTintAmount);
        _instanceMat.SetFloat(PropAuroraIntens, CardStyleKemet.AuroraIntensity);
        _instanceMat.SetFloat(PropGlareIntens,  CardStyleKemet.GlareIntensity);
        _instanceMat.SetColor(PropGlareTint,    CardStyleKemet.GlareGold);
        _instanceMat.SetFloat(PropFoilIntens,   CardStyleKemet.FoilIntensity);
        _instanceMat.SetFloat(PropFoilScale,    CardStyleKemet.FoilStripeScale);
        _instanceMat.SetColor(PropFoilDuoA,     CardStyleKemet.FoilGold);
        _instanceMat.SetColor(PropFoilDuoB,     CardStyleKemet.FoilCyan);
        _instanceMat.SetFloat(PropReliefIntens, CardStyleKemet.ReliefIntensity);
        _instanceMat.SetFloat(PropReliefScale,  CardStyleKemet.ReliefScale);
        _instanceMat.SetColor(PropSparkleColor, CardStyleKemet.Sparkle);

        // Capas de "arte vivo": se fijan una vez según la rareza (no hacen
        // fade — el parallax o las scanlines a medias se ven raros).
        float parallax = 0f, metal = 0f, scan = 0f, rays = 0f, glitch = 0f;
        switch (rarity)
        {
            case CardRarity.Rare:
                parallax = 0.5f;
                break;
            case CardRarity.Epic:
                parallax = 0.75f; metal = 1f; scan = 1f;
                break;
            case CardRarity.Legendary:
                parallax = 1f; metal = 1f; scan = 1f; rays = 1f; glitch = 1f;
                break;
        }
        _instanceMat.SetFloat(PropParallax, parallax);
        _instanceMat.SetFloat(PropMetal, metal);
        _instanceMat.SetFloat(PropScan, scan);
        _instanceMat.SetFloat(PropRays, rays);
        _instanceMat.SetFloat(PropGlitch, glitch);

        _instanceMat.SetFloat(PropTiltX, 0f);
        _instanceMat.SetFloat(PropTiltY, 0f);

        // Las capas arrancan en 0 y suben solas en Update().
        _instanceMat.SetFloat(PropAuroraStrength, 0f);
        _instanceMat.SetFloat(PropGlareStrength, 0f);
        _instanceMat.SetFloat(PropFoilStrength, 0f);
        _instanceMat.SetFloat(PropReliefStrength, 0f);
        _instanceMat.SetFloat(PropSparkleStrength, 0f);

        _targetImage.material = _instanceMat;
    }

    /// <summary>
    /// Marco por rareza. Legendary usa el shader de borde en modo circuito
    /// (aro dorado con pulsos turquesa); el resto, tinte plano de la paleta.
    /// </summary>
    private void ApplyFrame(CardRarity rarity)
    {
        if (rarity == CardRarity.Legendary && matRainbowBorder != null && frameBorder != null)
        {
            if (_instanceBorderMat != null) Destroy(_instanceBorderMat);
            _instanceBorderMat = new Material(matRainbowBorder);
            _instanceBorderMat.SetFloat(PropBorderDuotone, 1f);
            _instanceBorderMat.SetColor(PropBorderDuoA, CardStyleKemet.FrameLegendary);
            _instanceBorderMat.SetColor(PropBorderDuoB, CardStyleKemet.FoilCyan);
            _instanceBorderMat.SetFloat(PropBorderSpeed, borderPulseSpeed);
            _instanceBorderMat.SetFloat(PropBorderBandCount, borderPulseCount);
            _instanceBorderMat.SetFloat(PropBorderBrightness, borderBrightness);

            frameBorder.material = _instanceBorderMat;
            frameBorder.color = Color.white; // el color final lo decide el shader
            return;
        }

        if (_instanceBorderMat != null) Destroy(_instanceBorderMat);
        _instanceBorderMat = null;

        if (frameBorder != null)
        {
            frameBorder.material = null;
            frameBorder.color = rarity switch
            {
                CardRarity.Rare => CardStyleKemet.FrameRare,
                CardRarity.Epic => CardStyleKemet.FrameEpic,
                CardRarity.Legendary => CardStyleKemet.FrameLegendary,
                _ => CardStyleKemet.FrameCommon
            };
        }
    }

    public void RefreshTexture()
    {
        if (_instanceMat == null || _targetImage?.sprite == null) return;
        _instanceMat.SetTexture("_MainTex", _targetImage.sprite.texture);
    }

    /// <summary>
    /// Escala la intensidad de los efectos de esta carta (1 = pleno, como el grid).
    /// Se usa para atenuar cartas grandes (visor del modal, mesa de duelo). La
    /// rampa de Update converge suavemente al nuevo valor, así que puede llamarse
    /// tras Setup sin cortes.
    /// </summary>
    public void SetIntensityScale(float scale)
    {
        _intensityScale = Mathf.Clamp(scale, 0f, 1.5f);
    }

    void Update()
    {
        if (_instanceMat == null) return;

        // Capas objetivo por rareza; suben suavemente desde 0 al aparecer.
        // El valor "encendido" es _intensityScale (1 = pleno), para atenuar
        // cartas grandes sin tocar el grid.
        float on = _intensityScale;
        float tAurora = 0f, tGlare = 0f, tFoil = 0f, tRelief = 0f, tSparkle = 0f;

        switch (_currentRarity)
        {
            case CardRarity.Rare:
                tAurora = on;
                break;

            case CardRarity.Epic:
                tAurora = on;
                tGlare = on;
                tFoil = on;
                break;

            case CardRarity.Legendary:
                tAurora = on;
                tGlare = on;
                tFoil = on;
                tRelief = on;
                tSparkle = on;
                break;
        }

        // Se acota Time.deltaTime: al instanciar/mostrar una carta (modal, duelo)
        // hay un tirón de frame, y un delta grande haría que el lerp saltara al
        // máximo de golpe (el "flash" de efectos al abrir). Con el tope, la rampa
        // siempre sube suave en ~1s aunque el frame de aparición se congele.
        float dt = Mathf.Min(Time.deltaTime, 0.033f) * smoothSpeed;
        _instanceMat.SetFloat(PropAuroraStrength,
            Mathf.Lerp(_instanceMat.GetFloat(PropAuroraStrength), tAurora, dt));
        _instanceMat.SetFloat(PropGlareStrength,
            Mathf.Lerp(_instanceMat.GetFloat(PropGlareStrength), tGlare, dt));
        _instanceMat.SetFloat(PropFoilStrength,
            Mathf.Lerp(_instanceMat.GetFloat(PropFoilStrength), tFoil, dt));
        _instanceMat.SetFloat(PropReliefStrength,
            Mathf.Lerp(_instanceMat.GetFloat(PropReliefStrength), tRelief, dt));
        _instanceMat.SetFloat(PropSparkleStrength,
            Mathf.Lerp(_instanceMat.GetFloat(PropSparkleStrength), tSparkle, dt));
    }

    void OnDestroy()
    {
        if (_instanceMat != null) Destroy(_instanceMat);
        if (_instanceBorderMat != null) Destroy(_instanceBorderMat);
    }
}
