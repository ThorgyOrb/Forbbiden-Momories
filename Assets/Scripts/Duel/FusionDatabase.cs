using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject que almacena todas las recetas de fusión del juego.
/// Crea una sola instancia en: Assets > Create > YGO > Fusion Database
/// Luego referénciala desde DuelController.
///
/// ── Cómo se resuelve cada PAR de cartas (en orden de prioridad) ──────────
///   1. Receta ESPECÍFICA    (materialA == X && materialB == Y, exacta)
///   2. Receta por CATEGORÍA (groupA == X.fusionGroup && groupB == Y.fusionGroup)
///   3. Compatibilidad de EQUIPO (la segunda carta es un Equip Card aplicable)
///   4. ABSORCIÓN  → no hay ninguna regla: sobrevive la carta de mayor ATK,
///      la otra simplemente se consume sin transformación.
///
/// La cadena completa se evalúa estrictamente de IZQUIERDA A DERECHA:
///   ((A + B) + C) + D ...
/// nunca se reagrupa ni se evalúa en otro orden.
/// </summary>
[CreateAssetMenu(fileName = "FusionDatabase", menuName = "YGO/Fusion Database")]
public class FusionDatabase : ScriptableObject
{
    [Header("Recetas específicas (carta + carta = resultado exacto)")]
    public List<FusionRecipe> recipes = new();

    [Header("Recetas por CATEGORÍA con reglas (atributo + ATK/DEF + …)")]
    [Tooltip("Se comprueban antes que las de fusionGroup. Mayor prioridad = se evalúa antes, " +
             "para que una regla estrecha gane a otra que la engloba.")]
    public List<FusionCategoryRecipe> categoryFusions = new();

    [Header("Recetas por fusionGroup (legado: etiqueta + etiqueta = resultado)")]
    public List<CategoryFusionRecipe> categoryRecipes = new();

    // ────────────────────────────────────────────────────────────────────
    //  API pública — resolución de UN paso (un par de cartas)
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Intenta resolver el par (a, b) consultando solo recetas ESPECÍFICAS.
    /// El orden de los materiales no importa.
    /// Devuelve null si no hay ninguna receta específica para ese par.
    /// </summary>
    public CardData TryFuseSpecific(CardData a, CardData b)
    {
        foreach (var recipe in recipes)
        {
            bool match = (recipe.materialA == a && recipe.materialB == b)
                      || (recipe.materialA == b && recipe.materialB == a);
            if (match) return recipe.result;
        }
        return null;
    }

    /// <summary>
    /// Intenta resolver el par (a, b) con las categorías POR REGLAS
    /// (<see cref="FusionCategory"/>: atributo, tipo, rangos de ATK/DEF, nivel…).
    /// El orden de los materiales no importa.
    ///
    /// Se evalúan de MAYOR a MENOR <see cref="FusionCategoryRecipe.priority"/>: dos reglas
    /// pueden solaparse (p. ej. "OSCURO ≤1500" y "OSCURO cualquiera") y la prioridad es lo
    /// que decide cuál gana. A igual prioridad manda el orden de la lista.
    /// </summary>
    public CardData TryFuseByRuleCategory(CardData a, CardData b)
    {
        FusionCategoryRecipe best = null;
        foreach (var recipe in categoryFusions)
        {
            if (recipe == null || recipe.result == null) continue;
            if (!recipe.Matches(a, b)) continue;
            if (best == null || recipe.priority > best.priority) best = recipe;
        }
        return best?.result;
    }

    /// <summary>
    /// Intenta resolver el par (a, b) por CATEGORÍA (fusionGroup).
    /// El orden no importa. Devuelve null si ninguna categoría coincide.
    /// </summary>
    public CardData TryFuseByCategory(CardData a, CardData b)
    {
        if (string.IsNullOrEmpty(a.fusionGroup) || string.IsNullOrEmpty(b.fusionGroup))
            return null;

        foreach (var recipe in categoryRecipes)
        {
            bool match = (recipe.groupA == a.fusionGroup && recipe.groupB == b.fusionGroup)
                      || (recipe.groupA == b.fusionGroup && recipe.groupB == a.fusionGroup);
            if (match) return recipe.result;
        }
        return null;
    }

