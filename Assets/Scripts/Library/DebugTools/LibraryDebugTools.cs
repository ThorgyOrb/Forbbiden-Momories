using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Botones de prueba para simular cada forma de obtener una carta sin tener
/// implementados todavía duelos/eventos/fusión/trade reales. Asigna cada
/// método público a un botón distinto desde el Inspector (OnClick).
///
/// Esto es herramienta de desarrollo, no código de producción - bórralo (o
/// desactiva el GameObject) cuando ya tengas los sistemas reales integrados.
/// </summary>
public class LibraryDebugTools : MonoBehaviour
{
    [SerializeField] private LibraryManager libraryManager; // opcional, refresca el grid al instante
    [SerializeField] private TMP_Text feedbackText;          // opcional, muestra qué pasó

    // ── Un botón por cada CardSourceType ─────────────────────────────────

    public void SimulateDrop() => GrantFromSource(CardSourceType.Drop, unlockOpponent: true);
    public void SimulatePassword() => GrantFromSource(CardSourceType.Password, unlockOpponent: false);
    public void SimulateEvent() => GrantFromSource(CardSourceType.Event, unlockOpponent: false);
    public void SimulateFusion() => GrantFromSource(CardSourceType.Fusion, unlockOpponent: false);
    public void SimulateTrade() => GrantFromSource(CardSourceType.Trade, unlockOpponent: true);

    /// <summary>Simula "la vio pero no la tiene" (ej. espiar el mazo de un oponente).</summary>
    public void SimulateDiscoverWithoutOwning()
    {
        var card = AnyCard();
        if (card == null) return;

        PlayerCollection.Instance.DiscoverCard(card.cardId);
        Report($"Descubierta (sin copias): {card.cardName}");
        Refresh();
    }

    /// <summary>Borra todo el progreso para volver a probar desde cero.</summary>
    public void ResetCollection()
    {
        PlayerCollection.Instance.ResetCollection();
        Report("Colección reiniciada.");
        Refresh();
    }

    // ── Internos ──────────────────────────────────────────────────────────

    private void GrantFromSource(CardSourceType type, bool unlockOpponent)
    {
        var candidates = LibraryCatalog.AllCards
            .Where(c => c.sources.Any(s => s.sourceType == type))
            .ToList();

        CardData card;
        CardSourceEntry source = null;

        if (candidates.Count > 0)
        {
            card = candidates[Random.Range(0, candidates.Count)];
            source = card.sources.First(s => s.sourceType == type);
        }
        else
        {
            // Todavía no configuraste ningún CardSourceEntry de este tipo en tus
            // CardData - para no bloquear la prueba, otorga cualquier carta del
            // catálogo como respaldo.
            card = AnyCard();
            Debug.LogWarning($"LibraryDebugTools: no hay cartas con sourceType={type} " +
                              $"configuradas todavía. Se otorgó '{card?.cardName}' como respaldo.");
        }

        if (card == null) return;

        if (unlockOpponent && source != null && source.opponentId >= 0)
            PlayerCollection.Instance.UnlockOpponent(source.opponentId);

        PlayerCollection.Instance.AddCopy(card.cardId);
        Report($"[{type}] Obtuviste: {card.cardName}");
        Refresh();
    }

    private CardData AnyCard()
    {
        var all = LibraryCatalog.AllCards;
        if (all.Count == 0)
        {
            Debug.LogWarning("LibraryDebugTools: el catálogo está vacío (revisa Resources/Cards/Data).");
            return null;
        }
        return all[Random.Range(7, 7)];
    }

    private void Report(string message)
    {
        Debug.Log($"LibraryDebugTools: {message}");
        if (feedbackText != null) feedbackText.text = message;
    }

    private void Refresh()
    {
        if (libraryManager != null) libraryManager.RefreshNow();
    }
}
