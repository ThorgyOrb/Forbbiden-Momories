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
    private Dictionary<int, OpponentProgress> _opponents = new();

    /// <summary>Crea el singleton si aún no existe (auto-arranque). Idempotente.</summary>
    public static PlayerCollection EnsureExists()
    {
        if (Instance == null)
        {
            var go = new GameObject("PlayerCollection");
            go.AddComponent<PlayerCollection>(); // Awake fija Instance + DontDestroyOnLoad + Load
        }
        return Instance;
    }

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

    public OpponentProgress GetOpponentProgress(int opponentId) =>
        _opponents.TryGetValue(opponentId, out var p) ? p : null;

    /// <summary>Disponible en Duelo Libre = ya fue derrotado alguna vez.</summary>
    public bool IsOpponentUnlocked(int opponentId) =>
        _opponents.TryGetValue(opponentId, out var p) && p.defeated;

    public bool IsOpponentFound(int opponentId) =>
        _opponents.TryGetValue(opponentId, out var p) && p.found;

    /// <summary>Marca que el jugador se topó con el oponente (sin derrotarlo aún).</summary>
    public void MarkOpponentFound(int opponentId)
    {
        var p = GetOrCreateOpponent(opponentId);
        if (!p.found) { p.found = true; Save(); }
    }

    /// <summary>
    /// Registra el resultado de un duelo. Al GANAR marca al oponente como
    /// derrotado (lo desbloquea en Duelo Libre) y actualiza la mejor puntuación.
    /// Al perder solo suma una derrota — el oponente NO se desbloquea.
    /// </summary>
    public void RecordDuelResult(int opponentId, bool won, int score = 0)
    {
        var p = GetOrCreateOpponent(opponentId);
        p.found = true;
        if (won)
        {
            p.wins++;
            p.defeated = true;
            if (score > p.bestScore) p.bestScore = score;
        }
        else
        {
            p.losses++;
        }
        Save();
    }

    /// <summary>Desbloquea directamente (compat / debug).</summary>
    public void UnlockOpponent(int opponentId)
    {
        var p = GetOrCreateOpponent(opponentId);
        p.found = true;
        p.defeated = true;
        Save();
    }

    /// <summary>Todos los registros de oponentes conocidos (para el Duelo Libre).</summary>
    public IEnumerable<OpponentProgress> AllOpponentProgress => _opponents.Values;

    private OpponentProgress GetOrCreateOpponent(int opponentId)
    {
        if (!_opponents.TryGetValue(opponentId, out var p))
        {
            p = new OpponentProgress(opponentId);
            _opponents[opponentId] = p;
        }
        return p;
    }

    /// <summary>Borra todo el progreso (memoria + archivo de save). Para pruebas / debug.</summary>
    public void ResetCollection()
    {
        _entries.Clear();
        _opponents.Clear();

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
        public List<OpponentProgress> opponents = new();
        // Legado: saves antiguos guardaban solo ids desbloqueados. Se migra al cargar.
        public List<int> unlockedOpponents = new();
    }

    private static string SavePath => Path.Combine(Application.persistentDataPath, "collection_save.json");

    public void Save()
    {
        var data = new SaveData
        {
            entries = _entries.Values.ToList(),
            opponents = _opponents.Values.ToList()
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
        _opponents.Clear();

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

            foreach (var p in data.opponents)
                if (p != null) _opponents[p.opponentId] = p;

            // Migración de saves antiguos: ids desbloqueados → progreso derrotado.
            foreach (var id in data.unlockedOpponents)
                if (!_opponents.ContainsKey(id))
                    _opponents[id] = new OpponentProgress(id) { found = true, defeated = true };
        }
        catch (Exception e)
        {
            Debug.LogError($"PlayerCollection: error leyendo '{SavePath}'. {e}");
        }
    }
}