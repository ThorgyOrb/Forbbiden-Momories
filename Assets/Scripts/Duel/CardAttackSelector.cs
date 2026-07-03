using UnityEngine;

// ────────────────────────────────────────────────────────────────────────────
//  CardAttackSelector
//  Maneja la selección de atacante → objetivo en Battle Phase.
// ────────────────────────────────────────────────────────────────────────────
public class CardAttackSelector : MonoBehaviour
{
    public static CardAttackSelector Instance { get; private set; }

    private int _attackerSlot = -1;

    void Awake()
    {
        Instance = this;
        Debug.Log($"[CardAttackSelector] Awake en '{gameObject.name}' (activeInHierarchy={gameObject.activeInHierarchy})");
    }

    public void SelectAttacker(int slot)
    {
        _attackerSlot = slot;
        Debug.Log($"[CardAttackSelector] Atacante seleccionado: slot {slot}");
    }

    public void SelectTarget(int targetSlot)
    {
        Debug.Log($"[CardAttackSelector] SelectTarget llamado con targetSlot={targetSlot}, _attackerSlot={_attackerSlot}");
        if (_attackerSlot < 0) { Debug.Log("[CardAttackSelector] Abortado: no hay atacante seleccionado todavía."); return; }
        DuelManager.Instance.PlayerAttack(_attackerSlot, targetSlot);
        _attackerSlot = -1;
    }

    public void AttackDirect()
    {
        if (_attackerSlot < 0) return;
        DuelManager.Instance.PlayerAttack(_attackerSlot, -1);
        _attackerSlot = -1;
    }
}