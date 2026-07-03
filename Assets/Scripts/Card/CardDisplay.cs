using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CardHoloEffect))]
public class CardDisplay : MonoBehaviour
{
    [Header("Artwork")]
    [SerializeField] private Image artImage;
    [SerializeField] private Image cardBack;
    [SerializeField] private TMP_Text positionBadge;

    [Header("Frame")]
    [SerializeField] private Image frameBorder;
    [SerializeField] private Image rarityGem;

    [Header("Stats Panel (solo Monster)")]
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text atkText;
    [SerializeField] private TMP_Text defText;

    [Header("Guardian Stars (solo Monster)")]
    [SerializeField] private Button starAButton;
    [SerializeField] private Button starBButton;
    [SerializeField] private TMP_Text starALabel;
    [SerializeField] private TMP_Text starBLabel;

    [Header("Iconos (solo Monster)")]
    [SerializeField] private Image attributeIcon;
    [SerializeField] private Image typeIcon;
    [SerializeField] private CardIconConfig iconConfig;

    [Header("Spell / Equip")]
    // Panel y texto que se muestran SOLO cuando la carta es Spell o Equip,
    // en el mismo espacio donde Monster muestra StatsPanel/Guardian Stars/Iconos.
    [SerializeField] private GameObject spellEquipPanel;
    [SerializeField] private TMP_Text spellEquipNameText;
    [SerializeField] private TMP_Text spellEquipDescriptionText; // descripción del efecto o del bonus de equipo
    [SerializeField] private TMP_Text categoryBadge;             // opcional: "MAGIA" / "EQUIPO" / "MONSTRUO"

    // ── Estado interno ──────────────────────────────────────────
    private CardData _data;
    private GuardianStar _activeGuardian;
    private CardPosition _position = CardPosition.FaceUpAttack;
    private CardHoloEffect _holoEffect;

    // ── Colores Guardian Star ────────────────────────────────────
    private readonly Color _starActiveColor = new Color(1f, 0.85f, 0.2f);
    private readonly Color _starInactiveColor = new Color(0.6f, 0.6f, 0.7f);

    // ────────────────────────────────────────────────────────────
    //  API pública
    // ────────────────────────────────────────────────────────────
    void Awake()
    {
        _holoEffect = GetComponent<CardHoloEffect>();
    }

    /// <summary>
    /// Inicializa la carta con sus datos y refresca todos los visuales.
    /// Llama esto después de Instantiate.
    /// </summary>
    public void Setup(CardData data)
    {
        if (data == null) return;
        _data = data;
        _activeGuardian = data.starA;
        _position = CardPosition.FaceUpAttack;

        RefreshVisuals();
        RefreshIcons();

        // Importante: se le pasa la misma Image (artImage) que acaba de
        // recibir el sprite correcto en RefreshVisuals(). Así CardHoloEffect
        // nunca queda desincronizado con un Image distinto.
        if (artImage != null)
            _holoEffect.Apply(data.rarity, artImage);
    }

    private void RefreshIcons()
    {
        if (_data == null || !_data.IsMonster) return; // Spell/Equip no tienen attribute/monsterType relevantes
        if (iconConfig == null) return;

        if (attributeIcon != null)
            attributeIcon.sprite = iconConfig.GetAttributeIcon(_data.attribute);

        if (typeIcon != null)
            typeIcon.sprite = iconConfig.GetTypeIcon(_data.monsterType);
    }

    /// <summary>
    /// Cambia la posición de la carta en el campo.
    /// </summary>
    public void SetPosition(CardPosition pos)
    {
        _position = pos;
        RefreshVisuals();
    }

    /// <summary>
    /// Devuelve el ATK base de la carta. 0 si no es Monster.
    /// </summary>
    public int GetCurrentAtk() => (_data != null && _data.IsMonster) ? _data.baseAtk : 0;

    /// <summary>
    /// Devuelve el DEF base de la carta. 0 si no es Monster.
    /// </summary>
    public int GetCurrentDef() => (_data != null && _data.IsMonster) ? _data.baseDef : 0;

    // Propiedades de solo lectura
    public CardData Data => _data;
    public GuardianStar ActiveGuardian => _activeGuardian;
    public CardPosition Position => _position;

    // ────────────────────────────────────────────────────────────
    //  Botones Guardian Star — conectar en el Inspector
    // ────────────────────────────────────────────────────────────

    public void OnSelectStarA()
    {
        if (_data == null || !_data.IsMonster) return;
        _activeGuardian = _data.starA;
        RefreshGuardianStars();
    }

    public void OnSelectStarB()
    {
        if (_data == null || !_data.IsMonster) return;
        _activeGuardian = _data.starB;
        RefreshGuardianStars();
    }

    // ────────────────────────────────────────────────────────────
    //  Internos
    // ────────────────────────────────────────────────────────────

