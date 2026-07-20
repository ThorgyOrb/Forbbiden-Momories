using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Biblioteca de mazos del jugador (persistente). A diferencia del antiguo
/// <see cref="PlayerDeck"/> (un único mazo), aquí el jugador puede tener VARIOS
/// mazos con nombre; uno es el "activo" (el que usa el duelo).
///
/// Cada mazo se guarda como lista de cardIds (con repeticiones = copias) en
/// <c>player_decks.json</c>. Si al arrancar solo existe el save antiguo de un
/// mazo (<c>player_deck.json</c>), se migra automáticamente como el primer mazo.
///
/// Reglas del juego (validadas por el Constructor, no aquí): cada mazo debe
/// tener EXACTAMENTE 40 cartas para duelar, un máximo de 3 copias por carta, y
/// solo cartas que el jugador posea.
/// </summary>
public static class DeckLibrary
{
    /// <summary>Un mazo con nombre y sus cartas (ids con repeticiones).</summary>
    [Serializable]
    public class Deck
    {
        public string name = "Nuevo Mazo";
        public List<int> cardIds = new();

        public Deck() { }
        public Deck(string name) { this.name = name; }

        public int Count => cardIds.Count;
        public bool IsComplete => cardIds.Count == PlayerDeck.RequiredSize;
    }

    [Serializable]
    private class Data
    {
        public List<Deck> decks = new();
        public int activeIndex = 0;
    }

    private static Data _cached;

    private static string SavePath   => Path.Combine(Application.persistentDataPath, "player_decks.json");
    private static string LegacyPath => Path.Combine(Application.persistentDataPath, "player_deck.json");

    // ── Consultas ────────────────────────────────────────────────────────

    public static IReadOnlyList<Deck> Decks { get { Load(); return _cached.decks; } }

    public static int Count { get { Load(); return _cached.decks.Count; } }

    public static int ActiveIndex
    {
        get { Load(); return Mathf.Clamp(_cached.activeIndex, 0, _cached.decks.Count - 1); }
    }

    /// <summary>El mazo activo (el que usa el duelo). Nunca es null: siempre hay al menos uno.</summary>
    public static Deck Active
    {
        get { Load(); return _cached.decks[ActiveIndex]; }
    }

    // ── Mutaciones ───────────────────────────────────────────────────────

    /// <summary>Cambia el mazo activo por índice.</summary>
    public static void SetActive(int index)
    {
        Load();
        if (index < 0 || index >= _cached.decks.Count) return;
        _cached.activeIndex = index;
        Persist();
    }

    /// <summary>Crea un mazo nuevo vacío y lo deja como activo. Devuelve su índice.</summary>
    public static int CreateDeck(string name = null)
    {
        Load();
        var deck = new Deck(string.IsNullOrWhiteSpace(name) ? DefaultName() : name.Trim());
        _cached.decks.Add(deck);
        _cached.activeIndex = _cached.decks.Count - 1;
        Persist();
        return _cached.activeIndex;
    }

    /// <summary>Borra un mazo. Nunca deja la biblioteca vacía (recrea uno si hace falta).</summary>
    public static void DeleteDeck(int index)
    {
        Load();
        if (index < 0 || index >= _cached.decks.Count) return;
        _cached.decks.RemoveAt(index);
        if (_cached.decks.Count == 0)
            _cached.decks.Add(new Deck(DefaultName()));
        _cached.activeIndex = Mathf.Clamp(_cached.activeIndex, 0, _cached.decks.Count - 1);
        Persist();
    }

    /// <summary>Renombra el mazo activo.</summary>
    public static void RenameActive(string name)
    {
        Load();
        if (string.IsNullOrWhiteSpace(name)) return;
        Active.name = name.Trim();
        Persist();
    }

    /// <summary>Reemplaza las cartas del mazo activo y persiste.</summary>
    public static void SaveActive(List<int> cardIds)
    {
        Load();
        Active.cardIds = new List<int>(cardIds);
        Persist();
    }

    // ── Serialización ────────────────────────────────────────────────────

    private static void Load()
    {
        if (_cached != null) return;

        // 1) Save nuevo (multi-mazo).
        if (File.Exists(SavePath))
        {
            try { _cached = JsonUtility.FromJson<Data>(File.ReadAllText(SavePath)); }
            catch (Exception e) { Debug.LogError($"DeckLibrary: error leyendo '{SavePath}'. {e}"); }
        }

        // 2) Migración del save antiguo (un solo mazo).
        if (_cached == null && File.Exists(LegacyPath))
        {
            try
            {
                var legacy = JsonUtility.FromJson<LegacyData>(File.ReadAllText(LegacyPath));
                _cached = new Data();
                var deck = new Deck("Mi Mazo");
                if (legacy?.cardIds != null) deck.cardIds = new List<int>(legacy.cardIds);
                _cached.decks.Add(deck);
                Debug.Log("DeckLibrary: migrado el mazo antiguo (player_deck.json) al nuevo formato multi-mazo.");
            }
            catch (Exception e) { Debug.LogError($"DeckLibrary: error migrando '{LegacyPath}'. {e}"); }
        }

        // 3) Primera vez: biblioteca con un mazo vacío.
        if (_cached == null) _cached = new Data();

        if (_cached.decks == null || _cached.decks.Count == 0)
        {
            _cached.decks = new List<Deck> { new Deck(DefaultName()) };
            _cached.activeIndex = 0;
        }
        _cached.activeIndex = Mathf.Clamp(_cached.activeIndex, 0, _cached.decks.Count - 1);
    }

    private static void Persist()
    {
        try { File.WriteAllText(SavePath, JsonUtility.ToJson(_cached, prettyPrint: true)); }
        catch (Exception e) { Debug.LogError($"DeckLibrary: no se pudo guardar en '{SavePath}'. {e}"); }
    }

    private static string DefaultName()
    {
        int n = (_cached?.decks?.Count ?? 0) + 1;
        return $"Mazo {n}";
    }

    // Espejo del formato antiguo de PlayerDeck, solo para migrar.
    [Serializable]
    private class LegacyData { public List<int> cardIds = new(); }
}
