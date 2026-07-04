using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Construye la escena del Constructor de Deck como objetos reales editables:
/// columna de COLECCIÓN (búsqueda + filtros + lista), columna de MAZO (lista +
/// estadísticas) y barra superior (título, Guardar, Volver, estado). Cablea las
/// referencias del <see cref="DeckBuilderController"/> y de las plantillas.
/// </summary>
public static class DeckBuilderBuilder
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
    static readonly Color Field       = new Color(0.05f, 0.06f, 0.10f, 1f);

    public static void BuildInScene(DeckBuilderController controller)
    {
        Scene scene = controller.gameObject.scene;

        var previous = FindRootInScene(scene, "DeckBuilderCanvas");
        if (previous != null) Object.DestroyImmediate(previous);

        EnsureEventSystem(scene);

        var canvasGO = new GameObject("DeckBuilderCanvas", typeof(Canvas), typeof(CanvasScaler),
                                      typeof(GraphicRaycaster), typeof(ResponsiveCanvasScaler));
        MoveToScene(canvasGO, scene);
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var canvasRT = canvasGO.GetComponent<RectTransform>();

        var bg = NewImage("Background", canvasRT, BgColor);
        Stretch(bg.rectTransform);

        // ── Barra superior ───────────────────────────────────────────────
        var title = MakeText("Title", canvasRT, "CONSTRUCTOR DE DECK", 64, GoldBright, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        AnchorTopStretch(title.rectTransform, 280, 280, 84, -24);
        title.enableAutoSizing = true; title.fontSizeMin = 28; title.fontSizeMax = 64;

        var backBtn = MakeButton("Btn_Volver", canvasRT, "Volver");
        AnchorTopLeft(backBtn, new Vector2(40, -30), new Vector2(170, 66));

        var saveBtn = MakeButton("Btn_Guardar", canvasRT, "Guardar");
        var saveRT = (RectTransform)saveBtn.transform;
        saveRT.anchorMin = saveRT.anchorMax = new Vector2(1f, 1f);
        saveRT.pivot = new Vector2(1f, 1f);
        saveRT.anchoredPosition = new Vector2(-40, -30);
        saveRT.sizeDelta = new Vector2(190, 66);

        var status = MakeText("Status", canvasRT, "", 26, TextLight, TextAlignmentOptions.Center);
        AnchorTopStretch(status.rectTransform, 300, 300, 40, -118);

        // ── Panel de Colección (izquierda) ───────────────────────────────
        var left = MakeColumn("CollectionPanel", canvasRT, new Vector2(0f, 0f), new Vector2(0.52f, 1f),
                              new Vector2(40, 40), new Vector2(-12, -170));

        var search = MakeSearchInput(left, "Buscar por nombre o id...");
        LayoutMin(search.gameObject, 56);

        var catRow = MakeRow(left, 56);
        var catAll     = MakeButton("Btn_Todos", catRow, "Todos");
        var catMonster = MakeButton("Btn_Monstruos", catRow, "Monstruos");
        var catSpell   = MakeButton("Btn_Magias", catRow, "Magias");
        var catTrap    = MakeButton("Btn_Trampas", catRow, "Trampas");
        var catEquip   = MakeButton("Btn_Equipos", catRow, "Equipos");
        FitButtonText(catAll); FitButtonText(catMonster); FitButtonText(catSpell); FitButtonText(catTrap); FitButtonText(catEquip);

        var sortBtn = MakeButton("Btn_Orden", left, "Orden: Nombre");
        LayoutMin(sortBtn.gameObject, 50);
        var sortLabel = sortBtn.GetComponentInChildren<TextMeshProUGUI>();

        var (collScroll, collContent) = MakeVerticalScroll(left);
        var collLe = collScroll.AddComponent<LayoutElement>(); collLe.flexibleHeight = 1; collLe.minHeight = 200;

        // ── Panel de Mazo + Estadísticas (derecha) ───────────────────────
        var right = MakeColumn("DeckPanel", canvasRT, new Vector2(0.52f, 0f), new Vector2(1f, 1f),
                               new Vector2(12, 40), new Vector2(-40, -170));

        var deckCount = MakeText("DeckCount", right, "Mazo: 0/40", 34, GoldBright, TextAlignmentOptions.Left);
        deckCount.fontStyle = FontStyles.Bold;
        LayoutMin(deckCount.gameObject, 48);

        var (deckScroll, deckContent) = MakeVerticalScroll(right);
        var deckLe = deckScroll.AddComponent<LayoutElement>(); deckLe.flexibleHeight = 1.4f; deckLe.minHeight = 240;

        var statsLabel = MakeText("StatsLabel", right, "Estadísticas del mazo", 26, Gold, TextAlignmentOptions.Left);
        LayoutMin(statsLabel.gameObject, 38);

        var statsBox = NewImage("StatsBox", right, new Color(0f, 0f, 0f, 0.25f));
        var statsBoxLe = statsBox.gameObject.AddComponent<LayoutElement>(); statsBoxLe.flexibleHeight = 1f; statsBoxLe.minHeight = 220;
        var stats = MakeText("StatsText", statsBox.transform, "", 24, TextLight, TextAlignmentOptions.TopLeft);
        Stretch(stats.rectTransform); stats.margin = new Vector4(16, 12, 16, 12);

        // ── Plantillas ───────────────────────────────────────────────────
        var collTemplate = BuildCollectionTemplate(collContent);
        collTemplate.gameObject.SetActive(false);
        var deckTemplate = BuildDeckTemplate(deckContent);
        deckTemplate.gameObject.SetActive(false);

        // ── Cablear el controlador ───────────────────────────────────────
        var so = new SerializedObject(controller);
        Set(so, "titleText", title);
        Set(so, "backButton", backBtn);
        Set(so, "saveButton", saveBtn);
        Set(so, "deckCountText", deckCount);
        Set(so, "statusText", status);
        Set(so, "searchInput", search);
        Set(so, "sortButton", sortBtn);
        Set(so, "sortLabel", sortLabel);
        Set(so, "catAllButton", catAll);
        Set(so, "catMonsterButton", catMonster);
        Set(so, "catSpellButton", catSpell);
        Set(so, "catTrapButton", catTrap);
        Set(so, "catEquipButton", catEquip);
        Set(so, "collectionContent", collContent);
        Set(so, "collectionTemplate", collTemplate);
        Set(so, "deckContent", deckContent);
        Set(so, "deckTemplate", deckTemplate);
        Set(so, "statsText", stats);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("DeckBuilderBuilder: escena del Constructor de Deck construida y cableada.");
    }

    // ── Plantillas ───────────────────────────────────────────────────────

    static CollectionCardView BuildCollectionTemplate(Transform parent)
    {
        var card = new GameObject("CollectionCard",
            typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup),
            typeof(LayoutElement), typeof(CollectionCardView));
        card.transform.SetParent(parent, false);
        card.GetComponent<Image>().color = PanelFill;
        var cle = card.GetComponent<LayoutElement>(); cle.minHeight = 112; cle.preferredHeight = 112;
        var hlg = card.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 8, 8); hlg.spacing = 12;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var art = NewImage("Art", card.transform, Color.white);
        art.preserveAspect = true;
        var artLe = art.gameObject.AddComponent<LayoutElement>(); artLe.minWidth = 80; artLe.preferredWidth = 80;

        var info = MakeInfoColumn(card.transform, out var nameText, out var infoText, out var countText);

        var addBtn = MakeButton("Btn_Anadir", card.transform, "Añadir");
        var addLe = addBtn.GetComponent<LayoutElement>(); addLe.minWidth = 130; addLe.preferredWidth = 130;

        var view = card.GetComponent<CollectionCardView>();
        var so = new SerializedObject(view);
        Set(so, "art", art);
        Set(so, "nameText", nameText);
        Set(so, "infoText", infoText);
        Set(so, "countText", countText);
        Set(so, "addButton", addBtn);
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    static Transform MakeInfoColumn(Transform parent, out TextMeshProUGUI nameText, out TextMeshProUGUI infoText, out TextMeshProUGUI countText)
    {
        var info = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        info.transform.SetParent(parent, false);
        info.GetComponent<LayoutElement>().flexibleWidth = 1;
        var vlg = info.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 1; vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false; vlg.childAlignment = TextAnchor.MiddleLeft;

        nameText = MakeText("Name", info.transform, "Nombre", 28, GoldBright, TextAlignmentOptions.Left);
        nameText.fontStyle = FontStyles.Bold;
        infoText = MakeText("Info", info.transform, "Tipo · ATK / DEF", 20, TextLight, TextAlignmentOptions.Left);
        countText = MakeText("Count", info.transform, "Copias: 0    En mazo: 0", 20, TextLight, TextAlignmentOptions.Left);
        return info.transform;
    }

    static DeckCardView BuildDeckTemplate(Transform parent)
    {
        var card = new GameObject("DeckCard",
            typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup),
            typeof(LayoutElement), typeof(DeckCardView));
        card.transform.SetParent(parent, false);
        card.GetComponent<Image>().color = PanelFill;
        var cle = card.GetComponent<LayoutElement>(); cle.minHeight = 64; cle.preferredHeight = 64;
        var hlg = card.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 6, 6); hlg.spacing = 10;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var art = NewImage("Art", card.transform, Color.white);
        art.preserveAspect = true;
        var artLe = art.gameObject.AddComponent<LayoutElement>(); artLe.minWidth = 44; artLe.preferredWidth = 44;

        var nameText = MakeText("Name", card.transform, "Nombre", 26, TextLight, TextAlignmentOptions.Left);
        nameText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        var countText = MakeText("Count", card.transform, "x1", 26, GoldBright, TextAlignmentOptions.Center);
        countText.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;

        var removeBtn = MakeButton("Btn_Quitar", card.transform, "Quitar");
        var rmLe = removeBtn.GetComponent<LayoutElement>(); rmLe.minWidth = 110; rmLe.preferredWidth = 110;

        var view = card.GetComponent<DeckCardView>();
        var so = new SerializedObject(view);
        Set(so, "art", art);
        Set(so, "nameText", nameText);
        Set(so, "countText", countText);
        Set(so, "removeButton", removeBtn);
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // ── Fábricas ─────────────────────────────────────────────────────────

    static RectTransform MakeColumn(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10; vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        return rt;
    }

    static Transform MakeRow(Transform parent, float height)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().minHeight = height;
        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6; hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
        return go.transform;
    }

    static (GameObject scrollGO, RectTransform content) MakeVerticalScroll(Transform parent)
    {
        var scrollGO = DefaultControls.CreateScrollView(new DefaultControls.Resources());
        scrollGO.name = "Scroll";
        scrollGO.transform.SetParent(parent, false);
        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        var hbar = scrollGO.transform.Find("Scrollbar Horizontal");
        if (hbar != null) { scroll.horizontalScrollbar = null; Object.DestroyImmediate(hbar.gameObject); }
        var vp = scroll.viewport != null ? scroll.viewport.GetComponent<Image>() : null;
        if (vp != null) vp.color = new Color(0f, 0f, 0f, 0.22f);

        var content = scroll.content;
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8; vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return (scrollGO, content);
    }

    static TMP_InputField MakeSearchInput(Transform parent, string placeholder)
    {
        var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = "SearchInput";
        go.transform.SetParent(parent, false);
        var input = go.GetComponent<TMP_InputField>();
        var bg = go.GetComponent<Image>(); if (bg != null) bg.color = Field;
        if (input.placeholder is TextMeshProUGUI ph) { ph.text = placeholder; ph.color = new Color(0.7f, 0.72f, 0.8f, 0.7f); }
        if (input.textComponent is TextMeshProUGUI tc) tc.color = TextLight;
        return input;
    }

    static Button MakeButton(string name, Transform parent, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = Color.white;
        var le = go.GetComponent<LayoutElement>(); le.minHeight = 60; le.preferredHeight = 60;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = BtnNormal; cb.highlightedColor = BtnHover; cb.pressedColor = BtnPressed;
        cb.selectedColor = BtnHover; cb.disabledColor = BtnDisabled;
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.1f;
        btn.colors = cb;
        var txt = MakeText("Label", go.transform, label, 28, TextLight, TextAlignmentOptions.Center);
        Stretch(txt.rectTransform); txt.margin = new Vector4(10, 0, 10, 0);
        return btn;
    }

    static void FitButtonText(Button btn)
    {
        var t = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (t == null) return;
        t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = 26;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = color;
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

    // ── Utilidades ───────────────────────────────────────────────────────

    static void LayoutMin(GameObject go, float minHeight)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minHeight = minHeight; le.preferredHeight = minHeight;
    }

    static void AnchorTopLeft(Component c, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)c.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Set(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogError($"DeckBuilderBuilder: no existe el campo '{prop}'."); return; }
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
}
