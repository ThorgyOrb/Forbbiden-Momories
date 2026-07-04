using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta de editor para dejar la escena de Duelo Libre lista con un clic:
///   1. Crea/abre Assets/Scenes/FreeDuelScene.unity.
///   2. Asegura un GameObject con el <see cref="FreeDuelController"/>.
///   3. Construye la UI editable (vía <see cref="FreeDuelBuilder"/>) y cablea todo.
///   4. Reregistra las escenas en Build Settings.
///
/// Menú de Unity:  YGO > Setup > ...
/// </summary>
public static class FreeDuelSetup
{
    private const string ScenesDir = "Assets/Scenes";
    private const string FreeDuelPath = ScenesDir + "/FreeDuelScene.unity";

    [MenuItem("YGO/Setup/Configurar Duelo Libre (escena + Build Settings)")]
    public static void ConfigureFreeDuel()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        bool createdScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(FreeDuelPath) == null;

        Scene scene = createdScene
            ? EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single)
            : EditorSceneManager.OpenScene(FreeDuelPath, OpenSceneMode.Single);

        var controller = Object.FindObjectOfType<FreeDuelController>();
        if (controller == null)
        {
            var go = new GameObject("FreeDuel");
            controller = go.AddComponent<FreeDuelController>();
        }

        bool builtUI = GameObject.Find("FreeDuelCanvas") == null;
        if (builtUI)
            FreeDuelBuilder.BuildInScene(controller);

        if (createdScene)
            EditorSceneManager.SaveScene(scene, FreeDuelPath);
        else
            EditorSceneManager.SaveScene(scene);

        MainMenuSetup.RegisterBuildScenes();

        EditorUtility.DisplayDialog(
            "Duelo Libre configurado",
            (createdScene ? "Se creó la escena FreeDuelScene.\n" : "Se abrió la escena existente.\n") +
            (builtUI ? "Se construyó la UI editable y se cablearon las referencias.\n"
                     : "La UI ya existía; no se tocó.\n") +
            "Escenas reregistradas en Build Settings.\n\n" +
            "El botón 'Duelo Libre' del menú ya apunta a esta escena.",
            "Genial");
    }

    [MenuItem("YGO/Setup/Reconstruir UI del Duelo Libre (borra la actual)")]
    public static void RebuildUI()
    {
        var controller = Object.FindObjectOfType<FreeDuelController>();
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Sin escena de Duelo Libre",
                "Abre FreeDuelScene (o usa 'Configurar Duelo Libre') antes de reconstruir.", "Ok");
            return;
        }

        bool ok = EditorUtility.DisplayDialog("Reconstruir UI del Duelo Libre",
            "Esto BORRA el canvas actual y lo genera de nuevo. Perderás los ajustes manuales. ¿Continuar?",
            "Sí, reconstruir", "Cancelar");
        if (!ok) return;

        FreeDuelBuilder.BuildInScene(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }
}