    /// <summary>
    /// Resuelve un único paso de la cadena: "current" (carta o resultado acumulado
    /// hasta ahora) combinado con "next" (la siguiente carta seleccionada).
    /// Aplica la prioridad completa: específica → categoría → equipo → absorción.
    /// Nunca devuelve null — si nada coincide, aplica la regla de absorción.
    /// </summary>
    public FusionStepResult ResolveStep(CardData current, CardData next)
    {
        // 1. Receta específica
        var specific = TryFuseSpecific(current, next);
        if (specific != null)
            return new FusionStepResult(specific, FusionStepType.Specific);

        // 2a. Categoría POR REGLAS (atributo + rangos de ATK/DEF, etc.). Va antes que la
        //     de fusionGroup porque es la que puede expresar condiciones estrechas; dejar
        //     ganar a la etiqueta genérica haría inútiles los rangos.
        var byRule = TryFuseByRuleCategory(current, next);
        if (byRule != null)
            return new FusionStepResult(byRule, FusionStepType.Category);

        // 2b. Receta por fusionGroup (legado)
        var byCategory = TryFuseByCategory(current, next);
        if (byCategory != null)
            return new FusionStepResult(byCategory, FusionStepType.Category);

        // 3. Compatibilidad de EQUIPO en CUALQUIER orden: el MONSTRUO sobrevive y recibe
        //    el bonus, sin importar si el equipo va DESPUÉS (next) o ANTES (current) en la
        //    lista. (ver CardData.EquipAppliesTo: sin restricción aplica a cualquier
        //    monstruo; con restricción, solo a su monsterType). Un equipo incompatible no
        //    entra aquí y cae a absorción (se descarta sin efecto, como en FM).
        //    ANTES solo se contemplaba el equipo como "next", así que un equipo colocado
        //    ANTES del monstruo se descartaba por absorción en vez de equiparse.
        if (current.IsMonster && next.IsEquip && next.EquipAppliesTo(current))
            return new FusionStepResult(current, FusionStepType.Equip,
                equipAtkBonusApplied: next.equipAtkBonus, equipDefBonusApplied: next.equipDefBonus);
        if (next.IsMonster && current.IsEquip && current.EquipAppliesTo(next))
            return new FusionStepResult(next, FusionStepType.Equip,
                equipAtkBonusApplied: current.equipAtkBonus, equipDefBonusApplied: current.equipDefBonus);

        // 4. Absorción — no hay receta. PRIORIDAD para que el resultado sea siempre un
        //    MONSTRUO invocable: un monstruo NUNCA es descartado por un no-monstruo
        //    (equipo/magia/trampa incompatible), así una carta de equipo colocada al final
        //    de la lista no acaba "invocada" como monstruo (rompería el juego). Entre dos
        //    cartas del MISMO rango (dos monstruos, o dos no-monstruos) se resuelve por
        //    ORDEN: gana la ENTRANTE (next).
        CardData survivor;
        if (current.IsMonster && !next.IsMonster) survivor = current;
        else if (next.IsMonster && !current.IsMonster) survivor = next;
        else survivor = next;
        return new FusionStepResult(survivor, FusionStepType.Absorption);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Cadena completa: aplica ResolveStep en orden estricto izq→der
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Procesa la cadena completa de materiales en el orden EXACTO en que
    /// fueron seleccionados: ((m0+m1)+m2)+m3...
    /// Devuelve el resultado final, el ATK acumulado por equipos, y el detalle
    /// de cada paso (útil para loguear en la UI qué pasó con cada carta).
    ///
    /// Las cartas de tipo Spell no participan en fusión (se juegan aparte,
    /// ver DuelController) y se ignoran silenciosamente si
    /// aparecen en la lista por error.
    /// </summary>
    public FusionChainResult ResolveChain(List<CardData> materials)
    {
        var steps = new List<FusionStepResult>();
        var usable = new List<CardData>();
        // Participan en fusión: Monstruo, Equipo, Magia y Trampa (estas dos para recetas
        // específicas/categoría que las combinen entre sí o con monstruos → cartas nuevas).
        // Ritual/Especial se ignoran. El RESULTADO final debe ser un monstruo (lo valida
        // quien invoca la fusión); una carta sin receta cae a absorción y se descarta,
        // y un no-monstruo nunca sobrevive frente a un monstruo (ver ResolveStep).
        foreach (var m in materials)
            if (m != null && (m.IsMonster || m.IsEquip || m.IsSpell || m.IsTrap)) usable.Add(m);

        if (usable.Count == 0)
            return new FusionChainResult(null, 0, 0, steps);

        if (usable.Count == 1)
            return new FusionChainResult(usable[0], 0, 0, steps);

        CardData current = usable[0];
        int totalEquipAtkBonus = 0;
        int totalEquipDefBonus = 0;

        for (int i = 1; i < usable.Count; i++)
        {
            var step = ResolveStep(current, usable[i]);
            steps.Add(step);

            current = step.Result;
            if (step.Type == FusionStepType.Equip)
            {
                totalEquipAtkBonus += step.EquipAtkBonusApplied;
                totalEquipDefBonus += step.EquipDefBonusApplied;
            }
        }

        return new FusionChainResult(current, totalEquipAtkBonus, totalEquipDefBonus, steps);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Compatibilidad retro: usado por DuelAI.FindBestFusion
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Combinación directa de dos cartas (sin equipo ni absorción), usado por
    /// la IA para detectar si vale la pena intentar una fusión "real".
    /// Mantiene la firma original para no romper DuelAI.
    /// </summary>
    public CardData TryFuse(CardData a, CardData b)
    {
        var specific = TryFuseSpecific(a, b);
        if (specific != null) return specific;

        var byRule = TryFuseByRuleCategory(a, b);
        if (byRule != null) return byRule;

        return TryFuseByCategory(a, b);
    }

    /// <summary>
    /// Fusión en cadena para la IA: dado un conjunto de cartas en mano, encuentra
    /// la cadena de fusiones REALES (específica o por categoría, sin absorción)
    /// que produce el monstruo de mayor ATK. La IA no "rellena" con absorción
    /// porque no tiene sentido gastar cartas sin transformación real.
    /// Devuelve (resultado, materialesUsados) o (null, null) si no hay ninguna.
    /// </summary>
    public (CardData result, List<CardData> used) FindBestFusion(List<CardData> hand)
    {
        CardData bestResult = null;
        List<CardData> bestUsed = null;
        int bestAtk = -1;

        for (int i = 0; i < hand.Count; i++)
        {
            for (int j = i + 1; j < hand.Count; j++)
            {
                var chain = TryChainFuse(hand, i, j);
                if (chain.result != null && chain.result.baseAtk > bestAtk)
                {
                    bestAtk = chain.result.baseAtk;
                    bestResult = chain.result;
                    bestUsed = chain.used;
                }
            }
        }
        return (bestResult, bestUsed);
    }

    private (CardData result, List<CardData> used) TryChainFuse(
        List<CardData> hand, int idxA, int idxB)
    {
        var used = new List<CardData> { hand[idxA], hand[idxB] };
        var current = TryFuse(hand[idxA], hand[idxB]);
        if (current == null) return (null, null);

        bool progress = true;
        while (progress)
        {
            progress = false;
            foreach (var card in hand)
            {
                if (used.Contains(card)) continue;
                var next = TryFuse(current, card);
                if (next != null)
                {
                    current = next;
                    used.Add(card);
                    progress = true;
                    break;
                }
            }
        }
        return (current, used);
    }
}

// ────────────────────────────────────────────────────────────────────────
//  Recetas
// ────────────────────────────────────────────────────────────────────────

[System.Serializable]
public class FusionRecipe
{
    public CardData materialA;
    public CardData materialB;
    public CardData result;
}

/// <summary>
/// Receta entre dos <see cref="FusionCategory"/>: cualquier carta que pertenezca a
/// <see cref="categoryA"/> combinada con cualquiera de <see cref="categoryB"/> (en cualquier
/// orden) produce <see cref="result"/>.
///
/// Ejemplo de lo que el modelo viejo no podía expresar:
///   A = "OSCURO con ATK ≤ 1500", B = "LUZ con ATK ≤ 1500" → result = Guerrero del Alba.
/// La misma pareja de atributos con monstruos fuertes no dispararía esta receta.
/// </summary>
[System.Serializable]
public class FusionCategoryRecipe
{
    public FusionCategory categoryA;
    public FusionCategory categoryB;
    public CardData result;

    [Tooltip("Mayor gana cuando varias recetas encajan con el mismo par. Sube la prioridad " +
             "de las reglas MÁS ESTRECHAS para que no las tape una más general.")]
    public int priority;

    /// <summary>¿Encaja el par (a, b) en esta receta, en cualquier orden?</summary>
    public bool Matches(CardData a, CardData b)
    {
        if (categoryA == null || categoryB == null) return false;
        return (categoryA.Matches(a) && categoryB.Matches(b))
            || (categoryA.Matches(b) && categoryB.Matches(a));
    }
}

/// <summary>
/// Receta genérica por categoría: cualquier carta cuyo fusionGroup coincida
/// con groupA, combinada con cualquier carta cuyo fusionGroup coincida con
/// groupB (en cualquier orden), produce result.
/// Ejemplo: groupA="Dragon", groupB="Thunder" → result=DragonThunderCard.
/// </summary>
[System.Serializable]
public class CategoryFusionRecipe
{
    public string groupA;
    public string groupB;
    public CardData result;
}

// ────────────────────────────────────────────────────────────────────────
//  Resultado de un paso individual de la cadena
// ────────────────────────────────────────────────────────────────────────

public enum FusionStepType { Specific, Category, Equip, Absorption }

public struct FusionStepResult
{
    public CardData Result;
    public FusionStepType Type;
    public int EquipAtkBonusApplied;
    public int EquipDefBonusApplied;

    public FusionStepResult(CardData result, FusionStepType type, int equipAtkBonusApplied = 0, int equipDefBonusApplied = 0)
    {
        Result = result;
        Type = type;
        EquipAtkBonusApplied = equipAtkBonusApplied;
        EquipDefBonusApplied = equipDefBonusApplied;
    }
}

// ────────────────────────────────────────────────────────────────────────
//  Resultado de la cadena completa
// ────────────────────────────────────────────────────────────────────────

public class FusionChainResult
{
    public CardData FinalResult;
    public int TotalEquipAtkBonus;
    public int TotalEquipDefBonus;
    public List<FusionStepResult> Steps;

    public FusionChainResult(CardData finalResult, int totalEquipAtkBonus, int totalEquipDefBonus, List<FusionStepResult> steps)
    {
        FinalResult = finalResult;
        TotalEquipAtkBonus = totalEquipAtkBonus;
        TotalEquipDefBonus = totalEquipDefBonus;
        Steps = steps;
    }
}