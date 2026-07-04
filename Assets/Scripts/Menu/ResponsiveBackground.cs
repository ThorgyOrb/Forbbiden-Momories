using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hace que una imagen de fondo CUBRA toda la pantalla sin deformarse, a
/// cualquier resolución o relación de aspecto (como "background-size: cover"
/// en CSS): escala la imagen uniformemente hasta llenar el área del Canvas,
/// recortando lo que sobre por los bordes.
///
/// Uso: arrastra TU sprite al campo "Source Image" del Image de este objeto.
/// Mientras no haya sprite, el componente oculta la imagen (para que se vea el
/// color de fondo que hay detrás en vez de un rectángulo blanco).
///
/// Funciona también en el editor ([ExecuteAlways]): en cuanto asignes tu arte,
/// se ajusta al instante en la vista de escena/juego.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class ResponsiveBackground : MonoBehaviour
{
    private Image _img;
    private RectTransform _rt;
    private RectTransform _parent;

    private Vector2 _lastParentSize;
    private Sprite _lastSprite;
    private bool _initialized;

    void OnEnable()
    {
        Cache();
        Apply();
    }

    void OnRectTransformDimensionsChange()
    {
        if (_initialized) Apply();
    }

    void Update()
    {
        if (!_initialized) Cache();
        if (_parent == null) return;

        // Recalcula solo si cambió el tamaño del Canvas o el sprite (barato).
        if (_parent.rect.size != _lastParentSize || _img.sprite != _lastSprite)
            Apply();
    }

    private void Cache()
    {
        _img = GetComponent<Image>();
        _rt = (RectTransform)transform;
        _parent = transform.parent as RectTransform;
        _img.preserveAspect = false; // controlamos el tamaño exacto nosotros
        _initialized = true;
    }

    private void Apply()
    {
        if (_parent == null) return;

        _lastParentSize = _parent.rect.size;
        _lastSprite = _img.sprite;

        // Anclado al centro; nosotros fijamos el tamaño.
        _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0.5f, 0.5f);
        _rt.anchoredPosition = Vector2.zero;

        float pw = _parent.rect.width;
        float ph = _parent.rect.height;
        if (pw <= 0f || ph <= 0f) return;

        // Sin sprite: no dibujar (deja ver el color de fondo de detrás).
        if (_img.sprite == null)
        {
            _img.enabled = false;
            _rt.sizeDelta = new Vector2(pw, ph);
            return;
        }

        _img.enabled = true;

        Rect r = _img.sprite.rect;
        float spriteAspect = r.width / r.height;
        float parentAspect = pw / ph;

        float w, h;
        if (parentAspect > spriteAspect)
        {
            // El área es más ancha que la imagen → ajusta por ancho, sobra alto.
            w = pw;
            h = pw / spriteAspect;
        }
        else
        {
            // El área es más alta → ajusta por alto, sobra ancho.
            h = ph;
            w = ph * spriteAspect;
        }
        _rt.sizeDelta = new Vector2(w, h);
    }
}
