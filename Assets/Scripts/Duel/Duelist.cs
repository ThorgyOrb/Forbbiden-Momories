using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modelo en-runtime de uno de los dos participantes del duelo.
/// No es MonoBehaviour; es un objeto de datos puro que DuelController instancia.
/// </summary>
public class Duelist
{
    // ── Identidad ──────────────────────────────────────────────
    public string Name { get; }
    public bool IsHuman { get; }

    // ── Life Points ────────────────────────────────────────────
    public int LP { get; private set; } = 8000;
    public bool IsDefeated => LP <= 0;

    // ── Zonas ──────────────────────────────────────────────────
    public List<CardData> Deck { get; } = new();   // cartas sin revelar
    public List<CardData> Hand { get; } = new();   // mano actual
    public CardData[] MonsterZone { get; } = new CardData[5];
    public CardData[] SpellZone { get; } = new CardData[5];

    // Estado visual de cada slot monstruo
    public CardPosition[] MonsterPositions { get; } = new CardPosition[5]
    {
        CardPosition.FaceUpAttack, CardPosition.FaceUpAttack, CardPosition.FaceUpAttack,
        CardPosition.FaceUpAttack, CardPosition.FaceUpAttack
    };

    // ATK actual (base + equips + terreno + guardian)
    public int[] MonsterCurrentAtk { get; } = new int[5];

    // DEF actual (base + equips). El terreno y los Guardian Stars en este
    // juego solo bonifican ATK (igual que en Forbidden Memories), así que
    // DEF únicamente acumula equipBonus de fusión.
    public int[] MonsterCurrentDef { get; } = new int[5];

    // Reglas de turno por monstruo (se resetean al inicio del turno del dueño):
    //   HasAttacked        → cada monstruo solo declara UN ataque por turno.
    //   HasChangedPosition → solo UN cambio de posición por turno, y nunca
    //                        después de haber atacado ese mismo turno.
    public bool[] MonsterHasAttacked { get; } = new bool[5];
    public bool[] MonsterHasChangedPosition { get; } = new bool[5];

    // Estrella Guardiana ELEGIDA al invocar cada monstruo. La ventaja cíclica
    // entre estrellas se evalúa POR BATALLA (atacante vs defensor), no al invocar.
    public GuardianStar[] MonsterStars { get; } = new GuardianStar[5];

    // Estadísticas para rango
    public int DamageTaken { get; private set; }
    public int MonstersDestroyed { get; private set; }
    public int SpellsUsed { get; private set; }
    public int FusionsPerformed { get; private set; }
    public int TurnsPlayed { get; private set; }
    public bool DeckOut { get; private set; }

    public Duelist(string name, bool isHuman)
    {
        Name = name;
        IsHuman = isHuman;
    }

    // ── Life Points ────────────────────────────────────────────

    public void TakeDamage(int amount)
    {
        LP = Mathf.Max(0, LP - amount);
        DamageTaken += amount;
    }

    /// <summary>Cura LP. No hay tope máximo definido en las reglas actuales del juego.</summary>
    public void Heal(int amount)
    {
        LP += Mathf.Max(0, amount);
    }

    // ── Deck ───────────────────────────────────────────────────

    public void LoadDeck(List<CardData> cards)
    {
        Deck.Clear();
        Deck.AddRange(cards);
    }

    /// <summary>Baraja el mazo con Fisher-Yates.</summary>
    public void ShuffleDeck()
    {
        for (int i = Deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (Deck[i], Deck[j]) = (Deck[j], Deck[i]);
        }
    }

    /// <summary>
    /// Roba cartas hasta completar 5 en mano (o hasta vaciar el mazo).
    /// Devuelve las cartas realmente robadas.
    /// </summary>
    public List<CardData> DrawUpToFive()
    {
        var drawn = new List<CardData>();
        while (Hand.Count < 5 && Deck.Count > 0)
        {
            var card = Deck[0];
            Deck.RemoveAt(0);
            Hand.Add(card);
            drawn.Add(card);
        }
        if (Hand.Count < 5 && Deck.Count == 0)
            DeckOut = true;
        return drawn;
    }

    // ── Zona de monstruos ──────────────────────────────────────

    /// <summary>Coloca una carta en el primer slot libre. Devuelve el índice o -1.</summary>
    public int PlaceMonster(CardData card, CardPosition pos, int atk, int def = 0,
                            GuardianStar star = GuardianStar.Sun)
    {
        for (int i = 0; i < 5; i++)
        {
            if (MonsterZone[i] == null)
            {
                MonsterZone[i] = card;
                MonsterPositions[i] = pos;
                MonsterCurrentAtk[i] = atk;
                MonsterCurrentDef[i] = def;
                MonsterStars[i] = star;
                return i;
            }
        }
        return -1;
    }

