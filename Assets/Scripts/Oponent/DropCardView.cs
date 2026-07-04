using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una carta dentro de la grilla de "cartas que dropea" del detalle del rival.
/// Si el jugador ya la DESCUBRIÓ, muestra su ilustración y nombre; si sigue
/// PENDIENTE, se muestra oculta ("???") para que sepa que aún hay algo por
/// descubrir sin revelar cuál es.
/// </summary>
public class DropCardView : MonoBehaviour
{
    [SerializeField] private Image art;
    [SerializeField] private TextMeshProUGUI nameText;

    private static readonly Color Hidden = new Color(0.08f, 0.09f, 0.16f, 1f);

    public void Setup(CardData card, bool discovered)
    {
        if (discovered)
        {
            if (art != null)
            {
                art.sprite = card.artwork;
                art.color = Color.white;
                art.enabled = card.artwork != null;
            }
            if (nameText != null) nameText.text = card.cardName;
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
        }
    }
}
