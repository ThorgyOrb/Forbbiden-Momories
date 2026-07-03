using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Única fuente de verdad sobre el PROGRESO del jugador en la biblioteca.
/// No sabe nada de ATK, nombres ni sprites - eso es trabajo de CardData / LibraryCatalog.
///
/// Uso típico:
///   PlayerCollection.Instance.AddCopy(cardId);
///   PlayerCollection.Instance.GetState(cardId);
///   PlayerCollection.Instance.IsOpponentUnlocked(opponentId);
/// </summary>
public class PlayerCollection : MonoBehaviour
{
    public static PlayerCollection Instance { get; private set; }

    private Dictionary<int, PlayerCardEntry> _entries = new();
    private HashSet<int> _unlockedOpponents = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // ── Consultas de carta ──────────────────────────────────────────────

    public bool IsDiscovered(int cardId) =>
        _entries.TryGetValue(cardId, out var e) && e.discovered;

    public int GetCopies(int cardId) =>
        _entries.TryGetValue(cardId, out var e) ? e.copiesOwned : 0;

    public bool IsOwned(int cardId) => GetCopies(cardId) > 0;

    public bool IsFavorite(int cardId) =>
        _entries.TryGetValue(cardId, out var e) && e.favorite;

    public PlayerCardEntry GetEntry(int cardId) =>
        _entries.TryGetValue(cardId, out var e) ? e : null;

    public CardState GetState(int cardId)
    {
        if (!IsDiscovered(cardId)) return CardState.Locked;
        return IsOwned(cardId) ? CardState.Owned : CardState.Discovered;
    }

    // ── Mutaciones de carta ─────────────────────────────────────────────

    /// <summary>Marca la carta como vista (ej. apareció en duelo) sin darle copias.</summary>
    public void DiscoverCard(int cardId)
    {
        var entry = GetOrCreate(cardId);
        if (!entry.discovered)
        {
            entry.discovered = true;
            if (string.IsNullOrEmpty(entry.dateObtained))
                entry.dateObtained = DateTime.UtcNow.ToString("o");
        }
    }

    /// <summary>Añade copias y descubre la carta automáticamente si no lo estaba.</summary>
    public void AddCopy(int cardId, int amount = 1)
    {
        var entry = GetOrCreate(cardId);
        entry.copiesOwned += amount;
        entry.discovered = true;
        if (string.IsNullOrEmpty(entry.dateObtained))
            entry.dateObtained = DateTime.UtcNow.ToString("o");
        Save();
    }

    public void ToggleFavorite(int cardId)
    {
        var entry = GetOrCreate(cardId);
        entry.favorite = !entry.favorite;
        Save();
    }

    private PlayerCardEntry GetOrCreate(int cardId)
    {
        if (!_entries.TryGetValue(cardId, out var entry))
        {
            entry = new PlayerCardEntry(cardId);
            _entries[cardId] = entry;
        }
        return entry;
    }

    // ── Oponentes ────────────────────────────────────────────────────────

    public bool IsOpponentUnlocked(int opponentId) => _unlockedOpponents.Contains(opponentId);

    public void UnlockOpponent(int opponentId)
    {
        _unlockedOpponents.Add(opponentId);
        Save();
    }

    /// <summary>Borra todo el progreso (memoria + archivo de save). Para pruebas / debug.</summary>
    public void ResetCollection()
    {
        _entries.Clear();
        _unlockedOpponents.Clear();

        if (File.Exists(SavePath))
        {
            try { File.Delete(SavePath); }
            catch (Exception e) { Debug.LogError($"PlayerCollection: no se pudo borrar '{SavePath}'. {e}"); }
        }

        Debug.Log("PlayerCollection: colección reiniciada.");
    }

    // ── Save / Load ──────────────────────────────────────────────────────
    // Se guarda como un archivo JSON real en disco (no PlayerPrefs), para que
    // puedas abrirlo con cualquier editor de texto y ver exactamente qué hay.
    // En el Editor / Windows, la ruta suele ser algo como:
    //   C:\Users\TuUsuario\AppData\LocalLow\TuEmpresa\TuJuego\collection_save.json
    // En Mac/Linux/Android/iOS, Application.persistentDataPath apunta al
    // equivalente correcto en cada plataforma automáticamente.

    [Serializable]
    private class SaveData
    {
        public List<PlayerCardEntry> entries = new();
        public List<int> unlockedOpponents = new();
    }

    private static string SavePath => Path.Combine(Application.persistentDataPath, "collection_save.json");

    public void Save()
    {
        var data = new SaveData
        {
            entries = _entries.Values.ToList(),
            unlockedOpponents = _unlockedOpponents.ToList()
        };
        string json = JsonUtility.ToJson(data, prettyPrint: true);

        try
        {
            File.WriteAllText(SavePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"PlayerCollection: no se pudo guardar en '{SavePath}'. {e}");
        }
    }

    public void Load()
    {
        _entries.Clear();
        _unlockedOpponents.Clear();

        if (!File.Exists(SavePath))
        {
            Debug.Log($"PlayerCollection: no hay save previo en '{SavePath}', se empieza vacío.");
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return;

            foreach (var e in data.entries)
                _entries[e.cardId] = e;

            foreach (var id in data.unlockedOpponents)
                _unlockedOpponents.Add(id);
        }
        catch (Exception e)
        {
            Debug.LogError($"PlayerCollection: error leyendo '{SavePath}'. {e}");
        }
    }
}