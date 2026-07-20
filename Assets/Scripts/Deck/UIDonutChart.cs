using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gráfico de dona (anillo) que dibuja segmentos de color proporcionales a sus
/// valores, empezando arriba y girando en sentido horario. Se usa para la
/// "DISTRIBUCIÓN" del mazo (Monstruos / Magias / Trampas) en el Constructor.
///
/// No usa sprites: genera la malla en <see cref="OnPopulateMesh"/>. Cámbiale los
/// segmentos con <see cref="SetSegments"/> y se redibuja solo.
/// </summary>
public class UIDonutChart : MaskableGraphic
{
    [Tooltip("Grosor del anillo como fracción del radio (0 = línea fina, 1 = disco lleno).")]
    [Range(0.1f, 1f)] public float thickness = 0.42f;

    [Tooltip("Segmentos por vuelta completa: más = borde más suave.")]
    [Range(24, 240)] public int resolution = 120;

    [Tooltip("Color del anillo cuando el mazo está vacío (sin segmentos).")]
    public Color emptyColor = new Color(1f, 1f, 1f, 0.10f);

    private struct Seg { public float value; public Color color; }
    private readonly List<Seg> _segments = new();

    /// <summary>Define los segmentos (valor, color). Valores ≤ 0 se ignoran.</summary>
    public void SetSegments(IEnumerable<(float value, Color color)> segments)
    {
        _segments.Clear();
        if (segments != null)
            foreach (var (value, color) in segments)
                if (value > 0f) _segments.Add(new Seg { value = value, color = color });
        SetVerticesDirty();
    }

    public void Clear()
    {
        _segments.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        var rect = GetPixelAdjustedRect();
        float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
        if (outer <= 0f) return;
        float inner = outer * (1f - Mathf.Clamp01(thickness));
        Vector2 center = rect.center;

        float total = 0f;
        foreach (var s in _segments) total += s.value;

        // Anillo "vacío" cuando no hay datos.
        if (total <= 0f)
        {
            AddArc(vh, center, inner, outer, 0f, Mathf.PI * 2f, resolution, emptyColor);
            return;
        }

        float start = Mathf.PI * 0.5f; // empieza arriba (12 en punto)
        foreach (var s in _segments)
        {
            float sweep = (s.value / total) * Mathf.PI * 2f;
            int steps = Mathf.Max(2, Mathf.CeilToInt(resolution * (sweep / (Mathf.PI * 2f))));
            // Ángulo negativo = sentido horario.
            AddArc(vh, center, inner, outer, start, start - sweep, steps, s.color);
            start -= sweep;
        }
    }

    private static void AddArc(VertexHelper vh, Vector2 center, float inner, float outer,
                               float angA, float angB, int steps, Color color)
    {
        for (int i = 0; i < steps; i++)
        {
            float t0 = (float)i / steps;
            float t1 = (float)(i + 1) / steps;
            float a0 = Mathf.Lerp(angA, angB, t0);
            float a1 = Mathf.Lerp(angA, angB, t1);

            Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
            Vector2 d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));

            int idx = vh.currentVertCount;
            vh.AddVert(center + d0 * inner, color, Vector2.zero);
            vh.AddVert(center + d0 * outer, color, Vector2.zero);
            vh.AddVert(center + d1 * outer, color, Vector2.zero);
            vh.AddVert(center + d1 * inner, color, Vector2.zero);
            vh.AddTriangle(idx + 0, idx + 1, idx + 2);
            vh.AddTriangle(idx + 2, idx + 3, idx + 0);
        }
    }
}
