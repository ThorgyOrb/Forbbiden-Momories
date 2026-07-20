using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Genera el prefab de carta del juego ("CardMonsterV2"), basado en la propuesta
/// de diseño del usuario (layout tipo TCG moderno). Tiene DOS caras, y CardDisplay
/// enciende una u otra según la carta:
///   · Monstruo    → icono de tipo + estrellas de nivel + número arriba (sobre el
///                   arte), placa de nombre, subtipo ("BESTIA / EFECTO") con icono
///                   de atributo, y barra ATK/DEF.
///   · No-monstruo → arte + placa de nombre + barra de categoría ("MAGIA"). Sin
///                   estrellas ni ATK/DEF; la descripción se lee en el panel lateral.
///
/// Es el prefab que se usa en TODAS partes (grilla del catálogo, modal, constructor
/// de deck y duelo); el clásico Assets/Resources/Prefabs/Card.prefab ya no se
/// instancia en ningún sitio, pero se conserva y de él salen el material holo, el
/// icon config y el reverso. Comparte el MISMO componente CardDisplay (con los
/// campos opcionales de "Layout V2"), así que es drop-in.
///
/// OJO: al regenerarlo cambian los fileID internos del prefab. Las escenas con
/// generador se rehacen solas; LibraryCatalogScene (que no tiene) se repunta con
/// YGO ▸ Cartas ▸ Repuntar catálogo a la carta V2.
///
/// Estilo PLANO con la paleta Neo-Kemet + placeholders (como los otros builders
/// del proyecto); el arte ornamentado del mockup se conecta luego cambiando el
/// sprite del marco/insignias. Regenera con el menú y vuelve a instanciar.
///
/// Menú: YGO ▸ Cartas ▸ Construir Prefab Carta Monstruo V2.
/// </summary>
public static class CardMonsterV2PrefabBuilder
{
    const string OutPath        = "Assets/Resources/Prefabs/CardMonsterV2.prefab";
    const string SrcCardPath    = "Assets/Resources/Prefabs/Card.prefab";
    const string StarSpritePath = "Assets/Sprites/level_star.png";
    const string FrameSpritePath = "Assets/Sprites/frame_v4.png";    // FORMA del marco (máscara), líneas finas
    const string CardBasePath    = "Assets/Sprites/card_base_v3.png"; // silueta obsidiana con esquinas cortadas
    const string MetalTexPath    = "Assets/Sprites/metal_gold.png";  // TEXTURA metálica que rellena el marco (swappable)
    const string FoilMatPath     = "Assets/Materials/CardV2Foil.mat"; // material shader YGO/CardV2Foil (cristal + brillo diagonal)

    // GUIDs de assets compartidos con el prefab clásico (mismos materiales/config).
    const string GuidMatHolo    = "37f6d8c6dcf85da4e927f62bc1d43894";
    const string GuidMatBorder  = "4d0c72f8526753146bb3c5bdb932e877";
    const string GuidIconConfig = "e11fb358c459b6f4fa2747129de4eafa";
    const string GuidCardBack   = "e1b59363cf7339d408482f4e53adb170";
    const string GuidCardBackMat = "da47fa7bc7c68954597ff213d97f8bac";
    const string GuidThemeFont  = "8f586378b4e144a9851e7b34d9b748ee";

    // ── Paleta Neo-Kemet ────────────────────────────────────────────────
    static readonly Color Obsidian  = new Color(0.047f, 0.043f, 0.063f, 1f);
    static readonly Color Panel     = new Color(0.086f, 0.078f, 0.114f, 0.88f);
    static readonly Color PanelSoft = new Color(0.13f, 0.11f, 0.17f, 0.92f);
    static readonly Color Gold      = new Color(0.93f, 0.75f, 0.33f);
    static readonly Color Cyan      = new Color(0.22f, 0.95f, 0.86f);
    static readonly Color Violet    = new Color(0.63f, 0.40f, 0.95f);
    static readonly Color Bright    = new Color(0.94f, 0.91f, 0.84f);
    static readonly Color Muted     = new Color(0.60f, 0.57f, 0.52f);
    static readonly Color Line      = new Color(0.93f, 0.75f, 0.33f, 0.35f);

