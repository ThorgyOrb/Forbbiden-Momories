// Enums de soporte para el sistema de Library.
// No tocan los enums existentes de CardData (MonsterType, GuardianStar, CardRarity, CardAttribute).

/// <summary>
/// De dónde se puede obtener una carta. Es información de CATÁLOGO (igual para todos
/// los jugadores), no de progreso. Por eso vive en CardData, no en PlayerCardEntry.
/// </summary>
public enum CardSourceType
{
    Drop,        // un oponente la suelta al ganarle
    Password,    // se introduce un código
    Event,       // evento temporal
    Fusion,      // resultado de fusionar otras cartas
    Trade        // intercambio con NPC u otro jugador
}

/// <summary>
/// Estado visual/funcional de una carta para el jugador actual.
/// Se calcula en runtime combinando CardData (catálogo) + PlayerCardEntry (progreso).
/// </summary>
public enum CardState
{
    Locked,      // "???" - nunca fue descubierta, no se puede ver nada de ella
    Discovered,  // se vio al menos una vez (en duelo, en un sobre, etc.) pero x0 copias
    Owned        // tiene 1+ copias, lista para usarse en mazos
}

/// <summary>
/// Filtro rápido de posesión, el de los 3 checkboxes que pediste.
/// </summary>
public enum PossessionFilter
{
    All,
    OwnedOnly,
    DiscoveredOnly,
    NotDiscoveredOnly
}

/// <summary>
/// Opciones de ordenamiento de la lista de la biblioteca.
/// </summary>
public enum CardSortOption
{
    NameAsc,
    NameDesc,
    IdAsc,
    IdDesc,
    AtkDesc,
    AtkAsc,
    CopiesOwned,
    DateObtained
}
