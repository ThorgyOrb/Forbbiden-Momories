using UnityEngine;

/// <summary>
/// Escala este RectTransform para que su ALTO coincida con el de <see cref="source"/>,
/// preservando el aspecto nativo. Se usa para mostrar la carta COMPLETA (cuyo
/// layout interno no admite estirado) dentro del contenedor de vuelo del modal,
/// que anima su tamaño: el contenedor sigue mandando posición/rotación/tamaño y
/// la carta lo "rellena" por escala en vez de deformarse.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class RectScaleFitter : MonoBehaviour
{
    public RectTransform source;
    public float nativeHeight = 280f;
    [Tooltip("Opcional: si > 0, también limita por ancho (encaja DENTRO del contenedor).")]
    public float nativeWidth = 0f;

    void LateUpdate()
    {
        if (source == null || nativeHeight <= 0f) return;
        float s = source.rect.height / nativeHeight;
        if (nativeWidth > 0f)
            s = Mathf.Min(s, source.rect.width / nativeWidth);
        transform.localScale = new Vector3(s, s, 1f);
    }
}