    [MenuItem("YGO/Cartas/Construir Prefab Carta Monstruo V2")]
    public static void Build()
    {
        // ── Assets compartidos ────────────────────────────────────────────
        // NOTA: ya NO se usa el material holo del prefab clásico (el usuario quiere
        // esta carta SIN esos efectos por ahora). CardHoloEffect queda inerte: el
        // GameObject del borde se llama "Border" (no "Frame"), así su Awake no lo
        // encuentra, y no se cablea matHolo/matRainbowBorder.
        var iconCfg   = Load<CardIconConfig>(GuidIconConfig);
        var cardBackSprite = Load<Sprite>(GuidCardBack);
        var cardBackMat    = Load<Material>(GuidCardBackMat);
        var themeFont = Load<TMP_FontAsset>(GuidThemeFont);
        var starSprite  = EnsureStarSprite();
        var frameSprite = EnsureFrameSprite();       // FORMA del marco (esquinas cortadas) → máscara
        var cardBaseSprite = EnsureCardBaseSprite(); // silueta obsidiana con esquinas cortadas
        var metalSprite    = EnsureMetalSprite();    // textura metálica (oro) — cámbiala por tu imagen
        var vignetteSprite = EnsureVignetteSprite();     // máscara radial (bordes oscuros)
        var foilMat        = EnsureFoilMaterial();       // material shader del foil de cristal (toda la carta)
        var knob  = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(SrcCardPath) == null)
            Debug.LogWarning($"CardMonsterV2PrefabBuilder: no se encontró {SrcCardPath} (solo referencia, se sigue).");

