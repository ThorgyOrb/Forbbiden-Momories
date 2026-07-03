using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CardDisplay))]
public class CardHoloEffect : MonoBehaviour
{
    [Header("Material único (usa _RarityMode para diferenciar)")]
    [SerializeField] private Material matHolo;

    [Header("Colores de marco por rareza")]
    [SerializeField] private Image frameBorder;
    [SerializeField] private Color colorCommon = new Color(0.67f, 0.67f, 0.67f);
    [SerializeField] private Color colorRare = new Color(0.23f, 0.55f, 0.88f);
    [SerializeField] private Color colorEpic = new Color(0.61f, 0.36f, 0.94f);
    [SerializeField] private Color colorLegendary = new Color(0.83f, 0.63f, 0.09f);

    [Header("Colores de aurora por rareza (par A/B que se mezclan)")]
    [SerializeField] private Color auroraARare = new Color(0.3f, 0.6f, 1.0f);
    [SerializeField] private Color auroraBRare = new Color(0.6f, 0.85f, 1.0f);
    [SerializeField] private Color auroraAEpic = new Color(0.6f, 0.3f, 1.0f);
    [SerializeField] private Color auroraBEpic = new Color(1.0f, 0.4f, 0.8f);
    [SerializeField] private Color auroraALegendary = new Color(1.0f, 0.75f, 0.2f);
    [SerializeField] private Color auroraBLegendary = new Color(1.0f, 0.3f, 0.5f);

    [Header("Config")]
    [SerializeField] private float smoothSpeed = 6f;

    [Header("Borde Legendary (serpiente arcoiris)")]
    [SerializeField] private Material matRainbowBorder;       // usa el shader YGO/RainbowBorderShader
    [SerializeField] private float rainbowBorderSpeed = 0.6f;       // velocidad con la que la serpiente recorre el borde
    [SerializeField] private float rainbowBorderBandCount = 2f;     // veces que se repite el ciclo de color alrededor del marco
    [SerializeField] private float rainbowBorderSaturation = 0.9f;
    [SerializeField] private float rainbowBorderBrightness = 1f;

    private Material _instanceMat;
    private Material _instanceBorderMat;
    private Image _targetImage;
    private CardRarity _currentRarity;
    private Color _baseBorderColor;

    private static readonly int PropRarityMode = Shader.PropertyToID("_RarityMode");
    private static readonly int PropAuroraStrength = Shader.PropertyToID("_AuroraStrength");
    private static readonly int PropAuroraColorA = Shader.PropertyToID("_AuroraColorA");
    private static readonly int PropAuroraColorB = Shader.PropertyToID("_AuroraColorB");
    private static readonly int PropGlareStrength = Shader.PropertyToID("_GlareStrength");
    private static readonly int PropSparkleStrength = Shader.PropertyToID("_SparkleStrength");
    private static readonly int PropTiltX = Shader.PropertyToID("_TiltX");
    private static readonly int PropTiltY = Shader.PropertyToID("_TiltY");

    private static readonly int PropBorderSpeed = Shader.PropertyToID("_Speed");
    private static readonly int PropBorderBandCount = Shader.PropertyToID("_BandCount");
    private static readonly int PropBorderSaturation = Shader.PropertyToID("_Saturation");
    private static readonly int PropBorderBrightness = Shader.PropertyToID("_Brightness");

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

        // Color base del marco (rarezas que no son Legendary usan tinte plano)
        _baseBorderColor = rarity switch
        {
            CardRarity.Common => colorCommon,
            CardRarity.Rare => colorRare,
            CardRarity.Epic => colorEpic,
            CardRarity.Legendary => colorLegendary,
            _ => colorCommon
        };

        if (rarity == CardRarity.Legendary && matRainbowBorder != null && frameBorder != null)
        {
            // Serpiente arcoiris: el color varía a lo largo del borde y avanza
            // solo con el tiempo, todo calculado por pixel dentro del shader.
            if (_instanceBorderMat != null) Destroy(_instanceBorderMat);
            _instanceBorderMat = new Material(matRainbowBorder);
            _instanceBorderMat.SetFloat(PropBorderSpeed, rainbowBorderSpeed);
            _instanceBorderMat.SetFloat(PropBorderBandCount, rainbowBorderBandCount);
            _instanceBorderMat.SetFloat(PropBorderSaturation, rainbowBorderSaturation);
            _instanceBorderMat.SetFloat(PropBorderBrightness, rainbowBorderBrightness);

            frameBorder.material = _instanceBorderMat;
            frameBorder.color = Color.white; // el color final lo decide el shader, no el tinte
        }
        else
        {
            if (_instanceBorderMat != null) Destroy(_instanceBorderMat);
            _instanceBorderMat = null;
            if (frameBorder != null)
            {
                frameBorder.material = null;
                frameBorder.color = _baseBorderColor;
            }
        }

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

        // Colores de aurora segun rareza
        Color auroraA, auroraB;
        switch (rarity)
        {
            case CardRarity.Rare:
                auroraA = auroraARare; auroraB = auroraBRare;
                break;
            case CardRarity.Epic:
                auroraA = auroraAEpic; auroraB = auroraBEpic;
                break;
            case CardRarity.Legendary:
                auroraA = auroraALegendary; auroraB = auroraBLegendary;
                break;
            default:
                auroraA = Color.white; auroraB = Color.white;
                break;
        }
        _instanceMat.SetColor(PropAuroraColorA, auroraA);
        _instanceMat.SetColor(PropAuroraColorB, auroraB);

        // El glare ya no sigue al mouse: queda fijo en el centro de la carta.
        _instanceMat.SetFloat(PropTiltX, 0f);
        _instanceMat.SetFloat(PropTiltY, 0f);

        // Arrancan en 0 y suben solos en Update(), pero ya no dependen del mouse.
        _instanceMat.SetFloat(PropAuroraStrength, 0f);
        _instanceMat.SetFloat(PropGlareStrength, 0f);
        _instanceMat.SetFloat(PropSparkleStrength, 0f);

        _targetImage.material = _instanceMat;
    }

    public void RefreshTexture()
    {
        if (_instanceMat == null || _targetImage?.sprite == null) return;
        _instanceMat.SetTexture("_MainTex", _targetImage.sprite.texture);
    }

    void Update()
    {
        if (_instanceMat == null) return;

        // Los efectos ya no dependen del mouse ni de ningún tilt de la carta:
        // siempre están activos y son estáticos en cuanto a su posición/disparo.
        float tAurora = 0f, tGlare = 0f, tSparkle = 0f;

        switch (_currentRarity)
        {
            case CardRarity.Rare:
                tAurora = 1.0f;
                break;

            case CardRarity.Epic:
                tAurora = 1.0f;
                tGlare = 1.0f;
                break;

            case CardRarity.Legendary:
                tAurora = 1.0f;
                tGlare = 1.0f;
                tSparkle = 1.0f;
                break;
        }

        float dt = Time.deltaTime * smoothSpeed;
        _instanceMat.SetFloat(PropAuroraStrength,
            Mathf.Lerp(_instanceMat.GetFloat(PropAuroraStrength), tAurora, dt));
        _instanceMat.SetFloat(PropGlareStrength,
            Mathf.Lerp(_instanceMat.GetFloat(PropGlareStrength), tGlare, dt));
        _instanceMat.SetFloat(PropSparkleStrength,
            Mathf.Lerp(_instanceMat.GetFloat(PropSparkleStrength), tSparkle, dt));
    }

    void OnDestroy()
    {
        if (_instanceMat != null) Destroy(_instanceMat);
        if (_instanceBorderMat != null) Destroy(_instanceBorderMat);
    }
}