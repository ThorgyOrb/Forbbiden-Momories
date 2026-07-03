using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Puente entre DuelManager (lógica pura) y todos los GameObjects visuales
/// de la escena Duel. Coloca este script en el GameObject "DuelUI".
///
/// Todos los campos [SerializeField] se asignan en el Inspector.
/// </summary>
public class DuelUI : MonoBehaviour
{
    // ── LP ──────────────────────────────────────────────────────
    [Header("LP")]
    [SerializeField] private TMP_Text playerLPText;
    [SerializeField] private TMP_Text opponentLPText;

    // ── Fase ────────────────────────────────────────────────────
    [Header("Fase")]
    [SerializeField] private TMP_Text phaseText;

    // ── Log ─────────────────────────────────────────────────────
    [Header("Log")]
    [SerializeField] private TMP_Text logText;

    // ── Mano ────────────────────────────────────────────────────
    [Header("Mano")]
    [SerializeField] private Transform handContainer;    // HorizontalLayoutGroup
    [SerializeField] private GameObject cardUIPrefab;    // prefab con CardDisplay

    // ── Campo ───────────────────────────────────────────────────
    [Header("Campo — slots jugador")]
    [SerializeField] private Transform[] playerMonsterSlots  = new Transform[5];
    [SerializeField] private Transform[] playerSpellSlots    = new Transform[5];

    [Header("Campo — slots oponente")]
    [SerializeField] private Transform[] opponentMonsterSlots = new Transform[5];
    [SerializeField] private Transform[] opponentSpellSlots   = new Transform[5];

    // ── Botones de fase ─────────────────────────────────────────
    [Header("Botones")]
    [SerializeField] private Button btnEndMain;
    [SerializeField] private Button btnEndBattle;
    [SerializeField] private GameObject mainPhasePanel;   // panel con botones Invocar/Fusionar
    [SerializeField] private GameObject battlePhasePanel; // panel con Atacar/Pasar

