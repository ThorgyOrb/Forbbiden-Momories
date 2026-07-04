using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evalúa el desempeño del jugador al finalizar un duelo y determina el rango.
/// También calcula la puntuación numérica y selecciona la carta de recompensa.
/// </summary>
public static class RankEvaluator
{
    /// <summary>Calcula el rango basado en estadísticas del duelo.</summary>
    public static DuelRank Evaluate(
        int playerLP, int opponentLP, int damageReceived,
        int fusionsPerformed, int spellsUsed, int turnsPlayed)
    {
        var (score, isTec) = Compute(playerLP, opponentLP, damageReceived, fusionsPerformed, spellsUsed, turnsPlayed);

        if (isTec)
        {
            if (score >= 150) return DuelRank.STec;
            if (score >= 80)  return DuelRank.ATec;
            return DuelRank.BTec;
        }

        if (score >= 150) return DuelRank.SPow;
        if (score >= 80)  return DuelRank.APow;
        return DuelRank.BPow;
    }

    /// <summary>Puntuación numérica del duelo (para "mejor puntuación" por oponente).</summary>
    public static int ComputeScore(
        int playerLP, int opponentLP, int damageReceived,
        int fusionsPerformed, int spellsUsed, int turnsPlayed)
        => Compute(playerLP, opponentLP, damageReceived, fusionsPerformed, spellsUsed, turnsPlayed).score;

    private static (int score, bool isTec) Compute(
        int playerLP, int opponentLP, int damageReceived,
        int fusionsPerformed, int spellsUsed, int turnsPlayed)
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

        bool isTec = tecScore > powScore;
        return (Mathf.Max(tecScore, powScore), isTec);
    }

    /// <summary>
    /// Selecciona la carta de recompensa. Prioridad:
    ///   1. Override de recompensa del duelo (DuelConfig.rewardOverride).
    ///   2. Tabla del oponente según el tipo de victoria (Normal/Técnica/Perfecta).
    ///   3. Respaldo a la tabla Normal del oponente.
    /// Devuelve null si no cae ninguna carta.
    /// </summary>
    public static CardData SelectReward(DuelRank rank, OpponentData opponent, DuelConfig overrides)
    {
        // 1. Override SOLO de este duelo (p. ej. batalla de historia).
        if (overrides != null && overrides.rewardOverride != null && overrides.rewardOverride.entries.Count > 0)
        {
            var overridden = RollTable(overrides.rewardOverride.entries);
            if (overridden != null) return overridden;
        }

        if (opponent == null) return null;

        // 2. Tabla del rival según el tipo de victoria.
        VictoryTier tier = MapToVictoryTier(rank);
        var reward = RollTable(opponent.GetRewardTable(tier)?.entries);
        if (reward != null) return reward;

        // 3. Respaldo: la tabla Normal.
        if (tier != VictoryTier.Normal)
            return RollTable(opponent.normalVictoryRewards?.entries);

        return null;
    }

    /// <summary>Traduce el rango del duelo al tipo de victoria para elegir tabla.</summary>
    public static VictoryTier MapToVictoryTier(DuelRank rank) => rank switch
    {
        DuelRank.SPow or DuelRank.STec => VictoryTier.Perfect,
        DuelRank.BTec or DuelRank.ATec => VictoryTier.Technical,
        _ => VictoryTier.Normal
    };

    /// <summary>Ruleta ponderada: recorre las entradas y devuelve la primera que "cae".</summary>
    private static CardData RollTable(List<DropEntry> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        foreach (var entry in pool)
            if (entry.card != null && Random.value <= entry.probability)
                return entry.card;
        return null;
    }
}
