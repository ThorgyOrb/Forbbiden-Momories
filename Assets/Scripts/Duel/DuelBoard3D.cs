using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// El TABLERO 3D del duelo: mesa, cámara, anclas de slots y todas las
/// animaciones físicas de cartas (estilo Forbidden Memories):
///
///   • Entrada al duelo: barrido de cámara desde arriba hasta la vista de juego.
///   • Invocación/colocación: la carta vuela en arco desde el borde a su slot.
///   • Fusión: los materiales se reúnen flotando frente a la cámara y se
///     resuelven en pareja — fusión (destello y carta nueva), equipo
///     (absorción: la carta se encoge dentro del monstruo) o incompatible
///     (la carta descartada sale girando fuera de la mesa).
///   • Ataque: el monstruo embiste al objetivo (o al aire en ataque directo),
///     con sacudida de impacto; boost visual "+500 ★" si hay ventaja de
///     Estrella Guardiana; destrucción con giro/encogido/fade.
///
/// No sabe de reglas: el <see cref="DuelController"/> decide QUÉ pasa y este
/// tablero solo lo escenifica. Los clics sobre cartas 3D se detectan por
/// raycast físico y se reenvían como eventos con el índice de slot.
/// </summary>
public class DuelBoard3D : MonoBehaviour
{
    [Header("Cámara")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform cameraIntroPoint;   // (heredado; la intro calcula su propio arranque)
    [SerializeField] private Transform cameraPlayPoint;

    [Header("Cinemática de entrada")]
    [SerializeField] private Light keyLight;      // luz principal (se enciende al emerger la mesa)
    [SerializeField] private Transform tableRoot; // la mesa/altar (asciende ligeramente en la intro)

    [Header("Anclas de slots (5 por fila)")]
    [SerializeField] private Transform[] opponentSpellAnchors = new Transform[5];
    [SerializeField] private Transform[] opponentMonsterAnchors = new Transform[5];
    [SerializeField] private Transform[] playerMonsterAnchors = new Transform[5];
    [SerializeField] private Transform[] playerSpellAnchors = new Transform[5];

    [Header("Puntos de animación")]
    [SerializeField] private Transform playerSpawnPoint;    // de dónde salen tus cartas
    [SerializeField] private Transform opponentSpawnPoint;  // de dónde salen las del rival
    [SerializeField] private Transform fusionPoint;         // centro de la cola de fusión
    [SerializeField] private Transform discardPoint;        // a dónde vuelan las descartadas

    [Header("Piezas")]
    [SerializeField] private Duel3DCardView cardTemplate;   // plantilla inactiva
    [SerializeField] private Renderer groundRenderer;       // suelo (se tiñe por terreno)
    [Tooltip("Escala de las cartas 3D durante la FUSIÓN (reunión y remolino). Súbelo si se " +
             "ven más chicas que la carta 2D alzada en una invocación normal.")]
    [SerializeField] private float fusionCardScale = 1.15f;

    // ── Registro de vistas por slot ──────────────────────────────────────
    private readonly Duel3DCardView[] _playerMonsters = new Duel3DCardView[5];
    private readonly Duel3DCardView[] _opponentMonsters = new Duel3DCardView[5];
    private readonly Duel3DCardView[] _playerSpells = new Duel3DCardView[5];
    private readonly Duel3DCardView[] _opponentSpells = new Duel3DCardView[5];

    private readonly List<Duel3DCardView> _fusionQueue = new();

    // Estado cacheado de la cinemática de entrada (se restaura al valor de juego).
    private float _lightTarget;
    private float _ambientIntensityTarget, _ambientIntensityDark;
    private Color _ambientColorTarget, _ambientColorDark;
    private float _tableStartY, _tableTargetY;
    private LineRenderer[] _markers;      // rombos indicadores (material sin luz)
    private Color[] _markerColors;        // su color pleno, para desvanecerlos con la luz

    void Awake()
    {
        if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
        PrepareIntro();
    }

    /// <summary>
    /// Deja la escena en "oscuridad": cámara en el arranque bajo/lejano, luz
    /// principal apagada, ambiente casi negro y la mesa un pelo hundida. La
    /// iluminación de juego queda cacheada para reencenderla en <see cref="PlayIntro"/>.
    /// </summary>
    private void PrepareIntro()
    {
        // Resolver referencias por si la escena no fue reconstruida con el builder.
        if (keyLight == null)
        {
            keyLight = RenderSettings.sun;
            if (keyLight == null)
                foreach (var l in FindObjectsOfType<Light>())
                    if (l.type == LightType.Directional) { keyLight = l; break; }
        }
        if (tableRoot == null)
        {
            var t = transform.Find("Table");
            if (t != null) tableRoot = t;
        }

        // Los rombos usan material sin luz: para que no floten en el negro, se
        // desvanecen a la par que la iluminación (emergen con la mesa).
        _markers = GetComponentsInChildren<LineRenderer>(true);
        _markerColors = new Color[_markers.Length];
        for (int i = 0; i < _markers.Length; i++)
            _markerColors[i] = _markers[i].startColor;

        // Cachear los valores de iluminación "de juego" y preparar los "oscuros".
        _lightTarget = keyLight != null ? keyLight.intensity : 1f;
        _ambientIntensityTarget = RenderSettings.ambientIntensity;
        _ambientColorTarget = RenderSettings.ambientLight;
        _ambientIntensityDark = _ambientIntensityTarget * 0.05f;
        _ambientColorDark = _ambientColorTarget * 0.05f;
        ApplyLighting(0f);

        // Mesa un poco por debajo para que "ascienda" durante el acercamiento.
        if (tableRoot != null)
        {
            _tableTargetY = tableRoot.localPosition.y;
            _tableStartY = _tableTargetY - 0.6f;
            SetTableY(_tableStartY);
        }

        // Cámara en el arranque de la órbita (bajo, lejos y girado a un lado).
        if (mainCamera != null && cameraPlayPoint != null)
        {
            GetIntroPose(0f, out Vector3 pos, out Quaternion rot);
            mainCamera.transform.SetPositionAndRotation(pos, rot);
        }
    }

    // (El duelo se controla por TECLADO: ya no hay clics por raycast físico.)

    // ── Cámara / terreno ─────────────────────────────────────────────────

    // Arranque de la órbita: la cámara empieza ARRIBA del tablero mirándolo casi
    // en picado, y baja en espiral (media vuelta) hasta la vista del jugador.
    private static readonly Vector3 IntroPivot = new Vector3(0f, 0.8f, 0f);
    private const float IntroYawStart = 180f;    // media vuelta de órbita al frente
    private const float IntroRadiusStart = 6f;   // casi sobre el tablero (poco radio)
    private const float IntroHeightStart = 22f;  // muy por encima → mira hacia abajo

    /// <summary>
    /// Cinemática de entrada estilo Forbidden Memories:
    ///   • Aparición — la luz sube gradualmente y la mesa dorada emerge del negro
    ///     (el desvanecido del velo negro lo lleva <see cref="DuelScreen"/>).
    ///   • Órbita — la cámara, apuntando desde ARRIBA del tablero, baja en espiral
    ///     (media vuelta) hasta la vista de juego del jugador (su mano).
    ///   • Final — la mesa queda centrada, llenando el encuadre; entra la mano.
    /// El "negro absoluto" inicial lo mantiene el controlador (velo + luz apagada).
    /// </summary>
    public IEnumerator PlayIntro()
    {
        if (mainCamera == null || cameraPlayPoint == null) yield break;

        // La escena arranca oscura (PrepareIntro). Órbita reencendiendo la luz.
        // Lenta (5 s): con las fases del HUD suma ~10 s de presentación total.
        ApplyLighting(0f);
        var cam = mainCamera.transform;
        const float duration = 5.0f;
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            float s = Smooth(e / duration);
            GetIntroPose(s, out Vector3 pos, out Quaternion rot);
            cam.SetPositionAndRotation(pos, rot);

            ApplyLighting(Mathf.Clamp01(s / 0.7f));                 // luz plena al 70%
            SetTableY(Mathf.Lerp(_tableStartY, _tableTargetY,       // ascenso al 70%
                                 Mathf.Clamp01(s / 0.7f)));
            yield return null;
        }

        // Fase 6: asentar exactamente en la vista de juego, todo encendido.
        cam.SetPositionAndRotation(cameraPlayPoint.position, cameraPlayPoint.rotation);
        ApplyLighting(1f);
        SetTableY(_tableTargetY);
    }

