using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// El catálogo COMPLETO de cartas, sin importar lo que el jugador posea.
/// Estático y cacheado: se carga una sola vez por sesión.
/// </summary>
public static class LibraryCatalog
{
    private static List<CardData> _all;
    private static Dictionary<int, CardData> _byId;
    private static Dictionary<int, OpponentData> _opponentsById;

    public static IReadOnlyList<CardData> AllCards
    {
        get { EnsureLoaded(); return _all; }
    }

    public static void EnsureLoaded()
    {
        if (_all != null) return;

        _all = Resources.LoadAll<CardData>("Cards/Data")
                         .OrderBy(c => c.cardId)
                         .ToList();
        _byId = _all.ToDictionary(c => c.cardId, c => c);

        var opponents = Resources.LoadAll<OpponentData>("Opponents/Data");
        _opponentsById = opponents.ToDictionary(o => o.opponentId, o => o);

        if (_all.Count == 0)
            Debug.LogWarning("LibraryCatalog: no se encontraron CardData en Resources/Cards/Data/");
    }

    public static CardData GetCard(int cardId)
    {
        EnsureLoaded();
        return _byId.TryGetValue(cardId, out var c) ? c : null;
    }

    public static OpponentData GetOpponent(int opponentId)
    {
        EnsureLoaded();
        return _opponentsById.TryGetValue(opponentId, out var o) ? o : null;
    }

    public static int TotalCount
    {
        get { EnsureLoaded(); return _all.Count; }
    }
}
