using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Genera una escena NUEVA "LibraryGodsScene" con el rediseño del catálogo
/// (mockup "Library of the Gods / Kemet Archive System"), en estilo plano con la
/// paleta Neo-Kemet (placeholders donde luego va el arte). NO toca la escena
/// existente. Reusa LibraryCardSlot, LibraryQueryService, CardDisplay y un
/// Model3DViewer mínimo, todo cableado en <see cref="LibraryGodsController"/>.
///
/// Menú: YGO ▸ Escenas ▸ Construir Library of the Gods.
/// </summary>
public static class LibraryGodsSceneBuilder
{
    const string ScenePath      = "Assets/Scenes/LibraryGodsScene.unity";
    // Carta V2 (layout TCG) en toda la escena: grilla y preview del detalle.
    // OJO: el slot del grid es el PROPIO prefab de carta (trae CardDisplay +
    // LibraryCardSlot), igual que en LibraryCatalogScene. El viejo
    // Resources/Prefabs/LibraryCardSlot.prefab está obsoleto (sin CardDisplay,
    // campos de otra versión) y sale en blanco.
    const string CardPrefabPath = "Assets/Resources/Prefabs/CardMonsterV2.prefab";
    const string IconConfigPath = "Assets/Scripts/CardIconConfig.asset";

    // ── Paleta Neo-Kemet ─────────────────────────────────────────────────
    static readonly Color Bg        = new Color(0.043f, 0.045f, 0.075f);
    static readonly Color Panel     = new Color(0.075f, 0.075f, 0.11f, 0.94f);
    static readonly Color PanelSoft = new Color(0.11f, 0.10f, 0.15f, 0.85f);
    static readonly Color Gold      = new Color(0.93f, 0.75f, 0.33f);
    static readonly Color Cyan      = new Color(0.22f, 0.95f, 0.86f);
    static readonly Color Violet    = new Color(0.63f, 0.40f, 0.95f);
    static readonly Color Bright    = new Color(0.93f, 0.90f, 0.83f);
    static readonly Color Muted     = new Color(0.58f, 0.55f, 0.50f);
    static readonly Color Line      = new Color(0.93f, 0.75f, 0.33f, 0.30f);

