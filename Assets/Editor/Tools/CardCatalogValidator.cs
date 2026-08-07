using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Comprueba la salud del catálogo completo tal y como lo verá el juego: carga los
/// <see cref="CardData"/> igual que <c>LibraryCatalog</c> y avisa de lo que rompería en
/// tiempo de ejecución — ids repetidos (indexados con diccionario), arte que apunta a un
/// archivo inexistente, cartas sin nombre o monstruos sin estadísticas.
///
/// Vale la pena pasarlo tras cada importación masiva: 14.000 cartas no se revisan a ojo.
///
/// Menú: YGO ▸ Cartas ▸ Validar catálogo.
/// </summary>
public static class CardCatalogValidator
{
    [MenuItem("YGO/Cartas/Validar catálogo")]
    public static void Validate()
    {
        // Primero por el mismo camino que el juego, para que quede registrado en el log
        // cuánto tarda el arranque real del catálogo. Debajo se usa LoadAll directo porque
        // LibraryCatalog descarta los ids repetidos y aquí justamente hay que detectarlos.
        LibraryCatalog.EnsureLoaded();

        var cards = Resources.LoadAll<CardData>("Cards/Data");
        if (cards.Length == 0)
        {
            Debug.LogError("Validador: no hay ningún CardData en Resources/Cards/Data.");
            return;
        }

        var byId = new Dictionary<int, CardData>(cards.Length);
        var duplicateIds = new List<string>();
        var missingArt = new List<string>();
        var noName = 0;
        var noArtField = 0;
        var monstersNoStats = 0;
        var byCategory = new Dictionary<CardCategory, int>();

        string streaming = Application.streamingAssetsPath;

        foreach (var c in cards)
        {
            if (byId.TryGetValue(c.cardId, out var prev))
                duplicateIds.Add($"{c.cardId}: '{prev.cardName}' vs '{c.cardName}'");
            else
                byId[c.cardId] = c;

            if (string.IsNullOrWhiteSpace(c.cardName)) noName++;

            if (c.artwork == null)
            {
                if (string.IsNullOrEmpty(c.artFile)) noArtField++;
                else if (!File.Exists(Path.Combine(streaming, c.artFile)))
                    missingArt.Add($"{c.cardName} → {c.artFile}");
            }

            if (c.IsMonster && c.baseAtk == 0 && c.baseDef == 0 && c.stars == 0)
                monstersNoStats++;

            byCategory.TryGetValue(c.cardCategory, out int n);
            byCategory[c.cardCategory] = n + 1;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Validación del catálogo — {cards.Length} cartas");
        foreach (var kv in byCategory.OrderByDescending(k => k.Value))
            sb.AppendLine($"   · {kv.Key}: {kv.Value}");
        sb.AppendLine($"Ids únicos: {byId.Count}");
        sb.AppendLine($"Ids repetidos: {duplicateIds.Count}");
        sb.AppendLine($"Sin nombre: {noName}");
        sb.AppendLine($"Sin arte asignado (ni asset ni archivo): {noArtField}");
        sb.AppendLine($"Arte apuntando a un archivo inexistente: {missingArt.Count}");
        sb.AppendLine($"Monstruos sin ATK/DEF/nivel: {monstersNoStats}");

        foreach (var d in duplicateIds.Take(10)) sb.AppendLine("   ✗ id repetido " + d);
        foreach (var m in missingArt.Take(10)) sb.AppendLine("   ✗ falta arte " + m);

        bool ok = duplicateIds.Count == 0 && missingArt.Count == 0 && noName == 0;
        if (ok) Debug.Log(sb.ToString());
        else Debug.LogWarning(sb.ToString());
    }
}
