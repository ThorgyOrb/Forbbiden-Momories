using System.IO;
using UnityEngine.SceneManagement;

/// <summary>
/// Nombres centralizados de las escenas del juego. Toda la navegación pasa
/// por aquí para que no haya strings sueltos repartidos por el código: si
/// renombras una escena, la cambias en un solo sitio.
///
/// IMPORTANTE: estos nombres deben coincidir EXACTAMENTE con el nombre del
/// archivo .unity (sin la extensión) y la escena debe estar añadida en
/// File > Build Settings. El menú "YGO > Setup > Configurar Menú Principal"
/// registra automáticamente todas las escenas por ti.
/// </summary>
public static class GameScenes
{
    // Escena nueva creada por el setup del menú.
    public const string MainMenu = "MainMenu";

    // Escenas ya existentes en Assets/Scenes.
    public const string Duel = "DuelScene";
    public const string Story = "StoryScene";
    public const string FreeDuel = "FreeDuelScene";
    public const string DeckBuilder = "DeckBuilderScene";
    public const string Library = "LibraryScene";
    public const string LibraryCatalog = "LibraryCatalogScene";
    public const string Wireframe = "WireframeStage";
    public const string Sample = "SampleScene";

    /// <summary>
    /// Comprueba si una escena está realmente añadida en Build Settings.
    /// Se usa para navegar de forma segura: si el destino todavía no existe
    /// (una pantalla que aún no has creado), avisamos en lugar de crashear.
    /// </summary>
    public static bool IsInBuild(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;

        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (Path.GetFileNameWithoutExtension(path) == sceneName)
                return true;
        }
        return false;
    }
}
