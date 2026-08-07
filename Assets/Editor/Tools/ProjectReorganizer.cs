using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reordena el proyecto al layout estándar de Unity, de una sola pasada y sin romper
/// referencias.
///
/// TODO se mueve con <see cref="AssetDatabase.MoveAsset"/>, que conserva el GUID del asset:
/// escenas, prefabs y campos serializados siguen apuntando a lo mismo. Mover los archivos
/// desde el explorador (o desde PowerShell) sin su <c>.meta</c> los rompería.
///
/// Regla de oro del layout: <b>en Resources solo vive lo que se carga por ruta</b>
/// (<c>Resources.Load</c>). Hoy eso es Cards/Data, Opponents/Data, Fusions,
/// Prefabs/CardMonsterV2 y DuelAudioBank; el resto (audio, iconos, modelos, configs) sale
/// fuera, porque todo lo que está en Resources entra en la build aunque nadie lo use.
///
/// Las cartas NO se mueven aquí: son generadas, así que el importador las vuelve a crear
/// directamente en <c>Cards/Data/&lt;Categoría&gt;/&lt;Letra&gt;/</c>, que es mucho más
/// rápido que mover 14.000 assets. Sí se mueven las hechas a mano.
///
/// Menú: YGO ▸ Proyecto ▸ Reorganizar proyecto.
/// </summary>
public static class ProjectReorganizer
{
    /// <summary>Un movimiento: de dónde, a dónde. Las carpetas destino se crean solas.</summary>
    private readonly struct Move
    {
        public readonly string From;
        public readonly string To;
        public Move(string from, string to) { From = from; To = to; }
    }

    [MenuItem("YGO/Proyecto/Reorganizar proyecto")]
    public static void Run()
    {
        var moves = BuildMoveList();

        int moved = 0, missing = 0, failed = 0, handMade = 0;
        var problems = new List<string>();

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < moves.Count; i++)
            {
                var m = moves[i];
                EditorUtility.DisplayProgressBar("Reorganizando proyecto",
                    $"{Path.GetFileName(m.From)} → {m.To}", (float)i / moves.Count);

                if (!AssetExists(m.From)) { missing++; continue; }
                if (AssetExists(m.To)) { problems.Add($"YA EXISTE el destino: {m.To}"); failed++; continue; }

                EnsureFolder(ParentOf(m.To));

                string error = AssetDatabase.MoveAsset(m.From, m.To);
                if (string.IsNullOrEmpty(error)) moved++;
                else { problems.Add($"{m.From} → {m.To}: {error}"); failed++; }
            }

            // Dentro del mismo bloque a propósito: mover scripts provoca recompilación, y
            // el Refresh de después puede recargar el dominio y cortar este método. Todo
            // lo que mueve assets tiene que haber pasado ya a estas alturas.
            handMade = MoveHandMadeCards(problems);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int emptied = DeleteEmptyLeftovers();

        var sb = new StringBuilder();
        sb.AppendLine("REORGANIZACIÓN COMPLETADA");
        sb.AppendLine($"Assets movidos: {moved}");
        sb.AppendLine($"Cartas hechas a mano reubicadas: {handMade}");
        sb.AppendLine($"Ya estaban en su sitio (origen inexistente): {missing}");
        sb.AppendLine($"Carpetas vacías eliminadas: {emptied}");
        sb.AppendLine($"Fallos: {failed}");
        foreach (var p in problems.Take(25)) sb.AppendLine("   ✗ " + p);

        if (failed == 0) Debug.Log("ProjectReorganizer\n" + sb);
        else Debug.LogWarning("ProjectReorganizer\n" + sb);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  La lista de movimientos
    // ─────────────────────────────────────────────────────────────────────

