using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Carga bajo demanda el arte de las cartas importadas del set de Yu-Gi-Oh.
///
/// Las ~14.000 imágenes viven en <c>Assets/StreamingAssets/CardArt</c> (y
/// <c>AlternateArt</c>), NO como assets de Unity: StreamingAssets se copia tal cual a la
/// build sin pasar por el pipeline de importación. Eso evita importar 2 GB de texturas
/// (horas de proceso y una caché Library enorme) y, sobre todo, evita que
/// <c>Resources.LoadAll&lt;CardData&gt;</c> arrastre a memoria el arte de TODO el catálogo:
/// aquí cada sprite se lee del disco solo cuando alguien lo pide.
///
/// La caché es LRU con tope <see cref="MaxCachedSprites"/>; al desalojar se destruyen
/// sprite y textura para devolver la memoria. Las cartas hechas a mano siguen usando su
/// campo <c>artwork</c> normal y no pasan por aquí (ver <see cref="CardData.Artwork"/>).
/// </summary>
public static class CardArtLoader
{
    /// <summary>Subcarpeta por defecto dentro de StreamingAssets.</summary>
    public const string CardArtFolder = "CardArt";
    public const string AlternateArtFolder = "AlternateArt";

    /// <summary>
    /// Cuántos sprites se mantienen decodificados a la vez (~0,8 MB cada uno).
    /// Tiene que ser MAYOR que <see cref="IncrementalGridFiller.DefaultMaxSlots"/>: si la
    /// caché desaloja una textura que un slot visible sigue usando, esa carta se queda en
    /// blanco. Si subes el tope de slots de la grilla, sube también este.
    /// </summary>
    public const int MaxCachedSprites = 700;

    private static readonly Dictionary<string, Sprite> _cache = new();
    private static readonly LinkedList<string> _lru = new();
    private static readonly HashSet<string> _missing = new();

    /// <summary>
    /// Devuelve el sprite de <paramref name="relativePath"/> (ruta relativa a
    /// StreamingAssets, p. ej. "CardArt/Dark Magician_46986414.jpg"). Null si no existe.
    /// </summary>
    public static Sprite Load(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return null;
        if (_missing.Contains(relativePath)) return null;

        if (_cache.TryGetValue(relativePath, out var cached) && cached != null)
        {
            Touch(relativePath);
            return cached;
        }

        string full = Path.Combine(Application.streamingAssetsPath, relativePath);
        if (!File.Exists(full))
        {
            _missing.Add(relativePath);
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(full);
        }
        catch (IOException e)
        {
            Debug.LogWarning($"CardArtLoader: no se pudo leer {relativePath} ({e.Message}).");
            _missing.Add(relativePath);
            return null;
        }

        // markNonReadable: libera la copia en CPU tras subir la textura a GPU.
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!tex.LoadImage(bytes, true))
        {
            Object.Destroy(tex);
            _missing.Add(relativePath);
            return null;
        }
        tex.name = relativePath;

        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                   new Vector2(0.5f, 0.5f), 100f);
        sprite.name = relativePath;

        _cache[relativePath] = sprite;
        _lru.AddLast(relativePath);
        Evict();
        return sprite;
    }

    private static void Touch(string key)
    {
        _lru.Remove(key);
        _lru.AddLast(key);
    }

    private static void Evict()
    {
        while (_lru.Count > MaxCachedSprites)
        {
            string oldest = _lru.First.Value;
            _lru.RemoveFirst();
            if (!_cache.TryGetValue(oldest, out var sprite)) continue;
            _cache.Remove(oldest);
            Release(sprite);
        }
    }

    /// <summary>
    /// Destruye sprite y textura. Fuera de Play, <c>Object.Destroy</c> no destruye nada y
    /// además Unity lo rechaza, así que en el editor hay que usar DestroyImmediate: sin
    /// esto, las herramientas de editor que pintan cartas iban filtrando texturas.
    /// </summary>
    private static void Release(Sprite sprite)
    {
        if (sprite == null) return;
        var tex = sprite.texture;

        if (Application.isPlaying)
        {
            Object.Destroy(sprite);
            if (tex != null) Object.Destroy(tex);
        }
        else
        {
            Object.DestroyImmediate(sprite);
            if (tex != null) Object.DestroyImmediate(tex);
        }
    }

    /// <summary>Vacía la caché (cambio de escena pesado, o al terminar un duelo).</summary>
    public static void ClearCache()
    {
        foreach (var sprite in _cache.Values) Release(sprite);
        _cache.Clear();
        _lru.Clear();
        _missing.Clear();
    }

    /// <summary>¿Existe el archivo? Útil para el importador y para diagnósticos.</summary>
    public static bool Exists(string relativePath) =>
        !string.IsNullOrEmpty(relativePath) &&
        File.Exists(Path.Combine(Application.streamingAssetsPath, relativePath));
}
