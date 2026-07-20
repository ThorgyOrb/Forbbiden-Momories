using System.Collections.Generic;
using System.Text;

/// <summary>
/// Calcula y formatea las estadísticas de un mazo (total, monstruos/magias/
/// trampas, promedios de ATK/DEF, coste medio, conteo por tipo, distribución por
/// nivel y fusiones posibles).
/// </summary>
public static class DeckStats
{
    /// <summary>
    /// Resumen numérico de un mazo, listo para pintar en la UI (contadores,
    /// promedios, distribución por categoría/nivel y fusiones).
    /// </summary>
    public struct Summary
    {
        public int total;
        public int monsters, spells, traps, equips, rituals, specials;
        public int avgAtk, avgDef;
        public float avgCost;             // nivel medio de los monstruos (≈ "coste")
        public int fusions;               // pares distintos con receta de fusión
        public int[] levelHistogram;      // índice = nivel (0..12); [0] no se usa
        public Dictionary<MonsterType, int> typeCounts;

        /// <summary>Magias + Equipos, agrupados como "magia" para la distribución.</summary>
        public int SpellLike => spells + equips + rituals + specials;
    }

    /// <summary>Calcula el <see cref="Summary"/> del mazo. "deck" mapea cardId → nº de copias.</summary>
    public static Summary Compute(Dictionary<int, int> deck, FusionDatabase fusionDb = null)
    {
        var s = new Summary
        {
            levelHistogram = new int[13],
            typeCounts = new Dictionary<MonsterType, int>()
        };

        long atkSum = 0, defSum = 0;
        long levelSum = 0;
        var distinct = new List<CardData>();

        foreach (var kv in deck)
        {
            if (kv.Value <= 0) continue;
            var card = LibraryCatalog.GetCard(kv.Key);
            if (card == null) continue;

            distinct.Add(card);
            int n = kv.Value;
            s.total += n;

            if (card.IsMonster)
            {
                s.monsters += n;
                atkSum += (long)card.baseAtk * n;
                defSum += (long)card.baseDef * n;
                levelSum += (long)card.stars * n;

                int lvl = card.stars;
                if (lvl >= 0 && lvl < s.levelHistogram.Length) s.levelHistogram[lvl] += n;

                s.typeCounts.TryGetValue(card.monsterType, out int c);
                s.typeCounts[card.monsterType] = c + n;
            }
            else if (card.IsSpell)   s.spells += n;
            else if (card.IsEquip)   s.equips += n;
            else if (card.IsTrap)    s.traps += n;
            else if (card.IsRitual)  s.rituals += n;
            else if (card.IsSpecial) s.specials += n;
        }

        if (s.monsters > 0)
        {
            s.avgAtk = (int)(atkSum / s.monsters);
            s.avgDef = (int)(defSum / s.monsters);
            s.avgCost = (float)levelSum / s.monsters;
        }

        if (fusionDb != null) s.fusions = CountFusions(distinct, fusionDb);

        return s;
    }

    /// <summary>
    /// Texto de estadísticas listo para mostrar (compatibilidad con el resumen de
    /// texto plano). "deck" mapea cardId → nº de copias.
    /// </summary>
    public static string BuildSummary(Dictionary<int, int> deck, FusionDatabase fusionDb = null)
    {
        var s = Compute(deck, fusionDb);

        var sb = new StringBuilder();
        sb.AppendLine($"Total de cartas: {s.total}");
        sb.AppendLine($"Monstruos: {s.monsters}");
        sb.AppendLine($"Magias: {s.spells}");
        sb.AppendLine($"Trampas: {s.traps}");
        sb.AppendLine($"Equipos: {s.equips}");
        if (s.rituals > 0) sb.AppendLine($"Rituales: {s.rituals}");
        if (s.specials > 0) sb.AppendLine($"Especiales: {s.specials}");
        sb.AppendLine($"Promedio ATK: {s.avgAtk}");
        sb.AppendLine($"Promedio DEF: {s.avgDef}");

        foreach (var t in TopTypes(s.typeCounts, 4))
            sb.AppendLine($"{TypeName(t.Key)}: {t.Value}");

        if (fusionDb != null)
            sb.AppendLine($"Fusiones posibles: {s.fusions}");

        return sb.ToString();
    }

    private static IEnumerable<KeyValuePair<MonsterType, int>> TopTypes(Dictionary<MonsterType, int> counts, int take)
    {
        var list = new List<KeyValuePair<MonsterType, int>>(counts);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        if (list.Count > take) list.RemoveRange(take, list.Count - take);
        return list;
    }

    /// <summary>Cuenta pares distintos de cartas del mazo que tienen una fusión real.</summary>
    private static int CountFusions(List<CardData> distinct, FusionDatabase db)
    {
        int count = 0;
        for (int i = 0; i < distinct.Count; i++)
            for (int j = i + 1; j < distinct.Count; j++)
                if (db.TryFuse(distinct[i], distinct[j]) != null)
                    count++;
        return count;
    }

    public static string TypeName(MonsterType t) => t switch
    {
        MonsterType.Dragon => "Dragones",
        MonsterType.Spellcaster => "Lanzadores de Conjuros",
        MonsterType.Fiend => "Demonios",
        MonsterType.Beast => "Bestias",
        MonsterType.Insect => "Insectos",
        MonsterType.Plant => "Plantas",
        MonsterType.Fish => "Peces",
        MonsterType.Aqua => "Aqua",
        MonsterType.SeaSerpent => "Serpientes Marinas",
        MonsterType.Zombie => "Zombis",
        MonsterType.Dinosaur => "Dinosaurios",
        MonsterType.WingedBeast => "Bestias Aladas",
        MonsterType.Warrior => "Guerreros",
        MonsterType.Machine => "Máquinas",
        MonsterType.Thunder => "Truenos",
        MonsterType.Fairy => "Hadas",
        MonsterType.Reptile => "Reptiles",
        MonsterType.Rock => "Rocas",
        MonsterType.Pyro => "Pyro",
        _ => t.ToString()
    };
}
