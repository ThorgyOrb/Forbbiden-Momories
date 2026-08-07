using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Llena una grilla por trozos en vez de instanciar todas las entradas de golpe: crea el
/// primer bloque y añade el siguiente cuando el usuario se acerca al final del scroll.
///
/// Existe porque la biblioteca instanciaba un prefab de carta por cada entrada del
/// catálogo. Con 14.651 cartas eso son 14.651 copias de CardMonsterV2 (decenas de hijos,
/// TMP y materiales cada una) más 14.651 JPG decodificados: minutos de congelación y varios
/// GB. Con esto, abrir la biblioteca cuesta un bloque.
///
/// Encuentra el <see cref="ScrollRect"/> subiendo desde el contenedor, así que no hace
/// falta cablear nada nuevo en las escenas ya generadas.
///
/// Sigue siendo llenado incremental, NO virtualización: si el usuario baja hasta el final,
/// acaba habiendo un slot por carta. Lo que elimina es el coste de golpe al abrir.
/// </summary>
public sealed class IncrementalGridFiller
{
    /// <summary>
    /// Tamaño de bloque. Tiene que ser lo bastante grande como para que el contenido
    /// desborde el viewport a la primera; si no, no habría scroll que disparase el
    /// siguiente bloque y la grilla se quedaría a medias.
    /// </summary>
    public const int DefaultChunk = 120;

    /// <summary>
    /// Tope duro de slots vivos. No es solo por memoria de GameObjects: cada slot muestra
    /// un sprite y <see cref="CardArtLoader.MaxCachedSprites"/> tiene que ser MAYOR que
    /// este número, o la caché acabaría destruyendo texturas que aún están en pantalla y
    /// las cartas saldrían en blanco. Si tocas uno, toca el otro.
    /// Pasado el tope, el usuario acota con la búsqueda y los filtros.
    /// </summary>
    public const int DefaultMaxSlots = 600;

    /// <summary>Cuánto queda por debajo (0 = final) para pedir el siguiente bloque.</summary>
    private const float LoadThreshold = 0.2f;

    private readonly ScrollRect _scroll;
    private readonly int _chunk;
    private readonly int _maxSlots;

    private Action<int, int> _spawnRange;
    private int _total;
    private int _spawned;

    public IncrementalGridFiller(Transform content, int chunk = DefaultChunk,
                                 int maxSlots = DefaultMaxSlots)
    {
        _chunk = Mathf.Max(1, chunk);
        _maxSlots = Mathf.Max(_chunk, maxSlots);
        _scroll = content != null ? content.GetComponentInParent<ScrollRect>() : null;

        if (_scroll != null)
            _scroll.onValueChanged.AddListener(OnScroll);
        else
            Debug.LogWarning("IncrementalGridFiller: no encuentro un ScrollRect por encima " +
                             "del contenedor; solo se mostrará el primer bloque de cartas.");
    }

    /// <summary>Cuántas entradas se han instanciado ya.</summary>
    public int Spawned => _spawned;

    /// <summary>Quedan entradas por mostrar y aún cabe alguna.</summary>
    public bool HasMore => _spawned < _total && _spawned < _maxSlots;

    /// <summary>Cuántas quedaron fuera por el tope; 0 si se muestran todas.</summary>
    public int Hidden => Mathf.Max(0, _total - Mathf.Min(_total, _maxSlots));

    /// <summary>
    /// Reinicia el llenado: el llamante ya debe haber destruido los slots anteriores.
    /// <paramref name="spawnRange"/> recibe (índice inicial, cuántas) y crea esos slots.
    /// </summary>
    public void Restart(int total, Action<int, int> spawnRange)
    {
        _total = Mathf.Max(0, total);
        _spawnRange = spawnRange;
        _spawned = 0;

        if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        SpawnNext();
    }

    /// <summary>Suelta el listener del scroll. Llamar desde OnDestroy del controlador.</summary>
    public void Detach()
    {
        if (_scroll != null) _scroll.onValueChanged.RemoveListener(OnScroll);
    }

    private void OnScroll(Vector2 pos)
    {
        // En un scroll vertical, pos.y va de 1 (arriba) a 0 (abajo).
        if (pos.y <= LoadThreshold) SpawnNext();
    }

    private void SpawnNext()
    {
        if (_spawnRange == null || !HasMore) return;

        int count = Mathf.Min(_chunk, Mathf.Min(_total, _maxSlots) - _spawned);
        if (count <= 0) return;
        _spawnRange(_spawned, count);
        _spawned += count;
    }
}
