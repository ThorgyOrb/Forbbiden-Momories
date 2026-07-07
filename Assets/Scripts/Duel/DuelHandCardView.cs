using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UNA carta de la mano (UI 2D sobre el campo 3D). Muestra la carta COMPLETA
/// con el <see cref="CardDisplay"/> del prefab Card, envuelta en un botón para
/// seleccionarla. Al posar el puntero sobre ella dispara <see cref="OnHover"/>
/// para que la barra de información inferior muestre sus datos (estilo FM).
///
/// El DuelScreen clona la plantilla por cada carta en mano.
/// </summary>
public class DuelHandCardView : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private Button button;
    [SerializeField] private GameObject highlight;

    private CardDisplay _display;

    /// <summary>Carta que representa esta vista.</summary>
    public CardData Card { get; private set; }

    /// <summary>Se dispara al posar el puntero (para la barra de info).</summary>
    public Action<CardData> OnHover;

    public Button Button => button;

    public void Setup(CardData card)
    {
        Card = card;
        if (_display == null) _display = GetComponentInChildren<CardDisplay>(true);
        if (_display != null)
        {
            _display.Setup(card);
            _display.SetPosition(CardPosition.FaceUpAttack); // en mano siempre visible
        }
    }

    public void SetHighlight(bool on)
    {
        if (highlight != null) highlight.SetActive(on);
    }

    /// <summary>Voltea la carta (alzada al centro) entre boca arriba y boca abajo.</summary>
    public void SetFace(bool faceDown)
    {
        if (_display == null) _display = GetComponentInChildren<CardDisplay>(true);
        if (_display != null)
            _display.SetPosition(faceDown ? CardPosition.FaceDownAttack : CardPosition.FaceUpAttack);
    }

    // ── Número de orden en la lista de fusión (↑ en la mano) ─────────────

    private TextMeshProUGUI _fusionBadge;

    public void ShowFusionBadge(int order)
    {
        if (_fusionBadge == null)
        {
            var go = new GameObject("FusionBadge", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _fusionBadge = go.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) _fusionBadge.font = TMP_Settings.defaultFontAsset;
            _fusionBadge.fontSize = 88;
            _fusionBadge.fontStyle = FontStyles.Bold;
            _fusionBadge.color = new Color(1f, 0.32f, 0.25f);
            _fusionBadge.alignment = TextAlignmentOptions.Center;
            _fusionBadge.raycastTarget = false;
            var rt = _fusionBadge.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.72f);
            rt.sizeDelta = new Vector2(110, 110);
        }
        _fusionBadge.transform.SetAsLastSibling();
        _fusionBadge.gameObject.SetActive(true);
        _fusionBadge.text = order.ToString();
    }

    public void HideFusionBadge()
    {
        if (_fusionBadge != null) _fusionBadge.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnHover?.Invoke(Card);
    }
}
