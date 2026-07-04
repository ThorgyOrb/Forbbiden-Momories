using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Datos de catálogo de UNA carta (no el progreso del jugador — eso vive en
/// PlayerCollection). Una sola clase cubre TODAS las categorías; según
/// <see cref="cardCategory"/> se usan unos campos u otros. Los helpers de abajo
/// (IsMonster, IsSpell, …) dicen qué bloque aplica.
///
/// Categorías soportadas:
///   • Monster  — unidad de combate (ATK/DEF, nivel, tipo, atributo, Guardian Stars).
///   • Spell    — magia normal o de terreno (ver spellKind).
///   • Equip    — magia de equipo; también sirve de material de fusión (bonus ATK/DEF).
///   • Ritual   — invoca un monstruo especial si se aportan los materiales.
///   • Special  — comportamiento único (cambiar star, copiar, transformar, …).
/// </summary>
[CreateAssetMenu(fileName = "NewCard", menuName = "YGO/Card Data")]
public class CardData : ScriptableObject
{
    // ── Identidad (TODAS las cartas) ─────────────────────────────────────
    [Header("Identidad (todas las cartas)")]
    public int cardId;
    public string cardName;
    public Sprite artwork;
    public CardRarity rarity;
    public CardCategory cardCategory = CardCategory.Monster;

    [Tooltip("Atributo/elemento. Aplica sobre todo a monstruos; opcional en otras categorías.")]
    public CardAttribute attribute;

    [TextArea]
    [Tooltip("Descripción/efecto de la carta, mostrada en el detalle y en el campo.")]
    public string description = "";

    // ── Clasificación / fusión (todas) ───────────────────────────────────
    [Header("Clasificación / fusión (todas)")]
    [Tooltip("Familia usada por las recetas de fusión POR CATEGORÍA (ej. \"Dragon\", " +
             "\"Thunder\"). Independiente de monsterType. Vacío = no participa por categoría.")]
    public string fusionGroup = "";

    // ── Monstruo ─────────────────────────────────────────────────────────
    [Header("Monstruo (si cardCategory = Monster)")]
    public MonsterType monsterType;
    public int baseAtk;
    public int baseDef;
    [Tooltip("Nivel del monstruo (estrellas de nivel, NO Guardian Stars).")]
    public int stars;
    public GuardianStar starA;
    public GuardianStar starB;

    [Tooltip("Terreno favorito del monstruo. Si coincide con el terreno activo, recibe el " +
             "bonus aunque su tipo no esté en la tabla del terreno. Neutral = sin favorito.")]
    public TerrainType favoriteTerrain = TerrainType.Neutral;

    [Tooltip("Prefab del modelo 3D para el visor (Model3DViewer). Null si no está modelado o no es Monster.")]
    public GameObject monsterModelPrefab;

    // ── Magia (Spell) ────────────────────────────────────────────────────
    [Header("Magia (si cardCategory = Spell)")]
    [Tooltip("Normal = efecto inmediato. Field = magia de terreno (cambia el escenario).")]
    public SpellKind spellKind = SpellKind.Normal;

    [Tooltip("Efecto de una magia Normal — ver SpellEffectResolver.")]
    public SpellEffectType spellEffect = SpellEffectType.None;

    [Tooltip("Valor genérico del efecto (LP curados/daño, ATK ganado, etc.).")]
    public int spellValue = 0;

    [Tooltip("Terreno al que cambia el escenario (solo si spellKind = Field).")]
    public TerrainType fieldTerrain = TerrainType.Neutral;

    [TextArea]
    [Tooltip("(Opcional/legado) Texto propio del efecto de la magia. Si 'description' está " +
             "vacío se usa este como respaldo.")]
    public string spellDescription = "";

    // ── Equipo ───────────────────────────────────────────────────────────
    [Header("Equipo (si cardCategory = Equip)")]
    [Tooltip("Bonus que aporta al equiparse (o al usarse como material de fusión de equipo).")]
    public int equipAtkBonus = 0;
    public int equipDefBonus = 0;

    // ── Ritual ───────────────────────────────────────────────────────────
    [Header("Ritual (si cardCategory = Ritual)")]
    [Tooltip("Cartas requeridas para completar el ritual.")]
    public List<CardData> ritualMaterials = new();
    [Tooltip("Monstruo especial que se invoca al cumplir el ritual.")]
    public CardData ritualResult;

    // ── Especial ─────────────────────────────────────────────────────────
    [Header("Especial (si cardCategory = Special)")]
    public SpecialEffectType specialEffect = SpecialEffectType.None;
    [Tooltip("Valor auxiliar del efecto especial (cantidad, id objetivo, etc.).")]
    public int specialValue = 0;

    // ── Trampa ───────────────────────────────────────────────────────────
    [Header("Trampa (si cardCategory = Trap)")]
    [Tooltip("Normal (un uso), Continuous (permanece activa), Counter (respuesta inmediata).")]
    public TrapKind trapKind = TrapKind.Normal;

    [Tooltip("Evento que puede activar la trampa (declaración de ataque, invocación, etc.).")]
    public TrapTrigger trapTrigger = TrapTrigger.MonsterDeclaresAttack;

    [Tooltip("Efecto que resuelve la trampa al activarse.")]
    public TrapEffectType trapEffect = TrapEffectType.None;

    [Tooltip("Valor del efecto (daño infligido, ATK reducido, etc.).")]
    public int trapValue = 0;

    [Tooltip("Prioridad de resolución: mayor = se resuelve antes. Las Contra Trampa suelen ser altas.")]
    public int resolutionPriority = 0;

    // ── Fuentes de obtención (catálogo, no progreso) ─────────────────────
    [Header("Fuentes de obtención (catálogo, no progreso)")]
    public List<CardSourceEntry> sources = new();

    // ── Helpers ──────────────────────────────────────────────────────────
    public bool IsMonster => cardCategory == CardCategory.Monster;
    public bool IsSpell => cardCategory == CardCategory.Spell;
    public bool IsEquip => cardCategory == CardCategory.Equip;
    public bool IsRitual => cardCategory == CardCategory.Ritual;
    public bool IsSpecial => cardCategory == CardCategory.Special;
    public bool IsTrap => cardCategory == CardCategory.Trap;

    /// <summary>Magia de terreno (Spell + spellKind Field).</summary>
    public bool IsFieldSpell => IsSpell && spellKind == SpellKind.Field;

    /// <summary>Texto descriptivo preferente: 'description', o el legado 'spellDescription'.</summary>
    public string DisplayDescription =>
        !string.IsNullOrEmpty(description) ? description : spellDescription;

    /// <summary>Nombre legible de la categoría, para badges/detalle.</summary>
    public string CategoryLabel => cardCategory switch
    {
        CardCategory.Monster => "MONSTRUO",
        CardCategory.Spell => IsFieldSpell ? "MAGIA DE TERRENO" : "MAGIA",
        CardCategory.Equip => "EQUIPO",
        CardCategory.Ritual => "RITUAL",
        CardCategory.Special => "ESPECIAL",
        CardCategory.Trap => trapKind switch
        {
            TrapKind.Continuous => "TRAMPA CONTINUA",
            TrapKind.Counter => "CONTRA TRAMPA",
            _ => "TRAMPA"
        },
        _ => ""
    };
}

// ─────────────────────────────────────────────────────────────────────────
//  Enums de carta
//  IMPORTANTE: solo AÑADIR valores AL FINAL. Reordenar rompería los .asset
//  existentes (Unity serializa los enums por índice).
// ─────────────────────────────────────────────────────────────────────────

public enum CardCategory
{
    Monster,   // 0
    Spell,     // 1
    Equip,     // 2
    Ritual,    // 3
    Special,   // 4
    Trap       // 5
}

/// <summary>Subtipo de las cartas mágicas (categoría Spell).</summary>
public enum SpellKind
{
    Normal,    // efecto inmediato
    Field      // magia de terreno: cambia el escenario del duelo
}

public enum MonsterType
{
    Dragon, Spellcaster, Fiend, Beast, Insect,
    Plant, Fish, Aqua, SeaSerpent, Zombie,
    Dinosaur, WingedBeast, Warrior, Machine, Thunder,
    // ── Añadidos ──
    Fairy, Reptile, Rock, Pyro
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
/// Efecto de una carta mágica NORMAL (no de terreno ni de equipo).
/// Cada tipo interpreta "spellValue" a su manera — ver SpellEffectResolver.
/// </summary>
public enum SpellEffectType
{
    None,
    HealLP,                     // cura spellValue de LP al que la usa
    DamageOpponentLP,           // resta spellValue de LP al rival
    BuffAtkAllMonsters,         // +spellValue ATK a todos los monstruos propios
    DestroyWeakestEnemyMonster, // destruye el monstruo rival con menor ATK
    // ── Añadidos ──
    DestroyAllEnemyMonsters     // destruye TODOS los monstruos del rival
}

/// <summary>
/// Comportamientos de las cartas Especiales. Su ejecución en duelo se irá
/// implementando; de momento la carta ya puede describir su efecto.
/// </summary>
public enum SpecialEffectType
{
    None,
    ChangeGuardianStar,  // cambia la Guardian Star de un monstruo
    CopyCard,            // copia otra carta
    Transform,           // transforma un monstruo en otro
    SummonMultiple,      // invoca varios monstruos
    AlterRules           // altera una regla del duelo
}

/// <summary>
/// Subtipo de una Carta de Trampa:
///   • Normal     — un solo uso; tras resolverse va al cementerio.
///   • Continuous — permanece en el campo aplicando su efecto.
///   • Counter    — respuesta inmediata a otra activación; máxima prioridad.
/// </summary>
public enum TrapKind
{
    Normal,
    Continuous,
    Counter
}

/// <summary>
/// Evento del duelo que puede disparar la activación de una Trampa
/// (su "condición de activación").
/// </summary>
public enum TrapTrigger
{
    MonsterDeclaresAttack,   // un monstruo declara un ataque
    MonsterSummoned,         // un monstruo es invocado
    SpellActivated,          // se activa una Carta Mágica
    MonsterChangesPosition,  // un monstruo cambia de posición
    MonsterDestroyed,        // un monstruo es destruido
    PlayerTakesDamage,       // el jugador recibe daño
    OpponentDraws,           // el oponente roba una carta
    FusionPerformed,         // se realiza una fusión
    TerrainChanged,          // se cambia el terreno
    Custom                   // condición específica definida por la carta
}

/// <summary>
/// Efecto que resuelve una Trampa al activarse. La ejecución en duelo está
/// pendiente (requiere la zona de Magia/Trampa y los hooks de eventos), pero la
/// carta ya puede definir y describir su comportamiento.
/// </summary>
public enum TrapEffectType
{
    None,
    DestroyAttackingMonster,      // destruye el monstruo que declaró el ataque
    DestroyAllAttackingMonsters,  // Mirror Force: destruye todos los enemigos en ataque
    NegateAttack,                 // niega el ataque
    DestroySummonedMonster,       // Trap Hole: destruye el monstruo recién invocado
    DamageOpponent,               // inflige trapValue de daño al rival
    DestroyOneSpell,              // destruye una Carta Mágica
    NegateSpell,                  // Magic Jammer: niega la activación de una magia (counter)
    NegateTrap,                   // niega otra Trampa (counter)
    NegateSummon,                 // cancela una invocación (counter)
    ReduceEnemyAtk,               // continua: -trapValue ATK a los monstruos enemigos
    PreventDirectAttacks,         // continua: impide ataques directos
    LockPositionChanges           // continua: bloquea cambios de posición
}