    /// <summary>
    /// Pose de la cámara en la órbita para un progreso s∈[0,1]: interpola el giro
    /// lateral, el radio y la altura entre el arranque y la vista de juego, y
    /// mira siempre al pivote (mezclándose con la rotación exacta de juego al
    /// final para no dar un salto). s=0 = ángulo lateral bajo; s=1 = de frente.
    /// </summary>
    private void GetIntroPose(float s, out Vector3 pos, out Quaternion rot)
    {
        // Parámetros de destino (la vista de juego), relativos al pivote.
        Vector3 relEnd = cameraPlayPoint.position - IntroPivot;
        float radiusEnd = new Vector2(relEnd.x, relEnd.z).magnitude;
        float heightEnd = cameraPlayPoint.position.y;
        // Yaw exacto del punto de juego. OJO con los signos: la posición usa
        // -radius·sin(yaw) en X y -radius·cos(yaw) en Z, así que el yaw se
        // deriva de (-relEnd.x, -relEnd.z); con el signo mal, la órbita termina
        // espejada en X y la cámara da un salto al asentarse.
        float yawEnd = Mathf.Atan2(-relEnd.x, -relEnd.z) * Mathf.Rad2Deg;

        float yaw = Mathf.Lerp(IntroYawStart, yawEnd, s) * Mathf.Deg2Rad;
        float radius = Mathf.Lerp(IntroRadiusStart, radiusEnd, s);
        float height = Mathf.Lerp(IntroHeightStart, heightEnd, s);

        // Posición sobre un círculo alrededor del pivote (yaw=0 ⇒ de frente, -Z).
        pos = new Vector3(IntroPivot.x - radius * Mathf.Sin(yaw),
                          height,
                          IntroPivot.z - radius * Mathf.Cos(yaw));

        // Mira al pivote; al acercarse a s=1 converge a la rotación de juego.
        Quaternion look = Quaternion.LookRotation((IntroPivot - pos).normalized, Vector3.up);
        rot = Quaternion.SlerpUnclamped(look, cameraPlayPoint.rotation, s * s);
    }

    /// <summary>Interpola la iluminación entre "oscuro" (k=0) y "juego" (k=1).</summary>
    private void ApplyLighting(float k)
    {
        if (keyLight != null) keyLight.intensity = _lightTarget * k;
        RenderSettings.ambientIntensity = Mathf.Lerp(_ambientIntensityDark, _ambientIntensityTarget, k);
        RenderSettings.ambientLight = Color.Lerp(_ambientColorDark, _ambientColorTarget, k);

        if (_markers != null)
            for (int i = 0; i < _markers.Length; i++)
            {
                if (_markers[i] == null) continue;
                Color c = _markerColors[i]; c.a = _markerColors[i].a * k;
                _markers[i].startColor = _markers[i].endColor = c;
            }
    }

    private void SetTableY(float y)
    {
        if (tableRoot == null) return;
        var lp = tableRoot.localPosition;
        lp.y = y;
        tableRoot.localPosition = lp;
    }

    /// <summary>Smoothstep (arranque y frenado suaves).</summary>
    private static float Smooth(float t) => t * t * (3f - 2f * t);

    // ── Vistas de cámara del duelo ───────────────────────────────────────

    public enum CameraView { Play, MonsterZone, PlayerField, OpponentField }

    /// <summary>Mueve la cámara con animación a una de las vistas del duelo.</summary>
    public IEnumerator MoveCamera(CameraView view, float duration = 0.55f)
    {
        if (mainCamera == null) yield break;
        GetViewPose(view, out Vector3 pos, out Quaternion rot);
        yield return DuelTween.Parallel(this,
            DuelTween.MoveTo(mainCamera.transform, pos, duration),
            DuelTween.RotateTo(mainCamera.transform, rot, duration));
    }

    private void GetViewPose(CameraView v, out Vector3 pos, out Quaternion rot)
    {
        switch (v)
        {
            // Campo del jugador (cenital): misma pose para elegir casilla y para
            // la batalla — "siempre las coordenadas del campo del jugador".
            case CameraView.MonsterZone:
            case CameraView.PlayerField:
                pos = new Vector3(0f, 7.45f, -4.43f); rot = Quaternion.Euler(90f, 0f, 0f); break;
            case CameraView.OpponentField:  // cenital sobre el campo del rival (elegir objetivo)
                pos = new Vector3(0f, 7.45f, 3.35f); rot = Quaternion.Euler(90f, 0f, 0f); break;
            default:                        // vista de juego normal (con la mano)
                pos = cameraPlayPoint.position; rot = cameraPlayPoint.rotation; break;
        }
    }

    private static readonly Dictionary<TerrainType, Color> TerrainColors = new()
    {
        { TerrainType.Neutral,   new Color(0.16f, 0.17f, 0.22f) },
        { TerrainType.Forest,    new Color(0.10f, 0.22f, 0.10f) },
        { TerrainType.Mountain,  new Color(0.24f, 0.20f, 0.16f) },
        { TerrainType.Sea,       new Color(0.08f, 0.16f, 0.28f) },
        { TerrainType.Dark,      new Color(0.10f, 0.08f, 0.16f) },
        { TerrainType.Wasteland, new Color(0.26f, 0.22f, 0.12f) },
        { TerrainType.Meadow,    new Color(0.14f, 0.24f, 0.08f) },
        { TerrainType.Yami,      new Color(0.14f, 0.06f, 0.18f) },
    };

    /// <summary>Tiñe la mesa según el terreno activo.</summary>
    public void SetTerrain(TerrainType terrain)
    {
        if (groundRenderer == null) return;
        if (TerrainColors.TryGetValue(terrain, out var c))
            groundRenderer.material.color = c;
    }

    // ── Sincronización sin animación ─────────────────────────────────────

    /// <summary>
    /// Deja el tablero EXACTAMENTE como el estado lógico (crea/actualiza/borra
    /// vistas sin animar). Se usa tras efectos instantáneos (magias, revelar,
    /// cambio de posición) y como red de seguridad tras cada animación.
    /// </summary>
    // Las cartas se posan a una ALTURA FIJA sobre la mesa (Y = 0.5): con el ancla
    // (Y ≈ 0.06) quedaban hundidas dentro del tablero.
    private const float CardY = 0.5f;
    private Vector3 SlotPos(Transform anchor) => new Vector3(anchor.position.x, CardY, anchor.position.z);

    public void SyncField(Duelist player, Duelist opponent)
    {
        SyncMonsterRow(player, _playerMonsters, playerMonsterAnchors);
        SyncMonsterRow(opponent, _opponentMonsters, opponentMonsterAnchors);
        SyncSpellRow(player, _playerSpells, playerSpellAnchors);
        SyncSpellRow(opponent, _opponentSpells, opponentSpellAnchors);
    }

    private void SyncMonsterRow(Duelist owner, Duel3DCardView[] views, Transform[] anchors)
    {
        for (int i = 0; i < 5; i++)
        {
            var card = owner.MonsterZone[i];
            if (card == null)
            {
                if (views[i] != null) { Destroy(views[i].gameObject); views[i] = null; }
                continue;
            }

            if (views[i] == null)
                views[i] = SpawnView(SlotPos(anchors[i]));

            var pos = owner.MonsterPositions[i];
            views[i].transform.position = SlotPos(anchors[i]);
            views[i].Show(card, pos);
            views[i].SetStats(StatsFor(owner, i));
        }
    }

    private void SyncSpellRow(Duelist owner, Duel3DCardView[] views, Transform[] anchors)
    {
        for (int i = 0; i < 5; i++)
        {
            var card = owner.SpellZone[i];
            if (card == null)
            {
                if (views[i] != null) { Destroy(views[i].gameObject); views[i] = null; }
                continue;
            }

            if (views[i] == null)
                views[i] = SpawnView(SlotPos(anchors[i]));

            // Las trampas esperan boca abajo (se ve el dorso).
            views[i].transform.position = SlotPos(anchors[i]);
            views[i].Show(card, card.IsTrap ? CardPosition.FaceDownAttack : CardPosition.FaceUpAttack);
            views[i].SetStats("");
        }
    }

    /// <summary>Texto de stats actuales de un monstruo ("" si está boca abajo).</summary>
    private static string StatsFor(Duelist owner, int slot)
    {
        var pos = owner.MonsterPositions[slot];
        bool faceDown = pos == CardPosition.FaceDownAttack || pos == CardPosition.FaceDownDefense;
        if (faceDown) return "";
        return $"ATK {owner.MonsterCurrentAtk[slot]}  DEF {owner.MonsterCurrentDef[slot]}";
    }

    // ── Resaltados ───────────────────────────────────────────────────────

    public void SetPlayerMonsterHighlight(int slot, bool on)
    {
        if (slot >= 0 && slot < 5 && _playerMonsters[slot] != null) _playerMonsters[slot].SetHighlight(on);
    }

    public void SetOpponentMonsterHighlight(int slot, bool on)
    {
        if (slot >= 0 && slot < 5 && _opponentMonsters[slot] != null) _opponentMonsters[slot].SetHighlight(on);
    }

    public void SetPlayerSpellHighlight(int slot, bool on)
    {
        if (slot >= 0 && slot < 5 && _playerSpells[slot] != null) _playerSpells[slot].SetHighlight(on);
    }

    public void ClearHighlights()
    {
        for (int i = 0; i < 5; i++)
        {
            SetPlayerMonsterHighlight(i, false);
            SetOpponentMonsterHighlight(i, false);
            SetPlayerSpellHighlight(i, false);
        }
    }

    // ── Cursor de casilla (selección por teclado) ────────────────────────

    private Transform _slotCursor;

