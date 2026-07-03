using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Cerebro de la IA. Recibe el estado del duelo y devuelve la acción a ejecutar.
/// Sin MonoBehaviour — DuelManager la llama directamente.
/// </summary>
public class DuelAI
{
    private readonly int _level;             // 0-3
    private readonly FusionDatabase _fusionDb;

    public DuelAI(int level, FusionDatabase fusionDb)
    {
        _level    = level;
        _fusionDb = fusionDb;
    }

    // ── Acción Principal ───────────────────────────────────────────────────

    public AIAction DecideMainAction(Duelist ai, Duelist player, TerrainType terrain)
    {
        // 1. Intentar fusión si hay recetas disponibles
        if (_level >= 1)
        {
            var (fusedCard, usedCards) = _fusionDb.FindBestFusion(ai.Hand);
            if (fusedCard != null)
                return new AIAction { Type = AIActionType.Fuse, FuseResult = fusedCard, FuseMaterials = usedCards };
        }

        // 2. Invocar el monstruo con mayor ATK efectivo de la mano
        var monsters = ai.Hand.Where(c => c.baseAtk > 0).ToList();
        if (monsters.Count > 0)
        {
            var best = monsters.OrderByDescending(c =>
                CombatCalculator.CalculateAtk(c, c.starA, GuardianStar.Sun, terrain)).First();
            return new AIAction { Type = AIActionType.Summon, Card = best };
        }

        // 3. Nada útil que hacer
        return new AIAction { Type = AIActionType.Pass };
    }

    // ── Fase de Batalla ───────────────────────────────────────────────────

    /// <summary>
    /// Devuelve pares (slotAtacante, slotObjetivo) para la fase de batalla.
    /// La IA ataca siempre al monstruo enemigo con menor ATK (o directamente si el campo está vacío).
    /// </summary>
    public List<(int atkSlot, int defSlot)> DecideAttacks(
        Duelist ai, Duelist player, TerrainType terrain)
    {
        var attacks = new List<(int, int)>();

        for (int i = 0; i < 5; i++)
        {
            if (ai.MonsterZone[i] == null) continue;
            if (ai.MonsterPositions[i] == CardPosition.FaceUpDefense) continue;

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
            if (bestTarget >= 0)
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

// ── DTOs de acción ─────────────────────────────────────────────────────────

public enum AIActionType { Pass, Summon, Fuse, PlaySpell }

public class AIAction
{
    public AIActionType      Type;
    public CardData          Card;
    public CardData          FuseResult;
    public List<CardData>    FuseMaterials;
}
