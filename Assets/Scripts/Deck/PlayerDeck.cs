using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fachada del MAZO ACTIVO del jugador. Históricamente había un único mazo aquí;
/// ahora el jugador puede tener varios (<see cref="DeckLibrary"/>) y esta clase
/// apunta siempre al activo. Se mantiene para que el resto del código (duelo,
/// herramientas de editor) siga funcionando sin cambios.
///
/// Reglas del juego:
///   • el mazo debe tener EXACTAMENTE 40 cartas para poder duelar/guardar;
///   • máximo 3 copias de una misma carta;
///   • solo cartas que el jugador posea (lo valida el Constructor).
/// </summary>
public static class PlayerDeck
{
    /// <summary>Tamaño exacto exigido para duelar (mínimo = máximo = 40).</summary>
    public const int RequiredSize = 40;
    public const int MinSize = 40;
    public const int MaxSize = 40;

    /// <summary>Máximo de copias de una misma carta dentro de un mazo.</summary>
    public const int MaxCopiesPerCard = 3;

    /// <summary>Ids de las cartas del mazo activo (con repeticiones).</summary>
    public static List<int> GetCardIds() => new List<int>(DeckLibrary.Active.cardIds);

    public static int Count => DeckLibrary.Active.Count;

    /// <summary>¿El mazo activo tiene exactamente 40 cartas?</summary>
    public static bool IsComplete => Count == RequiredSize;

    /// <summary>Guarda las cartas del mazo activo (lista de ids con repeticiones).</summary>
    public static void Save(List<int> cardIds) => DeckLibrary.SaveActive(cardIds);

    /// <summary>Resuelve el mazo activo a CardData (para el duelo).</summary>
    public static List<CardData> ResolveCards()
    {
        var list = new List<CardData>();
        foreach (var id in DeckLibrary.Active.cardIds)
        {
            var c = LibraryCatalog.GetCard(id);
            if (c != null) list.Add(c);
        }
        return list;
    }
}
