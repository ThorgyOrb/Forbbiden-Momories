using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ajusta el <see cref="CanvasScaler"/> en modo "Scale With Screen Size" para que
/// el diseño de referencia (p. ej. 1920×1080) SIEMPRE quepa, sea cual sea el
/// tamaño/relación de la ventana, en lugar de recortarse:
///   • Pantalla más ancha que la referencia  → iguala por alto  (match = 1).
///   • Pantalla más alta/estrecha             → iguala por ancho (match = 0).
/// Así nada del catálogo queda fuera de cuadro al cambiar de resolución.
///
/// <see cref="LibraryManager"/> lo añade en runtime al canvas del catálogo; no
/// hay que colocarlo a mano ni tocar la escena.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class ResponsiveCanvasMatch : MonoBehaviour
{
    private CanvasScaler _scaler;
    private int _lastW, _lastH;

    void Awake() => _scaler = GetComponent<CanvasScaler>();
    void OnEnable() => Apply();

    void Update()
    {
        if (Screen.width != _lastW || Screen.height != _lastH)
            Apply();
    }

    private void Apply()
    {
        _lastW = Screen.width;
        _lastH = Screen.height;

        if (_scaler == null || _scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            return;

        Vector2 refRes = _scaler.referenceResolution;
        if (refRes.x <= 0f || refRes.y <= 0f) return;

        float refAspect = refRes.x / refRes.y;
        float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);

        _scaler.matchWidthOrHeight = screenAspect >= refAspect ? 1f : 0f;
    }
}