    /// <summary>Placa dorada parpadeante sobre la casilla que apunta el selector.</summary>
    public void ShowSlotCursor(bool playerSide, bool monsterRow, int index)
    {
        if (_slotCursor == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "SlotCursor";
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(1.7f, 0.015f, 2.6f);
            Destroy(go.GetComponent<Collider>());
            var mat = new Material(Shader.Find("Sprites/Default"))
            { color = new Color(1f, 0.85f, 0.35f, 0.45f) };
            go.GetComponent<Renderer>().sharedMaterial = mat;
            _slotCursor = go.transform;
        }

        var anchors = playerSide
            ? (monsterRow ? playerMonsterAnchors : playerSpellAnchors)
            : (monsterRow ? opponentMonsterAnchors : opponentSpellAnchors);
        _slotCursor.gameObject.SetActive(true);
        _slotCursor.position = anchors[index].position + Vector3.up * 0.01f;
    }

    public void HideSlotCursor()
    {
        if (_slotCursor != null) _slotCursor.gameObject.SetActive(false);
    }

    // ── Carta del campo alzada al centro (activar / re-setear) ──────────

    /// <summary>La magia/trampa seteada vuela al centro, de cara a la cámara.</summary>
    public IEnumerator AnimateFieldCardToCenter(int slot)
    {
        var v = _playerSpells[slot];
        if (v == null) yield break;
        FaceCamera(v, fusionPoint.position);
        yield return DuelTween.Parallel(this,
            DuelTween.MoveTo(v.transform, fusionPoint.position, 0.35f),
            DuelTween.ScaleTo(v.transform, Vector3.one * 0.9f, 0.35f));
    }

    /// <summary>Voltea la carta alzada (encoge en X, cambia cara, expande).</summary>
    public IEnumerator AnimateFlipFieldCard(int slot, CardData card, bool faceDown)
    {
        var v = _playerSpells[slot];
        if (v == null) yield break;
        yield return DuelTween.ScaleTo(v.transform, new Vector3(0.04f, 0.9f, 0.9f), 0.12f);
        v.Show(card, faceDown ? CardPosition.FaceDownAttack : CardPosition.FaceUpAttack);
        FaceCamera(v, v.transform.position);
        yield return DuelTween.ScaleTo(v.transform, Vector3.one * 0.9f, 0.12f);
    }

    /// <summary>Devuelve la carta alzada a su casilla (SyncField la normaliza).</summary>
    public IEnumerator AnimateFieldCardBack(int slot)
    {
        var v = _playerSpells[slot];
        if (v == null) yield break;
        yield return DuelTween.Parallel(this,
            DuelTween.MoveTo(v.transform, SlotPos(playerSpellAnchors[slot]), 0.3f),
            DuelTween.ScaleTo(v.transform, Vector3.one, 0.3f));
    }

    // ── Invocación / colocación ──────────────────────────────────────────

    /// <summary>
    /// Animación de invocar/colocar: la carta nace pequeña en el borde del
    /// dueño y vuela en arco hasta su slot. El estado lógico ya debe estar
    /// aplicado (el slot indica qué pintar).
    /// </summary>
    public IEnumerator AnimateSummon(bool playerSide, int slot, Duelist owner)
    {
        var anchors = playerSide ? playerMonsterAnchors : opponentMonsterAnchors;
        var views = playerSide ? _playerMonsters : _opponentMonsters;
        var spawn = playerSide ? playerSpawnPoint : opponentSpawnPoint;

        var view = SpawnView(spawn.position);
        views[slot] = view;
        view.Show(owner.MonsterZone[slot], owner.MonsterPositions[slot]);
        view.SetStats("");
        view.transform.localScale = Vector3.one * 0.25f;

        yield return DuelTween.Parallel(this,
            DuelTween.Arc(view.transform, spawn.position, SlotPos(anchors[slot]), 1.6f, 0.55f),
            DuelTween.ScaleTo(view.transform, Vector3.one, 0.55f));

        view.SetStats(StatsFor(owner, slot));
    }

    /// <summary>Cámara del duelo (para proyectar casillas 3D a la pantalla).</summary>
    public Camera Camera => mainCamera;

    /// <summary>Posición de mundo de una casilla de monstruo del jugador.</summary>
    public Vector3 GetPlayerMonsterSlotWorld(int slot) => SlotPos(playerMonsterAnchors[slot]);

    /// <summary>
    /// El monstruo APARECE en su casilla creciendo desde pequeño (sin arco desde
    /// el borde). Se usa cuando la carta 2D alzada ya voló hasta la casilla: el
    /// 3D crece mientras la 2D se desvanece → parece la misma carta.
    /// </summary>
    public IEnumerator AnimateAppearAtSlot(bool playerSide, int slot, Duelist owner)
    {
        var anchors = playerSide ? playerMonsterAnchors : opponentMonsterAnchors;
        var views = playerSide ? _playerMonsters : _opponentMonsters;

        if (views[slot] == null) views[slot] = SpawnView(SlotPos(anchors[slot]));
        var view = views[slot];
        view.transform.position = SlotPos(anchors[slot]);
        view.Show(owner.MonsterZone[slot], owner.MonsterPositions[slot]);
        view.SetStats("");
        view.transform.localScale = Vector3.one * 0.6f;

        yield return DuelTween.ScaleTo(view.transform, Vector3.one, 0.25f);
        view.SetStats(StatsFor(owner, slot));
    }

    /// <summary>Colocar una trampa boca abajo en la zona de magias.</summary>
    public IEnumerator AnimateSetTrap(bool playerSide, int slot, CardData card)
    {
        var anchors = playerSide ? playerSpellAnchors : opponentSpellAnchors;
        var views = playerSide ? _playerSpells : _opponentSpells;
        var spawn = playerSide ? playerSpawnPoint : opponentSpawnPoint;

        var view = SpawnView(spawn.position);
        views[slot] = view;
        view.Show(card, CardPosition.FaceDownAttack);
        view.SetStats("");
        view.transform.localScale = Vector3.one * 0.25f;

        yield return DuelTween.Parallel(this,
            DuelTween.Arc(view.transform, spawn.position, SlotPos(anchors[slot]), 1.2f, 0.45f),
            DuelTween.ScaleTo(view.transform, Vector3.one, 0.45f));
    }

    // ── Fusión ───────────────────────────────────────────────────────────

    /// <summary>
    /// Los materiales elegidos se reúnen flotando en fila frente a la cámara
    /// (la "cola de fusión"), mirando al jugador para que se vean completos.
    /// </summary>
    // Posición fija de las cartas ANTES de invocar (showcase) y durante la FUSIÓN.
    // Centrada en X (la del fusionPoint), con Y/Z fijas para que queden bien encuadradas.
    private const float FusionAnchorY = 1.586f;
    private const float FusionAnchorZ = -7.53f;

    /// <summary>Punto base del showcase/fusión: X centrada, Y/Z fijas (FusionAnchorY/Z).</summary>
    private Vector3 FusionAnchor()
    {
        float x = fusionPoint != null ? fusionPoint.position.x : 0f;
        return new Vector3(x, FusionAnchorY, FusionAnchorZ);
    }

    // Escenografía de la fusión en coordenadas de PANTALLA (fracción horizontal 0..1).
    // Con 3+ materiales, el "escenario" de fusión va a la IZQUIERDA y los materiales
    // pendientes se APILAN a la derecha, dando protagonismo y espacio a la animación.
    private const float FusionStageSx = 0.30f;      // dónde ocurre la fusión (mitad izq.)
    private const float FusionStackBaseSx = 0.66f;  // primera carta pendiente (derecha)
    private const float FusionStackStepSx = 0.05f;  // separación al apilar (se solapan)
    private const float FusionStackScaleMul = 0.8f; // las pendientes, algo más pequeñas
    private bool _fusionStacked;                     // modo apilado (3+ materiales)

    /// <summary>
    /// Punto en mundo a la ANCHURA de pantalla <paramref name="sx"/> (0=izq, 1=der), a la
    /// misma altura y profundidad de cámara que el FusionAnchor. Permite escenificar la
    /// fusión en coordenadas de pantalla (mitad izquierda, apilado a la derecha…).
    /// </summary>
    private Vector3 FusionScreenX(float sx)
    {
        if (mainCamera == null)
            return FusionAnchor() + Vector3.right * ((sx - 0.5f) * 9f);
        Vector3 a = mainCamera.WorldToScreenPoint(FusionAnchor());
        return mainCamera.ScreenToWorldPoint(new Vector3(Screen.width * sx, a.y, a.z));
    }

    /// <summary>Posición apilada de la carta pendiente n.º <paramref name="pendingIdx"/> (0-based).</summary>
    private Vector3 FusionStackPos(int pendingIdx)
        => FusionScreenX(FusionStackBaseSx + pendingIdx * FusionStackStepSx);

