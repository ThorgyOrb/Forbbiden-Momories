using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Construye la escena del MODO HISTORIA como OBJETOS REALES editables (título,
/// botón Volver, texto de progreso, hoja de ruta con una fila PLANTILLA de
/// capítulo, y el panel de detalle del rival) y cablea las referencias del
/// <see cref="StoryController"/> y del <see cref="StoryChapterView"/> de la
/// plantilla.
///
/// La hoja de ruta se genera en runtime clonando la plantilla; aquí solo se crea
/// el andamiaje visual, que puedes reestilizar libremente.
/// </summary>
public static class StoryBuilder
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
    static readonly Color RowFill     = new Color(0.09f, 0.10f, 0.18f, 0.96f);

    public static void BuildInScene(StoryController controller)
    {
        Scene scene = controller.gameObject.scene;

        var previous = FindRootInScene(scene, "StoryCanvas");
        if (previous != null) Object.DestroyImmediate(previous);

        EnsureEventSystem(scene);

        // ── Canvas ───────────────────────────────────────────────────────
        var canvasGO = new GameObject("StoryCanvas", typeof(Canvas), typeof(CanvasScaler),
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
        var title = MakeText("Title", canvasRT, "MODO HISTORIA", 84, GoldBright, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        AnchorTopStretch(title.rectTransform, 280, 280, 100, -40);
        title.enableAutoSizing = true; title.fontSizeMin = 32; title.fontSizeMax = 84;

        // ── Botón Volver (arriba-izquierda) ──────────────────────────────
        var backBtn = MakeButton("Btn_Volver", canvasRT, "Volver");
        var backRT = (RectTransform)backBtn.transform;
        backRT.anchorMin = backRT.anchorMax = new Vector2(0f, 1f);
        backRT.pivot = new Vector2(0f, 1f);
        backRT.anchoredPosition = new Vector2(40, -40);
        backRT.sizeDelta = new Vector2(180, 72);

        // ── Progreso (arriba-derecha) ────────────────────────────────────
        var progress = MakeText("Progress", canvasRT, "Progreso: 0 / 0", 30, TextLight, TextAlignmentOptions.Right);
        var progRT = progress.rectTransform;
        progRT.anchorMin = progRT.anchorMax = new Vector2(1f, 1f);
        progRT.pivot = new Vector2(1f, 1f);
        progRT.anchoredPosition = new Vector2(-40, -54);
        progRT.sizeDelta = new Vector2(460, 60);

        // ── Columna izquierda: hoja de ruta (ScrollView) ─────────────────
        var scrollGO = DefaultControls.CreateScrollView(new DefaultControls.Resources());
        scrollGO.name = "RoadmapScroll";
        scrollGO.transform.SetParent(canvasRT, false);
        var scrollRT = (RectTransform)scrollGO.transform;
        scrollRT.anchorMin = new Vector2(0f, 0f);
        scrollRT.anchorMax = new Vector2(0.40f, 1f);
        scrollRT.offsetMin = new Vector2(60, 60);
        scrollRT.offsetMax = new Vector2(-10, -240);

        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        var hbar = scrollGO.transform.Find("Scrollbar Horizontal");
        if (hbar != null) { scroll.horizontalScrollbar = null; Object.DestroyImmediate(hbar.gameObject); }
        var viewportImg = scroll.viewport != null ? scroll.viewport.GetComponent<Image>() : null;
        if (viewportImg != null) viewportImg.color = new Color(0f, 0f, 0f, 0.22f);

        var content = scroll.content;
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10; vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Fila plantilla de capítulo.
        var chapterView = BuildChapterTemplate(content);
        chapterView.gameObject.SetActive(false);

        // ── Columna derecha: detalle del capítulo ────────────────────────
        var (detailRoot, dPortrait, dChapter, dName, dDifficulty, dStatus, dStory, duelBtn, duelLabel, banner)
            = BuildDetailPanel(canvasRT);

        // ── Cablear el controlador ───────────────────────────────────────
        var so = new SerializedObject(controller);
        Set(so, "titleText", title);
        Set(so, "backButton", backBtn);
        Set(so, "progressText", progress);
        Set(so, "listContent", content);
        Set(so, "chapterTemplate", chapterView);
        Set(so, "detailRoot", detailRoot);
        Set(so, "detailPortrait", dPortrait);
        Set(so, "detailChapter", dChapter);
        Set(so, "detailName", dName);
        Set(so, "detailDifficulty", dDifficulty);
        Set(so, "detailStatus", dStatus);
        Set(so, "detailStory", dStory);
        Set(so, "duelButton", duelBtn);
        Set(so, "duelButtonLabel", duelLabel);
        Set(so, "completeBanner", banner);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        EditorSceneManager_MarkDirty(scene);
        Debug.Log("StoryBuilder: escena del Modo Historia construida y cableada.");
    }

    // ── Fila de capítulo ─────────────────────────────────────────────────

    static StoryChapterView BuildChapterTemplate(Transform parent)
    {
        var row = new GameObject("StoryChapter",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup),
            typeof(LayoutElement), typeof(StoryChapterView));
        row.transform.SetParent(parent, false);

        var rowImg = row.GetComponent<Image>();
        rowImg.color = Color.white;
        var rowBtn = row.GetComponent<Button>();
        rowBtn.targetGraphic = rowImg;
        var rcb = rowBtn.colors;
        rcb.normalColor = RowFill; rcb.highlightedColor = PanelHover; rcb.pressedColor = PanelHover;
        rcb.selectedColor = PanelHover; rcb.disabledColor = new Color(RowFill.r, RowFill.g, RowFill.b, 0.5f);
        rcb.colorMultiplier = 1f; rcb.fadeDuration = 0.1f;
        rowBtn.colors = rcb;

        var rle = row.GetComponent<LayoutElement>();
        rle.minHeight = 108; rle.preferredHeight = 108;
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 10, 10); hlg.spacing = 14;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        // Barra de estado (color según Derrotado/Actual/Bloqueado).
        var dot = NewImage("StatusDot", row.transform, Gold);
        var dotLe = dot.gameObject.AddComponent<LayoutElement>();
        dotLe.minWidth = 12; dotLe.preferredWidth = 12;

        // Retrato.
        var portrait = NewImage("Portrait", row.transform, Color.white);
        portrait.preserveAspect = true;
        var pLe = portrait.gameObject.AddComponent<LayoutElement>();
        pLe.minWidth = 84; pLe.preferredWidth = 84;

        // Info.
        var info = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        info.transform.SetParent(row.transform, false);
        info.GetComponent<LayoutElement>().flexibleWidth = 1;
        var ivlg = info.GetComponent<VerticalLayoutGroup>();
        ivlg.spacing = 1; ivlg.childControlWidth = true; ivlg.childControlHeight = true;
        ivlg.childForceExpandWidth = true; ivlg.childForceExpandHeight = false;
        ivlg.childAlignment = TextAnchor.MiddleLeft;

        var chapter = MakeText("Chapter", info.transform, "Capítulo 1", 22, Gold, TextAlignmentOptions.Left);
        var name = MakeText("Name", info.transform, "Nombre del rival", 30, GoldBright, TextAlignmentOptions.Left);
        name.fontStyle = FontStyles.Bold;
        var status = MakeText("Status", info.transform, "Actual", 22, TextLight, TextAlignmentOptions.Left);

        var view = row.GetComponent<StoryChapterView>();
        var so = new SerializedObject(view);
        Set(so, "selectButton", rowBtn);
        Set(so, "portrait", portrait);
        Set(so, "statusDot", dot);
        Set(so, "chapterText", chapter);
        Set(so, "nameText", name);
        Set(so, "statusText", status);
        so.ApplyModifiedPropertiesWithoutUndo();

        return view;
    }

    // ── Panel de detalle ─────────────────────────────────────────────────

    static (GameObject root, Image portrait, TextMeshProUGUI chapter, TextMeshProUGUI name,
            TextMeshProUGUI difficulty, TextMeshProUGUI status, TextMeshProUGUI story,
            Button duel, TextMeshProUGUI duelLabel, GameObject banner)
        BuildDetailPanel(RectTransform canvas)
    {
        // Contenedor derecho (referencia detailRoot: se oculta si no hay rival).
        var root = new GameObject("DetailPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        root.transform.SetParent(canvas, false);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.40f, 0f);
        rootRT.anchorMax = new Vector2(1f, 1f);
        rootRT.offsetMin = new Vector2(20, 60);
        rootRT.offsetMax = new Vector2(-60, -240);
        root.GetComponent<Image>().color = PanelFill;
        var rvlg = root.GetComponent<VerticalLayoutGroup>();
        rvlg.padding = new RectOffset(36, 36, 30, 30); rvlg.spacing = 16;
        rvlg.childControlWidth = true; rvlg.childControlHeight = true;
        rvlg.childForceExpandWidth = true; rvlg.childForceExpandHeight = false;

        // Header: retrato + info.
        var header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.transform.SetParent(rootRT, false);
        header.GetComponent<LayoutElement>().minHeight = 300;
        var hlg = header.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 28; hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true; hlg.childAlignment = TextAnchor.UpperLeft;

        var portrait = NewImage("Portrait", header.transform, Color.white);
        portrait.preserveAspect = true;
        var pLe = portrait.gameObject.AddComponent<LayoutElement>(); pLe.minWidth = 300; pLe.preferredWidth = 300;

        var info = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        info.transform.SetParent(header.transform, false);
        info.GetComponent<LayoutElement>().flexibleWidth = 1;
        var ivlg = info.GetComponent<VerticalLayoutGroup>();
        ivlg.spacing = 8; ivlg.childControlWidth = true; ivlg.childControlHeight = true;
        ivlg.childForceExpandWidth = true; ivlg.childForceExpandHeight = false; ivlg.childAlignment = TextAnchor.UpperLeft;

        var chapter = MakeText("Chapter", info.transform, "Capítulo 1", 30, Gold, TextAlignmentOptions.Left);
        var name = MakeText("Name", info.transform, "Rival", 52, GoldBright, TextAlignmentOptions.Left);
        name.fontStyle = FontStyles.Bold;
        var difficulty = MakeText("Difficulty", info.transform, "★★★☆☆", 32, Gold, TextAlignmentOptions.Left);
        var status = MakeText("Status", info.transform, "Siguiente rival", 28, TextLight, TextAlignmentOptions.Left);

        // Historia (scroll para textos largos).
        var storyLabel = MakeText("StoryLabel", rootRT, "Historia", 28, Gold, TextAlignmentOptions.Left);
        storyLabel.gameObject.AddComponent<LayoutElement>().minHeight = 38;

        var storyScrollGO = DefaultControls.CreateScrollView(new DefaultControls.Resources());
        storyScrollGO.name = "StoryScroll";
        storyScrollGO.transform.SetParent(rootRT, false);
        var storyScroll = storyScrollGO.GetComponent<ScrollRect>();
        storyScroll.horizontal = false; storyScroll.vertical = true;
        var shbar = storyScrollGO.transform.Find("Scrollbar Horizontal");
        if (shbar != null) { storyScroll.horizontalScrollbar = null; Object.DestroyImmediate(shbar.gameObject); }
        var svpImg = storyScroll.viewport != null ? storyScroll.viewport.GetComponent<Image>() : null;
        if (svpImg != null) svpImg.color = new Color(0f, 0f, 0f, 0.22f);
        var storyScrollLe = storyScrollGO.AddComponent<LayoutElement>();
        storyScrollLe.flexibleHeight = 1; storyScrollLe.minHeight = 300;

        var storyContent = storyScroll.content;
        var scvlg = storyContent.gameObject.AddComponent<VerticalLayoutGroup>();
        scvlg.padding = new RectOffset(16, 16, 16, 16); scvlg.spacing = 0;
        scvlg.childControlWidth = true; scvlg.childControlHeight = true;
        scvlg.childForceExpandWidth = true; scvlg.childForceExpandHeight = false;
        var scFitter = storyContent.gameObject.AddComponent<ContentSizeFitter>();
        scFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var story = MakeText("StoryText", storyContent, "", 26, TextLight, TextAlignmentOptions.TopLeft);
        story.color = new Color(TextLight.r, TextLight.g, TextLight.b, 0.9f);
        story.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Botón Duelar.
        var duelBtn = MakeButton("Btn_Duelar", rootRT, "Duelar");
        var duelLe = duelBtn.GetComponent<LayoutElement>();
        duelLe.minHeight = 80; duelLe.preferredHeight = 80;
        var duelLabel = duelBtn.GetComponentInChildren<TextMeshProUGUI>();

        // Cartel de campaña completada (oculto por defecto), encima del panel.
        var banner = MakeText("CompleteBanner", rootRT,
            "★ ¡Has completado la campaña! ★", 34, GoldBright, TextAlignmentOptions.Center);
        banner.fontStyle = FontStyles.Bold;
        banner.gameObject.AddComponent<LayoutElement>().minHeight = 56;
        banner.transform.SetAsFirstSibling();
        banner.gameObject.SetActive(false);

        return (root, portrait, chapter, name, difficulty, status, story, duelBtn, duelLabel, banner.gameObject);
    }

    // ── Fábricas / utilidades (idénticas al patrón de los otros builders) ─

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

        var txt = MakeText("Label", go.transform, label, 32, TextLight, TextAlignmentOptions.Center);
        Stretch(txt.rectTransform);
        txt.margin = new Vector4(14, 0, 14, 0);
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
        t.raycastTarget = false;
        return t;
    }

    static void Set(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogError($"StoryBuilder: no existe el campo '{prop}'."); return; }
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
