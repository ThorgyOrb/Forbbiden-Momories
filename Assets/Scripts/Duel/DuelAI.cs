using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Cerebro de la IA. Recibe el estado del duelo y devuelve la acción a ejecutar.
/// Sin MonoBehaviour — DuelController la llama directamente.
///
/// El COMPORTAMIENTO depende del perfil <see cref="AIStrategy"/> del oponente:
///   • Aggressive — invoca el más fuerte en ataque y ataca siempre que puede.
///   • Defensive  — invoca en defensa y solo ataca cuando es claramente favorable.
///   • Fusion     — prioriza fusionar por encima de todo.
///   • Control    — juega magias y se mantiene defensivo.
///   • Balanced   — mezcla ataque, defensa y fusión según la situación.
///
/// El nivel (0-3) sigue midiendo la "competencia" (p. ej. si intenta fusionar).
/// </summary>
public class DuelAI
{
    private readonly int _level;             // 0-3
    private readonly AIStrategy _strategy;
    private readonly FusionDatabase _fusionDb;

    public DuelAI(int level, AIStrategy strategy, FusionDatabase fusionDb)
    {
        _level    = level;
        _strategy = strategy;
        _fusionDb = fusionDb;
    }

    // ── Acción Principal ───────────────────────────────────────────────────

    public AIAction DecideMainAction(Duelist ai, Duelist player, TerrainType terrain)
    {
        // Perfil MASTER: usa TODAS las herramientas (fusión, invocación, equipar sus
        // propios monstruos del campo, magias) con criterio.
        if (_strategy == AIStrategy.Master)
            return DecideMasterAction(ai, player, terrain);

        // La IA fusiona/invoca cartas de la MANO y coloca el resultado en el campo, así
        // que ambas cosas NECESITAN un hueco libre en la zona de monstruos. Sin hueco (o
        // sin monstruos en mano) NO debe quedarse quieta: juega una magia útil si puede.
        bool hasFreeSlot = HasFreeSlot(ai.MonsterZone);
        bool wantsFusion = _strategy == AIStrategy.Fusion
                        || (_strategy != AIStrategy.Aggressive && _level >= 1);

        // 1. Fusión (solo con hueco: el resultado va al campo).
        if (wantsFusion && hasFreeSlot)
        {
            var (fusedCard, usedCards) = _fusionDb.FindBestFusion(ai.Hand);
            if (fusedCard != null && usedCards != null && usedCards.Count >= 2)
                return new AIAction
                {
                    Type = AIActionType.Fuse,
                    FuseResult = fusedCard,
                    FuseMaterials = usedCards,
                    SummonPosition = CardPosition.FaceUpAttack
                };
        }

        // 2. Control — juega una magia útil ANTES de invocar, pero solo si YA tiene
        //    monstruos en campo (si no, invoca primero para no quedarse indefenso).
        if (_strategy == AIStrategy.Control && ai.MonsterZone.Any(m => m != null))
        {
            var spell = PickUsefulSpell(ai, player);
            if (spell != null)
                return new AIAction { Type = AIActionType.PlaySpell, Card = spell };
        }

        // 3. Invocar el mejor monstruo disponible (si hay hueco).
        if (hasFreeSlot)
        {
            var monsters = ai.Hand.Where(c => c.IsMonster).ToList();
            if (monsters.Count > 0)
            {
                var best = monsters
                    .OrderByDescending(c => CombatCalculator.CalculateAtk(c, c.starA, GuardianStar.Sun, terrain))
                    .First();
                return new AIAction
                {
                    Type = AIActionType.Summon,
                    Card = best,
                    SummonPosition = SummonPositionFor(best, player, terrain)
                };
            }
        }

        // 4. No pudo invocar/fusionar (campo lleno o sin monstruos): juega una magia útil
        //    (cualquier perfil) para no quedarse pasivo en fases avanzadas.
        var fallbackSpell = PickUsefulSpell(ai, player);
        if (fallbackSpell != null)
            return new AIAction { Type = AIActionType.PlaySpell, Card = fallbackSpell };

        // 5. Nada útil que hacer.
        return new AIAction { Type = AIActionType.Pass };
    }

