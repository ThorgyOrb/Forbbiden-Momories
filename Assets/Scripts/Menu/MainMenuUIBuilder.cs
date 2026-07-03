using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Definición de un botón del menú: qué texto muestra, qué hace al pulsarlo y
/// si empieza activo o gris (ej. "Continuar" cuando no hay partida guardada).
/// </summary>
public struct MenuButtonDef
{
    public string label;
    public UnityAction onClick;
    public bool interactable;

    public MenuButtonDef(string label, UnityAction onClick, bool interactable = true)
    {
        this.label = label;
        this.onClick = onClick;
        this.interactable = interactable;
    }
}

/// <summary>Referencias a los widgets que crea el builder, para que el controlador los use.</summary>
public class MainMenuUI
{
    public Canvas canvas;
    public TextMeshProUGUI title;
    public TextMeshProUGUI subtitle;
    public readonly List<Button> buttons = new();
    public TextMeshProUGUI toast;

    // Panel de Opciones
    public GameObject optionsPanel;
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    public Button languageButton;
    public Button fullscreenButton;
    public Button optionsBackButton;

    // Panel de Créditos
    public GameObject creditsPanel;
    public Button creditsBackButton;
}

/// <summary>
/// Construye TODA la interfaz del Menú Principal por código (Canvas, fondo,
/// título, botones, paneles de Opciones y Créditos). Así el menú funciona en
/// cuanto pulsas Play, sin tener que montar nada a mano en el editor. Cuando
/// tengas arte definitivo, puedes sustituir esto por un prefab diseñado y dejar
/// que <see cref="MainMenuController"/> use referencias del Inspector.
/// </summary>
public static class MainMenuUIBuilder
{
    // ── Paleta (tono "memoria antigua": azul profundo + oro) ─────────────
    static readonly Color BgTop        = new Color(0.06f, 0.07f, 0.14f);
    static readonly Color BgBottom     = new Color(0.10f, 0.08f, 0.17f);
    static readonly Color Gold         = new Color(0.86f, 0.72f, 0.35f);
    static readonly Color GoldBright   = new Color(0.98f, 0.85f, 0.45f);
    static readonly Color TextLight     = new Color(0.93f, 0.94f, 0.98f);
    static readonly Color BtnNormal    = new Color(0.11f, 0.13f, 0.24f, 0.92f);
    static readonly Color BtnHover     = new Color(0.20f, 0.24f, 0.42f, 0.98f);
    static readonly Color BtnPressed   = new Color(0.16f, 0.19f, 0.34f, 1f);
    static readonly Color BtnDisabled  = new Color(0.10f, 0.11f, 0.16f, 0.6f);
    static readonly Color PanelDim     = new Color(0f, 0f, 0f, 0.72f);
    static readonly Color PanelFill    = new Color(0.08f, 0.09f, 0.17f, 0.98f);

    // ── API principal ────────────────────────────────────────────────────

