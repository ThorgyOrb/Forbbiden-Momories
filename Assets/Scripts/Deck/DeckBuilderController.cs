using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Constructor de Deck. A la izquierda muestra la COLECCIÓN (cartas que posees,
/// con búsqueda/filtro/orden); a la derecha el MAZO actual y sus estadísticas.
/// Añades/quitas copias respetando las que posees, y guardas cuando el mazo tiene
/// exactamente 40 cartas (regla del juego).
///
/// No crea la UI: usa referencias a objetos de la escena (las cablea
/// DeckBuilderBuilder). Las listas se generan en runtime clonando plantillas.
/// </summary>
public class DeckBuilderController : MonoBehaviour
{
    [Header("Chrome")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button backButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private TextMeshProUGUI deckCountText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Filtros")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Button sortButton;
    [SerializeField] private TextMeshProUGUI sortLabel;
    [SerializeField] private Button catAllButton;
    [SerializeField] private Button catMonsterButton;
    [SerializeField] private Button catSpellButton;
    [SerializeField] private Button catTrapButton;
    [SerializeField] private Button catEquipButton;

    [Header("Colección")]
    [SerializeField] private Transform collectionContent;
    [SerializeField] private CollectionCardView collectionTemplate;

    [Header("Mazo")]
    [SerializeField] private Transform deckContent;
    [SerializeField] private DeckCardView deckTemplate;

    [Header("Estadísticas")]
    [SerializeField] private TextMeshProUGUI statsText;

    private enum Category { All, Monster, Spell, Trap, Equip }
    private enum SortMode { NameAsc, AtkDesc, DefDesc }

    private readonly Dictionary<int, int> _deck = new();   // cardId → copias en el mazo
    private string _search = "";
    private Category _category = Category.All;
    private SortMode _sort = SortMode.NameAsc;
    private FusionDatabase _fusionDb;

    private readonly List<GameObject> _spawnedCollection = new();
    private readonly List<GameObject> _spawnedDeck = new();

    private int DeckTotal => _deck.Values.Sum();

    void Start()
    {
        GameNavigator.EnsureExists();
        PlayerCollection.EnsureExists();
        LibraryCatalog.EnsureLoaded();

        var dbs = Resources.LoadAll<FusionDatabase>("Fusions");
        _fusionDb = dbs != null && dbs.Length > 0 ? dbs[0] : null;

        LoadDeckFromStore();

        if (backButton != null) backButton.onClick.AddListener(Back);
        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (sortButton != null) sortButton.onClick.AddListener(CycleSort);
        if (searchInput != null) searchInput.onValueChanged.AddListener(OnSearchChanged);

        WireCategory(catAllButton, Category.All);
        WireCategory(catMonsterButton, Category.Monster);
        WireCategory(catSpellButton, Category.Spell);
        WireCategory(catTrapButton, Category.Trap);
        WireCategory(catEquipButton, Category.Equip);

        if (collectionTemplate != null) collectionTemplate.gameObject.SetActive(false);
        if (deckTemplate != null) deckTemplate.gameObject.SetActive(false);

        UpdateSortLabel();
        RefreshAll();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Back();
    }

    // ── Estado del mazo ──────────────────────────────────────────────────

    private void LoadDeckFromStore()
    {
        _deck.Clear();
        foreach (var id in PlayerDeck.GetCardIds())
        {
            _deck.TryGetValue(id, out int c);
            _deck[id] = c + 1;
        }
    }

    private int OwnedCopies(int cardId)
    {
        var pc = PlayerCollection.Instance;
        return pc != null ? pc.GetCopies(cardId) : 0;
    }

    private int InDeck(int cardId) => _deck.TryGetValue(cardId, out int c) ? c : 0;

    private void Add(int cardId)
    {
        if (DeckTotal >= PlayerDeck.RequiredSize) { SetStatus("El mazo ya tiene 40 cartas.", warn: true); return; }
        if (InDeck(cardId) >= OwnedCopies(cardId)) return;   // no más copias de las que posees

        _deck[cardId] = InDeck(cardId) + 1;
        RefreshAll();
    }

    private void Remove(int cardId)
    {
        int c = InDeck(cardId);
        if (c <= 0) return;
        if (c - 1 <= 0) _deck.Remove(cardId);
        else _deck[cardId] = c - 1;
        RefreshAll();
    }

    // ── Filtros / orden ──────────────────────────────────────────────────

    private void OnSearchChanged(string value) { _search = value; RefreshCollection(); }

    private void SetCategory(Category cat) { _category = cat; RefreshCollection(); }

    private void WireCategory(Button b, Category cat)
    {
        if (b != null) b.onClick.AddListener(() => SetCategory(cat));
    }

    private void CycleSort()
    {
        _sort = (SortMode)(((int)_sort + 1) % 3);
        UpdateSortLabel();
        RefreshCollection();
    }

    private void UpdateSortLabel()
    {
        if (sortLabel == null) return;
        sortLabel.text = _sort switch
        {
            SortMode.AtkDesc => "Orden: ATK",
            SortMode.DefDesc => "Orden: DEF",
            _ => "Orden: Nombre"
        };
    }

    // ── Refresco de UI ───────────────────────────────────────────────────

    private void RefreshAll()
    {
        RefreshCollection();
        RefreshDeck();
        RefreshCount();
        RefreshStats();
    }

    private void RefreshCollection()
    {
        foreach (var go in _spawnedCollection) Destroy(go);
        _spawnedCollection.Clear();
        if (collectionTemplate == null || collectionContent == null) return;

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

        cards = _sort switch
        {
            SortMode.AtkDesc => cards.OrderByDescending(c => c.baseAtk).ThenBy(c => c.cardName),
            SortMode.DefDesc => cards.OrderByDescending(c => c.baseDef).ThenBy(c => c.cardName),
            _                => cards.OrderBy(c => c.cardName)
        };

        foreach (var card in cards)
        {
            var go = Instantiate(collectionTemplate.gameObject, collectionContent);
            go.SetActive(true);
            var view = go.GetComponent<CollectionCardView>();
            int id = card.cardId;
            view.Setup(card, OwnedCopies(id), InDeck(id), () => Add(id));
            _spawnedCollection.Add(go);
        }
    }

    private void RefreshDeck()
    {
        foreach (var go in _spawnedDeck) Destroy(go);
        _spawnedDeck.Clear();
        if (deckTemplate == null || deckContent == null) return;

        foreach (var kv in _deck.OrderBy(k => k.Key))
        {
            var card = LibraryCatalog.GetCard(kv.Key);
            if (card == null) continue;

            var go = Instantiate(deckTemplate.gameObject, deckContent);
            go.SetActive(true);
            var view = go.GetComponent<DeckCardView>();
            int id = kv.Key;
            view.Setup(card, kv.Value, () => Remove(id));
            _spawnedDeck.Add(go);
        }
    }

    private void RefreshCount()
    {
        int total = DeckTotal;
        if (deckCountText != null) deckCountText.text = $"Mazo: {total}/{PlayerDeck.RequiredSize}";

        if (statusText != null)
        {
            if (total == PlayerDeck.RequiredSize) SetStatus("Mazo listo (40/40).", warn: false);
            else if (total < PlayerDeck.RequiredSize) SetStatus($"Faltan {PlayerDeck.RequiredSize - total} cartas.", warn: true);
            else SetStatus($"Sobran {total - PlayerDeck.RequiredSize} cartas.", warn: true);
        }
    }

    private void RefreshStats()
    {
        if (statsText != null) statsText.text = DeckStats.BuildSummary(_deck, _fusionDb);
    }

    // ── Guardar / volver ─────────────────────────────────────────────────

    private void Save()
    {
        if (DeckTotal != PlayerDeck.RequiredSize)
        {
            SetStatus($"El mazo debe contener exactamente {PlayerDeck.RequiredSize} cartas.", warn: true);
            return;
        }

        var flat = new List<int>();
        foreach (var kv in _deck)
            for (int i = 0; i < kv.Value; i++) flat.Add(kv.Key);

        PlayerDeck.Save(flat);
        SetStatus("¡Mazo guardado!", warn: false);
    }

    private void Back() => GameNavigator.EnsureExists().ToMainMenu();

    private void SetStatus(string message, bool warn)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = warn ? new Color(0.95f, 0.55f, 0.35f) : new Color(0.55f, 0.9f, 0.55f);
    }
}
