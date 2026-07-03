using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modelo en-runtime de uno de los dos participantes del duelo.
/// No es MonoBehaviour; es un objeto de datos puro que DuelManager instancia.
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
    public int PlaceMonster(CardData card, CardPosition pos, int atk, int def = 0)
    {
        for (int i = 0; i < 5; i++)
        {
            if (MonsterZone[i] == null)
            {
                MonsterZone[i] = card;
                MonsterPositions[i] = pos;
                MonsterCurrentAtk[i] = atk;
                MonsterCurrentDef[i] = def;
                return i;
            }
        }
        return -1;
    }

    public void RemoveMonster(int slot)
    {
        MonsterZone[slot] = null;
        MonsterCurrentAtk[slot] = 0;
        MonsterCurrentDef[slot] = 0;
        MonstersDestroyed++;
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

    // ── Zona de magias ─────────────────────────────────────────

    public int PlaceSpell(CardData card)
    {
        for (int i = 0; i < 5; i++)
        {
            if (SpellZone[i] == null)
            {
                SpellZone[i] = card;
                SpellsUsed++;
                return i;
            }
        }
        return -1;
    }

    // ── Fusión ─────────────────────────────────────────────────

    public void RegisterFusion() => FusionsPerformed++;

    // ── Turno ──────────────────────────────────────────────────

    public void EndTurn() => TurnsPlayed++;
}