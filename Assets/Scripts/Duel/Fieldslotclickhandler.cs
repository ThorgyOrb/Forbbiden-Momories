using UnityEngine;

// ────────────────────────────────────────────────────────────────────────────
//  FieldSlotClickHandler
//  Para los slots de monstruo en el campo:
//    - En Battle Phase: declara ataques (comportamiento original).
//    - En Main Phase, sobre un slot PROPIO boca abajo: lo revela manualmente.
// ────────────────────────────────────────────────────────────────────────────
public class FieldSlotClickHandler : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
{
    [SerializeField] private bool isPlayerSide = true;
    [SerializeField] private int slotIndex;

    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
    {
        var dm = DuelManager.Instance;
        if (dm == null) { Debug.Log("[FieldSlotClickHandler] DuelManager.Instance es NULL"); return; }
        Debug.Log($"[FieldSlotClickHandler] Click en slot {slotIndex} (isPlayerSide={isPlayerSide}). Phase={dm.Phase}, IsPlayerTurn={dm.IsPlayerTurn}");
        if (!dm.IsPlayerTurn) { Debug.Log("[FieldSlotClickHandler] Abortado: no es tu turno."); return; }

        if (dm.Phase == DuelPhase.MainPhase)
        {
            // Solo se puede voltear manualmente una carta propia, boca abajo,
            // durante la propia Main Phase.
            if (isPlayerSide) dm.PlayerRevealMonster(slotIndex);
            return;
        }

        if (dm.Phase != DuelPhase.BattlePhase) { Debug.Log($"[FieldSlotClickHandler] Abortado: Phase={dm.Phase}, no es BattlePhase."); return; }

        if (CardAttackSelector.Instance == null) { Debug.LogError("[FieldSlotClickHandler] CardAttackSelector.Instance es NULL."); return; }

        if (isPlayerSide)
        {
            // Seleccionar atacante
            CardAttackSelector.Instance.SelectAttacker(slotIndex);
        }
        else
        {
            // Seleccionar objetivo
            CardAttackSelector.Instance.SelectTarget(slotIndex);
        }
    }
}