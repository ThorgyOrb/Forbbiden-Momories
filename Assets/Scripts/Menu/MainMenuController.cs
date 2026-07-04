using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Cerebro del Menú Principal. NO crea la interfaz: usa referencias a objetos
/// reales de la escena (asignadas en el Inspector), para que puedas mover,
/// reestilizar y reorganizar todo desde el editor de Unity.
///
/// Para generar la UI ya cableada la primera vez, usa el menú
/// **YGO > Setup > Configurar Menú Principal**. Después edita libremente los
/// objetos en la Hierarchy; mientras conserves las referencias, todo sigue
/// funcionando.
///
/// Funciones: Nueva Partida · Continuar · Historia · Duelo Libre ·
/// Constructor de Deck · Colección · Opciones · Créditos · Salir.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private TextMeshProUGUI toastText;

    [Header("Botones del menú")]
    [SerializeField] private Button nuevaPartidaButton;
    [SerializeField] private Button continuarButton;
    [SerializeField] private Button historiaButton;
    [SerializeField] private Button dueloLibreButton;
    [SerializeField] private Button constructorDeckButton;
    [SerializeField] private Button coleccionButton;
    [SerializeField] private Button opcionesButton;
    [SerializeField] private Button creditosButton;
    [SerializeField] private Button salirButton;

    [Header("Panel de Opciones")]
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Button languageButton;
    [SerializeField] private Button fullscreenButton;
    [SerializeField] private Button optionsBackButton;

    [Header("Panel de Créditos")]
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private Button creditsBackButton;

    [Header("Música (opcional)")]
    [SerializeField] private AudioClip menuMusic;

    [Header("Destinos de escena (déjalos por defecto o repunta si renombras)")]
    [SerializeField] private string storyScene = GameScenes.Story;
    [SerializeField] private string freeDuelScene = GameScenes.FreeDuel;   // pantalla de selección de rival
    [SerializeField] private string deckBuilderScene = GameScenes.DeckBuilder;
    [SerializeField] private string collectionScene = GameScenes.Library;

    // ── Estado interno ───────────────────────────────────────────────────
    private AudioSource _music;
    private SettingsManager _settings;
    private GameNavigator _nav;

    private bool _awaitingNewGameConfirm;
    private Coroutine _toastRoutine;

    private Button[] MenuButtons => new[]
    {
        nuevaPartidaButton, continuarButton, historiaButton, dueloLibreButton,
        constructorDeckButton, coleccionButton, opcionesButton, creditosButton, salirButton
    };

    // ── Ciclo de vida ────────────────────────────────────────────────────

    void Start()
    {
        _nav = GameNavigator.EnsureExists();
        _settings = SettingsManager.EnsureExists();

        if (!ValidateReferences()) return;

        WireButtons();
        WireOptionsPanel();

        continuarButton.interactable = GameProgress.HasSave();

        optionsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        if (toastText != null) toastText.alpha = 0f;

        SetupMusic();
        _settings.OnSettingsChanged += RefreshMusicVolume;

        if (titleText != null) StartCoroutine(PulseTitle());
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
            if (optionsPanel != null && optionsPanel.activeSelf) { CloseOptions(); return; }
            if (creditsPanel != null && creditsPanel.activeSelf) { CloseCredits(); return; }
        }
    }

    // ── Cableado ─────────────────────────────────────────────────────────

    private void WireButtons()
    {
        nuevaPartidaButton.onClick.AddListener(NuevaPartida);
        continuarButton.onClick.AddListener(Continuar);
        historiaButton.onClick.AddListener(Historia);
        dueloLibreButton.onClick.AddListener(DueloLibre);
        constructorDeckButton.onClick.AddListener(ConstructorDeck);
        coleccionButton.onClick.AddListener(Coleccion);
        opcionesButton.onClick.AddListener(OpenOptions);
        creditosButton.onClick.AddListener(OpenCredits);
        salirButton.onClick.AddListener(Salir);
    }

    private void WireOptionsPanel()
    {
        masterSlider.value = _settings.MasterVolume;
        musicSlider.value = _settings.MusicVolume;
        sfxSlider.value = _settings.SfxVolume;

        masterSlider.onValueChanged.AddListener(_settings.SetMasterVolume);
        musicSlider.onValueChanged.AddListener(_settings.SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(_settings.SetSfxVolume);

        RefreshLanguageLabel();
        RefreshFullscreenLabel();

        languageButton.onClick.AddListener(CycleLanguage);
        fullscreenButton.onClick.AddListener(ToggleFullscreen);
        optionsBackButton.onClick.AddListener(CloseOptions);
        creditsBackButton.onClick.AddListener(CloseCredits);
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
        optionsPanel.SetActive(true);
        Select(optionsBackButton.gameObject);
    }

    private void CloseOptions()
    {
        optionsPanel.SetActive(false);
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
        SetButtonLabel(languageButton, $"Idioma:  {lang}");
    }

    private void RefreshFullscreenLabel()
    {
        string state = _settings.Fullscreen ? "Sí" : "No";
        SetButtonLabel(fullscreenButton, $"Pantalla completa:  {state}");
    }

    // ── Créditos ─────────────────────────────────────────────────────────

    private void OpenCredits()
    {
        creditsPanel.SetActive(true);
        Select(creditsBackButton.gameObject);
    }

    private void CloseCredits()
    {
        creditsPanel.SetActive(false);
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
        var rt = titleText.rectTransform;
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
        if (toastText == null) { Debug.Log($"[Menú] {message}"); return; }
        if (_toastRoutine != null) StopCoroutine(_toastRoutine);
        _toastRoutine = StartCoroutine(ToastRoutine(message));
    }

    private IEnumerator ToastRoutine(string message)
    {
        var t = toastText;
        t.text = message;

        for (float a = 0; a < 1; a += Time.unscaledDeltaTime * 5f) { t.alpha = a; yield return null; }
        t.alpha = 1f;

        yield return new WaitForSecondsRealtime(2.2f);

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
        foreach (var b in MenuButtons)
            if (b != null && b.interactable) { Select(b.gameObject); return; }
    }

    private void Select(GameObject go)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(go);
    }

    private static void SetButtonLabel(Button button, string text)
    {
        if (button == null) return;
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = text;
    }

    /// <summary>Comprueba que todas las referencias del Inspector estén asignadas.</summary>
    private bool ValidateReferences()
    {
        var missing = new List<string>();
        void Req(Object o, string n) { if (o == null) missing.Add(n); }

        Req(titleText, "titleText");
        Req(subtitleText, "subtitleText");
        Req(toastText, "toastText");
        Req(nuevaPartidaButton, "nuevaPartidaButton");
        Req(continuarButton, "continuarButton");
        Req(historiaButton, "historiaButton");
        Req(dueloLibreButton, "dueloLibreButton");
        Req(constructorDeckButton, "constructorDeckButton");
        Req(coleccionButton, "coleccionButton");
        Req(opcionesButton, "opcionesButton");
        Req(creditosButton, "creditosButton");
        Req(salirButton, "salirButton");
        Req(optionsPanel, "optionsPanel");
        Req(masterSlider, "masterSlider");
        Req(musicSlider, "musicSlider");
        Req(sfxSlider, "sfxSlider");
        Req(languageButton, "languageButton");
        Req(fullscreenButton, "fullscreenButton");
        Req(optionsBackButton, "optionsBackButton");
        Req(creditsPanel, "creditsPanel");
        Req(creditsBackButton, "creditsBackButton");

        if (missing.Count > 0)
        {
            Debug.LogError("MainMenuController: faltan referencias en el Inspector: " +
                           string.Join(", ", missing) +
                           ".\nEjecuta 'YGO > Setup > Configurar Menú Principal' para construir la UI ya cableada.");
            return false;
        }
        return true;
    }
}
