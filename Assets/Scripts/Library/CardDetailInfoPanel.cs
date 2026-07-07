using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye por código el contenido del panel de detalle (DetailPanelBox):
/// encabezado con categoría·Nº y nombre, chips de ATK/DEF, filas de datos y
/// bloque "Obtenida", con estética Neo-Kemet. Va en una COLUMNA a la derecha del
/// panel (la carta ocupa la izquierda) y NO crea fondo propio: conserva el
/// info_library del panel; solo pone un velo tenue tras el texto para legibilidad.
/// Sustituye al maquetado antiguo sin tocar la carta ni las animaciones.
///
/// Lo crea y rellena <see cref="CardDetailPanel"/>. El fade de aparición se hace
/// sobre <see cref="ContentGroup"/>.
/// </summary>
public class CardDetailInfoPanel : MonoBehaviour
{
    // ── Paleta (Neo-Kemet) ───────────────────────────────────────────────
    private static readonly Color Gold     = new Color(0.93f, 0.75f, 0.33f);
    private static readonly Color Cyan     = new Color(0.22f, 0.95f, 0.86f);
    private static readonly Color Inner    = new Color(0.09f, 0.08f, 0.13f, 0.55f);
    private static readonly Color Bright   = new Color(0.93f, 0.90f, 0.83f);
    private static readonly Color Muted    = new Color(0.62f, 0.58f, 0.50f);
    private static readonly Color Line     = new Color(0.93f, 0.75f, 0.33f, 0.28f);

    public CanvasGroup ContentGroup { get; private set; }

    private RectTransform _content;
    private TMP_FontAsset _font;

    /// <summary>Crea la jerarquía (una sola vez) dentro de <paramref name="parent"/>
    /// (= infoPanel/DetailPanelBox, que ya tiene su fondo info_library). NO se
    /// crea fondo ni velo propio: el contenido va en una COLUMNA sobre la
    /// "pantalla" derecha del marco (la carta ocupa la izquierda), integrándose
    /// con él. <paramref name="font"/> se toma de un texto existente para mantener
    /// la tipografía del juego.</summary>
    public void Build(RectTransform parent, TMP_FontAsset font)
    {
        _font = font;

        // Columna de contenido anclada a la "pantalla" derecha del marco (la carta
        // ocupa la izquierda). SIN fondo ni velo: el área derecha del info_library
        // ya es oscura y el texto se lee directamente sobre ella, integrándose con
        // el marco. Anclas normalizadas → aguanta cualquier tamaño.
        _content = MakeRect("InfoContent", parent);
        _content.anchorMin = new Vector2(0.485f, 0.12f);
        _content.anchorMax = new Vector2(0.865f, 0.88f);
        _content.offsetMin = Vector2.zero;
        _content.offsetMax = Vector2.zero;
        ContentGroup = _content.gameObject.AddComponent<CanvasGroup>();

        var vlg = _content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.spacing = 11f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
    }

    /// <summary>Reconstruye el contenido para la carta dada.</summary>
    public void Fill(LibraryEntry entry, string sourcesText, bool show3D, Action on3D)
    {
        if (_content == null) return;
        var card = entry.card;

        // Limpia el contenido anterior.
        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);

        Color catColor = card.IsMonster ? Gold : CardStyleKemet.BadgeColor(card.cardCategory);

        // Encabezado.
        Eyebrow($"{card.CategoryLabel}  ·  Nº {card.cardId:000}", catColor);
        Title(card.cardName);
        Divider();

        if (card.IsMonster)
        {
            StatChips(card.baseAtk.ToString(), card.baseDef.ToString());
            // La fuente no tiene el glifo de estrella; se muestra el número.
            Row("Nivel", card.stars.ToString());
            Row("Tipo", Pretty(card.monsterType.ToString()));
            Row("Atributo", Pretty(card.attribute.ToString()));
        }
        else
        {
            Row("Categoría", card.CategoryLabel);
            string desc = card.DisplayDescription;
            if (!string.IsNullOrEmpty(desc)) Body(desc);
        }

        Divider();
        Row("Copias", entry.Copies > 0 ? $"×{entry.Copies}" : "-");

        // Obtenida.
        Section("Obtenida");
        Body(StripHeader(sourcesText));

