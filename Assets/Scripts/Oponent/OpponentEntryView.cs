using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista de UNA tarjeta de rival en la lista de Duelo Libre: retrato, nombre,
/// dificultad, récord (victorias/derrotas), mejor puntuación, cartas descubiertas
/// y el botón para retarlo.
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
    [SerializeField] private TextMeshProUGUI difficultyText;
    [SerializeField] private TextMeshProUGUI recordText;
    [SerializeField] private TextMeshProUGUI bestScoreText;
    [SerializeField] private TextMeshProUGUI discoveryText;
    [SerializeField] private Button duelButton;   // "Retar"

    public void Setup(OpponentData opp, OpponentProgress progress, int discovered, int total, Action onDuel, Action onDetail)
    {
        if (portrait != null)
        {
            portrait.sprite = opp.portrait;
            portrait.enabled = opp.portrait != null;
        }
        if (nameText != null) nameText.text = opp.opponentName;
        if (difficultyText != null) difficultyText.text = Stars(opp.difficultyLevel);
        if (recordText != null)
            recordText.text = $"Victorias: {progress?.wins ?? 0}    Derrotas: {progress?.losses ?? 0}";
        if (bestScoreText != null) bestScoreText.text = $"Mejor puntuación: {progress?.bestScore ?? 0}";
        if (discoveryText != null) discoveryText.text = $"Cartas descubiertas: {discovered}/{total}";

        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => onDetail?.Invoke());
        }
        if (duelButton != null)
        {
            duelButton.onClick.RemoveAllListeners();
            duelButton.onClick.AddListener(() => onDuel?.Invoke());
        }
    }

    private static string Stars(int level)
    {
        level = Mathf.Clamp(level, 0, 5);
        return new string('★', level) + new string('☆', 5 - level);
    }
}
