using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// El único lugar donde catálogo y colección se "tocan". La UI nunca debería
/// leer LibraryCatalog o PlayerCollection directamente para armar listas -
/// siempre pasa por aquí, así el resto del código no necesita saber cómo
/// se calculan los estados.
/// </summary>
public static class LibraryQueryService
{
    /// <summary>Construye una fila por cada carta del catálogo (las 722), con su estado actual.</summary>
    public static List<LibraryEntry> BuildAllEntries()
    {
        var collection = PlayerCollection.Instance;
        var result = new List<LibraryEntry>(LibraryCatalog.AllCards.Count);

        foreach (var card in LibraryCatalog.AllCards)
        {
            var playerEntry = collection?.GetEntry(card.cardId);
            var state = collection?.GetState(card.cardId) ?? CardState.Locked;

            result.Add(new LibraryEntry
            {
                card = card,
                playerEntry = playerEntry,
                state = state
            });
        }
        return result;
    }

    public static List<LibraryEntry> Filter(List<LibraryEntry> entries, CardFilterCriteria criteria)
    {
        return entries.Where(e => criteria.Matches(e.card, e.playerEntry, e.state)).ToList();
    }

    public static List<LibraryEntry> Sort(List<LibraryEntry> entries, CardSortOption option)
    {
        return option switch
        {
            CardSortOption.NameAsc => entries.OrderBy(e => e.card.cardName).ToList(),
            CardSortOption.NameDesc => entries.OrderByDescending(e => e.card.cardName).ToList(),
            CardSortOption.IdAsc => entries.OrderBy(e => e.card.cardId).ToList(),
            CardSortOption.IdDesc => entries.OrderByDescending(e => e.card.cardId).ToList(),
            CardSortOption.AtkDesc => entries.OrderByDescending(e => e.card.baseAtk).ToList(),
            CardSortOption.AtkAsc => entries.OrderBy(e => e.card.baseAtk).ToList(),
            CardSortOption.CopiesOwned => entries.OrderByDescending(e => e.Copies).ToList(),
            CardSortOption.DateObtained => entries
                .OrderByDescending(e => ParseDate(e.playerEntry?.dateObtained))
                .ToList(),
            _ => entries
        };
    }

    /// <summary>Pipeline completo: catálogo + colección -> filtro -> orden. Lo que llama LibraryManager.</summary>
    public static List<LibraryEntry> Query(CardFilterCriteria criteria, CardSortOption sort)
    {
        var all = BuildAllEntries();
        var filtered = Filter(all, criteria);
        return Sort(filtered, sort);
    }

    /// <summary>
    /// Decide si una fuente de obtención puede revelarse al jugador.
    /// Regla de negocio pedida: la carta debe estar al menos descubierta, Y si la
    /// fuente involucra a un oponente (Drop/Trade), ese oponente debe estar desbloqueado.
    /// </summary>
    public static bool CanRevealSource(CardState cardState, CardSourceEntry source)
    {
        if (cardState == CardState.Locked) return false;

        bool requiresOpponent = source.sourceType == CardSourceType.Drop
                              || source.sourceType == CardSourceType.Trade;

        if (!requiresOpponent) return true;

        var collection = PlayerCollection.Instance;
        return collection != null && collection.IsOpponentUnlocked(source.opponentId);
    }

    /// <summary>Filtra la lista de sources de una carta a sólo las que se pueden mostrar.</summary>
    public static List<CardSourceEntry> GetVisibleSources(CardData card, CardState state)
    {
        return card.sources.Where(s => CanRevealSource(state, s)).ToList();
    }

    // ── Stats globales para el encabezado ────────────────────────────────

    public static (int total, int discovered, int owned, float completion) GetGlobalStats()
    {
        var all = BuildAllEntries();
        int total = all.Count;
        int discovered = all.Count(e => e.state != CardState.Locked);
        int owned = all.Count(e => e.state == CardState.Owned);
        float completion = total > 0 ? (discovered / (float)total) * 100f : 0f;
        return (total, discovered, owned, completion);
    }

    private static DateTime ParseDate(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return DateTime.MinValue;
        return DateTime.TryParse(iso, out var d) ? d : DateTime.MinValue;
    }
}