    // ── Resultado / Recompensa ───────────────────────────────────
    [Header("Resultado")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text   resultText;
    [SerializeField] private TMP_Text   rankText;
    [SerializeField] private GameObject rewardPanel;
    [SerializeField] private CardDisplay rewardCardDisplay;

    // ── Terreno ─────────────────────────────────────────────────
    [Header("Terreno")]
    [SerializeField] private Image terrainImage;
    [SerializeField] private TerrainSpriteConfig terrainConfig;

    // ── Instancias activas en mano/campo ─────────────────────────
    private List<CardDisplay> _handCards   = new();
    private CardDisplay[,]    _fieldCards  = new CardDisplay[2, 5];  // [0=player,1=opp, slot]

    // ────────────────────────────────────────────────────────────
    //  LP
    // ────────────────────────────────────────────────────────────

    public void UpdateLP(int playerLP, int opponentLP)
    {
        if (playerLPText   != null) playerLPText.text   = $"LP: {playerLP}";
        if (opponentLPText != null) opponentLPText.text = $"LP: {opponentLP}";
    }

    // ────────────────────────────────────────────────────────────
    //  FASE
    // ────────────────────────────────────────────────────────────

    public void ShowPhase(string phaseName)
    {
        if (phaseText != null) phaseText.text = phaseName;
    }

    // ────────────────────────────────────────────────────────────
    //  LOG
    // ────────────────────────────────────────────────────────────

    public void Log(string message)
    {
        if (logText == null) return;
        // Últimas 6 líneas visibles
        string current = logText.text;
        var lines = current.Split('\n');
        if (lines.Length >= 6)
        {
            current = string.Join('\n', lines, 1, lines.Length - 1);
        }
        logText.text = (current.Length > 0 ? current + "\n" : "") + message;
    }

    // ────────────────────────────────────────────────────────────
    //  MANO
    // ────────────────────────────────────────────────────────────

    public void RefreshHand(List<CardData> hand)
    {
        // Destruir cartas viejas
        foreach (var cd in _handCards)
            if (cd != null) Destroy(cd.gameObject);
        _handCards.Clear();

        if (handContainer == null || cardUIPrefab == null) return;

        foreach (var card in hand)
        {
            var go = Instantiate(cardUIPrefab, handContainer);
            var display = go.GetComponent<CardDisplay>();
            if (display != null)
            {
                display.Setup(card);
                _handCards.Add(display);
            }
        }
    }

    public void AnimateDraw(List<CardData> drawn)
    {
        // TODO: animar cada carta entrando desde el deck
        // Por ahora simplemente logueamos
        foreach (var c in drawn)
            Log($"  Robaste: {c.cardName}");
    }

    // ────────────────────────────────────────────────────────────
    //  CAMPO
    // ────────────────────────────────────────────────────────────

    public void RefreshField(Duelist player, Duelist opponent)
    {
        RefreshSide(player,   playerMonsterSlots,   0);
        RefreshSide(opponent, opponentMonsterSlots, 1);
    }

    private void RefreshSide(Duelist duelist, Transform[] slots, int side)
    {
        for (int i = 0; i < 5; i++)
        {
            // Destruir visual anterior
            if (_fieldCards[side, i] != null)
            {
                Destroy(_fieldCards[side, i].gameObject);
                _fieldCards[side, i] = null;
            }

            if (slots == null || i >= slots.Length || slots[i] == null) continue;

            var card = duelist.MonsterZone[i];
            if (card == null) continue;

            var go = Instantiate(cardUIPrefab, slots[i]);
            var display = go.GetComponent<CardDisplay>();
            if (display != null)
            {
                display.Setup(card);
                display.SetPosition(duelist.MonsterPositions[i]);
                _fieldCards[side, i] = display;
            }
        }
    }

    // ────────────────────────────────────────────────────────────
    //  INPUT CONTROLS
    // ────────────────────────────────────────────────────────────

    public void EnableMainPhaseInput(bool enable)
    {
        if (mainPhasePanel  != null) mainPhasePanel.SetActive(enable);
        if (battlePhasePanel != null) battlePhasePanel.SetActive(false);
        if (btnEndMain != null)
        {
            btnEndMain.gameObject.SetActive(enable);
            btnEndMain.onClick.RemoveAllListeners();
            if (enable) btnEndMain.onClick.AddListener(DuelManager.Instance.PlayerEndMainPhase);
        }
    }

    public void EnableBattleInput(bool enable)
    {
        if (battlePhasePanel != null) battlePhasePanel.SetActive(enable);
        if (mainPhasePanel   != null) mainPhasePanel.SetActive(false);
        if (btnEndBattle != null)
        {
            btnEndBattle.gameObject.SetActive(enable);
            btnEndBattle.onClick.RemoveAllListeners();
            if (enable) btnEndBattle.onClick.AddListener(DuelManager.Instance.PlayerEndBattle);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  RESULTADO / RECOMPENSA
    // ────────────────────────────────────────────────────────────

    public void ShowResult(DuelResult result, string message)
    {
        if (resultPanel != null) resultPanel.SetActive(true);
        if (resultText  != null) resultText.text = message;
        Log(message);
    }

    public void ShowRank(DuelRank rank)
    {
        if (rankText != null) rankText.text = $"Rango: {rank}";
        Log($"Rango obtenido: {rank}");
    }

    public void ShowReward(CardData reward)
    {
        if (rewardPanel != null) rewardPanel.SetActive(true);
        if (rewardCardDisplay != null) rewardCardDisplay.Setup(reward);
        Log($"¡Obtuviste: {reward.cardName}!");
    }

    // ────────────────────────────────────────────────────────────
    //  TERRENO
    // ────────────────────────────────────────────────────────────

    public void SetTerrain(TerrainType terrain)
    {
        if (terrainImage == null || terrainConfig == null) return;
        terrainImage.sprite = terrainConfig.GetSprite(terrain);
    }
}