    /// <summary>Coloca una carta en un slot CONCRETO (elegido con el selector).</summary>
    public bool PlaceMonsterAt(int slot, CardData card, CardPosition pos, int atk, int def = 0,
                               GuardianStar star = GuardianStar.Sun)
    {
        if (slot < 0 || slot >= 5 || MonsterZone[slot] != null) return false;
        MonsterZone[slot] = card;
        MonsterPositions[slot] = pos;
        MonsterCurrentAtk[slot] = atk;
        MonsterCurrentDef[slot] = def;
        MonsterStars[slot] = star;
        return true;
    }

    public void RemoveMonster(int slot)
    {
        MonsterZone[slot] = null;
        MonsterCurrentAtk[slot] = 0;
        MonsterCurrentDef[slot] = 0;
        MonsterHasAttacked[slot] = false;
        MonsterHasChangedPosition[slot] = false;
        MonstersDestroyed++;
    }

    /// <summary>
    /// Saca un monstruo del campo para usarlo como MATERIAL DE FUSIÓN.
    /// A diferencia de RemoveMonster, NO cuenta como "monstruo destruido"
    /// (no infla las estadísticas del rango).
    /// </summary>
    public CardData TakeMonsterForFusion(int slot)
    {
        var card = MonsterZone[slot];
        MonsterZone[slot] = null;
        MonsterCurrentAtk[slot] = 0;
        MonsterCurrentDef[slot] = 0;
        MonsterHasAttacked[slot] = false;
        MonsterHasChangedPosition[slot] = false;
        return card;
    }

    /// <summary>
    /// Resetea los flags por-monstruo al INICIO del turno del dueño:
    /// todos pueden volver a atacar y a cambiar de posición una vez.
    /// </summary>
    public void ResetTurnFlags()
    {
        for (int i = 0; i < 5; i++)
        {
            MonsterHasAttacked[i] = false;
            MonsterHasChangedPosition[i] = false;
        }
    }

    /// <summary>
    /// Cambia la posición/cara de un monstruo ya colocado (ej. al revelarse
    /// en combate, o al voltearse por decisión del dueño). No afecta ATK/DEF.
    /// </summary>
    public void SetMonsterPosition(int slot, CardPosition pos)
    {
        if (slot < 0 || slot >= 5 || MonsterZone[slot] == null) return;
        MonsterPositions[slot] = pos;
    }

    /// <summary>Indica si el monstruo en "slot" está actualmente boca abajo.</summary>
    public bool IsMonsterFaceDown(int slot)
    {
        if (slot < 0 || slot >= 5 || MonsterZone[slot] == null) return false;
        return MonsterPositions[slot] == CardPosition.FaceDownAttack
            || MonsterPositions[slot] == CardPosition.FaceDownDefense;
    }

    /// <summary>
    /// Revela un monstruo boca abajo, dejándolo en FaceUpDefense de forma
    /// permanente (regla: una carta Set que se revela queda en Defensa,
    /// sin importar si estaba en FaceDownAttack o FaceDownDefense).
    /// No hace nada si el monstruo ya estaba boca arriba.
    /// </summary>
    public void RevealMonster(int slot)
    {
        if (!IsMonsterFaceDown(slot)) return;
        MonsterPositions[slot] = CardPosition.FaceUpDefense;
    }

    /// <summary>
    /// Suma "amount" de ATK a todos los monstruos actualmente en campo.
    /// Devuelve cuántos monstruos fueron afectados (0 si el campo está vacío).
    /// </summary>
    public int BuffAllMonsters(int amount)
    {
        int affected = 0;
        for (int i = 0; i < MonsterZone.Length; i++)
        {
            if (MonsterZone[i] == null) continue;
            MonsterCurrentAtk[i] += amount;
            affected++;
        }
        return affected;
    }

    // ── Zona de magias/trampas ─────────────────────────────────

    /// <summary>
    /// Coloca una carta en la zona de Magias/Trampas (la usan las TRAMPAS, que
    /// esperan boca abajo). Las magias normales NO ocupan zona: se activan y
    /// se consumen al instante (regla del juego). Devuelve el slot o -1 si
    /// la zona está llena.
    /// </summary>
    public int PlaceSpell(CardData card)
    {
        for (int i = 0; i < 5; i++)
        {
            if (SpellZone[i] == null)
            {
                SpellZone[i] = card;
                return i;
            }
        }
        return -1;
    }

    /// <summary>Coloca una magia/trampa en un slot CONCRETO de la zona de magias.</summary>
    public bool PlaceSpellAt(int slot, CardData card)
    {
        if (slot < 0 || slot >= 5 || SpellZone[slot] != null) return false;
        SpellZone[slot] = card;
        return true;
    }

    /// <summary>Cuenta una magia usada para las estadísticas del rango.</summary>
    public void RegisterSpell() => SpellsUsed++;

    // ── Fusión ─────────────────────────────────────────────────

    public void RegisterFusion() => FusionsPerformed++;

    // ── Turno ──────────────────────────────────────────────────

    public void EndTurn() => TurnsPlayed++;
}