        // ── Raíz: 200×280 (tamaño nativo, igual que Card.prefab) ──────────
        var root = new GameObject("CardMonsterV2",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rootRT = (RectTransform)root.transform;
        rootRT.sizeDelta = new Vector2(200, 280);
        rootRT.pivot = new Vector2(0.5f, 0.5f);
        // La raíz es solo el ÁREA DE CLICK (Image transparente, raycast on). El cuerpo
        // visible de la carta es el hijo "CardBase".
        var rootImg = root.GetComponent<Image>();
        rootImg.color = new Color(0f, 0f, 0f, 0f);

        var display = root.AddComponent<CardDisplay>();          // auto-añade CardHoloEffect (queda inerte)
        var slot = root.AddComponent<LibraryCardSlot>();

        // ── Cuerpo visible (obsidiana, esquinas cortadas). ──
        var cardBase = Rect(rootRT, "CardBase", V(0, 0), V(1, 1), Vector2.zero, Vector2.zero);
        var cardBaseImg = AddImg(cardBase, Obsidian);
        cardBaseImg.sprite = cardBaseSprite;
        cardBaseImg.type = Image.Type.Simple;
        cardBaseImg.raycastTarget = false;

        // ── Reverso (oculto por defecto) ──────────────────────────────────
        var back = Rect(rootRT, "CardBack", V(0, 0), V(1, 1), Vector2.zero, Vector2.zero);
        var backImg = AddImg(back, Color.white);
        backImg.sprite = cardBackSprite;
        if (cardBackMat != null) backImg.material = cardBackMat;
        back.gameObject.SetActive(false);

        // ── Ilustración: LLENA toda la mitad superior (hasta la barra de
        //    nombre). El tipo/nivel se superponen sobre ella (ver TopBar). ──
        // Inset 10px (lados/arriba) para que el arte quede DENTRO de la línea interior
        // del marco y no asome entre las dos líneas ni en las esquinas cortadas.
        // Baja hasta 0.30 (nombre más chico) → el arte ocupa MÁS.
        var art = Rect(rootRT, "Art", V(0, 0.30f), V(1, 1f), new Vector2(10, 0), new Vector2(-10, -10));
        var artImg = AddImg(art, Color.white);
        artImg.raycastTarget = false;
        art.gameObject.AddComponent<RectMask2D>(); // recorta viñeta/destello/chispas al arte

        // ── Viñeta sobre el arte (siempre; oscurece bordes). ──
        var vignetteImg = AddImg(Rect(art, "Vignette", V(0, 0), V(1, 1), Vector2.zero, Vector2.zero),
                                 new Color(0f, 0f, 0f, 0.5f));
        vignetteImg.sprite = vignetteSprite; vignetteImg.raycastTarget = false;

        // ── Contenedor frontal (se apaga al estar boca abajo) ─────────────
        // OJO: debe llamarse "StatsPanel" y ser hijo directo de la raíz —
        // CardDisplay.RefreshVisuals lo busca con transform.Find("StatsPanel").
        var front = Rect(rootRT, "StatsPanel", V(0, 0), V(1, 1), Vector2.zero, Vector2.zero);

        // ═══════════ SUPERPOSICIÓN sobre el arte: tipo · estrellas · nivel ═══════════
        // Va DENTRO del GameObject "Art" para que se dibuje encima del artwork y
        // se oculte junto con él al poner la carta boca abajo. Todo cuelga de
        // "MonsterOverlay" porque es exclusivo de monstruos: CardDisplay lo apaga
        // en magias/trampas (el arte sí sigue visible, así que no basta con Art).
        var overlay = Rect(art, "MonsterOverlay", V(0, 0), V(1, 1), Vector2.zero, Vector2.zero);

        // Velo oscuro en el borde superior del arte, para legibilidad.
        var scrim = Rect(overlay, "TopScrim", V(0, 1), V(1, 1), Vector2.zero, Vector2.zero);
        scrim.pivot = new Vector2(0.5f, 1f);
        scrim.sizeDelta = new Vector2(0, 42);
        AddImg(scrim, new Color(0.02f, 0.02f, 0.04f, 0.55f)).raycastTarget = false;

        // Barra superpuesta (30px de alto, pegada al borde superior del arte).
        var topBar = Rect(overlay, "TopBar", V(0, 1), V(1, 1), new Vector2(5, -36), new Vector2(-5, -6));

        // Insignia de tipo (izquierda): círculo oscuro con aro dorado.
        var typeBadge = Rect(topBar, "TypeBadge", V(0, 0.5f), V(0, 0.5f), Vector2.zero, Vector2.zero);
        typeBadge.sizeDelta = new Vector2(28, 28);
        typeBadge.anchoredPosition = new Vector2(14, 0);
        var tbImg = AddImg(typeBadge, new Color(0.06f, 0.05f, 0.09f, 0.88f));
        tbImg.sprite = knob; tbImg.raycastTarget = false;
        Outline(typeBadge, Gold);
        var typeIcon = AddImg(Rect(typeBadge, "TypeIcon", V(0.5f, 0.5f), V(0.5f, 0.5f),
                              Vector2.zero, Vector2.zero), Bright);
        ((RectTransform)typeIcon.transform).sizeDelta = new Vector2(19, 19);
        typeIcon.preserveAspect = true; typeIcon.raycastTarget = false;

        // Estrellas de nivel (centro) — CardDisplay las instancia y posiciona a mano
        // (centradas, tamaño fijo). NO se usa LayoutGroup a propósito.
        var stars = Rect(topBar, "LevelStars", V(0.16f, 0f), V(0.84f, 1f), Vector2.zero, Vector2.zero);

        // Número de nivel (derecha): círculo carmesí con aro dorado.
        var lvlBadge = Rect(topBar, "LevelBadge", V(1, 0.5f), V(1, 0.5f), Vector2.zero, Vector2.zero);
        lvlBadge.sizeDelta = new Vector2(28, 28);
        lvlBadge.anchoredPosition = new Vector2(-14, 0);
        var lbImg = AddImg(lvlBadge, new Color(0.45f, 0.06f, 0.11f, 0.95f));
        lbImg.sprite = knob; lbImg.raycastTarget = false;
        Outline(lvlBadge, Gold);
        var lvlNum = Label(lvlBadge, "LevelNumber", "8", 15,
                           new Color(1f, 0.92f, 0.72f), TextAlignmentOptions.Center, FontStyles.Bold);

        // ═══════════ Placa de nombre (centrada, más compacta) ═══════════
        var namePlate = Rect(front, "NamePlate", V(0, 0.21f), V(1, 0.295f), new Vector2(9, 0), new Vector2(-9, 0));
        AddImg(namePlate, Panel).raycastTarget = false;
        Accent(namePlate, bottom: true);

        var nameText = Label(namePlate, "NameText", "Nombre de la Carta", 13, Gold,
                             TextAlignmentOptions.Center, FontStyles.Bold);
        Place(nameText, V(0, 0), V(1f, 1f), new Vector2(12, 0), new Vector2(-12, 0));
        nameText.enableAutoSizing = true; nameText.fontSizeMin = 8f; nameText.fontSizeMax = 13f;
        nameText.enableWordWrapping = false; nameText.overflowMode = TextOverflowModes.Ellipsis;
        if (themeFont != null) nameText.font = themeFont;

        // ═══════════ Barra de subtipo (con el atributo centrado a la derecha) ═══════════
        var subtypeBar = Rect(front, "SubtypeBar", V(0, 0.14f), V(1, 0.205f), new Vector2(9, 0), new Vector2(-9, 0));
        AddImg(subtypeBar, new Color(0.055f, 0.085f, 0.10f, 0.72f)).raycastTarget = false;
        Outline(subtypeBar, Cyan);
        var subtype = Label(subtypeBar, "Subtype", "DRAGÓN / EFECTO", 10, Cyan,
                            TextAlignmentOptions.Center, FontStyles.Italic);
        Place(subtype, V(0, 0), V(1f, 1f), new Vector2(10, 0), new Vector2(-34, 0));
        subtype.characterSpacing = 2f;

        // Insignia de atributo: hija de la barra de subtipo → CENTRADA verticalmente
        // con "DRAGÓN / EFECTO", a la derecha (asomando un poco por el borde).
        var attrBadge = Rect(subtypeBar, "AttrBadge", V(1, 0.5f), V(1, 0.5f), Vector2.zero, Vector2.zero);
        attrBadge.sizeDelta = new Vector2(26, 26);
        attrBadge.anchoredPosition = new Vector2(-16, 0); // adentro, para que NO se salga de la carta
        var abImg = AddImg(attrBadge, new Color(0.07f, 0.06f, 0.11f, 0.92f));
        abImg.sprite = knob; abImg.raycastTarget = false;
        Outline(attrBadge, Cyan);
        var attrIcon = AddImg(Rect(attrBadge, "AttrIcon", V(0.5f, 0.5f), V(0.5f, 0.5f),
                              Vector2.zero, Vector2.zero), Bright);
        ((RectTransform)attrIcon.transform).sizeDelta = new Vector2(19, 19);
        attrIcon.preserveAspect = true; attrIcon.raycastTarget = false;

        // ═══════════ Barra ATK / DEF (abajo, más BAJA para que el arte crezca) ═══════════
        var statsBar = Rect(front, "StatsBar", V(0, 0.045f), V(1, 0.13f), new Vector2(9, 0), new Vector2(-9, 0));
        var atkText = StatBox(statsBar, "ATK", 0f, 0.49f, Gold);
        var defText = StatBox(statsBar, "DEF", 0.51f, 1f, Cyan);

        // ═══════════ Cara de NO-MONSTRUO (magia / trampa / ritual / especial) ═══════════
        // Hermano de StatsPanel: CardDisplay enciende uno u otro según la carta
        // (RefreshVisuals). Es "arte + nombre + categoría": sin estrellas, sin
        // ATK/DEF y sin caja de efecto (la descripción se lee en el panel lateral).
        // La placa de nombre va en el MISMO sitio que la del monstruo, para que el
        // nombre no salte de posición al cambiar de carta; la banda de abajo (donde
        // el monstruo lleva ATK/DEF) queda limpia.
        var spellPanel = Rect(rootRT, "SpellPanel", V(0, 0), V(1, 1), Vector2.zero, Vector2.zero);

        var spNamePlate = Rect(spellPanel, "NamePlate", V(0, 0.21f), V(1, 0.295f),
                               new Vector2(9, 0), new Vector2(-9, 0));
        AddImg(spNamePlate, Panel).raycastTarget = false;
        Accent(spNamePlate, bottom: true);

        var spellNameText = Label(spNamePlate, "NameText", "Nombre de la Carta", 13, Gold,
                                  TextAlignmentOptions.Center, FontStyles.Bold);
        Place(spellNameText, V(0, 0), V(1f, 1f), new Vector2(12, 0), new Vector2(-12, 0));
        spellNameText.enableAutoSizing = true; spellNameText.fontSizeMin = 8f; spellNameText.fontSizeMax = 13f;
        spellNameText.enableWordWrapping = false; spellNameText.overflowMode = TextOverflowModes.Ellipsis;
        if (themeFont != null) spellNameText.font = themeFont;

        // Barra de categoría ("MAGIA" / "EQUIPO" / "TRAMPA"...). El texto Y SU COLOR
        // los pone CardDisplay desde CardStyleKemet.BadgeColor; lo de aquí es muestra.
        var spCatBar = Rect(spellPanel, "CategoryBar", V(0, 0.14f), V(1, 0.205f),
                            new Vector2(9, 0), new Vector2(-9, 0));
        AddImg(spCatBar, new Color(0.055f, 0.085f, 0.10f, 0.72f)).raycastTarget = false;
        Outline(spCatBar, Violet);
        var catText = Label(spCatBar, "Category", "MAGIA", 10, Violet,
                            TextAlignmentOptions.Center, FontStyles.Bold);
        catText.characterSpacing = 3f;

        spellPanel.gameObject.SetActive(false); // el prefab se autoría como monstruo

        // ═══════════ Marco (borde) — UN SOLO color, relleno con TEXTURA METÁLICA ═══════════
        // La FORMA del marco (esquinas cortadas, doble línea) actúa de MÁSCARA: solo se
        // ve la textura metálica donde está la forma. Así el marco se "rellena" con la
        // imagen de metal (cámbiala por `metal_gold.png` por tu propia textura).
        // Se llama "Border" (no "Frame") para que CardHoloEffect.Awake no lo tome.
        var frame = Rect(rootRT, "Border", V(0, 0), V(1, 1), Vector2.zero, Vector2.zero);
        var frameImg = AddImg(frame, Color.white);
        frameImg.sprite = frameSprite;                 // forma (estencil de la máscara)
        frameImg.type = Image.Type.Simple;
        frameImg.raycastTarget = false;
        var mask = frame.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;                  // la forma no se dibuja; solo recorta

        // Relleno metálico: se ve SOLO dentro de la forma del marco.
        var frameFill = AddImg(Rect(frame, "FrameFill", V(0, 0), V(1, 1), Vector2.zero, Vector2.zero),
                               Color.white);
        frameFill.sprite = metalSprite;
        frameFill.type = Image.Type.Simple;
        frameFill.raycastTarget = false;

        // ═══════════ FOIL de CRISTAL (ultra rara): cubre TODA la carta ═══════════
        // Va de ÚLTIMO hijo (sobre TODO, incluido el arte y el marco). El sprite es la
        // silueta (card_base) → su alfa enmascara el foil a la forma de la carta. Esquirlas
        // iridiscentes + brillo diagonal que las recorre; el shader lo anima solo (_Time).
        var foil = Rect(rootRT, "Foil", V(0, 0), V(1, 1), Vector2.zero, Vector2.zero);
        var foilImg = AddImg(foil, Color.white);
        foilImg.sprite = cardBaseSprite;
        foilImg.type = Image.Type.Simple;
        foilImg.raycastTarget = false;
        if (foilMat != null) foilImg.material = foilMat;
        foil.gameObject.SetActive(false); // lo enciende CardV2Effects (ultra rara)

        // ═══════════ Cableado de componentes (reflexión) ═══════════
        // CardHoloEffect NO se cablea (sin matHolo/matRainbowBorder/frameBorder):
        // queda inerte y la carta no muestra los efectos holo del prefab clásico.
        Set(display, "artImage", artImg);
        Set(display, "cardBack", backImg);
        Set(display, "frameBorder", frameImg);
        Set(display, "statsPanel", front.gameObject);
        Set(display, "nameText", nameText);
        Set(display, "atkText", atkText);
        Set(display, "defText", defText);
        Set(display, "attributeIcon", attrIcon);
        Set(display, "typeIcon", typeIcon);
        Set(display, "iconConfig", iconCfg);
        Set(display, "levelStarsContainer", stars);
        Set(display, "levelStarSprite", starSprite);
        Set(display, "levelNumberText", lvlNum);
        Set(display, "subtypeText", subtype);
        Set(display, "monsterOverlay", overlay.gameObject);
        // La carta NO muestra el efecto (según la referencia): effectText queda sin cablear.

        // Cara de no-monstruo. spellEquipDescriptionText queda sin cablear a propósito:
        // la magia es arte + nombre + categoría (CardDisplay se salta la descripción).
        Set(display, "spellEquipPanel", spellPanel.gameObject);
        Set(display, "spellEquipNameText", spellNameText);
        Set(display, "categoryBadge", catText);

        Set(slot, "cardDisplay", display);

        // ── Efectos V2: viñeta (siempre) + foil de cristal (ultra rara) ──
        var fx = root.AddComponent<CardV2Effects>();
        Set(fx, "vignette", vignetteImg);
        if (foilMat != null) Set(fx, "foil", foilImg); // sin material (shader ausente) → no se activa

        // ── Guardar prefab ────────────────────────────────────────────────
        System.IO.Directory.CreateDirectory("Assets/Resources/Prefabs");
        var saved = PrefabUtility.SaveAsPrefabAsset(root, OutPath, out bool ok);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (ok && saved != null)
        {
            EditorGUIUtility.PingObject(saved);
            Selection.activeObject = saved;
            Debug.Log($"CardMonsterV2PrefabBuilder: prefab creado en {OutPath}.");
            EditorUtility.DisplayDialog("Carta Monstruo V2",
                "Nuevo prefab creado en:\n" + OutPath +
                "\n\nEl prefab clásico (Card.prefab) queda INTACTO.\n" +
                "Usa el mismo CardDisplay, así que sirve de drop-in.\n\n" +
                "Estilo plano + placeholders: cambia el sprite del marco/insignias y\n" +
                "el de la estrella (Assets/Sprites/level_star.png) por tu arte cuando quieras.",
                "OK");
        }
        else
        {
            Debug.LogError($"CardMonsterV2PrefabBuilder: no se pudo guardar el prefab en {OutPath}.");
        }
    }

