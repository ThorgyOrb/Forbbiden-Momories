using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cerebro de la escena "Library of the Gods" (LibraryGodsScene), el rediseño del
/// catálogo. Reusa los sistemas existentes (LibraryQueryService, LibraryCardSlot,
/// CardDisplay, Model3DViewer) SIN tocar LibraryManager ni la escena original.
///
/// Todo lo cablea <c>LibraryGodsSceneBuilder</c>; no hace falta tocarlo a mano.
/// Es un primer pase estructural (estilo plano con la paleta); iterar con capturas.
/// </summary>
public class LibraryGodsController : MonoBehaviour
{
    public enum Tab { All, Monsters, Spells, Traps, Divine, Favorites }

    [Header("Grilla")]
    [SerializeField] private RectTransform gridContent;
    [SerializeField] private GameObject cardSlotPrefab;   // LibraryCardSlot

    [Header("Encabezado (stats)")]
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private TMP_Text discoveredText;
    [SerializeField] private TMP_Text ownedText;
    [SerializeField] private TMP_Text completionText;
    [SerializeField] private Image completionRing;        // Image tipo Filled/Radial360

    [Header("Búsqueda / orden")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private TMP_Dropdown sortDropdown;

    [Header("Pestañas (en orden del enum Tab)")]
    [SerializeField] private Button[] tabButtons;         // TODAS, MONSTRUOS, MAGIAS, TRAMPAS, DIVINAS, FAVORITAS
    [SerializeField] private Color tabActiveColor = new Color(0.63f, 0.40f, 0.95f);
    [SerializeField] private Color tabIdleColor = new Color(0.16f, 0.15f, 0.22f);

    [Header("Sidebar: rareza (Legendary, Epic, Rare, Common)")]
    [SerializeField] private TMP_Text[] rarityCountTexts; // 4, en ese orden

    [Header("Sidebar: colecciones (filas dinámicas)")]
    [Tooltip("Contenedor (con VerticalLayoutGroup) donde se generan las filas de colección; " +
             "una por cada tipo de monstruo que tenga cartas. Crece solo al añadir tipos.")]
    [SerializeField] private RectTransform collectionsContainer;

    [Header("Sidebar: botón Ver modelo 3D")]
    [SerializeField] private Button view3DButton;
    [SerializeField] private Model3DViewer model3DViewer;

    [Header("Panel derecho (detalle)")]
    [SerializeField] private CardDisplay previewCard;
    [SerializeField] private TMP_Text detNameText;
    [SerializeField] private TMP_Text detAtkText;
    [SerializeField] private TMP_Text detDefText;
    [SerializeField] private TMP_Text detTypeText;
    [SerializeField] private TMP_Text detAttrText;
    [SerializeField] private TMP_Text detRarityText;
    [SerializeField] private TMP_Text detSourceText;
    [SerializeField] private TMP_Text detRateText;

    [Header("Iconos del detalle")]
    [SerializeField] private CardIconConfig iconConfig;
    [SerializeField] private Image detTypeIcon;
    [SerializeField] private Image detAttrIcon;
    [SerializeField] private RectTransform levelIconsRow; // pips: icono × nivel
    [SerializeField] private Sprite levelIconSprite;
    [SerializeField] private Color levelIconColor = new Color(0.93f, 0.75f, 0.33f);

    [Header("Resaltado de selección (aura violeta suave)")]
    [SerializeField] private Color selectionLightColor = new Color(0.60f, 0.40f, 1f);

    private Tab _tab = Tab.All;
    private string _search = "";
    private readonly List<GameObject> _slots = new();
    private LibraryEntry _selected;
    private GameObject _selectionFx; // focos de luz, reparentados bajo el slot elegido

    // Nombre de colección (plural, en español) por tipo de monstruo. Los tipos
    // no listados usan el nombre del enum en mayúsculas — así la lista crece sola
    // al definir tipos nuevos, sin romperse.
    private static readonly Dictionary<MonsterType, string> CollectionNames = new()
    {
        { MonsterType.Dragon, "DRAGONES" },
        { MonsterType.Spellcaster, "HECHICEROS" },
        { MonsterType.Fiend, "DEMONIOS" },
        { MonsterType.Beast, "BESTIAS" },
        { MonsterType.Insect, "INSECTOS" },
        { MonsterType.Plant, "PLANTAS" },
        { MonsterType.Fish, "PECES" },
        { MonsterType.Aqua, "ACUÁTICOS" },
        { MonsterType.SeaSerpent, "SERPIENTES MARINAS" },
        { MonsterType.Zombie, "ZOMBIS" },
        { MonsterType.Dinosaur, "DINOSAURIOS" },
        { MonsterType.WingedBeast, "BESTIAS ALADAS" },
        { MonsterType.Warrior, "GUERREROS" },
        { MonsterType.Machine, "MÁQUINAS" },
        { MonsterType.Thunder, "TRUENO" },
        { MonsterType.Fairy, "HADAS" },
        { MonsterType.Reptile, "REPTILES" },
        { MonsterType.Rock, "ROCA" },
        { MonsterType.Pyro, "PIRO" },
    };

    void Start()
    {
        PlayerCollection.EnsureExists(); // singleton de progreso (carga guardado); si no, todo saldría bloqueado
        LibraryCatalog.EnsureLoaded();

        if (searchInput != null)
            searchInput.onValueChanged.AddListener(s => { _search = s; RefreshGrid(); SelectFirst(); });
        if (sortDropdown != null)
            sortDropdown.onValueChanged.AddListener(_ => { RefreshGrid(); SelectFirst(); });

        WireTabs();

        if (view3DButton != null) view3DButton.onClick.AddListener(OnView3D);

        RefreshGrid();
        UpdateHeaderAndSidebar();
        SelectFirst();
    }

    // ── Pestañas ─────────────────────────────────────────────────────────
    private void WireTabs()
    {
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            Tab t = (Tab)i;
            tabButtons[i].onClick.AddListener(() => { _tab = t; RefreshGrid(); HighlightTabs(); SelectFirst(); });
        }
        HighlightTabs();
    }