    [MenuItem("YGO/Escenas/Construir Library of the Gods")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (cardPrefab == null) Debug.LogError($"LibraryGodsSceneBuilder: falta {CardPrefabPath}");
        var slotPrefab = cardPrefab; // el slot del grid ES el prefab de carta (ver nota arriba)
        var iconConfig = AssetDatabase.LoadAssetAtPath<CardIconConfig>(IconConfigPath);
        if (iconConfig == null) Debug.LogWarning($"LibraryGodsSceneBuilder: no se encontró {IconConfigPath}");

        // ── Cámara UI (fondo) ────────────────────────────────────────────
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Bg;

        // ── Canvas + EventSystem ─────────────────────────────────────────
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler),
                                      typeof(GraphicRaycaster), typeof(ResponsiveCanvasMatch));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGO.transform;

        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Fondo obsidiana.
        var bg = Region(root, "BG", Vector2.zero, Vector2.one);
        AddImg(bg, Bg);

        var ctrlGO = new GameObject("LibraryGodsController", typeof(LibraryGodsController));
        var ctrl = ctrlGO.GetComponent<LibraryGodsController>();

        // ═══════════════════════ HEADER ═══════════════════════
        var header = Region(root, "Header", new Vector2(0f, 0.915f), new Vector2(1f, 1f));
        AddImg(header, Panel);
        Accent(header, bottom: true);

        var title = Label(header, "Title", "LIBRARY OF THE GODS", 34, Gold, TextAlignmentOptions.Left,
                          FontStyles.Bold);
        Place(title, new Vector2(0f, 0f), new Vector2(0.30f, 1f), new Vector2(24, 0), new Vector2(0, -6));
        title.characterSpacing = 6f;
        var subtitle = Label(header, "Subtitle", "KEMET ARCHIVE SYSTEM", 13, Muted, TextAlignmentOptions.Left);
        Place(subtitle, new Vector2(0f, 0f), new Vector2(0.30f, 1f), new Vector2(26, -34), new Vector2(0, -6));
        subtitle.characterSpacing = 8f;

        // Stats (4 bloques).
        var totalT = StatBlock(header, "TOTAL", 0.31f, 0.40f, Bright);
        var discT  = StatBlock(header, "DESCUBIERTAS", 0.40f, 0.51f, Cyan);
        var ownT   = StatBlock(header, "POSEÍDAS", 0.51f, 0.60f, Gold);
        var compT  = StatBlock(header, "COMPLETITUD", 0.60f, 0.70f, Violet);

        // Anillo de completitud (Image radial).
        var ring = Region(header, "CompletionRing", new Vector2(0.70f, 0.5f), new Vector2(0.70f, 0.5f));
        ring.sizeDelta = new Vector2(46, 46);
        ring.anchoredPosition = new Vector2(30, 0);
        var ringImg = AddImg(ring, Gold);
        ringImg.sprite = BuiltinKnob();
        ringImg.type = Image.Type.Filled;
        ringImg.fillMethod = Image.FillMethod.Radial360;
        ringImg.fillAmount = 0.55f;

        // Buscador.
        var search = MakeInputField(header, "Search", "Buscar carta...", out var searchField);
        Place(search, new Vector2(0.75f, 0.2f), new Vector2(0.965f, 0.8f), Vector2.zero, Vector2.zero);

        // ═══════════════════════ TABS (solo sobre la grilla) ═══════════════════════
        // Las pestañas ocupan SOLO la columna central (alineadas con la grilla); así el
        // sidebar y el panel de detalle pueden subir hasta justo debajo del header.
        var tabsBar = Region(root, "Tabs", new Vector2(0.165f, 0.85f), new Vector2(0.735f, 0.915f));
        tabsBar.offsetMin = new Vector2(8, 2);
        tabsBar.offsetMax = new Vector2(-8, -2);
        string[] tabNames = { "TODAS", "MONSTRUOS", "MAGIAS", "TRAMPAS", "DIVINAS", "FAVORITAS" };
        var tabButtons = new Button[tabNames.Length];
        float tabSlot = 1f / tabNames.Length;
        for (int i = 0; i < tabNames.Length; i++)
        {
            var b = TabButton(tabsBar, tabNames[i], i * tabSlot + 0.006f, (i + 1) * tabSlot - 0.006f,
                              i == 0 ? Violet : PanelSoft);
            tabButtons[i] = b;
        }
        // (La etiqueta RARIDAD se movió a la barra superior del panel de detalle.)

        // ═══════════════════════ SIDEBAR ═══════════════════════
        var side = Region(root, "Sidebar", new Vector2(0f, 0f), new Vector2(0.165f, 0.915f));
        side.offsetMin = new Vector2(24, 24);   // margen del borde de pantalla
        side.offsetMax = new Vector2(-8, -6);   // sube hasta justo debajo del header
        AddImg(side, Panel);
        Accent(side, right: true);

        // Emblema (placeholder).
        var emblem = Region(side, "Emblem", new Vector2(0.12f, 0.80f), new Vector2(0.88f, 0.97f));
        var embImg = AddImg(emblem, PanelSoft);
        embImg.sprite = BuiltinKnob();
        embImg.color = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
        Outline(emblem, Gold);
        var embTxt = Label(emblem, "EmblemMark", "OJO DE\nHORUS", 15, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(embTxt);

        // POR RARIDAD.
        SectionHeader(side, "POR RARIDAD", 0.76f);
        string[] rarNames = { "LEGENDARIA", "ÉPICA", "RARA", "COMÚN" };
        Color[] rarColors = { Gold, Violet, Cyan, Muted };
        var rarityCounts = new TMP_Text[rarNames.Length];
        float ry = 0.71f;
        for (int i = 0; i < rarNames.Length; i++)
        {
            rarityCounts[i] = ListRow(side, rarNames[i], "0", ry, rarColors[i]);
            ry -= 0.05f;
        }

        // COLECCIONES (filas dinámicas, generadas por el controlador → scrollable).
        // El sidebar ya no tiene botón, así que la lista baja hasta el borde inferior.
        SectionHeader(side, "COLECCIONES", 0.49f);
        var colScrollArea = Region(side, "ColScrollArea", new Vector2(0.05f, 0.04f), new Vector2(0.97f, 0.47f));
        var collectionsContent = MakeScrollList(colScrollArea);

        // ═══════════════════════ GRID (ScrollRect) ═══════════════════════
        var gridArea = Region(root, "GridArea", new Vector2(0.165f, 0.0f), new Vector2(0.735f, 0.85f));
        gridArea.offsetMin = new Vector2(8, 24);
        gridArea.offsetMax = new Vector2(-8, -6);
        var gridContent = MakeScrollGrid(gridArea);

        // ═══════════════════════ PANEL DERECHO ═══════════════════════
        var right = Region(root, "DetailPanel", new Vector2(0.735f, 0.0f), new Vector2(1f, 0.915f));
        right.offsetMin = new Vector2(8, 24);
        right.offsetMax = new Vector2(-24, -6);   // sube hasta justo debajo del header
        AddImg(right, Panel);
        Accent(right, left: true);

        // Preview de carta completa, GRANDE: se mantiene a tamaño nativo (200×280,
        // su layout interno no admite estirado) y RectScaleFitter la escala para
        // encajar dentro del host, llenando el ancho del panel como en el mockup.
        CardDisplay previewCard = null;
        if (cardPrefab != null)
        {
            var previewHost = Region(right, "PreviewHost", new Vector2(0.06f, 0.43f), new Vector2(0.94f, 0.98f));
            var card = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, previewHost);
            var cardRT = (RectTransform)card.transform;
            cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(200, 280);
            cardRT.anchoredPosition3D = Vector3.zero;
            var fitter = card.AddComponent<RectScaleFitter>();
            fitter.source = previewHost;
            fitter.nativeHeight = 280f;
            fitter.nativeWidth = 200f; // encaja DENTRO del host (limita por ambos ejes)
            previewCard = card.GetComponent<CardDisplay>();

            // Inclinación 3D (como el mockup): el host es el área de golpe estacionaria
            // y la carta (target) rota. Descansa ya inclinada y sigue el puntero al
            // pasar por encima, manteniendo vivos los reflejos holo.
            AddImg(previewHost, new Color(0, 0, 0, 0.002f)).raycastTarget = true; // capta el puntero en todo el host
            var inspect = previewHost.gameObject.AddComponent<InspectableCard>();
            inspect.target = cardRT;                      // SOLO la carta rota (no el panel)
            inspect.restEuler = new Vector2(-3f, -6f);    // inclinación 3D sutil en reposo
            inspect.maxAngle = 16f;                       // giro más marcado al pasar el ratón
            inspect.easeSpeed = 14f;                      // seguimiento algo más ágil
            inspect.idleSwayAmp = 1.5f;
        }

        // Info (sin descripción; la carta ocupa la parte superior a lo grande).
        var detName = Label(right, "DetName", "—", 30, Bright, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(detName, new Vector2(0.07f, 0.36f), new Vector2(0.95f, 0.415f), Vector2.zero, Vector2.zero);

        var detAtk = KV(right, "ATK", 0.07f, 0.295f, Gold, out var atkV);
        var detDef = KV(right, "DEF", 0.52f, 0.295f, Cyan, out var defV);
        var typeV  = InfoRowIcon(right, "TIPO", 0.245f, out var typeIcon);
        var attrV  = InfoRowIcon(right, "ATRIBUTO", 0.205f, out var attrIcon);
        var lvlRow = InfoRowLevel(right, 0.165f);
        var rarV   = InfoRow(right, "RARIDAD", 0.125f);
        var srcV   = InfoRow(right, "OBTENIDA", 0.085f);
        var rateV  = InfoRow(right, "TASA", 0.045f);

        // Botón VER MODELO 3D (movido aquí desde el sidebar; sustituye al de "carta completa").
        var btn3D = MakeButton(right, "Btn3D", "VER MODELO 3D", Violet, Bright);
        Place(btn3D, new Vector2(0.07f, 0.008f), new Vector2(0.93f, 0.05f), Vector2.zero, Vector2.zero);

        // ═══════════════════════ VISOR 3D (mínimo) ═══════════════════════
        var viewer = BuildModel3DViewer(root, out var model3DViewer);

        // ═══════════════════════ CABLEADO DEL CONTROLADOR ═══════════════════════
        Set(ctrl, "gridContent", gridContent);
        Set(ctrl, "cardSlotPrefab", slotPrefab);
        Set(ctrl, "totalText", totalT);
        Set(ctrl, "discoveredText", discT);
        Set(ctrl, "ownedText", ownT);
        Set(ctrl, "completionText", compT);
        Set(ctrl, "completionRing", ringImg);
        Set(ctrl, "searchInput", searchField);
        Set(ctrl, "tabButtons", tabButtons);
        Set(ctrl, "rarityCountTexts", rarityCounts);
        Set(ctrl, "collectionsContainer", collectionsContent);
        Set(ctrl, "view3DButton", btn3D);
        Set(ctrl, "model3DViewer", model3DViewer);
        Set(ctrl, "previewCard", previewCard);
        Set(ctrl, "detNameText", detName);
        Set(ctrl, "detAtkText", atkV);
        Set(ctrl, "detDefText", defV);
        Set(ctrl, "detTypeText", typeV);
        Set(ctrl, "detAttrText", attrV);
        Set(ctrl, "detRarityText", rarV);
        Set(ctrl, "detSourceText", srcV);
        Set(ctrl, "detRateText", rateV);
        Set(ctrl, "iconConfig", iconConfig);
        Set(ctrl, "detTypeIcon", typeIcon);
        Set(ctrl, "detAttrIcon", attrIcon);
        Set(ctrl, "levelIconsRow", lvlRow);
        Set(ctrl, "levelIconSprite", BuiltinKnob()); // placeholder: cámbialo por tu icono de nivel

        EditorUtility.SetDirty(ctrl);

        // ── Guardar ──────────────────────────────────────────────────────
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log($"LibraryGodsSceneBuilder: escena creada en {ScenePath}. Ábrela y dale Play.");
        EditorUtility.DisplayDialog("Library of the Gods",
            "Escena nueva creada en:\n" + ScenePath +
            "\n\nEstilo plano con la paleta (placeholders para tu arte).\nBotón 'Ver modelo 3D' en el sidebar.", "OK");
    }

    // ════════════════════════ Bloques de UI ════════════════════════

    static TMP_Text StatBlock(RectTransform parent, string label, float xMin, float xMax, Color valColor)
    {
        var box = Region(parent, "Stat_" + label, new Vector2(xMin, 0.12f), new Vector2(xMax, 0.88f));
        var lbl = Label(box, "L", label, 12, Muted, TextAlignmentOptions.Center);
        Place(lbl, new Vector2(0, 0.55f), new Vector2(1, 1f), Vector2.zero, Vector2.zero);
        lbl.characterSpacing = 3f;
        var val = Label(box, "V", "0", 26, valColor, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(val, new Vector2(0, 0f), new Vector2(1, 0.6f), Vector2.zero, Vector2.zero);
        return val;
    }

    static RectTransform SectionHeader(RectTransform parent, string text, float y)
    {
        var box = Region(parent, "Sec_" + text, new Vector2(0.08f, y), new Vector2(0.92f, y + 0.035f));
        var lbl = Label(box, "L", text, 14, Gold, TextAlignmentOptions.Left, FontStyles.UpperCase);
        Stretch(lbl);
        lbl.characterSpacing = 4f;
        return box;
    }

    static TMP_Text ListRow(RectTransform parent, string name, string value, float y, Color dot)
    {
        var box = Region(parent, "Row_" + name, new Vector2(0.08f, y), new Vector2(0.92f, y + 0.045f));
        // Gema de rareza (rombo) como icono antes del texto.
        var d = Region(box, "Gem", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        d.sizeDelta = new Vector2(15, 15); d.anchoredPosition = new Vector2(12, 0);
        d.localRotation = Quaternion.Euler(0, 0, 45f);
        AddImg(d, dot).raycastTarget = false;
        var nm = Label(box, "N", name, 14, Bright, TextAlignmentOptions.Left);
        Place(nm, new Vector2(0f, 0f), new Vector2(0.72f, 1f), new Vector2(24, 0), Vector2.zero);
        var v = Label(box, "V", value, 15, dot, TextAlignmentOptions.Right, FontStyles.Bold);
        Place(v, new Vector2(0.6f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        return v;
    }

    static RectTransform KV(RectTransform parent, string label, float xMin, float y, Color c, out TMP_Text value)
    {
        var box = Region(parent, "KV_" + label, new Vector2(xMin, y), new Vector2(xMin + 0.41f, y + 0.055f));
        AddImg(box, PanelSoft);
        var lbl = Label(box, "L", label, 11, Muted, TextAlignmentOptions.Center);
        Place(lbl, new Vector2(0, 0.5f), new Vector2(1, 1f), Vector2.zero, Vector2.zero);
        value = Label(box, "V", "0", 24, c, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(value, new Vector2(0, 0f), new Vector2(1, 0.55f), Vector2.zero, Vector2.zero);
        return box;
    }

    static TMP_Text InfoRow(RectTransform parent, string label, float y)
    {
        var box = Region(parent, "Info_" + label, new Vector2(0.07f, y), new Vector2(0.95f, y + 0.04f));
        var lbl = Label(box, "L", label, 13, Muted, TextAlignmentOptions.Left, FontStyles.UpperCase);
        Place(lbl, new Vector2(0f, 0f), new Vector2(0.42f, 1f), Vector2.zero, Vector2.zero);
        lbl.characterSpacing = 2f;
        var v = Label(box, "V", "—", 16, Bright, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(v, new Vector2(0.42f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        return v;
    }

    /// <summary>Fila de info con ICONO entre etiqueta y valor (para Tipo/Atributo).
    /// El icono arranca oculto; el controlador lo enciende con el sprite real.</summary>
    static TMP_Text InfoRowIcon(RectTransform parent, string label, float y, out Image icon)
    {
        var box = Region(parent, "Info_" + label, new Vector2(0.07f, y), new Vector2(0.95f, y + 0.04f));
        var lbl = Label(box, "L", label, 13, Muted, TextAlignmentOptions.Left, FontStyles.UpperCase);
        Place(lbl, new Vector2(0f, 0f), new Vector2(0.42f, 1f), Vector2.zero, Vector2.zero);
        lbl.characterSpacing = 2f;

        var ico = Region(box, "Icon", new Vector2(0.42f, 0.5f), new Vector2(0.42f, 0.5f));
        ico.sizeDelta = new Vector2(26, 26);
        ico.pivot = new Vector2(0f, 0.5f);
        ico.anchoredPosition = Vector2.zero;
        icon = AddImg(ico, Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;

        var v = Label(box, "V", "—", 16, Bright, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(v, new Vector2(0.52f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        return v;
    }

    /// <summary>Fila "NIVEL": etiqueta + contenedor donde el controlador instancia
    /// el icono de nivel repetido N veces (nivel 8 ⇒ 8 iconos).</summary>
    static RectTransform InfoRowLevel(RectTransform parent, float y)
    {
        var box = Region(parent, "Info_NIVEL", new Vector2(0.07f, y), new Vector2(0.95f, y + 0.04f));
        var lbl = Label(box, "L", "NIVEL", 13, Muted, TextAlignmentOptions.Left, FontStyles.UpperCase);
        Place(lbl, new Vector2(0f, 0f), new Vector2(0.42f, 1f), Vector2.zero, Vector2.zero);
        lbl.characterSpacing = 2f;

        var row = Region(box, "Pips", new Vector2(0.42f, 0f), new Vector2(1f, 1f));
        return row;
    }

    static RectTransform MakeScrollGrid(RectTransform area)
    {
        var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        var scrollRT = (RectTransform)scrollGO.transform;
        scrollRT.SetParent(area, false);
        Stretch(scrollRT);
        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.scrollSensitivity = 26f; scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = Region(scrollRT, "Viewport", Vector2.zero, Vector2.one);
        var vpImg = AddImg(viewport, new Color(0, 0, 0, 0.01f));
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = Region(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        // Celda al tamaño NATIVO del Card.prefab (200×280): su layout interno usa
        // anclas de píxeles fijos y a otro tamaño se descuadra (lección aprendida
        // en el modal). Con estos números caben 5 columnas en el área central.
        grid.cellSize = new Vector2(200, 280);
        grid.spacing = new Vector2(8, 14);
        grid.padding = new RectOffset(10, 10, 14, 14);
        grid.childAlignment = TextAnchor.UpperCenter;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        return content;
    }

    // Scroll VERTICAL con VerticalLayoutGroup (para listas que crecen, ej. colecciones).
    static RectTransform MakeScrollList(RectTransform area)
    {
        var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        var scrollRT = (RectTransform)scrollGO.transform;
        scrollRT.SetParent(area, false);
        Stretch(scrollRT);
        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.scrollSensitivity = 20f; scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = Region(scrollRT, "Viewport", Vector2.zero, Vector2.one);
        AddImg(viewport, new Color(0, 0, 0, 0.01f));
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = Region(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 3f;
        vlg.padding = new RectOffset(2, 2, 2, 2);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        return content;
    }

    // ════════════════════════ Model3DViewer mínimo ════════════════════════

    static RectTransform BuildModel3DViewer(RectTransform root, out Model3DViewer viewer)
    {
        var modalGO = new GameObject("Model3DViewer", typeof(Model3DViewer));
        viewer = modalGO.GetComponent<Model3DViewer>();

        var modalRoot = Region(root, "Viewer_Root", Vector2.zero, Vector2.one);
        var group = modalRoot.gameObject.AddComponent<CanvasGroup>();

        var backdrop = Region(modalRoot, "Viewer_Backdrop", Vector2.zero, Vector2.one);
        var backdropBtn = backdrop.gameObject.AddComponent<Button>();
        AddImg(backdrop, new Color(0f, 0f, 0f, 0.9f));

        // Render 3D: cámara lejana + RenderTexture + RawImage.
        var pivotGO = new GameObject("Viewer_Pivot");
        pivotGO.transform.position = new Vector3(1000, 1000, 1000);

        var vcamGO = new GameObject("Viewer_Camera", typeof(Camera));
        var vcam = vcamGO.GetComponent<Camera>();
        vcam.transform.position = new Vector3(1000, 1000, 996);
        vcam.transform.LookAt(pivotGO.transform.position);
        vcam.clearFlags = CameraClearFlags.SolidColor;
        vcam.backgroundColor = new Color(0.03f, 0.03f, 0.05f, 0f);
        vcam.fieldOfView = 35f;
        vcam.nearClipPlane = 0.1f; vcam.farClipPlane = 50f;

        // El RenderTexture debe ser un ASSET para que la escena guardada conserve
        // la referencia (un RT creado en runtime no se serializa en la escena).
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        var rt = new RenderTexture(900, 900, 16) { name = "LibraryGods_ViewerRT" };
        const string rtPath = "Assets/Scenes/LibraryGods_ViewerRT.renderTexture";
        AssetDatabase.CreateAsset(rt, rtPath);
        vcam.targetTexture = rt;

        var lightGO = new GameObject("Viewer_Light", typeof(Light));
        lightGO.transform.position = new Vector3(1002, 1003, 994);
        var light = lightGO.GetComponent<Light>();
        light.type = LightType.Point; light.range = 30f; light.intensity = 1.6f;
        light.color = new Color(1f, 0.96f, 0.86f);

        var display = Region(modalRoot, "Viewer_Display", new Vector2(0.3f, 0.12f), new Vector2(0.7f, 0.9f));
        var raw = display.gameObject.AddComponent<RawImage>();
        raw.texture = rt;

        var name = Label(modalRoot, "Viewer_Name", "", 26, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(name, new Vector2(0.3f, 0.05f), new Vector2(0.7f, 0.11f), Vector2.zero, Vector2.zero);

        var closeBtn = MakeButton(modalRoot, "Viewer_Close", "CERRAR", Violet, Bright);
        Place(closeBtn, new Vector2(0.44f, 0.02f), new Vector2(0.56f, 0.05f), Vector2.zero, Vector2.zero);

        Set(viewer, "root", modalRoot.gameObject);
        Set(viewer, "rootGroup", group);
        Set(viewer, "modelCamera", vcam);
        Set(viewer, "displayImage", raw);
        Set(viewer, "pivotPoint", pivotGO.transform);
        Set(viewer, "monsterNameText", name);
        Set(viewer, "closeButton", closeBtn);
        Set(viewer, "backdropButton", backdropBtn);
        EditorUtility.SetDirty(viewer);

        return modalRoot;
    }

    // ════════════════════════ Helpers de UI ════════════════════════

    static RectTransform Region(Transform parent, string name, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        return rt;
    }

    static void Stretch(Component c)
    {
        var rt = (RectTransform)c.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void Place(Component c, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        var rt = (RectTransform)c.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    static Image AddImg(RectTransform rt, Color c)
    {
        var img = rt.gameObject.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
        img.color = c;
        return img;
    }

    static void Accent(RectTransform panel, bool top = false, bool bottom = false,
                       bool left = false, bool right = false)
    {
        var a = new GameObject("Accent", typeof(RectTransform));
        var rt = (RectTransform)a.transform;
        rt.SetParent(panel, false);
        if (bottom) { rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.sizeDelta = new Vector2(0, 2); rt.pivot = new Vector2(0.5f, 0f); }
        else if (top) { rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.sizeDelta = new Vector2(0, 2); rt.pivot = new Vector2(0.5f, 1f); }
        else if (right) { rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1); rt.sizeDelta = new Vector2(2, 0); rt.pivot = new Vector2(1f, 0.5f); }
        else if (left) { rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.sizeDelta = new Vector2(2, 0); rt.pivot = new Vector2(0f, 0.5f); }
        rt.anchoredPosition = Vector2.zero;
        AddImg(rt, Line).raycastTarget = false;
    }

    static void Outline(RectTransform rt, Color c)
    {
        var o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(c.r, c.g, c.b, 0.5f);
        o.effectDistance = new Vector2(1.5f, -1.5f);
    }

    static TMP_Text Label(Transform parent, string name, string text, float size, Color color,
                          TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        Stretch(t);
        return t;
    }

    static Button TabButton(RectTransform parent, string text, float xMin, float xMax, Color fill)
    {
        var b = MakeButton(parent, "Tab_" + text, text, fill, Bright);
        Place(b, new Vector2(xMin, 0.15f), new Vector2(xMax, 0.85f), Vector2.zero, Vector2.zero);
        return b;
    }

    static Button MakeButton(Transform parent, string name, string text, Color fill, Color textColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = fill;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var lbl = Label(rt, "Label", text, 15, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(lbl);
        lbl.characterSpacing = 2f;
        return btn;
    }

    static RectTransform MakeInputField(RectTransform parent, string name, string placeholder,
                                        out TMP_InputField field)
    {
        var root = Region(parent, name, Vector2.zero, Vector2.one);
        var bg = AddImg(root, PanelSoft);
        Outline(root, Line);
        field = root.gameObject.AddComponent<TMP_InputField>();

        var area = Region(root, "TextArea", Vector2.zero, Vector2.one);
        area.offsetMin = new Vector2(14, 6); area.offsetMax = new Vector2(-14, -6);
        area.gameObject.AddComponent<RectMask2D>();

        var ph = Label(area, "Placeholder", placeholder, 20, Muted, TextAlignmentOptions.Left,
                       FontStyles.Italic);
        var txt = Label(area, "Text", "", 20, Bright, TextAlignmentOptions.Left);

        field.textViewport = area;
        field.textComponent = txt;
        field.placeholder = ph;
        field.targetGraphic = bg;
        field.fontAsset = txt.font;
        return root;
    }

    // Sprite built-in redondo de Unity (para anillo/emblema placeholder).
    static Sprite BuiltinKnob()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    }

    // Asigna un campo privado [SerializeField] por reflexión (para el generador).
    static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) { Debug.LogWarning($"LibraryGodsSceneBuilder: campo '{field}' no encontrado en {target.GetType().Name}"); return; }
        f.SetValue(target, value);
    }
}
