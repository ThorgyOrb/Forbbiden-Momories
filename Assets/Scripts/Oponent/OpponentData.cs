using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Definición COMPLETA de un oponente/duelista (catálogo, no progreso — si está
/// desbloqueado vive en PlayerCollection). Un mismo OpponentData describe su
/// identidad, mazo, estrategia de IA, recompensas, música y arena.
///
/// Se enlaza a un duelo desde <see cref="DuelConfig.opponent"/>. Los assets se
/// cargan en runtime desde Resources/Opponents/Data (ver LibraryCatalog).
/// </summary>
[CreateAssetMenu(fileName = "NewOpponent", menuName = "YGO/Opponent Data")]
public class OpponentData : ScriptableObject
{
    [Header("Identidad")]
    public int opponentId;
    public string opponentName;
    public Sprite portrait;
    [TextArea] public string story;   // narrativa del personaje

    [Header("Progresión")]
    [Tooltip("Nivel de dificultad para ordenar la campaña (1 = básico … 5 = combina todo).")]
    [Range(1, 5)] public int difficultyLevel = 1;
    [Tooltip("Orden de aparición en la campaña (para ordenar el Duelo Libre).")]
    public int appearanceOrder = 0;
    [Tooltip("Región donde se encontró (Palacio, Bosque, Montañas…). Para agrupar en Duelo Libre.")]
    public string region = "";
    [Tooltip("Es un JEFE: mazo único, IA avanzada, recompensas excepcionales.")]
    public bool isBoss = false;

    [Header("Mazo")]
    [Tooltip("Cartas del mazo del rival (número fijo, p. ej. 40). Puede seguir una estrategia muy concreta.")]
    public List<CardData> deck = new();

    [Header("Estrategia (IA)")]
    public AIStrategy aiStrategy = AIStrategy.Balanced;
    [Tooltip("Competencia de la IA (profundidad de decisión): 0 = fácil … 3 = difícil.")]
    [Range(0, 3)] public int aiLevel = 1;

    [Header("Presentación")]
    [Tooltip("Terreno de la arena. Neutral = sin terreno inicial.")]
    public TerrainType arena = TerrainType.Neutral;
    [Tooltip("(Opcional) Escena específica de la arena para este duelo.")]
    public string arenaScene = "";
    public AudioClip battleMusic;

    [Header("Recompensas por tipo de victoria")]
    [Tooltip("Su 'colección personal': qué cartas puede soltar y con qué probabilidad. " +
             "Rareza alta → probabilidad baja.")]
    public RewardTable normalVictoryRewards = new();
    public RewardTable technicalVictoryRewards = new();
    public RewardTable perfectVictoryRewards = new();

    /// <summary>Devuelve la tabla de recompensas correspondiente al tipo de victoria.</summary>
    public RewardTable GetRewardTable(VictoryTier tier) => tier switch
    {
        VictoryTier.Perfect => perfectVictoryRewards,
        VictoryTier.Technical => technicalVictoryRewards,
        _ => normalVictoryRewards
    };

    /// <summary>
    /// Todas las cartas distintas que este oponente puede soltar (unión de sus
    /// tres tablas). Útil para el Duelo Libre: "cartas descubiertas 18/26".
    /// </summary>
    public IEnumerable<CardData> AllRewardCards()
    {
        var set = new HashSet<CardData>();
        AddCards(set, normalVictoryRewards);
        AddCards(set, technicalVictoryRewards);
        AddCards(set, perfectVictoryRewards);
        return set;
    }

    private static void AddCards(HashSet<CardData> set, RewardTable table)
    {
        if (table?.entries == null) return;
        foreach (var e in table.entries)
            if (e != null && e.card != null) set.Add(e.card);
    }
}

/// <summary>
/// Una tabla de recompensas: cartas posibles y su probabilidad individual (0..1).
/// Reutiliza <see cref="DropEntry"/> (carta + probabilidad).
/// </summary>
[System.Serializable]
public class RewardTable
{
    public List<DropEntry> entries = new();
}
