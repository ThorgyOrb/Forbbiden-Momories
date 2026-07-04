using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Cerebro de la IA. Recibe el estado del duelo y devuelve la acción a ejecutar.
/// Sin MonoBehaviour — DuelManager la llama directamente.
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
        // 1. Fusión — la estrategia Fusion la intenta siempre; el resto solo si
        //    tiene nivel suficiente y no es puramente agresiva.
        bool wantsFusion = _strategy == AIStrategy.Fusion
                        || (_strategy != AIStrategy.Aggressive && _level >= 1);

        if (wantsFusion)
        {
            var (fusedCard, usedCards) = _fusionDb.FindBestFusion(ai.Hand);
            if (fusedCard != null)
                return new AIAction
                {
                    Type = AIActionType.Fuse,
                    FuseResult = fusedCard,
                    FuseMaterials = usedCards,
                    SummonPosition = CardPosition.FaceUpAttack
                };
        }

        // 2. Control — intenta jugar una magia útil antes de invocar.
        if (_strategy == AIStrategy.Control)
        {
            var spell = ai.Hand.FirstOrDefault(c => c.IsSpell);
            if (spell != null)
                return new AIAction { Type = AIActionType.PlaySpell, Card = spell };
        }

        // 3. Invocar el mejor monstruo disponible.
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
                SummonPosition = SummonPositionFor(best)
            };
        }

        // 4. Nada útil que hacer.
        return new AIAction { Type = AIActionType.Pass };
    }

    /// <summary>Posición en que la IA coloca sus invocaciones según su perfil.</summary>
    private CardPosition SummonPositionFor(CardData card)
    {
        return _strategy switch
        {
            AIStrategy.Defensive => CardPosition.FaceUpDefense,
            AIStrategy.Control => CardPosition.FaceUpDefense,
            _ => CardPosition.FaceUpAttack
        };
    }

    // ── Fase de Batalla ───────────────────────────────────────────────────

    /// <summary>
    /// Devuelve pares (slotAtacante, slotObjetivo) para la fase de batalla.
    /// Ataca al monstruo enemigo con menor ATK (o directamente si no hay). Los
    /// perfiles Defensive/Control solo atacan cuando es claramente favorable.
    /// </summary>
    public List<(int atkSlot, int defSlot)> DecideAttacks(
        Duelist ai, Duelist player, TerrainType terrain)
    {
        var attacks = new List<(int, int)>();

        for (int i = 0; i < 5; i++)
        {
            if (ai.MonsterZone[i] == null) continue;
            if (ai.MonsterPositions[i] == CardPosition.FaceUpDefense) continue;
            if (ai.IsMonsterFaceDown(i)) continue; // un monstruo boca abajo no ataca

            // ¿Hay monstruos enemigos?
            bool hasTargets = player.MonsterZone.Any(m => m != null);
            if (!hasTargets)
            {
                attacks.Add((i, -1));   // -1 = ataque directo al jugador
                continue;
            }

            // Busca el objetivo con menor ATK
            int bestTarget = -1;
            int lowestAtk  = int.MaxValue;
            for (int j = 0; j < 5; j++)
            {
                if (player.MonsterZone[j] == null) continue;
                if (player.MonsterCurrentAtk[j] < lowestAtk)
                {
                    lowestAtk  = player.MonsterCurrentAtk[j];
                    bestTarget = j;
                }
            }
            if (bestTarget < 0) continue;

            // Defensive/Control no arriesgan: solo atacan si ganan con claridad.
            if (_strategy == AIStrategy.Defensive || _strategy == AIStrategy.Control)
            {
                if (ai.MonsterCurrentAtk[i] <= lowestAtk) continue;
            }

            attacks.Add((i, bestTarget));
        }
        return attacks;
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
    Control      // usa magias y desgasta al rival
}

// ── DTOs de acción ─────────────────────────────────────────────────────────

public enum AIActionType { Pass, Summon, Fuse, PlaySpell }

public class AIAction
{
    public AIActionType      Type;
    public CardData          Card;
    public CardData          FuseResult;
    public List<CardData>    FuseMaterials;
    public CardPosition      SummonPosition = CardPosition.FaceUpAttack;
}
