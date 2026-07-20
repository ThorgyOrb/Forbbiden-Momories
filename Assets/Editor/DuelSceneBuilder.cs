using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Construye la escena de DUELO 3D desde cero:
///
///   MUNDO 3D — mesa teñible por terreno, marcadores de las 4 filas × 5 slots,
///   cámara con punto de intro (cenital) y punto de juego, puntos de spawn/
///   fusión/descarte, y la plantilla de carta física (canvas en World Space con
///   el prefab Card COMPLETO + letrero de stats + resaltado + collider de clic).
///
///   OVERLAY 2D — mano con cartas completas (prefab Card), LP/fase/turno/log,
///   paneles de acción / Estrella Guardiana / campo, botones de fase, intro de
///   duelistas y resultado (banner + estadísticas + premio).
///
/// Todo queda como objetos reales editables y con las referencias de
/// <see cref="DuelScreen"/>, <see cref="DuelBoard3D"/> y
/// <see cref="DuelController"/> cableadas.
/// </summary>
public static class DuelSceneBuilder
{
    // Paleta (idéntica al resto de builders)
    static readonly Color BgColor     = new Color(0.043f, 0.05f, 0.10f);
    static readonly Color Gold        = new Color(0.86f, 0.72f, 0.35f);
    static readonly Color GoldBright  = new Color(0.98f, 0.85f, 0.45f);
    static readonly Color TextLight   = new Color(0.93f, 0.94f, 0.98f);
    static readonly Color BtnNormal   = new Color(0.11f, 0.13f, 0.24f, 0.92f);
    static readonly Color BtnHover    = new Color(0.20f, 0.24f, 0.42f, 0.98f);
    static readonly Color BtnPressed  = new Color(0.16f, 0.19f, 0.34f, 1f);
    static readonly Color BtnDisabled = new Color(0.10f, 0.11f, 0.16f, 0.6f);
    static readonly Color PanelFill   = new Color(0.10f, 0.11f, 0.20f, 0.96f);
    static readonly Color PanelDim    = new Color(0f, 0f, 0f, 0.82f);

    // Geometría del tablero: 5 columnas × 4 filas repartidas en DOS BLOQUES
    // (rival arriba / jugador abajo) separados por una franja central (altar).
    const float SlotSpacingX = 2.0f;                 // separación entre columnas
    const float TileSize     = 1.95f;                // lado X de la casilla (junta fina)
    const float ZScale       = 1.5f;                 // campo ALARGADO en Z (tablero 1.5× más largo)
    const float CardWidth    = 1.5f;                 // ancho físico de una carta
    // Filas: oppSpell, oppMon | (franja central) | playMon, playSpell.
    // Base {4,2,-2,-4} × ZScale → hueco central para la franja del hexagrama.
    static readonly float[] RowZ = { 4.0f * ZScale, 2.0f * ZScale, -2.0f * ZScale, -4.0f * ZScale };

    // Colores de la mesa (damero dorado tipo altar de Forbidden Memories).
    static readonly Color TileLight     = new Color(0.88f, 0.74f, 0.42f);
    static readonly Color TileDark      = new Color(0.72f, 0.58f, 0.30f);
    static readonly Color TableBase     = new Color(0.30f, 0.21f, 0.10f); // junta cálida bajo las casillas
    static readonly Color Stone         = new Color(0.36f, 0.27f, 0.16f); // cuerpo del altar
    static readonly Color StoneDark     = new Color(0.25f, 0.18f, 0.11f);
    static readonly Color StoneEngraved = new Color(0.44f, 0.33f, 0.18f); // rieles laterales grabados
    static readonly Color CenterGold    = new Color(0.80f, 0.66f, 0.34f); // franja central del altar

    const string CardPrefabPath = "Assets/Resources/Prefabs/CardMonsterV2.prefab";

