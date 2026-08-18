using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista de UNA tarjeta de rival en la lista de Duelo Libre: retrato, nombre,
/// dificultad, récord (victorias/derrotas/mejor puntuación como "fichas" de dato
/// separadas) y el progreso de cartas descubiertas como barra + contador.
///
/// Dos interacciones:
///   • Tocar la tarjeta (cardButton)  → abre el DETALLE (cartas que dropea).
///   • Botón "Retar" (duelButton)      → lanza el duelo directamente.
///
/// El FreeDuelController clona la plantilla que tiene este componente y llama a
/// <see cref="Setup"/> por cada oponente desbloqueado.
/// </summary>
public class OpponentEntryView : MonoBehaviour
{
    [SerializeField] private Button cardButton;   // toda la tarjeta, abre el detalle
    [SerializeField] private Image portrait;
    [SerializeField] private TextMeshProUGUI nameText;
    [Tooltip("5 fichas que marcan el nivel de dificultad (1-5), de izquierda a derecha.")]
    [SerializeField] private Image[] difficultyPips;

    [Header("Fichas de dato (solo el VALOR; la etiqueta es texto fijo del prefab)")]
    [SerializeField] private TextMeshProUGUI winsValue;
    [SerializeField] private TextMeshProUGUI lossesValue;
    [SerializeField] private TextMeshProUGUI bestScoreValue;

    [Header("Descubrimiento")]
    [SerializeField] private TextMeshProUGUI discoveryValue;   // "18/27"
    [Tooltip("Image de tipo Filled (horizontal) que se rellena con el % descubierto.")]
    [SerializeField] private Image discoveryFill;

    [SerializeField] private Button duelButton;   // "Retar"

    public void Setup(OpponentData opp, OpponentProgress progress, int discovered, int total, Action onDuel, Action onDetail)
    {
        if (portrait != null)
        {
            portrait.sprite = opp.portrait;
            portrait.enabled = opp.portrait != null;
        }
        if (nameText != null) nameText.text = opp.opponentName;
        SetDifficultyPips(opp.difficultyLevel);

        if (winsValue != null) winsValue.text = (progress?.wins ?? 0).ToString();
        if (lossesValue != null) lossesValue.text = (progress?.losses ?? 0).ToString();
        if (bestScoreValue != null) bestScoreValue.text = (progress?.bestScore ?? 0).ToString();

        if (discoveryValue != null) discoveryValue.text = $"{discovered}/{total}";
        if (discoveryFill != null) discoveryFill.fillAmount = total > 0 ? (float)discovered / total : 0f;

        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => onDetail?.Invoke());
            cardButton.onClick.AddListener(GameAudio.Click);
        }
        if (duelButton != null)
        {
            duelButton.onClick.RemoveAllListeners();
            duelButton.onClick.AddListener(() => onDuel?.Invoke());
            duelButton.onClick.AddListener(GameAudio.Click);
        }
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
