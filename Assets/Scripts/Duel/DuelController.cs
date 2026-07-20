using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador del duelo 3D. Orquesta reglas + presentación:
///
///   Preparación → Barrido de cámara (intro) → [Robo → Principal → Batalla → Final]* → Fin
///
/// Flujos con animación (estilo Forbidden Memories):
///   • Invocar/colocar UNA carta: seleccionar carta → posición (boca
///     arriba/abajo, ATK/DEF) → Estrella Guardiana → animación de invocación.
///   • Fusión: elegir materiales en orden → cola de fusión flotante →
///     por pareja: fusión (destello), equipo (absorción) o incompatible
///     (descarte girando) → Estrella Guardiana → invocación del resultado.
///   • Ataque: elegir atacante → elegir objetivo (o directo al vacío) →
///     boost "+500 ★" si hay ventaja de estrella → embestida → destrucción.
///   • Final: banner ¡VICTORIA!/DERROTA → estadísticas + rango + premio.
///
/// Las reglas viven aquí; la física/animación en <see cref="DuelBoard3D"/> y
/// la interfaz en <see cref="DuelScreen"/>.
/// </summary>
public class DuelController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private DuelScreen screen;
    [SerializeField] private DuelBoard3D board;
    [SerializeField] private FusionDatabase fusionDb;

    [Header("Config de prueba (solo si entras a la escena directamente)")]
    [SerializeField] private DuelConfig testConfig;

    // ── Estado del duelo ──────────────────────────────────────────────────
    public DuelPhase Phase { get; private set; }
    public DuelResult Result { get; private set; } = DuelResult.None;
    public bool IsPlayerTurn { get; private set; } = true;

    public Duelist Player { get; private set; }
    public Duelist Opponent { get; private set; }

    private DuelAI _ai;
    private TerrainType _terrain;
    private OpponentData _opponent;
    private DuelConfig _overrides;
    private bool _hasSummonedThisTurn;

    // ── Control por teclado (estilo FM) ───────────────────────────────────
    //   ←/→ mover · ↑ marcar fusión (mano) / fila (campo) · A confirmar ·
    //   S/Esc atrás · W posición ATK/DEF · E batalla / terminar turno.
    private enum KeyCtx { None, Hand, Raised, SlotSelect, Star, Board, Target, FieldRaised }

    private KeyCtx _ctx = KeyCtx.None;
    private bool _busy;                     // bloquea input durante animaciones
    private int _handCursor;                // índice del selector en la mano
    private int _raisedIndex = -1;          // carta alzada al centro
    private bool _raisedFaceDown;           // cara elegida con ←/→
    private readonly List<int> _fusionOrder = new();  // índices de mano marcados con ↑
    private bool _slotRowMonsters;          // fila del selector de casilla
    private int _slotCursor;                // casilla elegida 0..4
    private int _boardRow;                  // 0=monstruos, 1=magias (ctx Board)
    private int _boardCursor;
    private int _targetCursor;
    private int _attackerSlot = -1;
    private int _fieldSlot = -1;            // magia/trampa del campo alzada
    private bool _fieldRaisedFaceDown;
    private int _playerTurnCount;           // sin ataque directo en el turno 1

    // Selección de Estrella Guardiana (↑/↓ resalta, A confirma).
    private bool _awaitingStar;
    private bool _starHoverA = true;
    private GuardianStar _chosenStar;
    private CardData _starCard;

    private AudioSource _music;

    // ── Arranque ──────────────────────────────────────────────────────────

    void Start()
    {
        GameNavigator.EnsureExists();
        PlayerCollection.EnsureExists();

        WireScreen();
        StartCoroutine(RunDuel());
    }

    void Update()
    {
        if (Result != DuelResult.None) return;

        // Estrella Guardiana: modal, siempre por encima del resto del input.
        if (_awaitingStar)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)) { _starHoverA = true; screen.HighlightStar(true); }
            else if (Input.GetKeyDown(KeyCode.DownArrow)) { _starHoverA = false; screen.HighlightStar(false); }
            else if (Input.GetKeyDown(KeyCode.A)) ResolveStar(_starHoverA);
            return;
        }

        if (_busy || !IsPlayerTurn) return;

        switch (_ctx)
        {
            case KeyCtx.Hand: HandInput(); break;
            case KeyCtx.Raised: RaisedInput(); break;
            case KeyCtx.SlotSelect: SlotInput(); break;
            case KeyCtx.Board: BoardInput(); break;
            case KeyCtx.Target: TargetInput(); break;
            case KeyCtx.FieldRaised: FieldRaisedInput(); break;
        }
    }

    private void WireScreen()
    {
        // El duelo se juega con TECLADO; solo quedan clicables la Estrella
        // Guardiana (opcional, también ↑/↓+A) y los botones del resultado.
        screen.BtnStarA.onClick.AddListener(() => ResolveStar(useA: true));
        screen.BtnStarB.onClick.AddListener(() => ResolveStar(useA: false));

        screen.BtnRematch.onClick.AddListener(Rematch);
        screen.BtnBackMenu.onClick.AddListener(() => GameNavigator.EnsureExists().ToMainMenu());

        screen.ShowMainButtons(false);
        screen.ShowBattleButtons(false);
    }

    // ── Preparación + presentación ────────────────────────────────────────

    private IEnumerator RunDuel()
    {
        Phase = DuelPhase.Setup;
        _busy = true;

        bool fromLauncher = DuelLauncher.PendingOpponent != null;
        _opponent = fromLauncher ? DuelLauncher.PendingOpponent
                                 : (testConfig != null ? testConfig.opponent : null);
        _overrides = DuelLauncher.PendingConfig != null ? DuelLauncher.PendingConfig : testConfig;
        DuelLauncher.Clear();

        if (_opponent == null)
        {
            Debug.LogError("DuelController: no hay oponente (ni DuelLauncher ni testConfig).");
            screen.Log("ERROR: no hay oponente configurado.");
            yield break;
        }
        Debug.Log($"DuelController: rival '{_opponent.opponentName}' (id {_opponent.opponentId}) — " +
                  (fromLauncher ? "seleccionado en runtime (DuelLauncher)." : "config de prueba de la escena."));

        TerrainType tOverride = _overrides != null ? _overrides.terrainOverride : TerrainType.Neutral;
        _terrain = tOverride != TerrainType.Neutral ? tOverride : _opponent.arena;

        Player = new Duelist("Jugador", isHuman: true);
        Opponent = new Duelist(string.IsNullOrEmpty(_opponent.opponentName) ? "Rival" : _opponent.opponentName,
                               isHuman: false);
        Player.LoadDeck(ResolvePlayerDeck());
        Opponent.LoadDeck(ResolveOpponentDeck());
        Player.ShuffleDeck();
        Opponent.ShuffleDeck();

        _ai = new DuelAI(_opponent.aiLevel, _opponent.aiStrategy, fusionDb);

        PlayerCollection.Instance?.MarkOpponentFound(_opponent.opponentId);
        PlayBattleMusic();

        screen.SetOpponentName(Opponent.Name);
        screen.SetTerrain(_terrain);
        board.SetTerrain(_terrain);
        screen.ShowTurn("");
        screen.ShowPhase("Preparación");

        // Presentación (~10 s) estilo Forbidden Memories:
        //   negro absoluto → los datos del rival/CAMPO/LP aparecen con un
        //   desvanecido → los LP de ambos suben de 0 a 8000 → y al llegar a
        //   8000 el tablero emerge de la oscuridad girando lentamente hasta
        //   la vista del jugador (su mano).
        screen.PrepareIntroHud();                       // HUD invisible, LP en 0
        screen.SetBlackout(true);
        yield return new WaitForSeconds(0.7f);          // negro absoluto (expectativa)
        yield return screen.FadeInHud(1.3f);            // rival + CAMPO + LP aparecen
        yield return screen.AnimateLPCountUp(Player.LP, Opponent.LP, 2.6f); // 0 → 8000
        yield return new WaitForSeconds(0.2f);
        StartCoroutine(screen.FadeFromBlack(2.8f));     // el negro se disuelve…
        yield return board.PlayIntro();                 // …mientras el tablero gira (5 s)
        yield return new WaitForSeconds(0.2f);

        // Mano inicial: ambos roban 5. Tus cartas entran repartidas desde la derecha.
        Player.DrawUpToFive();
        Opponent.DrawUpToFive();
        foreach (var c in Player.Hand)
            yield return screen.AnimateDrawToHand(c);
        screen.RefreshHand(Player.Hand);
        RefreshCounts();
        screen.Log($"¡Comienza el duelo contra {Opponent.Name}!");

        _busy = false;
        IsPlayerTurn = true; // el jugador siempre va primero
        StartCoroutine(RunTurn());
    }

    // ── Bucle de turno ────────────────────────────────────────────────────

    private IEnumerator RunTurn()
    {
        var current = IsPlayerTurn ? Player : Opponent;

        Phase = DuelPhase.Setup;
        current.ResetTurnFlags();
        _hasSummonedThisTurn = false;
        screen.ShowTurn(IsPlayerTurn ? "— TU TURNO —" : $"— Turno de {Opponent.Name} —");

        Phase = DuelPhase.DrawPhase;
        screen.ShowPhase("Fase de Robo");

        if (IsPlayerTurn)
        {
            _playerTurnCount++;   // el ataque directo se bloquea en el turno 1
            screen.HideFieldBar();
            screen.HideTargetBar();
            screen.SetHandVisible(true);   // la mano regresa para tu turno
            var drawn = Player.DrawUpToFive();
            foreach (var c in drawn)
            {
                screen.Log($"  Robaste: {c.cardName}");
                yield return screen.AnimateDrawToHand(c);
            }
            screen.RefreshHand(Player.Hand);
            if (Player.DeckOut) { StartCoroutine(EndSequence(DuelResult.OpponentWin, "Te quedaste sin cartas (Deck Out).")); yield break; }
        }
        else
        {
            Opponent.DrawUpToFive();
            if (Opponent.DeckOut) { StartCoroutine(EndSequence(DuelResult.PlayerWin, "¡El rival se quedó sin cartas!")); yield break; }
        }

        RefreshCounts();
        yield return new WaitForSeconds(0.5f);

        Phase = DuelPhase.MainPhase;
        screen.ShowPhase("Fase Principal");

        if (IsPlayerTurn)
        {
            EnterHandContext();
            screen.Log("Mano — ←→: mover · ↑: fusión · A: elegir · E: ir a batalla.");
        }
        else
        {
            yield return StartCoroutine(RunAIMainPhase());
        }
    }

    // ── Contexto MANO (fase principal) ────────────────────────────────────

    private void EnterHandContext()
    {
        _ctx = KeyCtx.Hand;
        _handCursor = Mathf.Clamp(_handCursor, 0, Mathf.Max(0, Player.Hand.Count - 1));
        board.ClearHighlights();
        board.HideSlotCursor();
        RefreshHandCursor();
    }

    private void RefreshHandCursor()
    {
        if (Player.Hand.Count == 0) { screen.HideHandCursor(); return; }
        screen.ShowHandCursor(_handCursor);
        screen.ShowCardInfo(Player.Hand[_handCursor]);
    }

    private void HandInput()
    {
        int n = Player.Hand.Count;
        if (Input.GetKeyDown(KeyCode.LeftArrow) && n > 0)
        { _handCursor = (_handCursor + n - 1) % n; RefreshHandCursor(); }
        else if (Input.GetKeyDown(KeyCode.RightArrow) && n > 0)
        { _handCursor = (_handCursor + 1) % n; RefreshHandCursor(); }
        else if (Input.GetKeyDown(KeyCode.UpArrow) && n > 0)
        { ToggleFusionMark(_handCursor); }
        else if (Input.GetKeyDown(KeyCode.A) && n > 0)
        {
            if (_fusionOrder.Count > 0) BeginFusionPlacement();
            else StartCoroutine(RaiseRoutine(_handCursor));
        }
        else if (Input.GetKeyDown(KeyCode.E))
        { GoToBattle(); }
    }

    /// <summary>↑ sobre una carta de la mano: entra/sale de la lista de fusión.</summary>
    private void ToggleFusionMark(int index)
    {
        var card = Player.Hand[index];
        if (!card.IsMonster && !card.IsEquip)
        {
            screen.Log("Solo Monstruos y Equipos entran en la fusión.");
            return;
        }
        if (!_fusionOrder.Remove(index)) _fusionOrder.Add(index);
        RefreshFusionBadges();
    }

    private void RefreshFusionBadges()
    {
        screen.ClearFusionBadges();
        for (int i = 0; i < _fusionOrder.Count; i++)
            screen.ShowFusionBadge(_fusionOrder[i], i + 1);
    }

    private void BeginFusionPlacement()
    {
        if (_hasSummonedThisTurn) { screen.Log("Ya invocaste/fusionaste este turno."); return; }
        _raisedIndex = -1;
        _raisedFaceDown = false;   // el resultado de fusión siempre va boca arriba
        EnterSlotSelect(monsterRow: true);
    }

    // ── Contexto CARTA ALZADA (elige cara con ←/→, confirma con A) ────────

    private IEnumerator RaiseRoutine(int index)
    {
        var card = Player.Hand[index];

        if (card.IsMonster && _hasSummonedThisTurn)
        { screen.Log("Ya invocaste/fusionaste este turno."); yield break; }
        if (card.IsEquip)
        { screen.Log($"{card.cardName}: los Equipos se usan como material de fusión (↑)."); yield break; }
        if (!card.IsMonster && !card.IsSpell && !card.IsTrap)
        { screen.Log($"{card.cardName} ({card.CategoryLabel}): aún no se puede jugar."); yield break; }

        _busy = true;
        _raisedIndex = index;
        _raisedFaceDown = false;
        screen.HideHandCursor();
        // La carta 3D se LEVANTA desde su posición en la mano (mismo tamaño que la fusión).
        Vector3 raiseStart = board.HandStartWorld(screen.HandCardScreenPos(index));
        screen.SetHandCardVisible(index, false);   // desaparece de la mano al levantarse
        yield return board.ShowcaseRaise(card, _raisedFaceDown, raiseStart);
        screen.ShowFlipArrows(true, 100f);   // flechas al centro de la carta
        screen.ShowCardInfo(card);   // el InfoBar de la mano sigue visible
        _busy = false;
        _ctx = KeyCtx.Raised;
    }

    private void RaisedInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            StartCoroutine(FlipRaisedRoutine());
        else if (Input.GetKeyDown(KeyCode.A))
            ConfirmRaised();
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Escape))
            CancelToHand();
    }

    private IEnumerator FlipRaisedRoutine()
    {
        _busy = true;
        _raisedFaceDown = !_raisedFaceDown;
        yield return board.ShowcaseFlip(_raisedFaceDown);
        _busy = false;
    }

    /// <summary>Baja la carta alzada (o el selector de casilla) y vuelve a la mano.</summary>
    private void CancelToHand() => StartCoroutine(CancelToHandRoutine());

    private IEnumerator CancelToHandRoutine()
    {
        _busy = true;
        screen.ShowFlipArrows(false);
        board.HideSlotCursor();

        // Si hay una carta 3D alzada, BAJA de vuelta a su hueco en la mano (animado);
        // si no (p. ej. cancelando desde el selector de casilla), retiro directo.
        if (board.HasShowcase && _raisedIndex >= 0)
            yield return board.ShowcaseLowerToHand(
                board.HandStartWorld(screen.HandCardScreenPos(_raisedIndex)));
        else
            board.ClearShowcase();   // por si quedó la carta 3D del showcase

        _raisedIndex = -1;
        screen.RefreshHand(Player.Hand);   // restaura la carta 2D en su hueco
        RefreshFusionBadges();
        _busy = false;
        EnterHandContext();
    }

    private void ConfirmRaised()
    {
        var card = Player.Hand[_raisedIndex];

        if (card.IsMonster)
        {
            StartCoroutine(LowerThenSlotSelect(monsterRow: true));
        }
        else if (card.IsSpell && !_raisedFaceDown)
        {
            StartCoroutine(CastRaisedSpellRoutine());   // boca arriba = se activa ya
        }
        else
        {
            if (card.IsTrap && !_raisedFaceDown)
            { screen.Log("Las trampas se colocan BOCA ABAJO (voltéala con ←/→)."); return; }
            StartCoroutine(LowerThenSlotSelect(monsterRow: false)); // boca abajo = se setea
        }
    }

    /// <summary>Retira la carta 3D del showcase y pasa a elegir la casilla;
    /// la cara/índice elegidos se conservan.</summary>
    private IEnumerator LowerThenSlotSelect(bool monsterRow)
    {
        screen.ShowFlipArrows(false);
        board.ClearShowcase();   // retira la carta 3D del showcase mientras se elige casilla
        screen.RefreshHand(Player.Hand);   // restaura la carta que se había levantado de la mano
        EnterSlotSelect(monsterRow);
        yield break;
    }

    // ── Contexto SELECTOR DE CASILLA ──────────────────────────────────────

    private void EnterSlotSelect(bool monsterRow) => StartCoroutine(EnterSlotSelectRoutine(monsterRow));

    /// <summary>La cámara baja a enfocar la zona de destino y aparece la barra
    /// de campo (sobre la mano) con el contenido de la casilla apuntada.</summary>
    private IEnumerator EnterSlotSelectRoutine(bool monsterRow)
    {
        _busy = true;
        screen.ShowFlipArrows(false);
        // Vista cenital del campo (muestra las dos filas). La info de la carta a
        // invocar SIGUE en el InfoBar de la mano (la mano aún está visible).
        if (_raisedIndex >= 0) screen.ShowCardInfo(Player.Hand[_raisedIndex]);
        yield return board.MoveCamera(DuelBoard3D.CameraView.MonsterZone, 0.5f);

        _slotRowMonsters = monsterRow;
        _slotCursor = FirstFreeSlot(monsterRow ? Player.MonsterZone : Player.SpellZone);
        if (_slotCursor < 0) _slotCursor = 0;
        _busy = false;
        _ctx = KeyCtx.SlotSelect;
        RefreshSlotCursor();
    }

    private static int FirstFreeSlot(CardData[] zone)
    {
        for (int i = 0; i < zone.Length; i++)
            if (zone[i] == null) return i;
        return -1;
    }

    private void RefreshSlotCursor()
    {
        board.ShowSlotCursor(true, _slotRowMonsters, _slotCursor);
        var zone = _slotRowMonsters ? Player.MonsterZone : Player.SpellZone;
        screen.ShowFieldBar(zone[_slotCursor], bottom: false);
    }

    private void SlotInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        { _slotCursor = (_slotCursor + 4) % 5; RefreshSlotCursor(); }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        { _slotCursor = (_slotCursor + 1) % 5; RefreshSlotCursor(); }
        else if (Input.GetKeyDown(KeyCode.A))
            ConfirmSlot();
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Escape))
            StartCoroutine(CancelSlotRoutine());
    }

    /// <summary>Cancela la elección de casilla: la cámara vuelve a la vista normal.</summary>
    private IEnumerator CancelSlotRoutine()
    {
        _busy = true;
        board.HideSlotCursor();
        screen.HideFieldBar();
        yield return board.MoveCamera(DuelBoard3D.CameraView.Play, 0.5f);
        _busy = false;
        CancelToHand();
    }

    private void ConfirmSlot()
    {
        // Fila de magias: solo se setea en casilla libre.
        if (!_slotRowMonsters)
        {
            if (Player.SpellZone[_slotCursor] != null)
            { screen.Log("Esa casilla está ocupada."); return; }
            board.HideSlotCursor();
            StartCoroutine(SetCardRoutine(_raisedIndex, _slotCursor));
            return;
        }

        // Fila de monstruos: invocación simple o fusión (lista ↑ y/o casilla ocupada).
        bool viaFusionList = _fusionOrder.Count > 0;
        var handIdx = viaFusionList ? new List<int>(_fusionOrder) : new List<int> { _raisedIndex };
        var materials = new List<CardData>();
        foreach (var i in handIdx) materials.Add(Player.Hand[i]);

        bool slotOccupied = Player.MonsterZone[_slotCursor] != null;
        if (slotOccupied)
            materials.Insert(0, Player.MonsterZone[_slotCursor]);   // el del campo va PRIMERO

        board.HideSlotCursor();
        if (!slotOccupied && materials.Count == 1)
        {
            bool faceDown = !viaFusionList && _raisedFaceDown;
            StartCoroutine(SummonSingleRoutine(handIdx[0], _slotCursor, faceDown));
        }
        else
        {
            StartCoroutine(FusionAtSlotRoutine(materials, handIdx, _slotCursor, slotOccupied));
        }
    }

    // ── Invocación de UNA carta (casilla ya elegida → estrella → animación) ─

    private IEnumerator SummonSingleRoutine(int handIndex, int slot, bool faceDown)
    {
        var card = Player.Hand[handIndex];

        // Fase de estrella: la mano se retira, la carta a invocar se vuelve a
        // alzar (centro-arriba) con el panel de estrella debajo, y la CÁMARA
        // regresa a su posición original (vista de juego).
        _busy = true;
        screen.HideFieldBar();
        screen.HideCardInfo();
        // La cámara vuelve a la vista de juego y luego la carta se LEVANTA desde la mano
        // (mientras el resto de la mano se retira).
        yield return board.MoveCamera(DuelBoard3D.CameraView.Play, 0.5f);
        Vector3 summonStart = board.HandStartWorld(screen.HandCardScreenPos(handIndex));
        yield return DuelTween.Parallel(this,
            board.ShowcaseRaise(card, faceDown, summonStart),
            screen.SlideHandDown(0.3f));

        _ctx = KeyCtx.Star;
        yield return WaitForStarChoice(card);
        GuardianStar star = _chosenStar;

        Player.Hand.RemoveAt(handIndex);
        _fusionOrder.Clear();
        _raisedIndex = -1;

        // La cara se eligió al alzar la carta; la posición de batalla inicial es
        // Ataque (vertical) y se cambia con W ya en el tablero.
        var pos = faceDown ? CardPosition.FaceDownAttack : CardPosition.FaceUpAttack;
        // ATK en campo = base + terreno (misma estrella vs sí misma = sin bonus);
        // la ventaja de estrella se evalúa por batalla.
        int atk = CombatCalculator.CalculateAtk(card, star, star, _terrain);
        int def = CombatCalculator.CalculateDef(card);
        Player.PlaceMonsterAt(slot, card, pos, atk, def, star);
        _hasSummonedThisTurn = true;

        screen.ShowFlipArrows(false);

        // 1) La carta 3D del showcase vuela a su casilla (mismo tamaño de siempre) y
        //    queda registrada como el monstruo 3D del tablero.
        yield return board.ShowcaseToSlot(slot, Player);

        // 2) Normaliza el campo (la carta ya está colocada).
        board.SyncField(Player, Opponent);
        screen.RefreshHand(Player.Hand);

        // 3) SOLO cuando ya está colocada, la cámara baja al campo (cenital).
        yield return board.MoveCamera(DuelBoard3D.CameraView.PlayerField, 0.55f);

        screen.Log(faceDown
            ? "Colocas un monstruo boca abajo."
            : $"¡{card.cardName}! (ATK {atk} / DEF {def}, ★{star})");

        _busy = false;
        EnterBattlePhase();   // ya en el campo del jugador → directo a la batalla
    }

    /// <summary>Muestra el panel de estrella y espera ↑/↓ + A (o clic).</summary>
    private IEnumerator WaitForStarChoice(CardData card)
    {
        _awaitingStar = true;
        _starCard = card;
        _chosenStar = card.starA;
        _starHoverA = true;
        screen.BtnCancelStar.gameObject.SetActive(false);
        screen.ShowStarPanel(card);
        screen.HighlightStar(true);

        while (_awaitingStar) yield return null;
        screen.HideStarPanel();
    }

    private void ResolveStar(bool useA)
    {
        if (!_awaitingStar || _starCard == null) return;
        _chosenStar = useA ? _starCard.starA : _starCard.starB;
        _awaitingStar = false;
    }

    // ── Magias y trampas ──────────────────────────────────────────────────

    /// <summary>Magia alzada boca arriba: se activa al instante.</summary>
    private IEnumerator CastRaisedSpellRoutine()
    {
        _busy = true;
        var card = Player.Hand[_raisedIndex];
        screen.ShowFlipArrows(false);
        board.ClearShowcase();   // retira la carta 3D del showcase

        Player.Hand.RemoveAt(_raisedIndex);
        _raisedIndex = -1;
        _fusionOrder.Clear();   // los índices marcados ya no valen
        Player.RegisterSpell();

        string message = card.IsFieldSpell
            ? SetTerrain(card.fieldTerrain)
            : SpellEffectResolver.Resolve(card, Player, Opponent);
        screen.Log(message);

        screen.RefreshHand(Player.Hand);
        screen.UpdateLP(Player.LP, Opponent.LP);
        board.SyncField(Player, Opponent);   // por si la magia destruyó monstruos
        _busy = false;

        if (CheckDefeatedAndEnd()) yield break;
        EnterHandContext();
    }

    /// <summary>Magia/trampa alzada boca abajo: se setea en la casilla elegida.</summary>
    private IEnumerator SetCardRoutine(int handIndex, int slot)
    {
        _busy = true;
        var card = Player.Hand[handIndex];
        screen.ShowFlipArrows(false);
        board.ClearShowcase();   // por si quedó la carta 3D del showcase

        Player.Hand.RemoveAt(handIndex);
        _raisedIndex = -1;
        _fusionOrder.Clear();
        Player.PlaceSpellAt(slot, card);

        screen.RefreshHand(Player.Hand);
        yield return board.AnimateSetTrap(playerSide: true, slot, card);
        screen.Log(card.IsTrap ? "Colocas una trampa boca abajo." : "Colocas una magia boca abajo.");

        // De vuelta a la mano: la cámara regresa a la vista normal.
        screen.HideFieldBar();
        yield return board.MoveCamera(DuelBoard3D.CameraView.Play, 0.5f);
        _busy = false;
        EnterHandContext();
    }

    // ── Cambio de posición (W sobre el tablero) ───────────────────────────

    /// <summary>
    /// W: alterna Ataque (vertical) ↔ Defensa (horizontal), respetando la cara.
    /// Se puede reposicionar cuantas veces se quiera; lo ÚNICO que lo bloquea
    /// es que el monstruo ya haya atacado este turno.
    /// </summary>
    private void TogglePositionAtCursor()
    {
        int slot = _boardCursor;
        if (Player.MonsterZone[slot] == null) return;
        if (Player.MonsterHasAttacked[slot]) { screen.Log("Ya atacó: no puede cambiar de posición."); return; }

        var newPos = Player.MonsterPositions[slot] switch
        {
            CardPosition.FaceUpAttack => CardPosition.FaceUpDefense,
            CardPosition.FaceUpDefense => CardPosition.FaceUpAttack,
            CardPosition.FaceDownAttack => CardPosition.FaceDownDefense,
            _ => CardPosition.FaceDownAttack
        };
        Player.SetMonsterPosition(slot, newPos);
        board.SyncField(Player, Opponent);

        bool toDefense = newPos == CardPosition.FaceUpDefense || newPos == CardPosition.FaceDownDefense;
        string name = Player.IsMonsterFaceDown(slot) ? "El monstruo boca abajo" : Player.MonsterZone[slot].cardName;
        screen.Log($"{name} pasa a {(toDefense ? "Defensa" : "Ataque")}.");
        RefreshBoardCursor();
    }

    // ── Fusión (lista ↑ y/o invocar sobre casilla ocupada) ────────────────

    /// <summary>
    /// Resuelve la cadena de fusión y coloca el resultado EN la casilla elegida.
    /// Si la casilla estaba ocupada, ese monstruo ya viene como PRIMER material
    /// (<paramref name="tookFieldMonster"/>). El resultado siempre queda boca
    /// arriba (en Ataque); su posición de batalla se cambia luego con W.
    /// </summary>
    private IEnumerator FusionAtSlotRoutine(List<CardData> materials, List<int> handIdx,
                                            int slot, bool tookFieldMonster)
    {
        // Validar la cadena ANTES de consumir nada.
        var chain = fusionDb.ResolveChain(materials);
        if (chain.FinalResult == null)
        {
            screen.Log("No hay fusión posible con esas cartas (en ese orden).");
            RefreshSlotCursor();
            yield break;
        }

        _busy = true;
        screen.ShowFlipArrows(false);
        screen.HideHandCursor();

        // Captura la posición EN PANTALLA de cada material que está en la mano (antes de
        // consumirlas), para que las cartas 3D se LEVANTEN desde ahí. handIdx está en
        // orden de fusión, igual que `materials` (el monstruo del campo, si la casilla
        // estaba ocupada, va PRIMERO en materials y sube desde su propia casilla).
        var handScreens = new List<Vector3>();
        foreach (var i in handIdx) handScreens.Add(screen.HandCardScreenPos(i));

        // Consumir materiales: monstruo del campo (sin contar como destruido)
        // y cartas de la mano (índices descendentes).
        if (tookFieldMonster) Player.TakeMonsterForFusion(slot);
        handIdx.Sort((a, b) => b.CompareTo(a));
        foreach (var i in handIdx)
            if (i < Player.Hand.Count) Player.Hand.RemoveAt(i);
        _fusionOrder.Clear();
        _raisedIndex = -1;

        screen.RefreshHand(Player.Hand);   // las cartas consumidas desaparecen de la mano
        board.SyncField(Player, Opponent);
        screen.HideFieldBar();
        screen.HideCardInfo();

        // Cámara a la vista de juego; luego las cartas 3D se levantan desde la mano.
        yield return board.MoveCamera(DuelBoard3D.CameraView.Play, 0.5f);

        // Punto de arranque (mundo) por material, alineado con `materials`.
        var worldStarts = new List<Vector3>();
        int handStart = 0;
        for (int i = 0; i < materials.Count; i++)
        {
            if (tookFieldMonster && i == 0)
                worldStarts.Add(board.GetPlayerMonsterSlotWorld(slot)); // el del campo sube desde su casilla
            else
                worldStarts.Add(board.HandStartWorld(handScreens[handStart++]));
        }

        // ── Cola de fusión: reunir materiales (levantándose de la mano) y resolver ──
        yield return board.AnimateFusionGather(materials, worldStarts);
        yield return screen.SlideHandDown(0.3f);   // el resto de la mano se retira

        CardData stepCurrent = materials[0];
        for (int i = 0; i < chain.Steps.Count; i++)
        {
            var step = chain.Steps[i];
            var next = materials[i + 1];
            bool firstSurvives = step.Result == stepCurrent;

            switch (step.Type)
            {
                case FusionStepType.Specific:
                    screen.Log($"  {stepCurrent.cardName} + {next.cardName} → ¡{step.Result.cardName}!");
                    break;
                case FusionStepType.Category:
                    screen.Log($"  {stepCurrent.cardName} + {next.cardName} → {step.Result.cardName} (categoría)");
                    break;
                case FusionStepType.Equip:
                    screen.Log($"  {stepCurrent.cardName} absorbe {next.cardName} (+{step.EquipAtkBonusApplied} ATK / +{step.EquipDefBonusApplied} DEF)");
                    break;
                case FusionStepType.Absorption:
                    var absorbed = firstSurvives ? next : stepCurrent;
                    screen.Log($"  Incompatibles — {absorbed.cardName} se descarta.");
                    break;
            }

            yield return board.AnimateFusionStep(step.Type, step.Result, firstSurvives);
            stepCurrent = step.Result;
        }

        // ── Estrella Guardiana del resultado (cámara ya en la vista original) ─
        _ctx = KeyCtx.Star;
        yield return WaitForStarChoice(chain.FinalResult);
        GuardianStar star = _chosenStar;

        if (chain.Steps.Count > 0) Player.RegisterFusion();
        int atk = CombatCalculator.CalculateAtk(chain.FinalResult, star, star, _terrain)
                  + chain.TotalEquipAtkBonus;
        int def = CombatCalculator.CalculateDef(chain.FinalResult, chain.TotalEquipDefBonus);
        Player.PlaceMonsterAt(slot, chain.FinalResult, CardPosition.FaceUpAttack, atk, def, star);
        _hasSummonedThisTurn = true;

        // El resultado vuela al tablero y, JUSTO al llegar, la cámara baja al campo.
        yield return board.AnimateFusionSummon(playerSide: true, slot, Player);
        yield return board.MoveCamera(DuelBoard3D.CameraView.PlayerField, 0.5f);

        screen.Log($"→ {chain.FinalResult.cardName} (ATK {atk} / DEF {def}, ★{star})");
        _busy = false;
        EnterBattlePhase();   // ya en el campo del jugador → directo a la batalla
    }

    // ── Fase de batalla (contexto TABLERO) ────────────────────────────────

    /// <summary>Ir a batalla desde la mano (E): retira la mano y enfoca tu campo.</summary>
    private void GoToBattle() => StartCoroutine(GoToBattleRoutine());

    private IEnumerator GoToBattleRoutine()
    {
        _busy = true;
        yield return DuelTween.Parallel(this,
            screen.SlideHandDown(0.3f),
            board.MoveCamera(DuelBoard3D.CameraView.PlayerField, 0.5f));
        _busy = false;
        EnterBattlePhase();
    }

    /// <summary>Entra en la fase de batalla (contexto tablero) SIN mover la cámara
    /// ni la mano — para cuando ya están colocadas (p. ej. justo tras invocar).</summary>
    private void EnterBattlePhase()
    {
        Phase = DuelPhase.BattlePhase;
        screen.ShowPhase("Fase de Batalla");
        screen.HideHandCursor();
        screen.HideCardInfo();
        _attackerSlot = -1;
        EnterBoardContext();
        screen.Log("Batalla — A: elegir · W: posición · E: terminar turno.");
    }

    private void EnterBoardContext()
    {
        _ctx = KeyCtx.Board;
        _boardRow = 0;
        _boardCursor = Mathf.Clamp(_boardCursor, 0, 4);
        RefreshBoardCursor();
    }

    private void RefreshBoardCursor()
    {
        board.ClearHighlights();
        board.HideSlotCursor();
        bool monsterRow = _boardRow == 0;
        var zone = monsterRow ? Player.MonsterZone : Player.SpellZone;
        if (zone[_boardCursor] != null)
        {
            if (monsterRow) board.SetPlayerMonsterHighlight(_boardCursor, true);
            else board.SetPlayerSpellHighlight(_boardCursor, true);
        }
        else
        {
            board.ShowSlotCursor(true, monsterRow, _boardCursor);
        }
        // Con la mano oculta, la info del campo va en la barra del fondo.
        screen.ShowFieldBar(zone[_boardCursor], bottom: true);
    }

    private void BoardInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        { _boardCursor = (_boardCursor + 4) % 5; RefreshBoardCursor(); }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        { _boardCursor = (_boardCursor + 1) % 5; RefreshBoardCursor(); }
        else if (Input.GetKeyDown(KeyCode.UpArrow))
        { _boardRow = 0; RefreshBoardCursor(); }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        { _boardRow = 1; RefreshBoardCursor(); }
        else if (Input.GetKeyDown(KeyCode.W) && _boardRow == 0)
            TogglePositionAtCursor();
        else if (Input.GetKeyDown(KeyCode.A))
            SelectBoardCard();
        else if (Input.GetKeyDown(KeyCode.E))
            EndPlayerTurn();
    }

    private void SelectBoardCard()
    {
        int slot = _boardCursor;
        if (_boardRow == 0)
        {
            // Monstruo: pasa a elegir objetivo en el campo rival.
            if (Player.MonsterZone[slot] == null) return;
            if (Player.MonsterPositions[slot] != CardPosition.FaceUpAttack)
            { screen.Log("Solo los monstruos boca arriba en Ataque pueden atacar."); return; }
            if (Player.MonsterHasAttacked[slot])
            { screen.Log($"{Player.MonsterZone[slot].cardName} ya atacó este turno."); return; }

            StartCoroutine(EnterTargetRoutine(slot));
        }
        else
        {
            // Magia/trampa seteada: se alza al centro (activar o re-setear).
            if (Player.SpellZone[slot] == null) return;
            StartCoroutine(FieldRaiseRoutine(slot));
        }
    }

    private void EndPlayerTurn()
    {
        _ctx = KeyCtx.None;
        screen.HideHandCursor();
        screen.ShowFlipArrows(false);
        screen.HideFieldBar();
        screen.HideTargetBar();
        board.ClearHighlights();
        board.HideSlotCursor();
        _fusionOrder.Clear();
        screen.ClearFusionBadges();
        StartCoroutine(board.MoveCamera(DuelBoard3D.CameraView.Play, 0.6f));
        StartCoroutine(RunEndPhase());
    }

    // ── Contexto OBJETIVO (campo rival) ───────────────────────────────────

    /// <summary>La cámara cruza al campo del rival y sube la barra del objetivo.</summary>
    private IEnumerator EnterTargetRoutine(int attackerSlot)
    {
        _busy = true;
        _attackerSlot = attackerSlot;
        yield return board.MoveCamera(DuelBoard3D.CameraView.OpponentField, 0.5f);
        _busy = false;

        _ctx = KeyCtx.Target;
        _targetCursor = 0;
        for (int i = 0; i < 5; i++)
            if (Opponent.MonsterZone[i] != null) { _targetCursor = i; break; }
        RefreshTargetCursor();
        screen.Log($"Atacante: {Player.MonsterZone[_attackerSlot].cardName}. Elige objetivo (A, S atrás).");
    }

    private void RefreshTargetCursor()
    {
        board.ClearHighlights();
        board.HideSlotCursor();
        board.SetPlayerMonsterHighlight(_attackerSlot, true);
        if (Opponent.MonsterZone[_targetCursor] != null)
        {
            board.SetOpponentMonsterHighlight(_targetCursor, true);
            // La barra del objetivo no revela una carta rival boca abajo.
            screen.ShowTargetBar(Opponent.MonsterZone[_targetCursor],
                                 Opponent.IsMonsterFaceDown(_targetCursor));
        }
        else
        {
            board.ShowSlotCursor(false, true, _targetCursor);
            screen.ShowTargetBar(null, false);
        }
    }

    private void TargetInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        { _targetCursor = (_targetCursor + 4) % 5; RefreshTargetCursor(); }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        { _targetCursor = (_targetCursor + 1) % 5; RefreshTargetCursor(); }
        else if (Input.GetKeyDown(KeyCode.A))
            ConfirmAttack();
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Escape))
            StartCoroutine(CancelTargetRoutine());
    }

    /// <summary>S: se cancela el ataque y la cámara vuelve a nuestro campo para
    /// elegir otro monstruo o replantear el ataque.</summary>
    private IEnumerator CancelTargetRoutine()
    {
        _busy = true;
        _attackerSlot = -1;
        screen.HideTargetBar();
        board.ClearHighlights();
        board.HideSlotCursor();
        yield return board.MoveCamera(DuelBoard3D.CameraView.PlayerField, 0.5f);
        _busy = false;
        EnterBoardContext();
    }

    private void ConfirmAttack()
    {
        int attacker = _attackerSlot;

        if (Opponent.MonsterZone[_targetCursor] != null)
        {
            _attackerSlot = -1;
            screen.HideTargetBar();
            board.ClearHighlights();
            board.HideSlotCursor();
            StartCoroutine(PlayerAttackRoutine(attacker, _targetCursor));
        }
        else if (IsFieldEmpty(Opponent))
        {
            // Directo: solo sin monstruos rivales y nunca en tu primer turno.
            if (_playerTurnCount <= 1)
            { screen.Log("No puedes atacar directo en el primer turno."); return; }
            _attackerSlot = -1;
            screen.HideTargetBar();
            board.ClearHighlights();
            board.HideSlotCursor();
            StartCoroutine(PlayerAttackRoutine(attacker, -1));
        }
        else
        {
            screen.Log("Elige un monstruo enemigo.");
        }
    }

    // ── Magia/trampa del campo alzada (activar o re-setear) ──────────────

    private IEnumerator FieldRaiseRoutine(int slot)
    {
        _busy = true;
        _fieldSlot = slot;
        _fieldRaisedFaceDown = true;   // en el campo estaba boca abajo
        board.ClearHighlights();
        board.HideSlotCursor();
        yield return board.AnimateFieldCardToCenter(slot);
        screen.ShowFlipArrows(true);
        screen.ShowFieldBar(Player.SpellZone[slot], bottom: true);
        _busy = false;
        _ctx = KeyCtx.FieldRaised;
    }

    private void FieldRaisedInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            StartCoroutine(FlipFieldRoutine());
        else if (Input.GetKeyDown(KeyCode.A))
            StartCoroutine(ConfirmFieldRaisedRoutine());
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Escape))
            StartCoroutine(LowerFieldRoutine());
    }

    private IEnumerator FlipFieldRoutine()
    {
        _busy = true;
        _fieldRaisedFaceDown = !_fieldRaisedFaceDown;
        yield return board.AnimateFlipFieldCard(_fieldSlot, Player.SpellZone[_fieldSlot], _fieldRaisedFaceDown);
        _busy = false;
    }

    private IEnumerator ConfirmFieldRaisedRoutine()
    {
        // Boca abajo = se vuelve a setear tal cual.
        if (_fieldRaisedFaceDown) { yield return LowerFieldRoutine(); yield break; }

        // Boca arriba = activar.
        _busy = true;
        screen.ShowFlipArrows(false);
        int slot = _fieldSlot;
        _fieldSlot = -1;
        var card = Player.SpellZone[slot];

        if (card.IsTrap)
        {
            screen.Log("Las trampas aún no pueden activarse (motor pendiente). Se vuelve a colocar.");
            yield return board.AnimateFlipFieldCard(slot, card, faceDown: true);
            yield return board.AnimateFieldCardBack(slot);
            board.SyncField(Player, Opponent);
            _busy = false;
            EnterBoardContext();
            yield break;
        }

        // Magia seteada: se activa y se consume.
        Player.SpellZone[slot] = null;
        Player.RegisterSpell();
        string msg = card.IsFieldSpell
            ? SetTerrain(card.fieldTerrain)
            : SpellEffectResolver.Resolve(card, Player, Opponent);
        screen.Log(msg);
        screen.UpdateLP(Player.LP, Opponent.LP);
        board.SyncField(Player, Opponent);
        _busy = false;

        if (CheckDefeatedAndEnd()) yield break;
        EnterBoardContext();
    }

    private IEnumerator LowerFieldRoutine()
    {
        _busy = true;
        screen.ShowFlipArrows(false);
        int slot = _fieldSlot;
        _fieldSlot = -1;
        // Si quedó boca arriba por los volteos, se devuelve boca abajo.
        if (slot >= 0 && !_fieldRaisedFaceDown && Player.SpellZone[slot] != null)
            yield return board.AnimateFlipFieldCard(slot, Player.SpellZone[slot], faceDown: true);
        yield return board.AnimateFieldCardBack(slot);
        board.SyncField(Player, Opponent);
        _busy = false;
        EnterBoardContext();
    }

    private IEnumerator RunEndPhase()
    {
        Phase = DuelPhase.EndPhase;
        screen.ShowPhase("Fase Final");

        if (IsPlayerTurn) Player.EndTurn();
        else Opponent.EndTurn();

        yield return new WaitForSeconds(0.5f);
        if (CheckDefeatedAndEnd()) yield break;

        IsPlayerTurn = !IsPlayerTurn;
        StartCoroutine(RunTurn());
    }

    // ── Batalla (jugador) ─────────────────────────────────────────────────

    /// <summary>Ataque a un monstruo rival (defSlot ≥ 0) o directo (defSlot = -1).</summary>
    private IEnumerator PlayerAttackRoutine(int atkSlot, int defSlot)
    {
        _busy = true;
        Player.MonsterHasAttacked[atkSlot] = true;
        yield return ResolveCombatAnimated(Player, Opponent, attackerIsPlayer: true, atkSlot, defSlot);
        if (CheckDefeatedAndEnd()) { _busy = false; yield break; }
        // La cámara regresa a nuestro lado del campo.
        yield return board.MoveCamera(DuelBoard3D.CameraView.PlayerField, 0.5f);
        _busy = false;
        EnterBoardContext();   // el selector vuelve a tu campo
    }

    // ── Turno de la IA ────────────────────────────────────────────────────

    private IEnumerator RunAIMainPhase()
    {
        _busy = true;
        yield return new WaitForSeconds(0.9f);

        var action = _ai.DecideMainAction(Opponent, Player, _terrain);
        switch (action.Type)
        {
            case AIActionType.Summon:
            {
                var star = _ai.ChooseGuardianStar(action.Card, Player);
                Opponent.Hand.Remove(action.Card);
                int atk = CombatCalculator.CalculateAtk(action.Card, star, star, _terrain);
                int def = CombatCalculator.CalculateDef(action.Card);
                int slot = Opponent.PlaceMonster(action.Card, action.SummonPosition, atk, def, star);

                yield return board.AnimateSummon(playerSide: false, slot, Opponent);

                bool inDef = action.SummonPosition == CardPosition.FaceUpDefense;
                screen.Log($"{Opponent.Name} invoca {action.Card.cardName} en {(inDef ? "Defensa" : "Ataque")} (ATK {atk}/DEF {def}).");
                break;
            }

            case AIActionType.PlaySpell:
            {
                Opponent.Hand.Remove(action.Card);
                Opponent.RegisterSpell();
                string msg = action.Card.IsFieldSpell
                    ? SetTerrain(action.Card.fieldTerrain)
                    : SpellEffectResolver.Resolve(action.Card, Opponent, Player);
                screen.Log($"{Opponent.Name}: {msg}");
                screen.UpdateLP(Player.LP, Opponent.LP);
                board.SyncField(Player, Opponent);
                if (CheckDefeatedAndEnd()) yield break;
                break;
            }

            case AIActionType.Fuse:
            {
                var materials = action.FuseMaterials;
                var chain = fusionDb.ResolveChain(materials);
                if (chain.FinalResult == null) { screen.Log($"{Opponent.Name} duda…"); break; }

                foreach (var m in materials)
                    Opponent.Hand.Remove(m);

                screen.Log($"¡{Opponent.Name} inicia una fusión!");
                yield return board.AnimateFusionGather(materials);

                CardData stepCurrent = materials[0];
                for (int i = 0; i < chain.Steps.Count; i++)
                {
                    var step = chain.Steps[i];
                    bool firstSurvives = step.Result == stepCurrent;
                    yield return board.AnimateFusionStep(step.Type, step.Result, firstSurvives);
                    stepCurrent = step.Result;
                }

                var fstar = _ai.ChooseGuardianStar(chain.FinalResult, Player);
                Opponent.RegisterFusion();
                int fatk = CombatCalculator.CalculateAtk(chain.FinalResult, fstar, fstar, _terrain)
                           + chain.TotalEquipAtkBonus;
                int fdef = CombatCalculator.CalculateDef(chain.FinalResult, chain.TotalEquipDefBonus);
                int fslot = Opponent.PlaceMonster(chain.FinalResult, CardPosition.FaceUpAttack, fatk, fdef, fstar);

                yield return board.AnimateFusionSummon(playerSide: false, fslot, Opponent);
                screen.Log($"¡{Opponent.Name} fusiona! → {chain.FinalResult.cardName} (ATK {fatk}/DEF {fdef})");
                break;
            }

            case AIActionType.Pass:
                screen.Log($"{Opponent.Name} pasa.");
                break;
        }

        yield return new WaitForSeconds(0.6f);
        yield return StartCoroutine(RunAIBattlePhase());
    }

    private IEnumerator RunAIBattlePhase()
    {
        Phase = DuelPhase.BattlePhase;
        screen.ShowPhase("Fase de Batalla");

        var attacks = _ai.DecideAttacks(Opponent, Player, _terrain);
        foreach (var (atkSlot, defSlot) in attacks)
        {
            if (Opponent.MonsterZone[atkSlot] == null) continue;
            if (Opponent.MonsterPositions[atkSlot] != CardPosition.FaceUpAttack) continue;
            if (Opponent.MonsterHasAttacked[atkSlot]) continue;

            int target = defSlot;
            if (target >= 0 && Player.MonsterZone[target] == null)
            {
                target = -1;
                for (int i = 0; i < 5; i++)
                    if (Player.MonsterZone[i] != null) { target = i; break; }
            }
            // Sin objetivo válido y con monstruos del jugador vivos: no hay directo.
            if (target == -1 && !IsFieldEmpty(Player)) continue;

            yield return new WaitForSeconds(0.5f);
            Opponent.MonsterHasAttacked[atkSlot] = true;
            yield return ResolveCombatAnimated(Opponent, Player, attackerIsPlayer: false, atkSlot, target);
            if (CheckDefeatedAndEnd()) { _busy = false; yield break; }
        }

        _busy = false;
        yield return StartCoroutine(RunEndPhase());
    }

    // ── Combate animado (reglas exactas + ventaja de estrella) ────────────

    private IEnumerator ResolveCombatAnimated(Duelist attacker, Duelist defender,
                                              bool attackerIsPlayer, int atkSlot, int defSlot)
    {
        if (attacker.MonsterZone[atkSlot] == null) yield break;
        string attackerName = attacker.MonsterZone[atkSlot].cardName;

        // ── Ataque directo: embiste al vacío, daño = ATK ──────────────────
        if (defSlot == -1)
        {
            int damage = attacker.MonsterCurrentAtk[atkSlot];
            yield return board.AnimateAttack(attackerIsPlayer, atkSlot, -1);
            defender.TakeDamage(damage);
            board.ShowDamageText(playerSide: !attackerIsPlayer, damage);
            screen.UpdateLP(Player.LP, Opponent.LP);
            screen.Log($"{attackerName} ataca directamente: {damage} de daño.");
            yield break;
        }

        if (defender.MonsterZone[defSlot] == null) yield break;

        // ── Revelación: una carta boca abajo se voltea al ser atacada ─────
        if (defender.IsMonsterFaceDown(defSlot))
        {
            defender.RevealMonster(defSlot);
            board.SyncField(Player, Opponent);
            screen.Log($"¡Carta boca abajo revelada: {defender.MonsterZone[defSlot].cardName}!");
            yield return new WaitForSeconds(0.5f);
        }

        string defenderName = defender.MonsterZone[defSlot].cardName;
        bool inDefense = defender.MonsterPositions[defSlot] == CardPosition.FaceUpDefense;

        // ── Ventaja de Estrella Guardiana (por batalla, solo ATK) ─────────
        GuardianStar atkStar = attacker.MonsterStars[atkSlot];
        GuardianStar defStar = defender.MonsterStars[defSlot];
        int atkBonus = CombatCalculator.GetGuardianStarBonus(atkStar, defStar);
        int defBonus = inDefense ? 0 : CombatCalculator.GetGuardianStarBonus(defStar, atkStar);

        if (atkBonus > 0)
        {
            screen.Log($"★ {attackerName} ({atkStar}) domina a {defStar}: +{atkBonus} ATK.");
            yield return board.AnimateStarBoost(attackerIsPlayer, atkSlot, atkBonus);
        }
        if (defBonus > 0)
        {
            screen.Log($"★ {defenderName} ({defStar}) domina a {atkStar}: +{defBonus} ATK.");
            yield return board.AnimateStarBoost(!attackerIsPlayer, defSlot, defBonus);
        }

        int atkPower = attacker.MonsterCurrentAtk[atkSlot] + atkBonus;

        // ── Embestida ─────────────────────────────────────────────────────
        yield return board.AnimateAttack(attackerIsPlayer, atkSlot, defSlot);

        if (inDefense)
        {
            // ATK vs DEF.
            int defPower = defender.MonsterCurrentDef[defSlot];
            int diff = atkPower - defPower;

            if (diff > 0)
            {
                defender.RemoveMonster(defSlot);
                yield return board.AnimateDestroy(!attackerIsPlayer, defSlot);
                screen.Log($"{attackerName} destruye a {defenderName} (Defensa). Sin daño de batalla.");
            }
            else if (diff < 0)
            {
                attacker.TakeDamage(-diff);
                board.ShowDamageText(playerSide: attackerIsPlayer, -diff);
                screen.Log($"{defenderName} resiste (DEF {defPower}): {attacker.Name} recibe {-diff} de daño.");
            }
            else
            {
                screen.Log($"{attackerName} empata con la Defensa de {defenderName}: sin efecto.");
            }
        }
        else
        {
            // ATK vs ATK.
            int defAtk = defender.MonsterCurrentAtk[defSlot] + defBonus;
            int diffAtk = atkPower - defAtk;

            if (diffAtk > 0)
            {
                defender.TakeDamage(diffAtk);
                defender.RemoveMonster(defSlot);
                yield return board.AnimateDestroy(!attackerIsPlayer, defSlot);
                board.ShowDamageText(playerSide: !attackerIsPlayer, diffAtk);
                screen.Log($"{attackerName} derrota a {defenderName}: {diffAtk} de daño.");
            }
            else if (diffAtk < 0)
            {
                attacker.TakeDamage(-diffAtk);
                attacker.RemoveMonster(atkSlot);
                yield return board.AnimateDestroy(attackerIsPlayer, atkSlot);
                board.ShowDamageText(playerSide: attackerIsPlayer, -diffAtk);
                screen.Log($"¡{attackerName} cae ante {defenderName}! {-diffAtk} de daño.");
            }
            else
            {
                attacker.RemoveMonster(atkSlot);
                defender.RemoveMonster(defSlot);
                yield return DuelTween.Parallel(this,
                    board.AnimateDestroy(attackerIsPlayer, atkSlot),
                    board.AnimateDestroy(!attackerIsPlayer, defSlot));
                screen.Log($"¡Empate! {attackerName} y {defenderName} se destruyen mutuamente.");
            }
        }

        screen.UpdateLP(Player.LP, Opponent.LP);
        board.SyncField(Player, Opponent);
    }

    // ── Fin del duelo → banner → estadísticas → recompensa ────────────────

    private bool CheckDefeatedAndEnd()
    {
        if (Result != DuelResult.None) return true;
        if (Player.IsDefeated) { StartCoroutine(EndSequence(DuelResult.OpponentWin, "Tus LP llegaron a 0.")); return true; }
        if (Opponent.IsDefeated) { StartCoroutine(EndSequence(DuelResult.PlayerWin, "¡LP del rival a 0!")); return true; }
        return false;
    }

    private IEnumerator EndSequence(DuelResult result, string reason)
    {
        if (Result != DuelResult.None) yield break; // ya terminó
        Result = result;
        Phase = DuelPhase.CheckWin;
        _busy = true;

        // Apagar todo el control por teclado y sus indicadores.
        _ctx = KeyCtx.None;
        screen.HideHandCursor();
        screen.ShowFlipArrows(false);
        screen.ClearFusionBadges();
        screen.HideStarPanel();
        screen.HideFieldBar();
        screen.HideTargetBar();
        board.ClearHighlights();
        board.HideSlotCursor();

        bool win = result == DuelResult.PlayerWin;

        // Banner animado de victoria/derrota.
        yield return screen.PlayResultBanner(win);

        // Estadísticas del duelo.
        string stats =
            $"Turnos jugados: {Player.TurnsPlayed + Opponent.TurnsPlayed}\n" +
            $"Daño recibido: {Player.DamageTaken}\n" +
            $"Fusiones realizadas: {Player.FusionsPerformed}\n" +
            $"Magias usadas: {Player.SpellsUsed}\n" +
            $"Monstruos rivales destruidos: {Opponent.MonstersDestroyed}\n" +
            $"Motivo: {reason}";

        screen.ShowResultPanel(win ? "Resultado del duelo" : "Fin del duelo",
                               stats, allowRematch: _opponent != null);

        if (win)
        {
            Phase = DuelPhase.RewardPhase;

            DuelRank rank = RankEvaluator.Evaluate(
                Player.LP, Opponent.LP, Player.DamageTaken,
                Player.FusionsPerformed, Player.SpellsUsed, Player.TurnsPlayed);
            int score = RankEvaluator.ComputeScore(
                Player.LP, Opponent.LP, Player.DamageTaken,
                Player.FusionsPerformed, Player.SpellsUsed, Player.TurnsPlayed);
            screen.ShowRank(rank, score);

            if (_opponent != null)
                PlayerCollection.Instance?.RecordDuelResult(_opponent.opponentId, won: true, score: score);

            CardData reward = RankEvaluator.SelectReward(rank, _opponent, _overrides);
            yield return new WaitForSeconds(0.5f);
            screen.ShowReward(reward);
            if (reward != null)
                PlayerCollection.Instance?.AddCopy(reward.cardId);

            Phase = DuelPhase.SavePhase;
            PlayerCollection.Instance?.Save();
            screen.Log("Progreso guardado.");
        }
        else if (_opponent != null)
        {
            // La derrota se registra pero el rival NO se desbloquea.
            PlayerCollection.Instance?.RecordDuelResult(_opponent.opponentId, won: false);
        }
    }

    private void Rematch()
    {
        if (_opponent == null) return;
        DuelLauncher.Launch(_opponent, _overrides);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Refresca el contador de cartas restantes en mazo del HUD.</summary>
    private void RefreshCounts()
    {
        if (Player != null && Opponent != null)
            screen.UpdateCounts(Player.Deck.Count, Opponent.Deck.Count);
    }

    private static bool IsFieldEmpty(Duelist d)
    {
        foreach (var m in d.MonsterZone)
            if (m != null) return false;
        return true;
    }

    private string SetTerrain(TerrainType terrain)
    {
        _terrain = terrain;
        screen.SetTerrain(terrain);
        board.SetTerrain(terrain);
        return $"El terreno cambia a {terrain}.";
    }

    // ── Mazos (con diagnóstico claro) ─────────────────────────────────────

    private List<CardData> ResolvePlayerDeck()
    {
        var saved = PlayerDeck.ResolveCards();
        if (saved != null && saved.Count > 0)
        {
            if (saved.Count != PlayerDeck.RequiredSize)
                Debug.LogWarning($"DuelController: el mazo guardado tiene {saved.Count} cartas " +
                                 $"(la regla pide {PlayerDeck.RequiredSize}).");
            Debug.Log($"DuelController: mazo del jugador = Constructor de Deck ({saved.Count} cartas).");
            return saved;
        }

        Debug.LogWarning("DuelController: no hay mazo guardado. Se genera uno aleatorio de 40 " +
                         "(solo desarrollo). Guarda un mazo en el Constructor de Deck.");
        return BuildRandomDeck();
    }

    private List<CardData> ResolveOpponentDeck()
    {
        var deck = new List<CardData>();
        if (_opponent != null && _opponent.deck != null)
            foreach (var c in _opponent.deck)
                if (c != null) deck.Add(c);

        if (deck.Count > 0)
        {
            Debug.Log($"DuelController: mazo de '{Opponent.Name}' = {deck.Count} cartas de su OpponentData.");
            return deck;
        }

        Debug.LogWarning($"DuelController: ¡el mazo de '{Opponent.Name}' está VACÍO! Se genera uno " +
                         "aleatorio. Usa 'YGO > Setup > Rellenar mazos de oponentes' para arreglarlo.");
        return BuildRandomDeck();
    }

    private static List<CardData> BuildRandomDeck()
    {
        var deck = new List<CardData>();
        var all = LibraryCatalog.AllCards;
        if (all == null || all.Count == 0) return deck;

        for (int i = 0; i < PlayerDeck.RequiredSize; i++)
            deck.Add(all[Random.Range(0, all.Count)]);
        return deck;
    }

    private void PlayBattleMusic()
    {
        if (_opponent == null || _opponent.battleMusic == null) return;
        _music = gameObject.AddComponent<AudioSource>();
        _music.clip = _opponent.battleMusic;
        _music.loop = true;
        _music.playOnAwake = false;
        _music.Play();
    }
}
