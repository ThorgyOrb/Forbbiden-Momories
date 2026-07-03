using UnityEngine;

/// <summary>
/// Una entrada de "de dónde se obtiene esta carta". Vive dentro de CardData
/// porque es información fija del juego, no del progreso del jugador.
///
/// Si sourceType == Drop o Trade, opponentId debe apuntar a un OpponentData válido.
/// Si sourceType == Password / Event / Fusion, opponentId se deja en -1.
///
/// IMPORTANTE: que esta entrada exista en el catálogo NO significa que se pueda
/// mostrar al jugador. Eso lo decide LibraryQueryService.CanRevealSource(), que
/// exige carta descubierta + (si aplica) oponente desbloqueado.
/// </summary>
[System.Serializable]
public class CardSourceEntry
{
    public CardSourceType sourceType;

    [Tooltip("ID del OpponentData si sourceType es Drop o Trade. -1 si no aplica.")]
    public int opponentId = -1;

    [Tooltip("Texto a mostrar, ej: 'Password: 89631139' o 'Evento: Duelista Legendario'")]
    public string description;
}
