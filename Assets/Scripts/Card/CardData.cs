using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "YGO/Card Data")]
public class CardData : ScriptableObject
{
    [Header("Identidad")]
    public int cardId;
    public string cardName;
    public Sprite artwork;

    [Header("Categoría de carta")]
    public CardCategory cardCategory = CardCategory.Monster;

    [Header("Monstruo (solo si cardCategory = Monster)")]
    public MonsterType monsterType;
    public int baseAtk;
    public int baseDef;
    public int stars;
    public GuardianStar starA;
    public GuardianStar starB;
    public CardAttribute attribute;

    [Tooltip("Prefab del modelo 3D para el visor de monstruo (Model3DViewer). " +
             "Null si la carta aún no está modelada o si no es Monster.")]
    public GameObject monsterModelPrefab;

    [Header("Rareza")]
    public CardRarity rarity;

    [Header("Fusión")]
    // Grupo de fusión INDEPENDIENTE de monsterType, usado por las recetas
    // por categoría (ej. "Dragon" + "Thunder" = resultado). Vacío = no participa.
    public string fusionGroup = "";

    [Header("Equipo (solo si cardCategory = Equip)")]
    // Bonus de ATK que aporta esta carta al equiparse a un monstruo en una
    // cadena de fusión. Solo se usa si cardCategory = Equip.
    public int equipAtkBonus = 0;
    public int equipDefBonus = 0;

    [Header("Magia (solo si cardCategory = Spell)")]
    public SpellEffectType spellEffect = SpellEffectType.None;
    public int spellValue = 0;          // ej: cuánto LP cura/daña, cuánto ATK sube, etc.
    [TextArea] public string spellDescription = "";

    [Header("Fuentes de obtención (catálogo, no progreso)")]
    public List<CardSourceEntry> sources = new();

    // ── Helpers ────────────────────────────────────────────────────────
    public bool IsMonster => cardCategory == CardCategory.Monster;
    public bool IsSpell => cardCategory == CardCategory.Spell;
    public bool IsEquip => cardCategory == CardCategory.Equip;
}

public enum CardCategory
{
    Monster,
    Spell,
    Equip
}

public enum MonsterType
{
    Dragon, Spellcaster, Fiend, Beast, Insect,
    Plant, Fish, Aqua, SeaSerpent, Zombie,
    Dinosaur, WingedBeast, Warrior, Machine, Thunder
}

public enum GuardianStar
{
    Sun, Moon, Venus, Mercury,
    Mars, Jupiter, Saturn, Uranus, Neptune, Pluto
}

public enum CardRarity
{
    Common, Rare, Epic, Legendary
}

public enum CardPosition
{
    FaceUpAttack,
    FaceUpDefense,
    FaceDownAttack,
    FaceDownDefense
}

public enum CardAttribute
{
    Dark, Light, Fire, Water, Earth, Wind
}

/// <summary>
/// Tipos de efecto básico para cartas mágicas normales (no de equipo).
/// Cada tipo usa "spellValue" de CardData de forma distinta — ver SpellEffectResolver.
/// </summary>
public enum SpellEffectType
{
    None,
    HealLP,             // cura spellValue de LP al jugador que la usa
    DamageOpponentLP,   // resta spellValue de LP al oponente directamente
    BuffAtkAllMonsters, // suma spellValue de ATK a todos los monstruos propios en campo
    DestroyWeakestEnemyMonster // destruye el monstruo enemigo con menor ATK en campo
}