    /// <summary>
    /// MASTER — el rival "hace de todo", en orden de mayor a menor swing:
    ///   1) Fusión real (si hay hueco).
    ///   2) Invocar (construir tablero si hay hueco y monstruo).
    ///   3) EQUIPAR un monstruo del campo (potenciar; clave cuando el campo está lleno).
    ///   4) Magia útil.
    ///   5) Pasar (solo si de verdad no hay nada).
    /// Ataca con criterio (umbral Balanced) — ver DecideAttacks.
    /// </summary>
    private AIAction DecideMasterAction(Duelist ai, Duelist player, TerrainType terrain)
    {
        bool hasFreeSlot = HasFreeSlot(ai.MonsterZone);
        bool hasBoard = ai.MonsterZone.Any(m => m != null);

        // 1. Fusión real (mayor impacto).
        if (hasFreeSlot)
        {
            var (fused, used) = _fusionDb.FindBestFusion(ai.Hand);
            if (fused != null && used != null && used.Count >= 2)
                return new AIAction
                {
                    Type = AIActionType.Fuse,
                    FuseResult = fused,
                    FuseMaterials = used,
                    SummonPosition = CardPosition.FaceUpAttack
                };
        }

        // 2. Invocar el mejor monstruo (construir tablero).
        if (hasFreeSlot)
        {
            var monsters = ai.Hand.Where(c => c.IsMonster).ToList();
            if (monsters.Count > 0)
            {
                var best = monsters
                    .OrderByDescending(c => CombatCalculator.CalculateAtk(c, c.starA, GuardianStar.Sun, terrain))
                    .First();
                return new AIAction
                {
                    Type = AIActionType.Summon,
                    Card = best,
                    SummonPosition = SummonPositionFor(best, player, terrain)
                };
            }
        }

        // 3. Equipar un monstruo del campo con un equipo aplicable de la mano.
        if (hasBoard)
        {
            var equipAction = TryEquipFieldMonster(ai);
            if (equipAction != null) return equipAction;
        }

        // 4. Magia útil.
        var spell = PickUsefulSpell(ai, player);
        if (spell != null)
            return new AIAction { Type = AIActionType.PlaySpell, Card = spell };

        // 5. Nada útil.
        return new AIAction { Type = AIActionType.Pass };
    }

    /// <summary>
    /// Busca un EQUIPO en la mano que aplique a algún monstruo propio del campo (respetando
    /// la restricción por tipo) y que dé bonus; lo asigna al monstruo de mayor ATK.
    /// Devuelve la acción Equip o null.
    /// </summary>
    private AIAction TryEquipFieldMonster(Duelist ai)
    {
        foreach (var equip in ai.Hand)
        {
            if (equip == null || !equip.IsEquip) continue;
            if (equip.equipAtkBonus <= 0 && equip.equipDefBonus <= 0) continue;

            int bestSlot = -1, bestAtk = -1;
            for (int s = 0; s < 5; s++)
            {
                var m = ai.MonsterZone[s];
                if (m == null) continue;
                if (!equip.EquipAppliesTo(m)) continue;
                if (ai.MonsterCurrentAtk[s] > bestAtk) { bestAtk = ai.MonsterCurrentAtk[s]; bestSlot = s; }
            }
            if (bestSlot >= 0)
                return new AIAction { Type = AIActionType.Equip, Card = equip, TargetSlot = bestSlot };
        }
        return null;
    }

    private static bool HasFreeSlot(CardData[] zone)
    {
        for (int i = 0; i < zone.Length; i++)
            if (zone[i] == null) return true;
        return false;
    }

    /// <summary>Elige una magia de la mano que sea ÚTIL en la situación actual (o null).</summary>
    private static CardData PickUsefulSpell(Duelist ai, Duelist player)
    {
        bool playerHasMonsters = player.MonsterZone.Any(m => m != null);
        bool aiHasMonsters = ai.MonsterZone.Any(m => m != null);

        foreach (var c in ai.Hand)
        {
            if (c == null || !c.IsSpell) continue;
            switch (c.spellEffect)
            {
                case SpellEffectType.DamageOpponentLP:
                case SpellEffectType.HealLP:
                    return c;                                   // siempre útil
                case SpellEffectType.BuffAtkAllMonsters:
                    if (aiHasMonsters) return c;                // solo con monstruos propios
                    break;
                case SpellEffectType.DestroyWeakestEnemyMonster:
                case SpellEffectType.DestroyAllEnemyMonsters:
                    if (playerHasMonsters) return c;            // solo si hay enemigos
                    break;
                default:
                    if (c.IsFieldSpell) return c;               // cambiar terreno (situacional)
                    break;
            }
        }
        return null;
    }

