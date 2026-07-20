using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aura violeta suave y PAREJA alrededor de la carta seleccionada en la grilla
/// (como la librería de referencia). Es un resplandor exterior emplumado, hecho con
/// un sprite 9-slice generado por código para que el grosor del brillo sea uniforme
/// en los cuatro lados sin deformarse. Late muy suavemente. Lo instancia y reparenta
/// <see cref="LibraryGodsController"/> DETRÁS de la carta.
/// </summary>
[RequireComponent(typeof(Image))]
public class SelectionGlow : MonoBehaviour
{
    public Color color = new Color(0.60f, 0.40f, 1f);
    public float minAlpha = 0.55f;
    public float maxAlpha = 0.95f;
    public float speed = 2.2f;    // latido lento y sutil

    // Ancho del emplumado del brillo, en px de la textura. Debe coincidir con el
    // margen (offsets) con que el controlador agranda el aura respecto a la carta.
    public const int Feather = 10;

    private Image _img;

    void Awake()
    {
        _img = GetComponent<Image>();
        _img.raycastTarget = false;
        _img.sprite = GlowSprite();
        _img.type = Image.Type.Sliced;
        _img.pixelsPerUnitMultiplier = 1f;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f;
        var c = color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        _img.color = c;
    }

    // Resplandor EXTERIOR: transparente en el centro (queda tras la carta, no la tiñe),
    // máximo en el borde de la carta y cae suave hacia afuera. 9-slice (border = Feather)
    // para que el grosor del brillo sea uniforme en los cuatro lados al estirarse.
    private static Sprite _glow;
    private static Sprite GlowSprite()
    {
        if (_glow != null) return _glow;
        const int size = 72;
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dEdge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                float a;
                if (dEdge >= Feather) a = 0f;              // centro transparente (lo tapa la carta)
                else { float e = dEdge / Feather; a = e * e * (3f - 2f * e); } // 0 afuera → 1 en el borde carta
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        _glow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                              SpriteMeshType.FullRect, new Vector4(Feather, Feather, Feather, Feather));
        return _glow;
    }
}
