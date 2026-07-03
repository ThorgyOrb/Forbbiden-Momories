using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evalúa el desempeño del jugador al finalizar un duelo y determina el rango.
/// También selecciona la carta de recompensa según la lista correspondiente.
/// </summary>
public static class RankEvaluator
{
    /// <summary>
    /// Calcula el rango basado en estadísticas del duelo.
    /// </summary>
    public static DuelRank Evaluate(
        int playerLP,
        int opponentLP,
        int damageReceived,
        int fusionsPerformed,
        int spellsUsed,
        int turnsPlayed)
    {
        // Puntuación técnica (TEC): fusiones, magias, eficiencia de turnos
        int tecScore = 0;
        tecScore += fusionsPerformed * 30;
        tecScore += spellsUsed * 20;
        if (turnsPlayed <= 10) tecScore += 50;
        else if (turnsPlayed <= 20) tecScore += 20;

        // Puntuación de poder (POW): LP restantes, daño recibido bajo
        int powScore = 0;
        powScore += playerLP / 100;
        if (damageReceived == 0) powScore += 50;
        else if (damageReceived < 2000) powScore += 25;

        // ¿Fue más técnico o más de fuerza bruta?
        bool isTec = tecScore > powScore;

        int finalScore = Mathf.Max(tecScore, powScore);

        if (isTec)
        {
            if (finalScore >= 150) return DuelRank.STec;
            if (finalScore >= 80)  return DuelRank.ATec;
            return DuelRank.BTec;
        }
        else
        {
            if (finalScore >= 150) return DuelRank.SPow;
            if (finalScore >= 80)  return DuelRank.APow;
            return DuelRank.BPow;
        }
    }

    /// <summary>
    /// Selecciona una carta de recompensa aleatoria según el rango y la lista del oponente.
    /// Devuelve null si no hay drops disponibles.
    /// </summary>
    public static CardData SelectReward(DuelRank rank, DuelConfig config)
    {
        List<DropEntry> pool = IsPowaRank(rank) ? config.powDrops : config.tecDrops;
        if (pool == null || pool.Count == 0) return null;

        // Ruleta ponderada
        foreach (var entry in pool)
        {
            if (Random.value <= entry.probability)
                return entry.card;
        }
        // Fallback: entregar la primera carta de la lista con probabilidad mínima
        return (Random.value < 0.05f) ? pool[0].card : null;
    }

    private static bool IsPowaRank(DuelRank rank) =>
        rank == DuelRank.BPow || rank == DuelRank.APow || rank == DuelRank.SPow;
}
