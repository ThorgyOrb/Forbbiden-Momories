/// <summary>
/// Cada fase del turno como máquina de estados.
/// El DuelManager avanza entre estos estados en orden.
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
