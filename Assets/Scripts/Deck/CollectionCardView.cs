using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Baldosa de la COLECCIÓN en el Constructor de Deck: muestra la carta completa
/// (usando el prefab Card / <see cref="CardDisplay"/>) con una insignia de copias
/// poseídas y otra de copias ya metidas en el mazo. Al hacer clic la selecciona
/// para inspeccionarla en el panel central (donde se añade/quita).
/// </summary>
public class CollectionCardView : MonoBehaviour
{
    [SerializeField] private CardDisplay cardDisplay;
    [SerializeField] private TextMeshProUGUI copiesBadge;    // "x3" copias poseídas
    [SerializeField] private TextMeshProUGUI inDeckBadge;    // "EN MAZO 2"
    [SerializeField] private Button selectButton;
    [SerializeField] private GameObject selectionHighlight;  // marco al estar seleccionada
    [SerializeField] private GameObject maxedOverlay;        // velo al alcanzar el tope en el mazo

    private int _cardId;
    public int CardId => _cardId;

    public void Setup(CardData card, int owned, int inDeck, int cap, bool selected, Action onClick)
    {
        _cardId = card.cardId;

        if (cardDisplay != null)
            cardDisplay.Setup(card);

        if (copiesBadge != null) copiesBadge.text = $"x{owned}";

        if (inDeckBadge != null)
        {
            bool show = inDeck > 0;
            inDeckBadge.gameObject.SetActive(show);
            if (show) inDeckBadge.text = $"EN MAZO {inDeck}";
        }

        if (maxedOverlay != null)
            maxedOverlay.SetActive(inDeck >= cap && cap > 0);

        SetSelected(selected);

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onClick?.Invoke());
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null) selectionHighlight.SetActive(selected);
    }
}