    public static void BuildInScene(DuelController controller)
    {
        Scene scene = controller.gameObject.scene;

        DestroyRoot(scene, "DuelCanvas");
        DestroyRoot(scene, "DuelBoard");
        EnsureEventSystem(scene);

        var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (cardPrefab == null)
            Debug.LogError($"DuelSceneBuilder: no se encontró el prefab de carta en {CardPrefabPath}.");

        // ═════════════════════════════ MUNDO 3D ═════════════════════════════

        var boardGO = new GameObject("DuelBoard", typeof(DuelBoard3D));
        MoveToScene(boardGO, scene);
        var board = boardGO.GetComponent<DuelBoard3D>();

        // ── Cámara (la de la escena) + puntos de intro/juego ─────────────
        Camera cam = Object.FindObjectOfType<Camera>();
        if (cam == null)
        {
            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            MoveToScene(camGO, scene);
            cam = camGO.GetComponent<Camera>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        cam.fieldOfView = 45f;

        // ── Luz principal (desde arriba y ligeramente a la izquierda) ─────
        // La cinemática de entrada la apaga y la reenciende al emerger la mesa.
        Light keyLight = Object.FindObjectOfType<Light>();
        if (keyLight == null)
        {
            var lightGO = new GameObject("Directional Light", typeof(Light));
            MoveToScene(lightGO, scene);
            keyLight = lightGO.GetComponent<Light>();
        }
        keyLight.type = LightType.Directional;
        keyLight.color = new Color(1f, 0.96f, 0.84f);   // blanco cálido
        keyLight.intensity = 1.15f;
        keyLight.shadows = LightShadows.Soft;
        keyLight.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

        // Vista cenital (intro) → vista de juego (mirando la mesa de frente,
        // recibiendo el tablero en perspectiva como en Forbidden Memories).
        var introPoint = MakePoint(boardGO.transform, "CameraIntroPoint",
            new Vector3(0f, 16f, 0.4f), Quaternion.Euler(90f, 0f, 0f));
        var playPoint = MakePoint(boardGO.transform, "CameraPlayPoint",
            new Vector3(-0.2f, 3.2f, -16f), Quaternion.Euler(17.4f, 1f, 0.4f));

        // ── Mesa/altar 3D (pedestal + damero + bordes + adornos) ──────────
        var tableRoot = BuildTable(boardGO.transform, out var groundRenderer);

        // ── Anclas de slots (sobre las casillas) ──────────────────────────
        var oppSpellAnchors  = MakeSlotRow(boardGO.transform, "OppSpellAnchors", RowZ[0]);
        var oppMonAnchors    = MakeSlotRow(boardGO.transform, "OppMonsterAnchors", RowZ[1]);
        var playMonAnchors   = MakeSlotRow(boardGO.transform, "PlayerMonsterAnchors", RowZ[2]);
        var playSpellAnchors = MakeSlotRow(boardGO.transform, "PlayerSpellAnchors", RowZ[3]);

        // Rombo indicador en las casillas de monstruo (vacías), estilo FM.
        BuildDiamondMarkers(boardGO.transform, RowZ[1]);
        BuildDiamondMarkers(boardGO.transform, RowZ[2]);

        // ── Puntos de animación ───────────────────────────────────────────
        var playerSpawn = MakePoint(boardGO.transform, "PlayerSpawnPoint", new Vector3(0f, 1.2f, -9.6f), Quaternion.identity);
        var oppSpawn    = MakePoint(boardGO.transform, "OpponentSpawnPoint", new Vector3(0f, 1.2f, 9.8f), Quaternion.identity);
        var fusionPoint = MakePoint(boardGO.transform, "FusionPoint", new Vector3(0f, 3.6f, -2.4f), Quaternion.identity);
        var discard     = MakePoint(boardGO.transform, "DiscardPoint", new Vector3(11f, 2.2f, 0.5f), Quaternion.identity);

        // ── Plantilla de carta física ─────────────────────────────────────
        var cardTemplate = BuildCard3DTemplate(boardGO.transform, cardPrefab);
        cardTemplate.gameObject.SetActive(false);

        // ── Cablear DuelBoard3D ───────────────────────────────────────────
        var bso = new SerializedObject(board);
        Set(bso, "mainCamera", cam);
        Set(bso, "cameraIntroPoint", introPoint);
        Set(bso, "cameraPlayPoint", playPoint);
        Set(bso, "keyLight", keyLight);
        Set(bso, "tableRoot", tableRoot);
        SetArray(bso, "opponentSpellAnchors", oppSpellAnchors);
        SetArray(bso, "opponentMonsterAnchors", oppMonAnchors);
        SetArray(bso, "playerMonsterAnchors", playMonAnchors);
        SetArray(bso, "playerSpellAnchors", playSpellAnchors);
        Set(bso, "playerSpawnPoint", playerSpawn);
        Set(bso, "opponentSpawnPoint", oppSpawn);
        Set(bso, "fusionPoint", fusionPoint);
        Set(bso, "discardPoint", discard);
        Set(bso, "cardTemplate", cardTemplate);
        Set(bso, "groundRenderer", groundRenderer);
        bso.ApplyModifiedPropertiesWithoutUndo();

        // ═════════════════════════════ OVERLAY 2D ════════════════════════════

        var canvasGO = new GameObject("DuelCanvas", typeof(Canvas), typeof(CanvasScaler),
                                      typeof(GraphicRaycaster), typeof(ResponsiveCanvasScaler), typeof(DuelScreen));
        MoveToScene(canvasGO, scene);
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var canvasRT = canvasGO.GetComponent<RectTransform>();
        var duelScreen = canvasGO.GetComponent<DuelScreen>();

        // ── HUD: caja de CAMPO (arriba-izquierda) ─────────────────────────
        var fieldBox = MakeHudBox(canvasRT, "FieldBox", 0.008f, 0.86f, 0.170f, 0.986f);
        var campoLabel = MakeText("CampoLabel", fieldBox, "CAMPO", 22, Gold, TextAlignmentOptions.Center);
        campoLabel.fontStyle = FontStyles.Bold;
        Anchor(campoLabel.rectTransform, 0.04f, 0.52f, 0.96f, 0.96f);
        var terrainTxt = MakeText("TerrainText", fieldBox, "—", 30, TextLight, TextAlignmentOptions.Center);
        terrainTxt.fontStyle = FontStyles.Bold;
        Anchor(terrainTxt.rectTransform, 0.04f, 0.04f, 0.96f, 0.52f);

        // ── HUD: nombre rival + fase + turno (centro) ─────────────────────
        var oppName = MakeText("OpponentName", canvasRT, "Rival", 30, GoldBright, TextAlignmentOptions.Center);
        oppName.fontStyle = FontStyles.Bold;
        Anchor(oppName.rectTransform, 0.32f, 0.955f, 0.68f, 0.995f);

        var phase = MakeText("PhaseText", canvasRT, "Preparación", 34, Gold, TextAlignmentOptions.Center);
        phase.fontStyle = FontStyles.Bold;
        Anchor(phase.rectTransform, 0.32f, 0.905f, 0.68f, 0.955f);

        var turn = MakeText("TurnText", canvasRT, "", 24, TextLight, TextAlignmentOptions.Center);
        Anchor(turn.rectTransform, 0.32f, 0.865f, 0.68f, 0.905f);

        // ── HUD: caja de LP (arriba-derecha) — COM (rival) / TÚ (jugador) ──
        var lpBox = MakeHudBox(canvasRT, "LPBox", 0.79f, 0.86f, 0.992f, 0.986f);
        // Fila COM (rival)
        MakeChip(lpBox, "COM", new Color(0.20f, 0.30f, 0.62f), 0.02f, 0.52f, 0.27f, 0.96f);
        var oppLP = MakeText("OpponentLP", lpBox, "8000", 30, TextLight, TextAlignmentOptions.Right);
        oppLP.fontStyle = FontStyles.Bold;
        Anchor(oppLP.rectTransform, 0.29f, 0.52f, 0.75f, 0.96f);
        var oppCount = MakeText("OpponentCount", lpBox, "40", 22, new Color(0.78f, 0.80f, 0.9f), TextAlignmentOptions.Right);
        Anchor(oppCount.rectTransform, 0.76f, 0.52f, 0.98f, 0.96f);
        // Fila TÚ (jugador)
        MakeChip(lpBox, "TÚ", new Color(0.66f, 0.22f, 0.24f), 0.02f, 0.04f, 0.27f, 0.48f);
        var playerLP = MakeText("PlayerLP", lpBox, "8000", 30, GoldBright, TextAlignmentOptions.Right);
        playerLP.fontStyle = FontStyles.Bold;
        Anchor(playerLP.rectTransform, 0.29f, 0.04f, 0.75f, 0.48f);
        var playerCount = MakeText("PlayerCount", lpBox, "40", 22, new Color(0.78f, 0.80f, 0.9f), TextAlignmentOptions.Right);
        Anchor(playerCount.rectTransform, 0.76f, 0.04f, 0.98f, 0.48f);

        // ── Log (izquierda-abajo) ─────────────────────────────────────────
        var logPanel = NewImage("LogPanel", canvasRT, new Color(0f, 0f, 0f, 0.42f));
        Anchor(logPanel.rectTransform, 0.005f, 0.26f, 0.20f, 0.60f);
        var logTxt = MakeText("LogText", logPanel.transform, "", 19, TextLight, TextAlignmentOptions.BottomLeft);
        Stretch(logTxt.rectTransform);
        logTxt.margin = new Vector4(10, 8, 10, 8);
        logTxt.enableWordWrapping = true;

        // ── Mano (cartas completas, sobre la barra de info) ───────────────
        var handPanel = new GameObject("Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        handPanel.transform.SetParent(canvasRT, false);
        Anchor((RectTransform)handPanel.transform, 0.14f, 0.105f, 0.86f, 0.33f);
        var hhlg = handPanel.GetComponent<HorizontalLayoutGroup>();
        hhlg.spacing = 12; hhlg.childAlignment = TextAnchor.LowerCenter;
        hhlg.childControlWidth = false; hhlg.childControlHeight = false;
        hhlg.childForceExpandWidth = false; hhlg.childForceExpandHeight = false;

        var handTemplate = BuildHandTemplate(handPanel.transform, cardPrefab);
        handTemplate.gameObject.SetActive(false);

        // ── Barra de info de carta (borde inferior, estilo FM) ────────────
        var infoBar = MakeHudBox(canvasRT, "InfoBar", 0.0f, 0.0f, 1.0f, 0.10f);
        var infoName = MakeText("InfoName", infoBar, "", 34, GoldBright, TextAlignmentOptions.Left);
        infoName.fontStyle = FontStyles.Bold;
        Anchor(infoName.rectTransform, 0.02f, 0.08f, 0.44f, 0.92f);
        var infoStats = MakeText("InfoStats", infoBar, "", 30, TextLight, TextAlignmentOptions.Center);
        Anchor(infoStats.rectTransform, 0.45f, 0.08f, 0.66f, 0.92f);
        var infoAttr = NewImage("InfoAttr", infoBar, Color.white);
        infoAttr.preserveAspect = true; infoAttr.enabled = false;
        Anchor(infoAttr.rectTransform, 0.67f, 0.15f, 0.715f, 0.85f);
        var infoType = NewImage("InfoType", infoBar, Color.white);
        infoType.preserveAspect = true; infoType.enabled = false;
        Anchor(infoType.rectTransform, 0.72f, 0.15f, 0.765f, 0.85f);
        var infoStar = MakeText("InfoStar", infoBar, "", 28, Gold, TextAlignmentOptions.Center);
        Anchor(infoStar.rectTransform, 0.77f, 0.08f, 0.92f, 0.92f);
        var infoLevel = MakeText("InfoLevel", infoBar, "", 28, TextLight, TextAlignmentOptions.Right);
        Anchor(infoLevel.rectTransform, 0.92f, 0.08f, 0.985f, 0.92f);

        var iconConfig = AssetDatabase.LoadAssetAtPath<CardIconConfig>("Assets/Scripts/CardIconConfig.asset");

        // ── Botones Main Phase (derecha) ──────────────────────────────────
        var mainBtns = MakeButtonColumn(canvasRT, "MainButtons", 0.80f, 0.30f, 0.995f, 0.62f);
        var btnFuse    = MakeButton("Btn_Fusionar", mainBtns.transform, "Fusionar");
        var btnConfirm = MakeButton("Btn_ConfirmarFusion", mainBtns.transform, "Confirmar Fusión");
        var btnBattle  = MakeButton("Btn_IrABatalla", mainBtns.transform, "Ir a Batalla");
        var btnEndTurn = MakeButton("Btn_TerminarTurno", mainBtns.transform, "Terminar Turno");

        // ── Botones Battle Phase ──────────────────────────────────────────
        var battleBtns = MakeButtonColumn(canvasRT, "BattleButtons", 0.80f, 0.34f, 0.995f, 0.54f);
        var btnDirect    = MakeButton("Btn_AtaqueDirecto", battleBtns.transform, "Ataque Directo");
        var btnEndBattle = MakeButton("Btn_TerminarBatalla", battleBtns.transform, "Terminar Turno");
        battleBtns.SetActive(false);

        // ── Panel de acción (izquierda) ───────────────────────────────────
        var (actionPanel, actionTitle, actionButtons) = MakeContextPanel(canvasRT, "ActionPanel");
        var btnSumAtk  = MakeButton("Btn_InvocarATK", actionButtons, "Boca arriba — Ataque");
        var btnSumDef  = MakeButton("Btn_InvocarDEF", actionButtons, "Boca arriba — Defensa");
        var btnSetAtk  = MakeButton("Btn_SetATK", actionButtons, "Boca abajo — Ataque");
        var btnSetDef  = MakeButton("Btn_SetDEF", actionButtons, "Boca abajo — Defensa");
        var btnCast    = MakeButton("Btn_JugarMagia", actionButtons, "Jugar Magia");
        var btnTrap    = MakeButton("Btn_ColocarTrampa", actionButtons, "Colocar Trampa");
        var btnCancelA = MakeButton("Btn_Cancelar", actionButtons, "Cancelar");
        actionPanel.SetActive(false);

        // ── Panel de Estrella Guardiana (central, modal) ──────────────────
        var starPanel = NewImage("StarPanel", canvasRT, PanelFill).gameObject;
        var starRT = (RectTransform)starPanel.transform;
        starRT.anchorMin = starRT.anchorMax = starRT.pivot = new Vector2(0.5f, 0.5f);
        starRT.sizeDelta = new Vector2(640, 380);
        var starLayout = starPanel.AddComponent<VerticalLayoutGroup>();
        starLayout.padding = new RectOffset(30, 30, 24, 24); starLayout.spacing = 14;
        starLayout.childControlWidth = true; starLayout.childControlHeight = true;
        starLayout.childForceExpandWidth = true; starLayout.childForceExpandHeight = false;
        var starTitle = MakeText("Title", starPanel.transform, "Elige Estrella Guardiana", 32, GoldBright, TextAlignmentOptions.Center);
        starTitle.fontStyle = FontStyles.Bold;
        starTitle.gameObject.AddComponent<LayoutElement>().minHeight = 92;
        var btnStarA = MakeButton("Btn_StarA", starPanel.transform, "★ Estrella A");
        var btnStarB = MakeButton("Btn_StarB", starPanel.transform, "★ Estrella B");
        var btnStarCancel = MakeButton("Btn_CancelarStar", starPanel.transform, "Cancelar");
        starPanel.SetActive(false);

        // ── Panel de campo (izquierda) ────────────────────────────────────
        var (fieldPanel, fieldTitle, fieldButtons) = MakeContextPanel(canvasRT, "FieldPanel");
        var btnChangePos = MakeButton("Btn_CambiarPosicion", fieldButtons, "Cambiar Posición");
        var btnReveal    = MakeButton("Btn_Revelar", fieldButtons, "Revelar");
        var btnCancelF   = MakeButton("Btn_CancelarCampo", fieldButtons, "Cancelar");
        fieldPanel.SetActive(false);

        // ── Intro de duelistas ────────────────────────────────────────────
        var intro = NewImage("IntroPanel", canvasRT, new Color(0f, 0f, 0f, 0.55f));
        Stretch(intro.rectTransform);
        var introPortrait = NewImage("Portrait", intro.transform, Color.white);
        introPortrait.preserveAspect = true;
        var ipRT = introPortrait.rectTransform;
        ipRT.anchorMin = ipRT.anchorMax = ipRT.pivot = new Vector2(0.5f, 0.62f);
        ipRT.sizeDelta = new Vector2(300, 300);
        var introName = MakeText("Name", intro.transform, "Rival", 60, GoldBright, TextAlignmentOptions.Center);
        introName.fontStyle = FontStyles.Bold;
        Anchor(introName.rectTransform, 0.10f, 0.30f, 0.90f, 0.42f);
        var introVs = MakeText("Duelo", intro.transform, "— ¡DUELO! —", 38, Gold, TextAlignmentOptions.Center);
        Anchor(introVs.rectTransform, 0.10f, 0.20f, 0.90f, 0.29f);
        intro.gameObject.SetActive(false);

        // ── Banner de resultado (animado por código) ──────────────────────
        var banner = new GameObject("ResultBanner", typeof(RectTransform));
        banner.transform.SetParent(canvasRT, false);
        Stretch((RectTransform)banner.transform);
        var bannerText = MakeText("Text", banner.transform, "¡VICTORIA!", 130, GoldBright, TextAlignmentOptions.Center);
        bannerText.fontStyle = FontStyles.Bold;
        var btRT = bannerText.rectTransform;
        btRT.anchorMin = btRT.anchorMax = btRT.pivot = new Vector2(0.5f, 0.55f);
        btRT.sizeDelta = new Vector2(1600, 260);
        banner.SetActive(false);

        // ── Panel de resultado (estadísticas + premio) ────────────────────
        var result = NewImage("ResultPanel", canvasRT, PanelDim);
        Stretch(result.rectTransform);

        var resultCard = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        resultCard.transform.SetParent(result.transform, false);
        var rcRT = resultCard.GetComponent<RectTransform>();
        rcRT.anchorMin = rcRT.anchorMax = rcRT.pivot = new Vector2(0.5f, 0.5f);
        rcRT.sizeDelta = new Vector2(780, 780);
        resultCard.GetComponent<Image>().color = PanelFill;
        var rvlg = resultCard.GetComponent<VerticalLayoutGroup>();
        rvlg.padding = new RectOffset(38, 38, 28, 28); rvlg.spacing = 12;
        rvlg.childControlWidth = true; rvlg.childControlHeight = true;
        rvlg.childForceExpandWidth = true; rvlg.childForceExpandHeight = false;
        rvlg.childAlignment = TextAnchor.UpperCenter;

        var resultTitle = MakeText("Title", resultCard.transform, "Resultado del duelo", 44, GoldBright, TextAlignmentOptions.Center);
        resultTitle.fontStyle = FontStyles.Bold;
        resultTitle.gameObject.AddComponent<LayoutElement>().minHeight = 60;

        var statsTxt = MakeText("Stats", resultCard.transform, "", 27, TextLight, TextAlignmentOptions.TopLeft);
        statsTxt.gameObject.AddComponent<LayoutElement>().minHeight = 210;

        var rankTxt = MakeText("Rank", resultCard.transform, "", 30, Gold, TextAlignmentOptions.Center);
        rankTxt.gameObject.AddComponent<LayoutElement>().minHeight = 44;

        var rewardGroup = new GameObject("RewardGroup", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        rewardGroup.transform.SetParent(resultCard.transform, false);
        rewardGroup.GetComponent<LayoutElement>().minHeight = 240;
        var rwvlg = rewardGroup.GetComponent<VerticalLayoutGroup>();
        rwvlg.spacing = 8; rwvlg.childAlignment = TextAnchor.MiddleCenter;
        rwvlg.childControlWidth = false; rwvlg.childControlHeight = false;
        var rewardArt = NewImage("RewardArt", rewardGroup.transform, Color.white);
        rewardArt.preserveAspect = true;
        rewardArt.rectTransform.sizeDelta = new Vector2(170, 170);
        var rewardName = MakeText("RewardName", rewardGroup.transform, "", 27, TextLight, TextAlignmentOptions.Center);
        rewardName.rectTransform.sizeDelta = new Vector2(680, 56);
        rewardGroup.SetActive(false);

        var resultBtnRow = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        resultBtnRow.transform.SetParent(resultCard.transform, false);
        resultBtnRow.GetComponent<LayoutElement>().minHeight = 74;
        var rblg = resultBtnRow.GetComponent<HorizontalLayoutGroup>();
        rblg.spacing = 16; rblg.childControlWidth = true; rblg.childControlHeight = true;
        rblg.childForceExpandWidth = true; rblg.childForceExpandHeight = true;
        var btnRematch = MakeButton("Btn_Revancha", resultBtnRow.transform, "Revancha");
        var btnMenu    = MakeButton("Btn_Menu", resultBtnRow.transform, "Volver al Menú");
        result.gameObject.SetActive(false);

        // ── Assets referenciados ──────────────────────────────────────────
        var fusionDb = AssetDatabase.LoadAssetAtPath<FusionDatabase>("Assets/Resources/Fusions/FusionDatabase.asset");
        var testConfig = AssetDatabase.LoadAssetAtPath<DuelConfig>("Assets/Resources/DuelOpponent/Heishin_DuelConfig.asset");

        // ── Cablear DuelScreen ────────────────────────────────────────────
        var so = new SerializedObject(duelScreen);
        Set(so, "opponentNameText", oppName);
        Set(so, "opponentLPText", oppLP);
        Set(so, "playerLPText", playerLP);
        Set(so, "opponentCountText", oppCount);
        Set(so, "playerCountText", playerCount);
        Set(so, "phaseText", phase);
        Set(so, "turnText", turn);
        Set(so, "terrainText", terrainTxt);
        Set(so, "logText", logTxt);
        Set(so, "handContainer", handPanel.transform);
        Set(so, "handTemplate", handTemplate);
        Set(so, "infoBar", infoBar.parent.gameObject);
        Set(so, "infoNameText", infoName);
        Set(so, "infoStatsText", infoStats);
        Set(so, "infoStarText", infoStar);
        Set(so, "infoLevelText", infoLevel);
        Set(so, "infoAttributeIcon", infoAttr);
        Set(so, "infoTypeIcon", infoType);
        Set(so, "iconConfig", iconConfig);
        Set(so, "actionPanel", actionPanel);
        Set(so, "actionTitleText", actionTitle);
        Set(so, "btnSummonAtk", btnSumAtk);
        Set(so, "btnSummonDef", btnSumDef);
        Set(so, "btnSetAtk", btnSetAtk);
        Set(so, "btnSetDef", btnSetDef);
        Set(so, "btnCastSpell", btnCast);
        Set(so, "btnSetTrap", btnTrap);
        Set(so, "btnCancelAction", btnCancelA);
        Set(so, "starPanel", starPanel);
        Set(so, "starTitleText", starTitle);
        Set(so, "btnStarA", btnStarA);
        Set(so, "btnStarB", btnStarB);
        Set(so, "btnCancelStar", btnStarCancel);
        Set(so, "fieldPanel", fieldPanel);
        Set(so, "fieldTitleText", fieldTitle);
        Set(so, "btnChangePosition", btnChangePos);
        Set(so, "btnReveal", btnReveal);
        Set(so, "btnCancelField", btnCancelF);
        Set(so, "mainButtons", mainBtns);
        Set(so, "btnFuse", btnFuse);
        Set(so, "btnConfirmFusion", btnConfirm);
        Set(so, "btnGoBattle", btnBattle);
        Set(so, "btnEndTurn", btnEndTurn);
        Set(so, "battleButtons", battleBtns);
        Set(so, "btnDirectAttack", btnDirect);
        Set(so, "btnEndBattle", btnEndBattle);
        Set(so, "introPanel", intro.gameObject);
        Set(so, "introNameText", introName);
        Set(so, "introPortrait", introPortrait);
        Set(so, "resultBanner", banner);
        Set(so, "resultBannerText", bannerText);
        Set(so, "resultPanel", result.gameObject);
        Set(so, "resultTitleText", resultTitle);
        Set(so, "statsText", statsTxt);
        Set(so, "rankText", rankTxt);
        Set(so, "rewardGroup", rewardGroup);
        Set(so, "rewardArt", rewardArt);
        Set(so, "rewardNameText", rewardName);
        Set(so, "btnRematch", btnRematch);
        Set(so, "btnBackMenu", btnMenu);
        so.ApplyModifiedPropertiesWithoutUndo();

        // ── Cablear DuelController ────────────────────────────────────────
        var cso = new SerializedObject(controller);
        Set(cso, "screen", duelScreen);
        Set(cso, "board", board);
        Set(cso, "fusionDb", fusionDb);
        Set(cso, "testConfig", testConfig);
        cso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("DuelSceneBuilder: escena de duelo 3D construida y cableada." +
                  (cardPrefab == null ? " AVISO: falta Card.prefab." : "") +
                  (fusionDb == null ? " AVISO: falta FusionDatabase." : ""));
    }

    // ── Carta física 3D (canvas de mundo + prefab Card completo) ─────────

    static Duel3DCardView BuildCard3DTemplate(Transform parent, GameObject cardPrefab)
    {
        var root = new GameObject("CardTemplate3D", typeof(Duel3DCardView), typeof(BoxCollider));
        root.transform.SetParent(parent, false);
        var collider = root.GetComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.12f, 0f);
        collider.size = new Vector3(CardWidth + 0.15f, 0.28f, CardWidth * 1.45f);

        // Canvas de la carta (yace sobre la mesa, X=90).
        var canvasGO = new GameObject("CardCanvas", typeof(Canvas), typeof(CanvasGroup));
        canvasGO.transform.SetParent(root.transform, false);
        canvasGO.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        canvasGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var canvasRT = (RectTransform)canvasGO.transform;

        Vector2 cardSize = new Vector2(300f, 420f);
        if (cardPrefab != null)
        {
            var cardInstance = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab);
            var cardRT = cardInstance.GetComponent<RectTransform>();
            if (cardRT != null && cardRT.sizeDelta.x > 50f && cardRT.sizeDelta.y > 50f)
                cardSize = cardRT.sizeDelta;

            canvasRT.sizeDelta = cardSize;
            cardInstance.transform.SetParent(canvasGO.transform, false);
            if (cardRT != null)
            {
                cardRT.anchorMin = cardRT.anchorMax = cardRT.pivot = new Vector2(0.5f, 0.5f);
                cardRT.anchoredPosition = Vector2.zero;
                cardRT.localScale = Vector3.one;
            }
        }
        else
        {
            canvasRT.sizeDelta = cardSize;
        }
        // Escala: el ancho de la carta = CardWidth unidades de mundo.
        canvasRT.localScale = Vector3.one * (CardWidth / cardSize.x);

        // Letrero flotante de stats actuales (inclinado hacia la cámara).
        var statsGO = new GameObject("StatsCanvas", typeof(Canvas));
        statsGO.transform.SetParent(root.transform, false);
        statsGO.transform.localPosition = new Vector3(0f, 0.55f, -1.35f);
        statsGO.transform.localRotation = Quaternion.Euler(48f, 0f, 0f);
        var statsCanvas = statsGO.GetComponent<Canvas>();
        statsCanvas.renderMode = RenderMode.WorldSpace;
        var statsRT = (RectTransform)statsGO.transform;
        statsRT.sizeDelta = new Vector2(600, 110);
        statsRT.localScale = Vector3.one * 0.0035f;

        var statsText = MakeText("Text", statsGO.transform, "", 56, GoldBright, TextAlignmentOptions.Center);
        statsText.fontStyle = FontStyles.Bold;
        Stretch(statsText.rectTransform);

        // Resaltado (placa dorada bajo la carta).
        var highlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        highlight.name = "Highlight";
        highlight.transform.SetParent(root.transform, false);
        highlight.transform.localPosition = new Vector3(0f, 0.008f, 0f);
        highlight.transform.localScale = new Vector3(CardWidth + 0.35f, 0.012f, CardWidth * 1.45f + 0.35f);
        Object.DestroyImmediate(highlight.GetComponent<Collider>());
        var hlMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.85f, 0.35f, 0.55f) };
        highlight.GetComponent<Renderer>().sharedMaterial = hlMat;
        highlight.SetActive(false);

