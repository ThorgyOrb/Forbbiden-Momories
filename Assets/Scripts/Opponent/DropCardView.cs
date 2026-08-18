using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una carta dentro de la grilla de "cartas que dropea" del detalle del rival.
/// Si el jugador ya la DESCUBRIÓ, muestra su ilustración, nombre y el borde
/// teñido por RAREZA (misma paleta que las cartas de verdad, ver
/// <see cref="CardStyleKemet"/>); si sigue PENDIENTE, se muestra oculta ("???",
/// borde neutro) para que sepa que aún hay algo por descubrir sin revelar cuál
/// es. La probabilidad de drop (0..1) SÍ se muestra siempre — no es spoiler.
/// </summary>
public class DropCardView : MonoBehaviour
{
    [SerializeField] private Image border;
    [SerializeField] private Image art;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI probabilityText;

    private static readonly Color Hidden = new Color(0.08f, 0.09f, 0.16f, 1f);

    public void Setup(CardData card, bool discovered, float probability)
    {
        if (discovered)
        {
            if (art != null)
            {
                art.sprite = card.Artwork;
                art.color = Color.white;
                art.enabled = art.sprite != null;
            }
            if (nameText != null) nameText.text = card.cardName;
            if (border != null) border.color = CardStyleKemet.FrameColorFor(card.rarity);
        }
        else
        {
            // Pendiente: sin ilustración, caja oscura y "???".
            if (art != null)
            {
                art.sprite = null;
                art.color = Hidden;
                art.enabled = true;
            }
            if (nameText != null) nameText.text = "???";
            if (border != null) border.color = CardStyleKemet.FrameCommon;
        }

        if (probabilityText != null) probabilityText.text = $"{probability * 100f:0.00}%";
    }
}
