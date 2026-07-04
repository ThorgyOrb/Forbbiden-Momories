using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Lógica del modo Duelo Libre: qué oponentes están disponibles (los DERROTADOS
/// alguna vez), cómo ordenarlos/agruparlos, cuántas de sus cartas has descubierto,
/// y cómo lanzar el duelo. La pantalla visual solo consume estos métodos.
///
/// Regla de desbloqueo (estilo Forbidden Memories): un oponente aparece aquí tras
/// ser derrotado por primera vez (ver PlayerCollection.RecordDuelResult).
/// </summary>
public static class FreeDuelService
{
    public enum SortMode { Appearance, Difficulty, Region }

    /// <summary>Oponentes desbloqueados (derrotados) disponibles en Duelo Libre.</summary>
    public static List<OpponentData> GetUnlockedOpponents(SortMode sort = SortMode.Appearance)
    {
        var pc = PlayerCollection.Instance;

        IEnumerable<OpponentData> unlocked = LibraryCatalog.AllOpponents
            .Where(o => o != null && pc != null && pc.IsOpponentUnlocked(o.opponentId));

        unlocked = sort switch
        {
            SortMode.Difficulty => unlocked.OrderByDescending(o => o.difficultyLevel).ThenBy(o => o.appearanceOrder),
            SortMode.Region     => unlocked.OrderBy(o => o.region).ThenBy(o => o.appearanceOrder),
            _                   => unlocked.OrderBy(o => o.appearanceOrder).ThenBy(o => o.opponentId)
        };

        return unlocked.ToList();
    }

    /// <summary>Oponentes desbloqueados agrupados por región (vista "Por región").</summary>
    public static Dictionary<string, List<OpponentData>> GroupByRegion()
    {
        return GetUnlockedOpponents(SortMode.Region)
            .GroupBy(o => string.IsNullOrEmpty(o.region) ? "Sin región" : o.region)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// (descubiertas, total) de las cartas que este oponente puede soltar. Para
    /// mostrar "Cartas descubiertas: 18/26".
    /// </summary>
    public static (int discovered, int total) GetDropDiscovery(OpponentData opp)
    {
        if (opp == null) return (0, 0);
        var pc = PlayerCollection.Instance;

        int total = 0, discovered = 0;
        foreach (var card in opp.AllRewardCards())
        {
            total++;
            if (pc != null && pc.IsDiscovered(card.cardId)) discovered++;
        }
        return (discovered, total);
    }

    /// <summary>Cartas que dropea el oponente aún NO descubiertas por el jugador.</summary>
    public static List<CardData> GetPendingDropCards(OpponentData opp)
    {
        if (opp == null) return new List<CardData>();
        var pc = PlayerCollection.Instance;
        return opp.AllRewardCards()
            .Where(c => pc == null || !pc.IsDiscovered(c.cardId))
            .ToList();
    }

    /// <summary>Lanza un Duelo Libre contra el oponente elegido (carga la DuelScene).</summary>
    public static void StartFreeDuel(OpponentData opp)
    {
        if (opp == null) return;
        DuelLauncher.Launch(opp);
    }
}
