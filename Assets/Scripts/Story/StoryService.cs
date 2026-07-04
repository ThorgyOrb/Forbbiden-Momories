using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Lógica del MODO HISTORIA (campaña). Define la secuencia de rivales, en qué
/// capítulo va el jugador y cómo lanzar el duelo de campaña. La pantalla visual
/// (<see cref="StoryController"/>) solo consume estos métodos.
///
/// Idea clave: el progreso NO se guarda a mano, se DERIVA de qué oponentes ha
/// derrotado el jugador (<see cref="PlayerCollection"/>). Así, cuando vuelves de
/// ganar un duelo, la campaña avanza sola: el "capítulo actual" es siempre el
/// primer rival de la secuencia que aún no has derrotado. <see cref="GameProgress"/>
/// solo refleja ese índice para el "Continuar" del menú y la fecha de última vez.
/// </summary>
public static class StoryService
{
    /// <summary>
    /// La campaña: todos los oponentes ordenados por orden de aparición.
    /// (appearanceOrder, luego opponentId como desempate estable).
    /// </summary>
    public static List<OpponentData> GetCampaign()
    {
        return LibraryCatalog.AllOpponents
            .Where(o => o != null)
            .OrderBy(o => o.appearanceOrder)
            .ThenBy(o => o.opponentId)
            .ToList();
    }

    /// <summary>¿El jugador ya derrotó a este rival? (capítulo completado).</summary>
    public static bool IsDefeated(OpponentData opp)
    {
        if (opp == null) return false;
        var pc = PlayerCollection.Instance;
        return pc != null && pc.IsOpponentUnlocked(opp.opponentId);
    }

    /// <summary>
    /// Índice del capítulo ACTUAL = primer rival de la secuencia no derrotado.
    /// Si están todos derrotados devuelve campaign.Count (campaña completada).
    /// </summary>
    public static int GetCurrentIndex(List<OpponentData> campaign = null)
    {
        campaign ??= GetCampaign();
        for (int i = 0; i < campaign.Count; i++)
            if (!IsDefeated(campaign[i])) return i;
        return campaign.Count;
    }

    /// <summary>El rival del capítulo actual (null si la campaña está completa o vacía).</summary>
    public static OpponentData GetCurrentOpponent(List<OpponentData> campaign = null)
    {
        campaign ??= GetCampaign();
        int i = GetCurrentIndex(campaign);
        return (i >= 0 && i < campaign.Count) ? campaign[i] : null;
    }

    /// <summary>¿Se completó toda la campaña?</summary>
    public static bool IsCampaignComplete(List<OpponentData> campaign = null)
    {
        campaign ??= GetCampaign();
        return campaign.Count > 0 && GetCurrentIndex(campaign) >= campaign.Count;
    }

    /// <summary>
    /// ¿Está desbloqueado (visible/jugable) este capítulo? Lo están el actual y
    /// todos los anteriores (ya derrotados). Los futuros aparecen bloqueados.
    /// </summary>
    public static bool IsChapterUnlocked(int index, List<OpponentData> campaign = null)
    {
        campaign ??= GetCampaign();
        return index <= GetCurrentIndex(campaign);
    }

    /// <summary>Estado de un capítulo para pintarlo en la hoja de ruta.</summary>
    public enum ChapterState { Completed, Current, Locked }

    public static ChapterState GetChapterState(int index, List<OpponentData> campaign = null)
    {
        campaign ??= GetCampaign();
        int current = GetCurrentIndex(campaign);
        if (index < current) return ChapterState.Completed;
        if (index == current) return ChapterState.Current;
        return ChapterState.Locked;
    }

    /// <summary>
    /// Sincroniza <see cref="GameProgress"/> con el progreso derivado (índice del
    /// capítulo actual) y actualiza la fecha de última vez. Llámalo al entrar a la
    /// escena de Historia. Si no había partida, la crea.
    /// </summary>
    public static void SyncProgress(List<OpponentData> campaign = null)
    {
        campaign ??= GetCampaign();
        var data = GameProgress.Load();
        if (data == null || !data.exists)
            data = GameProgress.StartNew();

        data.storyNode = GetCurrentIndex(campaign);
        GameProgress.Save();
    }

    /// <summary>
    /// Lanza el duelo de campaña contra el rival indicado (carga la DuelScene).
    /// Marca al rival como "encontrado" para que aparezca su registro.
    /// </summary>
    public static void StartStoryDuel(OpponentData opp)
    {
        if (opp == null) return;
        PlayerCollection.Instance?.MarkOpponentFound(opp.opponentId);
        DuelLauncher.Launch(opp);
    }
}
