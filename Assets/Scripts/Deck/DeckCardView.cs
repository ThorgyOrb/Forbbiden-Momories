using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila del MAZO en el constructor: miniatura de la carta, nombre, estrellas de
/// nivel (solo monstruos) y el contador de copias "x N". Un acento de color a la
/// izquierda indica la categoría (monstruo/magia/trampa/equipo). Al hacer clic se
/// selecciona la carta en el panel central para añadir/quitar copias.
/// </summary>
public class DeckCardView : MonoBehaviour
{
    [SerializeField] private Image art;
    [SerializeField] private Image categoryAccent;          // barra de color por categoría
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;     // "x3"
    [SerializeField] private RectTransform starsContainer;  // pips de nivel
    [SerializeField] private Sprite starSprite;
    [SerializeField] private Button selectButton;
    [SerializeField] private GameObject selectionHighlight;

    private static readonly Color StarColor = new Color(1f, 0.84f, 0.2f);

    private int _cardId;
    public int CardId => _cardId;

    public void Setup(CardData card, int count, bool selected, Action onClick)
    {
        _cardId = card.cardId;

        if (art != null)
        {
            art.sprite = card.artwork;
            art.enabled = card.artwork != null;
        }

        if (categoryAccent != null)
            categoryAccent.color = AccentColor(card);

        if (nameText != null) nameText.text = card.cardName;
        if (countText != null) countText.text = $"x{count}";

        RebuildStars(card.IsMonster ? card.stars : 0);
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

    private static Color AccentColor(CardData card)
    {
        if (card.IsMonster) return CardStyleKemet.FrameLegendary; // oro
        return CardStyleKemet.BadgeColor(card.cardCategory);
    }

    /// <summary>Fila de estrellas de nivel a tamaño fijo (nivel 8 ⇒ 8 estrellas).</summary>
    private void RebuildStars(int level)
    {
        if (starsContainer == null) return;

        for (int i = starsContainer.childCount - 1; i >= 0; i--)
            Destroy(starsContainer.GetChild(i).gameObject);

        level = Mathf.Clamp(level, 0, 12);
        if (level == 0) return;

        const float size = 13f, gap = 1.5f;
        float step = size + gap;

        for (int i = 0; i < level; i++)
        {
            var go = new GameObject("Star", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(starsContainer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(i * step, 0f);

            var img = go.GetComponent<Image>();
            img.sprite = starSprite;
            img.color = StarColor;
            img.raycastTarget = false;
            img.preserveAspect = true;
        }
    }
}
