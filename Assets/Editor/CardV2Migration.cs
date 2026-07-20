using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Repunta a la carta V2 (CardMonsterV2) las referencias de prefab que viven en
/// escenas SIN generador. Hoy solo LibraryCatalogScene: su grilla se cablea a mano
/// en el campo <c>cardSlotPrefab</c> de LibraryManager. El resto de escenas
/// (Library of the Gods, constructor de deck, duelo) hornean la carta desde sus
/// generadores, así que se arreglan solas al regenerarlas.
///
/// Se hace por AssetDatabase y SerializedObject, NO editando el YAML de la escena:
/// el prefab V2 se regenera desde su builder y sus fileID internos cambian, así que
/// una referencia escrita a mano quedaría rota (carta "Missing" en la grilla).
///
/// Menú: YGO ▸ Cartas ▸ Repuntar catálogo a la carta V2.
/// </summary>
public static class CardV2Migration
{
    const string CardV2Path       = "Assets/Resources/Prefabs/CardMonsterV2.prefab";
    const string CatalogScenePath = "Assets/Scenes/LibraryCatalogScene.unity";

    [MenuItem("YGO/Cartas/Repuntar catálogo a la carta V2")]
    public static void Run()
    {
        var v2 = AssetDatabase.LoadAssetAtPath<GameObject>(CardV2Path);
        if (v2 == null)
        {
            Debug.LogError($"CardV2Migration: falta {CardV2Path}. Constrúyelo antes con " +
                           "YGO ▸ Cartas ▸ Construir Prefab Carta Monstruo V2.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.OpenScene(CatalogScenePath, OpenSceneMode.Single);
        var manager = Object.FindObjectOfType<LibraryManager>();
        if (manager == null)
        {
            Debug.LogError($"CardV2Migration: no hay ningún LibraryManager en {CatalogScenePath}.");
            return;
        }

        var so = new SerializedObject(manager);
        var prop = so.FindProperty("cardSlotPrefab");
        if (prop == null)
        {
            Debug.LogError("CardV2Migration: LibraryManager ya no tiene el campo 'cardSlotPrefab'.");
            return;
        }

        var before = prop.objectReferenceValue;
        if (before == v2)
        {
            Debug.Log($"CardV2Migration: {CatalogScenePath} ya usaba {v2.name}; nada que hacer.");
            return;
        }

        prop.objectReferenceValue = v2;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string prev = before != null ? before.name : "(vacío)";
        Debug.Log($"CardV2Migration: la grilla de {CatalogScenePath} pasa de {prev} a {v2.name}.");
    }
}
