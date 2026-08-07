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
    private static List<OpponentData> _allOpponents;
    private static Dictionary<int, OpponentData> _opponentsById;

    public static IReadOnlyList<CardData> AllCards
    {
        get { EnsureLoaded(); return _all; }
    }

    public static IReadOnlyList<OpponentData> AllOpponents
    {
        get { EnsureLoaded(); return _allOpponents; }
    }

    public static void EnsureLoaded()
    {
        if (_all != null) return;

        // Medido a propósito: con ~14.600 cartas este LoadAll es el mayor coste fijo al
        // entrar en cualquier escena que use el catálogo. Si el log dispara, el siguiente
        // paso es un asset índice (id/nombre/categoría/stats) y cargar el CardData completo
        // solo al abrir el detalle.
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _all = Resources.LoadAll<CardData>("Cards/Data")
                         .OrderBy(c => c.cardId)
                         .ToList();

        // Sin ToDictionary a pelo: con ~14.000 cartas importadas, un solo cardId
        // repetido lanzaría y dejaría la biblioteca entera inservible. Aquí el
        // duplicado se avisa y se queda la primera carta.
        _byId = new Dictionary<int, CardData>(_all.Count);
        foreach (var c in _all)
        {
            if (_byId.TryGetValue(c.cardId, out var prev))
            {
                Debug.LogWarning($"LibraryCatalog: cardId {c.cardId} repetido " +
                                 $"('{prev.cardName}' y '{c.cardName}'). Se ignora la segunda.");
                continue;
            }
            _byId[c.cardId] = c;
        }

        _allOpponents = Resources.LoadAll<OpponentData>("Opponents/Data")
                                 .OrderBy(o => o.opponentId)
                                 .ToList();
        _opponentsById = _allOpponents.ToDictionary(o => o.opponentId, o => o);

        if (_all.Count == 0)
            Debug.LogWarning("LibraryCatalog: no se encontraron CardData en Resources/Cards/Data/");

        sw.Stop();
        Debug.Log($"LibraryCatalog: {_all.Count} cartas y {_allOpponents.Count} oponentes " +
                  $"cargados en {sw.ElapsedMilliseconds} ms.");
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
