using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Construye la escena de Duelo Libre como OBJETOS REALES editables (título,
/// botón Volver, orden, ScrollView y una tarjeta PLANTILLA de rival) y cablea
/// las referencias del <see cref="FreeDuelController"/> y del
/// <see cref="OpponentEntryView"/> de la plantilla.
///
/// La lista de rivales se genera en runtime clonando la plantilla; aquí solo se
/// crea el andamiaje visual, que puedes reestilizar libremente.
/// </summary>
public static class FreeDuelBuilder
{
    static readonly Color BgColor     = new Color(0.07f, 0.08f, 0.15f);
    static readonly Color Gold        = new Color(0.86f, 0.72f, 0.35f);
    static readonly Color GoldBright  = new Color(0.98f, 0.85f, 0.45f);
    static readonly Color TextLight   = new Color(0.93f, 0.94f, 0.98f);
    static readonly Color BtnNormal   = new Color(0.11f, 0.13f, 0.24f, 0.92f);
    static readonly Color BtnHover    = new Color(0.20f, 0.24f, 0.42f, 0.98f);
    static readonly Color BtnPressed  = new Color(0.16f, 0.19f, 0.34f, 1f);
    static readonly Color BtnDisabled = new Color(0.10f, 0.11f, 0.16f, 0.6f);
    static readonly Color PanelFill   = new Color(0.10f, 0.11f, 0.20f, 0.96f);
    static readonly Color PanelHover  = new Color(0.16f, 0.18f, 0.30f, 0.98f);
    static readonly Color PanelDim    = new Color(0f, 0f, 0f, 0.80f);

    public static void BuildInScene(FreeDuelController controller)
    {
        Scene scene = controller.gameObject.scene;

        var previous = FindRootInScene(scene, "FreeDuelCanvas");
        if (previous != null) Object.DestroyImmediate(previous);

        EnsureEventSystem(scene);

        // ── Canvas ───────────────────────────────────────────────────────
        var canvasGO = new GameObject("FreeDuelCanvas", typeof(Canvas), typeof(CanvasScaler),
                                      typeof(GraphicRaycaster), typeof(ResponsiveCanvasScaler));
        MoveToScene(canvasGO, scene);
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var canvasRT = canvasGO.GetComponent<RectTransform>();

        // ── Fondo ────────────────────────────────────────────────────────
        var bgColor = NewImage("BackgroundColor", canvasRT, BgColor);
        Stretch(bgColor.rectTransform);
        var bgArt = NewImage("BackgroundArt", canvasRT, Color.white);
        bgArt.gameObject.AddComponent<ResponsiveBackground>();

        // ── Título ───────────────────────────────────────────────────────
        var title = MakeText("Title", canvasRT, "DUELO LIBRE", 84, GoldBright, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        AnchorTopStretch(title.rectTransform, 260, 260, 100, -40);
        title.enableAutoSizing = true; title.fontSizeMin = 32; title.fontSizeMax = 84;

        // ── Botón Volver (arriba-izquierda) ──────────────────────────────
        var backBtn = MakeButton("Btn_Volver", canvasRT, "Volver");
        var backRT = (RectTransform)backBtn.transform;
        backRT.anchorMin = backRT.anchorMax = new Vector2(0f, 1f);
        backRT.pivot = new Vector2(0f, 1f);
        backRT.anchoredPosition = new Vector2(40, -40);
        backRT.sizeDelta = new Vector2(180, 72);

        // ── Fila de orden (Aparición / Dificultad / Región) ──────────────
        var sortRow = new GameObject("SortRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        sortRow.transform.SetParent(canvasRT, false);
        var sortRT = sortRow.GetComponent<RectTransform>();
        sortRT.anchorMin = sortRT.anchorMax = new Vector2(0.5f, 1f);
        sortRT.pivot = new Vector2(0.5f, 1f);
        sortRT.anchoredPosition = new Vector2(0, -150);
        sortRT.sizeDelta = new Vector2(720, 68);
        var srlg = sortRow.GetComponent<HorizontalLayoutGroup>();
        srlg.spacing = 14; srlg.childControlWidth = true; srlg.childControlHeight = true;
        srlg.childForceExpandWidth = true; srlg.childForceExpandHeight = true;
        var sortApp  = MakeButton("Btn_OrdenAparicion", sortRow.transform, "Aparición");
        var sortDiff = MakeButton("Btn_OrdenDificultad", sortRow.transform, "Dificultad");
        var sortReg  = MakeButton("Btn_OrdenRegion", sortRow.transform, "Región");
        FitButtonText(sortApp); FitButtonText(sortDiff); FitButtonText(sortReg);

        // ── ScrollView ───────────────────────────────────────────────────
        var scrollGO = DefaultControls.CreateScrollView(new DefaultControls.Resources());
        scrollGO.name = "OpponentScroll";
        scrollGO.transform.SetParent(canvasRT, false);
        var scrollRT = (RectTransform)scrollGO.transform;
        scrollRT.anchorMin = new Vector2(0f, 0f);
        scrollRT.anchorMax = new Vector2(1f, 1f);
        scrollRT.offsetMin = new Vector2(120, 60);
        scrollRT.offsetMax = new Vector2(-120, -240);

        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        var hbar = scrollGO.transform.Find("Scrollbar Horizontal");
        if (hbar != null) { scroll.horizontalScrollbar = null; Object.DestroyImmediate(hbar.gameObject); }
        var viewportImg = scroll.viewport != null ? scroll.viewport.GetComponent<Image>() : null;
        if (viewportImg != null) viewportImg.color = new Color(0f, 0f, 0f, 0.22f);

        var content = scroll.content;
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 12; vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── Texto de estado vacío ────────────────────────────────────────
        var empty = MakeText("EmptyText", canvasRT,
            "No hay rivales desbloqueados todavía.\nDerrota oponentes en la campaña para retarlos aquí.",
            34, TextLight, TextAlignmentOptions.Center);
        empty.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        empty.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        empty.rectTransform.sizeDelta = new Vector2(1100, 200);

        // ── Tarjeta plantilla (inactiva) ─────────────────────────────────
        var entryView = BuildEntryTemplate(content);
        entryView.gameObject.SetActive(false);

        // ── Panel de detalle del rival (oculto por defecto) ──────────────
        var detail = BuildDetailPanel(canvasRT);

        // ── Cablear el controlador ───────────────────────────────────────
        var so = new SerializedObject(controller);
        Set(so, "titleText", title);
        Set(so, "backButton", backBtn);
        Set(so, "emptyText", empty);
        Set(so, "sortAppearanceButton", sortApp);
        Set(so, "sortDifficultyButton", sortDiff);
        Set(so, "sortRegionButton", sortReg);
        Set(so, "listContent", content);
        Set(so, "entryTemplate", entryView);
        Set(so, "detailPanel", detail);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        EditorSceneManager_MarkDirty(scene);
        Debug.Log("FreeDuelBuilder: escena de Duelo Libre construida y cableada.");
    }

    // ── Tarjeta de rival ─────────────────────────────────────────────────

    static OpponentEntryView BuildEntryTemplate(Transform parent)
    {
        var card = new GameObject("OpponentEntry",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup),
            typeof(LayoutElement), typeof(OpponentEntryView));
        card.transform.SetParent(parent, false);

        var cardImg = card.GetComponent<Image>();
        cardImg.color = Color.white; // el color real vive en el ColorBlock del botón
        var cardBtn = card.GetComponent<Button>();
        cardBtn.targetGraphic = cardImg;
        var ccb = cardBtn.colors;
        ccb.normalColor = PanelFill; ccb.highlightedColor = PanelHover; ccb.pressedColor = PanelHover;
        ccb.selectedColor = PanelHover; ccb.disabledColor = PanelFill;
        ccb.colorMultiplier = 1f; ccb.fadeDuration = 0.1f;
        cardBtn.colors = ccb;

        var cle = card.GetComponent<LayoutElement>();
        cle.minHeight = 160; cle.preferredHeight = 160;
        var hlg = card.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 16, 12, 12); hlg.spacing = 18;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        // Retrato
        var portrait = NewImage("Portrait", card.transform, Color.white);
        portrait.preserveAspect = true;
        var pLe = portrait.gameObject.AddComponent<LayoutElement>();
        pLe.minWidth = 130; pLe.preferredWidth = 130;

        // Columna de info
        var info = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        info.transform.SetParent(card.transform, false);
        info.GetComponent<LayoutElement>().flexibleWidth = 1;
        var ivlg = info.GetComponent<VerticalLayoutGroup>();
        ivlg.spacing = 2; ivlg.childControlWidth = true; ivlg.childControlHeight = true;
        ivlg.childForceExpandWidth = true; ivlg.childForceExpandHeight = false;
        ivlg.childAlignment = TextAnchor.MiddleLeft;

        var nameText = MakeText("Name", info.transform, "Nombre del rival", 34, GoldBright, TextAlignmentOptions.Left);
        nameText.fontStyle = FontStyles.Bold;
        var difficulty = MakeText("Difficulty", info.transform, "★★★☆☆", 26, Gold, TextAlignmentOptions.Left);
        var record = MakeText("Record", info.transform, "Victorias: 0    Derrotas: 0", 24, TextLight, TextAlignmentOptions.Left);
        var best = MakeText("Best", info.transform, "Mejor puntuación: 0", 24, TextLight, TextAlignmentOptions.Left);
        var discovery = MakeText("Discovery", info.transform, "Cartas descubiertas: 0/0", 24, TextLight, TextAlignmentOptions.Left);

        // Botón Retar
        var duelBtn = MakeButton("Btn_Retar", card.transform, "Retar");
        var dLe = duelBtn.GetComponent<LayoutElement>();
        dLe.minWidth = 190; dLe.preferredWidth = 190;

        var view = card.GetComponent<OpponentEntryView>();
        var so = new SerializedObject(view);
        Set(so, "cardButton", cardBtn);
        Set(so, "portrait", portrait);
        Set(so, "nameText", nameText);
        Set(so, "difficultyText", difficulty);
        Set(so, "recordText", record);
        Set(so, "bestScoreText", best);
        Set(so, "discoveryText", discovery);
        Set(so, "duelButton", duelBtn);
        so.ApplyModifiedPropertiesWithoutUndo();

        return view;
    }

    // ── Panel de detalle ─────────────────────────────────────────────────

    static OpponentDetailPanel BuildDetailPanel(RectTransform canvas)
    {
        var panelGO = new GameObject("OpponentDetail", typeof(RectTransform), typeof(OpponentDetailPanel));
        panelGO.transform.SetParent(canvas, false);
        Stretch((RectTransform)panelGO.transform);
        var panel = panelGO.GetComponent<OpponentDetailPanel>();

        // Overlay oscurecedor (este es el "root" que se muestra/oculta).
        var overlay = NewImage("Overlay", panelGO.transform, PanelDim);
        Stretch(overlay.rectTransform);

        // Tarjeta central.
        var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        card.transform.SetParent(overlay.transform, false);
        var cardRT = card.GetComponent<RectTransform>();
        cardRT.anchorMin = cardRT.anchorMax = cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(1320, 880);
        cardRT.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = PanelFill;
        var cvlg = card.GetComponent<VerticalLayoutGroup>();
        cvlg.padding = new RectOffset(36, 36, 28, 28); cvlg.spacing = 14;
        cvlg.childControlWidth = true; cvlg.childControlHeight = true;
        cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;

        // Header: retrato + info.
        var header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.transform.SetParent(cardRT, false);
        header.GetComponent<LayoutElement>().minHeight = 240;
        var hlg = header.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24; hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true; hlg.childAlignment = TextAnchor.MiddleLeft;

        var portrait = NewImage("Portrait", header.transform, Color.white);
        portrait.preserveAspect = true;
        var pLe = portrait.gameObject.AddComponent<LayoutElement>(); pLe.minWidth = 220; pLe.preferredWidth = 220;

        var info = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        info.transform.SetParent(header.transform, false);
        info.GetComponent<LayoutElement>().flexibleWidth = 1;
        var ivlg = info.GetComponent<VerticalLayoutGroup>();
        ivlg.spacing = 4; ivlg.childControlWidth = true; ivlg.childControlHeight = true;
        ivlg.childForceExpandWidth = true; ivlg.childForceExpandHeight = false; ivlg.childAlignment = TextAnchor.MiddleLeft;

        var nameText = MakeText("Name", info.transform, "Rival", 46, GoldBright, TextAlignmentOptions.Left);
        nameText.fontStyle = FontStyles.Bold;
        var difficulty = MakeText("Difficulty", info.transform, "★★★☆☆", 30, Gold, TextAlignmentOptions.Left);
        var record = MakeText("Record", info.transform, "Victorias: 0    Derrotas: 0    Mejor: 0", 26, TextLight, TextAlignmentOptions.Left);
        var discovery = MakeText("Discovery", info.transform, "Cartas descubiertas: 0/0", 26, TextLight, TextAlignmentOptions.Left);

        // Historia.
        var story = MakeText("Story", cardRT, "", 24, TextLight, TextAlignmentOptions.TopLeft);
        story.gameObject.AddComponent<LayoutElement>().minHeight = 90;
        story.color = new Color(TextLight.r, TextLight.g, TextLight.b, 0.85f);

        // Etiqueta.
        var label = MakeText("DropsLabel", cardRT, "Cartas que dropea:", 28, Gold, TextAlignmentOptions.Left);
        label.gameObject.AddComponent<LayoutElement>().minHeight = 40;

        // Scroll + grilla de drops.
        var scrollGO = DefaultControls.CreateScrollView(new DefaultControls.Resources());
        scrollGO.name = "DropScroll";
        scrollGO.transform.SetParent(cardRT, false);
        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        var hbar = scrollGO.transform.Find("Scrollbar Horizontal");
        if (hbar != null) { scroll.horizontalScrollbar = null; Object.DestroyImmediate(hbar.gameObject); }
        var vpImg = scroll.viewport != null ? scroll.viewport.GetComponent<Image>() : null;
        if (vpImg != null) vpImg.color = new Color(0f, 0f, 0f, 0.22f);
        var scrollLe = scrollGO.AddComponent<LayoutElement>();
        scrollLe.flexibleHeight = 1; scrollLe.minHeight = 320;

        var gridContent = scroll.content;
        var grid = gridContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(150, 210); grid.spacing = new Vector2(14, 14);
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.childAlignment = TextAnchor.UpperLeft;
        var gridFitter = gridContent.gameObject.AddComponent<ContentSizeFitter>();
        gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var dropView = BuildDropTemplate(gridContent);
        dropView.gameObject.SetActive(false);

        // Botones.
        var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        buttons.transform.SetParent(cardRT, false);
        buttons.GetComponent<LayoutElement>().minHeight = 72;
        var blg = buttons.GetComponent<HorizontalLayoutGroup>();
        blg.spacing = 18; blg.childControlWidth = true; blg.childControlHeight = true;
        blg.childForceExpandWidth = true; blg.childForceExpandHeight = true;
        var retar = MakeButton("Btn_Retar", buttons.transform, "Retar");
        var close = MakeButton("Btn_Cerrar", buttons.transform, "Cerrar");

        // Cablear el panel.
        var so = new SerializedObject(panel);
        Set(so, "root", overlay.gameObject);
        Set(so, "portrait", portrait);
        Set(so, "nameText", nameText);
        Set(so, "storyText", story);
        Set(so, "difficultyText", difficulty);
        Set(so, "recordText", record);
        Set(so, "discoveryText", discovery);
        Set(so, "dropGridContent", gridContent);
        Set(so, "dropCardTemplate", dropView);
        Set(so, "retarButton", retar);
        Set(so, "closeButton", close);
        so.ApplyModifiedPropertiesWithoutUndo();

        overlay.gameObject.SetActive(false); // oculto por defecto
        return panel;
    }

    static DropCardView BuildDropTemplate(Transform parent)
    {
        var go = new GameObject("DropCard", typeof(RectTransform), typeof(Image), typeof(DropCardView));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.16f, 1f);

        var art = NewImage("Art", go.transform, Color.white);
        art.preserveAspect = true;
        var artRT = art.rectTransform;
        artRT.anchorMin = new Vector2(0.06f, 0.20f);
        artRT.anchorMax = new Vector2(0.94f, 0.96f);
        artRT.offsetMin = Vector2.zero; artRT.offsetMax = Vector2.zero;

        var nameText = MakeText("Name", go.transform, "???", 18, TextLight, TextAlignmentOptions.Center);
        var nameRT = nameText.rectTransform;
        nameRT.anchorMin = new Vector2(0.02f, 0.01f);
        nameRT.anchorMax = new Vector2(0.98f, 0.19f);
        nameRT.offsetMin = Vector2.zero; nameRT.offsetMax = Vector2.zero;
        nameText.enableAutoSizing = true; nameText.fontSizeMin = 12; nameText.fontSizeMax = 18;

        var view = go.GetComponent<DropCardView>();
        var so = new SerializedObject(view);
        Set(so, "art", art);
        Set(so, "nameText", nameText);
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // ── Fábricas / utilidades ────────────────────────────────────────────

    static Button MakeButton(string name, Transform parent, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = Color.white;

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 64; le.preferredHeight = 64;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = BtnNormal; cb.highlightedColor = BtnHover; cb.pressedColor = BtnPressed;
        cb.selectedColor = BtnHover; cb.disabledColor = BtnDisabled;
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.1f;
        btn.colors = cb;

        var txt = MakeText("Label", go.transform, label, 30, TextLight, TextAlignmentOptions.Center);
        Stretch(txt.rectTransform);
        txt.margin = new Vector4(14, 0, 14, 0);
        return btn;
    }

    static void FitButtonText(Button btn)
    {
        var t = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (t == null) return;
        t.enableAutoSizing = true; t.fontSizeMin = 18; t.fontSizeMax = 30;
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
        t.raycastTarget = false;
        return t;
    }

    static void Set(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogError($"FreeDuelBuilder: no existe el campo '{prop}'."); return; }
        p.objectReferenceValue = value;
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
