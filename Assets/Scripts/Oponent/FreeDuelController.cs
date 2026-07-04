using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla de Duelo Libre. Lista los oponentes DESBLOQUEADOS (derrotados en
/// campaña) leyendo <see cref="FreeDuelService"/>, y al elegir uno lanza el duelo.
///
/// No crea la UI: usa referencias a objetos reales de la escena (las genera y
/// cablea FreeDuelBuilder desde el menú YGO > Setup). La lista de rivales SÍ se
/// genera en runtime (depende del desbloqueo), clonando una plantilla editable.
/// </summary>
public class FreeDuelController : MonoBehaviour
{
    [Header("Chrome")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button backButton;
    [SerializeField] private TextMeshProUGUI emptyText;

    [Header("Orden")]
    [SerializeField] private Button sortAppearanceButton;
    [SerializeField] private Button sortDifficultyButton;
    [SerializeField] private Button sortRegionButton;

    [Header("Lista")]
    [Tooltip("Content del ScrollView donde se instancian las tarjetas.")]
    [SerializeField] private Transform listContent;
    [Tooltip("Tarjeta plantilla (inactiva) que se clona por cada oponente.")]
    [SerializeField] private OpponentEntryView entryTemplate;

    [Header("Detalle")]
    [SerializeField] private OpponentDetailPanel detailPanel;

    private FreeDuelService.SortMode _sort = FreeDuelService.SortMode.Appearance;
    private readonly List<GameObject> _spawned = new();

    void Start()
    {
        GameNavigator.EnsureExists();

        if (backButton != null) backButton.onClick.AddListener(Back);
        if (sortAppearanceButton != null) sortAppearanceButton.onClick.AddListener(() => SetSort(FreeDuelService.SortMode.Appearance));
        if (sortDifficultyButton != null) sortDifficultyButton.onClick.AddListener(() => SetSort(FreeDuelService.SortMode.Difficulty));
        if (sortRegionButton != null) sortRegionButton.onClick.AddListener(() => SetSort(FreeDuelService.SortMode.Region));

        if (entryTemplate != null) entryTemplate.gameObject.SetActive(false);

        Populate();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (detailPanel != null && detailPanel.IsOpen) detailPanel.Hide();
            else Back();
        }
    }

    private void SetSort(FreeDuelService.SortMode mode)
    {
        _sort = mode;
        Populate();
    }

    private void Populate()
    {
        foreach (var go in _spawned) Destroy(go);
        _spawned.Clear();

        List<OpponentData> opponents = FreeDuelService.GetUnlockedOpponents(_sort);

        if (emptyText != null) emptyText.gameObject.SetActive(opponents.Count == 0);

        if (entryTemplate == null || listContent == null) return;

        var pc = PlayerCollection.Instance;
        foreach (var opp in opponents)
        {
            var go = Instantiate(entryTemplate.gameObject, listContent);
            go.SetActive(true);

            var view = go.GetComponent<OpponentEntryView>();
            var progress = pc != null ? pc.GetOpponentProgress(opp.opponentId) : null;
            var (discovered, total) = FreeDuelService.GetDropDiscovery(opp);

            var captured = opp; // captura por-iteración para los callbacks
            view.Setup(opp, progress, discovered, total,
                onDuel: () => FreeDuelService.StartFreeDuel(captured),
                onDetail: () => { if (detailPanel != null) detailPanel.Show(captured); });

            _spawned.Add(go);
        }
    }

    private void Back() => GameNavigator.EnsureExists().ToMainMenu();
}
