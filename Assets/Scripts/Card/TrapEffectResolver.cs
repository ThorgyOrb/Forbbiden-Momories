using UnityEngine;

/// <summary>
/// Ejecuta el efecto de una carta de TRAMPA al activarse. Análogo a
/// <see cref="SpellEffectResolver"/>, pero las trampas se disparan por EVENTOS del
/// duelo (ver <see cref="TrapTrigger"/>) desde DuelController.CheckTraps.
///
/// Convención de parámetros:
///   • owner  = duelista dueño de la trampa (quien la tenía seteada).
///   • foe    = quien PROVOCÓ el evento (el atacante o el invocador); es sobre su
///              lado donde actúan la mayoría de los efectos.
///   • triggerSlot = slot del monstruo de "foe" implicado en el disparo
///              (el atacante o el recién invocado), o -1 si no aplica.
/// </summary>
public static class TrapEffectResolver
{
    /// <summary>Resultado de resolver una trampa (para que el flujo del duelo reaccione).</summary>
    public struct Result
    {
        public string message;                // texto para el log
        public bool negatedAction;            // el ataque/invocación queda anulado
        public bool destroyedTriggerMonster;  // se destruyó el monstruo que disparó
    }

    public static Result Resolve(CardData trap, Duelist owner, Duelist foe, int triggerSlot)
    {
        var r = new Result();
        if (trap == null || !trap.IsTrap) { r.message = "Trampa inválida."; return r; }

        switch (trap.trapEffect)
        {
            // Destruye al monstruo que disparó (el atacante o el recién invocado).
            case TrapEffectType.DestroyAttackingMonster:   // p. ej. respuesta a un ataque
            case TrapEffectType.DestroySummonedMonster:    // Trap Hole (respuesta a invocación)
            {
                string n = DestroyAt(foe, triggerSlot);
                if (n != null)
                {
                    r.destroyedTriggerMonster = true;
                    r.negatedAction = true;   // sin monstruo, no hay ataque/invocación efectiva
                    r.message = $"¡{trap.cardName}! Destruye a {n}.";
                }
                else r.message = $"{trap.cardName}: no hay monstruo objetivo.";
                break;
            }

            // Mirror Force: destruye TODOS los monstruos de "foe" en posición de ataque.
            case TrapEffectType.DestroyAllAttackingMonsters:
            {
                int c = 0;
                for (int i = 0; i < 5; i++)
                    if (foe.MonsterZone[i] != null && foe.MonsterPositions[i] == CardPosition.FaceUpAttack)
                    { foe.RemoveMonster(i); c++; }
                r.negatedAction = c > 0;
                r.destroyedTriggerMonster = c > 0;
                r.message = c > 0
                    ? $"¡{trap.cardName}! Destruye {c} monstruo(s) en ataque de {foe.Name}."
                    : $"{trap.cardName}: {foe.Name} no tiene monstruos en ataque.";
                break;
            }

            // Anula la acción sin destruir nada.
            case TrapEffectType.NegateAttack:
            case TrapEffectType.NegateSummon:
                r.negatedAction = true;
                r.message = $"¡{trap.cardName}! Se anula la acción de {foe.Name}.";
                break;

            // Daño directo a los LP de "foe".
            case TrapEffectType.DamageOpponent:
                foe.TakeDamage(trap.trapValue);
                r.message = $"¡{trap.cardName}! {foe.Name} recibe {trap.trapValue} de daño.";
                break;

            // Reduce el ATK de los monstruos de "foe" (aplicación puntual al dispararse).
            case TrapEffectType.ReduceEnemyAtk:
            {
                int aff = 0;
                for (int i = 0; i < 5; i++)
                    if (foe.MonsterZone[i] != null)
                    {
                        foe.MonsterCurrentAtk[i] = Mathf.Max(0, foe.MonsterCurrentAtk[i] - trap.trapValue);
                        aff++;
                    }
                r.message = aff > 0
                    ? $"¡{trap.cardName}! −{trap.trapValue} ATK a {aff} monstruo(s) de {foe.Name}."
                    : $"{trap.cardName}: {foe.Name} no tiene monstruos.";
                break;
            }

            // Destruye una carta seteada (magia/trampa) de "foe".
            case TrapEffectType.DestroyOneSpell:
            {
                string n = DestroyFirstSpell(foe);
                r.message = n != null
                    ? $"¡{trap.cardName}! Destruye la carta seteada de {foe.Name} ({n})."
                    : $"{trap.cardName}: {foe.Name} no tiene cartas seteadas.";
                break;
            }

            // Contra/continuas: se activan, pero su comportamiento PERSISTENTE requiere
            // más reglas (negar activaciones en el momento, estados continuos) todavía
            // no modeladas. Se registran para no romper el flujo.
            case TrapEffectType.NegateSpell:
            case TrapEffectType.NegateTrap:
            case TrapEffectType.PreventDirectAttacks:
            case TrapEffectType.LockPositionChanges:
                r.message = $"{trap.cardName}: efecto continuo/contra aún no implementado.";
                break;

            case TrapEffectType.None:
            default:
                r.message = $"{trap.cardName}: sin efecto programado.";
                break;
        }
        return r;
    }

    private static string DestroyAt(Duelist d, int slot)
    {
        if (slot < 0 || slot >= 5 || d.MonsterZone[slot] == null) return null;
        string n = d.MonsterZone[slot].cardName;
        d.RemoveMonster(slot);
        return n;
    }

    private static string DestroyFirstSpell(Duelist d)
    {
        for (int i = 0; i < 5; i++)
            if (d.SpellZone[i] != null)
            {
                string n = d.SpellZone[i].cardName;
                d.SpellZone[i] = null;
                return n;
            }
        return null;
    }
}
