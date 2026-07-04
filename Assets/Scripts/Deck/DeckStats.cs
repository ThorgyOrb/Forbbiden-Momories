using System.Collections.Generic;
using System.Text;

/// <summary>
/// Calcula y formatea las estadísticas de un mazo (total, monstruos/magias/
/// trampas, promedios de ATK/DEF, conteo por tipo y fusiones posibles).
/// </summary>
public static class DeckStats
{
    /// <summary>
    /// Texto de estadísticas listo para mostrar. "deck" mapea cardId → nº de copias.
    /// "fusionDb" es opcional: si se pasa, cuenta las fusiones posibles.
    /// </summary>
    public static string BuildSummary(Dictionary<int, int> deck, FusionDatabase fusionDb = null)
    {
        int total = 0, monsters = 0, spells = 0, traps = 0, equips = 0, rituals = 0, specials = 0;
        long atkSum = 0, defSum = 0;
        int monsterCount = 0;

        var typeCounts = new Dictionary<MonsterType, int>();
        var distinct = new List<CardData>();

        foreach (var kv in deck)
        {
            if (kv.Value <= 0) continue;
            var card = LibraryCatalog.GetCard(kv.Key);
            if (card == null) continue;

            distinct.Add(card);
            int n = kv.Value;
            total += n;

            if (card.IsMonster)
            {
                monsters += n;
                monsterCount += n;
                atkSum += (long)card.baseAtk * n;
                defSum += (long)card.baseDef * n;
                typeCounts.TryGetValue(card.monsterType, out int c);
                typeCounts[card.monsterType] = c + n;
            }
            else if (card.IsSpell) spells += n;
            else if (card.IsEquip) equips += n;
            else if (card.IsTrap) traps += n;
            else if (card.IsRitual) rituals += n;
            else if (card.IsSpecial) specials += n;
        }

        int avgAtk = monsterCount > 0 ? (int)(atkSum / monsterCount) : 0;
        int avgDef = monsterCount > 0 ? (int)(defSum / monsterCount) : 0;

        var sb = new StringBuilder();
        sb.AppendLine($"Total de cartas: {total}");
        sb.AppendLine($"Monstruos: {monsters}");
        sb.AppendLine($"Magias: {spells}");
        sb.AppendLine($"Trampas: {traps}");
        sb.AppendLine($"Equipos: {equips}");
        if (rituals > 0) sb.AppendLine($"Rituales: {rituals}");
        if (specials > 0) sb.AppendLine($"Especiales: {specials}");
        sb.AppendLine($"Promedio ATK: {avgAtk}");
        sb.AppendLine($"Promedio DEF: {avgDef}");

        foreach (var t in TopTypes(typeCounts, 4))
            sb.AppendLine($"{TypeName(t.Key)}: {t.Value}");

        if (fusionDb != null)
            sb.AppendLine($"Fusiones posibles: {CountFusions(distinct, fusionDb)}");

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

    private static string TypeName(MonsterType t) => t switch
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