    private void RefreshVisuals()
    {
        if (_data == null) return;

        bool isFaceDown = _position == CardPosition.FaceDownAttack
                       || _position == CardPosition.FaceDownDefense;
        bool isMonster = _data.IsMonster;
        bool isSpellOrEquip = _data.IsSpell || _data.IsEquip;

        // Reverso
        if (cardBack != null)
            cardBack.gameObject.SetActive(isFaceDown);

        // Artwork — activa Y asigna sprite
        if (artImage != null)
        {
            artImage.gameObject.SetActive(!isFaceDown);
            artImage.sprite = _data.artwork;
            artImage.preserveAspect = false;

            // Ya NO se toca artImage.material aquí. CardHoloEffect.Apply()
            // (llamado desde Setup) es el único responsable de crear/actualizar
            // el material de holo y su _MainTex. Tocarlo aquí también causaba
            // que se pisara con el sprite/material incorrecto.
        }

        // Frame
        if (frameBorder != null)
            frameBorder.gameObject.SetActive(!isFaceDown);

        // ── Bloque Monster: StatsPanel + Guardian Stars + Iconos ─────────
        Transform sp = transform.Find("StatsPanel");
        bool showMonsterBlock = !isFaceDown && isMonster;

        if (sp != null) sp.gameObject.SetActive(showMonsterBlock);
        if (statsPanel != null) statsPanel.SetActive(showMonsterBlock);

        if (attributeIcon != null)
            attributeIcon.transform.parent.gameObject.SetActive(showMonsterBlock);

        if (starAButton != null) starAButton.gameObject.SetActive(showMonsterBlock);
        if (starBButton != null) starBButton.gameObject.SetActive(showMonsterBlock);

        // ── Bloque Spell/Equip: panel descriptivo ────────────────────────
        bool showSpellEquipBlock = !isFaceDown && isSpellOrEquip;
        if (spellEquipPanel != null) spellEquipPanel.SetActive(showSpellEquipBlock);

        // Position Badge — solo tiene sentido para monstruos en campo (ATK/DEF visual)
        if (positionBadge != null)
        {
            positionBadge.gameObject.SetActive(showMonsterBlock);
            if (showMonsterBlock)
            {
                positionBadge.text = _position switch
                {
                    CardPosition.FaceUpAttack => "ATK",
                    CardPosition.FaceUpDefense => "DEF",
                    _ => ""
                };
            }
        }

        if (!isFaceDown)
        {
            if (isMonster)
            {
                if (nameText != null) nameText.text = _data.cardName;
                if (atkText != null) atkText.text = _data.baseAtk.ToString();
                if (defText != null) defText.text = _data.baseDef.ToString();
                RefreshGuardianStars();
                RefreshIcons();
            }
            else
            {
                RefreshSpellEquipText();
            }
        }
    }

    private void RefreshGuardianStars()
    {
        if (_data == null || !_data.IsMonster) return;

        // Labels
        if (starALabel != null) starALabel.text = _data.starA.ToString();
        if (starBLabel != null) starBLabel.text = _data.starB.ToString();

        // Resalta la star activa
        if (starALabel != null)
            starALabel.color = (_activeGuardian == _data.starA)
                ? _starActiveColor
                : _starInactiveColor;

        if (starBLabel != null)
            starBLabel.color = (_activeGuardian == _data.starB)
                ? _starActiveColor
                : _starInactiveColor;
    }

    /// <summary>
    /// Rellena el panel descriptivo para Spell/Equip: nombre, categoría y
    /// una descripción generada según el tipo de efecto/bonus.
    /// </summary>
    private void RefreshSpellEquipText()
    {
        if (spellEquipNameText != null)
            spellEquipNameText.text = _data.cardName;

        if (categoryBadge != null)
            categoryBadge.text = _data.IsEquip ? "EQUIPO" : "MAGIA";

        if (spellEquipDescriptionText == null) return;

        if (_data.IsEquip)
        {
            string atkPart = _data.equipAtkBonus != 0 ? $"+{_data.equipAtkBonus} ATK" : null;
            string defPart = _data.equipDefBonus != 0 ? $"+{_data.equipDefBonus} DEF" : null;
            var parts = new System.Collections.Generic.List<string>();
            if (atkPart != null) parts.Add(atkPart);
            if (defPart != null) parts.Add(defPart);
            spellEquipDescriptionText.text = parts.Count > 0
                ? $"Equipar: {string.Join(" / ", parts)}"
                : "Carta de equipo sin bonus configurado.";
        }
        else // Spell
        {
            if (!string.IsNullOrEmpty(_data.spellDescription))
            {
                spellEquipDescriptionText.text = _data.spellDescription;
            }
            else
            {
                // Descripción genérica de respaldo basada en el tipo de efecto,
                // por si la carta no tiene texto propio escrito todavía.
                spellEquipDescriptionText.text = _data.spellEffect switch
                {
                    SpellEffectType.HealLP => $"Recupera {_data.spellValue} LP.",
                    SpellEffectType.DamageOpponentLP => $"Inflige {_data.spellValue} de daño directo al rival.",
                    SpellEffectType.BuffAtkAllMonsters => $"Otorga +{_data.spellValue} ATK a todos tus monstruos en campo.",
                    SpellEffectType.DestroyWeakestEnemyMonster => "Destruye al monstruo rival con menor ATK.",
                    _ => "Sin efecto definido."
                };
            }
        }
    }
}