        if (show3D && on3D != null)
            Button3D("VER EN 3D", on3D);
    }

    // ── Bloques ──────────────────────────────────────────────────────────

    private void Eyebrow(string text, Color color)
    {
        var t = MakeText(_content, "Eyebrow", 16f, color, FontStyles.UpperCase, TextAlignmentOptions.Left);
        t.characterSpacing = 8f;
        t.text = text;
    }

    private void Title(string text)
    {
        var t = MakeText(_content, "Title", 40f, Bright, FontStyles.Bold, TextAlignmentOptions.Left);
        t.text = text;
    }

    private void Section(string text)
    {
        var t = MakeText(_content, "Section", 15f, Gold, FontStyles.UpperCase, TextAlignmentOptions.Left);
        t.characterSpacing = 6f;
        t.text = text;
        Spacer(2f);
    }

    private void Body(string text)
    {
        var t = MakeText(_content, "Body", 16f, Muted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        t.text = text;
    }

    private void Row(string label, string value)
    {
        var rowGO = new GameObject("Row", typeof(RectTransform));
        var rt = (RectTransform)rowGO.transform;
        rt.SetParent(_content, false);

        var h = rowGO.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 12f;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;

        var lbl = MakeText(rt, "Label", 16f, Muted, FontStyles.UpperCase, TextAlignmentOptions.Left);
        lbl.characterSpacing = 3f;
        lbl.text = label;
        var lblLE = lbl.gameObject.AddComponent<LayoutElement>();
        lblLE.minWidth = 130f; lblLE.preferredWidth = 130f; lblLE.flexibleWidth = 0f;

        var val = MakeText(rt, "Value", 19f, Bright, FontStyles.Bold, TextAlignmentOptions.Left);
        val.text = value;
        val.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }

    private void StatChips(string atk, string def)
    {
        var rowGO = new GameObject("StatChips", typeof(RectTransform));
        var rt = (RectTransform)rowGO.transform;
        rt.SetParent(_content, false);

        var h = rowGO.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 12f;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;

        Chip(rt, "ATK", atk, Gold);
        Chip(rt, "DEF", def, Cyan);
    }

    private void Chip(Transform parent, string label, string value, Color accent)
    {
        var go = new GameObject("Chip", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = Inner;

        var v = go.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(10, 10, 8, 8);
        v.spacing = 0f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.MiddleCenter;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 70f; le.flexibleWidth = 1f;

        var lbl = MakeText(go.transform, "Label", 14f, Muted, FontStyles.UpperCase, TextAlignmentOptions.Center);
        lbl.characterSpacing = 6f;
        lbl.text = label;

        var val = MakeText(go.transform, "Value", 34f, accent, FontStyles.Bold, TextAlignmentOptions.Center);
        val.text = value;

        // Acento neón inferior (oro/turquesa) para integrarlo con el marco.
        var bar = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        var barRT = (RectTransform)bar.transform;
        barRT.SetParent(go.transform, false);
        barRT.anchorMin = new Vector2(0f, 0f);
        barRT.anchorMax = new Vector2(1f, 0f);
        barRT.pivot = new Vector2(0.5f, 0f);
        barRT.offsetMin = Vector2.zero;
        barRT.offsetMax = new Vector2(0f, 3f);
        var barImg = bar.GetComponent<Image>();
        barImg.color = accent;
        barImg.raycastTarget = false;
        bar.AddComponent<LayoutElement>().ignoreLayout = true;
    }

    private void Button3D(string label, Action onClick)
    {
        Spacer(6f);
        var go = new GameObject("Btn3D", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_content, false);
        go.GetComponent<Image>().color = Cyan;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 46f;

        var t = MakeText(rt, "Label", 18f, new Color(0.06f, 0.07f, 0.09f), FontStyles.Bold, TextAlignmentOptions.Center);
        t.characterSpacing = 6f;
        t.text = label;
        var tRT = (RectTransform)t.transform;
        Stretch(tRT, Vector2.zero, Vector2.zero);

        go.GetComponent<Button>().onClick.AddListener(() => onClick());
    }

    private void Divider()
    {
        var go = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_content, false);
        go.GetComponent<Image>().color = Line;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 2f; le.flexibleWidth = 1f;
    }

    private void Spacer(float h)
    {
        var go = new GameObject("Spacer", typeof(RectTransform));
        go.transform.SetParent(_content, false);
        go.AddComponent<LayoutElement>().minHeight = h;
    }

    // ── Fábrica de elementos ─────────────────────────────────────────────

    private TMP_Text MakeText(Transform parent, string name, float size, Color color,
                              FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = align;
        t.enableWordWrapping = true;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    private static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = min;
        rt.offsetMax = new Vector2(-max.x, -max.y);
    }

    // ── Utilidades de texto ──────────────────────────────────────────────

    private static string Pretty(string enumName)
    {
        // Separa CamelCase → "SeaSerpent" => "Sea Serpent".
        if (string.IsNullOrEmpty(enumName)) return enumName;
        var sb = new System.Text.StringBuilder(enumName.Length + 4);
        for (int i = 0; i < enumName.Length; i++)
        {
            char c = enumName[i];
            if (i > 0 && char.IsUpper(c)) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    // El texto de fuentes ya viene como "Obtenida:\n- a\n- b"; aquí quitamos el
    // encabezado (lo pone el bloque Section) y dejamos solo la lista.
    private static string StripHeader(string sources)
    {
        if (string.IsNullOrEmpty(sources)) return "—";
        int nl = sources.IndexOf('\n');
        string body = nl >= 0 ? sources.Substring(nl + 1) : sources;
        body = body.TrimEnd('\n', '\r', ' ');
        return string.IsNullOrEmpty(body) ? "-" : body;
    }
}