    public static MainMenuUI Build(Transform parent, string title, string subtitle, List<MenuButtonDef> defs)
    {
        var ui = new MainMenuUI();

        EnsureEventSystem();

        // Canvas raíz (Screen Space Overlay, escala con resolución 1920x1080).
        var canvasGO = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(parent, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        ui.canvas = canvas;

        // Fondo con degradado vertical.
        BuildGradientBackground(canvas.transform);

        // Título + subtítulo (arriba, centrados).
        ui.title = MakeText(canvas.transform, title, 96, GoldBright, TextAlignmentOptions.Center);
        Anchor(ui.title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(1400, 130));
        ui.title.fontStyle = FontStyles.Bold;
        ui.title.enableWordWrapping = false;

        ui.subtitle = MakeText(canvas.transform, subtitle, 34, TextLight, TextAlignmentOptions.Center);
        Anchor(ui.subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(1400, 50));
        ui.subtitle.alpha = 0.85f;

        // Columna de botones (centro-izquierda, apilados verticalmente).
        var column = new GameObject("ButtonColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        column.transform.SetParent(canvas.transform, false);
        var colRT = column.GetComponent<RectTransform>();
        Anchor(colRT, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160, -30), new Vector2(470, 10));
        colRT.pivot = new Vector2(0f, 0.5f);
        var vlg = column.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 14;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var fitter = column.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (var def in defs)
        {
            var btn = MakeButton(colRT, def.label);
            btn.interactable = def.interactable;
            if (def.onClick != null) btn.onClick.AddListener(def.onClick);
            ui.buttons.Add(btn);
        }

        // Toast (aviso temporal, abajo centrado).
        ui.toast = MakeText(canvas.transform, "", 32, GoldBright, TextAlignmentOptions.Center);
        Anchor(ui.toast.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(1400, 60));
        ui.toast.alpha = 0f;

        // Paneles superpuestos (ocultos por defecto).
        BuildOptionsPanel(canvas.transform, ui);
        BuildCreditsPanel(canvas.transform, ui);

        return ui;
    }

    // ── Paneles ──────────────────────────────────────────────────────────

    static void BuildOptionsPanel(Transform canvas, MainMenuUI ui)
    {
        var (overlay, panel) = MakeModalPanel(canvas, "OptionsPanel", "Opciones", new Vector2(760, 620));
        ui.optionsPanel = overlay;

        var body = MakeVerticalBody(panel);

        ui.masterSlider = MakeSliderRow(body, "Volumen general");
        ui.musicSlider = MakeSliderRow(body, "Música");
        ui.sfxSlider = MakeSliderRow(body, "Efectos");

        ui.languageButton = MakeWideButton(body, "Idioma:  Español");
        ui.fullscreenButton = MakeWideButton(body, "Pantalla completa:  Sí");

        ui.optionsBackButton = MakeWideButton(body, "Volver");

        overlay.SetActive(false);
    }

    static void BuildCreditsPanel(Transform canvas, MainMenuUI ui)
    {
        var (overlay, panel) = MakeModalPanel(canvas, "CreditsPanel", "Créditos", new Vector2(820, 620));
        ui.creditsPanel = overlay;

        var body = MakeVerticalBody(panel);

        var text = MakeText(body, CreditsText(), 30, TextLight, TextAlignmentOptions.Top);
        text.alignment = TextAlignmentOptions.Center;
        var le = text.gameObject.AddComponent<LayoutElement>();
        le.flexibleHeight = 1;
        le.minHeight = 360;

        ui.creditsBackButton = MakeWideButton(body, "Volver");

        overlay.SetActive(false);
    }

    static string CreditsText()
    {
        return
            "<b>Sucesor Espiritual de\nYu-Gi-Oh! Forbidden Memories</b>\n\n" +
            "Un proyecto en desarrollo.\n\n" +
            "Diseño y programación\n<color=#DBB859>Tú</color>\n\n" +
            "Motor\nUnity\n\n" +
            "¡Gracias por jugar!";
    }

    // ── Fábricas de widgets ──────────────────────────────────────────────

    static (GameObject overlay, RectTransform panel) MakeModalPanel(Transform canvas, string name, string title, Vector2 size)
    {
        // Fondo oscurecedor a pantalla completa.
        var overlay = new GameObject(name, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas, false);
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.GetComponent<Image>().color = PanelDim;

        // Tarjeta central.
        var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        card.transform.SetParent(overlay.transform, false);
        var cardRT = card.GetComponent<RectTransform>();
        cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = size;
        cardRT.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = PanelFill;

        var vlg = card.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(40, 40, 32, 32);
        vlg.spacing = 18;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var header = MakeText(cardRT, title, 52, GoldBright, TextAlignmentOptions.Center);
        header.fontStyle = FontStyles.Bold;
        var hle = header.gameObject.AddComponent<LayoutElement>();
        hle.minHeight = 64;

        return (overlay, cardRT);
    }

    /// <summary>Un contenedor vertical que ocupa el resto de la tarjeta.</summary>
    static RectTransform MakeVerticalBody(RectTransform card)
    {
        var body = new GameObject("Body", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        body.transform.SetParent(card, false);
        var vlg = body.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 16;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var le = body.GetComponent<LayoutElement>();
        le.flexibleHeight = 1;
        return body.GetComponent<RectTransform>();
    }

    static Slider MakeSliderRow(Transform parent, string label)
    {
        var lbl = MakeText(parent, label, 30, TextLight, TextAlignmentOptions.Left);
        var lle = lbl.gameObject.AddComponent<LayoutElement>();
        lle.minHeight = 34;

        var slider = DefaultControls.CreateSlider(new DefaultControls.Resources()).GetComponent<Slider>();
        slider.transform.SetParent(parent, false);
        slider.minValue = 0f;
        slider.maxValue = 1f;

        RecolorImage(slider.transform.Find("Background"), new Color(0.05f, 0.06f, 0.1f, 1f));
        RecolorImage(slider.transform.Find("Fill Area/Fill"), Gold);
        RecolorImage(slider.transform.Find("Handle Slide Area/Handle"), GoldBright);

        var sle = slider.gameObject.AddComponent<LayoutElement>();
        sle.minHeight = 26;
        sle.preferredHeight = 26;
        return slider;
    }

    static Button MakeWideButton(Transform parent, string label)
    {
        var btn = MakeButton(parent, label);
        var le = btn.gameObject.GetComponent<LayoutElement>();
        le.minHeight = 60;
        le.preferredHeight = 60;
        return btn;
    }

    static Button MakeButton(Transform parent, string label)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = Color.white; // el color base real vive en el ColorBlock del botón

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 68;
        le.preferredHeight = 68;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = BtnNormal;
        cb.highlightedColor = BtnHover;
        cb.pressedColor = BtnPressed;
        cb.selectedColor = BtnHover;
        cb.disabledColor = BtnDisabled;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.1f;
        btn.colors = cb;

        var txt = MakeText(go.transform, label, 34, TextLight, TextAlignmentOptions.Center);
        Stretch(txt.rectTransform);
        txt.margin = new Vector4(24, 0, 24, 0);

        return btn;
    }

    // ── Fondo con degradado ──────────────────────────────────────────────

    static void BuildGradientBackground(Transform canvas)
    {
        var go = new GameObject("Background", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas, false);
        Stretch(go.GetComponent<RectTransform>());

        var tex = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 256; y++)
        {
            float t = y / 255f;
            tex.SetPixel(0, y, Color.Lerp(BgBottom, BgTop, t));
        }
        tex.Apply();

        var img = go.GetComponent<Image>();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 256), new Vector2(0.5f, 0.5f), 100);
        img.type = Image.Type.Simple;
    }

    // ── Utilidades ───────────────────────────────────────────────────────

    static TextMeshProUGUI MakeText(Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = true;
        t.richText = true;
        return t;
    }

    static void RecolorImage(Transform t, Color c)
    {
        if (t == null) return;
        var img = t.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (Object.FindObjectOfType<EventSystem>() != null) return;
        // Vive dentro de la escena del menú (no persiste), para no chocar con los
        // EventSystem propios de las demás escenas.
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }
}
