using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Crea una escena de PRUEBA con un botón que otorga TODAS las cartas del juego
/// (3 copias de cada una) a la colección persistente. El componente
/// <see cref="TestGrantAllCards"/> construye su propia UI en runtime, así que la
/// escena solo necesita montar ese GameObject.
/// Menú:  YGO > Setup > ...
/// </summary>
public static class TestGrantCardsSetup
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = ScenesDir + "/TestGrantCardsScene.unity";

    [MenuItem("YGO/Setup/Crear Escena de Test (Obtener todas las cartas)")]
    public static void CreateScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var go = new GameObject("TestGrantAllCards");
        go.AddComponent<TestGrantAllCards>();   // arma su Canvas/botones en runtime

        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);

        EditorUtility.DisplayDialog(
            "Escena de Test lista",
            "Se creó TestGrantCardsScene.unity.\n\n" +
            "Pulsa Play y usa el botón para obtener 3 copias de cada carta del juego.\n" +
            "Se guarda en la colección persistente (la misma que lee el Constructor de Deck),\n" +
            "así que después puedes ir al Constructor y armar cualquier mazo.",
            "Genial");
    }

    /// <summary>Añade la escena a Build Settings si no está (idempotente).</summary>
    private static void AddToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