    /// <summary>
    /// Posición de invocación: los perfiles defensivos siempre en Defensa; el agresivo
    /// siempre en Ataque; los demás en Defensa si el monstruo no supera al mejor enemigo
    /// y tiene mejor DEF que ATK (para no regalarlo).
    /// </summary>
    private CardPosition SummonPositionFor(CardData card, Duelist player, TerrainType terrain)
    {
        if (_strategy == AIStrategy.Defensive || _strategy == AIStrategy.Control)
            return CardPosition.FaceUpDefense;
        if (_strategy == AIStrategy.Aggressive)
            return CardPosition.FaceUpAttack;

        int myAtk = CombatCalculator.CalculateAtk(card, card.starA, GuardianStar.Sun, terrain);
        int enemyBest = 0;
        for (int j = 0; j < 5; j++)
            if (player.MonsterZone[j] != null)
                enemyBest = System.Math.Max(enemyBest, player.MonsterCurrentAtk[j]);

        // Si no le gano al mejor enemigo y defiendo mejor, en Defensa.
        if (myAtk <= enemyBest && card.baseDef > myAtk)
            return CardPosition.FaceUpDefense;
        return CardPosition.FaceUpAttack;
    }

    // ── Fase de Batalla ───────────────────────────────────────────────────

    /// <summary>
    /// Devuelve pares (slotAtacante, slotObjetivo) para la fase de batalla. Para CADA
    /// monstruo evalúa el mejor objetivo por RESULTADO de combate (con ventaja de estrella
    /// y posición del defensor) y SOLO ataca si conviene según el perfil — ya no ataca a
    /// ciegas al de menor ATK. Ataque directo (-1) solo si el rival no tiene monstruos.
    /// </summary>
    public List<(int atkSlot, int defSlot)> DecideAttacks(
        Duelist ai, Duelist player, TerrainType terrain)
    {
        var attacks = new List<(int, int)>();
        bool playerHasMonsters = player.MonsterZone.Any(m => m != null);
        int threshold = AttackThreshold();

        for (int i = 0; i < 5; i++)
        {
            if (ai.MonsterZone[i] == null) continue;
            if (ai.MonsterPositions[i] != CardPosition.FaceUpAttack) continue; // solo boca-arriba en Ataque
            if (ai.MonsterHasAttacked[i]) continue;

            if (!playerHasMonsters)
            {
                attacks.Add((i, -1));   // ataque directo (el controlador valida el turno 1)
                continue;
            }

            // Mejor objetivo por RESULTADO de combate (no por menor ATK).
            int bestTarget = -1;
            int bestScore = int.MinValue;
            for (int j = 0; j < 5; j++)
            {
                if (player.MonsterZone[j] == null) continue;
                int score = EvaluateAttack(ai, i, player, j);
                if (score > bestScore) { bestScore = score; bestTarget = j; }
            }

            // Solo ataca si el mejor resultado alcanza el umbral del perfil.
            if (bestTarget >= 0 && bestScore >= threshold)
                attacks.Add((i, bestTarget));
        }
        return attacks;
    }

    /// <summary>Qué tan favorable debe ser un ataque para lanzarlo (según el perfil).</summary>
    private int AttackThreshold() => _strategy switch
    {
        AIStrategy.Aggressive => -25,   // ataca salvo pérdida clara (acepta empates)
        AIStrategy.Defensive  => 50,    // solo victorias claras
        AIStrategy.Control    => 50,
        _                     => 1,     // Balanced/Fusion: solo con ventaja real
    };

    /// <summary>
    /// Puntúa atacar (atkSlot) al monstruo (defSlot): &gt;0 favorable, &lt;0 desfavorable.
    /// Considera la ventaja de Estrella Guardiana y si el defensor está en Defensa.
    /// </summary>
    private int EvaluateAttack(Duelist ai, int atkSlot, Duelist player, int defSlot)
    {
        int atkBonus = CombatCalculator.GetGuardianStarBonus(
            ai.MonsterStars[atkSlot], player.MonsterStars[defSlot]);
        int effAtk = ai.MonsterCurrentAtk[atkSlot] + atkBonus;

        bool inDefense = player.MonsterPositions[defSlot] == CardPosition.FaceUpDefense
                      || player.IsMonsterFaceDown(defSlot);

        if (inDefense)
        {
            int def = player.MonsterCurrentDef[defSlot];
            if (effAtk > def) return 60;        // destruye al defensor, sin daño → bueno
            if (effAtk == def) return -10;      // sin efecto → ataque desperdiciado
            return effAtk - def;                // negativo: la IA recibe (def - atk) de daño
        }

        int defBonus = CombatCalculator.GetGuardianStarBonus(
            player.MonsterStars[defSlot], ai.MonsterStars[atkSlot]);
        int enemyEff = player.MonsterCurrentAtk[defSlot] + defBonus;

        if (effAtk > enemyEff) return 100 + (effAtk - enemyEff) / 20; // gana y hace daño
        if (effAtk == enemyEff) return -20;                            // empate: ambos mueren
        return -100 + (effAtk - enemyEff) / 20;                        // pierde: penalización fuerte
    }

