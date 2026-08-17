using System.Collections.Generic;
using UnityEngine;

/// <summary>Una línea del desglose de puntuación (etiqueta + puntos sumados/restados).</summary>
public struct ScoreLine
{
    public string label;
    public int delta;
    public ScoreLine(string label, int delta) { this.label = label; this.delta = delta; }
}

/// <summary>Resultado completo de la evaluación del duelo.</summary>
public struct DuelScore
{
    public int score;              // 0..99
    public DuelRank rank;          // S/A/B/C/D-POW o -TEC
    public bool isTec;             // score < 50
    public List<ScoreLine> lines;  // desglose (para el modal)
}

/// <summary>
/// Evalúa el duelo estilo Forbidden Memories: se parte de 50 puntos y varias acciones
/// suman/restan. La puntuación final (0..99) determina el rango (90-99 S-POW … 0-9 S-TEC).
/// POW favorece drops de monstruos; TEC favorece magias/trampas/equipos. El rango elige de
/// qué tabla del rival (POW o TEC) se extraen los drops; siempre hay un drop garantizado.
/// </summary>
public static class RankEvaluator
{
    /// <summary>Calcula puntuación + rango + desglose a partir de las estadísticas del duelo.</summary>
    public static DuelScore Evaluate(
        int turns, int effectiveAttacks, int effectiveDefenses,
        int fusions, int equips, int spells, int trapsActivated,
        bool wonByLP, bool wonByDeckOut, bool wonByExodia)
    {
        var lines = new List<ScoreLine>();
        int score = 50;
        void Add(string label, int d) { lines.Add(new ScoreLine(label, d)); score += d; }

        // Tipo de victoria.
        if (wonByExodia)      Add("Victoria con Exodia", +40);
        else if (wonByDeckOut) Add("Victoria por Deck-Out del rival", -40);
        else if (wonByLP)     Add("Victoria por LP a 0", +2);

        // Turnos.
        int tB = turns <= 4 ? +12 : turns <= 8 ? +8 : turns <= 28 ? 0 : turns <= 32 ? -8 : -12;
        Add($"Turnos ({turns})", tB);

        // Ataques efectivos (destruir monstruo rival en ATK).
        int eaB = effectiveAttacks <= 1 ? +4 : effectiveAttacks <= 3 ? +2
                : effectiveAttacks <= 9 ? 0 : effectiveAttacks <= 19 ? -2 : -4;
        Add($"Ataques efectivos ({effectiveAttacks})", eaB);

        // Defensas efectivas (tu monstruo en DEF sobrevive).
        int edB = effectiveDefenses <= 1 ? 0 : effectiveDefenses <= 5 ? -10
                : effectiveDefenses <= 9 ? -20 : effectiveDefenses <= 14 ? -30 : -40;
        Add($"Defensas efectivas ({effectiveDefenses})", edB);

        // Fusiones.
        int fB = fusions == 0 ? +4 : fusions <= 4 ? 0 : fusions <= 9 ? -4 : fusions <= 14 ? -8 : -12;
        Add($"Fusiones ({fusions})", fB);

        // Equipos.
        int eqB = equips == 0 ? +4 : equips <= 4 ? 0 : equips <= 9 ? -4 : equips <= 14 ? -8 : -12;
        Add($"Equipos ({equips})", eqB);

        // Magias.
        int mB = spells == 0 ? +2 : spells <= 3 ? -4 : spells <= 6 ? -8 : spells <= 9 ? -12 : -16;
        Add($"Magias ({spells})", mB);

        // Trampas activadas.
        int trB = trapsActivated == 0 ? +2 : trapsActivated <= 2 ? -8 : trapsActivated <= 4 ? -16
                : trapsActivated <= 6 ? -24 : -32;
        Add($"Trampas ({trapsActivated})", trB);

        score = Mathf.Clamp(score, 0, 99);
        return new DuelScore { score = score, rank = RankFromScore(score), isTec = score < 50, lines = lines };
    }

    private static DuelRank RankFromScore(int s) =>
        s >= 90 ? DuelRank.SPow : s >= 80 ? DuelRank.APow : s >= 70 ? DuelRank.BPow :
        s >= 60 ? DuelRank.CPow : s >= 50 ? DuelRank.DPow :
        s >= 40 ? DuelRank.DTec : s >= 30 ? DuelRank.CTec : s >= 20 ? DuelRank.BTec :
        s >= 10 ? DuelRank.ATec : DuelRank.STec;

