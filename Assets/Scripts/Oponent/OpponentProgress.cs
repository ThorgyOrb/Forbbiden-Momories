using System;

/// <summary>
/// Progreso del jugador CONTRA un oponente concreto (no catálogo — eso es
/// OpponentData). Vive en PlayerCollection y se guarda en el JSON de progreso.
///
/// Regla de desbloqueo (estilo Forbidden Memories):
///   un oponente aparece en Duelo Libre tras ser DERROTADO por primera vez.
/// </summary>
[Serializable]
public class OpponentProgress
{
    public int opponentId;
    public bool found;      // "Encontrado": el jugador ya se topó con él
    public bool defeated;   // "Derrotado": el jugador ya le ganó al menos una vez
    public int wins;
    public int losses;
    public int bestScore;   // mejor puntuación obtenida contra él

    /// <summary>Disponible en Duelo Libre = ha sido derrotado alguna vez.</summary>
    public bool AvailableInFreeDuel => defeated;

    public OpponentProgress() { }
    public OpponentProgress(int id) { opponentId = id; }
}
