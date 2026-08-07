using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Construye el Menú Principal como OBJETOS REALES en la escena (no en runtime),
/// para que puedas inspeccionarlos y editarlos en la Hierarchy y el Inspector.
/// Deja todas las referencias del <see cref="MainMenuController"/> ya cableadas.
///
/// No se llama a mano: lo usa <see cref="MainMenuSetup"/> desde el menú
/// **YGO > Setup**. Puedes reestilizar/mover todo después; mientras conserves
/// las referencias, el menú seguirá funcionando.
/// </summary>
public static class MainMenuBuilder
{
    // ── Paleta (puedes cambiar los colores luego en el Inspector de cada objeto) ──
    static readonly Color BgColor      = new Color(0.07f, 0.08f, 0.15f);
    static readonly Color Gold         = new Color(0.86f, 0.72f, 0.35f);
    static readonly Color GoldBright   = new Color(0.98f, 0.85f, 0.45f);
    static readonly Color TextLight    = new Color(0.93f, 0.94f, 0.98f);
    static readonly Color BtnNormal    = new Color(0.11f, 0.13f, 0.24f, 0.92f);
    static readonly Color BtnHover     = new Color(0.20f, 0.24f, 0.42f, 0.98f);
    static readonly Color BtnPressed   = new Color(0.16f, 0.19f, 0.34f, 1f);
    static readonly Color BtnDisabled  = new Color(0.10f, 0.11f, 0.16f, 0.6f);
    static readonly Color PanelDim     = new Color(0f, 0f, 0f, 0.72f);
    static readonly Color PanelFill    = new Color(0.08f, 0.09f, 0.17f, 0.98f);

