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

    [Header("Recetas por categoría (fusionGroup + fusionGroup = resultado)")]
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

        // 2. Receta por categoría
        var byCategory = TryFuseByCategory(current, next);
        if (byCategory != null)
            return new FusionStepResult(byCategory, FusionStepType.Category);

        // 3. Compatibilidad de equipo — solo si "next" es una carta de Equipo
        //    aplicable a "current". Cualquier Equip Card es aplicable a cualquier
        //    monstruo por ahora; si luego quieres restringir por monsterType o
        //    attribute, agrega esa validación aquí.
        if (next.IsEquip)
        {
            return new FusionStepResult(
                current, FusionStepType.Equip,
                equipAtkBonusApplied: next.equipAtkBonus,
                equipDefBonusApplied: next.equipDefBonus);
        }

        // 4. Absorción — no hay ninguna regla. Sobrevive la carta de mayor ATK.
        //    "current" es casi siempre el resultado acumulado (normalmente más fuerte),
        //    pero comparamos por ATK real para respetar el caso Kuriboh + Blue-Eyes.
        CardData survivor = (current.baseAtk >= next.baseAtk) ? current : next;
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
        // Solo Monstruo o Equipo participan en fusión. Magia/Ritual/Especial/Trampa
        // se ignoran silenciosamente si aparecen en la lista por error.
        foreach (var m in materials)
            if (m != null && (m.IsMonster || m.IsEquip)) usable.Add(m);

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