    private static List<Move> BuildMoveList()
    {
        var m = new List<Move>();

        // ── Settings: los assets sueltos de URP que estaban en la raíz ──
        m.Add(new Move("Assets/New Universal Render Pipeline Asset.asset",
                       "Assets/Settings/UniversalRenderPipelineAsset.asset"));
        m.Add(new Move("Assets/New Universal Render Pipeline Asset_Renderer.asset",
                       "Assets/Settings/UniversalRenderPipelineAsset_Renderer.asset"));
        m.Add(new Move("Assets/UniversalRenderPipelineGlobalSettings.asset",
                       "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset"));

        // ── Art: materiales, shaders y render textures ──
        m.Add(new Move("Assets/Materials", "Assets/Art/Materials"));
        m.Add(new Move("Assets/Shaders", "Assets/Art/Shaders"));
        m.Add(new Move("Assets/RT_ModelViewer.renderTexture",
                       "Assets/Art/RenderTextures/RT_ModelViewer.renderTexture"));
        m.Add(new Move("Assets/Scenes/LibraryGods_ViewerRT.renderTexture",
                       "Assets/Art/RenderTextures/LibraryGods_ViewerRT.renderTexture"));

        // ── Art/Sprites: la carpeta plana de antes, repartida por uso ──
        m.Add(new Move("Assets/Sprites/Cards", "Assets/Art/Sprites/Cards"));
        m.Add(new Move("Assets/Sprites/BuilDeck", "Assets/Art/Sprites/DeckBuilder"));

        // Piezas con las que se hornea el prefab de carta.
        foreach (var f in new[]
        {
            "card_base_cut.png", "card_base_v3.png", "frame_cut.png", "frame_v2.png",
            "frame_v3.png", "frame_v4.png", "frame_v4_borde_negro_chico.png",
            "frame_v4_borde_v2.png", "metal_gold.png", "level_star.png", "vignette.png",
        })
            m.Add(new Move("Assets/Sprites/" + f, "Assets/Art/Sprites/CardFrames/" + f));

        // Fondos e iconos de interfaz.
        foreach (var f in new[]
        {
            "bg.png", "UI.png", "LibraryBG.png", "close.png", "info.png", "info_library.png",
            "curd_library.png", "curl_library.png", "Templo cibernético de Anubis.png",
            "ChatGPT Image 28 jun 2026, 08_54_54 p.m..png", "Sin título.jpg",
            "gemini-2.5-flash-image_Quiero_una_version_nueva_y_mejoradadel_back_de_mi_carta_la_tematica_es_egitpo_fu-0.jpg",
        })
            m.Add(new Move("Assets/Sprites/" + f, "Assets/Art/Sprites/UI/" + f));

        // Iconos de tipo/atributo: nadie los carga por ruta, así que salen de Resources.
        m.Add(new Move("Assets/Resources/Icons", "Assets/Art/Sprites/Icons"));

        // ── Art/Models: los FBX de Meshy y sus texturas vivían en Resources/Prefabs ──
        foreach (var f in new[]
        {
            "Meshy_AI_Neon_Eye_Citadel_0719213419_generate.fbx",
            "Meshy_AI_The_Molten_Ember_Egg_0701033819_texture.fbx",
            "Meshy_AI_The_Molten_Ember_Egg_0701033819_texture.png",
            "Meshy_AI_The_Molten_Ember_Egg_0701033819_texture_metallic.png",
            "Meshy_AI_The_Molten_Ember_Egg_0701033819_texture_normal.png",
            "Meshy_AI_The_Molten_Ember_Egg_0701033819_texture_roughness.png",
        })
            m.Add(new Move("Assets/Resources/Prefabs/" + f, "Assets/Art/Models/" + f));

        // ── Audio: se referencia desde DuelAudioBank por GUID, no por ruta ──
        m.Add(new Move("Assets/Resources/SoundEfects", "Assets/Audio/SFX"));
        m.Add(new Move("Assets/Resources/SoundTrack", "Assets/Audio/Music"));

        // ── Prefabs que NO se cargan por Resources.Load ──
        m.Add(new Move("Assets/Resources/Prefabs/Card.prefab", "Assets/Prefabs/Card.prefab"));
        m.Add(new Move("Assets/Resources/Prefabs/LibraryCardSlot.prefab", "Assets/Prefabs/LibraryCardSlot.prefab"));
        m.Add(new Move("Assets/Resources/Prefabs/WireframeStage.prefab", "Assets/Prefabs/WireframeStage.prefab"));
        m.Add(new Move("Assets/Resources/Prefabs/Monsters", "Assets/Prefabs/Monsters"));
        // CardMonsterV2.prefab se queda: CardDetailPanel hace Resources.Load("Prefabs/CardMonsterV2").

        // ── Data: ScriptableObjects de configuración (referenciados por GUID) ──
        m.Add(new Move("Assets/Scripts/CardIconConfig.asset", "Assets/Data/CardIconConfig.asset"));
        m.Add(new Move("Assets/Resources/Field/TerrainSpriteConfig.asset", "Assets/Data/TerrainSpriteConfig.asset"));
        m.Add(new Move("Assets/Resources/DuelOpponent/Heishin_DuelConfig.asset",
                       "Assets/Data/DuelConfigs/Heishin_DuelConfig.asset"));

        // ── Scripts: typos y un script suelto en la raíz de Assets ──
        m.Add(new Move("Assets/Scripts/Oponent", "Assets/Scripts/Opponent"));
        m.Add(new Move("Assets/Scripts/Test", "Assets/Scripts/Testing"));
        m.Add(new Move("Assets/Scripts/Library/Debug", "Assets/Scripts/Library/DebugTools"));
        m.Add(new Move("Assets/Scripts/Library.cs", "Assets/Scripts/Library/Library.cs"));
        // scr.cs declara 'class FindSpriteUsage': el nombre del archivo TIENE que coincidir
        // con el de la clase o Unity no deja usarlo como componente.
        m.Add(new Move("Assets/scr.cs", "Assets/Scripts/Testing/FindSpriteUsage.cs"));

        // ── Editor: por función ──
        foreach (var f in new[]
        {
            "CardMonsterV2PrefabBuilder.cs", "DeckBuilderBuilder.cs", "DeckBuilderSetup.cs",
            "DuelSceneBuilder.cs", "DuelSceneSetup.cs", "DuelAudioSetup.cs",
            "FreeDuelBuilder.cs", "FreeDuelSetup.cs", "LibraryGodsSceneBuilder.cs",
            "MainMenuBuilder.cs", "MainMenuSetup.cs", "StoryBuilder.cs", "StorySetup.cs",
            "TestGrantCardsSetup.cs",
        })
            m.Add(new Move("Assets/Editor/" + f, "Assets/Editor/Builders/" + f));

        m.Add(new Move("Assets/Editor/YuGiOhCardImporter.cs", "Assets/Editor/Importers/YuGiOhCardImporter.cs"));

        foreach (var f in new[] { "CardCatalogValidator.cs", "CardV2Migration.cs", "OpponentDeckFiller.cs" })
            m.Add(new Move("Assets/Editor/" + f, "Assets/Editor/Tools/" + f));

        return m;
    }

