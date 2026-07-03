using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Todos los filtros de la pantalla de Library en un solo objeto.
/// La UI sólo necesita mutar esta instancia y volver a pedir el query.
/// Los campos null/vacíos significan "sin filtro en ese criterio".
/// </summary>
public class CardFilterCriteria
{
    public string searchText = "";                  // nombre completo, parcial, o ID numérico
    public PossessionFilter possession = PossessionFilter.All;

    public HashSet<MonsterType> types = new();
    public HashSet<CardAttribute> attributes = new();
    public HashSet<CardRarity> rarities = new();
    public HashSet<CardSourceType> sourceTypes = new();

    public int? levelMin, levelMax;
    public int? atkMin, atkMax;
    public int? defMin, defMax;

    public bool favoritesOnly = false;

    public bool Matches(CardData card, PlayerCardEntry entry, CardState state)
    {
        // Búsqueda: ID numérico exacto, o nombre completo/parcial sin importar mayúsculas
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            bool matchesId = int.TryParse(searchText, out int id) && card.cardId == id;
            bool matchesName = card.cardName.ToLowerInvariant()
                                    .Contains(searchText.ToLowerInvariant());
            if (!matchesId && !matchesName) return false;
        }

        // Filtro de posesión
        switch (possession)
        {
            case PossessionFilter.OwnedOnly:
                if (state != CardState.Owned) return false;
                break;
            case PossessionFilter.DiscoveredOnly:
                if (state == CardState.Locked) return false;
                break;
            case PossessionFilter.NotDiscoveredOnly:
                if (state != CardState.Locked) return false;
                break;
        }

        // Si está bloqueada, ningún filtro de contenido aplica (no podemos
        // comparar Tipo/ATK/etc. de algo que el jugador no ha visto). La regla de
        // negocio es: una carta bloqueada sólo puede aparecer si no hay filtros
        // de contenido activos, igual que en Forbidden Memories.
        if (state == CardState.Locked && HasContentFilters()) return false;

        if (types.Count > 0 && !types.Contains(card.monsterType)) return false;
        if (attributes.Count > 0 && !attributes.Contains(card.attribute)) return false;
        if (rarities.Count > 0 && !rarities.Contains(card.rarity)) return false;

        if (levelMin.HasValue && card.stars < levelMin.Value) return false;
        if (levelMax.HasValue && card.stars > levelMax.Value) return false;
        if (atkMin.HasValue && card.baseAtk < atkMin.Value) return false;
        if (atkMax.HasValue && card.baseAtk > atkMax.Value) return false;
        if (defMin.HasValue && card.baseDef < defMin.Value) return false;
        if (defMax.HasValue && card.baseDef > defMax.Value) return false;

        if (sourceTypes.Count > 0)
        {
            bool any = card.sources.Any(s => sourceTypes.Contains(s.sourceType));
            if (!any) return false;
        }

        if (favoritesOnly && (entry == null || !entry.favorite)) return false;

        return true;
    }

    private bool HasContentFilters() =>
        types.Count > 0 || attributes.Count > 0 || rarities.Count > 0 ||
        sourceTypes.Count > 0 || levelMin.HasValue || levelMax.HasValue ||
        atkMin.HasValue || atkMax.HasValue || defMin.HasValue || defMax.HasValue;
}
