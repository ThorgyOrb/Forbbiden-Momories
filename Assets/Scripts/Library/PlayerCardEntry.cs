/// <summary>
/// Progreso del jugador para UNA carta puntual. Esto es lo que se serializa
/// al guardar partida. Nunca contiene datos de catálogo (nombre, ATK, etc.) -
/// esos siempre se leen de CardData vía cardId.
/// </summary>
[System.Serializable]
public class PlayerCardEntry
{
    public int cardId;
    public int copiesOwned;
    public bool discovered;
    public bool favorite;

    // Guardado como string ISO 8601 para que sea trivial de serializar/ordenar.
    // Ejemplo: System.DateTime.UtcNow.ToString("o")
    public string dateObtained = "";

    public PlayerCardEntry(int cardId)
    {
        this.cardId = cardId;
    }
}
