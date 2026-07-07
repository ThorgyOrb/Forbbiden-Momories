using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Recrea la escena de DUELO desde cero: borra el contenido anterior de
/// DuelScene.unity, monta el <see cref="DuelController"/> + la UI editable
/// (vía <see cref="DuelSceneBuilder"/>) y reregistra Build Settings.
/// Menú:  YGO > Setup > ...
/// </summary>
public static class DuelSceneSetup
{
    private const string ScenesDir = "Assets/Scenes";
    private const string DuelPath = ScenesDir + "/DuelScene.unity";

    [MenuItem("YGO/Setup/Reconstruir Escena de Duelo (desde cero)")]
    public static void RebuildDuelScene()
    {
        bool existed = AssetDatabase.LoadAssetAtPath<SceneAsset>(DuelPath) != null;
        if (existed)
        {
            bool ok = EditorUtility.DisplayDialog("Reconstruir Escena de Duelo",
                "Esto REEMPLAZA por completo DuelScene.unity con la nueva escena de duelo " +
                "(perderás cualquier ajuste manual de la escena vieja). ¿Continuar?",
                "Sí, reconstruir", "Cancelar");
            if (!ok) return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // Escena nueva vacía (con cámara/luz por defecto) que sustituye a la vieja.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var go = new GameObject("DuelController");
        var controller = go.AddComponent<DuelController>();

        DuelSceneBuilder.BuildInScene(controller);

        EditorSceneManager.SaveScene(scene, DuelPath);
        MainMenuSetup.RegisterBuildScenes();

        EditorUtility.DisplayDialog(
            "Escena de Duelo lista",
            (existed ? "DuelScene.unity fue reemplazada por la nueva escena.\n"
                     : "Se creó DuelScene.unity.\n") +
            "UI construida como objetos editables y todas las referencias cableadas.\n" +
            "Escenas reregistradas en Build Settings.\n\n" +
            "Entra desde Historia o Duelo Libre (o pulsa Play aquí para probar con " +
            "el config de prueba).",
            "Genial");
    }

    [MenuItem("YGO/Setup/Reconstruir UI del Duelo (mantiene la escena)")]
    public static void RebuildUI()
    {
        var controller = Object.FindObjectOfType<DuelController>();
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Sin escena",
                "Abre DuelScene (o usa 'Reconstruir Escena de Duelo') antes de reconstruir la UI.", "Ok");
            return;
        }

        bool ok = EditorUtility.DisplayDialog("Reconstruir UI del Duelo",
            "Esto BORRA el canvas actual del duelo y lo genera de nuevo. ¿Continuar?",
            "Sí, reconstruir", "Cancelar");
        if (!ok) return;

        DuelSceneBuilder.BuildInScene(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }
}
