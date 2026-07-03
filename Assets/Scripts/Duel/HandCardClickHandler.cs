using UnityEngine;

// ────────────────────────────────────────────────────────────────────────────
//  HandCardClickHandler
//  Agrega este componente al prefab de carta para detectar clics en la mano.
//  En Main Phase activa el selector de acción; en Battle Phase declara ataque.
// ────────────────────────────────────────────────────────────────────────────
[RequireComponent(typeof(CardDisplay))]
public class HandCardClickHandler : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
{
    private CardDisplay _display;

    void Awake() => _display = GetComponent<CardDisplay>();

    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
    {
        Debug.Log("Click en carta detectado");

        var dm = DuelManager.Instance;
        if (dm == null) { Debug.Log("DuelManager.Instance es NULL"); return; }

        Debug.Log($"Phase={dm.Phase}, IsPlayerTurn={dm.IsPlayerTurn}, CardActionPanel.Instance null? {CardActionPanel.Instance == null}");

        if (dm.Phase == DuelPhase.MainPhase && dm.IsPlayerTurn)
        {
            if (CardActionPanel.Instance == null)
            {
                Debug.LogError("CardActionPanel.Instance es NULL — el panel nunca corrió su Awake().");
                return;
            }
            CardActionPanel.Instance.ShowFor(_display.Data);
        }
        else
        {
            Debug.Log("No se muestra el panel: no estamos en MainPhase o no es turno del jugador.");
        }
    }
}