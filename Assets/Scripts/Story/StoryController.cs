using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla del MODO HISTORIA (campaña). Muestra la hoja de ruta de rivales en
/// orden de aparición y, a la derecha, el detalle del capítulo seleccionado con
/// su narrativa y el botón para duelar.
///
/// No crea la UI: usa referencias a objetos reales de la escena (las genera y
/// cablea StoryBuilder desde el menú YGO > Setup). La hoja de ruta SÍ se genera
/// en runtime (depende del progreso), clonando una plantilla editable.
///
/// El progreso se DERIVA de qué rivales has derrotado (ver <see cref="StoryService"/>),
/// así que al volver de ganar un duelo la campaña avanza sola.
/// </summary>
public class StoryController : MonoBehaviour
{
    [Header("Chrome")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button backButton;
    [SerializeField] private TextMeshProUGUI progressText;   // "Progreso: 3 / 12"

    [Header("Hoja de ruta")]
    [Tooltip("Content del ScrollView donde se instancian los capítulos.")]
    [SerializeField] private Transform listContent;
    [Tooltip("Fila plantilla (inactiva) que se clona por cada capítulo.")]
    [SerializeField] private StoryChapterView chapterTemplate;

    [Header("Detalle del capítulo")]
    [SerializeField] private GameObject detailRoot;          // se oculta si no hay rival que mostrar
    [SerializeField] private Image detailPortrait;
    [SerializeField] private TextMeshProUGUI detailChapter;  // "Capítulo 3"
    [SerializeField] private TextMeshProUGUI detailName;
    [SerializeField] private TextMeshProUGUI detailDifficulty;
    [SerializeField] private TextMeshProUGUI detailStatus;
    [SerializeField] private TextMeshProUGUI detailStory;
    [SerializeField] private Button duelButton;
    [SerializeField] private TextMeshProUGUI duelButtonLabel;

    [Header("Campaña completada")]
    [Tooltip("Cartel que se muestra cuando ya derrotaste a todos los rivales.")]
    [SerializeField] private GameObject completeBanner;

    private List<OpponentData> _campaign = new();
    private readonly List<GameObject> _spawned = new();
    private int _selectedIndex = -1;

    void Start()
    {
        GameNavigator.EnsureExists();
        PlayerCollection.EnsureExists();

        if (backButton != null) backButton.onClick.AddListener(Back);
        if (duelButton != null) duelButton.onClick.AddListener(DuelSelected);
        if (chapterTemplate != null) chapterTemplate.gameObject.SetActive(false);

        _campaign = StoryService.GetCampaign();
        StoryService.SyncProgress(_campaign);

        PopulateRoadmap();

        // Selección inicial: el capítulo actual, o el último si ya está completa.
        int current = StoryService.GetCurrentIndex(_campaign);
        int initial = Mathf.Clamp(current, 0, Mathf.Max(0, _campaign.Count - 1));
        if (_campaign.Count > 0) Select(initial);
        else ShowEmpty();

        RefreshCompletionState();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Back();
    }

    // ── Hoja de ruta ─────────────────────────────────────────────────────

    private void PopulateRoadmap()
    {
        foreach (var go in _spawned) Destroy(go);
        _spawned.Clear();

        if (progressText != null)
            progressText.text = $"Progreso: {StoryService.GetCurrentIndex(_campaign)} / {_campaign.Count}";

        if (chapterTemplate == null || listContent == null) return;

        for (int i = 0; i < _campaign.Count; i++)
        {
            var go = Instantiate(chapterTemplate.gameObject, listContent);
            go.SetActive(true);

            var view = go.GetComponent<StoryChapterView>();
            var state = StoryService.GetChapterState(i, _campaign);
            int captured = i; // captura por-iteración para el callback
            view.Setup(i, _campaign[i], state, onSelect: () => Select(captured));

            _spawned.Add(go);
        }
    }

    // ── Detalle ──────────────────────────────────────────────────────────

    private void Select(int index)
    {
        if (index < 0 || index >= _campaign.Count) return;
        _selectedIndex = index;

        var opp = _campaign[index];
        var state = StoryService.GetChapterState(index, _campaign);

        if (detailRoot != null) detailRoot.SetActive(true);

        if (detailPortrait != null)
        {
            detailPortrait.sprite = opp.portrait;
            detailPortrait.enabled = opp.portrait != null;
        }
        if (detailChapter != null) detailChapter.text = $"Capítulo {index + 1}";
        if (detailName != null) detailName.text = opp.opponentName;
        if (detailDifficulty != null) detailDifficulty.text = Stars(opp.difficultyLevel);
        if (detailStory != null)
            detailStory.text = string.IsNullOrWhiteSpace(opp.story)
                ? "<i>Sin descripción.</i>"
                : opp.story;

        if (detailStatus != null)
        {
            detailStatus.text = state switch
            {
                StoryService.ChapterState.Completed => "Rival derrotado",
                StoryService.ChapterState.Current   => "Siguiente rival",
                _                                   => "Bloqueado"
            };
        }

        // Duelar en el capítulo actual; revancha en los ya derrotados.
        bool canDuel = state != StoryService.ChapterState.Locked;
        if (duelButton != null) duelButton.interactable = canDuel;
        if (duelButtonLabel != null)
            duelButtonLabel.text = state == StoryService.ChapterState.Completed ? "Revancha" : "Duelar";
    }

    private void ShowEmpty()
    {
        if (detailRoot != null) detailRoot.SetActive(false);
    }

    private void RefreshCompletionState()
    {
        if (completeBanner != null)
            completeBanner.SetActive(StoryService.IsCampaignComplete(_campaign));
    }

    // ── Acciones ─────────────────────────────────────────────────────────

    private void DuelSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _campaign.Count) return;
        StoryService.StartStoryDuel(_campaign[_selectedIndex]);
    }

    private void Back() => GameNavigator.EnsureExists().ToMainMenu();

    // ── Utilidades ───────────────────────────────────────────────────────

    private static string Stars(int level)
    {
        level = Mathf.Clamp(level, 0, 5);
        return new string('★', level) + new string('☆', 5 - level);
    }
}
