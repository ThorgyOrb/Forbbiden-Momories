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

    /// <summary>CardDisplay interno (prefab Card).</summary>
    public CardDisplay Display
    {
        get
        {
            if (_display == null) _display = GetComponentInChildren<CardDisplay>(true);
            return _display;
        }
    }

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

    /// <summary>Fija el ATK/DEF ACTUALES a mostrar EN la carta (con terreno/equipos/buffs).</summary>
    public void SetCurrentStats(int atk, int def)
    {
        if (Display != null) Display.SetCurrentStats(atk, def);
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

    // ── Marca de la lista de fusión (↑ en la mano) ───────────────────────
    // Un CUADRADO NEGRO (con borde dorado) en la esquina superior derecha de la
    // carta, con el NÚMERO de orden en la pila de fusión.

    private GameObject _fusionBadge;
    private TextMeshProUGUI _fusionBadgeNum;

    public void ShowFusionBadge(int order)
    {
        if (_fusionBadge == null)
        {
            // Cuadrado exterior = borde dorado (para que resalte sobre la carta).
            var go = new GameObject("FusionBadge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var border = go.GetComponent<Image>();
            border.color = new Color(1f, 0.85f, 0.42f);   // oro
            border.raycastTarget = false;
            var rt = border.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);   // esquina superior derecha
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(56f, 56f);
            rt.anchoredPosition = new Vector2(-6f, -6f);          // pisando un poco la esquina

            // Cuadrado interior = relleno NEGRO.
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            var fill = fillGo.GetComponent<Image>();
            fill.color = new Color(0.05f, 0.05f, 0.06f);         // negro
            fill.raycastTarget = false;
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(4f, 4f); frt.offsetMax = new Vector2(-4f, -4f);

            // NÚMERO de orden en la pila de fusión.
            var numGo = new GameObject("Num", typeof(RectTransform));
            numGo.transform.SetParent(go.transform, false);
            _fusionBadgeNum = numGo.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) _fusionBadgeNum.font = TMP_Settings.defaultFontAsset;
            _fusionBadgeNum.fontSize = 36;
            _fusionBadgeNum.fontStyle = FontStyles.Bold;
            _fusionBadgeNum.color = Color.white;
            _fusionBadgeNum.alignment = TextAlignmentOptions.Center;
            _fusionBadgeNum.raycastTarget = false;
            var nrt = _fusionBadgeNum.rectTransform;
            nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
            nrt.offsetMin = Vector2.zero; nrt.offsetMax = Vector2.zero;

            _fusionBadge = go;
        }
        _fusionBadge.transform.SetAsLastSibling();
        _fusionBadge.SetActive(true);
        _fusionBadgeNum.text = order.ToString();
    }

    public void HideFusionBadge()
    {
        if (_fusionBadge != null) _fusionBadge.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnHover?.Invoke(Card);
    }
}
