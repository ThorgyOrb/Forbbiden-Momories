using System.Collections.Generic;

/// <summary>
/// Calcula bonificaciones de ATK/DEF según terreno, equips y Guardian Stars.
/// Stateless: todos los métodos son estáticos.
/// </summary>
public static class CombatCalculator
{
    // ── Terreno ────────────────────────────────────────────────────────────
    // Cada terreno FAVORECE a unos tipos (+500) y PERJUDICA a otros (-500),
    // igual que el ejemplo del Bosque: Bestia/Planta +500, Máquina -500.

    public const int TerrainAmount = 500;

    private struct TerrainRule
    {
        public MonsterType[] buffed;
        public MonsterType[] penalized;
        public TerrainRule(MonsterType[] buffed, MonsterType[] penalized)
        {
            this.buffed = buffed;
            this.penalized = penalized;
        }
    }

    private static readonly Dictionary<TerrainType, TerrainRule> TerrainRules = new()
    {
        { TerrainType.Forest,    new TerrainRule(
            new[] { MonsterType.Beast, MonsterType.Insect, MonsterType.Plant, MonsterType.Reptile },
            new[] { MonsterType.Machine }) },
        { TerrainType.Mountain,  new TerrainRule(
            new[] { MonsterType.Dragon, MonsterType.WingedBeast, MonsterType.Thunder, MonsterType.Rock },
            new MonsterType[0]) },
        { TerrainType.Sea,       new TerrainRule(
            new[] { MonsterType.Fish, MonsterType.Aqua, MonsterType.SeaSerpent, MonsterType.Reptile },
            new[] { MonsterType.Pyro, MonsterType.Machine }) },
        { TerrainType.Wasteland, new TerrainRule(
            new[] { MonsterType.Zombie, MonsterType.Dinosaur, MonsterType.Rock },
            new MonsterType[0]) },
        { TerrainType.Meadow,    new TerrainRule(
            new[] { MonsterType.Warrior, MonsterType.Spellcaster, MonsterType.Beast, MonsterType.Fairy },
            new MonsterType[0]) },
        { TerrainType.Yami,      new TerrainRule(
            new[] { MonsterType.Fiend, MonsterType.Spellcaster, MonsterType.Zombie },
            new[] { MonsterType.Fairy }) },
        { TerrainType.Dark,      new TerrainRule(
            new[] { MonsterType.Fiend },
            new[] { MonsterType.Fairy }) },
    };

    /// <summary>
    /// Modificador de ATK por terreno: +500 si el tipo (o el terreno favorito de
    /// la carta) coincide, -500 si el terreno lo perjudica, 0 en otro caso.
    /// El terreno favorito explícito siempre ayuda y anula la penalización.
    /// </summary>
    public static int GetTerrainBonus(CardData card, TerrainType terrain)
    {
        if (card == null || terrain == TerrainType.Neutral) return 0;

        // El terreno favorito de la carta tiene prioridad y siempre bonifica.
        if (card.favoriteTerrain != TerrainType.Neutral && card.favoriteTerrain == terrain)
            return TerrainAmount;

        if (!TerrainRules.TryGetValue(terrain, out var rule)) return 0;

        foreach (var t in rule.buffed)
            if (card.monsterType == t) return TerrainAmount;

        foreach (var t in rule.penalized)
            if (card.monsterType == t) return -TerrainAmount;

        return 0;
    }

    // ── Guardian Stars ─────────────────────────────────────────────────────
    // Orden cíclico de ventaja: Sun>Moon>Venus>Mercury>Mars>Jupiter>Saturn>Uranus>Neptune>Pluto>Sun
    // Un monstruo gana +500 si su star activa VENCE a la del oponente.

    private static readonly GuardianStar[] StarOrder =
    {
        GuardianStar.Sun, GuardianStar.Moon, GuardianStar.Venus,
        GuardianStar.Mercury, GuardianStar.Mars, GuardianStar.Jupiter,
        GuardianStar.Saturn, GuardianStar.Uranus, GuardianStar.Neptune,
        GuardianStar.Pluto
    };

    /// <summary>Devuelve +500 si attackerStar vence a defenderStar, 0 en otro caso.</summary>
    public static int GetGuardianStarBonus(GuardianStar attackerStar, GuardianStar defenderStar)
    {
        int aIdx = System.Array.IndexOf(StarOrder, attackerStar);
        int dIdx = System.Array.IndexOf(StarOrder, defenderStar);
        if (aIdx < 0 || dIdx < 0) return 0;
        // Gana la estrella que está UN paso adelante en el ciclo
        int next = (dIdx + 1) % StarOrder.Length;
        return (aIdx == next) ? 500 : 0;
    }

    // ── ATK final ──────────────────────────────────────────────────────────

    /// <summary>
    /// Calcula el ATK total de un monstruo considerando todos los modificadores.
    /// </summary>
    public static int CalculateAtk(
        CardData card,
        GuardianStar activeStar,
        GuardianStar opponentStar,
        TerrainType terrain,
        int equipBonus = 0)
    {
        int atk = card.baseAtk;
        atk += GetTerrainBonus(card, terrain);
        atk += GetGuardianStarBonus(activeStar, opponentStar);
        atk += equipBonus;
        return atk;
    }

    // ── DEF final ──────────────────────────────────────────────────────────

    /// <summary>
    /// Calcula el DEF total de un monstruo. A diferencia del ATK, el terreno
    /// y los Guardian Stars no bonifican DEF en este juego — solo el DEF base
    /// y el bonus de equipos aplicados durante la fusión.
    /// </summary>
    public static int CalculateDef(CardData card, int equipDefBonus = 0)
    {
        return card.baseDef + equipDefBonus;
    }
}