        var view = root.GetComponent<Duel3DCardView>();
        var so = new SerializedObject(view);
        Set(so, "cardCanvas", canvasRT);
        Set(so, "canvasGroup", canvasGO.GetComponent<CanvasGroup>());
        Set(so, "statsLabel", statsText);
        Set(so, "highlight", highlight);
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // ── Carta de mano (overlay, prefab Card completo + botón) ────────────

    static DuelHandCardView BuildHandTemplate(Transform parent, GameObject cardPrefab)
    {
        var root = new GameObject("HandCardTemplate",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(DuelHandCardView));
        root.transform.SetParent(parent, false);
        var rootRT = (RectTransform)root.transform;
        rootRT.sizeDelta = new Vector2(210, 300);
        var le = root.GetComponent<LayoutElement>();
        le.preferredWidth = 210; le.preferredHeight = 300;

        var bgImg = root.GetComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.02f);   // casi invisible pero clicable
        var btn = root.GetComponent<Button>();
        btn.targetGraphic = bgImg;
        btn.transition = Selectable.Transition.None;

        if (cardPrefab != null)
        {
            var cardInstance = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab);
            cardInstance.transform.SetParent(root.transform, false);
            var cardRT = cardInstance.GetComponent<RectTransform>();
            Vector2 size = (cardRT != null && cardRT.sizeDelta.x > 50f) ? cardRT.sizeDelta : new Vector2(300, 420);
            if (cardRT != null)
            {
                cardRT.anchorMin = cardRT.anchorMax = cardRT.pivot = new Vector2(0.5f, 0.5f);
                cardRT.anchoredPosition = Vector2.zero;
                float fit = Mathf.Min(204f / size.x, 292f / size.y);
                cardRT.localScale = Vector3.one * fit;
            }
        }

        // Marco de selección.
        var hl = NewImage("Highlight", root.transform, new Color(1f, 0.85f, 0.35f, 0.30f));
        Stretch(hl.rectTransform);
        hl.raycastTarget = false;
        hl.gameObject.SetActive(false);

        var view = root.GetComponent<DuelHandCardView>();
        var so = new SerializedObject(view);
        Set(so, "button", btn);
        Set(so, "highlight", hl.gameObject);
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // ── Piezas 3D ─────────────────────────────────────────────────────────

    static Transform[] MakeSlotRow(Transform parent, string name, float z)
    {
        var row = new GameObject(name).transform;
        row.SetParent(parent, false);

        // Solo anclas lógicas: la superficie visible la ponen las casillas del
        // damero (BuildTable). Las cartas se posan un pelo por encima.
        var anchors = new Transform[5];
        for (int i = 0; i < 5; i++)
        {
            float x = (i - 2) * SlotSpacingX;
            anchors[i] = MakePoint(row, $"Slot_{i}", new Vector3(x, 0.06f, z), Quaternion.identity);
        }
        return anchors;
    }

    // ── Mesa/altar 3D ─────────────────────────────────────────────────────

    /// <summary>
    /// Construye la mesa como el ALTAR de Forbidden Memories: pedestal de piedra,
    /// tapa dorada en DOS BLOQUES de damero 5×2 (rival / jugador) separados por
    /// una FRANJA CENTRAL con un HEXAGRAMA (estrella de David), rieles laterales
    /// de piedra grabada y CUATRO emblemas dorados flanqueando el altar central.
    /// Devuelve el Transform raíz de la mesa (que la cinemática hace ascender) y,
    /// por <paramref name="groundRenderer"/>, el Renderer de la BASE de la tapa
    /// (las juntas del damero), que es lo que se tiñe con el terreno.
    /// </summary>
    static Transform BuildTable(Transform parent, out Renderer groundRenderer)
    {
        var table = new GameObject("Table").transform;
        table.SetParent(parent, false);

        // Dimensiones. La media anchura deja sitio a los rieles laterales.
        float fieldHalf = 2 * SlotSpacingX + TileSize * 0.5f;      // borde exterior de las casillas
        float halfW = fieldHalf + 1.1f;                            // media anchura de la tapa (con rieles)
        float zBack = RowZ[0], zFront = RowZ[3];
        float depth = (zBack - zFront) + TileSize * ZScale + 1.2f;  // margen tras las casillas alargadas
        float topThick = 0.5f;      // grosor de la tapa
        float bodyHeight = 3.2f;    // altura del pedestal

        // Cuerpo del pedestal (piedra) + bisel inferior.
        MakeBox(table, "Pedestal", Stone,
            new Vector3(0f, -topThick - bodyHeight * 0.5f, 0f),
            new Vector3(halfW * 2f, bodyHeight, depth));
        MakeBox(table, "PedestalBase", StoneDark,
            new Vector3(0f, -topThick - bodyHeight + 0.15f, 0f),
            new Vector3(halfW * 2f + 0.8f, 0.5f, depth + 0.8f));

        // Tapa: base (juntas, se tiñe con el terreno).
        var top = MakeBox(table, "TableTop", TableBase,
            new Vector3(0f, -topThick * 0.5f, 0f),
            new Vector3(halfW * 2f, topThick, depth));
        groundRenderer = top.GetComponent<Renderer>();

        // Marco dorado alrededor de la tapa (4 barras).
        var frameMat = MakeLitMaterial("FrameMat", Gold);
        float fw = halfW * 2f + 0.3f, fd = depth + 0.3f, fy = 0.02f, ft = 0.12f, fb = 0.35f;
        MakeBoxMat(table, "FrameN", frameMat, new Vector3(0f, fy, fd * 0.5f), new Vector3(fw, ft, fb));
        MakeBoxMat(table, "FrameS", frameMat, new Vector3(0f, fy, -fd * 0.5f), new Vector3(fw, ft, fb));
        MakeBoxMat(table, "FrameE", frameMat, new Vector3(halfW + 0.15f, fy, 0f), new Vector3(fb, ft, fd));
        MakeBoxMat(table, "FrameW", frameMat, new Vector3(-halfW - 0.15f, fy, 0f), new Vector3(fb, ft, fd));

        // Rieles laterales de piedra grabada (izq/der), algo elevados.
        var railMat = MakeLitMaterial("RailMat", StoneEngraved);
        float railW = 0.9f, railX = halfW - railW * 0.5f - 0.12f;
        MakeBoxMat(table, "RailW", railMat, new Vector3(-railX, 0.06f, 0f), new Vector3(railW, 0.24f, depth - 0.3f));
        MakeBoxMat(table, "RailE", railMat, new Vector3(railX, 0.06f, 0f), new Vector3(railW, 0.24f, depth - 0.3f));

        // Casillas del damero: 2 bloques (RowZ ya trae el hueco central).
        var lightMat = MakeLitMaterial("TileLightMat", TileLight);
        var darkMat  = MakeLitMaterial("TileDarkMat", TileDark);
        for (int r = 0; r < RowZ.Length; r++)
        {
            for (int c = 0; c < 5; c++)
            {
                float x = (c - 2) * SlotSpacingX;
                bool light = (r + c) % 2 == 0;
                MakeBoxMat(table, $"Tile_{r}_{c}", light ? lightMat : darkMat,
                    new Vector3(x, 0.015f, RowZ[r]),
                    new Vector3(TileSize, 0.05f, TileSize * ZScale));  // casilla alargada en Z
            }
        }

        // ── Franja central (altar) con el HEXAGRAMA ──────────────────────
        var centerMat = MakeLitMaterial("CenterMat", CenterGold);
        MakeBoxMat(table, "CenterStrip", centerMat,
            new Vector3(0f, 0.016f, 0f), new Vector3(fieldHalf * 2f, 0.05f, 2.0f * ZScale));
        BuildHexagram(table, new Vector3(0f, 0.08f, 0f), 2.6f, 0.9f * ZScale);

        // ── Cuatro emblemas dorados flanqueando el altar central ─────────
        var emblemMat = MakeLitMaterial("EmblemMat", GoldBright);
        foreach (float sx in new[] { -railX, railX })
            foreach (float sz in new[] { 2.0f, -2.0f })
                MakeFlatEmblem(table, emblemMat, new Vector3(sx, 0.16f, sz), 0.8f);

        return table;
    }

    /// <summary>Emblema dorado (rombo) tumbado sobre la superficie, visto desde arriba.</summary>
    static void MakeFlatEmblem(Transform parent, Material mat, Vector3 pos, float size)
    {
        var go = MakeBoxMat(parent, "Emblem", mat, pos, new Vector3(size, 0.08f, size));
        go.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
    }

    /// <summary>Hexagrama (estrella de David) de dos triángulos, tumbado en la franja central.</summary>
    static void BuildHexagram(Transform parent, Vector3 center, float rx, float rz)
    {
        var mat = new Material(Shader.Find("Sprites/Default"));
        var col = new Color(0.30f, 0.22f, 0.10f, 0.95f);
        BuildStarTriangle(parent, center, rx, rz, 90f, mat, col, "HexUp");
        BuildStarTriangle(parent, center, rx, rz, 270f, mat, col, "HexDown");
    }

    static void BuildStarTriangle(Transform parent, Vector3 center, float rx, float rz,
                                  float startDeg, Material mat, Color col, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 3;
        var pts = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            float a = (startDeg + i * 120f) * Mathf.Deg2Rad;
            pts[i] = new Vector3(rx * Mathf.Cos(a), 0f, rz * Mathf.Sin(a));
        }
        lr.SetPositions(pts);
        lr.startWidth = lr.endWidth = 0.06f;
        lr.material = mat;
        lr.startColor = lr.endColor = col;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Rombos indicadores (LineRenderer) en las 5 casillas de una fila de monstruos.</summary>
    static void BuildDiamondMarkers(Transform parent, float z)
    {
        var mat = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < 5; i++)
        {
            float x = (i - 2) * SlotSpacingX;
            var go = new GameObject($"Diamond_{z}_{i}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, 0.07f, z);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 4;
            float h = TileSize * 0.34f;
            float hz = TileSize * ZScale * 0.34f;   // rombo alargado como la casilla
            lr.SetPositions(new[]
            {
                new Vector3(0f, 0f, hz), new Vector3(h, 0f, 0f),
                new Vector3(0f, 0f, -hz), new Vector3(-h, 0f, 0f)
            });
            lr.startWidth = lr.endWidth = 0.04f;
            lr.material = mat;
            lr.startColor = lr.endColor = new Color(0.30f, 0.24f, 0.10f, 0.9f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    static GameObject MakeBox(Transform parent, string name, Color color, Vector3 pos, Vector3 size)
        => MakeBoxMat(parent, name, MakeLitMaterial(name + "Mat", color), pos, size);

    static GameObject MakeBoxMat(Transform parent, string name, Material mat, Vector3 pos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        // Ninguna pieza decorativa de la mesa necesita física; sin collider los
        // clics por raycast solo pueden golpear cartas (nunca la mesa).
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    // ── Cajas del HUD ─────────────────────────────────────────────────────

    /// <summary>Caja del HUD con borde dorado. Devuelve el RectTransform interior (relleno).</summary>
    static RectTransform MakeHudBox(RectTransform canvas, string name, float xMin, float yMin, float xMax, float yMax)
    {
        var border = NewImage(name + "Border", canvas, Gold);
        Anchor(border.rectTransform, xMin, yMin, xMax, yMax);
        var fill = NewImage(name, border.transform, new Color(0.05f, 0.06f, 0.14f, 0.97f));
        var rt = fill.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(3, 3); rt.offsetMax = new Vector2(-3, -3);
        return rt;
    }

    /// <summary>Chip coloreado con etiqueta (COM / TÚ).</summary>
    static void MakeChip(RectTransform parent, string label, Color color,
                         float xMin, float yMin, float xMax, float yMax)
    {
        var chip = NewImage("Chip_" + label, parent, color);
        Anchor(chip.rectTransform, xMin, yMin, xMax, yMax);
        var t = MakeText("Label", chip.transform, label, 22, Color.white, TextAlignmentOptions.Center);
        Stretch(t.rectTransform);
        t.fontStyle = FontStyles.Bold;
    }

    static Transform MakePoint(Transform parent, string name, Vector3 localPos, Quaternion localRot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        return go.transform;
    }

    /// <summary>Material iluminado compatible con URP (con respaldo a Standard).</summary>
    static Material MakeLitMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return new Material(shader) { name = name, color = color };
    }

    // ── Piezas UI (mismo estilo que el resto de builders) ────────────────

    static GameObject MakeButtonColumn(RectTransform canvas, string name,
                                       float xMin, float yMin, float xMax, float yMax)
    {
        var col = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        col.transform.SetParent(canvas, false);
        Anchor((RectTransform)col.transform, xMin, yMin, xMax, yMax);
        var vlg = col.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 12; vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;
        return col;
    }

    static (GameObject panel, TextMeshProUGUI title, Transform buttons) MakeContextPanel(RectTransform canvas, string name)
    {
        var panel = NewImage(name, canvas, PanelFill).gameObject;
        Anchor((RectTransform)panel.transform, 0.005f, 0.28f, 0.20f, 0.86f);

        var title = MakeText("Title", panel.transform, "", 24, GoldBright, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        Anchor((RectTransform)title.transform, 0.04f, 0.90f, 0.96f, 0.995f);
        title.enableAutoSizing = true; title.fontSizeMin = 14; title.fontSizeMax = 24;

        var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
        buttons.transform.SetParent(panel.transform, false);
        Anchor((RectTransform)buttons.transform, 0.05f, 0.02f, 0.95f, 0.88f);
        var vlg = buttons.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 8; vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;

        return (panel, title, buttons.transform);
    }

    static Button MakeButton(string name, Transform parent, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = Color.white;
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 54; le.preferredHeight = 58;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = BtnNormal; cb.highlightedColor = BtnHover; cb.pressedColor = BtnPressed;
        cb.selectedColor = BtnHover; cb.disabledColor = BtnDisabled;
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.1f;
        btn.colors = cb;

        var txt = MakeText("Label", go.transform, label, 23, TextLight, TextAlignmentOptions.Center);
        Stretch(txt.rectTransform);
        txt.margin = new Vector4(10, 0, 10, 0);
        txt.enableAutoSizing = true; txt.fontSizeMin = 12; txt.fontSizeMax = 23;
        return btn;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    static void Set(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogError($"DuelSceneBuilder: no existe el campo '{prop}'."); return; }
        p.objectReferenceValue = value;
    }

    static void SetArray(SerializedObject so, string prop, Transform[] values)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogError($"DuelSceneBuilder: no existe el array '{prop}'."); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void EnsureEventSystem(Scene scene)
    {
        if (Object.FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        MoveToScene(go, scene);
    }

    static void DestroyRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name) { Object.DestroyImmediate(root); return; }
    }

    static void MoveToScene(GameObject go, Scene scene)
    {
        if (go.scene != scene && scene.IsValid())
            SceneManager.MoveGameObjectToScene(go, scene);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
