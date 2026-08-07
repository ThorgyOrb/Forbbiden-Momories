using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un conjunto CON NOMBRE de cartas, definido por reglas en vez de por una etiqueta suelta.
/// Es el material de las recetas de fusión por categoría (ver <see cref="FusionDatabase"/>).
///
/// Nació porque el modelo anterior —una cadena <c>fusionGroup</c> por carta— solo permitía
/// "todas las de este arquetipo". Con esto se puede decir "monstruos OSCUROS de 1500 ATK o
/// menos", que es lo que hace falta para que una fusión de dos atributos no se dispare con
/// cualquier bicho de ese atributo.
///
/// Todas las condiciones activas se combinan con Y (AND). Las desactivadas se ignoran, así
/// que una categoría sin nada marcado acepta cualquier monstruo.
///
/// <see cref="alwaysInclude"/> y <see cref="neverInclude"/> se aplican por encima de las
/// reglas: sirven para las excepciones sueltas sin tener que retorcer los rangos.
///
/// Se crean con Create ▸ YGO ▸ Fusion Category (recomendado: Assets/Data/FusionCategories).
/// No hace falta que estén en Resources: el FusionDatabase las referencia por GUID.
/// </summary>
[CreateAssetMenu(fileName = "FusionCategory", menuName = "YGO/Fusion Category")]
public class FusionCategory : ScriptableObject
{
    [Tooltip("Nombre legible para la UI del editor. Si se deja vacío se usa el del asset.")]
    public string displayName = "";

    [Header("Solo esta lista")]
    [Tooltip("Si está activo, SOLO valen las cartas de 'alwaysInclude' y se ignora todo lo " +
             "demás. Es la forma de decir «este monstruo concreto» o «solo estos cinco».")]
    public bool onlyExplicitList;

    [Header("Solo monstruos")]
    [Tooltip("Descarta magias, trampas, equipos y rituales. Normalmente sí.")]
    public bool monstersOnly = true;

    [Header("Categoría de carta")]
    [Tooltip("Para apuntar a Magias, Trampas, Equipos o cartas de Ritual en vez de monstruos.")]
    public bool useCardCategory;
    public CardCategory cardCategory = CardCategory.Monster;

    [Header("Monstruos de Ritual")]
    [Tooltip("Solo monstruos que alguna carta de Ritual invoque como resultado.")]
    public bool onlyRitualMonsters;

    [Header("Atributo")]
    public bool useAttribute;
    public CardAttribute attribute = CardAttribute.Dark;

    [Header("Tipo de monstruo")]
    public bool useMonsterType;
    public MonsterType monsterType = MonsterType.Dragon;

    [Header("Rango de ATK")]
    public bool useAtkRange;
    public int minAtk;
    public int maxAtk = 9999;

    [Header("Rango de DEF")]
    public bool useDefRange;
    public int minDef;
    public int maxDef = 9999;

    [Header("Rango de nivel (estrellas)")]
    public bool useStarRange;
    public int minStars;
    public int maxStars = 12;

    [Header("Arquetipo (fusionGroup de la carta)")]
    [Tooltip("Vacío = no se comprueba. Comparación exacta.")]
    public string fusionGroup = "";

    [Header("Excepciones (mandan sobre las reglas)")]
    [Tooltip("Estas cartas pertenecen a la categoría aunque no cumplan las reglas.")]
    public List<CardData> alwaysInclude = new();

    [Tooltip("Estas cartas NUNCA pertenecen, aunque cumplan las reglas.")]
    public List<CardData> neverInclude = new();

    public string Label => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    /// <summary>¿Pertenece <paramref name="card"/> a esta categoría?</summary>
    public bool Matches(CardData card)
    {
        if (card == null) return false;

        // Las excepciones se evalúan primero: son la vía de escape de las reglas.
        if (neverInclude != null && neverInclude.Contains(card)) return false;
        if (alwaysInclude != null && alwaysInclude.Contains(card)) return true;

        // Lista cerrada: si llegó aquí es que no estaba en alwaysInclude → fuera.
        if (onlyExplicitList) return false;

        if (monstersOnly && !card.IsMonster) return false;
        if (useCardCategory && card.cardCategory != cardCategory) return false;
        if (onlyRitualMonsters && !RitualMonsterIndex.IsRitualMonster(card)) return false;

        if (useAttribute && card.attribute != attribute) return false;
        if (useMonsterType && card.monsterType != monsterType) return false;

        if (useAtkRange && (card.baseAtk < minAtk || card.baseAtk > maxAtk)) return false;
        if (useDefRange && (card.baseDef < minDef || card.baseDef > maxDef)) return false;
        if (useStarRange && (card.stars < minStars || card.stars > maxStars)) return false;

        if (!string.IsNullOrWhiteSpace(fusionGroup) && card.fusionGroup != fusionGroup) return false;

        return true;
    }

    /// <summary>Invalida el índice de monstruos de Ritual (tras editar rituales).</summary>
    public static void InvalidateRitualIndex() => RitualMonsterIndex.Invalidate();

    /// <summary>Resumen de las reglas en una línea, para la UI del editor.</summary>
    public string DescribeRules()
    {
        var parts = new List<string>();
        if (onlyExplicitList)
            return $"solo la lista fija ({(alwaysInclude != null ? alwaysInclude.Count : 0)} cartas)";
        if (monstersOnly) parts.Add("monstruos");
        if (useCardCategory) parts.Add(cardCategory.ToString());
        if (onlyRitualMonsters) parts.Add("de Ritual");
        if (useAttribute) parts.Add(attribute.ToString().ToUpperInvariant());
        if (useMonsterType) parts.Add(monsterType.ToString());
        if (useAtkRange) parts.Add($"ATK {minAtk}–{maxAtk}");
        if (useDefRange) parts.Add($"DEF {minDef}–{maxDef}");
        if (useStarRange) parts.Add($"★{minStars}–{maxStars}");
        if (!string.IsNullOrWhiteSpace(fusionGroup)) parts.Add($"grupo '{fusionGroup}'");
        if (alwaysInclude != null && alwaysInclude.Count > 0) parts.Add($"+{alwaysInclude.Count} fijas");
        if (neverInclude != null && neverInclude.Count > 0) parts.Add($"−{neverInclude.Count} excluidas");
        return parts.Count > 0 ? string.Join(" · ", parts) : "sin condiciones (cualquier carta)";
    }
}

/// <summary>
/// Qué monstruos son "de Ritual". No hay un campo que lo marque, y no hace falta: un
/// monstruo es de ritual si ALGUNA carta de Ritual lo tiene como
/// <see cref="CardData.ritualResult"/>. Se deduce del catálogo y se cachea.
///
/// Deducirlo evita tener que añadir un campo a las 14.651 cartas y reimportarlas, y además
/// no se puede desincronizar: si cambias el resultado de un ritual en el editor, el índice
/// cambia con él (llamando a <see cref="Invalidate"/>).
/// </summary>
public static class RitualMonsterIndex
{
    private static HashSet<int> _ids;

    public static bool IsRitualMonster(CardData card)
    {
        if (card == null) return false;
        EnsureBuilt();
        return _ids.Contains(card.cardId);
    }

    public static void Invalidate() => _ids = null;

    private static void EnsureBuilt()
    {
        if (_ids != null) return;
        _ids = new HashSet<int>();

        foreach (var c in LibraryCatalog.AllCards)
        {
            if (c == null || !c.IsRitual || c.ritualResult == null) continue;
            _ids.Add(c.ritualResult.cardId);
        }
    }
}
