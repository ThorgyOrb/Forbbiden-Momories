using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Motor de audio GLOBAL del juego (singleton persistente, sobrevive a todos los cambios
/// de escena). Reproduce música de fondo con fundido cruzado y efectos de interfaz
/// comunes, leyendo <see cref="GameAudioBank"/> ("Resources/GameAudioBank"), y cambia de
/// pista solo al cargar cada escena según su mapa (<see cref="GameAudioBank.sceneMusic"/>).
/// Respeta SIEMPRE los sliders de Música/Efectos de <see cref="SettingsManager"/>, incluso
/// mientras el usuario los mueve con la música ya sonando.
///
/// <see cref="DuelAudio"/> delega en este motor la reproducción real (música y efectos)
/// para heredar el fundido cruzado y los volúmenes globales; su vocabulario de acciones
/// del duelo (invocar, atacar, fusionar...) no cambia. Se auto-crea con
/// <see cref="EnsureExists"/> y TODO es null-safe: sin banco o sin clip, no suena.
///
/// Uso desde cualquier escena/script:
///   GameAudio.EnsureExists();                    // arranca el sistema (una vez; ya lo
///                                                 // hace GameNavigator al crearse)
///   GameAudio.PlayMusic(miClip);                 // cambia de música con fundido cruzado
///   GameAudio.PlaySfx(miClip);                    // un efecto puntual
///   GameAudio.Click();  GameAudio.Hover();  GameAudio.Back();  GameAudio.Error();
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    [SerializeField] private GameAudioBank bank;

    private AudioSource _musicA;
    private AudioSource _musicB;
    private AudioSource _activeMusic;
    private AudioSource _sfx;

    private AudioClip _currentMusicClip;
    private float _musicBaseVolume = 1f;
    private Coroutine _fadeRoutine;

    private SettingsManager _settings;

    /// <summary>Crea el singleton si no existe (idempotente). Carga el banco de Resources.</summary>
    public static GameAudio EnsureExists()
    {
        if (Instance == null)
        {
            var go = new GameObject("GameAudio");
            go.AddComponent<GameAudio>();   // Awake fija Instance
        }
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (bank == null) bank = Resources.Load<GameAudioBank>("GameAudioBank");

        _musicA = gameObject.AddComponent<AudioSource>();
        _musicA.loop = true; _musicA.playOnAwake = false;
        _musicB = gameObject.AddComponent<AudioSource>();
        _musicB.loop = true; _musicB.playOnAwake = false;
        _activeMusic = _musicA;

        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;

        _settings = SettingsManager.EnsureExists();
        _settings.OnSettingsChanged += ApplyVolumeToActiveMusic;

        SceneManager.sceneLoaded += OnSceneLoaded;
        PlaySceneMusic(SceneManager.GetActiveScene().name); // música de la escena ya cargada
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (_settings != null) _settings.OnSettingsChanged -= ApplyVolumeToActiveMusic;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => PlaySceneMusic(scene.name);

    private void PlaySceneMusic(string sceneName)
    {
        if (bank == null) return;
        AudioClip clip = bank.GetMusicForScene(sceneName);
        // Sin entrada para esta escena (p. ej. "DuelScene"): no tocamos nada, la música
        // en curso sigue sonando hasta que algo la sustituya explícitamente (DuelAudio).
        if (clip != null) CrossfadeTo(clip, bank.musicVolume, bank.musicFadeSeconds);
    }

    // ── Música ───────────────────────────────────────────────────────────

    /// <summary>Cambia de música con fundido cruzado. No reinicia el clip si ya es el que suena.</summary>
    public static void PlayMusic(AudioClip clip, float baseVolume = 1f, float fadeSeconds = -1f)
    {
        if (Instance == null || clip == null) return;
        float fade = fadeSeconds >= 0f ? fadeSeconds : (Instance.bank != null ? Instance.bank.musicFadeSeconds : 0.6f);
        Instance.CrossfadeTo(clip, baseVolume, fade);
    }

    public static void StopMusic(float fadeSeconds = 0.4f)
    { if (Instance != null) Instance.FadeOutActive(fadeSeconds); }

    private void CrossfadeTo(AudioClip clip, float baseVolume, float fadeSeconds)
    {
        if (clip == null) return;
        _musicBaseVolume = baseVolume;

        if (_currentMusicClip == clip && _activeMusic.isPlaying)
        {
            ApplyVolumeToActiveMusic(); // mismo clip: solo re-sincroniza el volumen, no reinicia
            return;
        }
        _currentMusicClip = clip;

        var incoming = _activeMusic == _musicA ? _musicB : _musicA;
        var outgoing = _activeMusic;
        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.Play();
        _activeMusic = incoming;

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(outgoing, incoming, fadeSeconds));
    }

    private void FadeOutActive(float fadeSeconds)
    {
        _currentMusicClip = null;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(_activeMusic, null, fadeSeconds));
    }

    private IEnumerator FadeRoutine(AudioSource outgoing, AudioSource incoming, float seconds)
    {
        float startOut = outgoing != null ? outgoing.volume : 0f;
        seconds = Mathf.Max(seconds, 0.01f);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = t / seconds;
            if (outgoing != null) outgoing.volume = Mathf.Lerp(startOut, 0f, k);
            if (incoming != null) incoming.volume = Mathf.Lerp(0f, TargetMusicVolume(), k);
            yield return null;
        }
        if (outgoing != null) { outgoing.volume = 0f; outgoing.Stop(); }
        if (incoming != null) incoming.volume = TargetMusicVolume();
        _fadeRoutine = null;
    }

    private float TargetMusicVolume() => _musicBaseVolume * (_settings != null ? _settings.MusicVolume : 1f);

    /// <summary>Re-sincroniza el volumen de la música activa (p. ej. al mover el slider en Opciones).</summary>
    private void ApplyVolumeToActiveMusic()
    {
        if (_fadeRoutine == null && _activeMusic != null && _activeMusic.isPlaying)
            _activeMusic.volume = TargetMusicVolume();
    }

    // ── Efectos ──────────────────────────────────────────────────────────

    /// <summary>Reproduce un efecto puntual (no hace nada si falta el clip).</summary>
    public static void PlaySfx(AudioClip clip, float baseVolume = 1f)
    {
        if (Instance == null || clip == null || Instance._sfx == null) return;
        float v = baseVolume * (Instance._settings != null ? Instance._settings.SfxVolume : 1f);
        Instance._sfx.PlayOneShot(clip, v);
    }

    // ── UI común ─────────────────────────────────────────────────────────

    public static void Click()  { if (Instance != null && Instance.bank != null) PlaySfx(Instance.bank.uiClick,  Instance.bank.uiVolume); }
    public static void Hover()  { if (Instance != null && Instance.bank != null) PlaySfx(Instance.bank.uiHover,  Instance.bank.uiVolume); }
    public static void Back()   { if (Instance != null && Instance.bank != null) PlaySfx(Instance.bank.uiBack,   Instance.bank.uiVolume); }
    public static void Toggle() { if (Instance != null && Instance.bank != null) PlaySfx(Instance.bank.uiToggle, Instance.bank.uiVolume); }
    public static void Error()  { if (Instance != null && Instance.bank != null) PlaySfx(Instance.bank.uiError,  Instance.bank.uiVolume); }
}