    // ════════════════════════ Bloques ════════════════════════

    /// <summary>Caja "ATK 3000" / "DEF 2500"; devuelve el TMP del valor.</summary>
    static TMP_Text StatBox(RectTransform parent, string label, float xMin, float xMax, Color c)
    {
        var box = Rect(parent, "Box_" + label, V(xMin, 0f), V(xMax, 1f), Vector2.zero, Vector2.zero);
        AddImg(box, PanelSoft).raycastTarget = false;
        var lbl = Label(box, "L", label, 9, Muted, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(lbl, V(0, 0), V(0.42f, 1f), new Vector2(8, 0), Vector2.zero);
        lbl.characterSpacing = 1f;
        var val = Label(box, "V", "0", 15, c, TextAlignmentOptions.Right, FontStyles.Bold);
        Place(val, V(0.30f, 0), V(1, 1), Vector2.zero, new Vector2(-8, 0));
        return val;
    }

    // ════════════════════════ Estrella (asset) ════════════════════════

    static Sprite EnsureStarSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(StarSpritePath);
        if (existing != null) return existing;

        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var px = new Color32[S * S];
        Vector2 ctr = new Vector2(S / 2f, S / 2f);
        float outer = S * 0.47f, inner = outer * 0.42f;
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float ang = Mathf.Deg2Rad * (-90f + i * 36f);
            float r = (i % 2 == 0) ? outer : inner;
            pts[i] = ctr + new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
        }
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                px[y * S + x] = PointInPoly(new Vector2(x + 0.5f, y + 0.5f), pts)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
        tex.SetPixels32(px); tex.Apply();

