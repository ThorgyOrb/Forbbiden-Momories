using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de detalle de un rival en Duelo Libre. Muestra su información y la
/// GRILLA de cartas que puede soltar: las descubiertas con su arte, las
/// pendientes ocultas ("???"). Desde aquí también se puede retar.
///
/// No crea UI: usa referencias de la escena (las cablea FreeDuelBuilder). La
/// grilla se llena en runtime clonando una plantilla (DropCardView).
/// </summary>
public class OpponentDetailPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;            // overlay que se muestra/oculta
    [SerializeField] private Image portrait;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI storyText;
    [SerializeField] private TextMeshProUGUI difficultyText;
    [SerializeField] private TextMeshProUGUI recordText;
    [SerializeField] private TextMeshProUGUI discoveryText;

    [Header("Grilla de drops")]
    [SerializeField] private Transform dropGridContent;
    [SerializeField] private DropCardView dropCardTemplate;

    [Header("Botones")]
    [SerializeField] private Button retarButton;
    [SerializeField] private Button closeButton;

    private readonly List<GameObject> _spawned = new();
    private OpponentData _current;

    void Awake()
    {
        if (dropCardTemplate != null) dropCardTemplate.gameObject.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (retarButton != null) retarButton.onClick.AddListener(OnRetar);
        Hide();
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
        if (storyText != null) storyText.text = opp.story ?? "";
        if (difficultyText != null) difficultyText.text = Stars(opp.difficultyLevel);

        var pc = PlayerCollection.Instance;
        var progress = pc != null ? pc.GetOpponentProgress(opp.opponentId) : null;
        if (recordText != null)
            recordText.text = $"Victorias: {progress?.wins ?? 0}    Derrotas: {progress?.losses ?? 0}    " +
                              $"Mejor: {progress?.bestScore ?? 0}";

        var (discovered, total) = FreeDuelService.GetDropDiscovery(opp);
        if (discoveryText != null) discoveryText.text = $"Cartas descubiertas: {discovered}/{total}";

        PopulateDrops(opp);

        if (root != null) root.SetActive(true);
    }

    private void PopulateDrops(OpponentData opp)
    {
        foreach (var go in _spawned) Destroy(go);
        _spawned.Clear();

        if (dropCardTemplate == null || dropGridContent == null) return;

        var pc = PlayerCollection.Instance;
        // Orden estable por id (AllRewardCards devuelve un set sin orden).
        foreach (var card in opp.AllRewardCards().OrderBy(c => c.cardId))
        {
            var go = Instantiate(dropCardTemplate.gameObject, dropGridContent);
            go.SetActive(true);

            var view = go.GetComponent<DropCardView>();
            bool discovered = pc != null && pc.IsDiscovered(card.cardId);
            view.Setup(card, discovered);

            _spawned.Add(go);
        }
    }

    private void OnRetar()
    {
        if (_current != null) FreeDuelService.StartFreeDuel(_current);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    private static string Stars(int level)
    {
        level = Mathf.Clamp(level, 0, 5);
        return new string('★', level) + new string('☆', 5 - level);
    }
}
