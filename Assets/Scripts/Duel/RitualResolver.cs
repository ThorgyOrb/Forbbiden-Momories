using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Decide si una selección de cartas constituye una Invocación de Ritual válida.
///
/// Regla: la selección debe ser EXACTAMENTE una carta de categoría Ritual más, ni una más
/// ni una menos, las cartas de su <see cref="CardData.ritualMaterials"/>. Se compara por
/// <c>cardId</c> y como multiconjunto, así que un ritual que pide dos copias del mismo
/// material exige de verdad dos copias.
///
/// Lo de "exactamente" es a propósito: si sobraran cartas habría que decidir cuáles se
/// consumen, y consumir de más en un juego de cartas es la clase de sorpresa que arruina
/// una partida. Mejor rechazar y decir qué falta o qué sobra.
///
/// Separado de DuelController para poder razonarlo (y probarlo) sin el duelo entero
/// montado; el controlador solo orquesta la animación.
/// </summary>
public static class RitualResolver
{
    /// <summary>Por qué no se pudo hacer el ritual (o <see cref="Ok"/> si sí).</summary>
    public enum Status
    {
        NotARitual,        // la selección no contiene exactamente una carta Ritual
        NotConfigured,     // la carta Ritual no tiene resultado asignado
        ResultNotMonster,  // el resultado no es un monstruo: no se puede poner en el campo
        MissingMaterials,  // faltan materiales
        ExtraCards,        // sobran cartas que el ritual no pide
        Ok
    }

    public struct Attempt
    {
        public Status status;
        public CardData ritualCard;
        public CardData result;

        /// <summary>Materiales que faltan (con repetición si pide varias copias).</summary>
        public List<CardData> missing;

        /// <summary>Cartas seleccionadas que el ritual no pide.</summary>
        public List<CardData> extra;

        public bool IsRitualAttempt => status != Status.NotARitual;
        public bool Ok => status == Status.Ok;

        /// <summary>Mensaje listo para el log del duelo.</summary>
        public string Describe() => status switch
        {
            Status.Ok => $"¡Ritual! {ritualCard.cardName} invoca a {result.cardName}.",
            Status.NotConfigured =>
                $"{ritualCard.cardName} no tiene configurado su ritual (sin monstruo resultante).",
            Status.ResultNotMonster =>
                $"El ritual de {ritualCard.cardName} no produce un monstruo invocable.",
            Status.MissingMaterials =>
                $"Faltan materiales para {ritualCard.cardName}: " +
                string.Join(", ", missing.Select(m => m != null ? m.cardName : "¿?")),
            Status.ExtraCards =>
                $"Sobran cartas para {ritualCard.cardName}: " +
                string.Join(", ", extra.Select(m => m != null ? m.cardName : "¿?")),
            _ => ""
        };
    }

    /// <summary>Evalúa la selección (en cualquier orden).</summary>
    public static Attempt Evaluate(IReadOnlyList<CardData> selection)
    {
        var attempt = new Attempt
        {
            status = Status.NotARitual,
            missing = new List<CardData>(),
            extra = new List<CardData>()
        };

        if (selection == null) return attempt;

        var rituals = selection.Where(c => c != null && c.IsRitual).ToList();
        if (rituals.Count != 1) return attempt;

        var ritual = rituals[0];
        attempt.ritualCard = ritual;
        attempt.result = ritual.ritualResult;

        if (ritual.ritualResult == null) { attempt.status = Status.NotConfigured; return attempt; }
        if (!ritual.ritualResult.IsMonster) { attempt.status = Status.ResultNotMonster; return attempt; }

        // Multiconjunto de lo aportado (todo menos la propia carta de ritual).
        var offered = new List<CardData>();
        bool skippedRitual = false;
        foreach (var c in selection)
        {
            if (c == null) continue;
            if (!skippedRitual && c == ritual) { skippedRitual = true; continue; }
            offered.Add(c);
        }

        // Empareja cada material requerido con una carta aportada, por cardId.
        foreach (var required in ritual.ritualMaterials)
        {
            if (required == null) continue;
            int idx = offered.FindIndex(c => c.cardId == required.cardId);
            if (idx >= 0) offered.RemoveAt(idx);
            else attempt.missing.Add(required);
        }

        attempt.extra = offered;

        if (attempt.missing.Count > 0) attempt.status = Status.MissingMaterials;
        else if (attempt.extra.Count > 0) attempt.status = Status.ExtraCards;
        else attempt.status = Status.Ok;

        return attempt;
    }
}