        System.IO.Directory.CreateDirectory("Assets/Sprites");
        System.IO.File.WriteAllBytes(StarSpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(StarSpritePath);

        var imp = (TextureImporter)AssetImporter.GetAtPath(StarSpritePath);
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(StarSpritePath);
    }

    // ════════════════════════ Marco/base con esquinas cortadas ════════════════════════
    // Se generan al ASPECTO de la carta (2× de 200×280) y se usan como Image SIMPLE
    // (estirada 1:1), NO 9-slice: así el chaflán y las líneas NO se deforman en las
    // esquinas (el problema anterior era estirar un sprite cuadrado a una carta alta).
    const int FrameW = 400, FrameH = 560;
    const float FrameChamfer = 28f; // ≈ 14 px de carta

    /// <summary>Marco de esquinas cortadas: doble línea (exterior gruesa + interior fina).</summary>
    static Sprite EnsureFrameSprite() => SaveTex(FrameSpritePath, FrameW, FrameH, (x, y) =>
    {
        float d = OctaDist(x, y, FrameW, FrameH, FrameChamfer);
        bool line = (d >= 3f && d <= 7f)      // línea exterior (~2 px de carta)
                 || (d >= 12f && d <= 14f);    // línea interior (~1 px de carta)
        return line ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
    });

