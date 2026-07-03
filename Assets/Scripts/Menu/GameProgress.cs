using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Progreso GENERAL de la aventura (no la colección de cartas — eso vive en
/// <see cref="PlayerCollection"/>). Sirve para que el Menú Principal sepa si
/// hay una partida que "Continuar" y para arrancar una partida nueva.
///
/// Se guarda como un JSON legible en Application.persistentDataPath, igual que
/// PlayerCollection. De momento guarda lo mínimo (si existe partida, cuándo se
/// jugó por última vez y en qué nodo de la historia va); irá creciendo cuando
/// desarrolles el modo Historia.
/// </summary>
public static class GameProgress
{
    [Serializable]
    public class Data
    {
        public bool exists;             // ¿hay una partida empezada?
        public string playerName = "Jugador";
        public int storyNode = 0;       // nodo/capítulo actual del modo Historia
        public string lastPlayedUtc;    // ISO 8601, para mostrar "última vez"
    }

    private static string SavePath => Path.Combine(Application.persistentDataPath, "game_progress.json");

    private static Data _cached;

    /// <summary>¿Existe una partida guardada para ofrecer "Continuar"?</summary>
    public static bool HasSave()
    {
        var d = Load();
        return d != null && d.exists;
    }

    /// <summary>Devuelve el progreso actual (cargándolo de disco si hace falta).</summary>
    public static Data Load()
    {
        if (_cached != null) return _cached;

        if (!File.Exists(SavePath))
        {
            _cached = new Data { exists = false };
            return _cached;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            _cached = JsonUtility.FromJson<Data>(json) ?? new Data();
        }
        catch (Exception e)
        {
            Debug.LogError($"GameProgress: error leyendo '{SavePath}'. {e}");
            _cached = new Data { exists = false };
        }
        return _cached;
    }

    /// <summary>Empieza una partida nueva desde cero y la persiste.</summary>
    public static Data StartNew(string playerName = "Jugador")
    {
        _cached = new Data
        {
            exists = true,
            playerName = playerName,
            storyNode = 0,
            lastPlayedUtc = DateTime.UtcNow.ToString("o")
        };
        Save();
        return _cached;
    }

    /// <summary>Guarda el progreso actual (llámalo tras avanzar en la historia).</summary>
    public static void Save()
    {
        if (_cached == null) return;
        _cached.lastPlayedUtc = DateTime.UtcNow.ToString("o");

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(_cached, prettyPrint: true));
        }
        catch (Exception e)
        {
            Debug.LogError($"GameProgress: no se pudo guardar en '{SavePath}'. {e}");
        }
    }

    /// <summary>Borra la partida (para pruebas o para el botón "Borrar partida").</summary>
    public static void Delete()
    {
        _cached = new Data { exists = false };
        if (File.Exists(SavePath))
        {
            try { File.Delete(SavePath); }
            catch (Exception e) { Debug.LogError($"GameProgress: no se pudo borrar '{SavePath}'. {e}"); }
        }
    }
}