    /// <summary>
    /// Reconstruye la UI en la escena del controlador (borra la anterior si la hay)
    /// y cablea las referencias.
    /// </summary>
    public static void BuildInScene(MainMenuController controller)
    {
        Scene scene = controller.gameObject.scene;

        // Quita un canvas previo para no duplicar.
        var previous = FindRootInScene(scene, "MainMenuCanvas");
        if (previous != null) Object.DestroyImmediate(previous);

        EnsureEventSystem(scene);

        // ── Canvas raíz ──────────────────────────────────────────────────
        var canvasGO = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler),
                                      typeof(GraphicRaycaster), typeof(ResponsiveCanvasScaler));
        MoveToScene(canvasGO, scene);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var canvasRT = canvasGO.GetComponent<RectTransform>();

        // ── Fondo ────────────────────────────────────────────────────────
        // Capa 1: color de respaldo a pantalla completa (se ve si no hay arte).
        var bgColor = NewImage("BackgroundColor", canvasRT, BgColor);
        Stretch(bgColor.rectTransform);

        // Capa 2: TU arte. Arrastra tu sprite al "Source Image" de este objeto y
        // ResponsiveBackground lo hará cubrir la pantalla sin deformarse. Mientras
        // no tenga sprite, el componente lo mantiene oculto.
        var bgArt = NewImage("BackgroundArt", canvasRT, Color.white);
        bgArt.gameObject.AddComponent<ResponsiveBackground>();

        // ── Título + subtítulo ───────────────────────────────────────────
        var title = MakeText("Title", canvasRT, "MEMORIAS PROHIBIDAS", 96, GoldBright, TextAlignmentOptions.Center);
        AnchorTopStretch(title.rectTransform, 80, 80, 150, -110);
        title.fontStyle = FontStyles.Bold;
        title.enableWordWrapping = false;
        title.enableAutoSizing = true; title.fontSizeMin = 34; title.fontSizeMax = 96;

        var subtitle = MakeText("Subtitle", canvasRT,
            "Un sucesor espiritual de Yu-Gi-Oh! Forbidden Memories", 34, TextLight, TextAlignmentOptions.Center);
        AnchorTopStretch(subtitle.rectTransform, 120, 120, 80, -258);
        subtitle.enableAutoSizing = true; subtitle.fontSizeMin = 18; subtitle.fontSizeMax = 34;
        subtitle.color = new Color(TextLight.r, TextLight.g, TextLight.b, 0.85f);

        // ── Columna de botones ───────────────────────────────────────────
        var column = new GameObject("ButtonColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        column.transform.SetParent(canvasRT, false);
        var colRT = column.GetComponent<RectTransform>();
        colRT.anchorMin = colRT.anchorMax = new Vector2(0f, 0.5f);
        colRT.pivot = new Vector2(0f, 0.5f);
        colRT.anchoredPosition = new Vector2(160, -30);
        colRT.sizeDelta = new Vector2(470, 10);
        var vlg = column.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 14; vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        column.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var btnNueva     = MakeButton("Btn_NuevaPartida", colRT, "Nueva Partida");
        var btnContinuar = MakeButton("Btn_Continuar", colRT, "Continuar");
        var btnHistoria  = MakeButton("Btn_Historia", colRT, "Historia");
        var btnDuelo     = MakeButton("Btn_DueloLibre", colRT, "Duelo Libre");
        var btnDeck      = MakeButton("Btn_ConstructorDeck", colRT, "Constructor de Deck");
        var btnColeccion = MakeButton("Btn_Coleccion", colRT, "Colección");
        var btnOpciones  = MakeButton("Btn_Opciones", colRT, "Opciones");
        var btnCreditos  = MakeButton("Btn_Creditos", colRT, "Créditos");
        var btnSalir     = MakeButton("Btn_Salir", colRT, "Salir");

        // ── Toast (aviso inferior) ───────────────────────────────────────
        var toast = MakeText("Toast", canvasRT, "", 32, GoldBright, TextAlignmentOptions.Center);
        var toastRT = toast.rectTransform;
        toastRT.anchorMin = new Vector2(0f, 0f); toastRT.anchorMax = new Vector2(1f, 0f);
        toastRT.pivot = new Vector2(0.5f, 0f);
        toastRT.offsetMin = new Vector2(80, 70); toastRT.offsetMax = new Vector2(-80, 130);
        toast.enableAutoSizing = true; toast.fontSizeMin = 18; toast.fontSizeMax = 32;
        toast.color = new Color(GoldBright.r, GoldBright.g, GoldBright.b, 0f);

        // ── Panel de Opciones ────────────────────────────────────────────
        var optionsPanel = MakeModalPanel("OptionsPanel", canvasRT, "Opciones", new Vector2(760, 640), out var optBody);
        var masterSlider = MakeSliderRow(optBody, "Volumen general");
        var musicSlider  = MakeSliderRow(optBody, "Música");
        var sfxSlider    = MakeSliderRow(optBody, "Efectos");
        var langBtn      = MakeWideButton("Btn_Idioma", optBody, "Idioma:  Español");
        var fullBtn      = MakeWideButton("Btn_Pantalla", optBody, "Pantalla completa:  Sí");
        var optBack      = MakeWideButton("Btn_VolverOpciones", optBody, "Volver");
        optionsPanel.SetActive(false);

        // ── Panel de Créditos ────────────────────────────────────────────
        var creditsPanel = MakeModalPanel("CreditsPanel", canvasRT, "Créditos", new Vector2(820, 640), out var credBody);
        var credText = MakeText("CreditsText", credBody, CreditsText(), 30, TextLight, TextAlignmentOptions.Center);
        var credTextLE = credText.gameObject.AddComponent<LayoutElement>();
        credTextLE.flexibleHeight = 1; credTextLE.minHeight = 360;
        var credBack = MakeWideButton("Btn_VolverCreditos", credBody, "Volver");
        creditsPanel.SetActive(false);

        // ── Cablear referencias en el controlador ────────────────────────
        var so = new SerializedObject(controller);
        Set(so, "titleText", title);
        Set(so, "subtitleText", subtitle);
        Set(so, "toastText", toast);
        Set(so, "nuevaPartidaButton", btnNueva);
        Set(so, "continuarButton", btnContinuar);
        Set(so, "historiaButton", btnHistoria);
        Set(so, "dueloLibreButton", btnDuelo);
        Set(so, "constructorDeckButton", btnDeck);
        Set(so, "coleccionButton", btnColeccion);
        Set(so, "opcionesButton", btnOpciones);
        Set(so, "creditosButton", btnCreditos);
        Set(so, "salirButton", btnSalir);
        Set(so, "optionsPanel", optionsPanel);
        Set(so, "masterSlider", masterSlider);
        Set(so, "musicSlider", musicSlider);
        Set(so, "sfxSlider", sfxSlider);
        Set(so, "languageButton", langBtn);
        Set(so, "fullscreenButton", fullBtn);
        Set(so, "optionsBackButton", optBack);
        Set(so, "creditsPanel", creditsPanel);
        Set(so, "creditsBackButton", credBack);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        EditorSceneManager_MarkDirty(scene);
        Debug.Log("MainMenuBuilder: UI del menú construida y cableada en la escena.");
    }

    // ── Fábricas de widgets ──────────────────────────────────────────────

    static GameObject MakeModalPanel(string name, RectTransform canvas, string title, Vector2 size, out RectTransform body)
    {
        var overlay = NewImage(name, canvas, PanelDim);
        Stretch(overlay.rectTransform);

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        card.transform.SetParent(overlay.transform, false);
        var cardRT = card.GetComponent<RectTransform>();
        cardRT.anchorMin = cardRT.anchorMax = cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = size; cardRT.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = PanelFill;
        var cvlg = card.GetComponent<VerticalLayoutGroup>();
        cvlg.padding = new RectOffset(40, 40, 32, 32); cvlg.spacing = 18;
        cvlg.childControlWidth = true; cvlg.childControlHeight = true;
        cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;

        var header = MakeText("Header", cardRT, title, 52, GoldBright, TextAlignmentOptions.Center);
        header.fontStyle = FontStyles.Bold;
        header.gameObject.AddComponent<LayoutElement>().minHeight = 64;

        var bodyGO = new GameObject("Body", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        bodyGO.transform.SetParent(cardRT, false);
        var bvlg = bodyGO.GetComponent<VerticalLayoutGroup>();
        bvlg.spacing = 16; bvlg.childControlWidth = true; bvlg.childControlHeight = true;
        bvlg.childForceExpandWidth = true; bvlg.childForceExpandHeight = false;
        bodyGO.GetComponent<LayoutElement>().flexibleHeight = 1;
        body = bodyGO.GetComponent<RectTransform>();

        return overlay.gameObject;
    }

    static Slider MakeSliderRow(RectTransform parent, string label)
    {
        var lbl = MakeText("Label_" + label, parent, label, 30, TextLight, TextAlignmentOptions.Left);
        lbl.gameObject.AddComponent<LayoutElement>().minHeight = 34;

        var slider = DefaultControls.CreateSlider(new DefaultControls.Resources()).GetComponent<Slider>();
        slider.name = "Slider_" + label;
        slider.transform.SetParent(parent, false);
        slider.minValue = 0f; slider.maxValue = 1f;
        Recolor(slider.transform.Find("Background"), new Color(0.05f, 0.06f, 0.1f, 1f));
        Recolor(slider.transform.Find("Fill Area/Fill"), Gold);
        Recolor(slider.transform.Find("Handle Slide Area/Handle"), GoldBright);
        var le = slider.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 26; le.preferredHeight = 26;
        return slider;
    }

    static Button MakeWideButton(string name, RectTransform parent, string label)
    {
        var btn = MakeButton(name, parent, label);
        var le = btn.GetComponent<LayoutElement>();
        le.minHeight = 60; le.preferredHeight = 60;
        return btn;
    }

    static Button MakeButton(string name, RectTransform parent, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = Color.white; // el color base real vive en el ColorBlock

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 68; le.preferredHeight = 68;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = BtnNormal; cb.highlightedColor = BtnHover; cb.pressedColor = BtnPressed;
        cb.selectedColor = BtnHover; cb.disabledColor = BtnDisabled;
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.1f;
        btn.colors = cb;

        var txt = MakeText("Label", go.transform, label, 34, TextLight, TextAlignmentOptions.Center);
        Stretch(txt.rectTransform);
        txt.margin = new Vector4(24, 0, 24, 0);

        return btn;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.richText = true;
        t.raycastTarget = false; // los textos no deben interceptar clics
        return t;
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

    // ── Utilidades ───────────────────────────────────────────────────────

    static void Set(SerializedObject so, string propertyName, Object value)
    {
        var p = so.FindProperty(propertyName);
        if (p == null) { Debug.LogError($"MainMenuBuilder: el campo '{propertyName}' no existe en MainMenuController."); return; }
        p.objectReferenceValue = value;
    }

    static void Recolor(Transform t, Color c)
    {
        if (t == null) return;
        var img = t.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    static void EnsureEventSystem(Scene scene)
    {
        if (Object.FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        MoveToScene(go, scene);
    }

    static GameObject FindRootInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    static void MoveToScene(GameObject go, Scene scene)
    {
        if (go.scene != scene && scene.IsValid())
            SceneManager.MoveGameObjectToScene(go, scene);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void AnchorTopStretch(RectTransform rt, float leftMargin, float rightMargin, float height, float top)
    {
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(leftMargin, top - height);
        rt.offsetMax = new Vector2(-rightMargin, top);
    }

    static void EditorSceneManager_MarkDirty(Scene scene)
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    }
}
