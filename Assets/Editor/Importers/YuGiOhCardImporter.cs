using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera un <see cref="CardData"/> por cada fila de <c>Assets/YuGiOh/cards.csv</c>,
/// enlazando su arte de <c>Assets/StreamingAssets/CardArt</c> (o <c>AlternateArt</c>).
///
/// Cómo encuentra el arte: NO reconstruye el nombre del archivo (los nombres del CSV
/// llevan comillas, comas y barras que el descargador saneó de otra forma). En vez de eso
/// indexa una sola vez todos los archivos por el <c>_&lt;image_id&gt;</c> final de su nombre,
/// que es estable. Lo que se guarda en la carta es la ruta relativa a StreamingAssets;
/// el sprite se decodifica bajo demanda (ver <see cref="CardArtLoader"/>), así que
/// importar 14.000 cartas no mete 2 GB de textura en memoria.
///
/// Mapeos y heurísticas viven todos en esta clase, en tablas separadas y fáciles de tocar:
/// categoría, tipo de monstruo, atributo, terreno de las magias de campo, Estrellas
/// Guardianas, rareza y efectos jugables. Reimportar es idempotente: por defecto salta las
/// cartas ya creadas, así que puedes cortar a medias y continuar.
///
/// Menú: YGO ▸ Cartas ▸ Importar set de Yu-Gi-Oh (CSV).
/// </summary>
public class YuGiOhCardImporter : EditorWindow
{
    // ── Rutas por defecto ────────────────────────────────────────────────
    // El CSV/JSON de origen vive FUERA de Assets: son datos de partida, no assets del
    // juego, y dentro de Assets Unity los importaría como TextAsset de 28 MB para nada.
    const string DefaultCsv = "CardSource/cards.csv";
    const string DefaultOut = "Assets/Resources/Cards/Data";
    const string CatalogRoot = "Assets/Resources/Cards/Data";
    const string StreamingRoot = "Assets/StreamingAssets";

    // ── Opciones (persistidas en EditorPrefs) ────────────────────────────
    const string PrefPrefix = "YGO.CardImporter.";
    string _csvPath = DefaultCsv;
    string _outPath = DefaultOut;
    int _maxCards;                    // 0 = sin límite
    bool _skipExisting = true;        // no re-crear cartas ya importadas
    bool _skipCatalogNames = true;    // no duplicar las cartas hechas a mano
    bool _requireArt;                 // saltar filas sin imagen
    bool _includeSkills = true;
    bool _includeTokens = true;

    Vector2 _scroll;
    string _lastReport = "";

    [MenuItem("YGO/Cartas/Importar set de Yu-Gi-Oh (CSV)")]
    public static void Open()
    {
        var w = GetWindow<YuGiOhCardImporter>(true, "Importar cartas de Yu-Gi-Oh");
        w.minSize = new Vector2(520, 420);
    }

    void OnEnable()
    {
        _csvPath = EditorPrefs.GetString(PrefPrefix + "csv", DefaultCsv);
        _outPath = EditorPrefs.GetString(PrefPrefix + "out", DefaultOut);
        _maxCards = EditorPrefs.GetInt(PrefPrefix + "max", 0);
        _skipExisting = EditorPrefs.GetBool(PrefPrefix + "skipExisting", true);
        _skipCatalogNames = EditorPrefs.GetBool(PrefPrefix + "skipCatalog", true);
        _requireArt = EditorPrefs.GetBool(PrefPrefix + "requireArt", false);
        _includeSkills = EditorPrefs.GetBool(PrefPrefix + "skills", true);
        _includeTokens = EditorPrefs.GetBool(PrefPrefix + "tokens", true);
    }

    /// <summary>
    /// Importa el set completo sin abrir la ventana, con las opciones por defecto
    /// (todas las cartas, saltando las ya creadas y las que chocan con tus cartas
    /// hechas a mano). Pensado para el menú y para <c>-executeMethod</c> en batchmode.
    /// </summary>
    [MenuItem("YGO/Cartas/Importar set de Yu-Gi-Oh (todo, sin ventana)")]
    public static void BatchImport() => RunHeadless(dryRun: false);

    [MenuItem("YGO/Cartas/Analizar set de Yu-Gi-Oh (sin escribir)")]
    public static void BatchAnalyze() => RunHeadless(dryRun: true);

    static void RunHeadless(bool dryRun)
    {
        var w = CreateInstance<YuGiOhCardImporter>();   // OnEnable carga los prefs
        try
        {
            w._csvPath = DefaultCsv;
            w._outPath = DefaultOut;
            w._maxCards = 0;
            w._skipExisting = true;
            w._skipCatalogNames = true;
            w._requireArt = false;
            w._includeSkills = true;
            w._includeTokens = true;
            w.Run(dryRun);
        }
        finally { DestroyImmediate(w); }
    }

