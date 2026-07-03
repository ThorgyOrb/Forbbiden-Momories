using UnityEngine;

/// <summary>
/// Configuración global del juego (audio, idioma, pantalla). Es un singleton
/// persistente igual que <see cref="PlayerCollection"/>: sobrevive a los
/// cambios de escena y se crea solo si no existe (<see cref="EnsureExists"/>).
///
/// Guarda en PlayerPrefs (valores pequeños de configuración) y aplica los
/// ajustes al vuelo — el volumen maestro va directo al AudioListener global,
/// así que afecta a toda la mezcla de audio del juego.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    // ── Claves de PlayerPrefs ────────────────────────────────────────────
    private const string KeyMaster = "cfg_master";
    private const string KeyMusic = "cfg_music";
    private const string KeySfx = "cfg_sfx";
    private const string KeyLanguage = "cfg_language";
    private const string KeyFullscreen = "cfg_fullscreen";

    // ── Estado (0..1 para volúmenes) ─────────────────────────────────────
    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 0.8f;
    public float SfxVolume { get; private set; } = 1f;
    public int Language { get; private set; } = 0;   // 0 = Español, 1 = English…
    public bool Fullscreen { get; private set; } = true;

    /// <summary>Se dispara cuando cambia cualquier ajuste (para refrescar UI/música).</summary>
    public System.Action OnSettingsChanged;

    /// <summary>Crea el manager si todavía no existe. Idempotente.</summary>
    public static SettingsManager EnsureExists()
    {
        if (Instance == null)
        {
            var go = new GameObject("SettingsManager");
            Instance = go.AddComponent<SettingsManager>();
        }
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
        Apply();
    }

    // ── Setters (persisten + aplican + notifican) ────────────────────────

    public void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KeyMaster, MasterVolume); Commit(); }
    public void SetMusicVolume(float v)  { MusicVolume = Mathf.Clamp01(v);  PlayerPrefs.SetFloat(KeyMusic, MusicVolume);   Commit(); }
    public void SetSfxVolume(float v)    { SfxVolume = Mathf.Clamp01(v);    PlayerPrefs.SetFloat(KeySfx, SfxVolume);       Commit(); }
    public void SetLanguage(int index)   { Language = index;                PlayerPrefs.SetInt(KeyLanguage, Language);     Commit(); }

    public void SetFullscreen(bool on)
    {
        Fullscreen = on;
        PlayerPrefs.SetInt(KeyFullscreen, on ? 1 : 0);
        Screen.fullScreen = on;
        Commit();
    }

    private void Commit()
    {
        PlayerPrefs.Save();
        Apply();
        OnSettingsChanged?.Invoke();
    }

    // ── Carga / aplicación ───────────────────────────────────────────────

    private void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat(KeyMaster, 1f);
        MusicVolume = PlayerPrefs.GetFloat(KeyMusic, 0.8f);
        SfxVolume = PlayerPrefs.GetFloat(KeySfx, 1f);
        Language = PlayerPrefs.GetInt(KeyLanguage, 0);
        Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
    }

    private void Apply()
    {
        // El volumen maestro escala toda la mezcla global.
        AudioListener.volume = MasterVolume;
    }
}