    /// <summary>
    /// Convierte un punto de PANTALLA (píxeles) a mundo, a la distancia del FusionAnchor,
    /// para que una carta 3D "se levante" desde su posición en la mano (UI 2D) hacia el
    /// punto de fusión/showcase. Llamar con la cámara ya en su vista final.
    /// </summary>
    public Vector3 HandStartWorld(Vector3 screenPos)
    {
        if (mainCamera == null)
            return playerSpawnPoint != null ? playerSpawnPoint.position : FusionAnchor();
        float dist = Vector3.Distance(mainCamera.transform.position, FusionAnchor());
        return mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, dist));
    }

    /// <summary>
    /// Reúne los materiales en fila frente a la cámara. Si se pasan <paramref name="worldStarts"/>
    /// (uno por material), cada carta ARRANCA ahí (p. ej. desde su carta de la mano) y sube
    /// en arco; si no, salen del punto de aparición del jugador.
    /// </summary>
    public IEnumerator AnimateFusionGather(List<CardData> materials, List<Vector3> worldStarts = null)
    {
        ClearFusionQueue();

        // Con 3 o más materiales, apilamos: el primero va al escenario (izquierda) y el
        // resto se apila a la derecha (más pequeñas), dejando la mitad izquierda libre
        // para lucir la fusión. Con 2, la fila clásica centrada.
        _fusionStacked = materials.Count >= 3;
        float spacing = 1.9f;
        float x0 = -(materials.Count - 1) * spacing * 0.5f;

        var routines = new List<IEnumerator>();
        for (int i = 0; i < materials.Count; i++)
        {
            Vector3 target; float targetScale;
            if (_fusionStacked)
            {
                target = (i == 0) ? FusionScreenX(FusionStageSx) : FusionStackPos(i - 1);
                targetScale = (i == 0) ? fusionCardScale : fusionCardScale * FusionStackScaleMul;
            }
            else
            {
                target = FusionAnchor() + new Vector3(x0 + i * spacing, 0f, 0f);
                targetScale = fusionCardScale;
            }

            Vector3 start = (worldStarts != null && i < worldStarts.Count)
                ? worldStarts[i]
                : playerSpawnPoint.position;
            var view = SpawnView(start);
            view.Show(materials[i], CardPosition.FaceUpAttack);
            view.SetStats("");
            FaceCamera(view, target);
            view.transform.localScale = Vector3.one * 0.3f;
            _fusionQueue.Add(view);

            routines.Add(DuelTween.Arc(view.transform, start, target, 1.0f, 0.5f));
            routines.Add(DuelTween.ScaleTo(view.transform, Vector3.one * targetScale, 0.5f));
        }
        yield return DuelTween.Parallel(this, routines.ToArray());
        yield return new WaitForSeconds(0.15f);
    }

    /// <summary>
    /// Resuelve UN paso de la cadena entre las dos primeras cartas de la cola:
    ///   • Fusión (específica/categoría): remolino de luces → nace la fusionada.
    ///   • Equipo: la segunda es ABSORBIDA por la primera (se encoge dentro).
    ///   • Incompatible (absorción): la perdedora embiste y sale despedida a la derecha.
    /// </summary>
    public IEnumerator AnimateFusionStep(FusionStepType type, CardData stepResult, bool firstSurvives)
    {
        if (_fusionQueue.Count < 2) yield break;

        var a = _fusionQueue[0];
        var b = _fusionQueue[1];

        Vector3 center;
        if (_fusionStacked)
        {
            // Escenario fijo a la izquierda; la siguiente pendiente ENTRA desde la pila
            // de la derecha hacia el escenario, a tamaño completo, para fusionarse.
            center = FusionScreenX(FusionStageSx);
            Vector3 bIn = center + Vector3.right * 1.6f;
            FaceCamera(b, bIn);
            yield return DuelTween.Parallel(this,
                DuelTween.MoveTo(b.transform, bIn, 0.28f),
                DuelTween.ScaleTo(b.transform, Vector3.one * fusionCardScale, 0.28f));
        }
        else
        {
            center = (a.transform.position + b.transform.position) * 0.5f;
        }

        switch (type)
        {
            case FusionStepType.Specific:
            case FusionStepType.Category:
                // Compatibles: remolino de luces. Las dos cartas orbitan en espiral
                // hacia el centro entre destellos y de ahí nace la carta fusionada.
                yield return FusionVortex(a, b, center, stepResult);
                break;

            case FusionStepType.Equip:
            {
                // Absorción: el equipo se hunde dentro del monstruo.
                yield return DuelTween.Parallel(this,
                    DuelTween.MoveTo(b.transform, a.transform.position, 0.4f),
                    DuelTween.ScaleTo(b.transform, Vector3.zero, 0.4f),
                    DuelTween.Spin(b.transform, Vector3.up, 540f, 0.4f));
                Destroy(b.gameObject);
                _fusionQueue.RemoveAt(1);

                // El monstruo "late" al recibir el bonus.
                yield return DuelTween.ScaleTo(a.transform, Vector3.one * (fusionCardScale * 1.18f), 0.12f);
                yield return DuelTween.ScaleTo(a.transform, Vector3.one * fusionCardScale, 0.12f);
                break;
            }

            case FusionStepType.Absorption:
            {
                // Incompatibles: la carta de la DERECHA (b) embiste a la de la
                // IZQUIERDA (a) y la saca despedida hacia la izquierda (descarte).
                // La superviviente se queda y muestra la carta resultante del paso.
                var aggressor = b;  // derecha (queue[1])
                var victim = a;     // izquierda (queue[0])

                // 1) Embestida: la derecha se lanza sobre la izquierda.
                Vector3 impact = victim.transform.position;
                yield return DuelTween.MoveTo(aggressor.transform,
                    Vector3.Lerp(aggressor.transform.position, impact, 0.55f), 0.16f);

                // 2) Impacto: chispa de luz + onda de choque + chispas radiales +
                //    sacudida de la víctima y de la cámara (golpe con peso).
                var spark = SpawnFusionLight(impact, FusionGold);
                StartCoroutine(Shockwave(impact, 0f));
                StartCoroutine(CameraKick(0.12f, 0.20f));
                yield return DuelTween.Parallel(this,
                    DuelTween.Shake(victim.transform, 0.16f, 0.26f),
                    FlashLight(spark, 3.6f, 0.26f),
                    ImpactSparks(impact, FusionGold, 12));
                if (spark != null) Destroy(spark.gameObject);

                // 3) La víctima sale despedida hacia la IZQUIERDA (descarte), girando
                //    y desvaneciéndose. Reusa altura/profundidad del discardPoint.
                Vector3 outPos = impact + Vector3.left * 11f;
                if (discardPoint != null)
                {
                    outPos.y = discardPoint.position.y;
                    outPos.z = discardPoint.position.z;
                }
                outPos.x = Mathf.Min(outPos.x, impact.x - 7f); // asegura que sea a la izquierda
                var fade = victim.CanvasGroup != null
                    ? DuelTween.FadeCanvas(victim.CanvasGroup, 1f, 0f, 0.45f)
                    : DuelTween.ScaleTo(victim.transform, Vector3.zero, 0.45f);
                yield return DuelTween.Parallel(this,
                    DuelTween.Arc(victim.transform, victim.transform.position, outPos, 1.4f, 0.45f),
                    DuelTween.Spin(victim.transform, Vector3.forward, 900f, 0.45f),
                    fade);

                Destroy(victim.gameObject);
                _fusionQueue.Remove(victim);

                // La superviviente (la derecha) pasa al frente y muestra el resultado.
                _fusionQueue.Remove(aggressor);
                _fusionQueue.Insert(0, aggressor);
                aggressor.Show(stepResult, CardPosition.FaceUpAttack);
                FaceCamera(aggressor, aggressor.transform.position);
                break;
            }
        }

        // Reacomoda la cola restante en fila.
        yield return RealignFusionQueue();
    }

    private IEnumerator RealignFusionQueue()
    {
        if (_fusionQueue.Count == 0) yield break;

        var routines = new List<IEnumerator>();
        if (_fusionStacked)
        {
            // Índice 0 = resultado/acumulador en el escenario (izq.); el resto, apilado
            // a la derecha (más pequeñas). Se recolocan al consumirse una pendiente.
            for (int i = 0; i < _fusionQueue.Count; i++)
            {
                Vector3 target = (i == 0) ? FusionScreenX(FusionStageSx) : FusionStackPos(i - 1);
                float sc = (i == 0) ? fusionCardScale : fusionCardScale * FusionStackScaleMul;
                FaceCamera(_fusionQueue[i], target);
                routines.Add(DuelTween.MoveTo(_fusionQueue[i].transform, target, 0.25f));
                routines.Add(DuelTween.ScaleTo(_fusionQueue[i].transform, Vector3.one * sc, 0.25f));
            }
        }
        else
        {
            float spacing = 1.9f;
            float x0 = -(_fusionQueue.Count - 1) * spacing * 0.5f;
            for (int i = 0; i < _fusionQueue.Count; i++)
            {
                Vector3 target = FusionAnchor() + new Vector3(x0 + i * spacing, 0f, 0f);
                routines.Add(DuelTween.MoveTo(_fusionQueue[i].transform, target, 0.25f));
            }
        }
        yield return DuelTween.Parallel(this, routines.ToArray());
    }

    /// <summary>
    /// La carta final de la cola desciende en arco a su slot y queda registrada
    /// como la vista de ese monstruo. El estado lógico ya debe estar aplicado.
    /// </summary>
    public IEnumerator AnimateFusionSummon(bool playerSide, int slot, Duelist owner)
    {
        var anchors = playerSide ? playerMonsterAnchors : opponentMonsterAnchors;
        var views = playerSide ? _playerMonsters : _opponentMonsters;

        Duel3DCardView view;
        if (_fusionQueue.Count > 0)
        {
            view = _fusionQueue[0];
            _fusionQueue.RemoveAt(0);
            ClearFusionQueue(); // por si quedara algo colgado
        }
        else
        {
            view = SpawnView(FusionAnchor());
        }

        views[slot] = view;
        view.Show(owner.MonsterZone[slot], owner.MonsterPositions[slot]);
        view.SetStats("");

        yield return DuelTween.Parallel(this,
            DuelTween.Arc(view.transform, view.transform.position, anchors[slot].position, 1.2f, 0.5f),
            DuelTween.ScaleTo(view.transform, Vector3.one, 0.5f));

        view.SetStats(StatsFor(owner, slot));
    }

    // ── Showcase 3D de UNA carta (invocación simple) ─────────────────────
    // Para que la carta invocada tenga SIEMPRE el mismo tamaño que en la fusión,
    // la invocación de una sola carta también usa una carta 3D del tablero (no la
    // 2D de la mano). Misma altura (FusionAnchor) y escala (fusionCardScale).

    private Duel3DCardView _showcaseView;

    public bool HasShowcase => _showcaseView != null;

    /// <summary>Alza UNA carta como pieza 3D al showcase, ARRANCANDO desde
    /// <paramref name="worldStart"/> (p. ej. su carta de la mano) y subiendo en arco.</summary>
    public IEnumerator ShowcaseRaise(CardData card, bool faceDown, Vector3 worldStart)
    {
        Vector3 anchor = FusionAnchor();
        if (_showcaseView == null)
            _showcaseView = SpawnView(worldStart);
        else
            _showcaseView.transform.position = worldStart;
        var v = _showcaseView;
        v.Show(card, faceDown ? CardPosition.FaceDownAttack : CardPosition.FaceUpAttack);
        v.SetStats("");
        v.transform.localScale = Vector3.one * 0.3f;
        FaceCamera(v, anchor);
        yield return DuelTween.Parallel(this,
            DuelTween.Arc(v.transform, worldStart, anchor, 1.0f, 0.4f),
            DuelTween.ScaleTo(v.transform, Vector3.one * fusionCardScale, 0.4f));
        FaceCamera(v, anchor);
    }

    /// <summary>Voltea la carta de showcase (encoge en X, cambia cara, expande).</summary>
    public IEnumerator ShowcaseFlip(bool faceDown)
    {
        if (_showcaseView == null) yield break;
        var v = _showcaseView;
        Vector3 s = v.transform.localScale;
        yield return DuelTween.ScaleTo(v.transform, new Vector3(0.02f, s.y, s.z), 0.1f);
        v.SetPosition(faceDown ? CardPosition.FaceDownAttack : CardPosition.FaceUpAttack);
        yield return DuelTween.ScaleTo(v.transform, s, 0.12f);
        FaceCamera(v, v.transform.position);
    }

    /// <summary>Vuela la carta de showcase a su casilla y la deja como carta 3D del tablero.</summary>
    public IEnumerator ShowcaseToSlot(int slot, Duelist owner)
    {
        if (_showcaseView == null) yield break;
        var v = _showcaseView;
        _showcaseView = null;
        _playerMonsters[slot] = v;   // pasa a ser la carta 3D del tablero
        v.Show(owner.MonsterZone[slot], owner.MonsterPositions[slot]);
        v.SetStats("");
        yield return DuelTween.Parallel(this,
            DuelTween.Arc(v.transform, v.transform.position, playerMonsterAnchors[slot].position, 1.0f, 0.5f),
            DuelTween.ScaleTo(v.transform, Vector3.one, 0.5f));
        v.SetStats(StatsFor(owner, slot));
    }

    /// <summary>
    /// Baja la carta de showcase de vuelta a su hueco en la mano: arco descendente
    /// hacia <paramref name="worldTarget"/> mientras encoge, y se retira. Se usa al
    /// CANCELAR una invocación para que la carta "regrese a la mano" en vez de esfumarse.
    /// </summary>
    public IEnumerator ShowcaseLowerToHand(Vector3 worldTarget)
    {
        if (_showcaseView == null) yield break;
        var v = _showcaseView;
        _showcaseView = null;
        Vector3 from = v.transform.position;
        yield return DuelTween.Parallel(this,
            DuelTween.Arc(v.transform, from, worldTarget, 0.55f, 0.30f),
            DuelTween.ScaleTo(v.transform, Vector3.one * 0.3f, 0.30f));
        Destroy(v.gameObject);
    }

    /// <summary>Retira la carta de showcase (cancelar / tras activarla).</summary>
    public void ClearShowcase()
    {
        if (_showcaseView != null) { Destroy(_showcaseView.gameObject); _showcaseView = null; }
    }

    /// <summary>Limpia cualquier carta flotante que quede en la cola de fusión.</summary>
    public void ClearFusionQueue()
    {
        foreach (var v in _fusionQueue)
            if (v != null) Destroy(v.gameObject);
        _fusionQueue.Clear();
    }

    // ── Efectos de fusión: remolino de luces (materiales compatibles) ─────
    // Todo procedural (paleta Neo-Kemet): una luz puntual real que fulgura y unas
    // "motas" de brillo (sprite radial generado en runtime) que espiralan al centro.

    private static readonly Color FusionGold = new Color(1f, 0.82f, 0.36f);
    private static readonly Color[] MotePalette =
    {
        new Color(1f, 0.85f, 0.40f),   // oro
        new Color(0.40f, 0.95f, 0.90f),// cian
        new Color(0.70f, 0.50f, 1f),   // violeta
        Color.white,
    };
    private static Sprite _glowSprite;

    /// <summary>
    /// Las dos primeras cartas orbitan en espiral hacia el centro entre destellos;
    /// al fundirse, un flash de luz y nace la carta fusionada con un "pop".
    /// </summary>
    private IEnumerator FusionVortex(Duel3DCardView a, Duel3DCardView b, Vector3 center, CardData stepResult)
    {
        // Antes del remolino, las dos cartas se JUNTAN cerca del centro (como en la
        // referencia): se acercan y se aprietan un poco, y de ahí arranca el vórtice.
        Vector3 gatherA = center + Vector3.left * 0.5f;
        Vector3 gatherB = center + Vector3.right * 0.5f;
        FaceCamera(a, gatherA);
        FaceCamera(b, gatherB);
        yield return DuelTween.Parallel(this,
            DuelTween.MoveTo(a.transform, gatherA, 0.3f),
            DuelTween.MoveTo(b.transform, gatherB, 0.3f),
            DuelTween.ScaleTo(a.transform, Vector3.one * fusionCardScale, 0.3f),
            DuelTween.ScaleTo(b.transform, Vector3.one * fusionCardScale, 0.3f));
        yield return new WaitForSeconds(0.05f);

        // Carga: chispas convergen al centro y un núcleo pulsa (implosión inminente),
        // telegrafiando el remolino y dándole peso al momento.
        yield return FusionChargeUp(center, a, b);

        // Luz central que crece durante el remolino.
        var light = SpawnFusionLight(center, FusionGold);

        // Núcleo + halo de brillo ADITIVO en el centro (bloom en pantalla): crecen y
        // palpitan con fuerza para que la luz del vórtice se vea intensa, no solo la
        // luz 3D (que apenas afecta a las cartas del canvas de mundo).
        var core = SpawnMote(center, out var coreSr);
        coreSr.color = new Color(0.6f, 0.85f, 1f, 0f);
        var halo = SpawnMote(center, out var haloSr);
        haloSr.color = new Color(0.5f, 0.8f, 1f, 0f);

        // Rayos radiantes (starburst) que giran y crecen tras el núcleo → haces de luz.
        var rays = SpawnFx(RaysSprite(), center, out var raysSr);
        raysSr.color = new Color(1f, 0.95f, 0.7f, 0f);
        float raysSpin = 0f;

        // Motas de brillo repartidas alrededor del centro (en el plano XY, de cara
        // a la cámara). Cada una guarda su radio/ángulo/z inicial para espiralar.
        const int MoteCount = 26;
        var motes = new List<Transform>();
        var moteSr = new List<SpriteRenderer>();
        var moteR0 = new List<float>();
        var moteA0 = new List<float>();
        var moteZ0 = new List<float>();
        var moteDir = new List<float>();   // sentido de giro (±1) → doble banda turbulenta
        for (int i = 0; i < MoteCount; i++)
        {
            float radius = UnityEngine.Random.Range(1.6f, 3.4f);
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float z = UnityEngine.Random.Range(-0.5f, 0.5f);
            var m = SpawnMote(MotePos(center, radius, ang, z), out var sr);
            m.localScale = Vector3.one * UnityEngine.Random.Range(0.18f, 0.34f);
            sr.color = MotePalette[UnityEngine.Random.Range(0, MotePalette.Length)];
            motes.Add(m); moteSr.Add(sr); moteR0.Add(radius); moteA0.Add(ang); moteZ0.Add(z);
            moteDir.Add(i % 3 == 0 ? -1f : 1f);   // ~1/3 contragira
        }

        // Datos iniciales de las cartas respecto al centro (radio/ángulo en XY).
        Vector3 offA = a.transform.position - center, offB = b.transform.position - center;
        float rA = new Vector2(offA.x, offA.y).magnitude, angA = Mathf.Atan2(offA.y, offA.x);
        float rB = new Vector2(offB.x, offB.y).magnitude, angB = Mathf.Atan2(offB.y, offB.x);
        Vector3 sA = a.transform.localScale, sB = b.transform.localScale;

        const float SpinDur = 1.5f;     // más largo: da tiempo a MUCHAS vueltas
        const float CardRevs = 5f;      // vueltas de las cartas (giran mucho antes de fundirse)
        const float MoteRevs = 10f;     // las motas giran aún más → swirl frenético
        for (float e = 0f; e < SpinDur; e += Time.deltaTime)
        {
            float t = e / SpinDur;
            float k = Mathf.SmoothStep(0f, 1f, t);   // contracción de radio/escala (suave)
            float spin = t * t;                      // giro ACELERANTE (arranca lento, se embala)

            if (a != null)
            {
                float r = Mathf.Lerp(rA, 0.12f, k), th = angA + CardRevs * 2f * Mathf.PI * spin;
                a.transform.position = MotePos(center, r, th, Mathf.Lerp(offA.z, 0f, k));
                a.transform.localScale = Vector3.Lerp(sA, Vector3.one * (fusionCardScale * 0.3f), k);
                FaceCamera(a, a.transform.position);
            }
            if (b != null)
            {
                float r = Mathf.Lerp(rB, 0.12f, k), th = angB + CardRevs * 2f * Mathf.PI * spin;
                b.transform.position = MotePos(center, r, th, Mathf.Lerp(offB.z, 0f, k));
                b.transform.localScale = Vector3.Lerp(sB, Vector3.one * (fusionCardScale * 0.3f), k);
                FaceCamera(b, b.transform.position);
            }
            for (int i = 0; i < motes.Count; i++)
            {
                float r = Mathf.Lerp(moteR0[i], 0.05f, k);
                float th = moteA0[i] + moteDir[i] * MoteRevs * 2f * Mathf.PI * spin;
                Vector3 mp = MotePos(center, r, th, Mathf.Lerp(moteZ0[i], 0f, k));
                motes[i].position = mp;
                // Cometa: orienta el brillo hacia el centro y lo estira a lo largo del
                // radio (se alarga al acelerar hacia dentro) → estelas de convergencia.
                Vector2 dir = new Vector2(center.x - mp.x, center.y - mp.y);
                float deg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                motes[i].rotation = Quaternion.Euler(0f, 0f, deg);
                motes[i].localScale = new Vector3(Mathf.Lerp(0.26f, 0.04f, k), Mathf.Lerp(0.5f, 1.5f, k), 1f);
                var c = moteSr[i].color; c.a = Mathf.Lerp(0.9f, 0f, k); moteSr[i].color = c;
            }
            // Un DESTELLO que se va formando: estrobo cada vez más rápido e intenso
            // conforme el remolino se cierra, culminando en el fogonazo de la fusión.
            float strobeFreq = Mathf.Lerp(10f, 55f, k);
            float strobe = Mathf.Abs(Mathf.Sin(e * strobeFreq));   // 0..1
            Color glowTint = Color.Lerp(new Color(0.55f, 0.8f, 1f), FusionGold, k); // frío → dorado

            // El brillo NO deja de amplificarse: crece con fuerza hacia el final
            // (curva acelerada) hasta fundirse con el fogonazo de la fusión.
            float bright = k * k;   // ease-in: arranca suave y se dispara al cerrarse

            if (light != null)
            {
                light.intensity = Mathf.Lerp(0.5f, 9f, bright) * (1f + strobe * Mathf.Lerp(0.2f, 2.2f, k));
                light.range = Mathf.Lerp(6f, 15f, k);
                light.color = glowTint;
            }
            // Núcleo incandescente (crece y destella) + halo amplio + rayos radiantes.
            if (core != null)
            {
                core.position = center;
                core.localScale = Vector3.one * Mathf.Lerp(0.5f, 4.5f, bright) * (0.8f + 0.5f * strobe);
                Color cc = Color.Lerp(glowTint, Color.white, Mathf.Lerp(0.4f, 1f, k)); // → blanco al final
                cc.a = Mathf.Lerp(0.2f, 1f, bright) * (0.7f + 0.3f * strobe);
                coreSr.color = cc;
                BillboardFull(core);
            }
            if (halo != null)
            {
                halo.position = center;
                halo.localScale = Vector3.one * Mathf.Lerp(1.6f, 11f, bright);
                Color hc = glowTint; hc.a = Mathf.Lerp(0.05f, 0.75f, bright) * (0.75f + 0.25f * strobe);
                haloSr.color = hc;
                BillboardFull(halo);
            }
            // Rayos radiantes: giran más rápido y crecen/brillan al cerrarse el remolino.
            if (rays != null)
            {
                rays.position = center;
                raysSpin += Mathf.Lerp(60f, 320f, k) * Time.deltaTime;
                rays.localScale = Vector3.one * Mathf.Lerp(1.5f, 9f, bright) * (0.9f + 0.25f * strobe);
                Color rc = glowTint; rc.a = Mathf.Lerp(0f, 0.8f, bright) * (0.6f + 0.4f * strobe);
                raysSr.color = rc;
                BillboardFull(rays);
                rays.Rotate(0f, 0f, raysSpin, Space.Self);
            }
            yield return null;
        }

        // El núcleo/halo/rayos del remolino se retiran (el clímax lo toma FusionBurst).
        if (core != null) Destroy(core.gameObject);
        if (halo != null) Destroy(halo.gameObject);
        if (rays != null) Destroy(rays.gameObject);

        // Fundido: desaparecen los materiales.
        if (a != null) Destroy(a.gameObject);
        if (b != null) Destroy(b.gameObject);
        _fusionQueue.RemoveRange(0, 2);

        // Nace la carta (diminuta): el estallido la REVELA emergiendo del fogonazo.
        var result = SpawnView(center);
        result.Show(stepResult, CardPosition.FaceUpAttack);
        result.SetStats("");
        FaceCamera(result, center);
        result.transform.localScale = Vector3.one * 0.05f;
        _fusionQueue.Insert(0, result);

        // Clímax: pilar de luz + fogonazo; la carta emerge (pop) DESDE el fogonazo, al
        // mismo tiempo que se apaga el brillo.
        yield return FusionBurst(center, light, result);

        // Limpieza de los efectos.
        foreach (var m in motes) if (m != null) Destroy(m.gameObject);
        if (light != null) Destroy(light.gameObject);
    }

    /// <summary>
    /// Beat de "carga" previo al remolino: un núcleo de luz late creciendo entre las
    /// dos cartas mientras chispas convergen volando hacia el centro, telegrafiando la
    /// implosión. Las cartas se aprietan un pelín hacia dentro por la tensión.
    /// </summary>
    private IEnumerator FusionChargeUp(Vector3 center, Duel3DCardView a, Duel3DCardView b)
    {
        const float dur = 0.4f;

        // Núcleo que pulsa creciendo (de cara a la cámara).
        var core = SpawnMote(center, out var coreSr);
        coreSr.color = new Color(0.7f, 0.9f, 1f, 0f);

        // Chispas que entran desde un anillo amplio, cada una con su retardo.
        const int Sparks = 14;
        var sp = new List<Transform>();
        var spSr = new List<SpriteRenderer>();
        var spR0 = new List<float>();
        var spA = new List<float>();
        var spDelay = new List<float>();
        for (int i = 0; i < Sparks; i++)
        {
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float r0 = UnityEngine.Random.Range(2.4f, 4.0f);
            var m = SpawnMote(MotePos(center, r0, ang, UnityEngine.Random.Range(-0.5f, 0.5f)), out var sr);
            sr.color = MotePalette[UnityEngine.Random.Range(0, MotePalette.Length)];
            m.gameObject.SetActive(false);
            sp.Add(m); spSr.Add(sr); spR0.Add(r0); spA.Add(ang);
            spDelay.Add(UnityEngine.Random.Range(0f, dur * 0.5f));
        }

        Vector3 pullA = center + Vector3.left * 0.36f;
        Vector3 pullB = center + Vector3.right * 0.36f;
        Vector3 a0 = a != null ? a.transform.position : pullA;
        Vector3 b0 = b != null ? b.transform.position : pullB;

        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur;

            // Núcleo: latido acelerado que crece.
            float pulse = 0.7f + 0.3f * (0.5f + 0.5f * Mathf.Sin(k * Mathf.PI * 6f));
            core.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.1f, k) * pulse;
            coreSr.color = new Color(0.7f, 0.9f, 1f, Mathf.Lerp(0f, 0.9f, k));
            BillboardFull(core);

            for (int i = 0; i < sp.Count; i++)
            {
                if (e < spDelay[i]) continue;
                if (!sp[i].gameObject.activeSelf) sp[i].gameObject.SetActive(true);
                float kk = Mathf.Clamp01((e - spDelay[i]) / (dur - spDelay[i]));
                float r = Mathf.Lerp(spR0[i], 0.05f, kk * kk);   // acelera hacia dentro
                Vector3 mp = MotePos(center, r, spA[i], 0f);
                sp[i].position = mp;
                Vector2 dir = new Vector2(center.x - mp.x, center.y - mp.y);
                float deg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                sp[i].rotation = Quaternion.Euler(0f, 0f, deg);
                sp[i].localScale = new Vector3(Mathf.Lerp(0.22f, 0.05f, kk), Mathf.Lerp(0.4f, 1.3f, kk), 1f);
                var c = spSr[i].color; c.a = Mathf.Lerp(0.95f, 0f, kk); spSr[i].color = c;
            }

            // Las cartas se aprietan un poco hacia el centro (tensión).
            if (a != null) { a.transform.position = Vector3.Lerp(a0, pullA, k); FaceCamera(a, a.transform.position); }
            if (b != null) { b.transform.position = Vector3.Lerp(b0, pullB, k); FaceCamera(b, b.transform.position); }
            yield return null;
        }

        foreach (var m in sp) if (m != null) Destroy(m.gameObject);
        if (core != null) Destroy(core.gameObject);
    }

    /// <summary>
    /// Estallido radial breve de chispas que salen disparadas desde un punto de impacto
    /// y se apagan enseguida (para golpes). No caen: es un fogonazo hacia afuera.
    /// </summary>
    private IEnumerator ImpactSparks(Vector3 center, Color color, int count = 10)
    {
        var sp = new List<Transform>();
        var srs = new List<SpriteRenderer>();
        var dirs = new List<Vector3>();
        var dist = new List<float>();
        for (int i = 0; i < count; i++)
        {
            float ang = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.2f, 0.2f);
            var m = SpawnMote(center, out var sr);
            sr.color = color;
            sp.Add(m); srs.Add(sr);
            dirs.Add(new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f));
            dist.Add(UnityEngine.Random.Range(1.2f, 2.6f));
        }
        const float dur = 0.32f;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur;
            float ease = 1f - (1f - k) * (1f - k);   // out-quad: sale rápido y frena
            for (int i = 0; i < sp.Count; i++)
            {
                if (sp[i] == null) continue;
                sp[i].position = center + dirs[i] * dist[i] * ease;
                sp[i].localScale = Vector3.one * Mathf.Lerp(0.28f, 0.05f, k);
                var c = srs[i].color; c.a = Mathf.Lerp(1f, 0f, k); srs[i].color = c;
                BillboardFull(sp[i]);
            }
            yield return null;
        }
        foreach (var m in sp) if (m != null) Destroy(m.gameObject);
    }

    /// <summary>Luz puntual temporal en un punto (para fulgor/flash de fusión).</summary>
    private Light SpawnFusionLight(Vector3 pos, Color color)
    {
        var go = new GameObject("FusionLight");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 7f;
        l.color = color;
        l.intensity = 0f;
        return l;
    }

    /// <summary>Sube la intensidad hasta un pico y la baja a 0 (flash).</summary>
    private IEnumerator FlashLight(Light l, float peak, float duration)
    {
        if (l == null) yield break;
        float up = duration * 0.35f, down = duration - up;
        float start = l.intensity;
        for (float e = 0f; e < up; e += Time.deltaTime)
        {
            if (l == null) yield break;
            l.intensity = Mathf.Lerp(start, peak, e / up);
            yield return null;
        }
        for (float e = 0f; e < down; e += Time.deltaTime)
        {
            if (l == null) yield break;
            l.intensity = Mathf.Lerp(peak, 0f, e / down);
            yield return null;
        }
        if (l != null) l.intensity = 0f;
    }

    /// <summary>Crea una mota de brillo (SpriteRenderer con el glow radial runtime).</summary>
    private Transform SpawnMote(Vector3 pos, out SpriteRenderer sr) => SpawnFx(GlowSprite(), pos, out sr);

    /// <summary>Crea un SpriteRenderer temporal con el sprite dado, por encima de las cartas.</summary>
    private Transform SpawnFx(Sprite sprite, Vector3 pos, out SpriteRenderer sr)
    {
        var go = new GameObject("FusionFx");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 3000; // por encima de las cartas del canvas de mundo
        return go.transform;
    }

    /// <summary>Posición en un círculo del plano XY alrededor de un centro.</summary>
    private static Vector3 MotePos(Vector3 center, float radius, float angleRad, float zOff)
        => center + new Vector3(Mathf.Cos(angleRad) * radius, Mathf.Sin(angleRad) * radius, zOff);

    /// <summary>Sprite radial (núcleo blanco → halo transparente) generado una vez en runtime.</summary>
    private static Sprite GlowSprite()
    {
        if (_glowSprite != null) return _glowSprite;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * a; // núcleo brillante con halo suave
                px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        _glowSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 64f);
        return _glowSprite;
    }

    private static Sprite _ringSprite;
    /// <summary>Anillo (annulus) para las ondas de choque; generado una vez en runtime.</summary>
    private static Sprite RingSprite()
    {
        if (_ringSprite != null) return _ringSprite;
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - 63.5f) / 63.5f, dy = (y - 63.5f) / 63.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float band = Mathf.Exp(-Mathf.Pow((d - 0.80f) / 0.12f, 2f)); // banda anular
                px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(band) * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 64f);
        return _ringSprite;
    }

    private static Sprite _raysSprite;
    /// <summary>Estrella de rayos (starburst) para el fogonazo; generada una vez en runtime.</summary>
    private static Sprite RaysSprite()
    {
        if (_raysSprite != null) return _raysSprite;
        const int S = 128; const float Spokes = 12f;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - 63.5f) / 63.5f, dy = (y - 63.5f) / 63.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float ray = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(ang * Spokes)), 8f); // picos afilados
                float radial = Mathf.Clamp01(1f - d);
                float a = Mathf.Max(ray * radial * radial, radial * radial * radial * 0.5f); // + núcleo
                px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        _raysSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 64f);
        return _raysSprite;
    }

    /// <summary>
    /// Clímax de invocación: un PILAR de luz sube desde el punto de fusión y estalla en
    /// un FOGONAZO blanco (con rayos giratorios y ondas de choque). Al apagarse el
    /// fogonazo, la carta <paramref name="result"/> emerge del brillo con un pop, al
    /// mismo tiempo. La luz puntual (la del vórtice) se reaprovecha para el destello.
    /// </summary>
    private IEnumerator FusionBurst(Vector3 center, Light light, Duel3DCardView result)
    {
        Vector3 hi = center + Vector3.up * 1.5f;

        // Ondas de choque (anillos cian) que se expanden — el sello "moderno".
        StartCoroutine(Shockwave(center + Vector3.up * 1.0f, 0f));
        StartCoroutine(Shockwave(center + Vector3.up * 1.0f, 0.10f));

        // Rayos giratorios (starburst) detrás del fogonazo.
        var rays = SpawnFx(RaysSprite(), hi, out var raysSr);
        raysSr.color = new Color(1f, 0.97f, 0.8f, 0f);
        float raysAngle = 0f;

        // 1) Pilar de luz que asciende + rayos que crecen y giran.
        var pillar = SpawnMote(center, out var pillarSr);
        pillarSr.color = new Color(1f, 0.96f, 0.75f, 0.95f);
        for (float e = 0f; e < 0.22f; e += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, e / 0.22f);
            pillar.localScale = new Vector3(Mathf.Lerp(0.5f, 1.6f, k), Mathf.Lerp(0.6f, 10f, k), 1f);
            pillar.position = center + Vector3.up * Mathf.Lerp(0f, 4f, k);
            BillboardUpright(pillar);

            raysAngle += 200f * Time.deltaTime;
            rays.localScale = Vector3.one * Mathf.Lerp(2f, 9f, k);
            raysSr.color = new Color(1f, 0.97f, 0.8f, k * 0.85f);
            BillboardFull(rays); rays.Rotate(0f, 0f, raysAngle, Space.Self);

            if (light != null) { light.intensity = Mathf.Lerp(3.5f, 8f, k); light.color = Color.Lerp(FusionGold, Color.white, k); }
            yield return null;
        }

        // 2) Fogonazo blanco que cubre la vista + sacudida de cámara (impacto).
        var flash = SpawnMote(hi, out var flashSr);
        flashSr.color = Color.white;
        BillboardFull(flash);
        StartCoroutine(CameraKick(0.22f, 0.30f));
        for (float e = 0f; e < 0.12f; e += Time.deltaTime)
        {
            float k = e / 0.12f;
            flash.localScale = Vector3.one * Mathf.Lerp(1f, 18f, k);
            var pc = pillarSr.color; pc.a = Mathf.Lerp(0.95f, 0f, k); pillarSr.color = pc;
            raysAngle += 220f * Time.deltaTime;
            rays.localScale = Vector3.one * Mathf.Lerp(9f, 13f, k);
            BillboardFull(rays); rays.Rotate(0f, 0f, raysAngle, Space.Self);
            yield return null;
        }
        if (light != null) light.intensity = 9f;

        // 3) REVELADO: el fogonazo se apaga y, AL MISMO TIEMPO, la carta emerge del
        //    brillo con un pop elástico (overshoot). Luz a resplandor frío; rayos se disipan.
        const float reveal = 0.45f;
        Vector3 rs0 = result != null ? result.transform.localScale : Vector3.one * 0.05f;
        Vector3 rs1 = Vector3.one * fusionCardScale;
        for (float e = 0f; e < reveal; e += Time.deltaTime)
        {
            float k = e / reveal;
            float fk = Mathf.Clamp01(k / 0.6f); // el fogonazo se apaga en la primera parte
            var c = flashSr.color; c.a = Mathf.Lerp(1f, 0f, fk); flashSr.color = c;
            flash.localScale = Vector3.one * Mathf.Lerp(18f, 22f, fk);
            BillboardFull(flash);
            raysAngle += 150f * Time.deltaTime;
            var rc = raysSr.color; rc.a = Mathf.Lerp(0.85f, 0f, fk); raysSr.color = rc;
            rays.localScale = Vector3.one * Mathf.Lerp(13f, 16f, fk);
            BillboardFull(rays); rays.Rotate(0f, 0f, raysAngle, Space.Self);
            if (result != null)
            {
                result.transform.localScale = Vector3.LerpUnclamped(rs0, rs1, BackOut(k)); // emerge del brillo
                FaceCamera(result, result.transform.position);
            }
            if (light != null) { light.intensity = Mathf.Lerp(9f, 1.8f, k); light.color = Color.Lerp(Color.white, new Color(0.5f, 0.9f, 1f), k); }
            yield return null;
        }
        if (result != null) result.transform.localScale = rs1;

        if (pillar != null) Destroy(pillar.gameObject);
        if (flash != null) Destroy(flash.gameObject);
        if (rays != null) Destroy(rays.gameObject);
    }

    /// <summary>Onda de choque: un anillo que se expande y se desvanece (de cara a la cámara).</summary>
    private IEnumerator Shockwave(Vector3 pos, float delay, Color? color = null)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        var ring = SpawnFx(RingSprite(), pos, out var sr);
        Color baseC = color ?? new Color(0.7f, 0.95f, 1f);
        sr.color = new Color(baseC.r, baseC.g, baseC.b, 0.9f);
        const float dur = 0.5f;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur;
            BillboardFull(ring);
            float s = Mathf.Lerp(0.5f, 13f, Mathf.SmoothStep(0f, 1f, k));
            ring.localScale = new Vector3(s, s, 1f);
            var c = sr.color; c.a = Mathf.Lerp(0.9f, 0f, k); sr.color = c;
            yield return null;
        }
        if (ring != null) Destroy(ring.gameObject);
    }

    /// <summary>Sacudida breve de la cámara (impacto del fogonazo); restaura la posición al acabar.</summary>
    private IEnumerator CameraKick(float amplitude, float duration)
    {
        if (mainCamera == null) yield break;
        Vector3 origin = mainCamera.transform.position;
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            if (mainCamera == null) yield break;
            float damp = 1f - e / duration;
            mainCamera.transform.position = origin + UnityEngine.Random.insideUnitSphere * amplitude * damp;
            yield return null;
        }
        if (mainCamera != null) mainCamera.transform.position = origin;
    }

    private static float BackOut(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float xm = x - 1f;
        return 1f + c3 * xm * xm * xm + c1 * xm * xm;
    }

    /// <summary>Orienta un sprite hacia la cámara MANTENIÉNDOLO vertical (para el pilar).</summary>
    private void BillboardUpright(Transform t)
    {
        if (mainCamera == null) return;
        Vector3 dir = t.position - mainCamera.transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    /// <summary>Orienta un sprite de cara plana a la cámara (para el fogonazo).</summary>
    private void BillboardFull(Transform t)
    {
        if (mainCamera == null) return;
        Vector3 dir = t.position - mainCamera.transform.position;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    // ── Batalla ──────────────────────────────────────────────────────────

    /// <summary>
    /// Boost visual por ventaja de Estrella Guardiana: "+500 ★" flota sobre
    /// la carta y esta late. Se muestra ANTES del ataque.
    /// </summary>
    public IEnumerator AnimateStarBoost(bool playerSide, int slot, int amount)
    {
        var views = playerSide ? _playerMonsters : _opponentMonsters;
        var view = views[slot];
        if (view == null) yield break;

        SpawnFloatingText(view.transform.position + Vector3.up * 1.2f, $"+{amount} ★",
                          new Color(1f, 0.85f, 0.3f));

        yield return DuelTween.ScaleTo(view.transform, Vector3.one * 1.12f, 0.15f);
        yield return DuelTween.ScaleTo(view.transform, Vector3.one, 0.15f);
        yield return new WaitForSeconds(0.35f);
    }

    /// <summary>
    /// Embestida de ataque. defSlot = -1 ⇒ ataque directo: el monstruo ataca
    /// "a la nada" hacia el lado rival. Con objetivo: golpe + sacudida.
    /// </summary>
    public IEnumerator AnimateAttack(bool attackerIsPlayer, int atkSlot, int defSlot)
    {
        var atkViews = attackerIsPlayer ? _playerMonsters : _opponentMonsters;
        var defViews = attackerIsPlayer ? _opponentMonsters : _playerMonsters;
        var attacker = atkViews[atkSlot];
        if (attacker == null) yield break;

        Vector3 origin = attacker.transform.position;
        Vector3 target;

        if (defSlot >= 0 && defViews[defSlot] != null)
        {
            // Golpea justo delante del objetivo.
            Vector3 defPos = defViews[defSlot].transform.position;
            target = Vector3.Lerp(origin, defPos, 0.82f) + Vector3.up * 0.4f;
        }
        else
        {
            // Ataque directo: embiste al vacío hacia el lado rival.
            Vector3 dir = attackerIsPlayer ? Vector3.forward : Vector3.back;
            target = origin + dir * 3.2f + Vector3.up * 0.5f;
        }

        yield return DuelTween.Lunge(attacker.transform, target, 0.5f);

        if (defSlot >= 0 && defViews[defSlot] != null)
            yield return DuelTween.Shake(defViews[defSlot].transform, 0.15f, 0.25f);
        else if (mainCamera != null)
            yield return DuelTween.Shake(mainCamera.transform, 0.08f, 0.2f);
    }

    /// <summary>Texto de daño flotante sobre un lado del campo.</summary>
    public void ShowDamageText(bool playerSide, int amount)
    {
        Vector3 basePos = playerSide ? playerSpawnPoint.position : opponentSpawnPoint.position;
        SpawnFloatingText(basePos + Vector3.up * 1.6f, $"-{amount} LP", new Color(1f, 0.35f, 0.3f));
    }

    /// <summary>Destrucción de un monstruo: gira, se encoge y se desvanece.</summary>
    public IEnumerator AnimateDestroy(bool playerSide, int slot)
    {
        var views = playerSide ? _playerMonsters : _opponentMonsters;
        var view = views[slot];
        if (view == null) yield break;
        views[slot] = null;

        var fade = view.CanvasGroup != null
            ? DuelTween.FadeCanvas(view.CanvasGroup, 1f, 0f, 0.45f)
            : DuelTween.ScaleTo(view.transform, Vector3.zero, 0.45f);

        yield return DuelTween.Parallel(this,
            DuelTween.Spin(view.transform, Vector3.up, 900f, 0.45f),
            DuelTween.ScaleTo(view.transform, Vector3.zero, 0.45f),
            fade);

        Destroy(view.gameObject);
    }

    // ── Internos ─────────────────────────────────────────────────────────

    private Duel3DCardView SpawnView(Vector3 position)
    {
        var go = Instantiate(cardTemplate.gameObject, transform);
        go.transform.position = position;
        go.transform.localScale = Vector3.one;
        go.SetActive(true);
        return go.GetComponent<Duel3DCardView>();
    }

    /// <summary>Orienta el canvas de una carta flotante hacia la cámara.</summary>
    private void FaceCamera(Duel3DCardView view, Vector3 atPosition)
    {
        if (view.CardCanvas == null || mainCamera == null) return;
        Vector3 away = atPosition - mainCamera.transform.position;
        if (away.sqrMagnitude < 0.001f) away = Vector3.forward;
        view.CardCanvas.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
    }

    /// <summary>Crea un texto flotante 3D que sube y se desvanece solo.</summary>
    private void SpawnFloatingText(Vector3 position, string text, Color color)
    {
        var go = new GameObject("FloatingText");
        go.transform.SetParent(transform, false);
        go.transform.position = position;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(400, 120);
        rt.localScale = Vector3.one * 0.012f;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = 72;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        var textRT = (RectTransform)textGO.transform;
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero; textRT.offsetMax = Vector2.zero;

        if (mainCamera != null)
        {
            Vector3 away = position - mainCamera.transform.position;
            go.transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        StartCoroutine(FloatAndFade(go, tmp));
    }

    private IEnumerator FloatAndFade(GameObject go, TextMeshProUGUI tmp)
    {
        const float dur = 1.1f;
        Vector3 start = go.transform.position;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            if (go == null) yield break;
            float k = e / dur;
            go.transform.position = start + Vector3.up * (k * 1.2f);
            if (tmp != null) tmp.alpha = 1f - (k * k);
            yield return null;
        }
        if (go != null) Destroy(go);
    }
}
