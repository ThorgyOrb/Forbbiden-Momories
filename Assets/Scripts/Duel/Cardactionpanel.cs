using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ────────────────────────────────────────────────────────────────────────────
//  CardActionPanel
//  Panel de acciones (Invocar / Fusionar / Cancelar) en Main Phase.
//  Singleton de escena. Colócalo en un Panel desactivado inicialmente.
// ────────────────────────────────────────────────────────────────────────────
public class CardActionPanel : MonoBehaviour
{
    public static CardActionPanel Instance { get; private set; }

    [SerializeField] private Button btnSummonAttack;
    [SerializeField] private Button btnSummonDefense;
    [SerializeField] private Button btnAddToFusion;
    [SerializeField] private Button btnCancel;
    [SerializeField] private TMP_Text cardNameLabel;
    [SerializeField] private Button btnGuardianStarA;
    [SerializeField] private Button btnGuardianStarB;

    [Header("Set (invocación oculta — solo Monster)")]
    [SerializeField] private Button btnSetAttack;
    [SerializeField] private Button btnSetDefense;

    [Header("Magia (solo si la carta seleccionada es Spell)")]
    [SerializeField] private Button btnCastSpell;

    [Header("Fusión (panel separado, siempre visible en Main Phase)")]
    [SerializeField] private TMP_Text fusionMaterialsLabel; // ej: "Materiales: Kuriboh, Celtic Guardian"

    private CardData _selectedCard;
    private GuardianStar _chosenStar;
    private List<CardData> _fusionMaterials = new();

    public int FusionMaterialCount => _fusionMaterials.Count;

    void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    public void ShowFor(CardData card)
    {
        _selectedCard = card;
        _chosenStar = card.starA;
        gameObject.SetActive(true);

        if (cardNameLabel != null) cardNameLabel.text = card.cardName;

        bool isMonster = card.IsMonster;
        bool isSpell = card.IsSpell;
        bool isEquip = card.IsEquip;

        // ── Opciones de Monstruo: invocar (ATK/DEF) + Set oculto + Guardian Star ──
        if (btnSummonAttack != null) btnSummonAttack.gameObject.SetActive(isMonster);
        if (btnSummonDefense != null) btnSummonDefense.gameObject.SetActive(isMonster);
        if (btnSetAttack != null) btnSetAttack.gameObject.SetActive(isMonster);
        if (btnSetDefense != null) btnSetDefense.gameObject.SetActive(isMonster);
        if (btnGuardianStarA != null) btnGuardianStarA.gameObject.SetActive(isMonster);
        if (btnGuardianStarB != null) btnGuardianStarB.gameObject.SetActive(isMonster);

        // ── Opción de Magia: jugar directamente ──────────────────────────
        if (btnCastSpell != null) btnCastSpell.gameObject.SetActive(isSpell);

        // ── Fusión: disponible para Monstruos y Equipos, no para Spells ──
        if (btnAddToFusion != null) btnAddToFusion.gameObject.SetActive(isMonster || isEquip);

        // Botones Guardian Star
        if (isMonster && btnGuardianStarA != null)
        {
            btnGuardianStarA.GetComponentInChildren<TMP_Text>().text = card.starA.ToString();
            btnGuardianStarA.onClick.RemoveAllListeners();
            btnGuardianStarA.onClick.AddListener(() => _chosenStar = card.starA);
        }
        if (isMonster && btnGuardianStarB != null)
        {
            btnGuardianStarB.GetComponentInChildren<TMP_Text>().text = card.starB.ToString();
            btnGuardianStarB.onClick.RemoveAllListeners();
            btnGuardianStarB.onClick.AddListener(() => _chosenStar = card.starB);
        }

        btnSummonAttack?.onClick.RemoveAllListeners();
        btnSummonAttack?.onClick.AddListener(() => {
            DuelManager.Instance.PlayerSummon(_selectedCard, _chosenStar, CardPosition.FaceUpAttack);
            Hide();
        });

        btnSummonDefense?.onClick.RemoveAllListeners();
        btnSummonDefense?.onClick.AddListener(() => {
            DuelManager.Instance.PlayerSummon(_selectedCard, _chosenStar, CardPosition.FaceUpDefense);
            Hide();
        });

        btnSetAttack?.onClick.RemoveAllListeners();
        btnSetAttack?.onClick.AddListener(() => {
            DuelManager.Instance.PlayerSetMonster(_selectedCard, _chosenStar, CardPosition.FaceDownAttack);
            Hide();
        });

        btnSetDefense?.onClick.RemoveAllListeners();
        btnSetDefense?.onClick.AddListener(() => {
            DuelManager.Instance.PlayerSetMonster(_selectedCard, _chosenStar, CardPosition.FaceDownDefense);
            Hide();
        });

        btnCastSpell?.onClick.RemoveAllListeners();
        btnCastSpell?.onClick.AddListener(() => {
            DuelManager.Instance.PlayerCastSpell(_selectedCard);
            Hide();
        });

        btnAddToFusion?.onClick.RemoveAllListeners();
        btnAddToFusion?.onClick.AddListener(() => {
            if (!_fusionMaterials.Contains(_selectedCard))
                _fusionMaterials.Add(_selectedCard);
            RefreshFusionLabel();
            Hide();
        });

        btnCancel?.onClick.RemoveAllListeners();
        btnCancel?.onClick.AddListener(Hide);
    }

    public void ExecuteFusion()
    {
        if (_fusionMaterials.Count < 2)
        {
            if (fusionMaterialsLabel != null)
                fusionMaterialsLabel.text = "Selecciona al menos 2 cartas para fusionar.";
            return;
        }
        DuelManager.Instance.PlayerFuse(new List<CardData>(_fusionMaterials));
        _fusionMaterials.Clear();
        RefreshFusionLabel();
    }

    /// <summary>Vacía la selección de materiales sin fusionar. Conectar a un botón "Limpiar" opcional.</summary>
    public void ClearFusionMaterials()
    {
        _fusionMaterials.Clear();
        RefreshFusionLabel();
    }

    private void RefreshFusionLabel()
    {
        if (fusionMaterialsLabel == null) return;

        if (_fusionMaterials.Count == 0)
        {
            fusionMaterialsLabel.text = "Materiales de fusión: (ninguno)";
            return;
        }

        var names = _fusionMaterials.ConvertAll(c => c.cardName);
        fusionMaterialsLabel.text = $"Materiales ({_fusionMaterials.Count}): {string.Join(" + ", names)}";
    }

    private void Hide() => gameObject.SetActive(false);
}