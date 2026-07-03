using System.Linq;

/// <summary>
/// Ejecuta el efecto de una carta mágica (Spell) normal al jugarla.
/// No es MonoBehaviour — DuelManager lo llama directamente, igual que DuelAI.
///
/// Las cartas de Equipo NO pasan por aquí — se resuelven dentro de la cadena
/// de fusión (ver FusionDatabase.ResolveStep, caso FusionStepType.Equip),
/// porque equipar es conceptualmente "fusionar" el equipo con un monstruo.
/// </summary>
public static class SpellEffectResolver
{
    /// <summary>
    /// Aplica el efecto de "card" sobre el dueño (caster) y su rival (target).
    /// Devuelve un mensaje listo para loguear en la UI describiendo qué pasó.
    /// </summary>
    public static string Resolve(CardData card, Duelist caster, Duelist target)
    {
        if (card == null || !card.IsSpell) return $"{card?.cardName} no es una carta mágica válida.";

        switch (card.spellEffect)
        {
            case SpellEffectType.HealLP:
                caster.Heal(card.spellValue);
                return $"{card.cardName}: {caster.Name} recupera {card.spellValue} LP.";

            case SpellEffectType.DamageOpponentLP:
                target.TakeDamage(card.spellValue);
                return $"{card.cardName}: {target.Name} recibe {card.spellValue} de daño directo.";

            case SpellEffectType.BuffAtkAllMonsters:
                int buffed = caster.BuffAllMonsters(card.spellValue);
                return buffed > 0
                    ? $"{card.cardName}: todos los monstruos de {caster.Name} ganan +{card.spellValue} ATK."
                    : $"{card.cardName}: no hay monstruos en campo para potenciar.";

            case SpellEffectType.DestroyWeakestEnemyMonster:
                var destroyedName = DestroyWeakest(target);
                return destroyedName != null
                    ? $"{card.cardName}: destruye a {destroyedName} del lado de {target.Name}."
                    : $"{card.cardName}: {target.Name} no tiene monstruos que destruir.";

            case SpellEffectType.None:
            default:
                return $"{card.cardName} no tiene ningún efecto programado todavía.";
        }
    }

    private static string DestroyWeakest(Duelist target)
    {
        int bestSlot = -1;
        int lowestAtk = int.MaxValue;
        for (int i = 0; i < target.MonsterZone.Length; i++)
        {
            if (target.MonsterZone[i] == null) continue;
            if (target.MonsterCurrentAtk[i] < lowestAtk)
            {
                lowestAtk = target.MonsterCurrentAtk[i];
                bestSlot = i;
            }
        }
        if (bestSlot < 0) return null;

        string name = target.MonsterZone[bestSlot].cardName;
        target.RemoveMonster(bestSlot);
        return name;
    }
}