    /// <summary>Etiqueta legible del rango ("S-POW", "D-TEC"…).</summary>
    public static string RankLabel(DuelRank r) => r switch
    {
        DuelRank.SPow => "S-POW", DuelRank.APow => "A-POW", DuelRank.BPow => "B-POW",
        DuelRank.CPow => "C-POW", DuelRank.DPow => "D-POW",
        DuelRank.DTec => "D-TEC", DuelRank.CTec => "C-TEC", DuelRank.BTec => "B-TEC",
        DuelRank.ATec => "A-TEC", DuelRank.STec => "S-TEC",
        _ => r.ToString()
    };

    /// <summary>Nivel de letra del rango (S=5 … D=1), para premiar starships.</summary>
    private static int RankTier(DuelRank r) => r switch
    {
        DuelRank.SPow or DuelRank.STec => 5,
        DuelRank.APow or DuelRank.ATec => 4,
        DuelRank.BPow or DuelRank.BTec => 3,
        DuelRank.CPow or DuelRank.CTec => 2,
        _ => 1,   // D
    };

    /// <summary>Starships ganados por este duelo: escala con el nivel del rango (S=5 … D=1).</summary>
    public static int StarshipsFor(DuelScore s) => RankTier(s.rank);

    /// <summary>
    /// Cartas de recompensa: el DROP GARANTIZADO del rival (100%) + una carta extra
    /// probabilística de la tabla del RANGO exacto (POW / TEC / B-C-D). Prioriza un
    /// override de historia.
    /// </summary>
    public static List<CardData> SelectRewards(DuelScore score, OpponentData opponent, DuelConfig overrides)
    {
        var result = new List<CardData>();
        if (opponent == null) return result;

        // 1) DROP GARANTIZADO (100%): el configurado; si no hay, se elige uno PONDERADO
        //    (por probabilidad) de la tabla del rango o de cualquier otra tabla del rival.
        //    Al ganar SIEMPRE cae al menos una carta (si el rival tiene alguna tabla con cartas).
        CardData guaranteed = opponent.guaranteedDrop
                           ?? PickWeighted(opponent.GetRewardTable(score.rank)?.entries)
                           ?? PickWeighted(opponent.powRewards?.entries)
                           ?? PickWeighted(opponent.tecRewards?.entries)
                           ?? PickWeighted(opponent.bcdRewards?.entries);
        if (guaranteed != null) result.Add(guaranteed);

        // 2) Carta EXTRA probabilística: override de historia, o ruleta ponderada de la
        //    tabla del rango (estas tablas pueden tener decenas de entradas — la ruleta
        //    ponderada respeta el peso real de cada una, a diferencia de un roll por fila).
        CardData extra = null;
        if (overrides != null && overrides.rewardOverride != null && overrides.rewardOverride.entries.Count > 0)
            extra = RollTable(overrides.rewardOverride.entries);
        if (extra == null)
            extra = PickWeighted(opponent.GetRewardTable(score.rank)?.entries);

        if (extra != null && !result.Contains(extra)) result.Add(extra);
        return result;
    }

    /// <summary>Elige SIEMPRE una carta del pool (ruleta ponderada por probabilidad).</summary>
    private static CardData PickWeighted(List<DropEntry> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        float total = 0f;
        foreach (var e in pool) if (e.card != null) total += Mathf.Max(0.0001f, e.probability);
        if (total <= 0f) { foreach (var e in pool) if (e.card != null) return e.card; return null; }
        float r = Random.value * total;
        foreach (var e in pool)
        {
            if (e.card == null) continue;
            r -= Mathf.Max(0.0001f, e.probability);
            if (r <= 0f) return e.card;
        }
        foreach (var e in pool) if (e.card != null) return e.card;
        return null;
    }

    /// <summary>Ruleta ponderada: devuelve la primera entrada que "cae".</summary>
    private static CardData RollTable(List<DropEntry> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        foreach (var entry in pool)
            if (entry.card != null && Random.value <= entry.probability)
                return entry.card;
        return null;
    }
}