    // ── Posición de batalla (Ataque / Defensa) ─────────────────────────────

    /// <summary>
    /// Decide en qué posición conviene CADA monstruo propio ANTES de atacar: ATAQUE si
    /// puede atacar con provecho este turno (o hay ataque directo), DEFENSA si no (así, si
    /// lo destruyen, no pierde LP y bloquea). Un "muro" (DEF &gt; ATK) solo pasa a Ataque
    /// ante una victoria contundente. Devuelve SOLO los cambios (posición != actual).
    /// Aplica a TODOS los perfiles, con el umbral de ataque de cada uno.
    /// </summary>
    public List<(int slot, CardPosition pos)> DecidePositions(
        Duelist ai, Duelist player, TerrainType terrain)
    {
        var changes = new List<(int, CardPosition)>();
        bool playerHasMonsters = player.MonsterZone.Any(m => m != null);
        int threshold = AttackThreshold();

        for (int i = 0; i < 5; i++)
        {
            if (ai.MonsterZone[i] == null) continue;
            if (ai.IsMonsterFaceDown(i)) continue;    // boca abajo: no se toca
            if (ai.MonsterHasAttacked[i]) continue;    // ya atacó: no cambia de posición

            CardPosition ideal;
            if (!playerHasMonsters)
            {
                ideal = CardPosition.FaceUpAttack;     // hay ataque directo → conviene atacar
            }
            else
            {
                int best = int.MinValue;
                for (int j = 0; j < 5; j++)
                {
                    if (player.MonsterZone[j] == null) continue;
                    int s = EvaluateAttack(ai, i, player, j);
                    if (s > best) best = s;
                }
                bool wall = ai.MonsterCurrentDef[i] > ai.MonsterCurrentAtk[i];
                int need = wall ? 100 : threshold;     // el muro solo ataca por victoria clara
                ideal = (best >= need) ? CardPosition.FaceUpAttack : CardPosition.FaceUpDefense;
            }

            if (ideal != ai.MonsterPositions[i])
                changes.Add((i, ideal));
        }
        return changes;
    }

    // ── Selección Guardian Star ────────────────────────────────────────────

    /// <summary>
    /// La IA elige la estrella que da ventaja sobre el monstruo enemigo más fuerte.
    /// </summary>
    public GuardianStar ChooseGuardianStar(CardData card, Duelist player)
    {
        var strongestEnemy = player.MonsterZone
            .Where(m => m != null)
            .OrderByDescending(m => m.baseAtk)
            .FirstOrDefault();

        if (strongestEnemy == null) return card.starA;

        int bonusA = CombatCalculator.GetGuardianStarBonus(card.starA, strongestEnemy.starA);
        int bonusB = CombatCalculator.GetGuardianStarBonus(card.starB, strongestEnemy.starA);
        return (bonusA >= bonusB) ? card.starA : card.starB;
    }
}

// ── Perfil de comportamiento de la IA ───────────────────────────────────────

public enum AIStrategy
{
    Balanced,    // mezcla ataque, defensa y fusión
    Aggressive,  // invoca al más fuerte y ataca sin parar
    Defensive,   // invoca en defensa y espera
    Fusion,      // busca fusionar constantemente
    Control,     // usa magias y desgasta al rival
    // ── Añadido (append-only: se serializa en OpponentData) ──
    Master       // hace de TODO: fusiona, invoca, equipa sus monstruos, magias y ataca con criterio
}

// ── DTOs de acción ─────────────────────────────────────────────────────────

public enum AIActionType { Pass, Summon, Fuse, PlaySpell, Equip }

public class AIAction
{
    public AIActionType      Type;
    public CardData          Card;
    public CardData          FuseResult;
    public List<CardData>    FuseMaterials;
    public CardPosition      SummonPosition = CardPosition.FaceUpAttack;
    public int               TargetSlot = -1;   // Equip: casilla del monstruo propio a equipar
}