    void SavePrefs()
    {
        EditorPrefs.SetString(PrefPrefix + "csv", _csvPath);
        EditorPrefs.SetString(PrefPrefix + "out", _outPath);
        EditorPrefs.SetInt(PrefPrefix + "max", _maxCards);
        EditorPrefs.SetBool(PrefPrefix + "skipExisting", _skipExisting);
        EditorPrefs.SetBool(PrefPrefix + "skipCatalog", _skipCatalogNames);
        EditorPrefs.SetBool(PrefPrefix + "requireArt", _requireArt);
        EditorPrefs.SetBool(PrefPrefix + "skills", _includeSkills);
        EditorPrefs.SetBool(PrefPrefix + "tokens", _includeTokens);
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("Origen", EditorStyles.boldLabel);
        _csvPath = EditorGUILayout.TextField("CSV", _csvPath);
        EditorGUILayout.LabelField("Arte", StreamingRoot + "/CardArt (+ AlternateArt)");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Destino", EditorStyles.boldLabel);
        _outPath = EditorGUILayout.TextField("Carpeta", _outPath);
        EditorGUILayout.HelpBox(
            "Las cartas se crean en <Categoría>/<Inicial>/. Debe colgar de una carpeta " +
            "Resources: LibraryCatalog hace Resources.LoadAll<CardData>(\"Cards/Data\") " +
            "y recorre las subcarpetas.",
            MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Opciones", EditorStyles.boldLabel);
        _maxCards = EditorGUILayout.IntField(
            new GUIContent("Máximo de cartas", "0 = todas. Útil para probar con un lote."), _maxCards);
        _skipExisting = EditorGUILayout.Toggle(
            new GUIContent("Saltar ya importadas", "Permite continuar una importación cortada."), _skipExisting);
        _skipCatalogNames = EditorGUILayout.Toggle(
            new GUIContent("No duplicar cartas propias", "Salta los nombres que ya existen en " + CatalogRoot + "."), _skipCatalogNames);
        _requireArt = EditorGUILayout.Toggle(
            new GUIContent("Solo con arte", "Salta las filas cuya imagen no esté en StreamingAssets."), _requireArt);
        _includeSkills = EditorGUILayout.Toggle("Incluir Skill Cards", _includeSkills);
        _includeTokens = EditorGUILayout.Toggle("Incluir Tokens", _includeTokens);

        EditorGUILayout.Space();
        if (GUILayout.Button("Analizar (sin escribir nada)", GUILayout.Height(24)))
        {
            SavePrefs();
            Run(dryRun: true);
        }
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button("Importar", GUILayout.Height(32)))
        {
            SavePrefs();
            Run(dryRun: false);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        if (GUILayout.Button("Borrar cartas importadas"))
        {
            if (EditorUtility.DisplayDialog("Borrar importadas",
                    "Se eliminarán las cartas cuyo id aparezca en el CSV.\n" +
                    "Las hechas a mano NO se tocan.",
                    "Borrar", "Cancelar"))
                DeleteImported();
        }

        if (!string.IsNullOrEmpty(_lastReport))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resultado", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_lastReport, MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Importación
    // ─────────────────────────────────────────────────────────────────────

    void Run(bool dryRun)
    {
        if (!File.Exists(_csvPath))
        {
            EditorUtility.DisplayDialog("Falta el CSV", $"No encuentro {_csvPath}.", "Vale");
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        EditorUtility.DisplayProgressBar("Importando cartas", "Leyendo CSV…", 0f);

        List<string[]> rows;
        try
        {
            rows = ParseCsv(File.ReadAllText(_csvPath, Encoding.UTF8));
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Error leyendo el CSV", e.Message, "Vale");
            return;
        }

        if (rows.Count < 2)
        {
            EditorUtility.ClearProgressBar();
            _lastReport = "El CSV no tiene filas de datos.";
            return;
        }

        var col = HeaderIndex(rows[0]);
        string[] required = { "card_id", "image_id", "name", "type", "race", "attribute",
                              "level", "atk", "def", "archetype", "folder", "description" };
        var missingCols = required.Where(c => !col.ContainsKey(c)).ToArray();
        if (missingCols.Length > 0)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Cabecera inesperada",
                "Faltan columnas en el CSV: " + string.Join(", ", missingCols), "Vale");
            return;
        }

        EditorUtility.DisplayProgressBar("Importando cartas", "Indexando el arte…", 0.02f);
        var artIndex = BuildArtIndex();

        var csvIds = CsvCardIds(rows, col);
        var reservedNames = _skipCatalogNames ? HandMadeCardNames(csvIds) : new HashSet<string>();
        var usedIds = ExistingCardIds();   // los ids ya ocupados por el catálogo

        int created = 0, skippedExisting = 0, skippedNoArt = 0, skippedFiltered = 0,
            skippedDuplicateName = 0, skippedDuplicateId = 0, withoutArt = 0;
        var categoryCount = new Dictionary<CardCategory, int>();
        var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!dryRun)
        {
            EnsureFolder(_outPath);
            foreach (CardCategory cat in Enum.GetValues(typeof(CardCategory)))
                foreach (var bucket in BucketNames())
                    EnsureFolder($"{_outPath}/{cat}/{bucket}");
            AssetDatabase.StartAssetEditing();
        }

        bool canceled = false;
        try
        {
            for (int i = 1; i < rows.Count; i++)
            {
                if (_maxCards > 0 && created >= _maxCards) break;

                if ((i & 63) == 0)
                {
                    float p = 0.05f + 0.95f * i / rows.Count;
                    if (EditorUtility.DisplayCancelableProgressBar(
                            dryRun ? "Analizando cartas" : "Importando cartas",
                            $"{created} creadas — fila {i}/{rows.Count - 1}", p))
                    {
                        canceled = true;
                        break;
                    }
                }

                var r = rows[i];
                if (r.Length < required.Length) continue;

                string type = Field(r, col, "type");
                string name = CleanText(Field(r, col, "name"));
                if (string.IsNullOrEmpty(name)) continue;

                if (!_includeSkills && type.IndexOf("Skill", StringComparison.OrdinalIgnoreCase) >= 0)
                { skippedFiltered++; continue; }
                if (!_includeTokens && type.Equals("Token", StringComparison.OrdinalIgnoreCase))
                { skippedFiltered++; continue; }

                if (reservedNames.Contains(name)) { skippedDuplicateName++; continue; }

                // OJO: card_id se repite en los alternate art (124 casos), y
                // LibraryCatalog indexa por cardId con ToDictionary — un id repetido
                // reventaría TODA la biblioteca. image_id sí es único en las 14.642
                // filas (y coincide con card_id en el arte base), así que ese es el id.
                int passcode = ParseInt(Field(r, col, "card_id"));
                int imageId = ParseInt(Field(r, col, "image_id"));
                int cardId = imageId != 0 ? imageId : passcode;
                if (cardId == 0 || !usedIds.Add(cardId)) { skippedDuplicateId++; continue; }

                artIndex.TryGetValue(cardId, out string artFile);
                if (string.IsNullOrEmpty(artFile))
                {
                    if (_requireArt) { skippedNoArt++; continue; }
                    withoutArt++;
                }

                // Carpeta por categoría y, dentro, por inicial: 14.000 assets en un solo
                // directorio hacen inusable la ventana de Proyecto.
                string race = Field(r, col, "race");
                string folder = $"{_outPath}/{ToCategory(type, race)}/{BucketOf(name)}";
                string fileName = AssetFileName(name, cardId);
                string assetPath = $"{folder}/{fileName}.asset";

                // Dos filas pueden sanear al mismo nombre; desempata con el id de imagen.
                if (!usedPaths.Add(assetPath))
                {
                    assetPath = $"{folder}/{fileName}_{imageId}.asset";
                    usedPaths.Add(assetPath);
                }

                if (_skipExisting && File.Exists(assetPath)) { skippedExisting++; continue; }

                var card = BuildCard(r, col, name, cardId, artFile);
                categoryCount.TryGetValue(card.cardCategory, out int n);
                categoryCount[card.cardCategory] = n + 1;

                if (dryRun) DestroyImmediate(card);
                else AssetDatabase.CreateAsset(card, assetPath);

                created++;
            }
        }
        finally
        {
            if (!dryRun) AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        if (!dryRun)
        {
            EditorUtility.DisplayProgressBar("Importando cartas", "Refrescando AssetDatabase…", 0.99f);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }

        sw.Stop();
        var sb = new StringBuilder();
        sb.AppendLine(dryRun ? "ANÁLISIS (no se escribió nada)" : "IMPORTACIÓN COMPLETADA");
        if (canceled) sb.AppendLine("⚠ Cancelada: vuelve a ejecutar para continuar donde se quedó.");
        sb.AppendLine($"Cartas {(dryRun ? "que se crearían" : "creadas")}: {created}");
        foreach (var kv in categoryCount.OrderByDescending(k => k.Value))
            sb.AppendLine($"   · {kv.Key}: {kv.Value}");
        if (withoutArt > 0) sb.AppendLine($"Sin imagen (creadas igual): {withoutArt}");
        if (skippedExisting > 0) sb.AppendLine($"Saltadas por existir ya: {skippedExisting}");
        if (skippedDuplicateName > 0) sb.AppendLine($"Saltadas por chocar con carta propia: {skippedDuplicateName}");
        if (skippedDuplicateId > 0) sb.AppendLine($"Saltadas por id repetido: {skippedDuplicateId}");
        if (skippedNoArt > 0) sb.AppendLine($"Saltadas por no tener arte: {skippedNoArt}");
        if (skippedFiltered > 0) sb.AppendLine($"Saltadas por filtro de tipo: {skippedFiltered}");
        sb.AppendLine($"Tiempo: {sw.Elapsed.TotalSeconds:0.0} s");
        _lastReport = sb.ToString();
        Debug.Log("YuGiOhCardImporter\n" + _lastReport);
        Repaint();
    }

    /// <summary>Rellena una carta nueva a partir de una fila del CSV.</summary>
    static CardData BuildCard(string[] r, Dictionary<string, int> col, string name, int cardId, string artFile)
    {
        var card = CreateInstance<CardData>();
        card.cardId = cardId;
        card.cardName = name;
        card.artFile = artFile ?? "";
        card.artwork = null;

        string type = Field(r, col, "type");
        string race = Field(r, col, "race");
        string attribute = Field(r, col, "attribute");
        string desc = CleanText(Field(r, col, "description"));
        int level = ParseInt(Field(r, col, "level"));
        int atk = ParseInt(Field(r, col, "atk"));
        int def = ParseInt(Field(r, col, "def"));

        card.description = desc;
        card.fusionGroup = CleanText(Field(r, col, "archetype"));
        card.attribute = ToAttribute(attribute);
        card.cardCategory = ToCategory(type, race);

        switch (card.cardCategory)
        {
            case CardCategory.Monster:
                card.monsterType = ToMonsterType(race);
                card.baseAtk = atk;
                card.baseDef = def;
                card.stars = Mathf.Clamp(level, 0, 12);
                (card.starA, card.starB) = GuardianStarsFor(card.attribute, card.monsterType);
                card.favoriteTerrain = FavoriteTerrainFor(card.attribute, card.monsterType);
                break;

            case CardCategory.Spell:
                card.spellKind = race.Equals("Field", StringComparison.OrdinalIgnoreCase)
                    ? SpellKind.Field : SpellKind.Normal;
                if (card.spellKind == SpellKind.Field)
                    card.fieldTerrain = TerrainFromName(name, desc);
                else
                    (card.spellEffect, card.spellValue) = SpellEffectFor(desc);
                break;

            case CardCategory.Equip:
                (card.equipAtkBonus, card.equipDefBonus) = EquipBonusFor(desc);
                var restrict = EquipRestrictionFor(desc);
                if (restrict.HasValue)
                {
                    card.equipRestrictToType = true;
                    card.equipMonsterType = restrict.Value;
                }
                break;

            case CardCategory.Trap:
                card.trapKind = race.Equals("Continuous", StringComparison.OrdinalIgnoreCase) ? TrapKind.Continuous
                              : race.Equals("Counter", StringComparison.OrdinalIgnoreCase) ? TrapKind.Counter
                              : TrapKind.Normal;
                var t = TrapEffectFor(desc, card.trapKind);
                card.trapEffect = t.effect;
                card.trapValue = t.value;
                card.trapTrigger = t.trigger;
                card.resolutionPriority = card.trapKind == TrapKind.Counter ? 10 : 0;
                break;

            case CardCategory.Ritual:
                // Magia de Ritual: materiales y resultado se enlazan a mano (o con otra
                // pasada que lea cards.json, donde vienen los nombres de los materiales).
                break;
        }

        card.rarity = RarityFor(card, type);
        return card;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Índice de arte
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>image_id → ruta relativa a StreamingAssets ("CardArt/Nombre_123.jpg").</summary>
    static Dictionary<int, string> BuildArtIndex()
    {
        var index = new Dictionary<int, string>();
        foreach (var folder in new[] { CardArtLoader.CardArtFolder, CardArtLoader.AlternateArtFolder })
        {
            string dir = Path.Combine(StreamingRoot, folder);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir))
            {
                string ext = Path.GetExtension(file);
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png") continue;

                string stem = Path.GetFileNameWithoutExtension(file);
                int us = stem.LastIndexOf('_');
                if (us < 0 || !int.TryParse(stem.Substring(us + 1), out int id)) continue;

                // CardArt manda: AlternateArt solo cubre ids que no estén ya.
                if (!index.ContainsKey(id))
                    index[id] = folder + "/" + Path.GetFileName(file);
            }
        }
        return index;
    }