    /// <summary>
    /// Reparte las cartas hechas a mano en <c>Cards/Data/&lt;Categoría&gt;/&lt;Letra&gt;/</c>,
    /// igual que las importadas. Van por MoveAsset para no perder el GUID: los mazos de los
    /// oponentes las referencian.
    /// </summary>
    private static int MoveHandMadeCards(List<string> problems)
    {
        const string root = "Assets/Resources/Cards/Data";
        int moved = 0;

        // Solo las que cuelgan directamente de Data/ (las importadas ya están ordenadas).
        var loose = AssetDatabase.FindAssets("t:CardData", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => ParentOf(p) == root)
            .ToList();

        foreach (var path in loose)
        {
            var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card == null) continue;

            string dest = $"{root}/{card.cardCategory}/{BucketOf(card.cardName)}/{Path.GetFileName(path)}";
            if (AssetExists(dest)) { problems.Add("YA EXISTE el destino: " + dest); continue; }

            EnsureFolder(ParentOf(dest));
            string error = AssetDatabase.MoveAsset(path, dest);
            if (string.IsNullOrEmpty(error)) moved++;
            else problems.Add($"{path} → {dest}: {error}");
        }

        return moved;
    }

    /// <summary>Letra inicial de la carta; "#" si empieza por cifra o símbolo.</summary>
    public static string BucketOf(string name)
    {
        if (string.IsNullOrEmpty(name)) return "#";
        char c = char.ToUpperInvariant(name.FirstOrDefault(char.IsLetterOrDigit));
        return c >= 'A' && c <= 'Z' ? c.ToString() : "#";
    }

    /// <summary>Borra las carpetas que quedaron vacías tras mover su contenido.</summary>
    private static int DeleteEmptyLeftovers()
    {
        string[] candidates =
        {
            "Assets/Sprites", "Assets/Resources/Field", "Assets/Resources/DuelOpponent",
            "Assets/Resources/Icons", "Assets/YuGiOh",
        };

        int deleted = 0;
        foreach (var folder in candidates)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            if (Directory.EnumerateFileSystemEntries(folder).Any()) continue;
            if (AssetDatabase.DeleteAsset(folder)) deleted++;
        }
        if (deleted > 0) AssetDatabase.Refresh();
        return deleted;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Utilidades de ruta
    // ─────────────────────────────────────────────────────────────────────

    private static bool AssetExists(string path) =>
        AssetDatabase.IsValidFolder(path) || File.Exists(path);

    private static string ParentOf(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash < 0 ? "Assets" : path.Substring(0, slash);
    }

    /// <summary>Crea la carpeta y todas sus padres si hace falta.</summary>
    public static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
        string parent = ParentOf(path);
        if (parent != path) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(parent.Length + 1));
    }
}
