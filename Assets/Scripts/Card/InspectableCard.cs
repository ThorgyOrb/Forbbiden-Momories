using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Hace que una carta se incline siguiendo el puntero (igual que en la maqueta
/// de estilo), para poder apreciar los efectos holo al inspeccionarla. Este
/// componente vive en un "área de golpe" que NO rota; rota un <see cref="target"/>
/// hijo. Al soltar o salir, la carta vuelve al centro con un vaivén sutil para
/// que los reflejos sigan moviéndose.
///
/// Se construye por código desde <see cref="CardDetailPanel"/>; no hace falta
/// tocarlo en el Inspector.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class InspectableCard : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform target;         // la carta que gira
    public float maxAngle = 22f;         // inclinación máxima al llevar el puntero al borde
    public float easeSpeed = 12f;        // suavizado hacia la inclinación objetivo
    public float idleSwayAmp = 4f;       // grados de vaivén en reposo
    public float idleSwaySpeed = 0.7f;
    public Vector2 restEuler = Vector2.zero; // (pitch, yaw) base en reposo: deja la carta
                                             // ya inclinada en 3D aunque nadie interactúe.
                                             // 0 = comportamiento original (vuelve al frente).

    private RectTransform _hit;
    private Camera _cam;
    private bool _hovering, _dragging;
    private Vector2 _dragScreen;
    private Vector2 _curEuler;            // x = pitch, y = yaw

    void Awake()
    {
        _hit = (RectTransform)transform;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        _cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
             ? canvas.worldCamera : null;

        _curEuler = restEuler; // arranca ya en la pose de reposo (sin lerp desde el frente)
    }

    /// <summary>Devuelve la carta a su inclinación de reposo (al abrir/cerrar).</summary>
    public void ResetView()
    {
        _hovering = _dragging = false;
        _curEuler = restEuler;
        if (target != null) target.localRotation = Quaternion.Euler(restEuler.x, restEuler.y, 0f);
    }

    public void OnPointerEnter(PointerEventData e) => _hovering = true;
    public void OnPointerExit(PointerEventData e) => _hovering = false;
    public void OnPointerDown(PointerEventData e) { _dragging = true; _dragScreen = e.position; }
    public void OnDrag(PointerEventData e) => _dragScreen = e.position;
    public void OnPointerUp(PointerEventData e) => _dragging = false;

    void Update()
    {
        if (target == null) return;

        Vector2 aim;
        if (_dragging || _hovering)
        {
            // Seguimiento del puntero CENTRADO en 0 (no en restEuler): así izquierda y
            // derecha giran de forma simétrica. La carta "se endereza" hacia ti al tocarla.
            Vector2 sp = _dragging ? _dragScreen : (Vector2)Input.mousePosition;
            aim = AimFrom(sp);
        }
        else
        {
            // En reposo vuelve a la leve inclinación 3D + un vaivén que mantiene vivos
            // los reflejos aunque nadie toque.
            float t = Time.unscaledTime;
            aim = restEuler + new Vector2(
                Mathf.Sin(t * idleSwaySpeed) * idleSwayAmp,
                Mathf.Cos(t * idleSwaySpeed * 0.8f) * idleSwayAmp);
        }

        _curEuler = Vector2.Lerp(_curEuler, aim, Time.unscaledDeltaTime * easeSpeed);
        target.localRotation = Quaternion.Euler(_curEuler.x, _curEuler.y, 0f);
    }

    // (pitch, yaw) objetivo a partir de un punto de pantalla, relativo al área
    // estacionaria (que no rota, por eso no hay realimentación).
    private Vector2 AimFrom(Vector2 screen)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_hit, screen, _cam, out Vector2 local))
            return Vector2.zero;

        Vector2 half = _hit.rect.size * 0.5f;
        float nx = Mathf.Clamp(local.x / Mathf.Max(1f, half.x), -1f, 1f);
        float ny = Mathf.Clamp(local.y / Mathf.Max(1f, half.y), -1f, 1f);

        // Igual que la maqueta: el borde cercano al puntero "se hunde". Puntero
        // arriba ⇒ el borde superior se aleja (pitch negativo); a los lados, yaw.
        return new Vector2(-ny * maxAngle, nx * maxAngle);
    }
}
