using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Crea el <see cref="DuelAudioBank"/> en Assets/Resources para que el duelo lo cargue
/// solo. Luego basta arrastrar tus AudioClips a sus campos.
/// Menú: YGO > Audio > ...
/// </summary>
public static class DuelAudioSetup
{
    private const string Dir = "Assets/Resources";
    private const string BankPath = Dir + "/DuelAudioBank.asset";

    [MenuItem("YGO/Audio/Crear Duel Audio Bank (Resources)")]
    public static void CreateBank()
    {
        var existing = AssetDatabase.LoadAssetAtPath<DuelAudioBank>(BankPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            EditorUtility.DisplayDialog("Duel Audio Bank",
                "Ya existe en Assets/Resources/DuelAudioBank.asset.\nLo seleccioné para que asignes tus clips.", "Ok");
            return;
        }

        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        var bank = ScriptableObject.CreateInstance<DuelAudioBank>();
        AssetDatabase.CreateAsset(bank, BankPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = bank;
        EditorGUIUtility.PingObject(bank);
        EditorUtility.DisplayDialog("Duel Audio Bank",
            "Se creó Assets/Resources/DuelAudioBank.asset.\n\n" +
            "Arrastra tus AudioClips (música de fondo y efectos) a sus campos. El duelo los " +
            "cargará y sonará automáticamente — los que dejes vacíos no suenan.", "Genial");
    }
}
