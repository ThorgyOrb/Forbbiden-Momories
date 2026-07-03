/// <summary>
/// Una fila lista-para-UI: la carta de catálogo + su progreso (puede ser null
/// si nunca se creó la entrada, lo cual equivale a Locked/sin copias) + su estado.
/// </summary>
public class LibraryEntry
{
    public CardData card;
    public PlayerCardEntry playerEntry; // puede ser null
    public CardState state;

    public int Copies => playerEntry?.copiesOwned ?? 0;
    public bool IsFavorite => playerEntry?.favorite ?? false;
}
