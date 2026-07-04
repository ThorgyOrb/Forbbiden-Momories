using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta de editor para dejar la escena del MODO HISTORIA lista: crea/abre
/// StoryScene, asegura el <see cref="StoryController"/>, construye la UI editable
/// (vía <see cref="StoryBuilder"/>) y reregistra las escenas en Build Settings.
/// Menú:  YGO > Setup > ...
/// </summary>
public static class StorySetup
{
    private const string ScenesDir = "Assets/Scenes";
    private const string StoryPath = ScenesDir + "/StoryScene.unity";

    [MenuItem("YGO/Setup/Configurar Modo Historia (escena + Build Settings)")]
    public static void ConfigureStory()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        bool createdScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(StoryPath) == null;

        Scene scene = createdScene
            ? EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single)
            : EditorSceneManager.OpenScene(StoryPath, OpenSceneMode.Single);

        var controller = Object.FindObjectOfType<StoryController>();
        if (controller == null)
        {
            var go = new GameObject("Story");
            controller = go.AddComponent<StoryController>();
        }

        bool builtUI = GameObject.Find("StoryCanvas") == null;
        if (builtUI)
            StoryBuilder.BuildInScene(controller);

        if (createdScene)
            EditorSceneManager.SaveScene(scene, StoryPath);
        else
            EditorSceneManager.SaveScene(scene);

        MainMenuSetup.RegisterBuildScenes();

        EditorUtility.DisplayDialog(
            "Modo Historia configurado",
            (createdScene ? "Se creó la escena StoryScene.\n" : "Se abrió la escena existente.\n") +
            (builtUI ? "Se construyó la UI editable y se cablearon las referencias.\n"
                     : "La UI ya existía; no se tocó.\n") +
            "Escenas reregistradas en Build Settings.\n\n" +
            "El botón 'Historia' (y 'Nueva Partida'/'Continuar') del menú ya apunta a esta escena.",
            "Genial");
    }

    [MenuItem("YGO/Setup/Reconstruir UI del Modo Historia (borra la actual)")]
    public static void RebuildUI()
    {
        var controller = Object.FindObjectOfType<StoryController>();
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Sin escena",
                "Abre StoryScene (o usa 'Configurar Modo Historia') antes de reconstruir.", "Ok");
            return;
        }

        bool ok = EditorUtility.DisplayDialog("Reconstruir UI del Modo Historia",
            "Esto BORRA el canvas actual y lo genera de nuevo. Perderás los ajustes manuales. ¿Continuar?",
            "Sí, reconstruir", "Cancelar");
        if (!ok) return;

        StoryBuilder.BuildInScene(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }
}
