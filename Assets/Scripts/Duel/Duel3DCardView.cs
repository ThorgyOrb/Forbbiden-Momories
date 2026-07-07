using TMPro;
using UnityEngine;

/// <summary>
/// UNA carta física sobre el tablero 3D. Muestra la carta COMPLETA usando el
/// mismo <see cref="CardDisplay"/> del prefab Card (marco, arte, nombre, stats,
/// estrellas), dentro de un Canvas en World Space que yace sobre la mesa.
///
/// Estructura (la genera DuelSceneBuilder):
///   raíz (este componente + BoxCollider para clics por raycast)
///    ├─ CardCanvas (World Space, rotado para yacer en la mesa) → Card.prefab
///    ├─ StatsCanvas (letrero flotante con ATK/DEF actuales)
///    └─ Highlight (quad de selección)
///
/// La raíz NUNCA se rota (el collider queda alineado al mundo); la orientación
/// Ataque/Defensa se aplica girando el CardCanvas sobre la mesa.
/// </summary>
public class Duel3DCardView : MonoBehaviour
{
    [SerializeField] private RectTransform cardCanvas;     // canvas de la carta
    [SerializeField] private CanvasGroup canvasGroup;      // para fades
    [SerializeField] private TextMeshProUGUI statsLabel;   // ATK/DEF actuales
    [SerializeField] private GameObject highlight;

    private CardDisplay _display;

    /// <summary>CardDisplay interno (instancia del prefab Card).</summary>
    public CardDisplay Display
    {
        get
        {
            if (_display == null) _display = GetComponentInChildren<CardDisplay>(true);
            return _display;
        }
    }

    /// <summary>Pinta la carta con su posición (boca abajo ⇒ se ve el dorso).</summary>
    public void Show(CardData card, CardPosition pos)
    {
        gameObject.SetActive(true);
        if (Display != null)
        {
            Display.Setup(card);
            Display.SetPosition(pos);
        }
        ApplyOrientation(pos);
    }

    /// <summary>Solo actualiza la cara/posición (revelar, cambiar posición).</summary>
    public void SetPosition(CardPosition pos)
    {
        if (Display != null) Display.SetPosition(pos);
        ApplyOrientation(pos);
    }

    /// <summary>Letrero de valores ACTUALES (con terreno/equipos), ej. "ATK 2000 / DEF 1500".</summary>
    public void SetStats(string text)
    {
        if (statsLabel != null) statsLabel.text = text ?? "";
    }

    public void SetHighlight(bool on)
    {
        if (highlight != null) highlight.SetActive(on);
    }

    public CanvasGroup CanvasGroup => canvasGroup;

    /// <summary>Canvas físico de la carta (para orientarla en animaciones).</summary>
    public RectTransform CardCanvas => cardCanvas;

    /// <summary>
    /// Orientación física sobre la mesa: Defensa = girada 90° (apaisada),
    /// igual que en Forbidden Memories. El dorso lo pinta CardDisplay.
    /// </summary>
    private void ApplyOrientation(CardPosition pos)
    {
        if (cardCanvas == null) return;
        bool defense = pos == CardPosition.FaceUpDefense || pos == CardPosition.FaceDownDefense;
        // X=90 para yacer plana mirando al cielo; Y gira la carta sobre la mesa.
        cardCanvas.localRotation = Quaternion.Euler(90f, defense ? 90f : 0f, 0f);
    }
}
