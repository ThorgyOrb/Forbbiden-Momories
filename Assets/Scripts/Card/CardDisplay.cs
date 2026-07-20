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

    [Header("Layout V2 (opcional — prefab nuevo CardMonsterV2)")]
    // Estos campos SOLO existen en el prefab nuevo (propuesta de carta). En el
    // prefab clásico quedan nulos y se ignoran, así que su comportamiento no
    // cambia. Añadirlos a un MonoBehaviour es seguro: las instancias serializadas
    // que no los referencian los dejan en su default (null).
    [Tooltip("Fila donde se instancian las estrellas de nivel (nivel 8 ⇒ 8 estrellas).")]
    [SerializeField] private RectTransform levelStarsContainer;
    [SerializeField] private Sprite levelStarSprite;
    [SerializeField] private Color levelStarColor = new Color(1f, 0.84f, 0.2f);
    [Tooltip("Número de nivel en la esquina (redundante con las estrellas).")]
    [SerializeField] private TMP_Text levelNumberText;
    [Tooltip("Línea de subtipo: \"BESTIA / EFECTO\".")]
    [SerializeField] private TMP_Text subtypeText;
    [Tooltip("Caja de descripción/efecto del monstruo.")]
    [SerializeField] private TMP_Text effectText;
    [Tooltip("Superposición sobre el arte (insignia de tipo, estrellas y nivel). Vive DENTRO " +
             "del arte, que sigue visible en no-monstruos, así que hay que apagarla aparte.")]
    [SerializeField] private GameObject monsterOverlay;

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
    private CardV2Effects _v2Effects; // solo presente en el prefab nuevo; null en el clásico

    // ── Colores Guardian Star ────────────────────────────────────
    private readonly Color _starActiveColor = new Color(1f, 0.85f, 0.2f);
    private readonly Color _starInactiveColor = new Color(0.6f, 0.6f, 0.7f);

    // ────────────────────────────────────────────────────────────
    //  API pública
    // ────────────────────────────────────────────────────────────
    void Awake()
    {
        _holoEffect = GetComponent<CardHoloEffect>();
        _v2Effects = GetComponent<CardV2Effects>();
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

        // Efectos propios del prefab V2 (null en el clásico → no-op).
        if (_v2Effects != null)
            _v2Effects.Apply(data.rarity);
    }

    /// <summary>
    /// Atenúa (o intensifica) los efectos holo de esta carta. 1 = pleno (grid);
    /// se baja en cartas grandes (visor del modal, mesa de duelo). Llamar tras Setup.
    /// </summary>
    public void SetHoloIntensityScale(float scale)
    {
        if (_holoEffect == null) _holoEffect = GetComponent<CardHoloEffect>();
        if (_holoEffect != null) _holoEffect.SetIntensityScale(scale);
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

    // Nombre en español (singular, mayúsculas) por tipo de monstruo, para la
    // línea de subtipo del prefab nuevo ("BESTIA / EFECTO"). Los tipos no
    // listados usan el nombre del enum en mayúsculas — así crece solo.
    private static readonly System.Collections.Generic.Dictionary<MonsterType, string> TypeNamesEs = new()
    {
        { MonsterType.Dragon, "DRAGÓN" },
        { MonsterType.Spellcaster, "HECHICERO" },
        { MonsterType.Fiend, "DEMONIO" },
        { MonsterType.Beast, "BESTIA" },
        { MonsterType.Insect, "INSECTO" },
        { MonsterType.Plant, "PLANTA" },
        { MonsterType.Fish, "PEZ" },
        { MonsterType.Aqua, "ACUÁTICO" },
        { MonsterType.SeaSerpent, "SERPIENTE MARINA" },
        { MonsterType.Zombie, "ZOMBI" },
        { MonsterType.Dinosaur, "DINOSAURIO" },
        { MonsterType.WingedBeast, "BESTIA ALADA" },
        { MonsterType.Warrior, "GUERRERO" },
        { MonsterType.Machine, "MÁQUINA" },
        { MonsterType.Thunder, "TRUENO" },
        { MonsterType.Fairy, "HADA" },
        { MonsterType.Reptile, "REPTIL" },
        { MonsterType.Rock, "ROCA" },
        { MonsterType.Pyro, "PIRO" },
    };

    /// <summary>
    /// Rellena los elementos EXCLUSIVOS del prefab nuevo (estrellas de nivel,
    /// número de nivel, línea de subtipo y caja de efecto). Todos los campos son
    /// opcionales: si son nulos (prefab clásico) esto no hace nada.
    /// </summary>
    private void RefreshMonsterExtras()
    {
        if (_data == null || !_data.IsMonster) return;

        if (levelNumberText != null)
            levelNumberText.text = _data.stars.ToString();

        if (subtypeText != null)
        {
            string typeName = TypeNamesEs.TryGetValue(_data.monsterType, out var n)
                ? n : _data.monsterType.ToString().ToUpperInvariant();
            bool hasEffect = !string.IsNullOrWhiteSpace(_data.DisplayDescription);
            subtypeText.text = hasEffect ? $"{typeName} / EFECTO" : typeName;
        }

        if (effectText != null)
            effectText.text = _data.DisplayDescription;

        RebuildLevelStars(_data.stars);
    }

    /// <summary>
    /// Instancia una estrella por cada nivel (8 ⇒ 8), a TAMAÑO FIJO y CENTRADAS en el
    /// contenedor. Posición manual (sin LayoutGroup): con un HorizontalLayoutGroup las
    /// estrellas heredaban un tamaño por defecto enorme y se solapaban.
    /// </summary>
    private void RebuildLevelStars(int level)
    {
        if (levelStarsContainer == null) return;

        for (int i = levelStarsContainer.childCount - 1; i >= 0; i--)
            Destroy(levelStarsContainer.GetChild(i).gameObject);

        level = Mathf.Clamp(level, 0, 12);
        if (level == 0) return;

        const float size = 11f, gap = 1.5f;
        float step = size + gap;
        float mid = (level - 1) / 2f; // índice central, para centrar la fila

        for (int i = 0; i < level; i++)
        {
            var go = new GameObject("Star", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(levelStarsContainer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2((i - mid) * step, 0f);

            var img = go.GetComponent<Image>();
            img.sprite = levelStarSprite;
            img.color = levelStarColor;
            img.raycastTarget = false;
            img.preserveAspect = true;
        }
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
        bool isNonMonster = !isMonster; // Magia, Equipo, Ritual o Especial

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
        if (monsterOverlay != null) monsterOverlay.SetActive(showMonsterBlock);

        if (attributeIcon != null)
            attributeIcon.transform.parent.gameObject.SetActive(showMonsterBlock);

        if (starAButton != null) starAButton.gameObject.SetActive(showMonsterBlock);
        if (starBButton != null) starBButton.gameObject.SetActive(showMonsterBlock);

        // ── Bloque no-monstruo (Magia/Equipo/Ritual/Especial): panel descriptivo ──
        bool showSpellEquipBlock = !isFaceDown && isNonMonster;
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
                RefreshMonsterExtras(); // Layout V2 (no-op si los campos son nulos)
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
    /// Rellena el panel descriptivo para cualquier carta NO-monstruo (Magia,
    /// Equipo, Ritual, Especial): nombre, badge de categoría y una descripción
    /// (la propia de la carta, o una generada según su efecto/bonus).
    /// </summary>
    private void RefreshSpellEquipText()
    {
        if (spellEquipNameText != null)
            spellEquipNameText.text = _data.cardName;

        if (categoryBadge != null)
        {
            categoryBadge.text = _data.CategoryLabel;
            // Neo-Kemet: la categoría se lee por color, no por icono de tipo.
            categoryBadge.color = CardStyleKemet.BadgeColor(_data.cardCategory);
        }

        if (spellEquipDescriptionText == null) return;

        // 1) Descripción propia de la carta, si la tiene.
        if (!string.IsNullOrEmpty(_data.DisplayDescription))
        {
            spellEquipDescriptionText.text = _data.DisplayDescription;
            return;
        }

        // 2) Respaldo generado según la categoría.
        spellEquipDescriptionText.text = _data.cardCategory switch
        {
            CardCategory.Equip => DescribeEquip(),
            CardCategory.Spell => DescribeSpell(),
            CardCategory.Ritual => _data.ritualResult != null
                ? $"Ritual: invoca a {_data.ritualResult.cardName}."
                : "Carta de ritual.",
            CardCategory.Special => "Carta especial.",
            CardCategory.Trap => DescribeTrap(),
            _ => "Sin efecto definido."
        };
    }

    private string DescribeTrap()
    {
        return _data.trapEffect switch
        {
            TrapEffectType.DestroyAttackingMonster => "Destruye el monstruo atacante.",
            TrapEffectType.DestroyAllAttackingMonsters => "Destruye todos los monstruos enemigos en ataque.",
            TrapEffectType.NegateAttack => "Niega el ataque.",
            TrapEffectType.DestroySummonedMonster => "Destruye el monstruo invocado.",
            TrapEffectType.DamageOpponent => $"Inflige {_data.trapValue} de daño al rival.",
            TrapEffectType.DestroyOneSpell => "Destruye una Carta Mágica.",
            TrapEffectType.NegateSpell => "Niega la activación de una Carta Mágica.",
            TrapEffectType.NegateTrap => "Niega otra Trampa.",
            TrapEffectType.NegateSummon => "Cancela una invocación.",
            TrapEffectType.ReduceEnemyAtk => $"Reduce {_data.trapValue} ATK a los monstruos enemigos.",
            TrapEffectType.PreventDirectAttacks => "Impide los ataques directos.",
            TrapEffectType.LockPositionChanges => "Bloquea los cambios de posición.",
            _ => "Sin efecto definido."
        };
    }

    private string DescribeEquip()
    {
        string atkPart = _data.equipAtkBonus != 0 ? $"+{_data.equipAtkBonus} ATK" : null;
        string defPart = _data.equipDefBonus != 0 ? $"+{_data.equipDefBonus} DEF" : null;
        var parts = new System.Collections.Generic.List<string>();
        if (atkPart != null) parts.Add(atkPart);
        if (defPart != null) parts.Add(defPart);
        return parts.Count > 0
            ? $"Equipar: {string.Join(" / ", parts)}"
            : "Carta de equipo sin bonus configurado.";
    }

    private string DescribeSpell()
    {
        if (_data.IsFieldSpell)
            return $"Magia de terreno: cambia el escenario a {_data.fieldTerrain}.";

        return _data.spellEffect switch
        {
            SpellEffectType.HealLP => $"Recupera {_data.spellValue} LP.",
            SpellEffectType.DamageOpponentLP => $"Inflige {_data.spellValue} de daño directo al rival.",
            SpellEffectType.BuffAtkAllMonsters => $"Otorga +{_data.spellValue} ATK a todos tus monstruos en campo.",
            SpellEffectType.DestroyWeakestEnemyMonster => "Destruye al monstruo rival con menor ATK.",
            SpellEffectType.DestroyAllEnemyMonsters => "Destruye todos los monstruos del rival.",
            _ => "Sin efecto definido."
        };
    }
}