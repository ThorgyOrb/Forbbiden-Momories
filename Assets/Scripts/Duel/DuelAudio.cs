using UnityEngine;

/// <summary>
/// Vocabulario de audio del duelo (invocar, atacar, fusionar...). Singleton que traduce
/// cada acción a un AudioClip de <see cref="DuelAudioBank"/> ("Resources/DuelAudioBank")
/// y delega la reproducción real en <see cref="GameAudio"/>, el motor persistente
/// compartido por todas las escenas: así la música del duelo hereda el fundido cruzado
/// entre pistas y todos los efectos respetan los sliders de Música/Efectos de Opciones,
/// igual que en el resto del juego. Se auto-crea con <see cref="Ensure"/> y TODO sigue
/// siendo null-safe: sin banco o sin clip, no suena.
///
/// Uso (sin cambios respecto a antes):
///   DuelAudio.Ensure();                       // arranca el sistema (una vez)
///   DuelAudio.Music();                        // música de fondo en bucle
///   DuelAudio.Play(DuelAudio.Sfx.Summon);     // un efecto puntual
///   DuelAudio.Victory();  DuelAudio.Defeat();  DuelAudio.StopMusic();
/// </summary>
public class DuelAudio : MonoBehaviour
{
    public static DuelAudio Instance { get; private set; }

    /// <summary>Cada acción del duelo con sonido. Se mapea a un clip del banco.</summary>
    public enum Sfx
    {
        Cursor, Select, Cancel, GuardianStar,
        Draw, Summon, Flip, SetCard, Spell, Trap,
        FusionStart, Fuse, Equip,
        Attack, Slash, Destroy, Damage,
        Phase, TurnStart
    }

    [SerializeField] private DuelAudioBank bank;

    /// <summary>Crea el singleton si no existe (idempotente). Carga el banco de Resources.</summary>
    public static DuelAudio Ensure()
    {
        GameAudio.EnsureExists(); // motor de música/efectos compartido por todo el juego
        if (Instance == null)
        {
            var go = new GameObject("DuelAudio");
            go.AddComponent<DuelAudio>();   // Awake fija Instance
        }
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (bank == null) bank = Resources.Load<DuelAudioBank>("DuelAudioBank");
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ── Música de fondo ─────────────────────────────────────────────────
    /// <summary>Arranca la música de fondo (o el clip dado) en bucle, con fundido cruzado.</summary>
    public static void Music(AudioClip clip = null)
    { if (Instance != null) Instance.PlayMusic(clip); }

    public static void Victory()
    { if (Instance != null && Instance.bank != null) Instance.PlayMusic(Instance.bank.victoryBgm); }

    public static void Defeat()
    { if (Instance != null && Instance.bank != null) Instance.PlayMusic(Instance.bank.defeatBgm); }

    public static void StopMusic() => GameAudio.StopMusic();

    private void PlayMusic(AudioClip clip)
    {
        AudioClip c = clip != null ? clip : (bank != null ? bank.bgm : null);
        if (c == null) return;
        GameAudio.PlayMusic(c, bank != null ? bank.bgmVolume : 0.5f);
    }

    // ── Efectos ─────────────────────────────────────────────────────────
    /// <summary>Reproduce el efecto de una acción (no hace nada si falta el banco/clip).</summary>
    public static void Play(Sfx s)
    { if (Instance != null) Instance.PlayOne(s); }

    private void PlayOne(Sfx s)
    {
        if (bank == null) return;
        AudioClip c = Clip(s);
        if (c != null) GameAudio.PlaySfx(c, bank.sfxVolume);
    }

    private AudioClip Clip(Sfx s) => s switch
    {
        Sfx.Cursor => bank.cursorMove,
        Sfx.Select => bank.select,
        Sfx.Cancel => bank.cancel,
        Sfx.GuardianStar => bank.guardianStar,
        Sfx.Draw => bank.draw,
        Sfx.Summon => bank.summon,
        Sfx.Flip => bank.flip,
        Sfx.SetCard => bank.setCard,
        Sfx.Spell => bank.spell,
        Sfx.Trap => bank.trap,
        Sfx.FusionStart => bank.fusionStart,
        Sfx.Fuse => bank.fuse,
        Sfx.Equip => bank.equip,
        Sfx.Attack => bank.attack,
        Sfx.Slash => bank.slash,
        Sfx.Destroy => bank.destroy,
        Sfx.Damage => bank.damage,
        Sfx.Phase => bank.phase,
        Sfx.TurnStart => bank.turnStart,
        _ => null
    };
}
