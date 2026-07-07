/// <summary>
/// Cada fase del turno como máquina de estados.
/// El DuelController avanza entre estos estados en orden.
/// </summary>
public enum DuelPhase
{
    Setup,          // Inicialización, barajar, robo inicial
    DrawPhase,      // Robar hasta tener 5
    MainPhase,      // Acción principal (invocar / magia / trampa / fusión)
    BattlePhase,    // Declarar ataques
    EndPhase,       // Comprobar victoria, cambiar turno
    CheckWin,       // ¿Alguien ganó?
    RewardPhase,    // Calcular rango y drops
    SavePhase       // Persistir colección y StarChips
}

/// <summary>
/// Resultado final del duelo.
/// </summary>
public enum DuelResult
{
    None,
    PlayerWin,
    OpponentWin,
    Draw
}

/// <summary>
/// Rango de desempeño que determina de qué lista de drops se extrae la recompensa.
/// </summary>
public enum DuelRank
{
    BPow, APow, SPow,
    BTec, ATec, STec
}

/// <summary>
/// Tipo de victoria, usado para elegir la tabla de recompensas del oponente.
///   • Normal    — victoria de fuerza bruta (rango Pow no-S).
///   • Technical — victoria técnica (rango Tec no-S: fusiones, magias, eficiencia).
///   • Perfect   — victoria impecable (rango S).
/// </summary>
public enum VictoryTier
{
    Normal,
    Technical,
    Perfect
}
