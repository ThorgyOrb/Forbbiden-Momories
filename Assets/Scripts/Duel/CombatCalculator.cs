using System.Collections.Generic;

/// <summary>
/// Calcula bonificaciones de ATK/DEF según terreno, equips y Guardian Stars.
/// Stateless: todos los métodos son estáticos.
/// </summary>
public static class CombatCalculator
{
    // ── Terreno ────────────────────────────────────────────────────────────

    private static readonly Dictionary<TerrainType, (MonsterType[] buffed, int bonus)> TerrainBonuses
        = new()
    {
        { TerrainType.Forest,    (new[] { MonsterType.Beast, MonsterType.Insect, MonsterType.Plant }, 500) },
        { TerrainType.Mountain,  (new[] { MonsterType.Dragon, MonsterType.WingedBeast, MonsterType.Thunder }, 500) },
        { TerrainType.Sea,       (new[] { MonsterType.Fish, MonsterType.Aqua, MonsterType.SeaSerpent }, 500) },
        { TerrainType.Wasteland, (new[] { MonsterType.Zombie, MonsterType.Dinosaur }, 500) },
        { TerrainType.Meadow,    (new[] { MonsterType.Warrior, MonsterType.Spellcaster }, 500) },
        { TerrainType.Yami,      (new[] { MonsterType.Fiend, MonsterType.Spellcaster }, 500) },
        { TerrainType.Dark,      (new[] { MonsterType.Fiend }, 500) },
    };

    public static int GetTerrainBonus(CardData card, TerrainType terrain)
    {
        if (!TerrainBonuses.TryGetValue(terrain, out var entry)) return 0;
        foreach (var t in entry.buffed)
            if (card.monsterType == t) return entry.bonus;
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