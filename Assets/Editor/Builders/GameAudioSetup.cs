using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Crea el <see cref="GameAudioBank"/> en Assets/Resources para que todas las escenas lo
/// carguen solas, e intenta auto-asignar la música por escena que ya tienes en
/// Assets/Audio/Music según el nombre del archivo (puedes corregirlo a mano después).
/// Menú: YGO > Audio > ...
/// </summary>
public static class GameAudioSetup
{
    private const string Dir = "Assets/Resources";
    private const string BankPath = Dir + "/GameAudioBank.asset";
    private const string MusicDir = "Assets/Audio/Music";

    // Palabra clave del nombre de archivo → escenas a las que se asigna esa pista.
    // Ajusta este mapa si renombras clips o añades música nueva. La música del Menú
    // Principal NO se auto-asigna aquí a propósito: MainMenuController ya tiene su propio
    // campo "menuMusic" en el Inspector para elegirla a mano.
    private static readonly (string keyword, string[] scenes)[] AutoAssign =
    {
        ("library",   new[] { GameScenes.Library, GameScenes.LibraryCatalog, GameScenes.LibraryGods }),
        ("free-duel", new[] { GameScenes.FreeDuel }),
    };

    [MenuItem("YGO/Audio/Crear Game Audio Bank (Resources)")]
    public static void CreateBank()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameAudioBank>(BankPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            EditorUtility.DisplayDialog("Game Audio Bank",
                "Ya existe en Assets/Resources/GameAudioBank.asset.\nLo seleccioné para que lo revises.", "Ok");
            return;
        }

        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        var bank = ScriptableObject.CreateInstance<GameAudioBank>();
        AssetDatabase.CreateAsset(bank, BankPath);

        int assigned = AutoAssignSceneMusic(bank);

        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = bank;
        EditorGUIUtility.PingObject(bank);
        EditorUtility.DisplayDialog("Game Audio Bank",
            "Se creó Assets/Resources/GameAudioBank.asset.\n\n" +
            $"Se auto-asignaron {assigned} pista(s) de música por nombre de archivo (revísalas). " +
            "Arrastra tus AudioClips (efectos de UI y música por escena) a sus campos; los que " +
            "dejes vacíos no suenan. 'DuelScene' se deja fuera a propósito: el duelo gestiona su " +
            "propia música con DuelAudioBank.", "Genial");
    }

    /// <summary>Rellena de nuevo el mapa de música por escena a partir de Assets/Audio/Music, sin tocar los efectos de UI ya asignados.</summary>
    [MenuItem("YGO/Audio/Re-detectar música por escena")]
    public static void ReassignSceneMusic()
    {
        var bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>(BankPath);
        if (bank == null)
        {
            EditorUtility.DisplayDialog("Game Audio Bank",
                "Todavía no existe. Ejecuta primero 'YGO > Audio > Crear Game Audio Bank'.", "Ok");
            return;
        }

        int assigned = AutoAssignSceneMusic(bank);
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();

        Selection.activeObject = bank;
        EditorGUIUtility.PingObject(bank);
        EditorUtility.DisplayDialog("Game Audio Bank",
            $"Se auto-asignaron {assigned} pista(s) por nombre de archivo. Revisa el resultado " +
            "en el Inspector: es una detección por palabra clave, no siempre acierta.", "Ok");
    }

    private static int AutoAssignSceneMusic(GameAudioBank bank)
    {
        var byScene = new Dictionary<string, AudioClip>();
        if (bank.sceneMusic != null)
            foreach (var e in bank.sceneMusic)
                if (e != null && !string.IsNullOrEmpty(e.sceneName) && e.clip != null)
                    byScene[e.sceneName] = e.clip; // conserva lo ya asignado a mano

        if (Directory.Exists(MusicDir))
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { MusicDir });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                foreach (var (keyword, scenes) in AutoAssign)
                {
                    if (!fileName.Contains(keyword)) continue;
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null) continue;
                    foreach (var scene in scenes)
                        if (!byScene.ContainsKey(scene)) byScene[scene] = clip;
                }
            }
        }

        var list = new List<SceneMusicEntry>();
        foreach (var kv in byScene)
            list.Add(new SceneMusicEntry { sceneName = kv.Key, clip = kv.Value });
        bank.sceneMusic = list.ToArray();
        return list.Count;
    }
}
