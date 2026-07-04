using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila del MAZO en el constructor: una carta y cuántas copias hay en el mazo,
/// con un botón para quitar una copia. El mazo se muestra agrupado (una fila por
/// carta distinta con su contador "x N").
/// </summary>
public class DeckCardView : MonoBehaviour
{
    [SerializeField] private Image art;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;   // "x3"
    [SerializeField] private Button removeButton;

    public void Setup(CardData card, int count, Action onRemove)
    {
        if (art != null)
        {
            art.sprite = card.artwork;
            art.enabled = card.artwork != null;
        }
        if (nameText != null) nameText.text = card.cardName;
        if (countText != null) countText.text = $"x{count}";

        if (removeButton != null)
        {
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(() => onRemove?.Invoke());
        }
    }
}
