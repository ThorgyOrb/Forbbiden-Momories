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
    public IEnumerator AnimateFusionGather(List<CardData> materials)
    {
        ClearFusionQueue();

        float spacing = 1.9f;
        float x0 = -(materials.Count - 1) * spacing * 0.5f;

        var routines = new List<IEnumerator>();
        for (int i = 0; i < materials.Count; i++)
        {
            Vector3 target = fusionPoint.position + new Vector3(x0 + i * spacing, 0f, 0f);
            var view = SpawnView(playerSpawnPoint.position);
            view.Show(materials[i], CardPosition.FaceUpAttack);
            view.SetStats("");
            FaceCamera(view, target);
            view.transform.localScale = Vector3.one * 0.3f;
            _fusionQueue.Add(view);

            routines.Add(DuelTween.MoveTo(view.transform, target, 0.45f));
            routines.Add(DuelTween.ScaleTo(view.transform, Vector3.one * 0.85f, 0.45f));
        }
        yield return DuelTween.Parallel(this, routines.ToArray());
        yield return new WaitForSeconds(0.15f);
    }

    /// <summary>
    /// Resuelve UN paso de la cadena entre las dos primeras cartas de la cola:
    ///   • Fusión (específica/categoría): chocan, destello, aparece la nueva.
    ///   • Equipo: la segunda es ABSORBIDA por la primera (se encoge dentro).
    ///   • Incompatible (absorción): la perdedora sale girando de la mesa.
    /// </summary>
    public IEnumerator AnimateFusionStep(FusionStepType type, CardData stepResult, bool firstSurvives)
    {
        if (_fusionQueue.Count < 2) yield break;

        var a = _fusionQueue[0];
        var b = _fusionQueue[1];
        Vector3 center = (a.transform.position + b.transform.position) * 0.5f;

        switch (type)
        {
            case FusionStepType.Specific:
            case FusionStepType.Category:
            {
                // Chocan en el centro…
                yield return DuelTween.Parallel(this,
                    DuelTween.MoveTo(a.transform, center, 0.3f),
                    DuelTween.MoveTo(b.transform, center, 0.3f),
                    DuelTween.ScaleTo(a.transform, Vector3.one * 0.55f, 0.3f),
                    DuelTween.ScaleTo(b.transform, Vector3.one * 0.55f, 0.3f));

                Destroy(a.gameObject);
                Destroy(b.gameObject);
                _fusionQueue.RemoveRange(0, 2);

                // …y nace la carta fusionada con un "pop".
                var result = SpawnView(center);
                result.Show(stepResult, CardPosition.FaceUpAttack);
                result.SetStats("");
                FaceCamera(result, center);
                result.transform.localScale = Vector3.one * 0.1f;
                _fusionQueue.Insert(0, result);

                yield return DuelTween.ScaleTo(result.transform, Vector3.one * 1.1f, 0.25f);
                yield return DuelTween.ScaleTo(result.transform, Vector3.one * 0.85f, 0.15f);
                break;
            }

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
                yield return DuelTween.ScaleTo(a.transform, Vector3.one * 1.05f, 0.12f);
                yield return DuelTween.ScaleTo(a.transform, Vector3.one * 0.85f, 0.12f);
                break;
            }

            case FusionStepType.Absorption:
            {
                // Materiales incompatibles: la perdedora se descarta girando.
                var loser = firstSurvives ? b : a;
                var winner = firstSurvives ? a : b;

                var fade = loser.CanvasGroup != null
                    ? DuelTween.FadeCanvas(loser.CanvasGroup, 1f, 0f, 0.5f)
                    : DuelTween.ScaleTo(loser.transform, Vector3.zero, 0.5f);

                yield return DuelTween.Parallel(this,
                    DuelTween.MoveTo(loser.transform, discardPoint.position, 0.5f),
                    DuelTween.Spin(loser.transform, Vector3.forward, 720f, 0.5f),
                    fade);

                Destroy(loser.gameObject);
                _fusionQueue.Remove(loser);

                // La superviviente pasa al frente de la cola.
                _fusionQueue.Remove(winner);
                _fusionQueue.Insert(0, winner);

                // Si sobrevivió la segunda, muestra la carta del paso (por claridad).
                winner.Show(stepResult, CardPosition.FaceUpAttack);
                FaceCamera(winner, winner.transform.position);
                break;
            }
        }

        // Reacomoda la cola restante en fila.
        yield return RealignFusionQueue();
    }

    private IEnumerator RealignFusionQueue()
    {
        if (_fusionQueue.Count == 0) yield break;
        float spacing = 1.9f;
        float x0 = -(_fusionQueue.Count - 1) * spacing * 0.5f;

        var routines = new List<IEnumerator>();
        for (int i = 0; i < _fusionQueue.Count; i++)
        {
            Vector3 target = fusionPoint.position + new Vector3(x0 + i * spacing, 0f, 0f);
            routines.Add(DuelTween.MoveTo(_fusionQueue[i].transform, target, 0.25f));
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
            view = SpawnView(fusionPoint.position);
        }

        views[slot] = view;
        view.Show(owner.MonsterZone[slot], owner.MonsterPositions[slot]);
        view.SetStats("");

        yield return DuelTween.Parallel(this,
            DuelTween.Arc(view.transform, view.transform.position, anchors[slot].position, 1.2f, 0.5f),
            DuelTween.ScaleTo(view.transform, Vector3.one, 0.5f));

        view.SetStats(StatsFor(owner, slot));
    }

    /// <summary>Limpia cualquier carta flotante que quede en la cola de fusión.</summary>
    public void ClearFusionQueue()
    {
        foreach (var v in _fusionQueue)
            if (v != null) Destroy(v.gameObject);
        _fusionQueue.Clear();
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
