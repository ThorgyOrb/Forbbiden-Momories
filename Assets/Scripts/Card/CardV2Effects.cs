using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Efectos visuales del prefab de carta nuevo (CardMonsterV2). Solo vive en el prefab
/// V2; CardDisplay lo llama si el componente existe (no-op en el clásico).
///
///   • Viñeta   — oscurece los bordes del arte (SIEMPRE; look base).
///   • Foil     — foil de CRISTAL que cubre TODA la carta (esquirlas iridiscentes +
///                brillo diagonal que las recorre), SOLO en la rareza más alta
///                (ultra rara). Shader YGO/CardV2Foil; se anima solo (_Time).
///
/// El generador de editor (CardMonsterV2PrefabBuilder) crea y cablea las capas.
/// </summary>
public class CardV2Effects : MonoBehaviour
{
    [Header("Capas (las crea el builder)")]
    [SerializeField] private Image vignette; // oscurece bordes del arte (siempre)
    [SerializeField] private Image foil;      // foil de cristal en toda la carta (ultra rara)

    /// <summary>Enciende los efectos según la rareza. Lo llama CardDisplay.Setup.</summary>
    public void Apply(CardRarity rarity)
    {
        // SOLO la ULTRA RARA (rareza más alta) lleva foil. En este enum de 4
        // (Common/Rare/Epic/Legendary) eso es Legendary; el resto sin efectos.
        // (Si se añade una rareza "UltraRara"/"Secreta" al FINAL del enum, mover aquí.)
        bool foilOn = rarity >= CardRarity.Legendary;

        if (vignette != null) vignette.gameObject.SetActive(true);
        if (foil != null) foil.gameObject.SetActive(foilOn);
    }
}
