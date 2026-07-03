using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Herramienta de editor para dejar el Menú Principal listo con un clic:
///   1. Crea la escena Assets/Scenes/MainMenu.unity (si no existe) con un
///      GameObject que ya tiene el MainMenuController.
///   2. Registra esa escena — y todas las de Assets/Scenes — en Build Settings,
///      dejando MainMenu como escena inicial (índice 0).
///
/// Menú de Unity:  YGO > Setup > ...
/// </summary>
public static class MainMenuSetup
{
    private const string ScenesDir = "Assets/Scenes";
    private const string MainMenuPath = ScenesDir + "/MainMenu.unity";

    [MenuItem("YGO/Setup/Configurar Menú Principal (escena + Build Settings)")]
    public static void ConfigureMainMenu()
    {
        bool created = false;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath) == null)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return; // el usuario canceló

            CreateMainMenuScene();
            created = true;
        }

        RegisterBuildScenes();

        EditorUtility.DisplayDialog(
            "Menú Principal configurado",
            (created
                ? "Se creó la escena MainMenu con el MainMenuController.\n\n"
                : "La escena MainMenu ya existía.\n\n") +
            "Todas las escenas de Assets/Scenes quedaron registradas en Build Settings, " +
            "con MainMenu como escena inicial.\n\nPulsa Play para probar el menú.",
            "Genial");
    }

    [MenuItem("YGO/Setup/Abrir escena de Menú Principal")]
    public static void OpenMainMenuScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath) == null)
        {
            EditorUtility.DisplayDialog("No existe",
                "Todavía no hay escena MainMenu. Usa primero " +
                "'YGO > Setup > Configurar Menú Principal'.", "Ok");
            return;
        }

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
    }

    // ── Interno ──────────────────────────────────────────────────────────

    private static void CreateMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var go = new GameObject("MainMenu");
        go.AddComponent<MainMenuController>();

        EditorSceneManager.SaveScene(scene, MainMenuPath);
        Debug.Log($"MainMenuSetup: escena creada en {MainMenuPath}");
    }

    /// <summary>
    /// Deja en Build Settings todas las escenas de Assets/Scenes, con MainMenu
    /// primero (índice 0) para que sea la que arranca el juego.
    /// </summary>
    private static void RegisterBuildScenes()
    {
        var result = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MainMenuPath, true)
        };

        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { ScenesDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == MainMenuPath) continue;
            result.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = result.ToArray();
        Debug.Log($"MainMenuSetup: {result.Count} escenas registradas en Build Settings.");
    }
}
