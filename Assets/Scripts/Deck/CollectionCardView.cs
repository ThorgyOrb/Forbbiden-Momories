using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tarjeta de la lista de COLECCIÓN en el constructor de mazos: muestra una carta
/// que el jugador posee, cuántas copias tiene y cuántas ya están en el mazo, con
/// un botón para añadir una copia (deshabilitado si ya metió todas las suyas).
/// </summary>
public class CollectionCardView : MonoBehaviour
{
    [SerializeField] private Image art;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI infoText;    // tipo · ATK/DEF, o categoría
    [SerializeField] private TextMeshProUGUI countText;   // "Copias: 3   En mazo: 1"
    [SerializeField] private Button addButton;

    public void Setup(CardData card, int ownedCopies, int inDeck, Action onAdd)
    {
        if (art != null)
        {
            art.sprite = card.artwork;
            art.enabled = card.artwork != null;
        }
        if (nameText != null) nameText.text = card.cardName;
        if (infoText != null) infoText.text = Info(card);
        if (countText != null) countText.text = $"Copias: {ownedCopies}    En mazo: {inDeck}";

        if (addButton != null)
        {
            addButton.onClick.RemoveAllListeners();
            addButton.onClick.AddListener(() => onAdd?.Invoke());
            addButton.interactable = inDeck < ownedCopies;
        }
    }

    private static string Info(CardData card)
    {
        if (card.IsMonster)
            return $"{card.monsterType} · ATK {card.baseAtk} / DEF {card.baseDef}";
        return card.CategoryLabel;
    }
}
