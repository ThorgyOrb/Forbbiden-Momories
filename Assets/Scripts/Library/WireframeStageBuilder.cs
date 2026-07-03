using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera por código el "cuarto" de líneas blancas sobre negro que se ve
/// detrás del monstruo en el visor (piso con grid en perspectiva + dos
/// paredes). No depende de ningún modelo importado - todo son LineRenderer
/// generados en Awake, así que el prefab final es SOLO este script + un
/// material, nada de meshes que mantener.
///
/// Uso:
///  1. Crea un GameObject vacío, ej. "WireframeStage".
///  2. Agrégale este componente.
///  3. Asigna 'lineMaterial' (ver más abajo qué material usar).
///  4. Ponlo en la layer "ModelViewer" (o la que uses) - el propio script
///     propaga esa layer a todas las líneas que genera.
///  5. Guárdalo como prefab. Vive FIJO en la escena del visor (no se
///     destruye entre monstruos, sólo el modelo va y viene).
///
/// Material recomendado: Shader "Unlit/Color" (o "Sprites/Default"), color
/// blanco, sin necesitar luz - así el grid se ve igual de nítido sin
/// depender de cómo esté iluminada la escena.
/// </summary>
[ExecuteAlways]
public class WireframeStageBuilder : MonoBehaviour
{
    [Header("Material")]
    [SerializeField] private Material lineMaterial; // Unlit/Color, blanco

    [Header("Piso")]
    [SerializeField] private float floorSize = 20f;      // ancho/profundidad total del piso
    [SerializeField] private int floorDivisions = 12;    // nº de celdas por lado
    [SerializeField] private float floorY = 0f;

    [Header("Pared trasera")]
    [SerializeField] private bool buildBackWall = true;
    [SerializeField] private float wallHeight = 12f;
    [SerializeField] private int wallDivisions = 8;

    [Header("Pared lateral (derecha, como en la referencia)")]
    [SerializeField] private bool buildSideWall = true;

    [Header("Visual")]
    [SerializeField] private float lineWidth = 0.03f;
    [SerializeField] private Color lineColor = Color.white;
    [Tooltip("Las líneas más lejanas del centro se atenúan un poco, para dar sensación de profundidad (opcional).")]
    [SerializeField] private bool fadeWithDistance = true;
    [SerializeField] private float minAlpha = 0.35f;

    private readonly List<LineRenderer> _spawned = new();

    void Awake()
    {
        Build();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Reconstruye en el Editor cuando cambias valores en el Inspector,
        // así ves el resultado sin tener que dar Play.
        if (!Application.isPlaying && gameObject.scene.IsValid())
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) Build(); };
    }
#endif

    [ContextMenu("Reconstruir")]
    public void Build()
    {
        Clear();

        if (lineMaterial == null)
        {
            Debug.LogWarning("WireframeStageBuilder: falta 'lineMaterial'. Usa un Unlit/Color blanco.");
            return;
        }

        float half = floorSize * 0.5f;

        // ── Piso: grid en el plano XZ ──────────────────────────────────
        for (int i = 0; i <= floorDivisions; i++)
        {
            float t = -half + (floorSize / floorDivisions) * i;

            // líneas paralelas al eje X
            SpawnLine(
                new Vector3(-half, floorY, t),
                new Vector3(half, floorY, t),
                DistanceAlpha(t, half));

            // líneas paralelas al eje Z
            SpawnLine(
                new Vector3(t, floorY, -half),
                new Vector3(t, floorY, half),
                DistanceAlpha(t, half));
        }

        // ── Pared trasera: grid en el plano XY, en Z = -half ───────────
        if (buildBackWall)
        {
            for (int i = 0; i <= wallDivisions; i++)
            {
                float x = -half + (floorSize / wallDivisions) * i;
                SpawnLine(
                    new Vector3(x, floorY, -half),
                    new Vector3(x, floorY + wallHeight, -half),
                    DistanceAlpha(x, half));
            }
            for (int i = 0; i <= wallDivisions; i++)
            {
                float y = floorY + (wallHeight / wallDivisions) * i;
                SpawnLine(
                    new Vector3(-half, y, -half),
                    new Vector3(half, y, -half),
                    1f);
            }
        }

        // ── Pared lateral: grid en el plano ZY, en X = +half ───────────
        if (buildSideWall)
        {
            for (int i = 0; i <= wallDivisions; i++)
            {
                float z = -half + (floorSize / wallDivisions) * i;
                SpawnLine(
                    new Vector3(half, floorY, z),
                    new Vector3(half, floorY + wallHeight, z),
                    DistanceAlpha(z, half));
            }
            for (int i = 0; i <= wallDivisions; i++)
            {
                float y = floorY + (wallHeight / wallDivisions) * i;
                SpawnLine(
                    new Vector3(half, y, -half),
                    new Vector3(half, y, half),
                    1f);
            }
        }
    }

    private float DistanceAlpha(float t, float half)
    {
        if (!fadeWithDistance || half <= 0f) return 1f;
        float d = Mathf.Abs(t) / half; // 0 en el centro, 1 en el borde
        return Mathf.Lerp(1f, minAlpha, d);
    }

    private void SpawnLine(Vector3 a, Vector3 b, float alpha)
    {
        var go = new GameObject("GridLine");
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.material = lineMaterial;

        Color c = lineColor;
        c.a *= alpha;
        lr.startColor = c;
        lr.endColor = c;

        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.allowOcclusionWhenDynamic = false;

        _spawned.Add(lr);
    }

    private void Clear()
    {
        // Limpia tanto lo trackeado como cualquier hijo huérfano (por si se
        // reconstruyó en un dominio distinto, ej. tras recompilar scripts).
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
        _spawned.Clear();
    }
}
