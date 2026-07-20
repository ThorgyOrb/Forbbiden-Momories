using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Construye la escena del Constructor de Deck con el rediseño "Neo-Kemet"
/// (réplica del mockup): barra superior con pestañas Mis Decks / Constructor y
/// acciones Guardar/Nuevo/Exportar; tres columnas (Colección con filtros y grilla
/// paginada · Inspector central con carta grande, stepper y pestañas inferiores
/// Estadísticas/Curva/Mano/Fusiones · Mazo con selector de mazos, lista, dona de
/// distribución y promedios); y un panel de Ajustes. Cablea todas las referencias
/// del <see cref="DeckBuilderController"/> y de las plantillas.
///
/// Se regenera desde  YGO ▸ Setup ▸ (Re)construir Constructor de Deck.
/// </summary>
public static class DeckBuilderBuilder
{
    const string CardPrefabPath = "Assets/Resources/Prefabs/CardMonsterV2.prefab";
    const string StarSpritePath = "Assets/Sprites/level_star.png";

    // ── Paleta Neo-Kemet ─────────────────────────────────────────────────
    static readonly Color Bg        = new Color(0.043f, 0.045f, 0.075f);
    static readonly Color Panel     = new Color(0.075f, 0.075f, 0.11f, 0.96f);
    static readonly Color PanelSoft = new Color(0.11f, 0.10f, 0.15f, 0.90f);
    static readonly Color Field     = new Color(0.05f, 0.055f, 0.09f, 1f);
    static readonly Color Gold      = new Color(0.93f, 0.75f, 0.33f);
    static readonly Color GoldBright = new Color(0.99f, 0.86f, 0.48f);
    static readonly Color Cyan      = new Color(0.22f, 0.95f, 0.86f);
    static readonly Color Violet    = new Color(0.63f, 0.40f, 0.95f);
    static readonly Color Emerald   = new Color(0.16f, 0.85f, 0.66f);
    static readonly Color Fuchsia   = new Color(1.00f, 0.31f, 0.64f);
    static readonly Color Bright    = new Color(0.93f, 0.90f, 0.83f);
    static readonly Color Muted     = new Color(0.58f, 0.55f, 0.50f);
    static readonly Color Line      = new Color(0.93f, 0.75f, 0.33f, 0.30f);
    static readonly Color BtnIdle   = new Color(0.13f, 0.13f, 0.20f, 0.95f);

    static GameObject _cardPrefab;
    static Sprite _starSprite;

