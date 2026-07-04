using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hace que un <see cref="CanvasScaler"/> se adapte a CUALQUIER relación de
/// aspecto sin recortar nada de la interfaz.
///
/// La idea: el CanvasScaler en modo "Scale With Screen Size" con
/// "Match Width Or Height" puede ajustar por ancho (match = 0) o por alto
/// (match = 1). Este componente elige automáticamente:
///
///   · Pantalla MÁS ANCHA que la de referencia (ultrawide 21:9…) → ajusta por ALTO.
///   · Pantalla MÁS ALTA/ESTRECHA (vertical, móvil 9:16…)       → ajusta por ANCHO.
///
/// Con esa regla, el lienzo lógico nunca es más pequeño que la resolución de
/// referencia (1920x1080) en ninguna dimensión, así que todo lo que quepa en el
/// diseño 1920x1080 se ve completo en cualquier pantalla. Reacciona también a
/// cambios de tamaño de ventana en tiempo real.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class ResponsiveCanvasScaler : MonoBehaviour
{
    private CanvasScaler _scaler;
    private int _lastWidth;
    private int _lastHeight;

    void Awake()
    {
        _scaler = GetComponent<CanvasScaler>();
        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
    }

    void OnEnable() => Apply();

    void Update()
    {
        if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            Apply();
    }

    private void Apply()
    {
        _lastWidth = Screen.width;
        _lastHeight = Screen.height;

        Vector2 refRes = _scaler.referenceResolution;
        if (refRes.x <= 0f || refRes.y <= 0f) return;

        float referenceAspect = refRes.x / refRes.y;
        float currentAspect = (float)Screen.width / Mathf.Max(1, Screen.height);

        // Más ancha que la referencia → fija el alto (1); si no, fija el ancho (0).
        _scaler.matchWidthOrHeight = (currentAspect >= referenceAspect) ? 1f : 0f;
    }
}