    /// <summary>Silueta rellena con las mismas esquinas cortadas (fondo obsidiana).</summary>
    static Sprite EnsureCardBaseSprite() => SaveTex(CardBasePath, FrameW, FrameH, (x, y) =>
        OctaDist(x, y, FrameW, FrameH, FrameChamfer) >= 0f
            ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0));

    /// <summary>
    /// Textura metálica (oro) para RELLENAR el marco: gradiente vertical con dos bandas
    /// de brillo + vetas "cepilladas". Cámbiala por tu propia imagen de metal cuando la
    /// tengas (basta reemplazar el sprite de FrameFill / este PNG).
    /// </summary>
    static Sprite EnsureMetalSprite() => SaveTex(MetalTexPath, 96, 280, (x, y) =>
    {
        float v = y / 279f;                                            // 0 abajo .. 1 arriba
        float lum = 0.42f + 0.30f * v;                                 // más claro arriba
        lum += 0.50f * Mathf.Exp(-Mathf.Pow((v - 0.74f) / 0.05f, 2f)); // banda de brillo alta
        lum += 0.22f * Mathf.Exp(-Mathf.Pow((v - 0.30f) / 0.06f, 2f)); // reflejo secundario
        lum += (Mathf.PerlinNoise(x * 0.12f, y * 0.5f) - 0.5f) * 0.12f; // vetas cepilladas
        lum = Mathf.Clamp01(lum);
        Color c = lum < 0.5f
            ? Color.Lerp(new Color(0.32f, 0.22f, 0.07f), new Color(0.82f, 0.62f, 0.24f), lum * 2f)
            : Color.Lerp(new Color(0.82f, 0.62f, 0.24f), new Color(1f, 0.94f, 0.64f), (lum - 0.5f) * 2f);
        return new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255);
    });

    /// <summary>
    /// Distancia (px) al borde del octágono (rectángulo con las 4 esquinas cortadas a
    /// 45°). >0 dentro, 0 en el borde. Vale para texturas NO cuadradas.
    /// </summary>
    static float OctaDist(int x, int y, int W, int H, float chamfer)
    {
        float hw = (W - 1) / 2f, hh = (H - 1) / 2f;
        float ex = hw - Mathf.Abs(x - hw);   // dist. al lado vertical
        float ey = hh - Mathf.Abs(y - hh);   // dist. al lado horizontal
        float dCut = (ex + ey - chamfer) / 1.41421356f; // dist. perpendicular al chaflán
        return Mathf.Min(Mathf.Min(ex, ey), dCut);
    }

    /// <summary>Genera (una vez) un PNG WxH desde una función por-píxel y lo importa como Sprite.</summary>
    static Sprite SaveTex(string path, int W, int H, System.Func<int, int, Color32> fn)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                px[y * W + x] = fn(x, y);
        tex.SetPixels32(px); tex.Apply();

        System.IO.Directory.CreateDirectory("Assets/Sprites");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ════════════════════════ Sprites/materiales de efecto (asset) ════════════════════════

    // Máscara radial: centro transparente, bordes opacos (viñeta, se tiñe de negro).
    static Sprite EnsureVignetteSprite() => SavePng("Assets/Sprites/vignette.png", 128, (x, y) =>
    {
        float dx = (x - 63.5f) / 63.5f, dy = (y - 63.5f) / 63.5f;
        float a = Mathf.Clamp01(Mathf.SmoothStep(0.55f, 1.05f, Mathf.Sqrt(dx * dx + dy * dy)));
        return new Color32(255, 255, 255, (byte)(a * 255));
    });

    static Material EnsureFoilMaterial() => EnsureFxMaterial(FoilMatPath, "YGO/CardV2Foil", m =>
    {
        m.SetFloat("_Cells", 9f);           // tamaño de las esquirlas
        m.SetFloat("_Base", 0.06f);         // cristal base (siempre visible, sutil)
        m.SetFloat("_Saturation", 0.55f);
        m.SetFloat("_SweepIntensity", 0.45f); // brillo del barrido diagonal
        m.SetFloat("_SweepWidth", 0.14f);
        m.SetFloat("_SweepSpeed", 0.25f);
        m.SetFloat("_Crack", 0.5f);         // grietas entre esquirlas
    });

    // Crea (o carga) un material del shader dado y le aplica ajustes (aunque ya existiera).
    static Material EnsureFxMaterial(string path, string shaderName, System.Action<Material> tune)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"CardMonsterV2PrefabBuilder: shader '{shaderName}' no encontrado (¿compiló su .shader?).");
                return null;
            }
            mat = new Material(shader);
            System.IO.Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(mat, path);
        }
        tune(mat);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    /// <summary>Genera (una vez) un PNG a partir de una función por-píxel y lo importa como Sprite.</summary>
    static Sprite SavePng(string path, int S, System.Func<int, int, Color32> fn)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                px[y * S + x] = fn(x, y);
        tex.SetPixels32(px); tex.Apply();

        System.IO.Directory.CreateDirectory("Assets/Sprites");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static bool PointInPoly(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) /
                       (poly[j].y - poly[i].y) + poly[i].x))
                inside = !inside;
        }
        return inside;
    }

    // ════════════════════════ Helpers de UI ════════════════════════

    static Vector2 V(float x, float y) => new Vector2(x, y);

    static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax,
                              Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
        rt.localScale = Vector3.one;
        return rt;
    }

    static void Place(Component c, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        var rt = (RectTransform)c.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    static Image AddImg(RectTransform rt, Color c)
    {
        var img = rt.gameObject.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
        img.color = c;
        return img;
    }

    static void Accent(RectTransform panel, bool bottom = false)
    {
        var rt = Rect(panel, "Accent",
            bottom ? V(0, 0) : V(0, 1), bottom ? V(1, 0) : V(1, 1), Vector2.zero, Vector2.zero);
        rt.sizeDelta = new Vector2(0, 2);
        rt.pivot = new Vector2(0.5f, bottom ? 0f : 1f);
        rt.anchoredPosition = Vector2.zero;
        AddImg(rt, Line).raycastTarget = false;
    }

    static void Outline(RectTransform rt, Color c)
    {
        var o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(c.r, c.g, c.b, 0.5f);
        o.effectDistance = new Vector2(1f, -1f);
    }

    static TMP_Text Label(Transform parent, string name, string text, float size, Color color,
                          TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        var rt = (RectTransform)t.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return t;
    }

    // ── Reflexión ────────────────────────────────────────────────────────
    static T Load<T>(string guid) where T : Object
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) { Debug.LogWarning($"CardMonsterV2PrefabBuilder: GUID {guid} sin ruta."); return null; }
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null) Debug.LogWarning($"CardMonsterV2PrefabBuilder: no se cargó {typeof(T).Name} en {path}.");
        return a;
    }

    static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) { Debug.LogWarning($"CardMonsterV2PrefabBuilder: campo '{field}' no existe en {target.GetType().Name}"); return; }
        f.SetValue(target, value);
    }
}
