using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Mazo principal del jugador (persistente). Se guarda como lista de cardIds
/// (con repeticiones = copias) en un JSON, igual que el resto del progreso.
///
/// Reglas: el mazo debe tener EXACTAMENTE 40 cartas para poder duelar, y solo
/// puede contener cartas que el jugador posea (eso lo valida el constructor).
/// </summary>
public static class PlayerDeck
{
    public const int RequiredSize = 40;

    [Serializable]
    private class Data { public List<int> cardIds = new(); }

    private static Data _cached;
    private static string SavePath => Path.Combine(Application.persistentDataPath, "player_deck.json");

    /// <summary>Ids de las cartas del mazo (con repeticiones).</summary>
    public static List<int> GetCardIds()
    {
        Load();
        return new List<int>(_cached.cardIds);
    }

    public static int Count { get { Load(); return _cached.cardIds.Count; } }

    /// <summary>¿El mazo guardado tiene exactamente 40 cartas?</summary>
    public static bool IsComplete => Count == RequiredSize;

    /// <summary>Guarda el mazo (lista de ids con repeticiones).</summary>
    public static void Save(List<int> cardIds)
    {
        Load();
        _cached.cardIds = new List<int>(cardIds);
        Persist();
    }

    /// <summary>Resuelve el mazo guardado a CardData (para el duelo).</summary>
    public static List<CardData> ResolveCards()
    {
        Load();
        var list = new List<CardData>();
        foreach (var id in _cached.cardIds)
        {
            var c = LibraryCatalog.GetCard(id);
            if (c != null) list.Add(c);
        }
        return list;
    }

    private static void Load()
    {
        if (_cached != null) return;

        if (!File.Exists(SavePath)) { _cached = new Data(); return; }

        try { _cached = JsonUtility.FromJson<Data>(File.ReadAllText(SavePath)) ?? new Data(); }
        catch (Exception e) { Debug.LogError($"PlayerDeck: error leyendo '{SavePath}'. {e}"); _cached = new Data(); }
    }

    private static void Persist()
    {
        try { File.WriteAllText(SavePath, JsonUtility.ToJson(_cached, prettyPrint: true)); }
        catch (Exception e) { Debug.LogError($"PlayerDeck: no se pudo guardar en '{SavePath}'. {e}"); }
    }
}
