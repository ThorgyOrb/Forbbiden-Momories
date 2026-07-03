using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Cerebro del Menú Principal. Coloca este componente en un GameObject vacío de
/// la escena "MainMenu" (el menú "YGO > Setup > Configurar Menú Principal" lo
/// hace por ti). En Play construye la interfaz por código, conecta los nueve
/// botones y gestiona la navegación entre pantallas.
///
/// Funciones:
///   Nueva Partida · Continuar · Historia · Duelo Libre · Constructor de Deck
///   Colección · Opciones · Créditos · Salir
///
/// Los destinos que todavía no tengan escena creada muestran un aviso
/// ("Próximamente") en vez de fallar, para que puedas ir construyendo el juego
/// pantalla a pantalla.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Presentación")]
    [SerializeField] private string gameTitle = "MEMORIAS PROHIBIDAS";
    [SerializeField] private string subtitle = "Un sucesor espiritual de Yu-Gi-Oh! Forbidden Memories";

    [Header("Música (opcional)")]
    [SerializeField] private AudioClip menuMusic;

    [Header("Destinos de escena (déjalos por defecto o repunta si renombras)")]
    [SerializeField] private string storyScene = "StoryScene";
    [SerializeField] private string freeDuelScene = GameScenes.Duel;         // por ahora lanza el duelo de prueba
    [SerializeField] private string deckBuilderScene = "DeckBuilderScene";
    [SerializeField] private string collectionScene = GameScenes.Library;

    // ── Estado interno ───────────────────────────────────────────────────
    private MainMenuUI _ui;
    private AudioSource _music;
    private SettingsManager _settings;
    private GameNavigator _nav;

    private bool _awaitingNewGameConfirm;
    private Coroutine _toastRoutine;

    // ── Ciclo de vida ────────────────────────────────────────────────────

    void Start()
    {
        _nav = GameNavigator.EnsureExists();
        _settings = SettingsManager.EnsureExists();

        BuildMenu();
        SetupMusic();

        _settings.OnSettingsChanged += RefreshMusicVolume;

        StartCoroutine(PulseTitle());
        SelectFirstButton();
    }

    void OnDestroy()
    {
        if (_settings != null) _settings.OnSettingsChanged -= RefreshMusicVolume;
    }

    void Update()
    {
        // Escape cierra el panel abierto (o no hace nada en el menú raíz).
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_ui.optionsPanel.activeSelf) { CloseOptions(); return; }
            if (_ui.creditsPanel.activeSelf) { CloseCredits(); return; }
        }
    }

    // ── Construcción ─────────────────────────────────────────────────────

    private void BuildMenu()
    {
        bool hasSave = GameProgress.HasSave();

        var defs = new List<MenuButtonDef>
        {
            new MenuButtonDef("Nueva Partida",       NuevaPartida),
            new MenuButtonDef("Continuar",           Continuar, interactable: hasSave),
            new MenuButtonDef("Historia",            Historia),
            new MenuButtonDef("Duelo Libre",         DueloLibre),
            new MenuButtonDef("Constructor de Deck", ConstructorDeck),
            new MenuButtonDef("Colección",           Coleccion),
            new MenuButtonDef("Opciones",            OpenOptions),
            new MenuButtonDef("Créditos",            OpenCredits),
            new MenuButtonDef("Salir",               Salir),
        };

        _ui = MainMenuUIBuilder.Build(transform, gameTitle, subtitle, defs);

        WireOptionsPanel();
        _ui.creditsBackButton.onClick.AddListener(CloseCredits);
    }

    private void WireOptionsPanel()
    {
        _ui.masterSlider.value = _settings.MasterVolume;
        _ui.musicSlider.value = _settings.MusicVolume;
        _ui.sfxSlider.value = _settings.SfxVolume;

        _ui.masterSlider.onValueChanged.AddListener(_settings.SetMasterVolume);
        _ui.musicSlider.onValueChanged.AddListener(_settings.SetMusicVolume);
        _ui.sfxSlider.onValueChanged.AddListener(_settings.SetSfxVolume);

        RefreshLanguageLabel();
        RefreshFullscreenLabel();

        _ui.languageButton.onClick.AddListener(CycleLanguage);
        _ui.fullscreenButton.onClick.AddListener(ToggleFullscreen);
        _ui.optionsBackButton.onClick.AddListener(CloseOptions);
    }

    // ── Acciones de los botones ──────────────────────────────────────────

    private void NuevaPartida()
    {
        if (!GameScenes.IsInBuild(storyScene))
        {
            ShowToast("El modo Historia todavía no está disponible.");
            return;
        }

        // Confirmación en dos pasos para no sobrescribir una partida por error.
        if (GameProgress.HasSave() && !_awaitingNewGameConfirm)
        {
            _awaitingNewGameConfirm = true;
            ShowToast("Ya tienes una partida. Pulsa otra vez para empezar de cero.");
            StartCoroutine(ResetNewGameConfirm());
            return;
        }

        GameProgress.StartNew();
        _nav.GoTo(storyScene);
    }

    private void Continuar()
    {
        if (!GameProgress.HasSave())
        {
            ShowToast("No hay ninguna partida guardada.");
            return;
        }
        GoToOrToast(storyScene, "El modo Historia todavía no está disponible.");
    }

    private void Historia()
    {
        // Entra a la campaña: continúa si hay partida, o pide crear una nueva.
        if (GameProgress.HasSave())
            GoToOrToast(storyScene, "El modo Historia todavía no está disponible.");
        else
            NuevaPartida();
    }

    private void DueloLibre() =>
        GoToOrToast(freeDuelScene, "El Duelo Libre todavía no está disponible.");

    private void ConstructorDeck() =>
        GoToOrToast(deckBuilderScene, "El Constructor de Deck todavía no está disponible.");

    private void Coleccion() =>
        GoToOrToast(collectionScene, "La Colección todavía no está disponible.");

    private void Salir() => _nav.QuitGame();

    // ── Opciones ─────────────────────────────────────────────────────────

    private void OpenOptions()
    {
        _ui.optionsPanel.SetActive(true);
        Select(_ui.optionsBackButton.gameObject);
    }

    private void CloseOptions()
    {
        _ui.optionsPanel.SetActive(false);
        SelectFirstButton();
    }

    private void CycleLanguage()
    {
        int next = (_settings.Language + 1) % 2; // 0 = Español, 1 = English
        _settings.SetLanguage(next);
        RefreshLanguageLabel();
    }

    private void ToggleFullscreen()
    {
        _settings.SetFullscreen(!_settings.Fullscreen);
        RefreshFullscreenLabel();
    }

    private void RefreshLanguageLabel()
    {
        string lang = _settings.Language == 0 ? "Español" : "English";
        SetButtonLabel(_ui.languageButton, $"Idioma:  {lang}");
    }

    private void RefreshFullscreenLabel()
    {
        string state = _settings.Fullscreen ? "Sí" : "No";
        SetButtonLabel(_ui.fullscreenButton, $"Pantalla completa:  {state}");
    }

    // ── Créditos ─────────────────────────────────────────────────────────

    private void OpenCredits()
    {
        _ui.creditsPanel.SetActive(true);
        Select(_ui.creditsBackButton.gameObject);
    }

    private void CloseCredits()
    {
        _ui.creditsPanel.SetActive(false);
        SelectFirstButton();
    }

    // ── Música ───────────────────────────────────────────────────────────

    private void SetupMusic()
    {
        if (menuMusic == null) return;

        _music = gameObject.AddComponent<AudioSource>();
        _music.clip = menuMusic;
        _music.loop = true;
        _music.playOnAwake = false;
        _music.volume = _settings.MusicVolume; // el volumen general ya lo aplica el AudioListener
        _music.Play();
    }

    private void RefreshMusicVolume()
    {
        if (_music != null) _music.volume = _settings.MusicVolume;
    }

    // ── Animación del título ─────────────────────────────────────────────

    private IEnumerator PulseTitle()
    {
        var rt = _ui.title.rectTransform;
        while (true)
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * 1.6f) * 0.02f;
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    // ── Toast (aviso temporal) ───────────────────────────────────────────

    private void ShowToast(string message)
    {
        if (_toastRoutine != null) StopCoroutine(_toastRoutine);
        _toastRoutine = StartCoroutine(ToastRoutine(message));
    }

    private IEnumerator ToastRoutine(string message)
    {
        var t = _ui.toast;
        t.text = message;

        // Fade in
        for (float a = 0; a < 1; a += Time.unscaledDeltaTime * 5f) { t.alpha = a; yield return null; }
        t.alpha = 1f;

        yield return new WaitForSecondsRealtime(2.2f);

        // Fade out
        for (float a = 1; a > 0; a -= Time.unscaledDeltaTime * 3f) { t.alpha = a; yield return null; }
        t.alpha = 0f;
        _toastRoutine = null;
    }

    private IEnumerator ResetNewGameConfirm()
    {
        yield return new WaitForSecondsRealtime(4f);
        _awaitingNewGameConfirm = false;
    }

    // ── Utilidades ───────────────────────────────────────────────────────

    private void GoToOrToast(string scene, string friendlyMessage)
    {
        if (!_nav.GoTo(scene)) ShowToast(friendlyMessage);
    }

    private void SelectFirstButton()
    {
        foreach (var b in _ui.buttons)
            if (b.interactable) { Select(b.gameObject); return; }
    }

    private void Select(GameObject go)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(go);
    }

    private static void SetButtonLabel(Button button, string text)
    {
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = text;
    }
}
