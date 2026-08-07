using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Banco de trabajo para configurar las tres reglas de combinación del juego:
/// fusiones (específicas y por categoría), equipos y rituales.
///
/// Por qué una ventana de editor y no una escena: todo esto son datos en <c>.asset</c>
/// (FusionDatabase y campos de CardData). Editarlos desde Play mode es frágil —los cambios
/// se pierden al salir si no se fuerzan— y obliga a entrar en Play para cada retoque.
///
/// El SIMULADOR llama al <see cref="FusionDatabase.ResolveChain"/> y al
/// <see cref="RitualResolver"/> REALES, no a una copia: si el duelo cambia de reglas, la
/// previsualización cambia con él y nunca miente.
///
/// Menú: YGO ▸ Cartas ▸ Banco de fusiones y rituales.
/// </summary>
public class FusionLabWindow : EditorWindow
{
    private enum Tab { Fusiones, Categorias, Equipos, Rituales, Simulador }

    private const string FusionDbResource = "Fusions";

    private Tab _tab = Tab.Fusiones;
    private FusionDatabase _db;
    private Vector2 _listScroll;
    private Vector2 _rootScroll;

    // Selectores por pestaña (independientes, para no pisarse entre sí).
    private readonly CardPickerControl _pickA = new("Material A");
    private readonly CardPickerControl _pickB = new("Material B");
    private readonly CardPickerControl _pickResult = new("Resultado");
    private readonly CardPickerControl _pickEquip = new("Carta de equipo");
    private readonly CardPickerControl _pickRitual = new("Carta de Ritual");
    private readonly CardPickerControl _pickRitualMat = new("Añadir material");
    private readonly CardPickerControl _pickRitualResult = new("Monstruo resultante");
    private readonly CardPickerControl _pickSimCard = new("Añadir a la cadena");

    // Recetas por fusionGroup (legado) en edición.
    private int _groupAIndex, _groupBIndex;
    private string[] _groups;
    private bool _showLegacy;

    // Categorías por reglas.
    private List<FusionCategory> _categories;
    private int _catIndex;
    private int _recipeCatA, _recipeCatB, _recipePriority;

    /// <summary>
    /// Cuántas cartas encaja cada categoría. Se cachea porque recorrer las 14.651 del
    /// catálogo en cada repintado de la ventana se nota al escribir en los campos.
    /// </summary>
    private readonly Dictionary<FusionCategory, (int count, List<CardData> sample)> _matchCache = new();

    // Cadena del simulador.
    private readonly List<CardData> _chain = new();

    private string _filterRecipes = "";

    [MenuItem("YGO/Cartas/Banco de fusiones y rituales")]
    public static void Open()
    {
        var w = GetWindow<FusionLabWindow>(false, "Banco de fusiones");
        w.minSize = new Vector2(680, 560);
    }

    private void OnEnable() => LoadDatabase();

    private void LoadDatabase()
    {
        _db = Resources.LoadAll<FusionDatabase>(FusionDbResource).FirstOrDefault();
    }

