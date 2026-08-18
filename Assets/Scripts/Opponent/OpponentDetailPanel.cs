using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de detalle de un rival en Duelo Libre. La identidad del rival (retrato,
/// récord, descubrimiento) vive en una COLUMNA lateral estrecha; el protagonista
/// es la GRILLA de cartas que puede soltar, que ocupa el resto del panel: las
/// descubiertas con su arte, las pendientes ocultas ("???"). Tres pestañas eligen
/// de qué tabla de rango se ven los drops (POW / TEC / B-C-D).
///
/// No crea UI: usa referencias de la escena (las cablea FreeDuelBuilder). La
/// grilla se llena en runtime clonando una plantilla (DropCardView).
/// </summary>
public class OpponentDetailPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;            // overlay que se muestra/oculta
    [SerializeField] private Image portrait;
    [SerializeField] private TextMeshProUGUI nameText;
    [Tooltip("Se OCULTA solo si el rival no tiene historia escrita (no deja hueco vacío).")]
    [SerializeField] private TextMeshProUGUI storyText;
    [Tooltip("5 fichas que marcan el nivel de dificultad (1-5), de izquierda a derecha.")]
    [SerializeField] private Image[] difficultyPips;

    [Header("Fichas de dato (solo el VALOR; la etiqueta es texto fijo de la escena)")]
    [SerializeField] private TextMeshProUGUI winsValue;
    [SerializeField] private TextMeshProUGUI lossesValue;
    [SerializeField] private TextMeshProUGUI bestScoreValue;

    [Header("Descubrimiento")]
    [SerializeField] private TextMeshProUGUI discoveryValue;   // "18/27"
    [Tooltip("Image de tipo Filled (horizontal) que se rellena con el % descubierto.")]
    [SerializeField] private Image discoveryFill;

    [Header("Grilla de drops")]
    [SerializeField] private Transform dropGridContent;
    [SerializeField] private DropCardView dropCardTemplate;
    [Tooltip("Aviso cuando la tabla del rango elegido no tiene ninguna carta.")]
    [SerializeField] private TextMeshProUGUI emptyTableText;

    [Header("Tabs de rango (qué tabla de drops se muestra)")]
    [SerializeField] private Button tabPowButton;
    [SerializeField] private Button tabTecButton;
    [SerializeField] private Button tabBcdButton;

    [Header("Botones")]
    [SerializeField] private Button retarButton;
    [SerializeField] private Button closeButton;

    private enum DropTab { Pow, Tec, Bcd }

    private static readonly Color TabSelected = new Color(1f, 1f, 1f, 1f);
    private static readonly Color TabUnselected = new Color(0.55f, 0.52f, 0.6f, 0.65f);

    private readonly List<GameObject> _spawned = new();
    private OpponentData _current;
    private DropTab _selectedTab = DropTab.Pow;

    void Awake()
    {
        if (dropCardTemplate != null) dropCardTemplate.gameObject.SetActive(false);
        // El sonido de cancelar va DENTRO de Hide() para que también suene si se cierra
        // por otra vía (p. ej. Escape en FreeDuelController).
        if (closeButton != null) { closeButton.onClick.AddListener(Hide); UIButtonSfx.HookHover(closeButton.gameObject); }
        if (retarButton != null) { retarButton.onClick.AddListener(OnRetar); UIButtonSfx.Hook(retarButton); }
        if (tabPowButton != null) { tabPowButton.onClick.AddListener(() => SetTab(DropTab.Pow)); UIButtonSfx.Hook(tabPowButton); }
        if (tabTecButton != null) { tabTecButton.onClick.AddListener(() => SetTab(DropTab.Tec)); UIButtonSfx.Hook(tabTecButton); }
        if (tabBcdButton != null) { tabBcdButton.onClick.AddListener(() => SetTab(DropTab.Bcd)); UIButtonSfx.Hook(tabBcdButton); }
        if (root != null) root.SetActive(false); // estado inicial oculto SIN sonido (no es un "cancelar" real)
    }

    public bool IsOpen => root != null && root.activeSelf;

    public void Show(OpponentData opp)
    {
        if (opp == null) return;
        _current = opp;

        if (portrait != null)
        {
            portrait.sprite = opp.portrait;
            portrait.enabled = opp.portrait != null;
        }
        if (nameText != null) nameText.text = opp.opponentName;

        // Historia: casi ningún rival importado tiene una — ocultarla evita dejar
        // un bloque vacío robándole alto a la grilla de cartas.
        if (storyText != null)
        {
            bool hasStory = !string.IsNullOrWhiteSpace(opp.story);
            storyText.text = hasStory ? opp.story : "";
            storyText.gameObject.SetActive(hasStory);
        }

        SetDifficultyPips(opp.difficultyLevel);

        var pc = PlayerCollection.Instance;
        var progress = pc != null ? pc.GetOpponentProgress(opp.opponentId) : null;
        if (winsValue != null) winsValue.text = (progress?.wins ?? 0).ToString();
        if (lossesValue != null) lossesValue.text = (progress?.losses ?? 0).ToString();
        if (bestScoreValue != null) bestScoreValue.text = (progress?.bestScore ?? 0).ToString();

        var (discovered, total) = FreeDuelService.GetDropDiscovery(opp);
        if (discoveryValue != null) discoveryValue.text = $"{discovered}/{total}";
        if (discoveryFill != null) discoveryFill.fillAmount = total > 0 ? (float)discovered / total : 0f;

        RefreshTabVisuals();
        PopulateDrops();

        if (root != null) root.SetActive(true);
    }

    private void SetTab(DropTab tab)
    {
        if (_selectedTab == tab) return;
        _selectedTab = tab;
        RefreshTabVisuals();
        PopulateDrops();
    }

    private void RefreshTabVisuals()
    {
        Tint(tabPowButton, _selectedTab == DropTab.Pow);
        Tint(tabTecButton, _selectedTab == DropTab.Tec);
        Tint(tabBcdButton, _selectedTab == DropTab.Bcd);

        static void Tint(Button b, bool selected)
        {
            if (b == null || b.targetGraphic == null) return;
            b.targetGraphic.color = selected ? TabSelected : TabUnselected;
        }
    }

    /// <summary>Rellena la grilla con la tabla del rango SELECCIONADO (POW/TEC/B-C-D), la más probable primero.</summary>
    private void PopulateDrops()
    {
        foreach (var go in _spawned) Destroy(go);
        _spawned.Clear();

        if (_current == null || dropCardTemplate == null || dropGridContent == null) return;

        RewardTable table = _selectedTab switch
        {
            DropTab.Pow => _current.powRewards,
            DropTab.Tec => _current.tecRewards,
            _           => _current.bcdRewards
        };

        var entries = table?.entries?
            .Where(e => e != null && e.card != null)
            .OrderByDescending(e => e.probability)
            .ThenBy(e => e.card.cardName)
            .ToList();

        if (emptyTableText != null)
            emptyTableText.gameObject.SetActive(entries == null || entries.Count == 0);
        if (entries == null) return;

        var pc = PlayerCollection.Instance;
        foreach (var entry in entries)
        {
            var go = Instantiate(dropCardTemplate.gameObject, dropGridContent);
            go.SetActive(true);

            var view = go.GetComponent<DropCardView>();
            bool discovered = pc != null && pc.IsDiscovered(entry.card.cardId);
            view.Setup(entry.card, discovered, entry.probability);

            _spawned.Add(go);
        }
    }

    private void OnRetar()
    {
        if (_current != null) FreeDuelService.StartFreeDuel(_current);
    }

    public void Hide()
    {
        GameAudio.Back();
        if (root != null) root.SetActive(false);
    }

    private static readonly Color PipFilled = new Color(0.98f, 0.82f, 0.35f);
    private static readonly Color PipEmpty = new Color(0.98f, 0.82f, 0.35f, 0.18f);

    private void SetDifficultyPips(int level)
    {
        if (difficultyPips == null) return;
        level = Mathf.Clamp(level, 0, difficultyPips.Length);
        for (int i = 0; i < difficultyPips.Length; i++)
            if (difficultyPips[i] != null) difficultyPips[i].color = i < level ? PipFilled : PipEmpty;
    }
}
