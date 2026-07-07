using UnityEngine;

/// <summary>
/// Paleta del estilo "Neo-Kemet" (Egipto futurista + cyberpunk): obsidiana,
/// oro de circuito y neón turquesa. Vive en CÓDIGO y no en el prefab a
/// propósito: los colores serializados del prefab Card pisarían cualquier
/// default nuevo del componente, y este estilo debe aplicarse igual en toda
/// carta (duelo, mano, biblioteca) sin depender de qué versión del prefab
/// tenga guardada cada escena.
/// </summary>
public static class CardStyleKemet
{
    // ── Marco por rareza: la rareza es el "material" del borde ──────────
    public static readonly Color FrameCommon    = new Color(0.42f, 0.43f, 0.47f); // pizarra
    public static readonly Color FrameRare      = new Color(0.16f, 0.85f, 0.76f); // turquesa neón
    public static readonly Color FrameEpic      = new Color(0.63f, 0.40f, 0.95f); // violeta neón
    public static readonly Color FrameLegendary = new Color(0.93f, 0.75f, 0.33f); // oro

    // ── Aurora (par A/B que el shader mezcla) por rareza ─────────────────
    public static readonly Color AuroraARare      = new Color(0.10f, 0.72f, 0.65f); // turquesa
    public static readonly Color AuroraBRare      = new Color(0.12f, 0.37f, 0.85f); // azul profundo
    public static readonly Color AuroraAEpic      = new Color(0.54f, 0.24f, 0.96f); // violeta
    public static readonly Color AuroraBEpic      = new Color(0.96f, 0.26f, 0.71f); // magenta
    public static readonly Color AuroraALegendary = new Color(1.00f, 0.76f, 0.32f); // oro
    public static readonly Color AuroraBLegendary = new Color(0.16f, 0.88f, 0.78f); // turquesa

    // ── Dúo oro/turquesa del foil, el relieve y el borde de circuito ─────
    public static readonly Color FoilGold  = new Color(1.00f, 0.80f, 0.42f);
    public static readonly Color FoilCyan  = new Color(0.22f, 0.95f, 0.86f);
    public static readonly Color GlareGold = new Color(1.00f, 0.86f, 0.52f);
    public static readonly Color Sparkle   = new Color(1.00f, 0.92f, 0.70f);

    // ── Insignias de categoría (cartas no-monstruo) ──────────────────────
    public static readonly Color BadgeSpell   = new Color(0.16f, 0.85f, 0.66f); // esmeralda
    public static readonly Color BadgeTrap    = new Color(1.00f, 0.31f, 0.64f); // fucsia
    public static readonly Color BadgeEquip   = new Color(0.93f, 0.75f, 0.33f); // oro
    public static readonly Color BadgeRitual  = new Color(0.63f, 0.40f, 0.95f); // violeta
    public static readonly Color BadgeSpecial = new Color(0.35f, 0.65f, 1.00f); // azul eléctrico

    /// <summary>Color de insignia según la categoría de la carta.</summary>
    public static Color BadgeColor(CardCategory category) => category switch
    {
        CardCategory.Spell   => BadgeSpell,
        CardCategory.Trap    => BadgeTrap,
        CardCategory.Equip   => BadgeEquip,
        CardCategory.Ritual  => BadgeRitual,
        CardCategory.Special => BadgeSpecial,
        _                    => FrameCommon
    };

    // ── Intensidades del shader (valores del estilo, fijados por código
    //    para que el material asset viejo no arrastre su tuning anterior) ──
    public const float AuroraTintAmount = 0.35f;
    public const float AuroraIntensity  = 4.5f;
    public const float GlareIntensity   = 2.6f;
    public const float FoilIntensity    = 1.4f;
    public const float FoilStripeScale  = 14f;
    public const float ReliefIntensity  = 1.6f;
    public const float ReliefScale      = 15f;
}
