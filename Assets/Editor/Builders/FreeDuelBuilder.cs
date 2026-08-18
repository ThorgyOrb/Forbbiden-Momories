using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Construye la escena de Duelo Libre como OBJETOS REALES editables (título,
/// botón Volver, ScrollView y una tarjeta PLANTILLA de rival) y cablea las
/// referencias del <see cref="FreeDuelController"/>, del
/// <see cref="OpponentEntryView"/> de la plantilla y del
/// <see cref="OpponentDetailPanel"/>.
///
/// Estilo "Neo-Kemet" (ver memoria card-visual-style): fondo "Templo cibernético
/// de Anubis" + píldoras ornamentales egipcio-cyberpunk recortadas de
/// Resources/../UI/borders.png (Assets/Art/Sprites/UI/FreeDuel/, generadas por
/// <see cref="EnsureFreeDuelSprites"/> — recorte + import settings, idempotente).
///
/// Dos reglas de composición que se repiten por toda la pantalla:
///   • "Borde + relleno": un Image de color como marco y un hijo inset como fondo
///     de cristal. Evita depender de sprites 9-sliced que se deformarían.
///   • Los datos numéricos van en FICHAS separadas (valor grande + etiqueta
///     pequeña), no en líneas de texto apiladas — se leen de un vistazo.
///
/// La lista de rivales se genera en runtime clonando la plantilla; aquí solo se
/// crea el andamiaje visual, que puedes reestilizar libremente.
/// </summary>
public static class FreeDuelBuilder
{
    // ── Paleta "Neo-Kemet" ───────────────────────────────────────────────
    static readonly Color BgFallback   = new Color(0.03f, 0.03f, 0.06f);
    static readonly Color BgOverlay    = new Color(0.05f, 0.04f, 0.10f, 0.55f);
    static readonly Color Gold         = new Color(0.86f, 0.72f, 0.35f);
    static readonly Color GoldBright   = new Color(0.98f, 0.85f, 0.45f);
    static readonly Color TextLight    = new Color(0.86f, 0.85f, 0.95f, 0.92f);
    static readonly Color Muted        = new Color(0.60f, 0.58f, 0.72f, 0.85f);
    static readonly Color Violet       = new Color(0.56f, 0.36f, 0.92f, 0.9f);
    static readonly Color VioletBright = new Color(0.74f, 0.54f, 1f, 1f);
    static readonly Color GlassFill    = new Color(0.05f, 0.045f, 0.11f, 0.93f);
    static readonly Color TileBorder   = new Color(0.34f, 0.27f, 0.54f, 0.85f);
    static readonly Color TileFill     = new Color(0.085f, 0.075f, 0.155f, 0.95f);
    static readonly Color TrackDark    = new Color(0.11f, 0.10f, 0.19f, 1f);
    static readonly Color PanelBg      = new Color(0.045f, 0.04f, 0.095f, 0.97f);
    static readonly Color PanelDim     = new Color(0f, 0f, 0f, 0.82f);

    // ── Rutas de assets (recortados de borders.png/UI.png — ver EnsureFreeDuelSprites) ──
    const string SpriteDir = "Assets/Art/Sprites/UI/FreeDuel";
    const string BgPath = "Assets/Art/Sprites/UI/Templo cibernético de Anubis.png";
    const string PillPurplePath = SpriteDir + "/pill_purple.png";
    const string FrameWingsAnubisPath = SpriteDir + "/frame_wings_anubis.png";
    const string IconBackPath = SpriteDir + "/icon_back.png";

    /// <summary>Sprite blanco de UGUI. Un Image con sprite NULL ignora fillAmount
    /// (cae al mesh simple de Graphic), así que las barras de progreso lo necesitan.</summary>
    static Sprite UiSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    public static void BuildInScene(FreeDuelController controller)
    {
        EnsureFreeDuelSprites();

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
        var bgFallback = NewImage("BackgroundColor", canvasRT, BgFallback);
        Stretch(bgFallback.rectTransform);
        var bgArt = NewImage("BackgroundArt", canvasRT, Color.white);
        bgArt.sprite = Load<Sprite>(BgPath);
        bgArt.gameObject.AddComponent<ResponsiveBackground>();
        var bgOverlay = NewImage("BackgroundOverlay", canvasRT, BgOverlay);
        Stretch(bgOverlay.rectTransform);

        // ── Título ───────────────────────────────────────────────────────
        var title = MakeText("Title", canvasRT, "DUELO LIBRE", 76, GoldBright, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 6;
        AnchorTopStretch(title.rectTransform, 260, 260, 96, -36);
        title.enableAutoSizing = true; title.fontSizeMin = 32; title.fontSizeMax = 76;

        var titleRule = NewImage("TitleRule", canvasRT, Color.white);
        titleRule.sprite = Load<Sprite>(PillPurplePath);
        titleRule.type = Image.Type.Sliced;
        var ruleRT = titleRule.rectTransform;
        ruleRT.anchorMin = ruleRT.anchorMax = new Vector2(0.5f, 1f);
        ruleRT.pivot = new Vector2(0.5f, 1f);
        ruleRT.anchoredPosition = new Vector2(0, -142);
        ruleRT.sizeDelta = new Vector2(620, 20);

        // ── Botón Volver (arriba-izquierda) ──────────────────────────────
        var backBtn = MakeIconButton("Btn_Volver", canvasRT, IconBackPath, "VOLVER", 232);
        var backRT = (RectTransform)backBtn.transform;
        backRT.anchorMin = backRT.anchorMax = new Vector2(0f, 1f);
        backRT.pivot = new Vector2(0f, 1f);
        backRT.anchoredPosition = new Vector2(40, -40);

        // ── ScrollView ───────────────────────────────────────────────────
        var scrollGO = DefaultControls.CreateScrollView(new DefaultControls.Resources());
        scrollGO.name = "OpponentScroll";
        scrollGO.transform.SetParent(canvasRT, false);
        var scrollRT = (RectTransform)scrollGO.transform;
        scrollRT.anchorMin = new Vector2(0f, 0f);
        scrollRT.anchorMax = new Vector2(1f, 1f);
        scrollRT.offsetMin = new Vector2(140, 50);
        scrollRT.offsetMax = new Vector2(-140, -190);

        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        var hbar = scrollGO.transform.Find("Scrollbar Horizontal");
        if (hbar != null) { scroll.horizontalScrollbar = null; Object.DestroyImmediate(hbar.gameObject); }
        StyleScrollView(scrollGO, scroll);

        var content = scroll.content;
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 16; vlg.padding = new RectOffset(10, 10, 10, 10);
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
        Set(so, "listContent", content);
        Set(so, "entryTemplate", entryView);
        Set(so, "detailPanel", detail);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        EditorSceneManager_MarkDirty(scene);
        Debug.Log("FreeDuelBuilder: escena de Duelo Libre construida y cableada (estilo Neo-Kemet).");
    }

    // ── Tarjeta de rival ─────────────────────────────────────────────────

    /// <summary>
    /// Fila de rival, repartida en CUATRO bloques horizontales para que nada se
    /// amontone: retrato · (nombre + dificultad, barra de descubrimiento) ·
    /// fichas de récord · botón Retar.
    /// </summary>
    static OpponentEntryView BuildEntryTemplate(Transform parent)
    {
        var card = new GameObject("OpponentEntry",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup),
            typeof(LayoutElement), typeof(OpponentEntryView));
        card.transform.SetParent(parent, false);

        // El Image del propio "card" hace de BORDE (violeta); el Fill inset simula
        // el panel de cristal oscuro — sin necesitar un sprite dedicado.
        var cardImg = card.GetComponent<Image>();
        cardImg.color = Color.white;
        var cardBtn = card.GetComponent<Button>();
        cardBtn.targetGraphic = cardImg;
        var ccb = cardBtn.colors;
        ccb.normalColor = Violet; ccb.highlightedColor = VioletBright; ccb.pressedColor = Violet;
        ccb.selectedColor = VioletBright; ccb.disabledColor = new Color(0.3f, 0.3f, 0.35f, 0.5f);
        ccb.colorMultiplier = 1f; ccb.fadeDuration = 0.1f;
        cardBtn.colors = ccb;

        var cle = card.GetComponent<LayoutElement>();
        cle.minHeight = 152; cle.preferredHeight = 152;
        var hlg = card.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(20, 20, 18, 18); hlg.spacing = 22;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        // ForceExpandHeight NO: estiraría el botón-píldora a todo el alto de la fila y
        // deformaría las gemas de sus extremos. Cada bloque conserva su alto y se centra.
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var fill = NewImage("Fill", card.transform, GlassFill);
        var fillLe = fill.gameObject.AddComponent<LayoutElement>();
        fillLe.ignoreLayout = true;
        var frt = fill.rectTransform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(3, 3); frt.offsetMax = new Vector2(-3, -3);

        // 1) Retrato enmarcado.
        var portrait = BuildFramedSquare(card.transform, "PortraitFrame", 112, Gold, new Color(0.08f, 0.07f, 0.14f, 1f), 4);

        // 2) Columna de identidad: nombre + dificultad arriba, barra de descubrimiento abajo.
        var info = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        info.transform.SetParent(card.transform, false);
        info.GetComponent<LayoutElement>().flexibleWidth = 1;
        var ivlg = info.GetComponent<VerticalLayoutGroup>();
        ivlg.spacing = 14; ivlg.childControlWidth = true; ivlg.childControlHeight = true;
        ivlg.childForceExpandWidth = true; ivlg.childForceExpandHeight = false;
        ivlg.childAlignment = TextAnchor.MiddleLeft;

        var nameRow = new GameObject("NameRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        nameRow.transform.SetParent(info.transform, false);
        nameRow.GetComponent<LayoutElement>().minHeight = 44;
        var nrlg = nameRow.GetComponent<HorizontalLayoutGroup>();
        nrlg.spacing = 18; nrlg.childControlWidth = true; nrlg.childControlHeight = true;
        nrlg.childForceExpandWidth = false; nrlg.childForceExpandHeight = false;
        nrlg.childAlignment = TextAnchor.MiddleLeft;
        var nameText = MakeText("Name", nameRow.transform, "Nombre del rival", 34, GoldBright, TextAlignmentOptions.Left);
        nameText.fontStyle = FontStyles.Bold;
        var nameLe = nameText.gameObject.AddComponent<LayoutElement>();
        nameLe.preferredWidth = 380; nameLe.flexibleWidth = 0;
        var pips = BuildDifficultyPips(nameRow.transform);

        var discoveryFill = BuildDiscoveryRow(info.transform, out var discoveryValue);

        // 3) Fichas de récord (valor grande + etiqueta): se leen de un vistazo.
        var stats = new GameObject("Stats", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        stats.transform.SetParent(card.transform, false);
        var statsLe = stats.GetComponent<LayoutElement>();
        statsLe.minWidth = statsLe.preferredWidth = 332; statsLe.flexibleWidth = 0;
        var slg = stats.GetComponent<HorizontalLayoutGroup>();
        slg.spacing = 10; slg.childControlWidth = true; slg.childControlHeight = true;
        slg.childForceExpandWidth = false; slg.childForceExpandHeight = false;
        slg.childAlignment = TextAnchor.MiddleCenter;
        var winsValue = MakeStatTile(stats.transform, "VICTORIAS", 104, 66);
        var lossesValue = MakeStatTile(stats.transform, "DERROTAS", 104, 66);
        var bestValue = MakeStatTile(stats.transform, "MEJOR", 104, 66);

        // 4) Botón Retar.
        var duelBtn = MakePillButton("Btn_Retar", card.transform, "Retar  »", PillPurplePath);
        var dLe = duelBtn.GetComponent<LayoutElement>();
        dLe.minWidth = 200; dLe.preferredWidth = 200; dLe.flexibleWidth = 0;
        dLe.minHeight = 66; dLe.preferredHeight = 66; dLe.flexibleHeight = 0;

        var view = card.GetComponent<OpponentEntryView>();
        var so = new SerializedObject(view);
        Set(so, "cardButton", cardBtn);
        Set(so, "portrait", portrait);
        Set(so, "nameText", nameText);
        SetArray(so, "difficultyPips", pips);
        Set(so, "winsValue", winsValue);
        Set(so, "lossesValue", lossesValue);
        Set(so, "bestScoreValue", bestValue);
        Set(so, "discoveryValue", discoveryValue);
        Set(so, "discoveryFill", discoveryFill);
        Set(so, "duelButton", duelBtn);
        so.ApplyModifiedPropertiesWithoutUndo();

        return view;
    }

    // ── Panel de detalle ─────────────────────────────────────────────────

    /// <summary>
    /// Modal a dos columnas: una RAIL ESTRECHA a la izquierda con la identidad del
    /// rival (retrato, récord, progreso, acciones) y el resto del panel — la parte
    /// ancha y alta — dedicado a la grilla de cartas que dropea, que es lo que el
    /// jugador viene a mirar.
    /// </summary>
    static OpponentDetailPanel BuildDetailPanel(RectTransform canvas)
    {
        var panelGO = new GameObject("OpponentDetail", typeof(RectTransform), typeof(OpponentDetailPanel));
        panelGO.transform.SetParent(canvas, false);
        Stretch((RectTransform)panelGO.transform);
        var panel = panelGO.GetComponent<OpponentDetailPanel>();

        // Overlay oscurecedor (este es el "root" que se muestra/oculta).
        var overlay = NewImage("Overlay", panelGO.transform, PanelDim);
        Stretch(overlay.rectTransform);

        // Panel grande de cristal (borde violeta + relleno oscuro, mismo lenguaje que
        // las filas de la lista). El marco ornamental con ala/estatuas de Anubis se
        // probó como fondo, pero sus bandas decorativas fijas se comían más de la
        // mitad del alto útil; se dejó fuera a propósito.
        var panelFill = BuildBorderedPanel(overlay.transform, "Panel", 1700, 950, Violet, PanelBg, 4);
        var rootHlg = panelFill.gameObject.AddComponent<HorizontalLayoutGroup>();
        rootHlg.padding = new RectOffset(30, 30, 30, 30); rootHlg.spacing = 26;
        rootHlg.childControlWidth = true; rootHlg.childControlHeight = true;
        rootHlg.childForceExpandWidth = false; rootHlg.childForceExpandHeight = true;

        // ── Columna izquierda: identidad del rival ───────────────────────
        var left = new GameObject("RivalColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        left.transform.SetParent(panelFill, false);
        var leftLe = left.GetComponent<LayoutElement>();
        leftLe.minWidth = leftLe.preferredWidth = 380; leftLe.flexibleWidth = 0;
        var lvlg = left.GetComponent<VerticalLayoutGroup>();
        lvlg.spacing = 14; lvlg.childControlWidth = true; lvlg.childControlHeight = true;
        lvlg.childForceExpandWidth = true; lvlg.childForceExpandHeight = false;

        var portrait = BuildFramedSquare(left.transform, "PortraitFrame", 300, Gold, new Color(0.08f, 0.07f, 0.14f, 1f), 5);

        var nameText = MakeText("Name", left.transform, "Rival", 38, GoldBright, TextAlignmentOptions.Center);
        nameText.fontStyle = FontStyles.Bold;
        nameText.gameObject.AddComponent<LayoutElement>().minHeight = 46;
        nameText.enableAutoSizing = true; nameText.fontSizeMin = 24; nameText.fontSizeMax = 38;

        var pipsRow = new GameObject("DifficultyRow", typeof(RectTransform), typeof(LayoutElement));
        pipsRow.transform.SetParent(left.transform, false);
        pipsRow.GetComponent<LayoutElement>().minHeight = 26;
        var pips = BuildDifficultyPips(pipsRow.transform, TextAnchor.MiddleCenter, stretch: true);

        var statsRow = new GameObject("Stats", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        statsRow.transform.SetParent(left.transform, false);
        statsRow.GetComponent<LayoutElement>().minHeight = 68;
        var srlg = statsRow.GetComponent<HorizontalLayoutGroup>();
        srlg.spacing = 12; srlg.childControlWidth = true; srlg.childControlHeight = true;
        srlg.childForceExpandWidth = false; srlg.childForceExpandHeight = false;
        srlg.childAlignment = TextAnchor.MiddleCenter;
        var winsValue = MakeStatTile(statsRow.transform, "VICTORIAS", 118, 68);
        var lossesValue = MakeStatTile(statsRow.transform, "DERROTAS", 118, 68);
        var bestValue = MakeStatTile(statsRow.transform, "MEJOR", 118, 68);

        var discoveryFill = BuildDiscoveryRow(left.transform, out var discoveryValue);

        // Historia: el panel la OCULTA si el rival no tiene uno escrito.
        var story = MakeText("Story", left.transform, "", 20, TextLight, TextAlignmentOptions.TopLeft);
        var storyLe = story.gameObject.AddComponent<LayoutElement>();
        storyLe.minHeight = 40; storyLe.flexibleHeight = 1;

        // Empuja los botones al fondo de la columna.
        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(left.transform, false);
        spacer.GetComponent<LayoutElement>().flexibleHeight = 1;

        var retar = MakePillButton("Btn_Retar", left.transform, "Retar  »", PillPurplePath);
        retar.GetComponent<LayoutElement>().minHeight = 62;
        var close = MakePillButton("Btn_Cerrar", left.transform, "Cerrar", PillPurplePath);
        close.GetComponent<LayoutElement>().minHeight = 62;

        // ── Columna derecha: LAS CARTAS (protagonista) ───────────────────
        var right = new GameObject("DropsColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        right.transform.SetParent(panelFill, false);
        right.GetComponent<LayoutElement>().flexibleWidth = 1;
        var rvlg = right.GetComponent<VerticalLayoutGroup>();
        rvlg.spacing = 14; rvlg.childControlWidth = true; rvlg.childControlHeight = true;
        rvlg.childForceExpandWidth = true; rvlg.childForceExpandHeight = false;

        var headerRow = new GameObject("DropsHeader", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        headerRow.transform.SetParent(right.transform, false);
        headerRow.GetComponent<LayoutElement>().minHeight = 52;
        var hrlg = headerRow.GetComponent<HorizontalLayoutGroup>();
        hrlg.spacing = 14; hrlg.childControlWidth = true; hrlg.childControlHeight = true;
        hrlg.childForceExpandWidth = false; hrlg.childForceExpandHeight = false;
        hrlg.childAlignment = TextAnchor.MiddleLeft;

        var label = MakeText("DropsLabel", headerRow.transform, "CARTAS QUE DROPEA", 26, Gold, TextAlignmentOptions.Left);
        label.fontStyle = FontStyles.Bold; label.characterSpacing = 3;
        var labelLe = label.gameObject.AddComponent<LayoutElement>();
        labelLe.preferredWidth = 340; labelLe.flexibleWidth = 1;

        var tabPow = MakePillButton("Tab_Pow", headerRow.transform, "POW", PillPurplePath);
        var tabTec = MakePillButton("Tab_Tec", headerRow.transform, "TEC", PillPurplePath);
        var tabBcd = MakePillButton("Tab_Bcd", headerRow.transform, "B/C/D", PillPurplePath);
        foreach (var tab in new[] { tabPow, tabTec, tabBcd })
        {
            var tle = tab.GetComponent<LayoutElement>();
            tle.minWidth = 118; tle.preferredWidth = 118; tle.flexibleWidth = 0;
            tle.minHeight = 48; tle.preferredHeight = 48; tle.flexibleHeight = 0;
            var tlabel = tab.GetComponentInChildren<TextMeshProUGUI>();
            tlabel.fontSize = 22; tlabel.fontSizeMax = 22;
        }

        var scrollGO = DefaultControls.CreateScrollView(new DefaultControls.Resources());
        scrollGO.name = "DropScroll";
        scrollGO.transform.SetParent(right.transform, false);
        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        var hbar = scrollGO.transform.Find("Scrollbar Horizontal");
        if (hbar != null) { scroll.horizontalScrollbar = null; Object.DestroyImmediate(hbar.gameObject); }
        StyleScrollView(scrollGO, scroll);
        var scrollLe = scrollGO.AddComponent<LayoutElement>();
        scrollLe.flexibleHeight = 1; scrollLe.minHeight = 400;

        var gridContent = scroll.content;
        var grid = gridContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(172, 232); grid.spacing = new Vector2(16, 16);
        grid.padding = new RectOffset(14, 14, 14, 14);
        grid.childAlignment = TextAnchor.UpperLeft;
        var gridFitter = gridContent.gameObject.AddComponent<ContentSizeFitter>();
        gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var dropView = BuildDropTemplate(gridContent);
        dropView.gameObject.SetActive(false);

        // Aviso de tabla vacía, centrado SOBRE la grilla (fuera del layout, hijo
        // del ScrollView y no del content, para no contar como una celda).
        var emptyTable = MakeText("EmptyTable", scrollGO.transform,
            "Este rival no suelta cartas con este rango.", 24, Muted, TextAlignmentOptions.Center);
        Stretch(emptyTable.rectTransform);
        emptyTable.gameObject.SetActive(false);

        // Cablear el panel.
        var so = new SerializedObject(panel);
        Set(so, "root", overlay.gameObject);
        Set(so, "portrait", portrait);
        Set(so, "nameText", nameText);
        Set(so, "storyText", story);
        SetArray(so, "difficultyPips", pips);
        Set(so, "winsValue", winsValue);
        Set(so, "lossesValue", lossesValue);
        Set(so, "bestScoreValue", bestValue);
        Set(so, "discoveryValue", discoveryValue);
        Set(so, "discoveryFill", discoveryFill);
        Set(so, "dropGridContent", gridContent);
        Set(so, "dropCardTemplate", dropView);
        Set(so, "emptyTableText", emptyTable);
        Set(so, "tabPowButton", tabPow);
        Set(so, "tabTecButton", tabTec);
        Set(so, "tabBcdButton", tabBcd);
        Set(so, "retarButton", retar);
        Set(so, "closeButton", close);
        so.ApplyModifiedPropertiesWithoutUndo();

        overlay.gameObject.SetActive(false); // oculto por defecto
        return panel;
    }

    static DropCardView BuildDropTemplate(Transform parent)
    {
        // El Image del propio "go" es el BORDE (lo tiñe DropCardView por rareza al
        // descubrirla); el Fill inset es el fondo real de la carta.
        var go = new GameObject("DropCard", typeof(RectTransform), typeof(Image), typeof(DropCardView));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = Gold;

        var fill = NewImage("Fill", go.transform, new Color(0.06f, 0.05f, 0.12f, 1f));
        var fillRT = fill.rectTransform;
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = new Vector2(2, 2); fillRT.offsetMax = new Vector2(-2, -2);

        var art = NewImage("Art", fill.transform, Color.white);
        art.preserveAspect = true;
        var artRT = art.rectTransform;
        artRT.anchorMin = new Vector2(0.06f, 0.22f);
        artRT.anchorMax = new Vector2(0.94f, 0.96f);
        artRT.offsetMin = Vector2.zero; artRT.offsetMax = Vector2.zero;

        var nameText = MakeText("Name", fill.transform, "???", 18, TextLight, TextAlignmentOptions.Center);
        var nameRT = nameText.rectTransform;
        nameRT.anchorMin = new Vector2(0.03f, 0.02f);
        nameRT.anchorMax = new Vector2(0.97f, 0.21f);
        nameRT.offsetMin = Vector2.zero; nameRT.offsetMax = Vector2.zero;
        nameText.enableAutoSizing = true; nameText.fontSizeMin = 12; nameText.fontSizeMax = 18;

        var probText = MakeText("Probability", fill.transform, "0.00%", 16, GoldBright, TextAlignmentOptions.TopRight);
        var probRT = probText.rectTransform;
        probRT.anchorMin = new Vector2(0f, 0.86f);
        probRT.anchorMax = new Vector2(0.96f, 1f);
        probRT.offsetMin = Vector2.zero; probRT.offsetMax = Vector2.zero;

        var view = go.GetComponent<DropCardView>();
        var so = new SerializedObject(view);
        Set(so, "border", go.GetComponent<Image>());
        Set(so, "art", art);
        Set(so, "nameText", nameText);
        Set(so, "probabilityText", probText);
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // ── Piezas reutilizables ─────────────────────────────────────────────

    /// <summary>
    /// Ficha de dato: VALOR grande arriba + etiqueta pequeña abajo, en su propia
    /// cajita con borde. Devuelve el texto del VALOR (lo que se actualiza en runtime).
    /// </summary>
    static TextMeshProUGUI MakeStatTile(Transform parent, string labelText, float width, float height)
    {
        var holder = new GameObject($"Stat_{labelText}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        holder.transform.SetParent(parent, false);
        holder.GetComponent<Image>().color = TileBorder;
        var le = holder.GetComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = width; le.flexibleWidth = 0;
        le.minHeight = le.preferredHeight = height; le.flexibleHeight = 0;

        var fill = NewImage("Fill", holder.transform, TileFill);
        var frt = fill.rectTransform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(2, 2); frt.offsetMax = new Vector2(-2, -2);
        var vlg = fill.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(4, 4, 8, 6); vlg.spacing = 0;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.MiddleCenter;

        var value = MakeText("Value", fill.transform, "0", 28, GoldBright, TextAlignmentOptions.Center);
        value.fontStyle = FontStyles.Bold;
        value.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

        var lbl = MakeText("Label", fill.transform, labelText, 13, Muted, TextAlignmentOptions.Center);
        lbl.characterSpacing = 2;
        lbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

        return value;
    }

    /// <summary>
    /// Fila "CARTAS  [====barra====]  18/27". Devuelve el Image de RELLENO de la
    /// barra (tipo Filled) y saca por <paramref name="valueText"/> el contador.
    /// </summary>
    static Image BuildDiscoveryRow(Transform parent, out TextMeshProUGUI valueText)
    {
        var row = new GameObject("DiscoveryRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().minHeight = 30;
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12; hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var lbl = MakeText("Label", row.transform, "CARTAS", 13, Muted, TextAlignmentOptions.Left);
        lbl.characterSpacing = 2;
        var lblLe = lbl.gameObject.AddComponent<LayoutElement>();
        lblLe.preferredWidth = 66; lblLe.flexibleWidth = 0; lblLe.preferredHeight = 18;

        var track = NewImage("Track", row.transform, TrackDark);
        var trackLe = track.gameObject.AddComponent<LayoutElement>();
        trackLe.flexibleWidth = 1; trackLe.preferredHeight = 12; trackLe.flexibleHeight = 0;

        var barFill = NewImage("Fill", track.transform, Gold);
        Stretch(barFill.rectTransform);
        barFill.sprite = UiSprite;                       // sin sprite, fillAmount se ignora
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        barFill.fillAmount = 1f;

        valueText = MakeText("Value", row.transform, "0/0", 20, GoldBright, TextAlignmentOptions.Right);
        var valLe = valueText.gameObject.AddComponent<LayoutElement>();
        valLe.preferredWidth = 96; valLe.flexibleWidth = 0; valLe.preferredHeight = 24;

        return barFill;
    }

    /// <summary>5 fichas cuadradas (llenas en oro / vacías tenues) que marcan la dificultad.</summary>
    static Image[] BuildDifficultyPips(Transform parent, TextAnchor align = TextAnchor.MiddleLeft, bool stretch = false)
    {
        var row = new GameObject("DifficultyPips", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        var rlg = row.GetComponent<HorizontalLayoutGroup>();
        rlg.spacing = 6; rlg.childControlWidth = true; rlg.childControlHeight = true;
        rlg.childForceExpandWidth = false; rlg.childForceExpandHeight = false;
        rlg.childAlignment = align;
        row.GetComponent<LayoutElement>().preferredWidth = 130;
        if (stretch) Stretch((RectTransform)row.transform);

        var pips = new Image[5];
        for (int i = 0; i < pips.Length; i++)
        {
            var pip = NewImage($"Pip{i}", row.transform, Gold);
            var le = pip.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 20; le.preferredHeight = 20;
            pips[i] = pip;
        }
        return pips;
    }

    /// <summary>Cuadro con borde de color y relleno; devuelve el Image de RELLENO (para pintar retratos encima).</summary>
    static Image BuildFramedSquare(Transform parent, string name, float size, Color borderColor, Color fillColor, float thickness)
    {
        var holder = new GameObject(name, typeof(RectTransform), typeof(Image));
        holder.transform.SetParent(parent, false);
        holder.GetComponent<Image>().color = borderColor;
        var le = holder.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = size;
        le.minHeight = le.preferredHeight = size;

        var back = NewImage("Back", holder.transform, fillColor);
        var brt = back.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(thickness, thickness); brt.offsetMax = new Vector2(-thickness, -thickness);

        var content = NewImage("Portrait", holder.transform, Color.white);
        content.preserveAspect = true;
        var crt = content.rectTransform;
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(thickness, thickness); crt.offsetMax = new Vector2(-thickness, -thickness);

        return content;
    }

    /// <summary>Panel rectangular con borde de color y relleno; devuelve el RectTransform de
    /// RELLENO (ya inset por <paramref name="thickness"/>) para colgarle contenido encima.</summary>
    static RectTransform BuildBorderedPanel(Transform parent, string name, float width, float height,
                                             Color borderColor, Color fillColor, float thickness)
    {
        var holder = new GameObject(name, typeof(RectTransform), typeof(Image));
        holder.transform.SetParent(parent, false);
        var hrt = (RectTransform)holder.transform;
        hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.sizeDelta = new Vector2(width, height);
        holder.GetComponent<Image>().color = borderColor;

        var fill = NewImage("Fill", holder.transform, fillColor);
        var frt = fill.rectTransform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(thickness, thickness); frt.offsetMax = new Vector2(-thickness, -thickness);

        return frt;
    }

    // ── Fábricas de botón ────────────────────────────────────────────────

    /// <summary>
    /// Botón con fondo de imagen (9-sliced, el border viene del import del sprite —
    /// ver <see cref="EnsureFreeDuelSprites"/>) y etiqueta de texto centrada, con margen
    /// izquierdo opcional (para no tapar un icono pintado en el propio sprite).
    /// </summary>
    static Button MakePillButton(string name, Transform parent, string label, string spritePath, float leftTextMargin = 0)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = Load<Sprite>(spritePath);
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 64; le.preferredHeight = 64;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.25f, 1.2f, 1.35f);
        cb.pressedColor = new Color(0.8f, 0.78f, 0.85f); cb.selectedColor = new Color(1.15f, 1.1f, 1.25f);
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.1f;
        btn.colors = cb;

        var txt = MakeText("Label", go.transform, label, 28, GoldBright, TextAlignmentOptions.Center);
        Stretch(txt.rectTransform);
        txt.margin = new Vector4(16 + leftTextMargin, 0, 16, 0);
        txt.enableAutoSizing = true; txt.fontSizeMin = 16; txt.fontSizeMax = 28;
        return btn;
    }

    /// <summary>Botón pill con un icono a la izquierda + etiqueta (para "Volver").</summary>
    static Button MakeIconButton(string name, Transform parent, string iconPath, string label, float width)
    {
        var btn = MakePillButton(name, parent, "", PillPurplePath);
        var le = btn.GetComponent<LayoutElement>();
        le.minWidth = width; le.preferredWidth = width;
        var rt = (RectTransform)btn.transform;
        rt.sizeDelta = new Vector2(width, 64);

        var icon = NewImage("Icon", btn.transform, Color.white);
        icon.sprite = Load<Sprite>(iconPath);
        icon.preserveAspect = true;
        var irt = icon.rectTransform;
        irt.anchorMin = new Vector2(0f, 0.5f); irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(18, 0);
        irt.sizeDelta = new Vector2(34, 34);

        var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        txt.text = label;
        txt.margin = new Vector4(64, 0, 16, 0);
        return btn;
    }

    /// <summary>
    /// Restiliza un ScrollView de <see cref="DefaultControls.CreateScrollView"/> (que sale con
    /// colores grises de Unity por defecto) a la paleta Neo-Kemet: viewport con borde violeta +
    /// relleno de cristal (mismo truco borde/relleno que las tarjetas), scrollbar oscura con
    /// manija dorada, y <see cref="ScrollRect.MovementType.Clamped"/> para que NO haga rebote
    /// elástico más allá del contenido (el "se ve raro" de scrollear hacia donde no hay nada).
    /// </summary>
    static void StyleScrollView(GameObject scrollGO, ScrollRect scroll)
    {
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var rootImg = scrollGO.GetComponent<Image>();
        if (rootImg != null) rootImg.color = Violet;

        if (scroll.viewport != null)
        {
            scroll.viewport.offsetMin = new Vector2(3, 3);
            scroll.viewport.offsetMax = new Vector2(-3, -3);
            var vpImg = scroll.viewport.GetComponent<Image>();
            if (vpImg != null) vpImg.color = GlassFill;
        }

        var vbar = scroll.verticalScrollbar;
        if (vbar == null) return;
        var track = vbar.GetComponent<Image>();
        if (track != null) track.color = new Color(0.05f, 0.045f, 0.10f, 0.85f);
        var vbarRT = vbar.GetComponent<RectTransform>();
        vbarRT.sizeDelta = new Vector2(14, vbarRT.sizeDelta.y);
        if (vbar.handleRect != null)
        {
            var handleImg = vbar.handleRect.GetComponent<Image>();
            if (handleImg != null) handleImg.color = Gold;
        }
    }

    // ── Fábricas / utilidades ────────────────────────────────────────────

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

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    static void Set(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogError($"FreeDuelBuilder: no existe el campo '{prop}'."); return; }
        p.objectReferenceValue = value;
    }

    static void SetArray(SerializedObject so, string prop, Object[] values)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogError($"FreeDuelBuilder: no existe el campo '{prop}'."); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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

    // ── Import de los sprites recortados (idempotente) ──────────────────

    [MenuItem("YGO/Setup/Recortar sprites de Duelo Libre (borders.png)")]
    public static void EnsureFreeDuelSprites()
    {
        // Los PNG de FreeDuel/ pueden existir en disco sin que el AssetDatabase los
        // conozca todavía (recortados fuera del Editor) — sin este Refresh,
        // AssetImporter.GetAtPath los devuelve null la primera vez.
        AssetDatabase.Refresh();

        EnsureSprite(PillPurplePath, new Vector4(95, 10, 95, 10));
        EnsureSprite(FrameWingsAnubisPath, null);
        EnsureSprite(IconBackPath, null);
    }

    static void EnsureSprite(string path, Vector4? border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"FreeDuelBuilder: no encuentro '{path}'."); return; }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
        if (border.HasValue && importer.spriteBorder != border.Value) { importer.spriteBorder = border.Value; changed = true; }

        if (changed) { EditorUtility.SetDirty(importer); importer.SaveAndReimport(); }
    }
}
