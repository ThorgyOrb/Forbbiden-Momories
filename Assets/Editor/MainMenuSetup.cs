using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta de editor para dejar el Menú Principal listo con un clic:
///   1. Crea/abre la escena Assets/Scenes/MainMenu.unity.
///   2. Asegura un GameObject con el <see cref="MainMenuController"/>.
///   3. Construye la UI como OBJETOS REALES editables (vía <see cref="MainMenuBuilder"/>)
///      y cablea todas las referencias del controlador.
///   4. Registra todas las escenas de Assets/Scenes en Build Settings, con
///      MainMenu como escena inicial (índice 0).
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
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return; // el usuario canceló

        bool createdScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath) == null;

        Scene scene = createdScene
            ? EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single)
            : EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);

        // Asegura el controlador en la escena.
        var controller = Object.FindObjectOfType<MainMenuController>();
        if (controller == null)
        {
            var go = new GameObject("MainMenu");
            controller = go.AddComponent<MainMenuController>();
        }

        // Construye la UI solo si aún no existe (para no pisar tus ajustes manuales).
        bool builtUI = GameObject.Find("MainMenuCanvas") == null;
        if (builtUI)
            MainMenuBuilder.BuildInScene(controller);

        if (createdScene)
            EditorSceneManager.SaveScene(scene, MainMenuPath);
        else
            EditorSceneManager.SaveScene(scene);

        RegisterBuildScenes();

        EditorUtility.DisplayDialog(
            "Menú Principal configurado",
            (createdScene ? "Se creó la escena MainMenu.\n" : "Se abrió la escena MainMenu existente.\n") +
            (builtUI
                ? "Se construyó la UI como objetos reales (editables en la Hierarchy) y se cablearon las referencias.\n"
                : "La UI del menú ya existía; no se tocó nada.\n") +
            "Todas las escenas quedaron en Build Settings, con MainMenu como inicial.\n\n" +
            "Pulsa Play para probar. Puedes mover y reestilizar los objetos libremente.",
            "Genial");
    }

    [MenuItem("YGO/Setup/Reconstruir UI del Menú (borra la actual)")]
    public static void RebuildUI()
    {
        var controller = Object.FindObjectOfType<MainMenuController>();
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Sin menú en la escena",
                "Abre la escena MainMenu (o usa 'Configurar Menú Principal') antes de reconstruir.", "Ok");
            return;
        }

        bool ok = EditorUtility.DisplayDialog("Reconstruir UI del menú",
            "Esto BORRA el canvas actual del menú y lo genera de nuevo. " +
            "Perderás los ajustes manuales de posición y estilo. ¿Continuar?",
            "Sí, reconstruir", "Cancelar");
        if (!ok) return;

        MainMenuBuilder.BuildInScene(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
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

    /// <summary>
    /// Deja en Build Settings todas las escenas de Assets/Scenes, con MainMenu
    /// primero (índice 0) para que sea la que arranca el juego. Público para que
    /// otros setups (p. ej. Duelo Libre) reutilicen el mismo registro.
    /// </summary>
    public static void RegisterBuildScenes()
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
