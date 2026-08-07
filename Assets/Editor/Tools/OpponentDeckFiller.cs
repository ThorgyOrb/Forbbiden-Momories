using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de contenido: repara los OpponentData incompletos para que los
/// duelos funcionen de verdad. Hoy TODOS los mazos de oponentes están vacíos,
/// así que cualquier duelo degenera (el rival nunca juega nada). Este menú:
///
///   • Limpia entradas null del deck.
///   • Rellena cada mazo hasta 40 cartas con cartas del catálogo
///     (Resources/Cards/Data), al azar pero reproducible por oponente.
///   • Pone nombre a los oponentes sin nombre ("Oponente N").
///   • Si TODOS tienen appearanceOrder = 0, asigna el orden de campaña = id.
///
/// Menú:  YGO > Setup > Rellenar mazos de oponentes (40 cartas)
/// </summary>
public static class OpponentDeckFiller
{
    [MenuItem("YGO/Setup/Rellenar mazos de oponentes (40 cartas)")]
    public static void FillOpponentDecks()
    {
        var opponents = Resources.LoadAll<OpponentData>("Opponents/Data");
        var cards = Resources.LoadAll<CardData>("Cards/Data");

        if (opponents.Length == 0)
        {
            EditorUtility.DisplayDialog("Sin oponentes",
                "No hay OpponentData en Resources/Opponents/Data.", "Ok");
            return;
        }
        if (cards.Length == 0)
        {
            EditorUtility.DisplayDialog("Sin cartas",
                "No hay CardData en Resources/Cards/Data — no puedo armar mazos.", "Ok");
            return;
        }

        var report = new StringBuilder();
        bool allOrdersZero = true;
        foreach (var o in opponents)
            if (o.appearanceOrder != 0) { allOrdersZero = false; break; }

        foreach (var opp in opponents)
        {
            bool changed = false;

            // 1. Nombre por defecto si está vacío.
            if (string.IsNullOrWhiteSpace(opp.opponentName))
            {
                opp.opponentName = $"Oponente {opp.opponentId}";
                changed = true;
            }

            // 2. Orden de campaña explícito si nadie lo ha configurado aún.
            if (allOrdersZero && opp.appearanceOrder == 0)
            {
                opp.appearanceOrder = opp.opponentId;
                changed = true;
            }

            // 3. Deck: limpiar nulls y rellenar hasta 40.
            opp.deck ??= new List<CardData>();
            int removed = opp.deck.RemoveAll(c => c == null);
            if (removed > 0) changed = true;

            int before = opp.deck.Count;
            if (before < PlayerDeck.RequiredSize)
            {
                // Random reproducible por oponente: mismo asset → mismo mazo.
                var rng = new System.Random(opp.opponentId * 7919 + 17);
                while (opp.deck.Count < PlayerDeck.RequiredSize)
                    opp.deck.Add(cards[rng.Next(cards.Length)]);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(opp);
                report.AppendLine($"• {opp.opponentName} (id {opp.opponentId}): " +
                                  $"deck {before} → {opp.deck.Count} cartas" +
                                  (removed > 0 ? $" ({removed} nulls fuera)" : ""));
            }
            else
            {
                report.AppendLine($"• {opp.opponentName} (id {opp.opponentId}): ya estaba completo.");
            }
        }

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Mazos de oponentes",
            report.ToString() +
            "\nPuedes ajustar cada mazo a mano en su asset (Resources/Opponents/Data) " +
            "para darle una estrategia concreta a cada rival.",
            "Genial");
        Debug.Log($"OpponentDeckFiller:\n{report}");
    }
}