    public static void BuildInScene(DeckBuilderController controller)
    {
        Scene scene = controller.gameObject.scene;

        var previous = FindRootInScene(scene, "DeckBuilderCanvas");
        if (previous != null) Object.DestroyImmediate(previous);

        EnsureEventSystem(scene);
        _cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (_cardPrefab == null) Debug.LogError($"DeckBuilderBuilder: falta {CardPrefabPath}");
        _starSprite = AssetDatabase.LoadAssetAtPath<Sprite>(StarSpritePath);

        // ── Canvas ───────────────────────────────────────────────────────
        var canvasGO = new GameObject("DeckBuilderCanvas", typeof(Canvas), typeof(CanvasScaler),
                                      typeof(GraphicRaycaster), typeof(ResponsiveCanvasScaler));
        MoveToScene(canvasGO, scene);
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGO.transform;

        AddImg(Region(root, "Background", Vector2.zero, Vector2.one), Bg);

        var refs = new Refs();

        BuildHeader(root, refs);

        // Vista Constructor (3 columnas) y vista Mis Decks (gestor).
        var constructor = Region(root, "ConstructorView", Vector2.zero, new Vector2(1f, 0.928f));
        refs.constructorView = constructor.gameObject;
        BuildColumns(constructor, refs);

        var misDecks = Region(root, "MisDecksView", Vector2.zero, new Vector2(1f, 0.928f));
        refs.misDecksView = misDecks.gameObject;
        BuildMisDecks(misDecks, refs);

        BuildSettings(root, refs);

        WireController(controller, refs);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("DeckBuilderBuilder: escena del Constructor de Deck (Neo-Kemet) construida y cableada.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Barra superior
    // ═══════════════════════════════════════════════════════════════════════
    static void BuildHeader(RectTransform root, Refs r)
    {
        var header = Region(root, "Header", new Vector2(0f, 0.928f), Vector2.one);
        AddImg(header, Panel);
        Accent(header, bottom: true);

        r.backButton = MakeButton(header, "Btn_Back", "←", BtnIdle, GoldBright, 30);
        Place(r.backButton, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        SizePivot(r.backButton, new Vector2(0f, 0.5f), new Vector2(18, 0), new Vector2(56, 56));

        var title = Label(header, "Title", "CONSTRUCTOR DE DECK", 30, GoldBright, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title, new Vector2(0f, 0f), new Vector2(0.30f, 1f), new Vector2(86, 0), Vector2.zero);
        title.characterSpacing = 4f;

        // Pestañas Mis Decks / Constructor (centro).
        r.tabMisDecksButton    = MakeButton(header, "Tab_MisDecks", "MIS DECKS", BtnIdle, Bright, 18);
        Place(r.tabMisDecksButton, new Vector2(0.36f, 0.2f), new Vector2(0.47f, 0.82f), Vector2.zero, Vector2.zero);
        r.tabConstructorButton = MakeButton(header, "Tab_Constructor", "CONSTRUCTOR", Violet, Bright, 18);
        Place(r.tabConstructorButton, new Vector2(0.475f, 0.2f), new Vector2(0.60f, 0.82f), Vector2.zero, Vector2.zero);

        // Acciones (derecha).
        r.saveButton   = MakeButton(header, "Btn_Guardar", "GUARDAR", new Color(0.15f, 0.35f, 0.28f, 0.95f), Bright, 18);
        Place(r.saveButton, new Vector2(0.66f, 0.2f), new Vector2(0.76f, 0.82f), Vector2.zero, Vector2.zero);
        r.newButton    = MakeButton(header, "Btn_Nuevo", "NUEVO", BtnIdle, Bright, 18);
        Place(r.newButton, new Vector2(0.765f, 0.2f), new Vector2(0.85f, 0.82f), Vector2.zero, Vector2.zero);
        r.exportButton = MakeButton(header, "Btn_Exportar", "EXPORTAR", BtnIdle, Bright, 18);
        Place(r.exportButton, new Vector2(0.855f, 0.2f), new Vector2(0.95f, 0.82f), Vector2.zero, Vector2.zero);

        r.settingsButton = MakeButton(header, "Btn_Ajustes", "⚙", BtnIdle, GoldBright, 24);
        Place(r.settingsButton, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        SizePivot(r.settingsButton, new Vector2(1f, 0.5f), new Vector2(-18, 0), new Vector2(56, 56));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Tres columnas
    // ═══════════════════════════════════════════════════════════════════════
    static void BuildColumns(RectTransform parent, Refs r)
    {
        BuildCollectionColumn(parent, r);
        BuildCenterColumn(parent, r);
        BuildDeckColumn(parent, r);

        r.statusText = Label(parent, "Status", "", 17, Muted, TextAlignmentOptions.Left);
        Place(r.statusText, new Vector2(0f, 0.005f), new Vector2(0.335f, 0.05f), new Vector2(24, 0), Vector2.zero);
        r.statusText.enableWordWrapping = false;
        r.statusText.overflowMode = TextOverflowModes.Ellipsis;
    }

    // ── Columna COLECCIÓN ────────────────────────────────────────────────
    static void BuildCollectionColumn(RectTransform parent, Refs r)
    {
        var panel = Region(parent, "CollectionPanel", new Vector2(0f, 0.055f), new Vector2(0.335f, 1f));
        panel.offsetMin = new Vector2(20, 0); panel.offsetMax = new Vector2(-8, -8);
        AddImg(panel, Panel);
        Accent(panel, right: true);

        var title = Label(panel, "T", "COLECCIÓN", 22, GoldBright, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title, new Vector2(0f, 0.955f), new Vector2(0.6f, 1f), new Vector2(18, -6), Vector2.zero);
        title.characterSpacing = 3f;
        r.collectionCountText = Label(panel, "Count", "0 cartas", 16, Muted, TextAlignmentOptions.Right);
        Place(r.collectionCountText, new Vector2(0.5f, 0.955f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-18, -8));

        var search = MakeInputField(panel, "Search", "Buscar carta...", out r.searchInput);
        Place(search, new Vector2(0.03f, 0.905f), new Vector2(0.97f, 0.95f), Vector2.zero, Vector2.zero);

        // Filtros (2 filas × 3).
        r.typeDropdown      = MakeDropdown(panel, "Dd_Tipo");     Place(r.typeDropdown, F(0.03f, 0.855f, 0.345f, 0.898f));
        r.attributeDropdown = MakeDropdown(panel, "Dd_Atributo"); Place(r.attributeDropdown, F(0.355f, 0.855f, 0.665f, 0.898f));
        r.levelDropdown     = MakeDropdown(panel, "Dd_Nivel");    Place(r.levelDropdown, F(0.675f, 0.855f, 0.97f, 0.898f));
        r.rarityDropdown    = MakeDropdown(panel, "Dd_Rareza");   Place(r.rarityDropdown, F(0.03f, 0.805f, 0.345f, 0.848f));
        r.sortDropdown      = MakeDropdown(panel, "Dd_Orden");    Place(r.sortDropdown, F(0.355f, 0.805f, 0.665f, 0.848f));
        r.clearFiltersButton = MakeButton(panel, "Btn_Limpiar", "LIMPIAR", BtnIdle, Bright, 15);
        Place(r.clearFiltersButton, F(0.675f, 0.805f, 0.97f, 0.848f));

        // Pestañas de categoría.
        string[] catNames = { "Todos", "Monstruos", "Magias", "Trampas", "Equipos" };
        var catButtons = new Button[5];
        float slot = 0.94f / 5f;
        for (int i = 0; i < 5; i++)
        {
            catButtons[i] = MakeButton(panel, "Cat_" + catNames[i], catNames[i], BtnIdle, Bright, 14);
            Place(catButtons[i], F(0.03f + i * slot, 0.755f, 0.03f + (i + 1) * slot - 0.006f, 0.798f));
            FitLabel(catButtons[i], 11, 15);
        }
        r.catAllButton = catButtons[0]; r.catMonsterButton = catButtons[1]; r.catSpellButton = catButtons[2];
        r.catTrapButton = catButtons[3]; r.catEquipButton = catButtons[4];

        // Grilla paginada.
        var gridArea = Region(panel, "GridArea", new Vector2(0.02f, 0.075f), new Vector2(0.98f, 0.745f));
        r.collectionGridContent = MakeGrid(gridArea, 3, new Vector2(176, 246), new Vector2(12, 14));

        // Barra de páginas.
        var pageBar = Region(panel, "PageBar", new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.07f));
        r.prevPageButton = MakeButton(pageBar, "Btn_Prev", "‹", BtnIdle, Bright, 22);
        Place(r.prevPageButton, new Vector2(0f, 0.1f), new Vector2(0.1f, 0.9f), Vector2.zero, Vector2.zero);
        r.nextPageButton = MakeButton(pageBar, "Btn_Next", "›", BtnIdle, Bright, 22);
        Place(r.nextPageButton, new Vector2(0.9f, 0.1f), new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);

        var pagesHost = Region(pageBar, "Pages", new Vector2(0.11f, 0.1f), new Vector2(0.89f, 0.9f));
        var hlg = pagesHost.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        r.pageButtonsContent = pagesHost;

        r.pageButtonTemplate = MakeButton(pagesHost, "PageTemplate", "1", BtnIdle, Bright, 15);
        var pble = r.pageButtonTemplate.GetComponent<LayoutElement>() ?? r.pageButtonTemplate.gameObject.AddComponent<LayoutElement>();
        pble.minWidth = 40; pble.preferredWidth = 40;

        // Plantilla de baldosa.
        r.collectionTemplate = BuildCollectionTile(r.collectionGridContent);
    }

    // ── Columna INSPECTOR (centro) ───────────────────────────────────────
    static void BuildCenterColumn(RectTransform parent, Refs r)
    {
        // Pestañas inferiores (bajo la columna central).
        var tabBar = Region(parent, "CenterTabs", new Vector2(0.335f, 0.005f), new Vector2(0.665f, 0.05f));
        string[] names = { "ESTADÍSTICAS", "CURVA", "MANO", "FUSIONES" };
        var tabs = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            tabs[i] = MakeButton(tabBar, "CTab_" + names[i], names[i], BtnIdle, Bright, 13);
            Place(tabs[i], new Vector2(i * 0.25f + 0.004f, 0.1f), new Vector2((i + 1) * 0.25f - 0.004f, 0.95f), Vector2.zero, Vector2.zero);
            FitLabel(tabs[i], 10, 14);
        }
        r.tabStatsButton = tabs[0]; r.tabCurveButton = tabs[1]; r.tabHandButton = tabs[2]; r.tabFusionButton = tabs[3];

        var col = Region(parent, "CenterPanel", new Vector2(0.335f, 0.055f), new Vector2(0.665f, 1f));
        col.offsetMin = new Vector2(8, 0); col.offsetMax = new Vector2(-8, -8);

        BuildInspectorPanel(col, r);
        BuildCurvePanel(col, r);
        BuildHandPanel(col, r);
        BuildFusionPanel(col, r);
    }

    static void BuildInspectorPanel(RectTransform parent, Refs r)
    {
        var panel = Region(parent, "InspectorPanel", Vector2.zero, Vector2.one);
        r.centerInspectorPanel = panel.gameObject;

        // Carta grande.
        if (_cardPrefab != null)
        {
            var host = Region(panel, "PreviewHost", new Vector2(0.14f, 0.43f), new Vector2(0.86f, 0.99f));
            var card = (GameObject)PrefabUtility.InstantiatePrefab(_cardPrefab, host);
            var cardRT = (RectTransform)card.transform;
            cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(200, 280);
            cardRT.anchoredPosition3D = Vector3.zero;
            var fitter = card.AddComponent<RectScaleFitter>();
            fitter.source = host; fitter.nativeHeight = 280f; fitter.nativeWidth = 200f;
            r.previewCard = card.GetComponent<CardDisplay>();

            AddImg(host, new Color(0, 0, 0, 0.002f)).raycastTarget = true;
            var inspect = host.gameObject.AddComponent<InspectableCard>();
            inspect.target = cardRT;
            inspect.restEuler = new Vector2(-3f, -5f);
            inspect.maxAngle = 14f; inspect.easeSpeed = 14f; inspect.idleSwayAmp = 1.2f;
        }

        // Estrella de favorito (sobre la esquina superior derecha de la carta).
        r.favoriteButton = MakeIconButton(panel, "Btn_Fav", _starSprite, out r.favoriteIcon);
        Place(r.favoriteButton, new Vector2(0.86f, 0.93f), new Vector2(0.94f, 0.99f), Vector2.zero, Vector2.zero);

        r.previewNameText = Label(panel, "Name", "Selecciona una carta", 26, GoldBright, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(r.previewNameText, new Vector2(0.03f, 0.375f), new Vector2(0.97f, 0.425f), Vector2.zero, Vector2.zero);
        r.previewCategoryText = Label(panel, "Category", "", 16, Cyan, TextAlignmentOptions.Center);
        Place(r.previewCategoryText, new Vector2(0.03f, 0.335f), new Vector2(0.97f, 0.375f), Vector2.zero, Vector2.zero);

        var descBox = Region(panel, "DescBox", new Vector2(0.04f, 0.20f), new Vector2(0.96f, 0.33f));
        AddImg(descBox, new Color(0, 0, 0, 0.28f));
        r.previewDescText = Label(descBox, "Desc", "", 16, Bright, TextAlignmentOptions.TopLeft);
        Stretch(r.previewDescText); r.previewDescText.margin = new Vector4(14, 10, 14, 10);
        r.previewDescText.enableWordWrapping = true;

        r.previewAtkText = Label(panel, "Atk", "", 20, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(r.previewAtkText, new Vector2(0.06f, 0.15f), new Vector2(0.5f, 0.195f), Vector2.zero, Vector2.zero);
        r.previewDefText = Label(panel, "Def", "", 20, Cyan, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(r.previewDefText, new Vector2(0.5f, 0.15f), new Vector2(0.94f, 0.195f), Vector2.zero, Vector2.zero);

        // "En posesión" + stepper "Copias en deck".
        var ownedL = Label(panel, "OwnedL", "En posesión", 13, Muted, TextAlignmentOptions.Center);
        Place(ownedL, new Vector2(0.06f, 0.115f), new Vector2(0.34f, 0.15f), Vector2.zero, Vector2.zero);
        r.ownedCountText = Label(panel, "Owned", "0", 24, Bright, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(r.ownedCountText, new Vector2(0.06f, 0.075f), new Vector2(0.34f, 0.115f), Vector2.zero, Vector2.zero);

        var copiesL = Label(panel, "CopiesL", "Copias en deck", 13, Muted, TextAlignmentOptions.Center);
        Place(copiesL, new Vector2(0.4f, 0.115f), new Vector2(0.94f, 0.15f), Vector2.zero, Vector2.zero);
        r.stepMinusButton = MakeButton(panel, "Btn_Minus", "−", BtnIdle, GoldBright, 26);
        Place(r.stepMinusButton, new Vector2(0.4f, 0.075f), new Vector2(0.52f, 0.115f), Vector2.zero, Vector2.zero);
        r.copiesInDeckText = Label(panel, "Copies", "0", 24, GoldBright, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(r.copiesInDeckText, new Vector2(0.52f, 0.075f), new Vector2(0.82f, 0.115f), Vector2.zero, Vector2.zero);
        r.stepPlusButton = MakeButton(panel, "Btn_Plus", "+", BtnIdle, GoldBright, 26);
        Place(r.stepPlusButton, new Vector2(0.82f, 0.075f), new Vector2(0.94f, 0.115f), Vector2.zero, Vector2.zero);

        r.addButton = MakeButton(panel, "Btn_Add", "AÑADIR AL DECK", new Color(0.28f, 0.20f, 0.45f, 0.98f), Bright, 18);
        Place(r.addButton, new Vector2(0.04f, 0.01f), new Vector2(0.49f, 0.065f), Vector2.zero, Vector2.zero);
        r.removeButton = MakeButton(panel, "Btn_Remove", "ELIMINAR DEL DECK", new Color(0.42f, 0.14f, 0.18f, 0.98f), Bright, 18);
        Place(r.removeButton, new Vector2(0.51f, 0.01f), new Vector2(0.96f, 0.065f), Vector2.zero, Vector2.zero);
    }

    static void BuildCurvePanel(RectTransform parent, Refs r)
    {
        var panel = Region(parent, "CurvePanel", Vector2.zero, Vector2.one);
        AddImg(panel, new Color(0, 0, 0, 0.15f));
        r.centerCurvePanel = panel.gameObject;
        var title = Label(panel, "T", "CURVA DE NIVELES", 22, GoldBright, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(title, new Vector2(0f, 0.9f), new Vector2(1f, 0.98f), Vector2.zero, Vector2.zero);

        var host = Region(panel, "Bars", new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.86f));
        var hlg = host.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6; hlg.childAlignment = TextAnchor.LowerCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
        r.curveContent = host;
        panel.gameObject.SetActive(false);
    }

    static void BuildHandPanel(RectTransform parent, Refs r)
    {
        var panel = Region(parent, "HandPanel", Vector2.zero, Vector2.one);
        AddImg(panel, new Color(0, 0, 0, 0.15f));
        r.centerHandPanel = panel.gameObject;
        var title = Label(panel, "T", "SIMULAR MANO INICIAL", 22, GoldBright, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(title, new Vector2(0f, 0.9f), new Vector2(1f, 0.98f), Vector2.zero, Vector2.zero);

        var host = Region(panel, "Cards", new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.88f));
        var hlg = host.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
        r.handContent = host;

        r.redrawHandButton = MakeButton(panel, "Btn_Redraw", "ROBAR DE NUEVO", Violet, Bright, 17);
        Place(r.redrawHandButton, new Vector2(0.33f, 0.03f), new Vector2(0.67f, 0.12f), Vector2.zero, Vector2.zero);
        panel.gameObject.SetActive(false);
    }

    static void BuildFusionPanel(RectTransform parent, Refs r)
    {
        var panel = Region(parent, "FusionPanel", Vector2.zero, Vector2.one);
        AddImg(panel, new Color(0, 0, 0, 0.15f));
        r.centerFusionPanel = panel.gameObject;
        var title = Label(panel, "T", "FUSIONES POSIBLES", 22, GoldBright, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(title, new Vector2(0f, 0.9f), new Vector2(1f, 0.98f), Vector2.zero, Vector2.zero);

        var area = Region(panel, "ListArea", new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.88f));
        r.fusionContent = MakeVScroll(area);
        panel.gameObject.SetActive(false);
    }

    // ── Columna MAZO (derecha) ───────────────────────────────────────────
    static void BuildDeckColumn(RectTransform parent, Refs r)
    {
        var panel = Region(parent, "DeckPanel", new Vector2(0.665f, 0.055f), new Vector2(1f, 1f));
        panel.offsetMin = new Vector2(8, 0); panel.offsetMax = new Vector2(-20, -8);
        AddImg(panel, Panel);
        Accent(panel, left: true);

        var title = Label(panel, "T", "MI DECK", 22, GoldBright, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title, new Vector2(0f, 0.955f), new Vector2(0.5f, 1f), new Vector2(18, -6), Vector2.zero);
        title.characterSpacing = 3f;
        r.deckHeaderCountText = Label(panel, "Count", "0 / 40", 24, Bright, TextAlignmentOptions.Right, FontStyles.Bold);
        Place(r.deckHeaderCountText, new Vector2(0.45f, 0.95f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-18, -6));

        // Nombre editable + borrar.
        var nameField = MakeInputField(panel, "DeckName", "Nombre del mazo", out r.deckNameInput);
        Place(nameField, new Vector2(0.03f, 0.905f), new Vector2(0.75f, 0.948f), Vector2.zero, Vector2.zero);
        r.deleteDeckButton = MakeButton(panel, "Btn_DelDeck", "BORRAR", new Color(0.4f, 0.12f, 0.16f, 0.95f), Bright, 15);
        Place(r.deleteDeckButton, new Vector2(0.76f, 0.905f), new Vector2(0.97f, 0.948f), Vector2.zero, Vector2.zero);

        // Selector de mazo.
        r.deckSlotDropdown = MakeDropdown(panel, "Dd_Slot");
        Place(r.deckSlotDropdown, new Vector2(0.03f, 0.858f), new Vector2(0.97f, 0.9f), Vector2.zero, Vector2.zero);

        // Contadores M / S / T.
        r.monsterCountText = CounterBlock(panel, "Monstruos", Gold, 0.03f, 0.345f, out _);
        r.spellCountText   = CounterBlock(panel, "Magias", Emerald, 0.355f, 0.665f, out _);
        r.trapCountText    = CounterBlock(panel, "Trampas", Fuchsia, 0.675f, 0.97f, out _);

        // Lista del mazo.
        var listArea = Region(panel, "DeckListArea", new Vector2(0.03f, 0.365f), new Vector2(0.97f, 0.79f));
        AddImg(listArea, new Color(0, 0, 0, 0.22f));
        r.deckListContent = MakeVScroll(listArea);
        r.deckRowTemplate = BuildDeckRow(r.deckListContent);

        r.extraButton = MakeButton(panel, "Btn_Extra", "VER CARTAS EXTRA", BtnIdle, Muted, 15);
        Place(r.extraButton, new Vector2(0.03f, 0.315f), new Vector2(0.97f, 0.358f), Vector2.zero, Vector2.zero);

        // DISTRIBUCIÓN (dona + leyenda).
        var distTitle = Label(panel, "DistT", "DISTRIBUCIÓN", 14, Gold, TextAlignmentOptions.Left, FontStyles.UpperCase);
        Place(distTitle, new Vector2(0.03f, 0.28f), new Vector2(0.6f, 0.31f), new Vector2(4, 0), Vector2.zero);
        distTitle.characterSpacing = 3f;

        var donutGO = new GameObject("Donut", typeof(RectTransform), typeof(CanvasRenderer), typeof(UIDonutChart));
        var donutRT = (RectTransform)donutGO.transform;
        donutRT.SetParent(panel, false);
        donutRT.anchorMin = new Vector2(0.05f, 0.15f); donutRT.anchorMax = new Vector2(0.32f, 0.275f);
        donutRT.offsetMin = Vector2.zero; donutRT.offsetMax = Vector2.zero;
        r.donut = donutGO.GetComponent<UIDonutChart>();

        r.distMonstersText = LegendRow(panel, "Monstruos", Gold, 0.255f, out _);
        r.distSpellsText   = LegendRow(panel, "Magias", Emerald, 0.215f, out _);
        r.distTrapsText    = LegendRow(panel, "Trampas", Fuchsia, 0.175f, out _);

        // Promedios (coste / ATK / DEF).
        r.avgCostText = AvgBlock(panel, "COSTE MEDIO", 0.03f, 0.345f, GoldBright, out _);
        r.avgAtkText  = AvgBlock(panel, "ATK MEDIO", 0.355f, 0.665f, Gold, out _);
        r.avgDefText  = AvgBlock(panel, "DEF MEDIO", 0.675f, 0.97f, Cyan, out _);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Mis Decks (gestor)
    // ═══════════════════════════════════════════════════════════════════════
    static void BuildMisDecks(RectTransform parent, Refs r)
    {
        var panel = Region(parent, "MisDecksPanel", new Vector2(0.12f, 0.055f), new Vector2(0.88f, 1f));
        panel.offsetMax = new Vector2(0, -8);
        AddImg(panel, Panel);
        Accent(panel, bottom: true);

        var title = Label(panel, "T", "MIS DECKS", 26, GoldBright, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title, new Vector2(0.03f, 0.93f), new Vector2(0.7f, 0.99f), Vector2.zero, Vector2.zero);
        title.characterSpacing = 4f;
        var hint = Label(panel, "H", "Elige un mazo para editarlo, o crea uno nuevo con NUEVO.", 16, Muted, TextAlignmentOptions.Left);
        Place(hint, new Vector2(0.03f, 0.9f), new Vector2(0.97f, 0.93f), Vector2.zero, Vector2.zero);

        var listArea = Region(panel, "ListArea", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.89f));
        r.misDecksListContent = MakeVScroll(listArea);

        parent.gameObject.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Ajustes
    // ═══════════════════════════════════════════════════════════════════════
    static void BuildSettings(RectTransform root, Refs r)
    {
        var overlay = Region(root, "SettingsPanel", Vector2.zero, Vector2.one);
        r.settingsPanel = overlay.gameObject;
        AddImg(overlay, new Color(0, 0, 0, 0.75f));

        var box = Region(overlay, "Box", new Vector2(0.35f, 0.34f), new Vector2(0.65f, 0.66f));
        AddImg(box, Panel);
        Accent(box, top: true);

        var title = Label(box, "T", "AJUSTES", 26, GoldBright, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(title, new Vector2(0f, 0.8f), new Vector2(1f, 0.95f), Vector2.zero, Vector2.zero);

        r.resetDeckButton = MakeButton(box, "Btn_Reset", "VACIAR MAZO ACTUAL", new Color(0.42f, 0.14f, 0.18f, 0.98f), Bright, 18);
        Place(r.resetDeckButton, new Vector2(0.12f, 0.5f), new Vector2(0.88f, 0.64f), Vector2.zero, Vector2.zero);

        var info = Label(box, "I", "Reglas: 40 cartas exactas para guardar · máx. 3 copias por carta.",
                         14, Muted, TextAlignmentOptions.Center);
        Place(info, new Vector2(0.08f, 0.3f), new Vector2(0.92f, 0.45f), Vector2.zero, Vector2.zero);
        info.enableWordWrapping = true;

        r.settingsCloseButton = MakeButton(box, "Btn_Close", "CERRAR", Violet, Bright, 18);
        Place(r.settingsCloseButton, new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.22f), Vector2.zero, Vector2.zero);

        overlay.gameObject.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Plantillas
    // ═══════════════════════════════════════════════════════════════════════
    static CollectionCardView BuildCollectionTile(Transform parent)
    {
        var tile = new GameObject("CollectionCard", typeof(RectTransform), typeof(CollectionCardView));
        var tileRT = (RectTransform)tile.transform;
        tileRT.SetParent(parent, false);
        tileRT.sizeDelta = new Vector2(176, 246);

        // Resalte de selección (detrás).
        var sel = Region(tileRT, "Selection", Vector2.zero, Vector2.one);
        sel.offsetMin = new Vector2(-6, -6); sel.offsetMax = new Vector2(6, 6);
        AddImg(sel, new Color(Gold.r, Gold.g, Gold.b, 0.55f)).raycastTarget = false;
        sel.gameObject.SetActive(false);

        CardDisplay display = null;
        if (_cardPrefab != null)
        {
            var card = (GameObject)PrefabUtility.InstantiatePrefab(_cardPrefab, tileRT);
            var cardRT = (RectTransform)card.transform;
            cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(200, 280);
            cardRT.anchoredPosition3D = Vector3.zero;
            var fitter = card.AddComponent<RectScaleFitter>();
            fitter.source = tileRT; fitter.nativeHeight = 280f; fitter.nativeWidth = 200f;
            display = card.GetComponent<CardDisplay>();
        }

        // Velo cuando la carta llegó a su tope en el mazo.
        var maxed = Region(tileRT, "Maxed", Vector2.zero, Vector2.one);
        AddImg(maxed, new Color(0.02f, 0.03f, 0.06f, 0.62f)).raycastTarget = false;
        maxed.gameObject.SetActive(false);

        // Insignia de copias poseídas (abajo-derecha).
        var badge = Region(tileRT, "Badge", new Vector2(0.62f, 0.02f), new Vector2(0.98f, 0.16f));
        AddImg(badge, new Color(0.05f, 0.05f, 0.09f, 0.92f)).raycastTarget = false;
        var copiesBadge = Label(badge, "x", "x0", 20, GoldBright, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(copiesBadge);

        // Insignia "en mazo" (arriba-izquierda).
        var inDeck = Region(tileRT, "InDeck", new Vector2(0.02f, 0.86f), new Vector2(0.7f, 0.99f));
        AddImg(inDeck, new Color(0.28f, 0.20f, 0.45f, 0.95f)).raycastTarget = false;
        var inDeckBadge = Label(inDeck, "d", "EN MAZO 0", 13, Bright, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(inDeckBadge);
        inDeck.gameObject.SetActive(false);

        // Captador de clic (encima de todo).
        var click = Region(tileRT, "Click", Vector2.zero, Vector2.one);
        var clickImg = AddImg(click, new Color(0, 0, 0, 0.001f)); clickImg.raycastTarget = true;
        var clickBtn = click.gameObject.AddComponent<Button>();
        clickBtn.transition = Selectable.Transition.None;
        clickBtn.targetGraphic = clickImg;

        var view = tile.GetComponent<CollectionCardView>();
        var so = new SerializedObject(view);
        Set(so, "cardDisplay", display);
        Set(so, "copiesBadge", copiesBadge);
        Set(so, "inDeckBadge", inDeckBadge);
        Set(so, "selectButton", clickBtn);
        Set(so, "selectionHighlight", sel.gameObject);
        Set(so, "maxedOverlay", maxed.gameObject);
        so.ApplyModifiedPropertiesWithoutUndo();

        tile.SetActive(false);
        return view;
    }

    static DeckCardView BuildDeckRow(Transform parent)
    {
        var row = new GameObject("DeckCard", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(DeckCardView));
        var rowRT = (RectTransform)row.transform;
        row.GetComponent<Image>().color = PanelSoft;
        var le = row.GetComponent<LayoutElement>(); le.minHeight = 54; le.preferredHeight = 54;

        // Acento de categoría (barra izquierda).
        var accent = Region(rowRT, "Accent", new Vector2(0f, 0f), new Vector2(0f, 1f));
        accent.sizeDelta = new Vector2(5, 0); accent.pivot = new Vector2(0f, 0.5f);
        accent.anchoredPosition = Vector2.zero;
        var accentImg = AddImg(accent, Gold); accentImg.raycastTarget = false;

        // Miniatura.
        var artRT = Region(rowRT, "Art", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        artRT.sizeDelta = new Vector2(40, 46); artRT.pivot = new Vector2(0f, 0.5f);
        artRT.anchoredPosition = new Vector2(12, 0);
        var art = AddImg(artRT, Color.white); art.preserveAspect = true; art.raycastTarget = false;

        var name = Label(rowRT, "Name", "Nombre", 18, Bright, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(name, new Vector2(0f, 0.45f), new Vector2(0.78f, 1f), new Vector2(62, 0), Vector2.zero);

        var stars = Region(rowRT, "Stars", new Vector2(0f, 0f), new Vector2(0.78f, 0.5f));
        stars.offsetMin = new Vector2(62, 4); stars.offsetMax = new Vector2(0, 0);

        var count = Label(rowRT, "Count", "x1", 20, GoldBright, TextAlignmentOptions.Right, FontStyles.Bold);
        Place(count, new Vector2(0.78f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-12, 0));

        var sel = Region(rowRT, "Selection", Vector2.zero, Vector2.one);
        AddImg(sel, new Color(Gold.r, Gold.g, Gold.b, 0.18f)).raycastTarget = false;
        sel.gameObject.SetActive(false);

        var click = Region(rowRT, "Click", Vector2.zero, Vector2.one);
        var clickImg = AddImg(click, new Color(0, 0, 0, 0.001f)); clickImg.raycastTarget = true;
        var clickBtn = click.gameObject.AddComponent<Button>();
        clickBtn.transition = Selectable.Transition.None; clickBtn.targetGraphic = clickImg;

        var view = row.GetComponent<DeckCardView>();
        var so = new SerializedObject(view);
        Set(so, "art", art);
        Set(so, "categoryAccent", accentImg);
        Set(so, "nameText", name);
        Set(so, "countText", count);
        Set(so, "starsContainer", stars);
        Set(so, "starSprite", _starSprite);
        Set(so, "selectButton", clickBtn);
        Set(so, "selectionHighlight", sel.gameObject);
        so.ApplyModifiedPropertiesWithoutUndo();

        row.SetActive(false);
        return view;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Bloques reutilizables
    // ═══════════════════════════════════════════════════════════════════════
    static TMP_Text CounterBlock(RectTransform parent, string label, Color color, float xMin, float xMax, out RectTransform box)
    {
        box = Region(parent, "Cnt_" + label, new Vector2(xMin, 0.795f), new Vector2(xMax, 0.85f));
        AddImg(box, PanelSoft);
        var lbl = Label(box, "L", label, 13, Muted, TextAlignmentOptions.Center);
        Place(lbl, new Vector2(0f, 0.5f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var val = Label(box, "V", "0", 24, color, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(val, new Vector2(0f, 0f), new Vector2(1f, 0.55f), Vector2.zero, Vector2.zero);
        return val;
    }

    static TMP_Text LegendRow(RectTransform parent, string label, Color color, float y, out RectTransform box)
    {
        box = Region(parent, "Leg_" + label, new Vector2(0.36f, y), new Vector2(0.97f, y + 0.038f));
        var dot = Region(box, "Dot", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        dot.sizeDelta = new Vector2(12, 12); dot.anchoredPosition = new Vector2(8, 0);
        AddImg(dot, color).raycastTarget = false;
        var nm = Label(box, "N", label, 15, Bright, TextAlignmentOptions.Left);
        Place(nm, new Vector2(0f, 0f), new Vector2(0.7f, 1f), new Vector2(22, 0), Vector2.zero);
        var v = Label(box, "V", "0%", 15, color, TextAlignmentOptions.Right, FontStyles.Bold);
        Place(v, new Vector2(0.6f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        return v;
    }

    static TMP_Text AvgBlock(RectTransform parent, string label, float xMin, float xMax, Color color, out RectTransform box)
    {
        box = Region(parent, "Avg_" + label, new Vector2(xMin, 0.02f), new Vector2(xMax, 0.14f));
        AddImg(box, PanelSoft);
        var lbl = Label(box, "L", label, 12, Muted, TextAlignmentOptions.Center);
        Place(lbl, new Vector2(0f, 0.55f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        lbl.characterSpacing = 1f;
        var val = Label(box, "V", "0", 24, color, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(val, new Vector2(0f, 0f), new Vector2(1f, 0.6f), Vector2.zero, Vector2.zero);
        return val;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Cableado del controlador
    // ═══════════════════════════════════════════════════════════════════════
    static void WireController(DeckBuilderController controller, Refs r)
    {
        var so = new SerializedObject(controller);
        Set(so, "backButton", r.backButton);
        Set(so, "saveButton", r.saveButton);
        Set(so, "newButton", r.newButton);
        Set(so, "exportButton", r.exportButton);
        Set(so, "settingsButton", r.settingsButton);
        Set(so, "statusText", r.statusText);
        Set(so, "tabConstructorButton", r.tabConstructorButton);
        Set(so, "tabMisDecksButton", r.tabMisDecksButton);
        Set(so, "constructorView", r.constructorView);
        Set(so, "misDecksView", r.misDecksView);
        Set(so, "misDecksListContent", r.misDecksListContent);

        Set(so, "searchInput", r.searchInput);
        Set(so, "collectionCountText", r.collectionCountText);
        Set(so, "catAllButton", r.catAllButton);
        Set(so, "catMonsterButton", r.catMonsterButton);
        Set(so, "catSpellButton", r.catSpellButton);
        Set(so, "catTrapButton", r.catTrapButton);
        Set(so, "catEquipButton", r.catEquipButton);
        Set(so, "typeDropdown", r.typeDropdown);
        Set(so, "attributeDropdown", r.attributeDropdown);
        Set(so, "levelDropdown", r.levelDropdown);
        Set(so, "rarityDropdown", r.rarityDropdown);
        Set(so, "sortDropdown", r.sortDropdown);
        Set(so, "clearFiltersButton", r.clearFiltersButton);
        Set(so, "collectionGridContent", r.collectionGridContent);
        Set(so, "collectionTemplate", r.collectionTemplate);
        Set(so, "pageButtonsContent", r.pageButtonsContent);
        Set(so, "pageButtonTemplate", r.pageButtonTemplate);
        Set(so, "prevPageButton", r.prevPageButton);
        Set(so, "nextPageButton", r.nextPageButton);

        Set(so, "previewCard", r.previewCard);
        Set(so, "previewNameText", r.previewNameText);
        Set(so, "previewCategoryText", r.previewCategoryText);
        Set(so, "previewDescText", r.previewDescText);
        Set(so, "previewAtkText", r.previewAtkText);
        Set(so, "previewDefText", r.previewDefText);
        Set(so, "ownedCountText", r.ownedCountText);
        Set(so, "copiesInDeckText", r.copiesInDeckText);
        Set(so, "stepMinusButton", r.stepMinusButton);
        Set(so, "stepPlusButton", r.stepPlusButton);
        Set(so, "addButton", r.addButton);
        Set(so, "removeButton", r.removeButton);
        Set(so, "favoriteButton", r.favoriteButton);
        Set(so, "favoriteIcon", r.favoriteIcon);

        Set(so, "tabStatsButton", r.tabStatsButton);
        Set(so, "tabCurveButton", r.tabCurveButton);
        Set(so, "tabHandButton", r.tabHandButton);
        Set(so, "tabFusionButton", r.tabFusionButton);
        Set(so, "centerInspectorPanel", r.centerInspectorPanel);
        Set(so, "centerCurvePanel", r.centerCurvePanel);
        Set(so, "centerHandPanel", r.centerHandPanel);
        Set(so, "centerFusionPanel", r.centerFusionPanel);
        Set(so, "curveContent", r.curveContent);
        Set(so, "handContent", r.handContent);
        Set(so, "fusionContent", r.fusionContent);
        Set(so, "redrawHandButton", r.redrawHandButton);

        Set(so, "deckNameInput", r.deckNameInput);
        Set(so, "deckSlotDropdown", r.deckSlotDropdown);
        Set(so, "deleteDeckButton", r.deleteDeckButton);
        Set(so, "deckHeaderCountText", r.deckHeaderCountText);
        Set(so, "monsterCountText", r.monsterCountText);
        Set(so, "spellCountText", r.spellCountText);
        Set(so, "trapCountText", r.trapCountText);
        Set(so, "deckListContent", r.deckListContent);
        Set(so, "deckRowTemplate", r.deckRowTemplate);
        Set(so, "extraButton", r.extraButton);
        Set(so, "donut", r.donut);
        Set(so, "distMonstersText", r.distMonstersText);
        Set(so, "distSpellsText", r.distSpellsText);
        Set(so, "distTrapsText", r.distTrapsText);
        Set(so, "avgCostText", r.avgCostText);
        Set(so, "avgAtkText", r.avgAtkText);
        Set(so, "avgDefText", r.avgDefText);

        Set(so, "settingsPanel", r.settingsPanel);
        Set(so, "settingsCloseButton", r.settingsCloseButton);
        Set(so, "resetDeckButton", r.resetDeckButton);
        Set(so, "levelStarSprite", _starSprite);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Fábricas de UI
    // ═══════════════════════════════════════════════════════════════════════
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

    // Ayuda: Place con una tupla de 4 fracciones (xMin,yMin,xMax,yMax).
    static (Vector2, Vector2, Vector2, Vector2) F(float xMin, float yMin, float xMax, float yMax)
        => (new Vector2(xMin, yMin), new Vector2(xMax, yMax), Vector2.zero, Vector2.zero);

    static void Place(Component c, (Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax) f)
        => Place(c, f.aMin, f.aMax, f.offMin, f.offMax);

    static void SizePivot(Component c, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)c.transform;
        rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static Image AddImg(RectTransform rt, Color c)
    {
        var img = rt.gameObject.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
        img.color = c;
        return img;
    }

    static void Accent(RectTransform panel, bool top = false, bool bottom = false, bool left = false, bool right = false)
    {
        var rt = Region(panel, "Accent", Vector2.zero, Vector2.zero);
        if (bottom) { rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.sizeDelta = new Vector2(0, 2); rt.pivot = new Vector2(0.5f, 0f); }
        else if (top) { rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.sizeDelta = new Vector2(0, 2); rt.pivot = new Vector2(0.5f, 1f); }
        else if (right) { rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1); rt.sizeDelta = new Vector2(2, 0); rt.pivot = new Vector2(1f, 0.5f); }
        else if (left) { rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.sizeDelta = new Vector2(2, 0); rt.pivot = new Vector2(0f, 0.5f); }
        rt.anchoredPosition = Vector2.zero;
        AddImg(rt, Line).raycastTarget = false;
    }

    static TMP_Text Label(Transform parent, string name, string text, float size, Color color,
                          TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.raycastTarget = false; t.enableWordWrapping = false;
        Stretch(t);
        return t;
    }

    static Button MakeButton(Transform parent, string name, string text, Color fill, Color textColor, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = fill;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f); cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
        btn.colors = cb;
        var lbl = Label(go.transform, "Label", text, fontSize, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(lbl); lbl.margin = new Vector4(6, 0, 6, 0);
        return btn;
    }

    static Button MakeIconButton(Transform parent, string name, Sprite icon, out Image iconImg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var bg = go.GetComponent<Image>(); bg.color = new Color(0, 0, 0, 0.001f);
        var btn = go.GetComponent<Button>(); btn.targetGraphic = bg;
        var icoRT = Region(go.transform, "Icon", Vector2.zero, Vector2.one);
        icoRT.offsetMin = new Vector2(4, 4); icoRT.offsetMax = new Vector2(-4, -4);
        iconImg = AddImg(icoRT, new Color(1f, 1f, 1f, 0.3f));
        iconImg.sprite = icon; iconImg.preserveAspect = true; iconImg.raycastTarget = false;
        return btn;
    }

    static void FitLabel(Button btn, float min, float max)
    {
        var t = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (t == null) return;
        t.enableAutoSizing = true; t.fontSizeMin = min; t.fontSizeMax = max;
    }

    static TMP_InputField MakeInputField(Transform parent, string name, string placeholder, out TMP_InputField field)
    {
        var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        field = go.GetComponent<TMP_InputField>();
        var bg = go.GetComponent<Image>(); if (bg != null) bg.color = Field;
        if (field.placeholder is TextMeshProUGUI ph) { ph.text = placeholder; ph.color = new Color(0.7f, 0.72f, 0.8f, 0.7f); ph.fontSize = 18; }
        if (field.textComponent is TextMeshProUGUI tc) { tc.color = Bright; tc.fontSize = 18; }
        return field;
    }

    static TMP_Dropdown MakeDropdown(Transform parent, string name)
    {
        var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        var dd = go.GetComponent<TMP_Dropdown>();
        var bg = go.GetComponent<Image>(); if (bg != null) bg.color = Field;
        var lbl = go.GetComponentInChildren<TextMeshProUGUI>();
        if (lbl != null) { lbl.color = Bright; lbl.fontSize = 16; }
        return dd;
    }

    static RectTransform MakeGrid(RectTransform area, int columns, Vector2 cell, Vector2 spacing)
    {
        var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        var scrollRT = (RectTransform)scrollGO.transform;
        scrollRT.SetParent(area, false); Stretch(scrollRT);
        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 26f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = Region(scrollRT, "Viewport", Vector2.zero, Vector2.one);
        AddImg(viewport, new Color(0, 0, 0, 0.01f));
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = Region(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f); content.anchoredPosition = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = cell; grid.spacing = spacing;
        grid.padding = new RectOffset(6, 6, 6, 6);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns; grid.childAlignment = TextAnchor.UpperCenter;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport; scroll.content = content;
        return content;
    }

    static RectTransform MakeVScroll(RectTransform area)
    {
        var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        var scrollRT = (RectTransform)scrollGO.transform;
        scrollRT.SetParent(area, false); Stretch(scrollRT);
        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 24f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = Region(scrollRT, "Viewport", Vector2.zero, Vector2.one);
        AddImg(viewport, new Color(0, 0, 0, 0.01f));
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = Region(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f); content.anchoredPosition = Vector2.zero;
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 5f; vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport; scroll.content = content;
        return content;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Utilidades de editor
    // ═══════════════════════════════════════════════════════════════════════
    static void Set(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogError($"DeckBuilderBuilder: no existe el campo '{prop}' en {so.targetObject.GetType().Name}."); return; }
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
        foreach (var rootGO in scene.GetRootGameObjects())
            if (rootGO.name == name) return rootGO;
        return null;
    }

    static void MoveToScene(GameObject go, Scene scene)
    {
        if (go.scene != scene && scene.IsValid())
            SceneManager.MoveGameObjectToScene(go, scene);
    }

    // Contenedor de todas las referencias construidas, para cablear al final.
    class Refs
    {
        public Button backButton, saveButton, newButton, exportButton, settingsButton;
        public TMP_Text statusText;
        public Button tabConstructorButton, tabMisDecksButton;
        public GameObject constructorView, misDecksView;
        public RectTransform misDecksListContent;

        public TMP_InputField searchInput;
        public TMP_Text collectionCountText;
        public Button catAllButton, catMonsterButton, catSpellButton, catTrapButton, catEquipButton;
        public TMP_Dropdown typeDropdown, attributeDropdown, levelDropdown, rarityDropdown, sortDropdown;
        public Button clearFiltersButton;
        public RectTransform collectionGridContent;
        public CollectionCardView collectionTemplate;
        public RectTransform pageButtonsContent;
        public Button pageButtonTemplate, prevPageButton, nextPageButton;

        public CardDisplay previewCard;
        public TMP_Text previewNameText, previewCategoryText, previewDescText, previewAtkText, previewDefText;
        public TMP_Text ownedCountText, copiesInDeckText;
        public Button stepMinusButton, stepPlusButton, addButton, removeButton, favoriteButton;
        public Image favoriteIcon;

        public Button tabStatsButton, tabCurveButton, tabHandButton, tabFusionButton;
        public GameObject centerInspectorPanel, centerCurvePanel, centerHandPanel, centerFusionPanel;
        public RectTransform curveContent, handContent, fusionContent;
        public Button redrawHandButton;

        public TMP_InputField deckNameInput;
        public TMP_Dropdown deckSlotDropdown;
        public Button deleteDeckButton;
        public TMP_Text deckHeaderCountText, monsterCountText, spellCountText, trapCountText;
        public RectTransform deckListContent;
        public DeckCardView deckRowTemplate;
        public Button extraButton;
        public UIDonutChart donut;
        public TMP_Text distMonstersText, distSpellsText, distTrapsText, avgCostText, avgAtkText, avgDefText;

        public GameObject settingsPanel;
        public Button settingsCloseButton, resetDeckButton;
    }
}
