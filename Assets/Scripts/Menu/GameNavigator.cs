using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Punto único de navegación entre escenas. Nadie llama a SceneManager.LoadScene
/// directamente: todo pasa por aquí para tener un único sitio donde añadir
/// transiciones, sonidos, comprobaciones, etc.
///
/// Es un singleton persistente que se crea solo la primera vez que se necesita
/// (<see cref="EnsureExists"/>), y de paso se asegura de que existan los otros
/// managers globales (Settings). Carga de escena SEGURA: si el destino todavía
/// no está en Build Settings, avisa por consola en vez de lanzar una excepción,
/// para que puedas ir construyendo el juego escena a escena sin romper el menú.
/// </summary>
public class GameNavigator : MonoBehaviour
{
    public static GameNavigator Instance { get; private set; }

    /// <summary>
    /// Se dispara cuando se intenta ir a una escena que aún no existe en Build
    /// Settings. El Menú lo usa para mostrar un aviso tipo "Próximamente".
    /// </summary>
    public System.Action<string> OnSceneMissing;

    public static GameNavigator EnsureExists()
    {
        if (Instance == null)
        {
            var go = new GameObject("GameNavigator");
            Instance = go.AddComponent<GameNavigator>();
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

        // Garantiza que la configuración global esté viva desde el arranque.
        SettingsManager.EnsureExists();
    }

    // ── Navegación ───────────────────────────────────────────────────────

    /// <summary>
    /// Carga una escena por nombre de forma segura. Devuelve false (y avisa)
    /// si la escena no está registrada en Build Settings todavía.
    /// </summary>
    public bool GoTo(string sceneName)
    {
        if (!GameScenes.IsInBuild(sceneName))
        {
            Debug.LogWarning($"GameNavigator: la escena '{sceneName}' no está en Build Settings todavía. " +
                             "Añádela (o créala) para poder navegar a ella.");
            OnSceneMissing?.Invoke(sceneName);
            return false;
        }

        SceneManager.LoadScene(sceneName);
        return true;
    }

    public void ToMainMenu() => GoTo(GameScenes.MainMenu);

    /// <summary>Cierra el juego (en el Editor detiene el Play Mode).</summary>
    public void QuitGame()
    {
        Debug.Log("GameNavigator: saliendo del juego.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