    private void HighlightTabs()
    {
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            var img = tabButtons[i].GetComponent<Image>();
            if (img != null) img.color = (Tab)i == _tab ? tabActiveColor : tabIdleColor;
        }
    }

    // ── Consulta / grilla ────────────────────────────────────────────────
    private IEnumerable<LibraryEntry> QueryEntries()
    {
        var all = LibraryQueryService.BuildAllEntries();
        IEnumerable<LibraryEntry> q = all.Where(MatchesTab);

        if (!string.IsNullOrWhiteSpace(_search))
        {
            string s = _search.Trim().ToLowerInvariant();
            q = q.Where(e => !string.IsNullOrEmpty(e.card.cardName)
                             && e.card.cardName.ToLowerInvariant().Contains(s));
        }

        return SortBy(q);
    }

    private IEnumerable<LibraryEntry> SortBy(IEnumerable<LibraryEntry> q)
    {
        int opt = sortDropdown != null ? sortDropdown.value : 0;
        return opt switch
        {
            1 => q.OrderBy(e => e.card.cardName),
            2 => q.OrderByDescending(e => e.card.baseAtk),
            3 => q.OrderByDescending(e => e.card.rarity),
            _ => q.OrderBy(e => e.card.cardId),
        };
    }

    private bool MatchesTab(LibraryEntry e) => _tab switch
    {
        Tab.Monsters => e.card.IsMonster,
        Tab.Spells => e.card.IsSpell || e.card.IsEquip,
        Tab.Traps => e.card.IsTrap,
        Tab.Divine => e.card.IsSpecial || e.card.IsRitual,
        Tab.Favorites => e.IsFavorite,
        _ => true
    };

    private void RefreshGrid()
    {
        foreach (var go in _slots) if (go != null) Destroy(go);
        _slots.Clear();

        if (gridContent == null || cardSlotPrefab == null) return;

        foreach (var entry in QueryEntries())
        {
            var go = Instantiate(cardSlotPrefab, gridContent);
            var slot = go.GetComponent<LibraryCardSlot>();
            if (slot != null) slot.Setup(entry, (en, _) => SelectCard(en, go));
            _slots.Add(go);
        }
    }

    private void SelectFirst()
    {
        // El slot que corresponde a la primera carta seleccionable (para el brillo).
        var q = QueryEntries().ToList();
        int idx = q.FindIndex(e => e.state != CardState.Locked);
        if (idx < 0) return;
        GameObject slotGO = idx < _slots.Count ? _slots[idx] : null;
        SelectCard(q[idx], slotGO);
    }

    // ── Selección / panel derecho ────────────────────────────────────────
    private void SelectCard(LibraryEntry entry, GameObject slotGO = null)
    {
        if (entry == null || entry.state == CardState.Locked) return;
        _selected = entry;
        var card = entry.card;

        HighlightSelected(slotGO);

        if (previewCard != null)
        {
            previewCard.Setup(card);
            previewCard.SetPosition(CardPosition.FaceUpAttack);
            previewCard.SetHoloIntensityScale(0.35f); // carta grande: efectos suaves
        }

        Set(detNameText, card.cardName);
        Set(detRarityText, RarityName(card.rarity));
        Set(detSourceText, FirstSource(entry));
        Set(detRateText, DropRate(card));

        if (card.IsMonster)
        {
            Set(detAtkText, card.baseAtk.ToString());
            Set(detDefText, card.baseDef.ToString());
            Set(detTypeText, card.monsterType.ToString());
            Set(detAttrText, card.attribute.ToString());
            SetIcon(detTypeIcon, iconConfig != null ? iconConfig.GetTypeIcon(card.monsterType) : null);
            SetIcon(detAttrIcon, iconConfig != null ? iconConfig.GetAttributeIcon(card.attribute) : null);
            RebuildLevelIcons(card.stars);
        }
        else
        {
            Set(detAtkText, "—");
            Set(detDefText, "—");
            Set(detTypeText, card.CategoryLabel);
            Set(detAttrText, "—");
            SetIcon(detTypeIcon, null);
            SetIcon(detAttrIcon, null);
            RebuildLevelIcons(0);
        }

        if (view3DButton != null)
            view3DButton.interactable = card.IsMonster && card.monsterModelPrefab != null;
    }

    /// <summary>
    /// Mueve (o crea) el aura violeta suave detrás del slot seleccionado. Queda
    /// DETRÁS de la carta y asoma como un brillo parejo por los cuatro lados (como la
    /// librería de referencia). Si el slot se destruyó (al refrescar), se recrea.
    /// </summary>
    private void HighlightSelected(GameObject slotGO)
    {
        if (slotGO == null) return;

        if (_selectionFx == null)
        {
            _selectionFx = new GameObject("SelectionGlow", typeof(RectTransform), typeof(SelectionGlow));
            _selectionFx.GetComponent<SelectionGlow>().color = selectionLightColor;
        }

        var rt = (RectTransform)_selectionFx.transform;
        rt.SetParent(slotGO.transform, false);
        rt.SetAsFirstSibling();                 // detrás de la carta
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        // El margen debe coincidir con el emplumado del sprite para que el 9-slice
        // deje el brillo justo por FUERA de la carta, parejo en los cuatro lados.
        float m = SelectionGlow.Feather;
        rt.offsetMin = new Vector2(-m, -m);
        rt.offsetMax = new Vector2(m, m);
        rt.localScale = Vector3.one;
        _selectionFx.SetActive(true);
    }

    private void OnView3D()
    {
        if (_selected?.card == null) return;
        if (model3DViewer == null)
        {
            Debug.LogWarning("LibraryGodsController: 'model3DViewer' no asignado.");
            return;
        }
        model3DViewer.Show(_selected.card);
    }

    // ── Encabezado + sidebar ─────────────────────────────────────────────
    private void UpdateHeaderAndSidebar()
    {
        var (total, discovered, owned, completion) = LibraryQueryService.GetGlobalStats();
        Set(totalText, total.ToString());
        Set(discoveredText, discovered.ToString());
        Set(ownedText, owned.ToString());
        Set(completionText, completion.ToString("0.0") + "%");
        if (completionRing != null) completionRing.fillAmount = completion / 100f;

        var all = LibraryQueryService.BuildAllEntries();

        if (rarityCountTexts != null)
        {
            CardRarity[] order = { CardRarity.Legendary, CardRarity.Epic, CardRarity.Rare, CardRarity.Common };
            for (int i = 0; i < order.Length && i < rarityCountTexts.Length; i++)
                Set(rarityCountTexts[i], all.Count(e => e.card.rarity == order[i]).ToString());
        }

        BuildCollections(all);
    }

    /// <summary>
    /// Genera una fila por cada tipo de monstruo que TENGA cartas en el catálogo
    /// (owned/total), con su icono. Crece solo al definir tipos nuevos; no hay que
    /// mantener una lista fija. Va en un contenedor con VerticalLayoutGroup.
    /// </summary>
    private void BuildCollections(List<LibraryEntry> all)
    {
        if (collectionsContainer == null) return;

        for (int i = collectionsContainer.childCount - 1; i >= 0; i--)
            Destroy(collectionsContainer.GetChild(i).gameObject);

        foreach (MonsterType type in System.Enum.GetValues(typeof(MonsterType)))
        {
            int totalT = all.Count(e => e.card.IsMonster && e.card.monsterType == type);
            if (totalT == 0) continue; // aún sin cartas de ese tipo → no se muestra
            int ownedT = all.Count(e => e.card.IsMonster && e.card.monsterType == type && e.state == CardState.Owned);
            BuildCollectionRow(type, ownedT, totalT);
        }
    }

    private void BuildCollectionRow(MonsterType type, int owned, int total)
    {
        var rowGO = new GameObject("Col_" + type, typeof(RectTransform));
        var rt = (RectTransform)rowGO.transform;
        rt.SetParent(collectionsContainer, false);
        var le = rowGO.AddComponent<LayoutElement>();
        le.minHeight = 30f; le.preferredHeight = 30f;

        // Icono del tipo (antes del texto).
        var icoGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var icoRT = (RectTransform)icoGO.transform;
        icoRT.SetParent(rt, false);
        icoRT.anchorMin = new Vector2(0f, 0.5f); icoRT.anchorMax = new Vector2(0f, 0.5f);
        icoRT.pivot = new Vector2(0f, 0.5f);
        icoRT.sizeDelta = new Vector2(22, 22);
        icoRT.anchoredPosition = new Vector2(2, 0);
        var ico = icoGO.GetComponent<Image>();
        ico.preserveAspect = true;
        ico.raycastTarget = false;
        var typeSprite = iconConfig != null ? iconConfig.GetTypeIcon(type) : null;
        ico.sprite = typeSprite;
        ico.color = typeSprite != null ? Color.white : new Color(0.5f, 0.5f, 0.6f, 0.5f);

        // Nombre.
        var name = NewText(rt, "N", CollectionName(type), 14, new Color(0.85f, 0.83f, 0.75f),
                           TextAlignmentOptions.Left);
        var nrt = (RectTransform)name.transform;
        nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(0.7f, 1f);
        nrt.offsetMin = new Vector2(30, 0); nrt.offsetMax = Vector2.zero;

        // Conteo owned/total.
        var count = NewText(rt, "V", $"{owned}/{total}", 14, new Color(0.93f, 0.75f, 0.33f),
                            TextAlignmentOptions.Right, FontStyles.Bold);
        var crt = (RectTransform)count.transform;
        crt.anchorMin = new Vector2(0.6f, 0f); crt.anchorMax = new Vector2(1f, 1f);
        crt.offsetMin = Vector2.zero; crt.offsetMax = new Vector2(-4, 0);
    }

    private static string CollectionName(MonsterType type)
        => CollectionNames.TryGetValue(type, out var n) ? n : type.ToString().ToUpperInvariant();

    private static TMP_Text NewText(Transform parent, string name, string text, float size,
                                    Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.raycastTarget = false; t.enableWordWrapping = false;
        return t;
    }

    // ── Helpers ──────────────────────────────────────────────────────────
    private static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }

    /// <summary>Asigna sprite al icono, ocultándolo si no hay (no-monstruo o sin arte).</summary>
    private static void SetIcon(Image img, Sprite sprite)
    {
        if (img == null) return;
        img.sprite = sprite;
        img.enabled = sprite != null;
    }

    /// <summary>Pips de nivel: el icono repetido tantas veces como estrellas (8 ⇒ 8 iconos).</summary>
    private void RebuildLevelIcons(int level)
    {
        if (levelIconsRow == null) return;

        for (int i = levelIconsRow.childCount - 1; i >= 0; i--)
            Destroy(levelIconsRow.GetChild(i).gameObject);

        level = Mathf.Clamp(level, 0, 12);
        const float size = 20f, gap = 4f;
        for (int i = 0; i < level; i++)
        {
            var go = new GameObject("Pip", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(levelIconsRow, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(i * (size + gap), 0f);

            var img = go.GetComponent<Image>();
            img.sprite = levelIconSprite;
            img.color = levelIconColor;
            img.raycastTarget = false;
        }
    }

    private static string RarityName(CardRarity r) => r switch
    {
        CardRarity.Legendary => "Legendaria",
        CardRarity.Epic => "Épica",
        CardRarity.Rare => "Rara",
        _ => "Común"
    };

    private static string FirstSource(LibraryEntry entry)
    {
        var vis = LibraryQueryService.GetVisibleSources(entry.card, entry.state);
        if (vis.Count == 0) return "Información oculta";
        var s = vis[0];
        if (!string.IsNullOrEmpty(s.description)) return s.description;
        if (s.sourceType == CardSourceType.Drop || s.sourceType == CardSourceType.Trade)
        {
            var opp = LibraryCatalog.GetOpponent(s.opponentId);
            return opp != null ? opp.opponentName : $"Oponente #{s.opponentId}";
        }
        return s.sourceType.ToString();
    }

    // Tasa de obtención de ejemplo (placeholder derivado de la rareza).
    private static string DropRate(CardData card) => card.rarity switch
    {
        CardRarity.Legendary => "4.2%",
        CardRarity.Epic => "9.5%",
        CardRarity.Rare => "18%",
        _ => "35%"
    };
}
