using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Constructor de Deck (rediseño "Neo-Kemet", réplica del mockup). Tres columnas:
///   • COLECCIÓN (izq): búsqueda + filtros (tipo/atributo/nivel/rareza/orden) +
///     pestañas de categoría + grilla paginada de cartas poseídas.
///   • INSPECTOR (centro): carta grande seleccionada, stepper de copias y botones
///     Añadir/Eliminar. Las pestañas inferiores cambian este panel por Curva de
///     niveles, Simular mano o Fusiones.
///   • MAZO (der): selector de mazo (varios mazos), nombre editable, lista de
///     cartas, contadores, dona de distribución y promedios.
///
/// Reglas: exactamente 40 cartas para guardar como válido; no se pueden añadir
/// más de 40; máximo 3 copias por carta (y nunca más de las que posees).
///
/// No crea la UI: usa referencias que cablea <see cref="DeckBuilderBuilder"/>.
/// </summary>
public class DeckBuilderController : MonoBehaviour
{
    // ── Barra superior ───────────────────────────────────────────────────
    [Header("Barra superior")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button newButton;
    [SerializeField] private Button exportButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button tabConstructorButton;
    [SerializeField] private Button tabMisDecksButton;
    [SerializeField] private GameObject constructorView;
    [SerializeField] private GameObject misDecksView;
    [SerializeField] private RectTransform misDecksListContent;

    // ── Colección (izquierda) ────────────────────────────────────────────
    [Header("Colección")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private TextMeshProUGUI collectionCountText;
    [SerializeField] private Button catAllButton;
    [SerializeField] private Button catMonsterButton;
    [SerializeField] private Button catSpellButton;
    [SerializeField] private Button catTrapButton;
    [SerializeField] private Button catEquipButton;
    [SerializeField] private TMP_Dropdown typeDropdown;
    [SerializeField] private TMP_Dropdown attributeDropdown;
    [SerializeField] private TMP_Dropdown levelDropdown;
    [SerializeField] private TMP_Dropdown rarityDropdown;
    [SerializeField] private TMP_Dropdown sortDropdown;
    [SerializeField] private Button clearFiltersButton;
    [SerializeField] private RectTransform collectionGridContent;
    [SerializeField] private CollectionCardView collectionTemplate;
    [SerializeField] private RectTransform pageButtonsContent;
    [SerializeField] private Button pageButtonTemplate;
    [SerializeField] private Button prevPageButton;
    [SerializeField] private Button nextPageButton;

    // ── Inspector central (Estadísticas) ─────────────────────────────────
    [Header("Inspector central")]
    [SerializeField] private CardDisplay previewCard;
    [SerializeField] private TextMeshProUGUI previewNameText;
    [SerializeField] private TextMeshProUGUI previewCategoryText;
    [SerializeField] private TextMeshProUGUI previewDescText;
    [SerializeField] private TextMeshProUGUI previewAtkText;
    [SerializeField] private TextMeshProUGUI previewDefText;
    [SerializeField] private TextMeshProUGUI ownedCountText;
    [SerializeField] private TextMeshProUGUI copiesInDeckText;
    [SerializeField] private Button stepMinusButton;
    [SerializeField] private Button stepPlusButton;
    [SerializeField] private Button addButton;
    [SerializeField] private Button removeButton;
    [SerializeField] private Button favoriteButton;
    [SerializeField] private Image favoriteIcon;

    // ── Pestañas inferiores + paneles del centro ─────────────────────────
    [Header("Pestañas inferiores")]
    [SerializeField] private Button tabStatsButton;
    [SerializeField] private Button tabCurveButton;
    [SerializeField] private Button tabHandButton;
    [SerializeField] private Button tabFusionButton;
    [SerializeField] private GameObject centerInspectorPanel;
    [SerializeField] private GameObject centerCurvePanel;
    [SerializeField] private GameObject centerHandPanel;
    [SerializeField] private GameObject centerFusionPanel;
    [SerializeField] private RectTransform curveContent;
    [SerializeField] private RectTransform handContent;
    [SerializeField] private RectTransform fusionContent;
    [SerializeField] private Button redrawHandButton;

    // ── Mazo (derecha) ────────────────────────────────────────────────────
    [Header("Mazo")]
    [SerializeField] private TMP_InputField deckNameInput;
    [SerializeField] private TMP_Dropdown deckSlotDropdown;
    [SerializeField] private Button deleteDeckButton;
    [SerializeField] private TextMeshProUGUI deckHeaderCountText;
    [SerializeField] private TextMeshProUGUI monsterCountText;
    [SerializeField] private TextMeshProUGUI spellCountText;
    [SerializeField] private TextMeshProUGUI trapCountText;
    [SerializeField] private RectTransform deckListContent;
    [SerializeField] private DeckCardView deckRowTemplate;
    [SerializeField] private Button extraButton;
    [SerializeField] private UIDonutChart donut;
    [SerializeField] private TextMeshProUGUI distMonstersText;
    [SerializeField] private TextMeshProUGUI distSpellsText;
    [SerializeField] private TextMeshProUGUI distTrapsText;
    [SerializeField] private TextMeshProUGUI avgCostText;
    [SerializeField] private TextMeshProUGUI avgAtkText;
    [SerializeField] private TextMeshProUGUI avgDefText;

    // ── Ajustes ──────────────────────────────────────────────────────────
    [Header("Ajustes")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button settingsCloseButton;
    [SerializeField] private Button resetDeckButton;

    // ── Recursos ─────────────────────────────────────────────────────────
    [Header("Recursos")]
    [SerializeField] private Sprite levelStarSprite;

    [Header("Colores de pestaña")]
    [SerializeField] private Color tabActiveColor = new Color(0.63f, 0.40f, 0.95f);
    [SerializeField] private Color tabIdleColor   = new Color(0.11f, 0.10f, 0.15f, 0.85f);

    // ── Estado ───────────────────────────────────────────────────────────
    private enum Category { All, Monster, Spell, Trap, Equip }
    private enum CenterTab { Stats, Curve, Hand, Fusion }

    private static readonly Color GoldCol   = new Color(0.93f, 0.75f, 0.33f);
    private static readonly Color EmeraldCol = new Color(0.16f, 0.85f, 0.66f);
    private static readonly Color FuchsiaCol = new Color(1.00f, 0.31f, 0.64f);
    private static readonly Color MutedCol   = new Color(0.58f, 0.55f, 0.50f);
    private static readonly Color BrightCol  = new Color(0.93f, 0.90f, 0.83f);

    private readonly Dictionary<int, int> _deck = new();   // cardId → copias en el mazo
    private string _search = "";
    private Category _category = Category.All;
    private int _typeFilter = -1;      // índice de MonsterType, o -1 = todos
    private int _attrFilter = -1;      // índice de CardAttribute, o -1 = todos
    private int _levelFilter = 0;      // nivel exacto, o 0 = todos
    private int _rarityFilter = -1;    // índice de CardRarity, o -1 = todas
    private int _sort = 0;
    private int _page = 0;
    private int _selectedCardId = -1;
    private CenterTab _centerTab = CenterTab.Stats;
    private bool _unsaved = false;

    private FusionDatabase _fusionDb;
    private readonly List<GameObject> _spawnedCollection = new();
    private readonly List<GameObject> _spawnedDeck = new();
    private readonly List<GameObject> _spawnedPages = new();
    private List<CardData> _filtered = new();

    private const int PageSize = 12;

    private static readonly MonsterType[] MonsterTypes = (MonsterType[])Enum.GetValues(typeof(MonsterType));
    private static readonly CardAttribute[] Attributes = (CardAttribute[])Enum.GetValues(typeof(CardAttribute));

    private int DeckTotal => _deck.Values.Sum();

    // ═══════════════════════════════════════════════════════════════════════
    //  Ciclo de vida
    // ═══════════════════════════════════════════════════════════════════════
    void Start()
    {
        GameNavigator.EnsureExists();
        PlayerCollection.EnsureExists();
        LibraryCatalog.EnsureLoaded();

        var dbs = Resources.LoadAll<FusionDatabase>("Fusions");
        _fusionDb = dbs != null && dbs.Length > 0 ? dbs[0] : null;

        BuildFilterDropdowns();
        LoadDeckFromStore();

        // Barra superior.
        Wire(backButton, Back);
        Wire(saveButton, Save);
        Wire(newButton, NewDeck);
        Wire(exportButton, ExportDeck);
        Wire(settingsButton, () => ToggleSettings(true));
        Wire(tabConstructorButton, () => ShowMisDecks(false));
        Wire(tabMisDecksButton, () => ShowMisDecks(true));

        // Colección.
        if (searchInput != null) searchInput.onValueChanged.AddListener(OnSearchChanged);
        WireCategory(catAllButton, Category.All);
        WireCategory(catMonsterButton, Category.Monster);
        WireCategory(catSpellButton, Category.Spell);
        WireCategory(catTrapButton, Category.Trap);
        WireCategory(catEquipButton, Category.Equip);
        if (typeDropdown != null) typeDropdown.onValueChanged.AddListener(v => { _typeFilter = v - 1; _page = 0; RefreshCollection(); });
        if (attributeDropdown != null) attributeDropdown.onValueChanged.AddListener(v => { _attrFilter = v - 1; _page = 0; RefreshCollection(); });
        if (levelDropdown != null) levelDropdown.onValueChanged.AddListener(v => { _levelFilter = v; _page = 0; RefreshCollection(); });
        if (rarityDropdown != null) rarityDropdown.onValueChanged.AddListener(v => { _rarityFilter = v - 1; _page = 0; RefreshCollection(); });
        if (sortDropdown != null) sortDropdown.onValueChanged.AddListener(v => { _sort = v; _page = 0; RefreshCollection(); });
        Wire(clearFiltersButton, ClearFilters);
        Wire(prevPageButton, () => ChangePage(_page - 1));
        Wire(nextPageButton, () => ChangePage(_page + 1));

        // Inspector.
        Wire(addButton, () => Add(_selectedCardId));
        Wire(removeButton, () => Remove(_selectedCardId));
        Wire(stepPlusButton, () => Add(_selectedCardId));
        Wire(stepMinusButton, () => Remove(_selectedCardId));
        Wire(favoriteButton, ToggleFavorite);

        // Pestañas inferiores.
        Wire(tabStatsButton, () => SetCenterTab(CenterTab.Stats));
        Wire(tabCurveButton, () => SetCenterTab(CenterTab.Curve));
        Wire(tabHandButton, () => SetCenterTab(CenterTab.Hand));
        Wire(tabFusionButton, () => SetCenterTab(CenterTab.Fusion));
        Wire(redrawHandButton, RefreshHand);

        // Mazo.
        if (deckNameInput != null) deckNameInput.onEndEdit.AddListener(OnDeckNameEdited);
        if (deckSlotDropdown != null) deckSlotDropdown.onValueChanged.AddListener(OnDeckSlotChanged);
        Wire(deleteDeckButton, DeleteDeck);
        Wire(extraButton, () => SetStatus("Este modo no usa Extra Deck.", warn: true));

        // Ajustes.
        Wire(settingsCloseButton, () => ToggleSettings(false));
        Wire(resetDeckButton, ResetDeck);

        // Plantillas fuera de vista.
        if (collectionTemplate != null) collectionTemplate.gameObject.SetActive(false);
        if (deckRowTemplate != null) deckRowTemplate.gameObject.SetActive(false);
        if (pageButtonTemplate != null) pageButtonTemplate.gameObject.SetActive(false);
        ToggleSettings(false);

        RefreshDeckSlotDropdown();
        ShowMisDecks(false);
        SetCenterTab(CenterTab.Stats);
        HighlightCategory();
        RefreshAll();
        AutoSelectFirst();
    }

    /// <summary>Selecciona la primera carta de la colección al abrir, para que el
    /// inspector central no arranque vacío.</summary>
    private void AutoSelectFirst()
    {
        if (_selectedCardId < 0 && _filtered.Count > 0)
            SelectCard(_filtered[0].cardId);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null && settingsPanel.activeSelf) { ToggleSettings(false); return; }
            Back();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Mazos (multi-deck)
    // ═══════════════════════════════════════════════════════════════════════
    private void LoadDeckFromStore()
    {
        _deck.Clear();
        foreach (var id in DeckLibrary.Active.cardIds)
        {
            _deck.TryGetValue(id, out int c);
            _deck[id] = c + 1;
        }
        _unsaved = false;
        if (deckNameInput != null) deckNameInput.SetTextWithoutNotify(DeckLibrary.Active.name);
    }

    private void RefreshDeckSlotDropdown()
    {
        if (deckSlotDropdown == null) return;
        deckSlotDropdown.onValueChanged.RemoveListener(OnDeckSlotChanged);
        deckSlotDropdown.ClearOptions();
        deckSlotDropdown.AddOptions(DeckLibrary.Decks.Select(d => d.name).ToList());
        deckSlotDropdown.SetValueWithoutNotify(DeckLibrary.ActiveIndex);
        deckSlotDropdown.onValueChanged.AddListener(OnDeckSlotChanged);
    }

    private void OnDeckSlotChanged(int index)
    {
        if (_unsaved) SetStatus("Se descartaron cambios no guardados del mazo anterior.", warn: true);
        DeckLibrary.SetActive(index);
        LoadDeckFromStore();
        _selectedCardId = -1;
        _page = 0;
        RefreshAll();
    }

    private void NewDeck()
    {
        DeckLibrary.CreateDeck();
        LoadDeckFromStore();
        _selectedCardId = -1;
        _page = 0;
        RefreshDeckSlotDropdown();
        RefreshAll();
        SetStatus($"Nuevo mazo creado: «{DeckLibrary.Active.name}».", warn: false);
    }

    private void DeleteDeck()
    {
        string gone = DeckLibrary.Active.name;
        DeckLibrary.DeleteDeck(DeckLibrary.ActiveIndex);
        LoadDeckFromStore();
        _selectedCardId = -1;
        _page = 0;
        RefreshDeckSlotDropdown();
        RefreshAll();
        SetStatus($"Mazo «{gone}» eliminado.", warn: true);
    }

    private void OnDeckNameEdited(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { deckNameInput.SetTextWithoutNotify(DeckLibrary.Active.name); return; }
        DeckLibrary.RenameActive(value);
        RefreshDeckSlotDropdown();
    }

    private void Save()
    {
        int total = DeckTotal;
        if (total != PlayerDeck.RequiredSize)
        {
            SetStatus($"El mazo debe tener exactamente {PlayerDeck.RequiredSize} cartas (tienes {total}). No se guarda.", warn: true);
            return;
        }
        // Salvaguarda de las 3 copias (el añadir ya lo impide, pero validamos igual).
        if (_deck.Any(kv => kv.Value > PlayerDeck.MaxCopiesPerCard))
        {
            SetStatus($"Hay cartas con más de {PlayerDeck.MaxCopiesPerCard} copias. No se guarda.", warn: true);
            return;
        }

        var flat = new List<int>();
        foreach (var kv in _deck)
            for (int i = 0; i < kv.Value; i++) flat.Add(kv.Key);

        PlayerDeck.Save(flat);
        _unsaved = false;
        SetStatus($"¡Mazo «{DeckLibrary.Active.name}» guardado! (40/40)", warn: false);
    }

    private void ResetDeck()
    {
        _deck.Clear();
        _unsaved = true;
        _selectedCardId = -1;
        ToggleSettings(false);
        RefreshAll();
        SetStatus("Mazo vaciado (no se guarda hasta pulsar Guardar).", warn: true);
    }

    private void ExportDeck()
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "exports");
            Directory.CreateDirectory(dir);
            string safe = string.Concat(DeckLibrary.Active.name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            if (string.IsNullOrWhiteSpace(safe)) safe = "mazo";
            string path = Path.Combine(dir, safe + ".txt");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# {DeckLibrary.Active.name} ({DeckTotal}/{PlayerDeck.RequiredSize})");
            foreach (var kv in OrderedDeck())
            {
                var card = LibraryCatalog.GetCard(kv.Key);
                sb.AppendLine($"{kv.Value}x [{kv.Key}] {(card != null ? card.cardName : "?")}");
            }
            File.WriteAllText(path, sb.ToString());
            SetStatus($"Mazo exportado a: {path}", warn: false);
            Debug.Log($"DeckBuilder: mazo exportado a {path}");
        }
        catch (Exception e)
        {
            SetStatus("No se pudo exportar el mazo (ver consola).", warn: true);
            Debug.LogError($"DeckBuilder: error exportando. {e}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Reglas de añadir / quitar
    // ═══════════════════════════════════════════════════════════════════════
    private int OwnedCopies(int cardId)
    {
        var pc = PlayerCollection.Instance;
        return pc != null ? pc.GetCopies(cardId) : 0;
    }

    private int InDeck(int cardId) => _deck.TryGetValue(cardId, out int c) ? c : 0;

    /// <summary>Tope de copias de una carta = mín(poseídas, 3).</summary>
    private int CopyCap(int cardId) => Mathf.Min(OwnedCopies(cardId), PlayerDeck.MaxCopiesPerCard);

    private void Add(int cardId)
    {
        if (cardId < 0) return;
        if (DeckTotal >= PlayerDeck.MaxSize) { SetStatus($"El mazo ya tiene {PlayerDeck.MaxSize} cartas.", warn: true); return; }

        int cap = CopyCap(cardId);
        if (InDeck(cardId) >= cap)
        {
            SetStatus(OwnedCopies(cardId) < PlayerDeck.MaxCopiesPerCard
                ? $"Solo posees {OwnedCopies(cardId)} copia(s) de esta carta."
                : $"Máximo {PlayerDeck.MaxCopiesPerCard} copias por carta.", warn: true);
            return;
        }

        _deck[cardId] = InDeck(cardId) + 1;
        _unsaved = true;
        RefreshAll();
    }

    private void Remove(int cardId)
    {
        if (cardId < 0) return;
        int c = InDeck(cardId);
        if (c <= 0) return;
        if (c - 1 <= 0) _deck.Remove(cardId);
        else _deck[cardId] = c - 1;
        _unsaved = true;
        RefreshAll();
    }

    private void ToggleFavorite()
    {
        if (_selectedCardId < 0) return;
        PlayerCollection.Instance?.ToggleFavorite(_selectedCardId);
        RefreshInspector();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Filtros / categorías / orden
    // ═══════════════════════════════════════════════════════════════════════
    private void OnSearchChanged(string value) { _search = value; _page = 0; RefreshCollection(); }

    private void SetCategory(Category cat) { _category = cat; _page = 0; HighlightCategory(); RefreshCollection(); }

    private void WireCategory(Button b, Category cat)
    {
        if (b != null) b.onClick.AddListener(() => SetCategory(cat));
    }

    private void HighlightCategory()
    {
        Tint(catAllButton, _category == Category.All);
        Tint(catMonsterButton, _category == Category.Monster);
        Tint(catSpellButton, _category == Category.Spell);
        Tint(catTrapButton, _category == Category.Trap);
        Tint(catEquipButton, _category == Category.Equip);
    }

    private void Tint(Button b, bool active)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>();
        if (img != null) img.color = active ? tabActiveColor : tabIdleColor;
    }

    private void ClearFilters()
    {
        _search = ""; _typeFilter = -1; _attrFilter = -1; _levelFilter = 0; _rarityFilter = -1; _sort = 0; _page = 0;
        if (searchInput != null) searchInput.SetTextWithoutNotify("");
        typeDropdown?.SetValueWithoutNotify(0);
        attributeDropdown?.SetValueWithoutNotify(0);
        levelDropdown?.SetValueWithoutNotify(0);
        rarityDropdown?.SetValueWithoutNotify(0);
        sortDropdown?.SetValueWithoutNotify(0);
        RefreshCollection();
    }

    private void BuildFilterDropdowns()
    {
        if (typeDropdown != null)
        {
            typeDropdown.ClearOptions();
            var opts = new List<string> { "Todos los tipos" };
            opts.AddRange(MonsterTypes.Select(t => DeckStats.TypeName(t)));
            typeDropdown.AddOptions(opts);
        }
        if (attributeDropdown != null)
        {
            attributeDropdown.ClearOptions();
            var opts = new List<string> { "Todos los atributos" };
            opts.AddRange(Attributes.Select(AttrName));
            attributeDropdown.AddOptions(opts);
        }
        if (levelDropdown != null)
        {
            levelDropdown.ClearOptions();
            var opts = new List<string> { "Todos los niveles" };
            for (int i = 1; i <= 12; i++) opts.Add($"Nivel {i}");
            levelDropdown.AddOptions(opts);
        }
        if (rarityDropdown != null)
        {
            rarityDropdown.ClearOptions();
            rarityDropdown.AddOptions(new List<string> { "Todas las rarezas", "Común", "Rara", "Épica", "Legendaria" });
        }
        if (sortDropdown != null)
        {
            sortDropdown.ClearOptions();
            sortDropdown.AddOptions(new List<string> { "Nombre (A-Z)", "ATK ▼", "DEF ▼", "Nivel ▼", "Rareza ▼" });
        }
    }

    private IEnumerable<CardData> QueryCollection()
    {
        var pc = PlayerCollection.Instance;
        IEnumerable<CardData> cards = LibraryCatalog.AllCards.Where(c => pc != null && pc.IsOwned(c.cardId));

        if (!string.IsNullOrWhiteSpace(_search))
        {
            string s = _search.ToLowerInvariant();
            bool isId = int.TryParse(_search, out int idv);
            cards = cards.Where(c => c.cardName.ToLowerInvariant().Contains(s) || (isId && c.cardId == idv));
        }

        cards = _category switch
        {
            Category.Monster => cards.Where(c => c.IsMonster),
            Category.Spell   => cards.Where(c => c.IsSpell),
            Category.Trap    => cards.Where(c => c.IsTrap),
            Category.Equip   => cards.Where(c => c.IsEquip),
            _                => cards
        };

        if (_typeFilter >= 0)   cards = cards.Where(c => c.IsMonster && (int)c.monsterType == _typeFilter);
        if (_attrFilter >= 0)   cards = cards.Where(c => c.IsMonster && (int)c.attribute == _attrFilter);
        if (_levelFilter > 0)   cards = cards.Where(c => c.IsMonster && c.stars == _levelFilter);
        if (_rarityFilter >= 0) cards = cards.Where(c => (int)c.rarity == _rarityFilter);

        cards = _sort switch
        {
            1 => cards.OrderByDescending(c => c.baseAtk).ThenBy(c => c.cardName),
            2 => cards.OrderByDescending(c => c.baseDef).ThenBy(c => c.cardName),
            3 => cards.OrderByDescending(c => c.stars).ThenBy(c => c.cardName),
            4 => cards.OrderByDescending(c => (int)c.rarity).ThenBy(c => c.cardName),
            _ => cards.OrderBy(c => c.cardName)
        };
        return cards;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Refresco general
    // ═══════════════════════════════════════════════════════════════════════
    private void RefreshAll()
    {
        RefreshCollection();
        RefreshDeckList();
        RefreshDeckStats();
        RefreshInspector();
        RefreshCenterTab();
    }

    // ── Colección + paginación ───────────────────────────────────────────
    private void RefreshCollection()
    {
        _filtered = QueryCollection().ToList();

        int pages = Mathf.Max(1, Mathf.CeilToInt(_filtered.Count / (float)PageSize));
        _page = Mathf.Clamp(_page, 0, pages - 1);

        if (collectionCountText != null)
            collectionCountText.text = $"{_filtered.Count} cartas";

        foreach (var go in _spawnedCollection) Destroy(go);
        _spawnedCollection.Clear();

        if (collectionTemplate != null && collectionGridContent != null)
        {
            int start = _page * PageSize;
            int end = Mathf.Min(start + PageSize, _filtered.Count);
            for (int i = start; i < end; i++)
            {
                var card = _filtered[i];
                var go = Instantiate(collectionTemplate.gameObject, collectionGridContent);
                go.SetActive(true);
                var view = go.GetComponent<CollectionCardView>();
                int id = card.cardId;
                view.Setup(card, OwnedCopies(id), InDeck(id), CopyCap(id), id == _selectedCardId, () => SelectCard(id));
                _spawnedCollection.Add(go);
            }
        }

        RefreshPageBar(pages);
    }

    private void RefreshPageBar(int pages)
    {
        foreach (var go in _spawnedPages) Destroy(go);
        _spawnedPages.Clear();

        if (prevPageButton != null) prevPageButton.interactable = _page > 0;
        if (nextPageButton != null) nextPageButton.interactable = _page < pages - 1;

        if (pageButtonTemplate == null || pageButtonsContent == null) return;

        foreach (int p in PageWindow(_page, pages))
        {
            var go = Instantiate(pageButtonTemplate.gameObject, pageButtonsContent);
            go.SetActive(true);
            var btn = go.GetComponent<Button>();
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (p < 0)
            {
                if (label != null) label.text = "…";
                btn.interactable = false;
            }
            else
            {
                int page = p;
                if (label != null) label.text = (p + 1).ToString();
                var img = btn.GetComponent<Image>();
                if (img != null) img.color = p == _page ? tabActiveColor : tabIdleColor;
                btn.onClick.AddListener(() => ChangePage(page));
            }
            _spawnedPages.Add(go);
        }
    }

    /// <summary>Ventana de páginas estilo "1 2 3 … 21" alrededor de la actual.</summary>
    private static IEnumerable<int> PageWindow(int current, int pages)
    {
        var set = new SortedSet<int>();
        set.Add(0);
        set.Add(pages - 1);
        for (int i = current - 1; i <= current + 1; i++)
            if (i >= 0 && i < pages) set.Add(i);

        int prev = -2;
        foreach (int p in set)
        {
            if (p - prev > 1) yield return -1; // separador "…"
            yield return p;
            prev = p;
        }
    }

    private void ChangePage(int page)
    {
        _page = page;
        RefreshCollection();
    }

    // ── Inspector central ────────────────────────────────────────────────
    private void SelectCard(int cardId)
    {
        _selectedCardId = cardId;
        // Marca visualmente sin re-instanciar toda la grilla.
        foreach (var go in _spawnedCollection)
        {
            var v = go.GetComponent<CollectionCardView>();
            if (v != null) v.SetSelected(v.CardId == cardId);
        }
        foreach (var go in _spawnedDeck)
        {
            var v = go.GetComponent<DeckCardView>();
            if (v != null) v.SetSelected(v.CardId == cardId);
        }
        RefreshInspector();
    }

    private void RefreshInspector()
    {
        var card = _selectedCardId >= 0 ? LibraryCatalog.GetCard(_selectedCardId) : null;

        if (card == null)
        {
            if (previewCard != null) previewCard.gameObject.SetActive(false);
            Set(previewNameText, "Selecciona una carta");
            Set(previewCategoryText, "");
            Set(previewDescText, "Elige una carta de la colección o del mazo para inspeccionarla y añadirla.");
            Set(previewAtkText, "—");
            Set(previewDefText, "—");
            Set(ownedCountText, "0");
            Set(copiesInDeckText, "0");
            SetInteractable(addButton, false);
            SetInteractable(removeButton, false);
            SetInteractable(stepPlusButton, false);
            SetInteractable(stepMinusButton, false);
            SetInteractable(favoriteButton, false);
            return;
        }

        if (previewCard != null)
        {
            previewCard.gameObject.SetActive(true);
            previewCard.Setup(card);
            previewCard.SetPosition(CardPosition.FaceUpAttack);
            previewCard.SetHoloIntensityScale(0.3f);
        }

        Set(previewNameText, card.cardName);
        Set(previewCategoryText, CategoryLine(card));
        Set(previewDescText, card.DisplayDescription);
        if (card.IsMonster)
        {
            Set(previewAtkText, $"ATK {card.baseAtk}");
            Set(previewDefText, $"DEF {card.baseDef}");
        }
        else
        {
            Set(previewAtkText, "");
            Set(previewDefText, "");
        }

        int owned = OwnedCopies(_selectedCardId);
        int inDeck = InDeck(_selectedCardId);
        int cap = CopyCap(_selectedCardId);
        Set(ownedCountText, owned.ToString());
        Set(copiesInDeckText, inDeck.ToString());

        SetInteractable(addButton, inDeck < cap && DeckTotal < PlayerDeck.MaxSize);
        SetInteractable(stepPlusButton, inDeck < cap && DeckTotal < PlayerDeck.MaxSize);
        SetInteractable(removeButton, inDeck > 0);
        SetInteractable(stepMinusButton, inDeck > 0);
        SetInteractable(favoriteButton, true);

        if (favoriteIcon != null)
        {
            bool fav = PlayerCollection.Instance != null && PlayerCollection.Instance.IsFavorite(_selectedCardId);
            favoriteIcon.color = fav ? GoldCol : new Color(1f, 1f, 1f, 0.25f);
        }
    }

    private static string CategoryLine(CardData card)
    {
        if (card.IsMonster)
        {
            string type = DeckStats.TypeName(card.monsterType);
            return $"[ {type} / Nivel {card.stars} ]";
        }
        return $"[ {card.CategoryLabel} ]";
    }

    // ── Panel del mazo (derecha) ─────────────────────────────────────────
    private IEnumerable<KeyValuePair<int, int>> OrderedDeck()
    {
        return _deck.Where(kv => kv.Value > 0)
                    .Select(kv => (kv, card: LibraryCatalog.GetCard(kv.Key)))
                    .Where(x => x.card != null)
                    .OrderBy(x => CategoryRank(x.card))
                    .ThenByDescending(x => x.card.IsMonster ? x.card.stars : 0)
                    .ThenBy(x => x.card.cardName)
                    .Select(x => x.kv);
    }

    private static int CategoryRank(CardData c)
    {
        if (c.IsMonster) return 0;
        if (c.IsSpell) return 1;
        if (c.IsEquip) return 2;
        if (c.IsRitual) return 3;
        if (c.IsSpecial) return 4;
        return 5; // trap
    }

    private void RefreshDeckList()
    {
        foreach (var go in _spawnedDeck) Destroy(go);
        _spawnedDeck.Clear();
        if (deckRowTemplate == null || deckListContent == null) return;

        foreach (var kv in OrderedDeck())
        {
            var card = LibraryCatalog.GetCard(kv.Key);
            if (card == null) continue;
            var go = Instantiate(deckRowTemplate.gameObject, deckListContent);
            go.SetActive(true);
            var view = go.GetComponent<DeckCardView>();
            int id = kv.Key;
            view.Setup(card, kv.Value, id == _selectedCardId, () => SelectCard(id));
            _spawnedDeck.Add(go);
        }
    }

    private void RefreshDeckStats()
    {
        var s = DeckStats.Compute(_deck, _fusionDb);
        int total = s.total;

        if (deckHeaderCountText != null)
        {
            deckHeaderCountText.text = $"{total} / {PlayerDeck.RequiredSize}";
            deckHeaderCountText.color = total == PlayerDeck.RequiredSize ? EmeraldCol
                                      : total > PlayerDeck.RequiredSize ? FuchsiaCol : BrightCol;
        }

        int spellLike = s.SpellLike;
        Set(monsterCountText, s.monsters.ToString());
        Set(spellCountText, spellLike.ToString());
        Set(trapCountText, s.traps.ToString());

        if (donut != null)
            donut.SetSegments(new[]
            {
                ((float)s.monsters, GoldCol),
                ((float)spellLike, EmeraldCol),
                ((float)s.traps,   FuchsiaCol),
            });

        float pTotal = Mathf.Max(1, total);
        Set(distMonstersText, $"{Mathf.RoundToInt(100f * s.monsters / pTotal)}%");
        Set(distSpellsText,   $"{Mathf.RoundToInt(100f * spellLike / pTotal)}%");
        Set(distTrapsText,    $"{Mathf.RoundToInt(100f * s.traps / pTotal)}%");

        Set(avgCostText, s.avgCost.ToString("0.0"));
        Set(avgAtkText, s.avgAtk.ToString());
        Set(avgDefText, s.avgDef.ToString());
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Pestañas superiores (Mis Decks / Constructor)
    // ═══════════════════════════════════════════════════════════════════════
    private void ShowMisDecks(bool show)
    {
        if (constructorView != null) constructorView.SetActive(!show);
        if (misDecksView != null) misDecksView.SetActive(show);
        Tint(tabConstructorButton, !show);
        Tint(tabMisDecksButton, show);
        if (show) RefreshMisDecks();
    }

    private void RefreshMisDecks()
    {
        if (misDecksListContent == null) return;
        for (int i = misDecksListContent.childCount - 1; i >= 0; i--)
            Destroy(misDecksListContent.GetChild(i).gameObject);

        var decks = DeckLibrary.Decks;
        for (int i = 0; i < decks.Count; i++)
            BuildDeckSlotCard(i, decks[i]);
    }

    private void BuildDeckSlotCard(int index, DeckLibrary.Deck deck)
    {
        var s = SummaryOf(deck);

        var rowGO = new GameObject("DeckSlot_" + index, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)rowGO.transform;
        rt.SetParent(misDecksListContent, false);
        var le = rowGO.AddComponent<LayoutElement>(); le.minHeight = 92; le.preferredHeight = 92;
        rowGO.GetComponent<Image>().color = index == DeckLibrary.ActiveIndex ? new Color(0.16f, 0.14f, 0.24f, 0.95f)
                                                                             : new Color(0.09f, 0.09f, 0.14f, 0.9f);
        int captured = index;
        rowGO.GetComponent<Button>().onClick.AddListener(() =>
        {
            OnDeckSlotChanged(captured);
            RefreshDeckSlotDropdown();
            ShowMisDecks(false);
        });

        var name = NewText(rt, "Name", deck.name, 26, GoldCol, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(name, new Vector2(0f, 0.5f), new Vector2(0.7f, 1f), new Vector2(18, 0), new Vector2(0, -6));

        string valid = s.total == PlayerDeck.RequiredSize ? "✔ Listo" : $"{s.total}/40";
        var count = NewText(rt, "Count", valid, 20,
                            s.total == PlayerDeck.RequiredSize ? EmeraldCol : MutedCol,
                            TextAlignmentOptions.Right, FontStyles.Bold);
        Place(count, new Vector2(0.6f, 0.5f), new Vector2(1f, 1f), new Vector2(0, 0), new Vector2(-18, -6));

        var detail = NewText(rt, "Detail", $"Monstruos {s.monsters}   ·   Magias {s.SpellLike}   ·   Trampas {s.traps}",
                             17, BrightCol, TextAlignmentOptions.Left);
        Place(detail, new Vector2(0f, 0f), new Vector2(0.75f, 0.5f), new Vector2(18, 6), Vector2.zero);

        // Botón borrar (x).
        var delGO = new GameObject("Del", typeof(RectTransform), typeof(Image), typeof(Button));
        var delRT = (RectTransform)delGO.transform;
        delRT.SetParent(rt, false);
        delRT.anchorMin = delRT.anchorMax = new Vector2(1f, 0f);
        delRT.pivot = new Vector2(1f, 0f);
        delRT.sizeDelta = new Vector2(90, 34);
        delRT.anchoredPosition = new Vector2(-14, 10);
        delGO.GetComponent<Image>().color = new Color(0.4f, 0.12f, 0.16f, 0.9f);
        var delLbl = NewText(delRT, "L", "Borrar", 15, BrightCol, TextAlignmentOptions.Center);
        Stretch(delLbl);
        delGO.GetComponent<Button>().onClick.AddListener(() =>
        {
            DeckLibrary.SetActive(captured);
            LoadDeckFromStore();
            DeleteDeck();
            RefreshMisDecks();
        });
    }

    private DeckStats.Summary SummaryOf(DeckLibrary.Deck deck)
    {
        var map = new Dictionary<int, int>();
        foreach (var id in deck.cardIds)
        {
            map.TryGetValue(id, out int c);
            map[id] = c + 1;
        }
        return DeckStats.Compute(map, null);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Pestañas inferiores (Estadísticas / Curva / Mano / Fusiones)
    // ═══════════════════════════════════════════════════════════════════════
    private void SetCenterTab(CenterTab tab)
    {
        _centerTab = tab;
        if (centerInspectorPanel != null) centerInspectorPanel.SetActive(tab == CenterTab.Stats);
        if (centerCurvePanel != null) centerCurvePanel.SetActive(tab == CenterTab.Curve);
        if (centerHandPanel != null) centerHandPanel.SetActive(tab == CenterTab.Hand);
        if (centerFusionPanel != null) centerFusionPanel.SetActive(tab == CenterTab.Fusion);

        Tint(tabStatsButton, tab == CenterTab.Stats);
        Tint(tabCurveButton, tab == CenterTab.Curve);
        Tint(tabHandButton, tab == CenterTab.Hand);
        Tint(tabFusionButton, tab == CenterTab.Fusion);

        RefreshCenterTab();
    }

    private void RefreshCenterTab()
    {
        switch (_centerTab)
        {
            case CenterTab.Curve: RefreshCurve(); break;
            case CenterTab.Hand: RefreshHand(); break;
            case CenterTab.Fusion: RefreshFusions(); break;
        }
    }

    private void RefreshCurve()
    {
        if (curveContent == null) return;
        for (int i = curveContent.childCount - 1; i >= 0; i--)
            Destroy(curveContent.GetChild(i).gameObject);

        var s = DeckStats.Compute(_deck, null);
        int max = 1;
        for (int lvl = 1; lvl <= 12; lvl++) max = Mathf.Max(max, s.levelHistogram[lvl]);

        for (int lvl = 1; lvl <= 12; lvl++)
        {
            int count = s.levelHistogram[lvl];

            var col = new GameObject($"Lvl{lvl}", typeof(RectTransform), typeof(LayoutElement));
            var colRT = (RectTransform)col.transform;
            colRT.SetParent(curveContent, false);
            var le = col.AddComponent<LayoutElement>(); le.flexibleWidth = 1; le.minWidth = 20;

            // Barra (crece desde abajo).
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            var barRT = (RectTransform)bar.transform;
            barRT.SetParent(colRT, false);
            barRT.anchorMin = new Vector2(0.15f, 0.12f);
            barRT.anchorMax = new Vector2(0.85f, 0.12f + 0.78f * (count / (float)max));
            barRT.offsetMin = Vector2.zero; barRT.offsetMax = Vector2.zero;
            bar.GetComponent<Image>().color = count > 0 ? GoldCol : new Color(1, 1, 1, 0.08f);

            var num = NewText(colRT, "N", count.ToString(), 15, count > 0 ? BrightCol : MutedCol, TextAlignmentOptions.Center);
            Place(num, new Vector2(0f, 0.9f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var lbl = NewText(colRT, "L", lvl.ToString(), 14, MutedCol, TextAlignmentOptions.Center);
            Place(lbl, new Vector2(0f, 0f), new Vector2(1f, 0.1f), Vector2.zero, Vector2.zero);
        }
    }

    private void RefreshHand()
    {
        if (handContent == null) return;
        for (int i = handContent.childCount - 1; i >= 0; i--)
            Destroy(handContent.GetChild(i).gameObject);

        var pool = new List<int>();
        foreach (var kv in _deck)
            for (int i = 0; i < kv.Value; i++) pool.Add(kv.Key);

        if (pool.Count < 5)
        {
            var note = NewText(handContent, "Note",
                "Necesitas al menos 5 cartas en el mazo para simular una mano.",
                20, MutedCol, TextAlignmentOptions.Center);
            note.enableWordWrapping = true;
            Stretch(note);
            return;
        }

        // Baraja y roba 5.
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        for (int i = 0; i < 5; i++)
        {
            var card = LibraryCatalog.GetCard(pool[i]);
            if (card == null) continue;

            var tile = new GameObject("Hand_" + i, typeof(RectTransform), typeof(LayoutElement));
            var tileRT = (RectTransform)tile.transform;
            tileRT.SetParent(handContent, false);
            var le = tile.AddComponent<LayoutElement>(); le.flexibleWidth = 1; le.minWidth = 90;

            var img = new GameObject("Art", typeof(RectTransform), typeof(Image));
            var imgRT = (RectTransform)img.transform;
            imgRT.SetParent(tileRT, false);
            imgRT.anchorMin = new Vector2(0.05f, 0.16f);
            imgRT.anchorMax = new Vector2(0.95f, 0.98f);
            imgRT.offsetMin = Vector2.zero; imgRT.offsetMax = Vector2.zero;
            var artImg = img.GetComponent<Image>();
            artImg.sprite = card.artwork;
            artImg.preserveAspect = true;
            artImg.color = card.artwork != null ? Color.white : new Color(0.2f, 0.2f, 0.28f);

            var name = NewText(tileRT, "N", card.cardName, 13, BrightCol, TextAlignmentOptions.Center);
            name.enableWordWrapping = true;
            Place(name, new Vector2(0f, 0f), new Vector2(1f, 0.16f), Vector2.zero, Vector2.zero);
        }
    }

    private void RefreshFusions()
    {
        if (fusionContent == null) return;
        for (int i = fusionContent.childCount - 1; i >= 0; i--)
            Destroy(fusionContent.GetChild(i).gameObject);

        if (_fusionDb == null)
        {
            AddFusionRow("No hay base de datos de fusiones cargada.", MutedCol);
            return;
        }

        var distinct = _deck.Where(kv => kv.Value > 0)
                            .Select(kv => LibraryCatalog.GetCard(kv.Key))
                            .Where(c => c != null).ToList();

        int shown = 0;
        var seen = new HashSet<string>();
        for (int i = 0; i < distinct.Count && shown < 40; i++)
        {
            for (int j = i + 1; j < distinct.Count && shown < 40; j++)
            {
                var result = _fusionDb.TryFuse(distinct[i], distinct[j]);
                if (result == null) continue;
                string key = distinct[i].cardName + "|" + distinct[j].cardName + "|" + result.cardName;
                if (!seen.Add(key)) continue;
                AddFusionRow($"{distinct[i].cardName}  +  {distinct[j].cardName}   →   {result.cardName}", BrightCol);
                shown++;
            }
        }

        if (shown == 0)
            AddFusionRow("Ninguna fusión posible con las cartas del mazo actual.", MutedCol);
    }

    private void AddFusionRow(string text, Color color)
    {
        var rowGO = new GameObject("Fusion", typeof(RectTransform), typeof(LayoutElement));
        var rt = (RectTransform)rowGO.transform;
        rt.SetParent(fusionContent, false);
        var le = rowGO.AddComponent<LayoutElement>(); le.minHeight = 34; le.preferredHeight = 34;
        var t = NewText(rt, "T", text, 17, color, TextAlignmentOptions.Left);
        Stretch(t);
        t.margin = new Vector4(10, 0, 10, 0);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Ajustes / navegación / helpers
    // ═══════════════════════════════════════════════════════════════════════
    private void ToggleSettings(bool show)
    {
        if (settingsPanel != null) settingsPanel.SetActive(show);
    }

    private void Back()
    {
        if (_unsaved) SetStatus("Saliendo con cambios no guardados.", warn: true);
        GameNavigator.EnsureExists().ToMainMenu();
    }

    private void SetStatus(string message, bool warn)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = warn ? new Color(0.98f, 0.62f, 0.38f) : new Color(0.45f, 0.92f, 0.62f);
    }

    private static string AttrName(CardAttribute a) => a switch
    {
        CardAttribute.Dark => "Oscuridad",
        CardAttribute.Light => "Luz",
        CardAttribute.Fire => "Fuego",
        CardAttribute.Water => "Agua",
        CardAttribute.Earth => "Tierra",
        CardAttribute.Wind => "Viento",
        _ => a.ToString()
    };

    private static void Wire(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b != null) b.onClick.AddListener(action);
    }

    private static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }

    private static void SetInteractable(Selectable s, bool v) { if (s != null) s.interactable = v; }

    private static void Stretch(Component c)
    {
        var rt = (RectTransform)c.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void Place(Component c, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        var rt = (RectTransform)c.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    private static TMP_Text NewText(Transform parent, string name, string text, float size,
                                    Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.raycastTarget = false; t.enableWordWrapping = false;
        return t;
    }
}