    private void OnGUI()
    {
        if (_db == null)
        {
            EditorGUILayout.HelpBox(
                "No encuentro ningún FusionDatabase en Resources/Fusions.\n" +
                "Créalo con Create ▸ YGO ▸ Fusion Database dentro de esa carpeta.",
                MessageType.Error);
            if (GUILayout.Button("Volver a buscar")) LoadDatabase();
            return;
        }

        _tab = (Tab)GUILayout.Toolbar((int)_tab, new[]
        {
            "Fusiones", "Categorías", "Equipos", "Rituales", "Simulador"
        });
        EditorGUILayout.Space();

        _rootScroll = EditorGUILayout.BeginScrollView(_rootScroll);
        switch (_tab)
        {
            case Tab.Fusiones: DrawSpecificTab(); break;
            case Tab.Categorias: DrawCategoryTab(); break;
            case Tab.Equipos: DrawEquipTab(); break;
            case Tab.Rituales: DrawRitualTab(); break;
            case Tab.Simulador: DrawSimulatorTab(); break;
        }
        EditorGUILayout.EndScrollView();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Fusiones específicas
    // ─────────────────────────────────────────────────────────────────────

    private void DrawSpecificTab()
    {
        EditorGUILayout.HelpBox(
            "Carta + carta = resultado exacto. Es la regla de MÁXIMA prioridad: se comprueba " +
            "antes que las de categoría, el equipo y la absorción. El orden de los materiales " +
            "no importa.", MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width / 3 - 12)))
                _pickA.Draw(190);
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width / 3 - 12)))
                _pickB.Draw(190);
            using (new EditorGUILayout.VerticalScope())
                _pickResult.Draw(190);
        }

        EditorGUILayout.Space();

        var a = _pickA.Selected; var b = _pickB.Selected; var r = _pickResult.Selected;
        bool complete = a != null && b != null && r != null;

        if (complete && !r.IsMonster)
            EditorGUILayout.HelpBox(
                "El resultado NO es un monstruo. La fusión lo coloca en la zona de monstruos, " +
                "así que el duelo rechazará esta receta.", MessageType.Warning);

        var clash = complete ? FindExisting(a, b) : null;
        if (clash != null)
            EditorGUILayout.HelpBox(
                $"Ya existe una receta para ese par → {NameOf(clash.result)}. " +
                "Añadirla la sustituirá.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(!complete))
        {
            if (GUILayout.Button(clash != null ? "Sustituir receta" : "Añadir receta", GUILayout.Height(26)))
                AddSpecific(a, b, r);
        }

        EditorGUILayout.Space();
        DrawSpecificList();
    }

    private FusionRecipe FindExisting(CardData a, CardData b) =>
        _db.recipes.FirstOrDefault(x => (x.materialA == a && x.materialB == b)
                                     || (x.materialA == b && x.materialB == a));

    private void AddSpecific(CardData a, CardData b, CardData r)
    {
        Undo.RecordObject(_db, "Añadir receta de fusión");
        var existing = FindExisting(a, b);
        if (existing != null) existing.result = r;
        else _db.recipes.Add(new FusionRecipe { materialA = a, materialB = b, result = r });
        Save();
    }

    private void DrawSpecificList()
    {
        EditorGUILayout.LabelField($"Recetas específicas ({_db.recipes.Count})", EditorStyles.boldLabel);
        _filterRecipes = EditorGUILayout.TextField("Filtrar", _filterRecipes);

        _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(200));
        for (int i = 0; i < _db.recipes.Count; i++)
        {
            var rec = _db.recipes[i];
            string line = $"{NameOf(rec.materialA)}  +  {NameOf(rec.materialB)}  =  {NameOf(rec.result)}";
            if (!string.IsNullOrWhiteSpace(_filterRecipes) &&
                line.IndexOf(_filterRecipes, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(line);
                if (GUILayout.Button("Cargar", GUILayout.Width(60)))
                {
                    _pickA.SetSelected(rec.materialA);
                    _pickB.SetSelected(rec.materialB);
                    _pickResult.SetSelected(rec.result);
                }
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    Undo.RecordObject(_db, "Borrar receta");
                    _db.recipes.RemoveAt(i);
                    Save();
                    break;
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Fusiones por categoría (fusionGroup)
    // ─────────────────────────────────────────────────────────────────────

    private void DrawCategoryTab()
    {
        EditorGUILayout.HelpBox(
            "Una CATEGORÍA es un conjunto de cartas definido por reglas (atributo, tipo, " +
            "rangos de ATK/DEF, nivel) más excepciones a mano. Una receta une dos categorías.\n\n" +
            "Ejemplo: «OSCURO ATK ≤ 1500» + «LUZ ATK ≤ 1500» = X — y esa misma pareja de " +
            "atributos con monstruos fuertes NO dispara la receta.",
            MessageType.None);

        DrawCategoryEditor();
        EditorGUILayout.Space();
        DrawCategoryRecipeEditor();
        EditorGUILayout.Space();
        DrawLegacyGroupRecipes();
    }

    // ── Editor de categorías ─────────────────────────────────────────────

    private void DrawCategoryEditor()
    {
        EditorGUILayout.LabelField("Categorías", EditorStyles.boldLabel);

        EnsureCategories();

        using (new EditorGUILayout.HorizontalScope())
        {
            var names = _categories.Select(c => c.Label).ToArray();
            int newIdx = EditorGUILayout.Popup("Editando", _catIndex,
                                               names.Length > 0 ? names : new[] { "(ninguna)" });
            if (newIdx != _catIndex) { _catIndex = newIdx; _matchCache.Clear(); }

            if (GUILayout.Button("Nueva", GUILayout.Width(60))) CreateCategory();
        }

        var cat = CurrentCategory;
        if (cat == null)
        {
            EditorGUILayout.HelpBox("Todavía no hay categorías. Crea una con «Nueva».", MessageType.Info);
            return;
        }

        EditorGUI.BeginChangeCheck();
        var so = new SerializedObject(cat);
        so.Update();
        var prop = so.GetIterator();
        prop.NextVisible(true);   // salta m_Script
        while (prop.NextVisible(false))
            EditorGUILayout.PropertyField(prop, true);
        so.ApplyModifiedProperties();
        if (EditorGUI.EndChangeCheck()) _matchCache.Clear();

        // Vista previa: lo importante con 14.651 cartas es ver a cuántas afecta la regla
        // ANTES de crear la receta. Se cachea porque recorrer el catálogo en cada repintado
        // de la ventana se nota.
        var (count, sample) = MatchesOf(cat);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Coinciden {count} cartas", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(cat.DescribeRules(), EditorStyles.miniLabel);

        if (count == 0)
            EditorGUILayout.HelpBox("Ninguna carta cumple estas reglas: la receta nunca se activaría.",
                                    MessageType.Warning);
        else if (count > 500)
            EditorGUILayout.HelpBox($"{count} cartas es MUCHO: la fusión se disparará casi siempre. " +
                                    "Acota con rangos de ATK/DEF o nivel.", MessageType.Warning);

        foreach (var c in sample)
            EditorGUILayout.LabelField($"   · {c.cardName}  ({c.attribute}, ATK {c.baseAtk}/DEF {c.baseDef}, ★{c.stars})",
                                       EditorStyles.miniLabel);
        if (count > sample.Count)
            EditorGUILayout.LabelField($"   … y {count - sample.Count} más", EditorStyles.miniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Añadir la carta seleccionada como excepción fija"))
            {
                if (_pickResult.Selected != null)
                {
                    Undo.RecordObject(cat, "Añadir excepción");
                    cat.alwaysInclude.Add(_pickResult.Selected);
                    EditorUtility.SetDirty(cat);
                    AssetDatabase.SaveAssets();
                    _matchCache.Clear();
                }
            }
            if (GUILayout.Button("Refrescar", GUILayout.Width(80))) _matchCache.Clear();
        }
    }

    // ── Recetas entre categorías ─────────────────────────────────────────

    private void DrawCategoryRecipeEditor()
    {
        EditorGUILayout.LabelField("Nueva receta entre categorías", EditorStyles.boldLabel);
        EnsureCategories();

        if (_categories.Count == 0) return;

        var names = _categories.Select(c => c.Label).ToArray();
        using (new EditorGUILayout.HorizontalScope())
        {
            _recipeCatA = EditorGUILayout.Popup("Categoría A", _recipeCatA, names);
            _recipeCatB = EditorGUILayout.Popup("Categoría B", _recipeCatB, names);
        }
        _recipePriority = EditorGUILayout.IntField(
            new GUIContent("Prioridad", "Mayor gana si varias recetas encajan con el mismo par. " +
                                        "Sube la de las reglas más estrechas."), _recipePriority);

        _pickResult.Draw(170);

        var catA = _categories.ElementAtOrDefault(_recipeCatA);
        var catB = _categories.ElementAtOrDefault(_recipeCatB);
        bool complete = catA != null && catB != null && _pickResult.Selected != null;

        if (complete && !_pickResult.Selected.IsMonster)
            EditorGUILayout.HelpBox("El resultado no es un monstruo: el duelo rechazará la fusión.",
                                    MessageType.Warning);

        using (new EditorGUI.DisabledScope(!complete))
        {
            if (GUILayout.Button("Añadir receta", GUILayout.Height(24)))
            {
                Undo.RecordObject(_db, "Añadir receta entre categorías");
                var existing = _db.categoryFusions.FirstOrDefault(
                    x => (x.categoryA == catA && x.categoryB == catB)
                      || (x.categoryA == catB && x.categoryB == catA));
                if (existing != null) { existing.result = _pickResult.Selected; existing.priority = _recipePriority; }
                else _db.categoryFusions.Add(new FusionCategoryRecipe
                {
                    categoryA = catA,
                    categoryB = catB,
                    result = _pickResult.Selected,
                    priority = _recipePriority
                });
                Save();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Recetas entre categorías ({_db.categoryFusions.Count})",
                                   EditorStyles.boldLabel);

        foreach (var rec in _db.categoryFusions.OrderByDescending(r => r.priority).ToList())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string a = rec.categoryA != null ? rec.categoryA.Label : "—";
                string b = rec.categoryB != null ? rec.categoryB.Label : "—";
                EditorGUILayout.LabelField($"[{rec.priority}]  {a}  +  {b}  =  {NameOf(rec.result)}");
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    Undo.RecordObject(_db, "Borrar receta entre categorías");
                    _db.categoryFusions.Remove(rec);
                    Save();
                    break;
                }
            }
        }
    }

    // ── Legado: recetas por fusionGroup ──────────────────────────────────

    private void DrawLegacyGroupRecipes()
    {
        _showLegacy = EditorGUILayout.Foldout(_showLegacy,
            $"Recetas por fusionGroup — legado ({_db.categoryRecipes.Count})");
        if (!_showLegacy) return;

        EditorGUILayout.HelpBox(
            "Modelo antiguo: solo compara la etiqueta 'fusionGroup'. Se sigue evaluando, pero " +
            "DESPUÉS de las categorías con reglas.", MessageType.None);

        EnsureGroups();
        using (new EditorGUILayout.HorizontalScope())
        {
            _groupAIndex = EditorGUILayout.Popup("Grupo A", _groupAIndex, _groups);
            _groupBIndex = EditorGUILayout.Popup("Grupo B", _groupBIndex, _groups);
        }

        string ga = GroupAt(_groupAIndex), gb = GroupAt(_groupBIndex);
        if (!string.IsNullOrEmpty(ga))
            EditorGUILayout.LabelField($"A: {CountInGroup(ga)} cartas · B: {CountInGroup(gb)} cartas",
                                       EditorStyles.miniLabel);

        bool complete = !string.IsNullOrEmpty(ga) && !string.IsNullOrEmpty(gb) && _pickResult.Selected != null;
        using (new EditorGUI.DisabledScope(!complete))
        {
            if (GUILayout.Button("Añadir receta por fusionGroup"))
            {
                Undo.RecordObject(_db, "Añadir receta por fusionGroup");
                var existing = _db.categoryRecipes.FirstOrDefault(
                    x => (x.groupA == ga && x.groupB == gb) || (x.groupA == gb && x.groupB == ga));
                if (existing != null) existing.result = _pickResult.Selected;
                else _db.categoryRecipes.Add(new CategoryFusionRecipe
                { groupA = ga, groupB = gb, result = _pickResult.Selected });
                Save();
            }
        }

        for (int i = 0; i < _db.categoryRecipes.Count; i++)
        {
            var rec = _db.categoryRecipes[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                bool empty = string.IsNullOrEmpty(rec.groupA) && string.IsNullOrEmpty(rec.groupB);
                EditorGUILayout.LabelField(empty
                    ? "(fila vacía — bórrala)"
                    : $"{rec.groupA}  +  {rec.groupB}  =  {NameOf(rec.result)}");
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    Undo.RecordObject(_db, "Borrar receta por fusionGroup");
                    _db.categoryRecipes.RemoveAt(i);
                    Save();
                    break;
                }
            }
        }
    }

    // ── Soporte de categorías ────────────────────────────────────────────

    private const string CategoryFolder = "Assets/Data/FusionCategories";

    private FusionCategory CurrentCategory =>
        _categories != null ? _categories.ElementAtOrDefault(_catIndex) : null;

    private void EnsureCategories()
    {
        if (_categories != null) return;
        _categories = AssetDatabase.FindAssets("t:FusionCategory")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<FusionCategory>)
            .Where(c => c != null)
            .OrderBy(c => c.Label)
            .ToList();
    }

    private void CreateCategory()
    {
        if (!AssetDatabase.IsValidFolder(CategoryFolder))
            ProjectReorganizer.EnsureFolder(CategoryFolder);

        var cat = CreateInstance<FusionCategory>();
        string path = AssetDatabase.GenerateUniqueAssetPath(CategoryFolder + "/NuevaCategoria.asset");
        AssetDatabase.CreateAsset(cat, path);
        AssetDatabase.SaveAssets();

        _categories = null;
        EnsureCategories();
        _catIndex = _categories.IndexOf(cat);
        _matchCache.Clear();
    }

    /// <summary>Cuántas cartas del catálogo caen en la categoría, con una muestra.</summary>
    private (int count, List<CardData> sample) MatchesOf(FusionCategory cat)
    {
        if (_matchCache.TryGetValue(cat, out var cached)) return cached;

        int count = 0;
        var sample = new List<CardData>();
        foreach (var c in CardCatalogCache.All)
        {
            if (!cat.Matches(c)) continue;
            count++;
            if (sample.Count < 8) sample.Add(c);
        }

        var result = (count, sample);
        _matchCache[cat] = result;
        return result;
    }

    private void EnsureGroups()
    {
        if (_groups != null) return;
        var set = CardCatalogCache.All
            .Select(c => c.fusionGroup)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct()
            .OrderBy(g => g)
            .ToList();
        set.Insert(0, "(ninguno)");
        _groups = set.ToArray();
    }

    private string GroupAt(int i) => _groups != null && i > 0 && i < _groups.Length ? _groups[i] : "";

    private int CountInGroup(string g) =>
        string.IsNullOrEmpty(g) ? 0 : CardCatalogCache.All.Count(c => c.fusionGroup == g);

    // ─────────────────────────────────────────────────────────────────────
    //  Equipos
    // ─────────────────────────────────────────────────────────────────────

    private void DrawEquipTab()
    {
        EditorGUILayout.HelpBox(
            "Los equipos NO viven en el FusionDatabase: son campos de la propia carta. " +
            "Al combinar un monstruo con un equipo compatible, el monstruo sobrevive y suma " +
            "el bonus. Un equipo incompatible se descarta por absorción.", MessageType.None);

        _pickEquip.Draw(200);
        var e = _pickEquip.Selected;
        if (e == null) return;

        if (!e.IsEquip)
        {
            EditorGUILayout.HelpBox(
                $"'{e.cardName}' es {e.CategoryLabel}, no un Equipo. Cambia su categoría a Equip " +
                "para que pueda equiparse.", MessageType.Warning);
            if (GUILayout.Button("Convertir en carta de Equipo"))
            {
                Undo.RecordObject(e, "Cambiar categoría a Equipo");
                e.cardCategory = CardCategory.Equip;
                EditorUtility.SetDirty(e);
                AssetDatabase.SaveAssets();
            }
            return;
        }

        EditorGUI.BeginChangeCheck();
        int atk = EditorGUILayout.IntField("Bonus ATK", e.equipAtkBonus);
        int def = EditorGUILayout.IntField("Bonus DEF", e.equipDefBonus);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("¿A qué monstruos se puede equipar?", EditorStyles.boldLabel);

        EnsureCategories();
        var options = new List<string> { "(cualquier monstruo / regla antigua)" };
        options.AddRange(_categories.Select(c => c.Label));
        int current = e.equipTargets != null ? _categories.IndexOf(e.equipTargets) + 1 : 0;
        int chosen = EditorGUILayout.Popup("Regla de objetivo", Mathf.Max(0, current), options.ToArray());
        FusionCategory rule = chosen > 0 ? _categories.ElementAtOrDefault(chosen - 1) : null;

        // Si la regla asignada no salió en la lista (asset movido o aún sin indexar), el
        // desplegable marcaría "(ninguna)" y al guardar la borraríamos sin que nadie la
        // tocara. Mientras el usuario no elija otra cosa, se respeta la que ya tenía.
        if (rule == null && e.equipTargets != null && current == 0 && chosen == 0)
            rule = e.equipTargets;

        // Sin regla asignada sigue mandando el modelo antiguo (solo por tipo).
        bool restrict = e.equipRestrictToType;
        MonsterType type = e.equipMonsterType;
        if (rule == null)
        {
            restrict = EditorGUILayout.Toggle("(Legado) Restringir por tipo", restrict);
            using (new EditorGUI.DisabledScope(!restrict))
                type = (MonsterType)EditorGUILayout.EnumPopup("Tipo permitido", type);
        }

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(e, "Editar equipo");
            e.equipAtkBonus = atk;
            e.equipDefBonus = def;
            e.equipTargets = rule;
            e.equipRestrictToType = restrict;
            e.equipMonsterType = type;
            EditorUtility.SetDirty(e);
            AssetDatabase.SaveAssets();
        }

        // El recuento usa EquipAppliesTo, o sea la MISMA función que el duelo: si aquí
        // aparecen 12 monstruos, son exactamente esos 12 los que podrán llevar el equipo.
        int compatible = 0;
        var sample = new List<CardData>();
        foreach (var c in CardCatalogCache.All)
        {
            if (!e.EquipAppliesTo(c)) continue;
            compatible++;
            if (sample.Count < 8) sample.Add(c);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Monstruos compatibles: {compatible}", EditorStyles.boldLabel);
        if (rule != null) EditorGUILayout.LabelField(rule.DescribeRules(), EditorStyles.miniLabel);
        foreach (var c in sample)
            EditorGUILayout.LabelField($"   · {c.cardName}  ({c.attribute}, ATK {c.baseAtk}, ★{c.stars})",
                                       EditorStyles.miniLabel);
        if (compatible > sample.Count)
            EditorGUILayout.LabelField($"   … y {compatible - sample.Count} más", EditorStyles.miniLabel);

        if (compatible == 0)
            EditorGUILayout.HelpBox("Ningún monstruo cumple la regla: este equipo no se podría usar nunca.",
                                    MessageType.Warning);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Las reglas se crean y editan en la pestaña «Categorías». Sirven para las dos cosas: " +
            "materiales de fusión y objetivos de equipo.", MessageType.None);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Rituales
    // ─────────────────────────────────────────────────────────────────────

    private void DrawRitualTab()
    {
        EditorGUILayout.HelpBox(
            "Para invocar, en el duelo hay que seleccionar la carta de Ritual MÁS exactamente " +
            "sus materiales: ni una carta de más. Se comparan por id, así que pedir dos copias " +
            "del mismo material exige de verdad dos copias.", MessageType.None);

        _pickRitual.Draw(170);
        var ritual = _pickRitual.Selected;
        if (ritual == null) return;

        if (!ritual.IsRitual)
        {
            EditorGUILayout.HelpBox(
                $"'{ritual.cardName}' es {ritual.CategoryLabel}, no un Ritual.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Monstruo resultante", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Actual:", NameOf(ritual.ritualResult));
        _pickRitualResult.Draw(150);
        using (new EditorGUI.DisabledScope(_pickRitualResult.Selected == null))
        {
            if (GUILayout.Button("Fijar como resultado"))
            {
                Undo.RecordObject(ritual, "Fijar resultado de ritual");
                ritual.ritualResult = _pickRitualResult.Selected;
                EditorUtility.SetDirty(ritual);
                AssetDatabase.SaveAssets();
            }
        }

        if (ritual.ritualResult != null && !ritual.ritualResult.IsMonster)
            EditorGUILayout.HelpBox("El resultado no es un monstruo: el duelo rechazará el ritual.",
                                    MessageType.Warning);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Materiales ({ritual.ritualMaterials.Count})", EditorStyles.boldLabel);
        for (int i = 0; i < ritual.ritualMaterials.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{i + 1}. {NameOf(ritual.ritualMaterials[i])}");
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    Undo.RecordObject(ritual, "Quitar material");
                    ritual.ritualMaterials.RemoveAt(i);
                    EditorUtility.SetDirty(ritual);
                    AssetDatabase.SaveAssets();
                    break;
                }
            }
        }

        _pickRitualMat.Draw(150);
        using (new EditorGUI.DisabledScope(_pickRitualMat.Selected == null))
        {
            if (GUILayout.Button("Añadir material"))
            {
                Undo.RecordObject(ritual, "Añadir material de ritual");
                ritual.ritualMaterials.Add(_pickRitualMat.Selected);
                EditorUtility.SetDirty(ritual);
                AssetDatabase.SaveAssets();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Simulador
    // ─────────────────────────────────────────────────────────────────────

    private void DrawSimulatorTab()
    {
        EditorGUILayout.HelpBox(
            "Ejecuta el MISMO código que el duelo (FusionDatabase.ResolveChain y " +
            "RitualResolver), en el mismo orden estricto izquierda→derecha. Lo que salga aquí " +
            "es lo que pasará en partida.", MessageType.None);

        EditorGUILayout.LabelField($"Cadena ({_chain.Count})", EditorStyles.boldLabel);
        for (int i = 0; i < _chain.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{i + 1}. {NameOf(_chain[i])}");
                if (GUILayout.Button("✕", GUILayout.Width(24))) { _chain.RemoveAt(i); break; }
            }
        }
        if (_chain.Count > 0 && GUILayout.Button("Vaciar cadena")) _chain.Clear();

        _pickSimCard.Draw(160);
        using (new EditorGUI.DisabledScope(_pickSimCard.Selected == null))
            if (GUILayout.Button("Añadir a la cadena")) _chain.Add(_pickSimCard.Selected);

        if (_chain.Count < 2) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Resultado", EditorStyles.boldLabel);

        // Ritual primero: es lo que hace DuelController.ConfirmSlot.
        var ritual = RitualResolver.Evaluate(_chain);
        if (ritual.IsRitualAttempt)
        {
            EditorGUILayout.HelpBox("Se interpreta como RITUAL.\n" + ritual.Describe(),
                                    ritual.Ok ? MessageType.Info : MessageType.Warning);
            return;
        }

        var chain = _db.ResolveChain(new List<CardData>(_chain));
        var sb = new System.Text.StringBuilder();
        var current = _chain[0];
        for (int i = 0; i < chain.Steps.Count; i++)
        {
            var step = chain.Steps[i];
            string label = step.Type switch
            {
                FusionStepType.Specific => "Específica",
                FusionStepType.Category => "Categoría",
                FusionStepType.Equip => $"Equipo (+{step.EquipAtkBonusApplied} ATK / +{step.EquipDefBonusApplied} DEF)",
                _ => "Absorción (se descarta)"
            };
            sb.AppendLine($"{i + 1}. {NameOf(current)} + {NameOf(_chain[i + 1])} → " +
                          $"{NameOf(step.Result)}   [{label}]");
            current = step.Result;
        }

        EditorGUILayout.LabelField(sb.ToString(), EditorStyles.wordWrappedLabel);

        var final = chain.FinalResult;
        if (final == null)
            EditorGUILayout.HelpBox("La cadena no produce nada.", MessageType.Warning);
        else if (!final.IsMonster)
            EditorGUILayout.HelpBox(
                $"El resultado ({final.cardName}) NO es un monstruo: el duelo rechazaría esta fusión.",
                MessageType.Error);
        else
            EditorGUILayout.HelpBox(
                $"RESULTADO: {final.cardName}\n" +
                $"ATK {final.baseAtk + chain.TotalEquipAtkBonus} / " +
                $"DEF {final.baseDef + chain.TotalEquipDefBonus}" +
                (chain.TotalEquipAtkBonus + chain.TotalEquipDefBonus > 0
                    ? $"  (incluye +{chain.TotalEquipAtkBonus}/+{chain.TotalEquipDefBonus} de equipos)"
                    : ""),
                MessageType.Info);
    }

    // ─────────────────────────────────────────────────────────────────────

    private void Save()
    {
        EditorUtility.SetDirty(_db);
        AssetDatabase.SaveAssets();
    }

    private static string NameOf(CardData c) => c != null ? c.cardName : "—";
}