    /// <summary>
    /// Ids ya usados en TODO el catálogo (propias + importadas de una pasada anterior).
    /// Evita que dos cartas compartan cardId, cosa que rompería LibraryCatalog.
    /// </summary>
    static HashSet<int> ExistingCardIds()
    {
        var ids = new HashSet<int>();
        foreach (var guid in AssetDatabase.FindAssets("t:CardData", new[] { CatalogRoot }))
        {
            var c = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null) ids.Add(c.cardId);
        }
        return ids;
    }

    /// <summary>
    /// Nombres de las cartas hechas a mano. Ya no se distinguen por carpeta (ahora todas
    /// se ordenan juntas por categoría), sino por id: una carta del catálogo cuyo cardId
    /// no aparece en el CSV no la generó este importador.
    /// </summary>
    static HashSet<string> HandMadeCardNames(HashSet<int> csvIds)
    {
        var names = new HashSet<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:CardData", new[] { CatalogRoot }))
        {
            var c = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
            if (c == null || string.IsNullOrEmpty(c.cardName)) continue;
            if (csvIds.Contains(c.cardId)) continue;      // generada por el importador
            names.Add(c.cardName);
        }
        return names;
    }

    /// <summary>
    /// Borra solo las cartas generadas por el importador: las que tienen un id presente en
    /// el CSV. Nunca toca las hechas a mano, que ahora viven en las mismas carpetas.
    /// </summary>
    void DeleteImported()
    {
        if (!File.Exists(_csvPath))
        {
            EditorUtility.DisplayDialog("Falta el CSV",
                $"No encuentro {_csvPath}; sin él no puedo saber cuáles son importadas.", "Vale");
            return;
        }

        var rows = ParseCsv(File.ReadAllText(_csvPath, Encoding.UTF8));
        if (rows.Count < 2) return;
        var csvIds = CsvCardIds(rows, HeaderIndex(rows[0]));

        var toDelete = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:CardData", new[] { CatalogRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var c = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (c != null && csvIds.Contains(c.cardId)) toDelete.Add(path);
        }

        var failed = new List<string>();
        AssetDatabase.DeleteAssets(toDelete.ToArray(), failed);
        AssetDatabase.Refresh();

        _lastReport = $"Eliminadas {toDelete.Count - failed.Count} cartas importadas" +
                      (failed.Count > 0 ? $" ({failed.Count} fallaron)." : ".");
        Debug.Log("YuGiOhCardImporter: " + _lastReport);
    }

    /// <summary>Todos los ids que produce el CSV (image_id, con card_id de respaldo).</summary>
    static HashSet<int> CsvCardIds(List<string[]> rows, Dictionary<string, int> col)
    {
        var ids = new HashSet<int>();
        for (int i = 1; i < rows.Count; i++)
        {
            if (rows[i].Length < 3) continue;
            int imageId = ParseInt(Field(rows[i], col, "image_id"));
            int passcode = ParseInt(Field(rows[i], col, "card_id"));
            int id = imageId != 0 ? imageId : passcode;
            if (id != 0) ids.Add(id);
        }
        return ids;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Mapeos CSV → enums del proyecto
    // ─────────────────────────────────────────────────────────────────────

    static CardCategory ToCategory(string type, string race)
    {
        if (type.IndexOf("Skill", StringComparison.OrdinalIgnoreCase) >= 0)
            return CardCategory.Special;

        if (type.IndexOf("Spell", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (race.Equals("Equip", StringComparison.OrdinalIgnoreCase)) return CardCategory.Equip;
            if (race.Equals("Ritual", StringComparison.OrdinalIgnoreCase)) return CardCategory.Ritual;
            return CardCategory.Spell;   // Normal, Continuous, Quick-Play, Field
        }

        if (type.IndexOf("Trap", StringComparison.OrdinalIgnoreCase) >= 0)
            return CardCategory.Trap;

        // Todo lo demás es monstruo: Normal/Effect/Fusion/Ritual/Synchro/XYZ/Link/
        // Pendulum/Tuner/Spirit/Gemini/Union/Toon/Token.
        return CardCategory.Monster;
    }

    static readonly Dictionary<string, MonsterType> RaceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Dragon", MonsterType.Dragon },             { "Spellcaster", MonsterType.Spellcaster },
        { "Fiend", MonsterType.Fiend },               { "Beast", MonsterType.Beast },
        { "Insect", MonsterType.Insect },             { "Plant", MonsterType.Plant },
        { "Fish", MonsterType.Fish },                 { "Aqua", MonsterType.Aqua },
        { "Sea Serpent", MonsterType.SeaSerpent },    { "Zombie", MonsterType.Zombie },
        { "Dinosaur", MonsterType.Dinosaur },         { "Winged Beast", MonsterType.WingedBeast },
        { "Warrior", MonsterType.Warrior },           { "Machine", MonsterType.Machine },
        { "Thunder", MonsterType.Thunder },           { "Fairy", MonsterType.Fairy },
        { "Reptile", MonsterType.Reptile },           { "Rock", MonsterType.Rock },
        { "Pyro", MonsterType.Pyro },                 { "Beast-Warrior", MonsterType.BeastWarrior },
        { "Psychic", MonsterType.Psychic },           { "Wyrm", MonsterType.Wyrm },
        { "Cyberse", MonsterType.Cyberse },           { "Divine-Beast", MonsterType.DivineBeast },
        { "Illusion", MonsterType.Illusion },         { "Creator God", MonsterType.CreatorGod },
    };

    static MonsterType ToMonsterType(string race) =>
        RaceMap.TryGetValue(race ?? "", out var t) ? t : MonsterType.Unknown;

    static CardAttribute ToAttribute(string attr) => (attr ?? "").ToUpperInvariant() switch
    {
        "DARK" => CardAttribute.Dark,
        "LIGHT" => CardAttribute.Light,
        "FIRE" => CardAttribute.Fire,
        "WATER" => CardAttribute.Water,
        "EARTH" => CardAttribute.Earth,
        "WIND" => CardAttribute.Wind,
        "DIVINE" => CardAttribute.Divine,
        _ => CardAttribute.None
    };

    /// <summary>
    /// Estrellas Guardianas deterministas: la A sale del atributo y la B del tipo, así
    /// que reimportar siempre da el mismo resultado. Si coinciden, la B avanza una
    /// posición en la rueda (el orden del enum es la rueda de ventajas).
    /// </summary>
    static (GuardianStar, GuardianStar) GuardianStarsFor(CardAttribute attr, MonsterType type)
    {
        GuardianStar a = attr switch
        {
            CardAttribute.Light => GuardianStar.Sun,
            CardAttribute.Dark => GuardianStar.Moon,
            CardAttribute.Fire => GuardianStar.Mars,
            CardAttribute.Water => GuardianStar.Neptune,
            CardAttribute.Wind => GuardianStar.Jupiter,
            CardAttribute.Earth => GuardianStar.Uranus,
            CardAttribute.Divine => GuardianStar.Sun,
            _ => GuardianStar.Mercury
        };

        GuardianStar b = type switch
        {
            MonsterType.Dragon or MonsterType.Wyrm => GuardianStar.Mars,
            MonsterType.Spellcaster or MonsterType.Illusion => GuardianStar.Mercury,
            MonsterType.Fiend or MonsterType.Zombie => GuardianStar.Pluto,
            MonsterType.Beast or MonsterType.BeastWarrior => GuardianStar.Moon,
            MonsterType.Insect or MonsterType.Plant => GuardianStar.Venus,
            MonsterType.Fish or MonsterType.Aqua or MonsterType.SeaSerpent => GuardianStar.Neptune,
            MonsterType.Dinosaur or MonsterType.Rock => GuardianStar.Saturn,
            MonsterType.WingedBeast => GuardianStar.Jupiter,
            MonsterType.Warrior => GuardianStar.Mars,
            MonsterType.Machine or MonsterType.Cyberse => GuardianStar.Uranus,
            MonsterType.Thunder => GuardianStar.Jupiter,
            MonsterType.Fairy or MonsterType.DivineBeast or MonsterType.CreatorGod => GuardianStar.Sun,
            MonsterType.Reptile or MonsterType.Psychic => GuardianStar.Saturn,
            MonsterType.Pyro => GuardianStar.Mars,
            _ => GuardianStar.Venus
        };

        if (a == b)
        {
            int wheel = Enum.GetValues(typeof(GuardianStar)).Length;
            b = (GuardianStar)(((int)b + 1) % wheel);
        }
        return (a, b);
    }

    static TerrainType FavoriteTerrainFor(CardAttribute attr, MonsterType type) => type switch
    {
        MonsterType.Fish or MonsterType.Aqua or MonsterType.SeaSerpent => TerrainType.Sea,
        MonsterType.Plant or MonsterType.Insect => TerrainType.Forest,
        MonsterType.Dragon or MonsterType.WingedBeast or MonsterType.Thunder => TerrainType.Mountain,
        MonsterType.Fiend or MonsterType.Zombie => TerrainType.Yami,
        MonsterType.Fairy or MonsterType.Spellcaster => TerrainType.Meadow,
        MonsterType.Machine or MonsterType.Rock or MonsterType.Dinosaur or MonsterType.Pyro => TerrainType.Wasteland,
        _ => TerrainType.Neutral
    };

    /// <summary>Terreno de una magia de campo, deducido de su nombre (y del texto).</summary>
    static TerrainType TerrainFromName(string name, string desc)
    {
        string s = (name + " " + desc).ToLowerInvariant();
        if (Has(s, "umi", "ocean", "sea", "water", "aqua", "wetlands")) return TerrainType.Sea;
        if (Has(s, "forest", "jungle", "wood", "sogen")) return TerrainType.Forest;
        if (Has(s, "mountain", "canyon", "peak", "sky", "cloud")) return TerrainType.Mountain;
        if (Has(s, "yami", "darkness", "shadow", "dark world")) return TerrainType.Yami;
        if (Has(s, "wasteland", "desert", "ruins", "barren")) return TerrainType.Wasteland;
        if (Has(s, "meadow", "plain", "field of", "grass")) return TerrainType.Meadow;
        return TerrainType.Neutral;
    }

    static bool Has(string haystack, params string[] needles) =>
        needles.Any(n => haystack.Contains(n));

    static CardRarity RarityFor(CardData c, string type)
    {
        bool fancy = type.IndexOf("Fusion", StringComparison.OrdinalIgnoreCase) >= 0
                  || type.IndexOf("Ritual", StringComparison.OrdinalIgnoreCase) >= 0
                  || type.IndexOf("Synchro", StringComparison.OrdinalIgnoreCase) >= 0
                  || type.IndexOf("XYZ", StringComparison.OrdinalIgnoreCase) >= 0
                  || type.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0;

        if (c.IsMonster)
        {
            if (c.attribute == CardAttribute.Divine || c.baseAtk >= 3000 || c.stars >= 10) return CardRarity.Legendary;
            if (fancy || c.baseAtk >= 2400 || c.stars >= 8) return CardRarity.Epic;
            if (c.baseAtk >= 1700 || c.stars >= 6) return CardRarity.Rare;
            return CardRarity.Common;
        }

        if (c.IsTrap && c.trapKind == TrapKind.Counter) return CardRarity.Rare;
        if (c.IsRitual || c.IsFieldSpell) return CardRarity.Rare;
        if (c.IsSpell && c.spellEffect != SpellEffectType.None) return CardRarity.Rare;
        if (c.IsTrap && c.trapEffect != TrapEffectType.None) return CardRarity.Rare;
        return CardRarity.Common;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Heurística de efectos (texto real de Yu-Gi-Oh → enums del duelo)
    //  Solo cubre los patrones frecuentes; lo que no encaja queda en None y
    //  la carta se queda con su descripción (no rompe nada en el duelo).
    // ─────────────────────────────────────────────────────────────────────

    // Cada efecto lleva la redacción moderna Y la clásica: las cartas de la época de
    // Forbidden Memories dicen "increases its ATK by 300 points" donde las nuevas dicen
    // "gains 300 ATK", y son justo las que interesan aquí.
    // Ojo: el "gain" tiene que exigir Life Points, o "gain 500 ATK" se colaría como cura.
    static readonly Regex RxGainLP = new(
        @"(?:gain (\d[\d,]*) (?:Life Points|LPs?)\b|increases? your Life Points by (\d[\d,]*))",
        RegexOptions.IgnoreCase);
    static readonly Regex RxDamage = new(
        @"inflicts? (\d[\d,]*) (?:points of )?damage", RegexOptions.IgnoreCase);
    static readonly Regex RxGainAtk = new(
        @"(?:gain (\d[\d,]*) ATK|increases? the ATK of .*? by (\d[\d,]*))", RegexOptions.IgnoreCase);
    static readonly Regex RxLoseAtk = new(
        @"(?:lose (\d[\d,]*) ATK|decreases? the ATK of .*? by (\d[\d,]*))", RegexOptions.IgnoreCase);

    static readonly Regex RxEquipAtkDef = new(
        @"(?:gains? (\d[\d,]*) ATK and (?:(\d[\d,]*) )?DEF|increases? its ATK and DEF by (\d[\d,]*))",
        RegexOptions.IgnoreCase);
    static readonly Regex RxEquipAtk = new(
        @"(?:gains? (\d[\d,]*) ATK|increases? its ATK by (\d[\d,]*))", RegexOptions.IgnoreCase);
    static readonly Regex RxEquipDef = new(
        @"(?:gains? (\d[\d,]*) DEF|increases? its DEF by (\d[\d,]*))", RegexOptions.IgnoreCase);

    /// <summary>Primer grupo con valor de un match con alternativas.</summary>
    static int FirstNum(Match m)
    {
        for (int g = 1; g < m.Groups.Count; g++)
            if (m.Groups[g].Success && m.Groups[g].Value.Length > 0)
                return Num(m.Groups[g].Value);
        return 0;
    }

    static (SpellEffectType, int) SpellEffectFor(string desc)
    {
        string d = desc ?? "";
        string low = d.ToLowerInvariant();

        if (low.Contains("destroy all monsters your opponent controls") ||
            low.Contains("destroy all face-up monsters your opponent controls") ||
            low.Contains("destroy all monsters on the field"))
            return (SpellEffectType.DestroyAllEnemyMonsters, 0);

        var m = RxGainLP.Match(d);
        if (m.Success) return (SpellEffectType.HealLP, FirstNum(m));

        m = RxDamage.Match(d);
        if (m.Success) return (SpellEffectType.DamageOpponentLP, FirstNum(m));

        m = RxGainAtk.Match(d);
        if (m.Success && (low.Contains("monsters you control") || low.Contains("all monsters")))
            return (SpellEffectType.BuffAtkAllMonsters, FirstNum(m));

        if (low.Contains("destroy that target") || low.Contains("destroy it") ||
            Regex.IsMatch(low, @"destroy 1 .*monster"))
            return (SpellEffectType.DestroyWeakestEnemyMonster, 0);

        return (SpellEffectType.None, 0);
    }

    static (TrapEffectType effect, int value, TrapTrigger trigger) TrapEffectFor(string desc, TrapKind kind)
    {
        string d = desc ?? "";
        string low = d.ToLowerInvariant();

        if (low.Contains("negate the activation") && low.Contains("spell"))
            return (TrapEffectType.NegateSpell, 0, TrapTrigger.SpellActivated);
        if (low.Contains("negate the activation") && low.Contains("trap"))
            return (TrapEffectType.NegateTrap, 0, TrapTrigger.Custom);
        if (low.Contains("negate the summon"))
            return (TrapEffectType.NegateSummon, 0, TrapTrigger.MonsterSummoned);

        if (low.Contains("destroy all attack position monsters") ||
            low.Contains("destroy all your opponent's attack position monsters"))
            return (TrapEffectType.DestroyAllAttackingMonsters, 0, TrapTrigger.MonsterDeclaresAttack);

        if (low.Contains("destroy the attacking monster") || low.Contains("destroy that attacking monster"))
            return (TrapEffectType.DestroyAttackingMonster, 0, TrapTrigger.MonsterDeclaresAttack);

        if (low.Contains("negate the attack"))
            return (TrapEffectType.NegateAttack, 0, TrapTrigger.MonsterDeclaresAttack);

        if ((low.Contains("normal summon") || low.Contains("special summon")) && low.Contains("destroy"))
            return (TrapEffectType.DestroySummonedMonster, 0, TrapTrigger.MonsterSummoned);

        var m = RxDamage.Match(d);
        if (m.Success)
            return (TrapEffectType.DamageOpponent, FirstNum(m), TrapTrigger.PlayerTakesDamage);

        if (Regex.IsMatch(low, @"destroy 1 .*spell"))
            return (TrapEffectType.DestroyOneSpell, 0, TrapTrigger.SpellActivated);

        if (kind == TrapKind.Continuous)
        {
            m = RxLoseAtk.Match(d);
            if (m.Success && low.Contains("opponent"))
                return (TrapEffectType.ReduceEnemyAtk, FirstNum(m), TrapTrigger.Custom);
            if (low.Contains("cannot attack directly"))
                return (TrapEffectType.PreventDirectAttacks, 0, TrapTrigger.Custom);
            if (low.Contains("cannot change") && low.Contains("position"))
                return (TrapEffectType.LockPositionChanges, 0, TrapTrigger.MonsterChangesPosition);
        }

        return (TrapEffectType.None, 0, TrapTrigger.MonsterDeclaresAttack);
    }

    static (int atk, int def) EquipBonusFor(string desc)
    {
        string d = desc ?? "";

        // "gains 400 ATK and 200 DEF" da dos cifras; "…and DEF" o la redacción clásica
        // "increases its ATK and DEF by 300 points" dan una sola, que aplica a ambos.
        var m = RxEquipAtkDef.Match(d);
        if (m.Success)
        {
            int a = Num(m.Groups[1].Value);
            if (a == 0) { int both = FirstNum(m); return (both, both); }
            int b = m.Groups[2].Success && m.Groups[2].Value.Length > 0 ? Num(m.Groups[2].Value) : a;
            return (a, b);
        }

        int atk = 0, def = 0;
        m = RxEquipAtk.Match(d); if (m.Success) atk = FirstNum(m);
        m = RxEquipDef.Match(d); if (m.Success) def = FirstNum(m);
        return (atk, def);
    }

    /// <summary>
    /// Restricción de tipo del equipo, en las dos redacciones:
    /// "Equip only to a Dragon monster" y la clásica
    /// "A Beast-Type monster equipped with this card…".
    /// </summary>
    static MonsterType? EquipRestrictionFor(string desc)
    {
        string d = desc ?? "";

        var m = Regex.Match(d, @"[Ee]quip only to (?:a |an )?([A-Za-z\- ]+?)(?:-Type)? monster");
        if (!m.Success)
            m = Regex.Match(d, @"\b(?:A|An) ([A-Za-z\- ]+?)-Type monster equipped with this card");
        if (!m.Success) return null;

        string race = m.Groups[1].Value.Trim();
        return RaceMap.TryGetValue(race, out var t) ? t : (MonsterType?)null;
    }

    static int Num(string s) =>
        int.TryParse(s.Replace(",", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

    // ─────────────────────────────────────────────────────────────────────
    //  CSV
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parser CSV mínimo pero correcto (RFC-4180): comillas dobles, comas y saltos de
    /// línea dentro de campo, y "" como comilla escapada.
    /// </summary>
    static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(field.ToString()); field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row.ToArray()); row.Clear();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }
        return rows;
    }

    /// <summary>BOM UTF-8; el volcado lo trae pegado al primer nombre de columna.</summary>
    const char Bom = (char)0xFEFF;

    static Dictionary<string, int> HeaderIndex(string[] header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Length; i++)
        {
            // El archivo viene con BOM UTF-8 pegado a la primera columna.
            string key = header[i].Trim().Trim(Bom);
            if (!map.ContainsKey(key)) map[key] = i;
        }
        return map;
    }

    static string Field(string[] row, Dictionary<string, int> col, string name) =>
        col.TryGetValue(name, out int i) && i < row.Length ? row[i].Trim() : "";

    static int ParseInt(string s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

    /// <summary>
    /// Arregla los dos artefactos de comillas del volcado: las dobles duplicadas
    /// (<c>""X""</c>) y el texto de ambientación de los monstruos normales, que viene
    /// envuelto en dobles apóstrofos (<c>''X''</c>) donde el original lleva comillas.
    /// </summary>
    static string CleanText(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\"\"", "\"").Trim();
        if (s.Length > 4 && s.StartsWith("''") && s.EndsWith("''"))
            s = "\"" + s.Substring(2, s.Length - 4).Trim() + "\"";
        return s;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Rutas de asset
    // ─────────────────────────────────────────────────────────────────────

    static readonly char[] InvalidFileChars = Path.GetInvalidFileNameChars();

    static string AssetFileName(string name, int cardId)
    {
        var sb = new StringBuilder(name.Length + 12);
        foreach (char c in name)
            sb.Append(Array.IndexOf(InvalidFileChars, c) >= 0 || c == '"' || c == '.' ? '_' : c);

        string clean = sb.ToString().Trim();
        if (clean.Length > 60) clean = clean.Substring(0, 60).Trim();
        if (clean.Length == 0) clean = "Card";
        return $"{clean}_{cardId}";
    }

    static IEnumerable<string> BucketNames()
    {
        yield return "#";
        for (char c = 'A'; c <= 'Z'; c++) yield return c.ToString();
    }

    static string BucketOf(string name)
    {
        char c = char.ToUpperInvariant(name.FirstOrDefault(ch => char.IsLetterOrDigit(ch)));
        return c >= 'A' && c <= 'Z' ? c.ToString() : "#";
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
