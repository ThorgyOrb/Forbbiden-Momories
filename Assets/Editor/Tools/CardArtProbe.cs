using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Comprueba que el arte de las cartas <b>se ve de verdad</b>, no solo que el archivo
/// exista: coge una muestra del catálogo, pide <see cref="CardData.Artwork"/> (que es lo
/// mismo que hace la UI) y verifica que devuelve un sprite decodificado con tamaño.
///
/// Existe porque en el Inspector el campo <c>artwork</c> de las cartas importadas aparece
/// vacío —y parece que falta arte— cuando en realidad la imagen se resuelve al vuelo desde
/// StreamingAssets vía <see cref="CardArtLoader"/>. Esta prueba es la forma rápida de
/// distinguir "está vacío por diseño" de "está roto".
///
/// Menú: YGO ▸ Cartas ▸ Probar carga de arte.
/// </summary>
public static class CardArtProbe
{
    const int SampleSize = 40;

    [MenuItem("YGO/Cartas/Probar carga de arte")]
    public static void Run()
    {
        var cards = Resources.LoadAll<CardData>("Cards/Data");
        if (cards.Length == 0) { Debug.LogError("CardArtProbe: catálogo vacío."); return; }

        // Muestra repartida por todo el catálogo, no las primeras N.
        int step = Mathf.Max(1, cards.Length / SampleSize);
        var sample = Enumerable.Range(0, SampleSize)
                               .Select(i => cards[Mathf.Min(i * step, cards.Length - 1)])
                               .Distinct()
                               .ToList();

        int ok = 0, empty = 0, noSource = 0;
        var failures = new System.Collections.Generic.List<string>();

        foreach (var c in sample)
        {
            bool hasSource = c.artwork != null || !string.IsNullOrEmpty(c.artFile);
            if (!hasSource) { noSource++; continue; }

            var sprite = c.Artwork;
            if (sprite != null && sprite.texture != null && sprite.texture.width > 1)
            {
                ok++;
                continue;
            }

            empty++;
            failures.Add($"{c.cardName} (artFile='{c.artFile}')");
        }

        var first = sample.FirstOrDefault(c => c.Artwork != null);
        string example = first != null
            ? $"{first.cardName}: {first.Artwork.texture.width}x{first.Artwork.texture.height} px desde '{first.artFile}'"
            : "—";

        string report =
            $"Prueba de carga de arte — muestra de {sample.Count} cartas de {cards.Length}\n" +
            $"   Arte cargado correctamente: {ok}\n" +
            $"   Con origen pero NO carga:   {empty}\n" +
            $"   Sin arte asignado:          {noSource}\n" +
            $"   Ejemplo: {example}";

        foreach (var f in failures.Take(10)) report += "\n   ✗ " + f;

        if (empty == 0) Debug.Log(report);
        else Debug.LogWarning(report);

        CardArtLoader.ClearCache();
    }
}
