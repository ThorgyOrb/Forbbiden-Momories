/// <summary>
/// Puente entre "elegir un rival" (menú, Duelo Libre, campaña) y la escena de
/// duelo. Guarda a QUIÉN enfrentar y carga la DuelScene; el DuelController lee esa
/// selección al arrancar en vez de tener un rival fijo en la escena.
///
///   • Duelo normal:  DuelLauncher.Launch(opponentData)
///   • Duelo especial: DuelLauncher.Launch(opponentData, duelConfigConOverrides)
///
/// Es estático (la selección sobrevive al cambio de escena). El DuelController
/// consume y limpia la selección al iniciar el duelo.
/// </summary>
public static class DuelLauncher
{
    /// <summary>Rival elegido para el próximo duelo (null = usar el config de la escena).</summary>
    public static OpponentData PendingOpponent { get; private set; }

    /// <summary>Overrides opcionales de ese duelo (terreno/recompensa forzados). Puede ser null.</summary>
    public static DuelConfig PendingConfig { get; private set; }

    /// <summary>Elige el rival y carga la escena de duelo.</summary>
    public static void Launch(OpponentData opponent, DuelConfig overrides = null)
    {
        PendingOpponent = opponent;
        PendingConfig = overrides;
        GameNavigator.EnsureExists().GoTo(GameScenes.Duel);
    }

    /// <summary>Limpia la selección (la consume el DuelController al empezar).</summary>
    public static void Clear()
    {
        PendingOpponent = null;
        PendingConfig = null;
    }
}
