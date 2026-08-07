using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta de editor para dejar la escena del Constructor de Deck lista:
/// crea/abre DeckBuilderScene, asegura el <see cref="DeckBuilderController"/>,
/// construye la UI editable (vía <see cref="DeckBuilderBuilder"/>) y reregistra
/// las escenas en Build Settings.  Menú:  YGO > Setup > ...
/// </summary>
public static class DeckBuilderSetup
{
    private const string ScenesDir = "Assets/Scenes";
    private const string DeckPath = ScenesDir + "/DeckBuilderScene.unity";

    [MenuItem("YGO/Setup/Configurar Constructor de Deck (escena + Build Settings)")]
    public static void ConfigureDeckBuilder()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        bool createdScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DeckPath) == null;

        Scene scene = createdScene
            ? EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single)
            : EditorSceneManager.OpenScene(DeckPath, OpenSceneMode.Single);

        var controller = Object.FindObjectOfType<DeckBuilderController>();
        if (controller == null)
        {
            var go = new GameObject("DeckBuilder");
            controller = go.AddComponent<DeckBuilderController>();
        }

        bool builtUI = GameObject.Find("DeckBuilderCanvas") == null;
        if (builtUI)
            DeckBuilderBuilder.BuildInScene(controller);

        if (createdScene)
            EditorSceneManager.SaveScene(scene, DeckPath);
        else
            EditorSceneManager.SaveScene(scene);

        MainMenuSetup.RegisterBuildScenes();

        EditorUtility.DisplayDialog(
            "Constructor de Deck configurado",
            (createdScene ? "Se creó la escena DeckBuilderScene.\n" : "Se abrió la escena existente.\n") +
            (builtUI ? "Se construyó la UI editable y se cablearon las referencias.\n"
                     : "La UI ya existía; no se tocó.\n") +
            "Escenas reregistradas en Build Settings.\n\n" +
            "El botón 'Constructor de Deck' del menú ya apunta a esta escena.",
            "Genial");
    }

    [MenuItem("YGO/Setup/Reconstruir UI del Constructor de Deck (borra la actual)")]
    public static void RebuildUI()
    {
        var controller = Object.FindObjectOfType<DeckBuilderController>();
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Sin escena",
                "Abre DeckBuilderScene (o usa 'Configurar Constructor de Deck') antes de reconstruir.", "Ok");
            return;
        }

        bool ok = EditorUtility.DisplayDialog("Reconstruir UI del Constructor de Deck",
            "Esto BORRA el canvas actual y lo genera de nuevo. Perderás los ajustes manuales. ¿Continuar?",
            "Sí, reconstruir", "Cancelar");
        if (!ok) return;

        DeckBuilderBuilder.BuildInScene(controller);
        // Guardar aquí mismo: BuildInScene solo marca la escena como sucia, y si el
        // usuario cierra sin Ctrl+S se pierde la reconstrucción (y con ella el swap
        // al prefab de carta nuevo). Guardar evita esa trampa.
        var scene = controller.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
