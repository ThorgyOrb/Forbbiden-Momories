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
    private enum KeyCtx { None, Hand, Raised, SlotSelect, Star, Board, Target, FieldRaised, EquipTarget }

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
    private int _equipCursor;          // EquipTarget: casilla de monstruo propio a equipar
    private int _equipSlot = -1;       // EquipTarget (desde campo): casilla de magias del equipo
    private bool _equipFromHand;       // EquipTarget: el equipo viene de la MANO (no del campo)
    private int _equipHandIndex = -1;  // EquipTarget (desde mano): índice del equipo en la mano
    private int _fieldSlot = -1;            // magia/trampa del campo alzada
    private bool _fieldRaisedFaceDown;
    private int _playerTurnCount;           // sin ataque directo en el turno 1
    private bool _opponentOpeningDealt;     // ¿ya se animó el robo inicial del rival?

    // Selección de Estrella Guardiana (↑/↓ resalta, A confirma).
    private bool _awaitingStar;
    private bool _starHoverA = true;
    private GuardianStar _chosenStar;
    private CardData _starCard;

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
            if (Input.GetKeyDown(KeyCode.UpArrow)) { _starHoverA = true; screen.HighlightStar(true); DuelAudio.Play(DuelAudio.Sfx.Cursor); }
            else if (Input.GetKeyDown(KeyCode.DownArrow)) { _starHoverA = false; screen.HighlightStar(false); DuelAudio.Play(DuelAudio.Sfx.Cursor); }
            else if (Input.GetKeyDown(KeyCode.A)) ResolveStar(_starHoverA);
            return;
        }

        // F: modal de información de la carta seleccionada (jugador o rival, boca arriba).
        // Se puede abrir/cerrar en cualquier momento; mientras está abierto, bloquea el resto.
        if (Input.GetKeyDown(KeyCode.F)) { ToggleCardModal(); return; }
        if (_cardModalOpen)
        {
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Escape)) CloseCardModal();
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
            case KeyCtx.EquipTarget: EquipTargetInput(); break;
        }
    }

    // ── Modal de información de carta (tecla F) ───────────────────────────
    private bool _cardModalOpen;

    /// <summary>Abre/cierra el modal con la carta seleccionada BOCA ARRIBA en el contexto actual.</summary>
    private void ToggleCardModal()
    {
        if (_cardModalOpen) { CloseCardModal(); return; }

        var sel = CurrentInspectableCard();
        if (sel.card == null || !sel.faceUp)
        {
            screen.Log("Selecciona una carta boca arriba para ver su información (F).");
            return;
        }
        _cardModalOpen = true;
        screen.ShowCardModal(sel.card, sel.atk, sel.def);
        DuelAudio.Play(DuelAudio.Sfx.Select);
    }

    private void CloseCardModal()
    {
        _cardModalOpen = false;
        screen.HideCardModal();
        DuelAudio.Play(DuelAudio.Sfx.Cancel);
    }

    /// <summary>La carta actualmente SELECCIONADA en el contexto de teclado, si es visible
    /// (boca arriba). Devuelve también el ATK/DEF actuales del campo (−1 = usar los base).</summary>
    private (CardData card, bool faceUp, int atk, int def) CurrentInspectableCard()
    {
        switch (_ctx)
        {
            case KeyCtx.Hand:
                if (_handCursor >= 0 && _handCursor < Player.Hand.Count)
                    return (Player.Hand[_handCursor], true, -1, -1);
                break;

            case KeyCtx.Raised:
                if (_raisedIndex >= 0 && _raisedIndex < Player.Hand.Count)
                    return (Player.Hand[_raisedIndex], true, -1, -1);
                break;

            case KeyCtx.Board:
                if (_boardRow == 0)
                {
                    var m = Player.MonsterZone[_boardCursor];
                    // Son TUS cartas: se pueden consultar aunque estén boca abajo.
                    if (m != null)
                        return (m, true,
                                Player.MonsterCurrentAtk[_boardCursor], Player.MonsterCurrentDef[_boardCursor]);
                }
                else
                {
                    var s = Player.SpellZone[_boardCursor];
                    if (s != null) return (s, true, -1, -1);   // tuya: consultable aunque esté boca abajo
                }
                break;

            case KeyCtx.EquipTarget:
                var em = Player.MonsterZone[_equipCursor];
                if (em != null)
                    return (em, true, Player.MonsterCurrentAtk[_equipCursor], Player.MonsterCurrentDef[_equipCursor]);
                break;

            case KeyCtx.Target:
                var t = Opponent.MonsterZone[_targetCursor];
                if (t != null)
                    return (t, !Opponent.IsMonsterFaceDown(_targetCursor),
                            Opponent.MonsterCurrentAtk[_targetCursor], Opponent.MonsterCurrentDef[_targetCursor]);
                break;
        }
        return (null, false, -1, -1);
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
        board.SetTerrain(_terrain, animated: false);   // estado inicial sin transición
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
        {
            DuelAudio.Play(DuelAudio.Sfx.Draw);
            yield return screen.AnimateDrawToHand(c);
        }
        screen.RefreshHand(Player.Hand);
        // La mano del rival NO se renderiza aún: aparece cuando la cámara gira a su lado.
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

        _busy = true;   // bloquea input durante el robo hasta la Fase Principal
        Phase = DuelPhase.Setup;
        current.ResetTurnFlags();
        _hasSummonedThisTurn = false;
        screen.ShowTurn(IsPlayerTurn ? "— TU TURNO —" : $"— Turno de {Opponent.Name} —");
        DuelAudio.Play(DuelAudio.Sfx.TurnStart);

        Phase = DuelPhase.DrawPhase;
        screen.ShowPhase("Fase de Robo");

        if (IsPlayerTurn)
        {
            // La cámara VUELVE (girando el tablero) a tu lado; la mano del rival se retira
            // y aparece la tuya. (En el turno 1 la cámara ya está aquí: no gira.)
            yield return board.OrbitCameraTo(DuelBoard3D.CameraView.Play, 1.2f, boardYaw: 0f);
            screen.ClearOpponentHand();
            _playerTurnCount++;   // el ataque directo se bloquea en el turno 1
            screen.HideFieldBar();
            screen.HideTargetBar();
            screen.SetHandVisible(true);   // la mano regresa para tu turno
            var drawn = Player.DrawUpToFive();
            foreach (var c in drawn)
            {
                screen.Log($"  Robaste: {c.cardName}");
                DuelAudio.Play(DuelAudio.Sfx.Draw);
                yield return screen.AnimateDrawToHand(c);
            }
            screen.RefreshHand(Player.Hand);
            if (Player.DeckOut) { StartCoroutine(EndSequence(DuelResult.OpponentWin, "Te quedaste sin cartas (Deck Out).")); yield break; }
        }
        else
        {
            // TU mano se retira y la cámara GIRA hasta la mano del rival; recién ahí se
            // renderiza la suya (dorsos) con las mismas animaciones que la tuya.
            screen.SetHandVisible(false);
            yield return board.OrbitCameraTo(DuelBoard3D.CameraView.OpponentHand, 1.2f, boardYaw: 180f);

            var before = new List<CardData>(Opponent.Hand);   // lo que ya tenía
            var drawn = Opponent.DrawUpToFive();              // lo que roba ahora
            screen.ClearOpponentHand();

            if (!_opponentOpeningDealt)
            {
                // ROBO INICIAL del rival: su mano de apertura entra carta a carta.
                _opponentOpeningDealt = true;
                foreach (var c in Opponent.Hand)
                {
                    screen.Log($"  {Opponent.Name} roba una carta.");
                    DuelAudio.Play(DuelAudio.Sfx.Draw);
                    yield return screen.AnimateOpponentDraw(c);
                }
            }
            else
            {
                screen.RefreshOpponentHand(before);   // lo que ya tenía, al instante
                foreach (var c in drawn)
                {
                    screen.Log($"  {Opponent.Name} roba una carta.");
                    DuelAudio.Play(DuelAudio.Sfx.Draw);
                    yield return screen.AnimateOpponentDraw(c);   // solo los nuevos, animados
                }
            }
            if (Opponent.DeckOut) { StartCoroutine(EndSequence(DuelResult.PlayerWin, "¡El rival se quedó sin cartas!")); yield break; }
        }

        RefreshCounts();
        yield return new WaitForSeconds(0.5f);

        Phase = DuelPhase.MainPhase;
        screen.ShowPhase("Fase Principal");

        if (IsPlayerTurn)
        {
            _busy = false;
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
        DuelAudio.Play(DuelAudio.Sfx.Cursor);
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
        if (!card.IsMonster && !card.IsEquip && !card.IsSpell && !card.IsTrap)
        {
            screen.Log("Esa carta no puede usarse como material de fusión.");
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
        if (!card.IsMonster && !card.IsSpell && !card.IsTrap && !card.IsEquip)
        { screen.Log($"{card.cardName} ({card.CategoryLabel}): aún no se puede jugar."); yield break; }

        DuelAudio.Play(DuelAudio.Sfx.Select);
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
    private void CancelToHand() { DuelAudio.Play(DuelAudio.Sfx.Cancel); StartCoroutine(CancelToHandRoutine()); }

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
        else if (card.IsTrap && !_raisedFaceDown)
        {
            screen.Log("Las trampas se colocan BOCA ABAJO (voltéala con ←/→).");
        }
        else if (card.IsEquip && !_raisedFaceDown)
        {
            // Equipo BOCA ARRIBA desde la mano → se aplica YA a un monstruo del campo.
            if (!System.Array.Exists(Player.MonsterZone, m => m != null))
            { screen.Log("No hay monstruos en el campo. Coloca el equipo boca abajo (←/→) o invoca un monstruo."); return; }
            EnterEquipTargetFromHand(_raisedIndex);
        }
        else
        {
            // Magia / trampa / equipo boca abajo → elegir casilla de magias.
            StartCoroutine(LowerThenSlotSelect(monsterRow: false));
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
        // La info de la carta a invocar SIGUE en el InfoBar de la mano (la mano aún visible).
        // Monstruos → vista del campo; magias/trampas → cámara BAJA a la zona de magias.
        if (_raisedIndex >= 0) screen.ShowCardInfo(Player.Hand[_raisedIndex]);
        yield return board.MoveCamera(
            monsterRow ? DuelBoard3D.CameraView.MonsterZone : DuelBoard3D.CameraView.PlayerSpellZone, 0.5f);

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
        DuelAudio.Play(DuelAudio.Sfx.Cursor);
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
        DuelAudio.Play(DuelAudio.Sfx.Cancel);
        _busy = true;
        board.HideSlotCursor();
        screen.HideFieldBar();
        yield return board.MoveCamera(DuelBoard3D.CameraView.Play, 0.5f);
        _busy = false;
        CancelToHand();
    }

    private void ConfirmSlot()
    {
        // Fila de magias: casilla libre = setear; casilla ocupada = combinar (fusión S/T/E).
        if (!_slotRowMonsters)
        {
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

        // ¿Invocación de Ritual? Se detecta ANTES que la fusión: una carta de categoría
        // Ritual no participa en ResolveChain, así que sin esto acabaría colocada como si
        // fuera un monstruo (o descartada por absorción).
        var ritual = RitualResolver.Evaluate(materials);
        if (ritual.IsRitualAttempt)
        {
            if (!ritual.Ok)
            {
                screen.Log(ritual.Describe());
                RefreshSlotCursor();
                return;
            }
            board.HideSlotCursor();
            StartCoroutine(RitualAtSlotRoutine(ritual, materials, handIdx, _slotCursor, slotOccupied));
            return;
        }

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
        screen.SetHandCardVisible(handIndex, false);   // oculta la 2D: se levanta la 3D, sin duplicado
        yield return DuelTween.Parallel(this,
            board.ShowcaseRaise(card, faceDown, summonStart),
            screen.SlideHandDown(0.3f));

        // La carta se CENTRA y hace su destello de presentación (igual que la fusión),
        // y flota suavemente mientras se elige la Estrella Guardiana.
        yield return board.PresentSummonCard(playerSide: true);
        board.StartSummonIdle();

        _ctx = KeyCtx.Star;
        yield return WaitForStarChoice(card);
        board.StopSummonIdle();
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
        DuelAudio.Play(DuelAudio.Sfx.Summon);
        yield return board.ShowcaseToSlot(slot, Player);

        // 2) Normaliza el campo (la carta ya está colocada).
        board.SyncField(Player, Opponent);
        screen.RefreshHand(Player.Hand);

        // 3) SOLO cuando ya está colocada, la cámara baja al campo (cenital).
        yield return board.MoveCamera(DuelBoard3D.CameraView.PlayerField, 0.55f);

        screen.Log(faceDown
            ? "Colocas un monstruo boca abajo."
            : $"¡{card.cardName}! (ATK {atk} / DEF {def}, ★{star})");

        // Trampas del rival que respondan a la invocación (Trap Hole, Negate Summon…).
        yield return AfterSummonTraps(Player, slot);
        if (CheckDefeatedAndEnd()) yield break;

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
        DuelAudio.Play(DuelAudio.Sfx.GuardianStar);
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
        DuelAudio.Play(DuelAudio.Sfx.Spell);

        screen.RefreshHand(Player.Hand);
        screen.UpdateLP(Player.LP, Opponent.LP);
        board.SyncField(Player, Opponent);   // por si la magia destruyó monstruos
        _busy = false;

        if (CheckDefeatedAndEnd()) yield break;
        EnterHandContext();
    }

    // ── Colocación en la ZONA DE MAGIAS (setear o "fusionar" S/T/E) ───────
    //
    // La zona de magias admite magias, trampas Y equipos boca abajo. Colocar una
    // carta sobre una casilla OCUPADA la combina con la existente (igual que la
    // fusión de monstruos, pero el resultado se queda boca abajo si es S/T/E, o va
    // a la zona de monstruos boca abajo si la receta produce un monstruo). Sin
    // receta (absorción) la existente se descarta y queda la entrante boca abajo.

    private enum SpellPlacement { SetEmpty, FuseInPlace, FuseToMonster }

    private struct SpellPlacePlan
    {
        public SpellPlacement kind;
        public CardData result;      // carta que acaba en el tablero
        public bool realFusion;      // hubo receta (no simple absorción)
        public CardData consumed;    // carta previa descartada (o null)
    }

    /// <summary>
    /// Decide qué ocurre al colocar <paramref name="incoming"/> en la casilla de magias
    /// <paramref name="slot"/> de <paramref name="who"/>. No muta el estado (solo planifica).
    /// </summary>
    private SpellPlacePlan PlanSpellPlacement(Duelist who, int slot, CardData incoming)
    {
        var existing = who.SpellZone[slot];
        if (existing == null)
            return new SpellPlacePlan { kind = SpellPlacement.SetEmpty, result = incoming };

        var step = fusionDb.ResolveStep(existing, incoming);
        bool real = step.Type == FusionStepType.Specific || step.Type == FusionStepType.Category;
        var res = step.Result;

        // Resultado MONSTRUO → a la zona de monstruos (boca abajo), si hay hueco.
        if (res.IsMonster && FirstFreeSlot(who.MonsterZone) >= 0)
            return new SpellPlacePlan
            { kind = SpellPlacement.FuseToMonster, result = res, realFusion = real, consumed = existing };

        // Sin hueco de monstruo, un resultado-monstruo degrada a "queda la entrante boca abajo".
        var boardCard = res.IsMonster ? incoming : res;
        return new SpellPlacePlan
        { kind = SpellPlacement.FuseInPlace, result = boardCard, realFusion = real && !res.IsMonster, consumed = existing };
    }

    /// <summary>Magia/trampa/equipo alzada boca abajo: se coloca (o fusiona) en la casilla elegida.</summary>
    private IEnumerator SetCardRoutine(int handIndex, int slot)
    {
        _busy = true;
        var card = Player.Hand[handIndex];
        screen.ShowFlipArrows(false);
        board.ClearShowcase();   // por si quedó la carta 3D del showcase

        var plan = PlanSpellPlacement(Player, slot, card);

        Player.Hand.RemoveAt(handIndex);
        _raisedIndex = -1;
        _fusionOrder.Clear();
        screen.RefreshHand(Player.Hand);

        switch (plan.kind)
        {
            case SpellPlacement.SetEmpty:
                Player.PlaceSpellAt(slot, card);
                if (card.IsTrap) Player.RegisterTrapSet();
                DuelAudio.Play(DuelAudio.Sfx.SetCard);
                yield return board.AnimateSetTrap(playerSide: true, slot, card);
                screen.Log(card.IsEquip ? "Colocas un equipo boca abajo."
                         : card.IsTrap ? "Colocas una trampa boca abajo."
                         : "Colocas una magia boca abajo.");
                break;

            case SpellPlacement.FuseInPlace:
                Player.SpellZone[slot] = plan.result;
                Player.SpellFaceUp[slot] = false;   // el resultado queda boca abajo
                if (plan.realFusion) Player.RegisterFusion();
                if (plan.result.IsTrap) Player.RegisterTrapSet();
                DuelAudio.Play(plan.realFusion ? DuelAudio.Sfx.Fuse : DuelAudio.Sfx.SetCard);
                yield return board.AnimateSetTrap(playerSide: true, slot, plan.result);
                board.SyncField(Player, Opponent);
                screen.Log(plan.realFusion
                    ? $"¡Combinas cartas! Queda {plan.result.CategoryLabel} boca abajo."
                    : $"Se descarta {plan.consumed.cardName}; queda {card.cardName} boca abajo.");
                break;

            case SpellPlacement.FuseToMonster:
                Player.ClearSpell(slot);
                int mslot = Player.PlaceMonster(plan.result, CardPosition.FaceDownDefense,
                    CombatCalculator.CalculateAtk(plan.result, plan.result.starA, plan.result.starA, _terrain),
                    CombatCalculator.CalculateDef(plan.result), plan.result.starA);
                if (plan.realFusion) Player.RegisterFusion();
                DuelAudio.Play(DuelAudio.Sfx.Fuse);
                yield return board.AnimateSetTrap(playerSide: true, slot, card);  // destello en la casilla
                board.SyncField(Player, Opponent);
                screen.Log($"¡La combinación forma un monstruo! {plan.result.cardName} aparece boca abajo.");
                break;
        }

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
        // Validar la cadena ANTES de consumir nada. El resultado DEBE ser un monstruo:
        // se coloca en la zona de monstruos, así que un equipo/magia/trampa como resultado
        // final rompería el juego (por eso los no-monstruos nunca sobreviven una absorción).
        var chain = fusionDb.ResolveChain(materials);
        if (chain.FinalResult == null || !chain.FinalResult.IsMonster)
        {
            screen.Log("Esa combinación no produce un monstruo invocable.");
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

        // Si se EQUIPA sobre un monstruo del campo (no una fusión real), el bonus debe
        // SUMARSE a lo que ya tenía (base + terreno + equipos previos), que vive en
        // MonsterCurrentAtk/Def. Se captura ANTES de consumir el monstruo, porque
        // TakeMonsterForFusion pone esos valores a 0.
        int fieldAtkBefore = tookFieldMonster ? Player.MonsterCurrentAtk[slot] : 0;
        int fieldDefBefore = tookFieldMonster ? Player.MonsterCurrentDef[slot] : 0;
        CardData fieldCardBefore = tookFieldMonster ? Player.MonsterZone[slot] : null;

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
        DuelAudio.Play(DuelAudio.Sfx.FusionStart);
        yield return board.AnimateFusionGather(materials, worldStarts);
        yield return screen.SlideHandDown(0.3f);   // el resto de la mano se retira

        // ATK/DEF "efectivo" base de un monstruo: si es el del campo, con sus equipos
        // previos (fieldAtkBefore); si no, su base + terreno.
        int BaseAtkOf(CardData m) => (m == fieldCardBefore)
            ? fieldAtkBefore : CombatCalculator.CalculateAtk(m, m.starA, m.starA, _terrain);
        int BaseDefOf(CardData m) => (m == fieldCardBefore)
            ? fieldDefBefore : CombatCalculator.CalculateDef(m);

        // ATK/DEF que muestra la carta acumuladora durante la fusión. En cada EQUIPO el
        // número SUBE en la propia carta (funcione el equipo antes o después en la lista).
        int curAtk = materials[0].IsMonster ? BaseAtkOf(materials[0]) : 0;
        int curDef = materials[0].IsMonster ? BaseDefOf(materials[0]) : 0;
        if (materials[0].IsMonster) board.SetFusionResultStats(curAtk, curDef);

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
                    DuelAudio.Play(DuelAudio.Sfx.Fuse);
                    break;
                case FusionStepType.Category:
                    screen.Log($"  {stepCurrent.cardName} + {next.cardName} → {step.Result.cardName} (categoría)");
                    DuelAudio.Play(DuelAudio.Sfx.Fuse);
                    break;
                case FusionStepType.Equip:
                    screen.Log($"  {step.Result.cardName} se equipa (+{step.EquipAtkBonusApplied} ATK / +{step.EquipDefBonusApplied} DEF)");
                    DuelAudio.Play(DuelAudio.Sfx.Equip);
                    Player.RegisterEquip();
                    break;
                case FusionStepType.Absorption:
                    var absorbed = firstSurvives ? next : stepCurrent;
                    screen.Log($"  Incompatibles — {absorbed.cardName} se descarta.");
                    break;
            }

            // ATK/DEF antes → después del paso (para el conteo en la carta al equipar).
            int fromAtk, fromDef, toAtk, toDef;
            if (step.Type == FusionStepType.Equip)
            {
                // El monstruo es step.Result (sobreviva antes o después el equipo): si ya
                // era el acumulado, parte de curAtk; si "llega" ahora, de su base efectivo.
                fromAtk = (stepCurrent == step.Result) ? curAtk : BaseAtkOf(step.Result);
                fromDef = (stepCurrent == step.Result) ? curDef : BaseDefOf(step.Result);
                toAtk = fromAtk + step.EquipAtkBonusApplied;
                toDef = fromDef + step.EquipDefBonusApplied;
            }
            else   // fusión real / absorción
            {
                fromAtk = curAtk; fromDef = curDef;
                if (step.Result == stepCurrent)   // el acumulado sobrevive: conserva sus stats
                { toAtk = curAtk; toDef = curDef; }
                else                              // nace/gana otra carta → sus stats base
                { toAtk = BaseAtkOf(step.Result); toDef = BaseDefOf(step.Result); }
            }
            curAtk = toAtk; curDef = toDef;

            yield return board.AnimateFusionStep(step.Type, step.Result, firstSurvives,
                                                 fromAtk, toAtk, fromDef, toDef);
            stepCurrent = step.Result;
        }

        // La carta fusionada se CENTRA y hace su destello de presentación (mismo punto y
        // tamaño que la invocación simple) y flota mientras se elige la Estrella.
        yield return board.PresentSummonCard(playerSide: true);
        board.StartSummonIdle();

        // ── Estrella Guardiana del resultado (cámara ya en la vista original) ─
        _ctx = KeyCtx.Star;
        yield return WaitForStarChoice(chain.FinalResult);
        board.StopSummonIdle();
        GuardianStar star = _chosenStar;

        if (chain.Steps.Count > 0) Player.RegisterFusion();

        int atk, def;
        if (tookFieldMonster && chain.FinalResult == fieldCardBefore)
        {
            // SOLO se equipó (el monstruo del campo sobrevive, no hubo fusión real):
            // suma el bonus sobre lo que YA tenía, sin recalcular desde base (así no se
            // pierden los equipos aplicados en turnos anteriores).
            atk = fieldAtkBefore + chain.TotalEquipAtkBonus;
            def = fieldDefBefore + chain.TotalEquipDefBonus;
        }
        else
        {
            // Fusión real (nace un monstruo nuevo): stats desde su base + equipos de ESTA
            // cadena. Los equipos previos del material se pierden (regla FM), correcto.
            atk = CombatCalculator.CalculateAtk(chain.FinalResult, star, star, _terrain)
                  + chain.TotalEquipAtkBonus;
            def = CombatCalculator.CalculateDef(chain.FinalResult, chain.TotalEquipDefBonus);
        }
        Player.PlaceMonsterAt(slot, chain.FinalResult, CardPosition.FaceUpAttack, atk, def, star);
        _hasSummonedThisTurn = true;

        // El resultado vuela al tablero y, JUSTO al llegar, la cámara baja al campo.
        yield return board.AnimateFusionSummon(playerSide: true, slot, Player);
        yield return board.MoveCamera(DuelBoard3D.CameraView.PlayerField, 0.5f);

        screen.Log($"→ {chain.FinalResult.cardName} (ATK {atk} / DEF {def}, ★{star})");

        // Trampas del rival que respondan a la invocación (la fusión también invoca).
        yield return AfterSummonTraps(Player, slot);
        if (CheckDefeatedAndEnd()) yield break;

        _busy = false;
        EnterBattlePhase();   // ya en el campo del jugador → directo a la batalla
    }

    // ── Invocación de Ritual ──────────────────────────────────────────────

    /// <summary>
    /// Consume la carta de Ritual y sus materiales, e invoca el monstruo resultante en la
    /// casilla elegida. Reutiliza las animaciones de fusión (reunir → presentar → volar a
    /// la casilla) porque para el jugador es el mismo gesto; lo que cambia es la regla.
    ///
    /// El resultado entra con sus stats BASE + terreno: no arrastra equipos de los
    /// materiales, igual que una fusión real.
    /// </summary>
    private IEnumerator RitualAtSlotRoutine(RitualResolver.Attempt ritual, List<CardData> materials,
                                            List<int> handIdx, int slot, bool tookFieldMonster)
    {
        _busy = true;
        screen.ShowFlipArrows(false);
        screen.HideHandCursor();

        // Posición en pantalla de cada material de la mano, ANTES de consumirlos.
        var handScreens = new List<Vector3>();
        foreach (var i in handIdx) handScreens.Add(screen.HandCardScreenPos(i));

        if (tookFieldMonster) Player.TakeMonsterForFusion(slot);
        handIdx.Sort((a, b) => b.CompareTo(a));
        foreach (var i in handIdx)
            if (i < Player.Hand.Count) Player.Hand.RemoveAt(i);
        _fusionOrder.Clear();
        _raisedIndex = -1;

        screen.RefreshHand(Player.Hand);
        board.SyncField(Player, Opponent);
        screen.HideFieldBar();
        screen.HideCardInfo();

        yield return board.MoveCamera(DuelBoard3D.CameraView.Play, 0.5f);

        var worldStarts = new List<Vector3>();
        int handStart = 0;
        for (int i = 0; i < materials.Count; i++)
        {
            if (tookFieldMonster && i == 0)
                worldStarts.Add(board.GetPlayerMonsterSlotWorld(slot));
            else
                worldStarts.Add(board.HandStartWorld(handScreens[handStart++]));
        }

        screen.Log($"Ritual: {ritual.ritualCard.cardName}");
        DuelAudio.Play(DuelAudio.Sfx.FusionStart);
        yield return board.AnimateFusionGather(materials, worldStarts);
        yield return screen.SlideHandDown(0.3f);

        // Un único "paso": todos los materiales se funden en el monstruo del ritual.
        var result = ritual.result;
        int baseAtk = CombatCalculator.CalculateAtk(result, result.starA, result.starA, _terrain);
        int baseDef = CombatCalculator.CalculateDef(result);
        DuelAudio.Play(DuelAudio.Sfx.Fuse);
        yield return board.AnimateFusionStep(FusionStepType.Specific, result, false,
                                             0, baseAtk, 0, baseDef);

        yield return board.PresentSummonCard(playerSide: true);
        board.StartSummonIdle();

        _ctx = KeyCtx.Star;
        yield return WaitForStarChoice(result);
        board.StopSummonIdle();
        GuardianStar star = _chosenStar;

        Player.RegisterFusion();   // el ritual gasta la invocación especial del turno

        int atk = CombatCalculator.CalculateAtk(result, star, star, _terrain);
        int def = CombatCalculator.CalculateDef(result);
        Player.PlaceMonsterAt(slot, result, CardPosition.FaceUpAttack, atk, def, star);
        _hasSummonedThisTurn = true;

        yield return board.AnimateFusionSummon(playerSide: true, slot, Player);
        yield return board.MoveCamera(DuelBoard3D.CameraView.PlayerField, 0.5f);

        screen.Log($"→ {result.cardName} (ATK {atk} / DEF {def}, ★{star})");

        yield return AfterSummonTraps(Player, slot);
        if (CheckDefeatedAndEnd()) yield break;

        _busy = false;
        EnterBattlePhase();
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
        DuelAudio.Play(DuelAudio.Sfx.Phase);
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
        board.HideSlotCursor();
        board.ClearSpellHighlights();
        bool monsterRow = _boardRow == 0;
        var zone = monsterRow ? Player.MonsterZone : Player.SpellZone;

        if (monsterRow && zone[_boardCursor] != null)
        {
            board.SelectMonster(true, _boardCursor);   // se eleva + flota + pulsa (fluido)
        }
        else
        {
            board.ClearSelectionAnim();
            if (zone[_boardCursor] != null) board.SetPlayerSpellHighlight(_boardCursor, true);
            else board.ShowSlotCursor(true, monsterRow, _boardCursor);
        }
        // Con la mano oculta, la info del campo va en la barra del fondo. Para un
        // monstruo se muestran su ATK/DEF ACTUALES (base + equipos + terreno).
        if (monsterRow && zone[_boardCursor] != null)
            screen.ShowFieldBar(zone[_boardCursor], bottom: true,
                Player.MonsterCurrentAtk[_boardCursor], Player.MonsterCurrentDef[_boardCursor]);
        else
            screen.ShowFieldBar(zone[_boardCursor], bottom: true);
        DuelAudio.Play(DuelAudio.Sfx.Cursor);
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
            var mpos = Player.MonsterPositions[slot];
            bool canAttack = mpos == CardPosition.FaceUpAttack || mpos == CardPosition.FaceDownAttack;
            if (!canAttack)
            { screen.Log("Solo los monstruos en posición de Ataque pueden atacar."); return; }
            if (Player.MonsterHasAttacked[slot])
            { screen.Log($"{Player.MonsterZone[slot].cardName} ya atacó este turno."); return; }

            // El monstruo se mantiene BOCA ABAJO durante la selección de objetivo;
            // se revela solo al CONFIRMAR el ataque (ver PlayerAttackRoutine).
            StartCoroutine(EnterTargetRoutine(slot));
        }
        else
        {
            if (Player.SpellZone[slot] == null) return;
            // Un EQUIPO seteado se ACTIVA eligiendo un monstruo (misma animación que al
            // equipar desde la mano: la carta vuela al monstruo y se absorbe). No se alza
            // al centro como las magias/trampas.
            if (Player.SpellZone[slot].IsEquip)
            {
                if (!System.Array.Exists(Player.MonsterZone, m => m != null))
                { screen.Log("No tienes ningún monstruo al que equipar."); return; }
                EnterEquipTargetFromField(slot);
                return;
            }
            // Magia/trampa seteada: se alza al centro (activar o re-setear).
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
        board.HideSlotCursor();
        if (Opponent.MonsterZone[_targetCursor] != null)
        {
            board.SelectMonster(false, _targetCursor);   // el OBJETIVO se eleva + flota + pulsa
            // La barra del objetivo no revela una carta rival boca abajo.
            screen.ShowTargetBar(Opponent.MonsterZone[_targetCursor],
                                 Opponent.IsMonsterFaceDown(_targetCursor));
        }
        else
        {
            board.ClearSelectionAnim();
            board.ShowSlotCursor(false, true, _targetCursor);
            screen.ShowTargetBar(null, false);
        }
        // El ATACANTE (tu carta) queda con resaltado FIJO durante toda la selección
        // (se fija DESPUÉS de deseleccionar, para que no lo apague el cambio de objetivo).
        board.SetPlayerMonsterHighlight(_attackerSlot, true);
        screen.ShowFieldBar(Player.MonsterZone[_attackerSlot], bottom: true,
            Player.MonsterCurrentAtk[_attackerSlot], Player.MonsterCurrentDef[_attackerSlot]);
        DuelAudio.Play(DuelAudio.Sfx.Cursor);
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
        DuelAudio.Play(DuelAudio.Sfx.Cancel);
        _busy = true;
        _attackerSlot = -1;
        screen.HideTargetBar();
        screen.HideFieldBar();
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
            // Activación MANUAL de la trampa (sin un disparador concreto): se resuelve
            // lo que pueda contra el rival y se descarta. Los efectos que necesitan un
            // atacante/invocado concreto no tendrán objetivo (se indica en el log); esos
            // se disparan solos por evento (ver CheckTraps).
            Player.SpellZone[slot] = null;
            var tres = TrapEffectResolver.Resolve(card, Player, Opponent, -1);
            screen.Log(tres.message);
            screen.UpdateLP(Player.LP, Opponent.LP);
            board.SyncField(Player, Opponent);
            _busy = false;
            if (CheckDefeatedAndEnd()) yield break;
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

    // ── Activar un EQUIPO seteado: elegir monstruo propio (compatible o no) ──
    //
    // Regla (jugador y rival): al activar un equipo colocado se selecciona un monstruo
    // propio. Si es COMPATIBLE se equipa (suma bonus); si NO lo es, el equipo se DESCARTA.

    private void EnterEquipTargetFromField(int spellSlot)
        => StartCoroutine(EnterEquipTargetRoutine(fromHand: false, spellSlot: spellSlot, handIndex: -1));

    private void EnterEquipTargetFromHand(int handIndex)
        => StartCoroutine(EnterEquipTargetRoutine(fromHand: true, spellSlot: -1, handIndex: handIndex));

    private IEnumerator EnterEquipTargetRoutine(bool fromHand, int spellSlot, int handIndex)
    {
        _busy = true;
        _equipFromHand = fromHand;
        _equipSlot = spellSlot;
        _equipHandIndex = handIndex;
        board.ClearSpellHighlights();

        if (fromHand)
        {
            // Venías de ALZAR el equipo en la mano: retira el showcase 3D y restaura la mano 2D.
            screen.ShowFlipArrows(false);
            board.ClearShowcase();
            screen.RefreshHand(Player.Hand);
            screen.HideHandCursor();
            screen.HideCardInfo();
        }

        // Cámara al campo del jugador para elegir el monstruo.
        yield return board.MoveCamera(DuelBoard3D.CameraView.PlayerField, 0.5f);

        _equipCursor = 0;
        for (int i = 0; i < 5; i++)
            if (Player.MonsterZone[i] != null) { _equipCursor = i; break; }
        _busy = false;
        _ctx = KeyCtx.EquipTarget;
        RefreshEquipCursor();
        var eq = fromHand ? Player.Hand[handIndex] : Player.SpellZone[spellSlot];
        screen.Log($"Equipar {eq.cardName}: elige un monstruo (A · S cancela).");
    }

    private void RefreshEquipCursor()
    {
        board.HideSlotCursor();
        if (Player.MonsterZone[_equipCursor] != null)
        {
            board.SelectMonster(true, _equipCursor);
            screen.ShowFieldBar(Player.MonsterZone[_equipCursor], bottom: true,
                Player.MonsterCurrentAtk[_equipCursor], Player.MonsterCurrentDef[_equipCursor]);
        }
        else
        {
            board.ClearSelectionAnim();
            board.ShowSlotCursor(true, true, _equipCursor);
            screen.ShowFieldBar(null, bottom: true);
        }
        DuelAudio.Play(DuelAudio.Sfx.Cursor);
    }

    private void EquipTargetInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        { _equipCursor = (_equipCursor + 4) % 5; RefreshEquipCursor(); }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        { _equipCursor = (_equipCursor + 1) % 5; RefreshEquipCursor(); }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            if (Player.MonsterZone[_equipCursor] == null) { screen.Log("Elige una casilla con un monstruo."); return; }
            StartCoroutine(ConfirmEquipTargetRoutine());
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Escape))
        {
            DuelAudio.Play(DuelAudio.Sfx.Cancel);
            board.ClearSelectionAnim();
            if (_equipFromHand) StartCoroutine(CancelEquipFromHandRoutine());
            else { _equipSlot = -1; EnterBoardContext(); }
        }
    }

    /// <summary>Cancela un equip desde la MANO: la cámara vuelve a tu mano y sigues tu turno
    /// (el equipo sigue en la mano, sin gastarse).</summary>
    private IEnumerator CancelEquipFromHandRoutine()
    {
        _busy = true;
        _equipHandIndex = -1;
        _equipFromHand = false;
        yield return board.MoveCamera(DuelBoard3D.CameraView.Play, 0.5f);
        screen.RefreshHand(Player.Hand);
        _busy = false;
        EnterHandContext();
    }

    /// <summary>
    /// Activa un equipo colocado sobre el monstruo elegido usando la MISMA coreografía que
    /// equipar desde la mano por fusión: la cámara vuelve a la mano del jugador, el monstruo
    /// (desde su casilla) y el equipo (desde su casilla de magias) se LEVANTAN a la pila de
    /// fusión y se resuelve el paso (equipo compatible = absorción con conteo de ATK/DEF;
    /// incompatible = el equipo sale despedido y se descarta). El monstruo vuelve a su casilla
    /// conservando posición, estrella y su estado de ataque de este turno.
    /// </summary>
    private IEnumerator ConfirmEquipTargetRoutine()
    {
        _busy = true;
        int mslot = _equipCursor;
        bool fromHand = _equipFromHand;
        int spellSlot = _equipSlot;
        int handIndex = _equipHandIndex;
        _equipSlot = -1;
        _equipHandIndex = -1;
        _equipFromHand = false;
        var equip = fromHand ? Player.Hand[handIndex] : Player.SpellZone[spellSlot];
        var monster = Player.MonsterZone[mslot];
        board.ClearSelectionAnim();
        board.ClearHighlights();
        screen.HideFieldBar();

        // Estado del monstruo a CONSERVAR (no es una invocación nueva: sigue siendo el mismo).
        CardPosition prevPos = Player.MonsterPositions[mslot];
        GuardianStar prevStar = Player.MonsterStars[mslot];
        bool prevAttacked = Player.MonsterHasAttacked[mslot];
        bool prevChangedPos = Player.MonsterHasChangedPosition[mslot];
        int fromAtk = Player.MonsterCurrentAtk[mslot];
        int fromDef = Player.MonsterCurrentDef[mslot];

        // Resolver el paso (1 material + 1: equipo compatible → Equip; si no → Absorción).
        var materials = new List<CardData> { monster, equip };
        var chain = fusionDb.ResolveChain(materials);
        bool compatible = chain.Steps.Count > 0 && chain.Steps[0].Type == FusionStepType.Equip;

        // Punto de arranque en MUNDO del EQUIPO: su casilla de magias, o su carta en la mano.
        Vector3 equipStart = fromHand
            ? board.HandStartWorld(screen.HandCardScreenPos(handIndex))
            : board.GetPlayerSpellSlotWorld(spellSlot);
        var worldStarts = new List<Vector3> { board.GetPlayerMonsterSlotWorld(mslot), equipStart };

        // Retira el monstruo y el equipo de sus zonas (para que la pila no los duplique).
        Player.MonsterZone[mslot] = null;
        if (fromHand) { Player.Hand.RemoveAt(handIndex); _raisedIndex = -1; _fusionOrder.Clear(); screen.RefreshHand(Player.Hand); }
        else Player.ClearSpell(spellSlot);
        board.SyncField(Player, Opponent);

        // Cámara a la MANO del jugador (misma vista que al fusionar desde la mano).
        yield return board.MoveCamera(DuelBoard3D.CameraView.Play, 0.5f);

        // Las cartas se LEVANTAN a la pila de fusión; el resto de la mano se retira.
        DuelAudio.Play(DuelAudio.Sfx.FusionStart);
        yield return board.AnimateFusionGather(materials, worldStarts);
        if (fromHand) yield return screen.SlideHandDown(0.3f);

        // La carta acumuladora (el monstruo) parte con su ATK/DEF actual.
        board.SetFusionResultStats(fromAtk, fromDef);

        int toAtk = fromAtk, toDef = fromDef;
        if (compatible)
        {
            var step = chain.Steps[0];
            toAtk = fromAtk + step.EquipAtkBonusApplied;
            toDef = fromDef + step.EquipDefBonusApplied;
            Player.RegisterEquip();
            DuelAudio.Play(DuelAudio.Sfx.Equip);
            yield return board.AnimateFusionStep(FusionStepType.Equip, monster, firstSurvives: true,
                                                 fromAtk, toAtk, fromDef, toDef);
            screen.Log($"Equipas {equip.cardName} a {monster.cardName} (ATK {toAtk} / DEF {toDef}).");
        }
        else
        {
            DuelAudio.Play(DuelAudio.Sfx.Cancel);
            yield return board.AnimateFusionStep(FusionStepType.Absorption, monster, firstSurvives: true);
            screen.Log($"{equip.cardName} no es compatible con {monster.cardName}: se descarta.");
        }

        // Restaura el monstruo en SU casilla con los stats finales y su estado conservado
        // (antes de AnimateFusionSummon, que lee MonsterZone[slot] para pintar la carta).
        Player.MonsterZone[mslot] = monster;
        Player.MonsterPositions[mslot] = prevPos;
        Player.MonsterCurrentAtk[mslot] = toAtk;
        Player.MonsterCurrentDef[mslot] = toDef;
        Player.MonsterStars[mslot] = prevStar;
        Player.MonsterHasAttacked[mslot] = prevAttacked;
        Player.MonsterHasChangedPosition[mslot] = prevChangedPos;

        yield return board.AnimateFusionSummon(playerSide: true, mslot, Player);
        board.SyncField(Player, Opponent);
        screen.UpdateLP(Player.LP, Opponent.LP);

        if (fromHand)
        {
            // Equipaste desde la MANO durante tu fase principal: la mano vuelve y sigues tu turno.
            yield return board.MoveCamera(DuelBoard3D.CameraView.Play, 0.5f);
            screen.SetHandVisible(true);
            screen.RefreshHand(Player.Hand);
            _busy = false;
            EnterHandContext();
        }
        else
        {
            // Activaste un equipo del campo durante la batalla: sigues en el tablero.
            yield return board.MoveCamera(DuelBoard3D.CameraView.PlayerField, 0.5f);
            _busy = false;
            EnterBoardContext();
        }
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

        // Una carta boca abajo (en Ataque) SE VOLTEA al CONFIRMARSE el ataque (no antes).
        if (Player.IsMonsterFaceDown(atkSlot))
        {
            Player.SetMonsterPosition(atkSlot, CardPosition.FaceUpAttack);
            board.SyncField(Player, Opponent);
            yield return new WaitForSeconds(0.35f);
        }

        // ResolveCombatAnimated maneja la cámara (mano → cinemática → tablero).
        yield return ResolveCombatAnimated(Player, Opponent, attackerIsPlayer: true, atkSlot, defSlot);
        if (CheckDefeatedAndEnd()) { _busy = false; yield break; }
        _busy = false;
        EnterBoardContext();   // el selector vuelve a tu campo
    }

    // ── Turno de la IA ────────────────────────────────────────────────────

    /// <summary>La flecha del rival "camina" por su mano hasta la carta elegida.</summary>
    private IEnumerator OppArrowWalkTo(int target)
    {
        int n = Opponent.Hand.Count;
        if (n == 0) yield break;
        target = Mathf.Clamp(target, 0, n - 1);
        for (int i = 0; i <= target; i++)
        {
            screen.ShowOpponentHandCursor(i);
            DuelAudio.Play(DuelAudio.Sfx.Cursor);
            yield return new WaitForSeconds(0.16f);
        }
        DuelAudio.Play(DuelAudio.Sfx.Select);    // se detiene y "elige" la carta
        yield return new WaitForSeconds(0.3f);
    }

    /// <summary>El selector de casilla del rival "camina" hasta la casilla elegida
    /// (cámara ya en el campo del rival), como cuando tú eliges casilla.</summary>
    private IEnumerator OppSlotWalkTo(int slot, bool monsterRow)
    {
        slot = Mathf.Clamp(slot, 0, 4);
        for (int i = 0; i <= slot; i++)
        {
            board.ShowSlotCursor(playerSide: false, monsterRow, i);
            DuelAudio.Play(DuelAudio.Sfx.Cursor);
            yield return new WaitForSeconds(0.16f);
        }
        yield return new WaitForSeconds(0.3f);
    }

    /// <summary>
    /// El "selector" del rival RECORRE los monstruos (de su campo o del tuyo) hasta uno
    /// concreto, elevándolos con el MISMO pulso fluido que tu selección en batalla, para
    /// que su turno se vea vivo. Se detiene resaltando el monstruo destino.
    /// </summary>
    private IEnumerator OppWalkSelectMonster(bool playerSide, int toSlot)
    {
        toSlot = Mathf.Clamp(toSlot, 0, 4);
        var zone = playerSide ? Player.MonsterZone : Opponent.MonsterZone;
        for (int i = 0; i <= toSlot; i++)
        {
            if (zone[i] == null) continue;
            board.SelectMonster(playerSide, i);
            DuelAudio.Play(DuelAudio.Sfx.Cursor);
            yield return new WaitForSeconds(i == toSlot ? 0.28f : 0.16f);
        }
        board.SelectMonster(playerSide, toSlot);   // asegura el destino elevado
    }

    private bool _aiActedThisTurn;   // el rival hizo alguna acción este turno (para garantizar una)

    private IEnumerator RunAIMainPhase()
    {
        _busy = true;
        yield return new WaitForSeconds(0.9f);

        var action = _ai.DecideMainAction(Opponent, Player, _terrain);
        _aiActedThisTurn = action.Type != AIActionType.Pass;
        switch (action.Type)
        {
            case AIActionType.Summon:
            {
                var card = action.Card;
                var star = _ai.ChooseGuardianStar(card, Player);
                bool faceDown = action.SummonPosition == CardPosition.FaceDownDefense;
                bool inDef = action.SummonPosition == CardPosition.FaceUpDefense;

                // ── Mismo recorrido que el jugador (mano visible hasta invocar) ──
                // 1) Selección de carta: la flecha camina por su mano; HUD con su info.
                int handIdx = Opponent.Hand.IndexOf(card);
                yield return OppArrowWalkTo(handIdx);
                Vector3 handScreen = screen.OpponentHandCardScreenPos(handIdx);
                screen.HideOpponentHandCursor();
                screen.ShowCardInfoBlank();   // HUD del rival VACÍO: no se revela hasta invocar boca arriba

                // 2) Cámara al TABLERO: el selector camina hasta la casilla (mano SIGUE visible).
                int slot = FirstFreeSlot(Opponent.MonsterZone);
                if (slot < 0) { screen.Log($"{Opponent.Name} no tiene espacio para invocar."); break; }
                yield return board.MoveCamera(DuelBoard3D.CameraView.OpponentMonsterZone, 0.5f);
                yield return OppSlotWalkTo(slot, monsterRow: true);
                board.HideSlotCursor();

                // 3) Cámara de vuelta a la MANO: la carta se LEVANTA de la mano (misma
                //    animación pulida del jugador). AQUÍ desaparece la mano.
                yield return board.MoveCamera(DuelBoard3D.CameraView.OpponentHand, 0.5f);
                screen.SetOpponentHandCardVisible(handIdx, false);
                Vector3 raiseStart = board.HandStartWorldFor(playerSide: false, handScreen);
                yield return DuelTween.Parallel(this,
                    board.ShowcaseRaise(card, faceDown, raiseStart, playerSide: false),
                    screen.SlideOpponentHandDown(0.3f));

                // Igual que el jugador: la carta se CENTRA y se presenta antes de invocarse
                // (el rival ya eligió su Estrella); flota un instante y luego vuela a la casilla.
                yield return board.PresentSummonCard(playerSide: false);
                board.StartSummonIdle();
                DuelAudio.Play(DuelAudio.Sfx.GuardianStar);   // "elige" su Estrella (como tu modal)
                yield return new WaitForSeconds(0.5f);
                board.StopSummonIdle();

                // Coloca en el estado y la carta 3D vuela a su casilla.
                Opponent.Hand.Remove(card);
                int atk = CombatCalculator.CalculateAtk(card, star, star, _terrain);
                int def = CombatCalculator.CalculateDef(card);
                Opponent.PlaceMonsterAt(slot, card, action.SummonPosition, atk, def, star);
                DuelAudio.Play(DuelAudio.Sfx.Summon);
                yield return board.ShowcaseToSlot(slot, Opponent, playerSide: false);

                // 4) Ya invocada: normaliza y la CÁMARA vuelve al tablero a mostrarla.
                //    El HUD se rellena SOLO si quedó boca arriba; si es boca abajo, en blanco.
                board.SyncField(Player, Opponent);
                if (faceDown) screen.ShowCardInfoBlank();
                else screen.ShowCardInfo(card);
                yield return board.MoveCamera(DuelBoard3D.CameraView.OpponentMonsterZone, 0.55f);

                screen.Log($"{Opponent.Name} invoca {card.cardName} en " +
                           $"{(faceDown ? "boca abajo" : inDef ? "Defensa" : "Ataque")} (ATK {atk}/DEF {def}).");

                // TUS trampas pueden responder a la invocación del rival (Trap Hole…).
                yield return AfterSummonTraps(Opponent, slot);
                if (CheckDefeatedAndEnd()) yield break;
                break;
            }

            case AIActionType.PlaySpell:
            {
                // La flecha CAMINA hasta la magia elegida antes de activarla.
                yield return OppArrowWalkTo(Opponent.Hand.IndexOf(action.Card));
                screen.HideOpponentHandCursor();

                Opponent.Hand.Remove(action.Card);
                screen.RefreshOpponentHand(Opponent.Hand); // sale de su mano
                Opponent.RegisterSpell();
                string msg = action.Card.IsFieldSpell
                    ? SetTerrain(action.Card.fieldTerrain)
                    : SpellEffectResolver.Resolve(action.Card, Opponent, Player);
                screen.Log($"{Opponent.Name}: {msg}");
                DuelAudio.Play(DuelAudio.Sfx.Spell);
                screen.UpdateLP(Player.LP, Opponent.LP);
                board.SyncField(Player, Opponent);
                if (CheckDefeatedAndEnd()) yield break;
                break;
            }

            case AIActionType.SetTrap:
            {
                // El rival coloca una TRAMPA boca abajo; se activará sola por evento
                // (ver CheckTraps) cuando el jugador ataque o invoque.
                var card = action.Card;
                int slot = FirstFreeSlot(Opponent.SpellZone);
                if (card == null || slot < 0) { screen.Log($"{Opponent.Name} no puede colocar la carta."); break; }

                int tIdx = Opponent.Hand.IndexOf(card);
                if (tIdx >= 0) yield return OppArrowWalkTo(tIdx);
                screen.HideOpponentHandCursor();

                Opponent.Hand.Remove(card);
                screen.RefreshOpponentHand(Opponent.Hand);
                Opponent.PlaceSpellAt(slot, card);
                if (card.IsTrap) Opponent.RegisterTrapSet();

                yield return board.MoveCamera(DuelBoard3D.CameraView.OpponentMonsterZone, 0.5f);
                DuelAudio.Play(DuelAudio.Sfx.SetCard);
                yield return board.AnimateSetTrap(playerSide: false, slot, card);
                screen.Log($"{Opponent.Name} coloca una carta boca abajo.");
                break;
            }

            case AIActionType.Fuse:
            {
                var materials = action.FuseMaterials;
                var chain = fusionDb.ResolveChain(materials);
                if (chain.FinalResult == null || !chain.FinalResult.IsMonster) { screen.Log($"{Opponent.Name} duda…"); break; }
                // Sin hueco no se puede colocar el resultado: aborta ANTES de consumir
                // los materiales (si no, se perderían de la mano).
                if (FirstFreeSlot(Opponent.MonsterZone) < 0) { screen.Log($"{Opponent.Name} no tiene espacio para fusionar."); break; }

                // Marca una a una (flecha) las cartas que el rival va a fusionar y CAPTURA su
                // posición en pantalla (antes de quitarlas) para que suban desde SU mano.
                var oppHandScreens = new List<Vector3>();
                foreach (var m in materials)
                {
                    int mi = Opponent.Hand.IndexOf(m);
                    oppHandScreens.Add(screen.OpponentHandCardScreenPos(mi));
                    screen.ShowOpponentHandCursor(mi);
                    DuelAudio.Play(DuelAudio.Sfx.Cursor);
                    yield return new WaitForSeconds(0.45f);
                }
                screen.HideOpponentHandCursor();

                foreach (var m in materials)
                    Opponent.Hand.Remove(m);
                screen.RefreshOpponentHand(Opponent.Hand); // salen de su mano

                // Cámara a la mano del RIVAL: la fusión se escenifica de SU lado (no del jugador).
                yield return board.MoveCamera(DuelBoard3D.CameraView.OpponentHand, 0.5f);
                var oppWorldStarts = new List<Vector3>();
                foreach (var sp in oppHandScreens)
                    oppWorldStarts.Add(board.HandStartWorldFor(playerSide: false, sp));

                screen.Log($"¡{Opponent.Name} inicia una fusión!");
                DuelAudio.Play(DuelAudio.Sfx.FusionStart);
                yield return board.AnimateFusionGather(materials, oppWorldStarts, playerSide: false);
                yield return screen.SlideOpponentHandDown(0.3f);   // el resto de su mano se retira (como el jugador)

                CardData stepCurrent = materials[0];
                for (int i = 0; i < chain.Steps.Count; i++)
                {
                    var step = chain.Steps[i];
                    bool firstSurvives = step.Result == stepCurrent;
                    if (step.Type == FusionStepType.Equip) Opponent.RegisterEquip();
                    DuelAudio.Play(step.Type == FusionStepType.Equip ? DuelAudio.Sfx.Equip : DuelAudio.Sfx.Fuse);
                    yield return board.AnimateFusionStep(step.Type, step.Result, firstSurvives);
                    stepCurrent = step.Result;
                }

                // La carta fusionada se CENTRA y se presenta (igual que el jugador) antes
                // de volar a su casilla.
                yield return board.PresentSummonCard(playerSide: false);
                board.StartSummonIdle();
                DuelAudio.Play(DuelAudio.Sfx.GuardianStar);   // "elige" su Estrella (como tu modal)
                yield return new WaitForSeconds(0.5f);
                board.StopSummonIdle();

                var fstar = _ai.ChooseGuardianStar(chain.FinalResult, Player);
                Opponent.RegisterFusion();
                int fatk = CombatCalculator.CalculateAtk(chain.FinalResult, fstar, fstar, _terrain)
                           + chain.TotalEquipAtkBonus;
                int fdef = CombatCalculator.CalculateDef(chain.FinalResult, chain.TotalEquipDefBonus);
                int fslot = Opponent.PlaceMonster(chain.FinalResult, CardPosition.FaceUpAttack, fatk, fdef, fstar);

                yield return board.AnimateFusionSummon(playerSide: false, fslot, Opponent);
                board.SyncField(Player, Opponent);   // normaliza igual que su invocación normal (misma posición)
                screen.Log($"¡{Opponent.Name} fusiona! → {chain.FinalResult.cardName} (ATK {fatk}/DEF {fdef})");

                // TUS trampas pueden responder a la invocación por fusión del rival.
                yield return AfterSummonTraps(Opponent, fslot);
                if (CheckDefeatedAndEnd()) yield break;
                break;
            }

            case AIActionType.Equip:
            {
                var equip = action.Card;
                int slot = action.TargetSlot;
                // Guard: el monstruo objetivo debe seguir en el campo.
                if (equip == null || slot < 0 || slot >= 5 || Opponent.MonsterZone[slot] == null)
                { screen.Log($"{Opponent.Name} duda…"); break; }

                // 1) La flecha camina hasta el equipo en su mano.
                int eqIdx = Opponent.Hand.IndexOf(equip);
                if (eqIdx >= 0) yield return OppArrowWalkTo(eqIdx);
                screen.HideOpponentHandCursor();

                // 2) Aplica el bonus sobre lo que el monstruo YA tenía (suma acumulativa).
                int fromAtk = Opponent.MonsterCurrentAtk[slot];
                int fromDef = Opponent.MonsterCurrentDef[slot];
                int toAtk = fromAtk + equip.equipAtkBonus;
                int toDef = fromDef + equip.equipDefBonus;

                Opponent.Hand.Remove(equip);
                screen.RefreshOpponentHand(Opponent.Hand);
                Opponent.RegisterFusion();          // equipar cuenta como la fusión del turno
                Opponent.RegisterEquip();
                Opponent.MonsterCurrentAtk[slot] = toAtk;
                Opponent.MonsterCurrentDef[slot] = toDef;

                // 3) Cámara a su campo y absorción del equipo con conteo de ATK/DEF.
                yield return board.MoveCamera(DuelBoard3D.CameraView.OpponentMonsterZone, 0.5f);
                DuelAudio.Play(DuelAudio.Sfx.Equip);
                yield return board.AnimateFieldEquip(playerSide: false, slot, equip, fromAtk, toAtk, fromDef, toDef);
                board.SyncField(Player, Opponent);

                screen.Log($"{Opponent.Name} equipa {equip.cardName} a {Opponent.MonsterZone[slot].cardName} " +
                           $"(ATK {toAtk} / DEF {toDef}).");
                break;
            }

            case AIActionType.ActivateSetEquip:
            {
                // El rival ACTIVA un equipo que ya tenía colocado boca abajo, sobre un
                // monstruo propio compatible (esperó a tenerlo en el campo).
                int sslot = action.SpellSlot;
                int mslot = action.TargetSlot;
                if (sslot < 0 || sslot >= 5 || mslot < 0 || mslot >= 5) { screen.Log($"{Opponent.Name} duda…"); break; }
                var equip = Opponent.SpellZone[sslot];
                if (equip == null || !equip.IsEquip || Opponent.MonsterZone[mslot] == null
                    || !equip.EquipAppliesTo(Opponent.MonsterZone[mslot]))
                { screen.Log($"{Opponent.Name} duda…"); break; }

                int fromAtk = Opponent.MonsterCurrentAtk[mslot];
                int fromDef = Opponent.MonsterCurrentDef[mslot];
                int toAtk = fromAtk + equip.equipAtkBonus;
                int toDef = fromDef + equip.equipDefBonus;
                Opponent.MonsterCurrentAtk[mslot] = toAtk;
                Opponent.MonsterCurrentDef[mslot] = toDef;
                Opponent.ClearSpell(sslot);   // el equipo colocado se consume
                Opponent.RegisterEquip();

                yield return board.MoveCamera(DuelBoard3D.CameraView.OpponentMonsterZone, 0.5f);
                board.SyncField(Player, Opponent);   // retira la carta seteada ANTES de la animación
                DuelAudio.Play(DuelAudio.Sfx.Equip);
                yield return board.AnimateFieldEquip(playerSide: false, mslot, equip, fromAtk, toAtk, fromDef, toDef);
                board.SyncField(Player, Opponent);
                screen.Log($"{Opponent.Name} activa {equip.cardName} sobre {Opponent.MonsterZone[mslot].cardName} " +
                           $"(ATK {toAtk} / DEF {toDef}).");
                break;
            }

            case AIActionType.SpellFuse:
            {
                // Zona de magias LLENA: coloca una S/T/E sobre una casilla ocupada; se combinan.
                var incoming = action.Card;
                int sslot = action.SpellSlot;
                if (incoming == null || sslot < 0 || sslot >= 5 || Opponent.SpellZone[sslot] == null)
                { screen.Log($"{Opponent.Name} duda…"); break; }

                var plan = PlanSpellPlacement(Opponent, sslot, incoming);

                int hIdx = Opponent.Hand.IndexOf(incoming);
                if (hIdx >= 0) yield return OppArrowWalkTo(hIdx);
                screen.HideOpponentHandCursor();
                Opponent.Hand.Remove(incoming);
                screen.RefreshOpponentHand(Opponent.Hand);

                yield return board.MoveCamera(DuelBoard3D.CameraView.OpponentMonsterZone, 0.5f);

                if (plan.kind == SpellPlacement.FuseToMonster)
                {
                    Opponent.ClearSpell(sslot);
                    Opponent.PlaceMonster(plan.result, CardPosition.FaceDownDefense,
                        CombatCalculator.CalculateAtk(plan.result, plan.result.starA, plan.result.starA, _terrain),
                        CombatCalculator.CalculateDef(plan.result), plan.result.starA);
                    if (plan.realFusion) Opponent.RegisterFusion();
                    DuelAudio.Play(DuelAudio.Sfx.Fuse);
                    yield return board.AnimateSetTrap(playerSide: false, sslot, incoming);
                    board.SyncField(Player, Opponent);
                    screen.Log($"{Opponent.Name} combina cartas: aparece un monstruo boca abajo.");
                }
                else
                {
                    Opponent.SpellZone[sslot] = plan.result;
                    Opponent.SpellFaceUp[sslot] = false;   // el resultado queda boca abajo
                    if (plan.realFusion) Opponent.RegisterFusion();
                    if (plan.result.IsTrap) Opponent.RegisterTrapSet();
                    DuelAudio.Play(plan.realFusion ? DuelAudio.Sfx.Fuse : DuelAudio.Sfx.SetCard);
                    yield return board.AnimateSetTrap(playerSide: false, sslot, plan.result);
                    board.SyncField(Player, Opponent);
                    screen.Log($"{Opponent.Name} combina cartas boca abajo.");
                }
                break;
            }

            case AIActionType.Pass:
                screen.Log($"{Opponent.Name} pasa.");
                break;
        }
        // Nota: cada caso ya actualiza su propia mano (la invocación la OCULTA al elegir
        // posición y no reaparece; magia/fusión muestran lo que queda).

        yield return new WaitForSeconds(0.6f);
        yield return StartCoroutine(RunAIBattlePhase());
    }

    /// <summary>El rival ajusta la posición (Ataque/Defensa) de sus monstruos antes de
    /// atacar, resaltando cada cambio. Igual que tú cambias con W.</summary>
    private IEnumerator AIApplyPositions()
    {
        var changes = _ai.DecidePositions(Opponent, Player, _terrain);
        if (changes.Count == 0) yield break;
        _aiActedThisTurn = true;   // cambiar posiciones cuenta como acción

        yield return board.MoveCamera(DuelBoard3D.CameraView.OpponentMonsterZone, 0.5f);
        foreach (var (slot, pos) in changes)
        {
            if (Opponent.MonsterZone[slot] == null) continue;
            Opponent.SetMonsterPosition(slot, pos);
            board.SyncField(Player, Opponent);
            board.SetOpponentMonsterHighlight(slot, true);

            bool def = pos == CardPosition.FaceUpDefense;
            screen.Log($"{Opponent.Name} pone {Opponent.MonsterZone[slot].cardName} en {(def ? "Defensa" : "Ataque")}.");
            DuelAudio.Play(DuelAudio.Sfx.Flip);
            yield return new WaitForSeconds(0.55f);
            board.SetOpponentMonsterHighlight(slot, false);
        }
    }

    private IEnumerator RunAIBattlePhase()
    {
        Phase = DuelPhase.BattlePhase;
        screen.ShowPhase("Fase de Batalla");
        DuelAudio.Play(DuelAudio.Sfx.Phase);

        // Antes de atacar, el rival coloca cada monstruo en la mejor posición
        // (Ataque si le conviene atacar; Defensa si no) — como tú con W.
        yield return AIApplyPositions();

        var attacks = _ai.DecideAttacks(Opponent, Player, _terrain);
        foreach (var (atkSlot, defSlot) in attacks)
        {
            if (Opponent.MonsterZone[atkSlot] == null) continue;
            if (Opponent.MonsterPositions[atkSlot] != CardPosition.FaceUpAttack) continue;
            if (Opponent.MonsterHasAttacked[atkSlot]) continue;

            int target = ResolveAITarget(defSlot);
            if (target == -1 && !IsFieldEmpty(Player)) continue;   // sin objetivo válido ni directo

            _aiActedThisTurn = true;
            yield return AIRunAttack(atkSlot, target);
            if (CheckDefeatedAndEnd()) { _busy = false; yield break; }
        }

        // GARANTÍA: si el rival no hizo NADA en todo el turno (ni jugó carta, ni cambió
        // posición, ni atacó), fuerza un ataque de último recurso (o gira un monstruo a
        // Ataque para poder atacar). El rival nunca debe pasar el turno sin actuar.
        if (!_aiActedThisTurn)
        {
            var (fa, fd) = _ai.ForceAnyAttack(Opponent, Player);
            if (fa < 0)
            {
                // Nadie en ATAQUE: gira el monstruo de mayor ATK a Ataque para poder atacar.
                int best = -1, bestAtk = -1;
                for (int i = 0; i < 5; i++)
                    if (Opponent.MonsterZone[i] != null && !Opponent.MonsterHasAttacked[i]
                        && Opponent.MonsterCurrentAtk[i] > bestAtk)
                    { bestAtk = Opponent.MonsterCurrentAtk[i]; best = i; }
                if (best >= 0)
                {
                    Opponent.SetMonsterPosition(best, CardPosition.FaceUpAttack);
                    board.SyncField(Player, Opponent);
                    (fa, fd) = _ai.ForceAnyAttack(Opponent, Player);   // reevalúa objetivo ya en Ataque
                }
            }
            if (fa >= 0)
            {
                int target = ResolveAITarget(fd);
                if (target != -1 || IsFieldEmpty(Player))
                {
                    _aiActedThisTurn = true;
                    yield return AIRunAttack(fa, target);
                    if (CheckDefeatedAndEnd()) { _busy = false; yield break; }
                }
            }
        }

        // Fin de sus ataques: la cámara se reposiciona en SU MANO, para que el giro de
        // vuelta a la tuya (al empezar tu turno) arranque siempre desde el mismo sitio.
        yield return board.MoveCamera(DuelBoard3D.CameraView.OpponentHand, 0.5f);

        _busy = false;
        yield return StartCoroutine(RunEndPhase());
    }

    /// <summary>Objetivo válido para un ataque del rival: el slot pedido, o si está vacío el
    /// primer monstruo del jugador; -1 = ataque directo (campo del jugador vacío).</summary>
    private int ResolveAITarget(int defSlot)
    {
        int target = defSlot;
        if (target >= 0 && Player.MonsterZone[target] == null)
        {
            target = -1;
            for (int i = 0; i < 5; i++)
                if (Player.MonsterZone[i] != null) { target = i; break; }
        }
        return target;
    }

    /// <summary>Ejecuta un ataque del rival con su presentación (cámara + selector) y la
    /// cinemática de combate. <paramref name="target"/> = -1 → ataque directo.</summary>
    private IEnumerator AIRunAttack(int atkSlot, int target)
    {
        Opponent.MonsterHasAttacked[atkSlot] = true;
        yield return board.MoveCamera(DuelBoard3D.CameraView.OpponentMonsterZone, 0.5f);
        yield return OppWalkSelectMonster(playerSide: false, atkSlot);
        screen.ShowFieldBar(Opponent.MonsterZone[atkSlot], bottom: true);
        yield return new WaitForSeconds(0.3f);
        board.ClearSelectionAnim();
        board.SetOpponentMonsterHighlight(atkSlot, true);
        yield return ResolveCombatAnimated(Opponent, Player, attackerIsPlayer: false, atkSlot, target);
        board.ClearHighlights();
    }

    // ── Combate animado (reglas exactas + ventaja de estrella) ────────────

    // ── Trampas (motor de disparo por eventos) ────────────────────────────

    /// <summary>Qué provocó una tanda de trampas (para que el flujo reaccione).</summary>
    private class TrapOutcome
    {
        public bool anyActivated;      // se activó al menos una trampa
        public bool negatedAction;     // el ataque/invocación queda anulado
        public bool destroyedTrigger;  // se destruyó el monstruo que disparó
    }

    /// <summary>
    /// Revisa las trampas seteadas de <paramref name="owner"/> cuyo disparador coincide
    /// con <paramref name="trigger"/> y las activa por prioridad (mayor primero). El
    /// evento lo provocó <paramref name="foe"/> con su monstruo en <paramref name="triggerSlot"/>.
    /// Revela cada trampa, resuelve su efecto y la descarta (salvo las Continuas).
    /// Se detiene si una trampa anula la acción o destruye al monstruo que disparó.
    /// </summary>
    private IEnumerator CheckTraps(Duelist owner, Duelist foe, TrapTrigger trigger,
                                   int triggerSlot, TrapOutcome outcome)
    {
        // Trampas boca abajo que respondan a este evento, ordenadas por prioridad desc.
        var hits = new List<int>();
        for (int i = 0; i < owner.SpellZone.Length; i++)
        {
            var c = owner.SpellZone[i];
            if (c != null && c.IsTrap && c.trapTrigger == trigger) hits.Add(i);
        }
        if (hits.Count == 0) yield break;
        hits.Sort((a, b) => owner.SpellZone[b].resolutionPriority
                                 .CompareTo(owner.SpellZone[a].resolutionPriority));

        bool ownerIsPlayer = owner == Player;
        bool foeIsPlayer = foe == Player;

        foreach (int slot in hits)
        {
            var trap = owner.SpellZone[slot];
            if (trap == null) continue;   // pudo consumirse antes

            screen.Log($"¡{owner.Name} activa una trampa!");
            DuelAudio.Play(DuelAudio.Sfx.Trap);

            // La cámara regresa a la MANO del jugador en turno ANTES de la animación de la
            // trampa (igual que el combate entre monstruos). Los callers la recolocan luego.
            yield return board.MoveCamera(
                IsPlayerTurn ? DuelBoard3D.CameraView.Play : DuelBoard3D.CameraView.OpponentHand, 0.5f);

            // Posiciones EN PANTALLA (antes de resolver: el monstruo puede desaparecer)
            // para que la cinemática arranque desde el tablero.
            Vector2 trapScreen = board.SpellSlotScreenPos(ownerIsPlayer, slot);
            CardData triggerCard = (triggerSlot >= 0 && foe.MonsterZone[triggerSlot] != null)
                ? foe.MonsterZone[triggerSlot] : null;
            Vector2 triggerScreen = triggerCard != null
                ? board.MonsterSlotScreenPos(foeIsPlayer, triggerSlot) : Vector2.zero;

            // Resolver el efecto (aplica el estado lógico) ANTES de escenificarlo.
            var res = TrapEffectResolver.Resolve(trap, owner, foe, triggerSlot);

            // Consumir: Normal/Counter se descartan; Continuous permanece en el campo, ya
            // BOCA ARRIBA (revelada al activarse).
            if (trap.trapKind != TrapKind.Continuous) owner.ClearSpell(slot);
            else owner.SpellFaceUp[slot] = true;

            outcome.anyActivated = true;
            owner.RegisterTrapActivated();
            if (res.negatedAction) outcome.negatedAction = true;
            if (res.destroyedTriggerMonster) outcome.destroyedTrigger = true;

            // Cinemática a pantalla completa: trampa + monstruo al frente, la trampa
            // se desvanece en fuego rosa y el monstruo arde si fue destruido.
            yield return screen.PlayTrapCinematic(trap, trapScreen, triggerCard, triggerScreen,
                                                  res.destroyedTriggerMonster);
            screen.Log(res.message);

            screen.UpdateLP(Player.LP, Opponent.LP);
            board.SyncField(Player, Opponent);
            yield return new WaitForSeconds(0.2f);

            // Si anuló la acción o destruyó al disparador, no siguen más trampas.
            if (res.negatedAction || res.destroyedTriggerMonster) break;
        }
    }

    /// <summary>Tras invocar un monstruo, las trampas del RIVAL del invocador pueden
    /// dispararse (Trap Hole, Negate Summon…). Devuelve true si el monstruo fue destruido.</summary>
    private IEnumerator AfterSummonTraps(Duelist summoner, int summonedSlot)
    {
        var trapOwner = summoner == Player ? Opponent : Player;
        var outcome = new TrapOutcome();
        yield return CheckTraps(trapOwner, summoner, TrapTrigger.MonsterSummoned, summonedSlot, outcome);
        if (outcome.anyActivated)
        {
            board.SyncField(Player, Opponent);
            // Vuelve la cámara al campo del invocador para continuar el flujo.
            yield return board.MoveCamera(summoner == Player ? DuelBoard3D.CameraView.PlayerField
                                                             : DuelBoard3D.CameraView.OpponentMonsterZone, 0.45f);
        }
    }

    private IEnumerator ResolveCombatAnimated(Duelist attacker, Duelist defender,
                                              bool attackerIsPlayer, int atkSlot, int defSlot)
    {
        if (attacker.MonsterZone[atkSlot] == null) yield break;
        string attackerName = attacker.MonsterZone[atkSlot].cardName;

        // Estadística: ataque declarado (directo si no hay defensor).
        if (defSlot == -1) attacker.RegisterDirectAttack(); else attacker.RegisterAttack();

        // ── Trampas del DEFENSOR al DECLARARSE el ataque (Mirror Force, Negate
        //    Attack, destruir atacante, daño…). Si anulan el ataque o destruyen al
        //    atacante, el combate no llega a resolverse. ────────────────────────
        var atkTrap = new TrapOutcome();
        yield return CheckTraps(defender, attacker, TrapTrigger.MonsterDeclaresAttack, atkSlot, atkTrap);
        if (atkTrap.anyActivated && (atkTrap.negatedAction || attacker.MonsterZone[atkSlot] == null))
        {
            board.SyncField(Player, Opponent);
            screen.UpdateLP(Player.LP, Opponent.LP);
            yield return board.MoveCamera(
                attackerIsPlayer ? DuelBoard3D.CameraView.PlayerField : DuelBoard3D.CameraView.OpponentMonsterZone, 0.45f);
            yield break;
        }

        // ── Ataque directo: la carta se centra en pantalla y el daño sale en un
        //    destello, IGUAL que el combate entre cartas (misma cinemática 2D).
        if (defSlot == -1)
        {
            int damage = attacker.MonsterCurrentAtk[atkSlot];
            CardData directAttacker = attacker.MonsterZone[atkSlot];

            // La cámara regresa a la MANO del jugador en turno ANTES del destello
            // (igual que el combate entre monstruos); al terminar vuelve a su campo.
            yield return board.MoveCamera(
                attackerIsPlayer ? DuelBoard3D.CameraView.Play : DuelBoard3D.CameraView.OpponentHand, 0.5f);

            // Barra del atacante (como en el combate) y punto de partida de la carta.
            screen.ShowFieldBar(directAttacker, bottom: true);
            Vector2 atkScr = board.MonsterSlotScreenPos(attackerIsPlayer, atkSlot);

            int dirDef = attacker.MonsterCurrentDef[atkSlot];
            defender.TakeDamage(damage);
            DuelAudio.Play(DuelAudio.Sfx.Attack);
            yield return screen.PlayDirectAttackCinematic(directAttacker, atkScr, damage, damage, dirDef, attackerIsPlayer);

            screen.UpdateLP(Player.LP, Opponent.LP);
            screen.Log($"{attackerName} ataca directamente: {damage} de daño.");
            screen.HideFieldBar();

            yield return board.MoveCamera(
                attackerIsPlayer ? DuelBoard3D.CameraView.PlayerField : DuelBoard3D.CameraView.OpponentMonsterZone, 0.5f);
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

        // El boost de estrella se ESCENIFICA dentro de la cinemática (glow + número que
        // sube), justo cuando las cartas llegan al frente. Aquí solo se registra en el log.
        if (atkBonus > 0) screen.Log($"★ {attackerName} ({atkStar}) domina a {defStar}: +{atkBonus} ATK.");
        if (defBonus > 0) screen.Log($"★ {defenderName} ({defStar}) domina a {atkStar}: +{defBonus} ATK.");

        int atkPower = attacker.MonsterCurrentAtk[atkSlot] + atkBonus;

        // ── Resolución (se calcula ANTES de aplicar, para escenificarla) ──
        CardData attackerCard = attacker.MonsterZone[atkSlot];
        CardData defenderCard = defender.MonsterZone[defSlot];
        int atkShown = attacker.MonsterCurrentAtk[atkSlot];   // ATK/DEF ACTUALES antes de aplicar
        int defShown = defender.MonsterCurrentAtk[defSlot];   // el estado (para pintarlos en la carta:
        int atkDefShown = attacker.MonsterCurrentDef[atkSlot];// se leen ahora porque RemoveMonster
        int defDefShown = defender.MonsterCurrentDef[defSlot];// pone estos valores a 0).
        bool attackerDies = false, defenderDies = false, attackerWeaker = false;
        int lpLost = 0; bool lpOnAttacker = false;
        string logMsg;

        if (inDefense)
        {
            int defPower = defender.MonsterCurrentDef[defSlot];
            int diff = atkPower - defPower;
            if (diff > 0) { defenderDies = true; logMsg = $"{attackerName} destruye a {defenderName} (Defensa). Sin daño."; }
            else if (diff < 0) { attackerWeaker = true; lpLost = -diff; lpOnAttacker = true; logMsg = $"{defenderName} resiste (DEF {defPower}): {attacker.Name} recibe {-diff} de daño."; }
            else logMsg = $"{attackerName} empata con la Defensa de {defenderName}: sin efecto.";
        }
        else
        {
            int defAtk = defender.MonsterCurrentAtk[defSlot] + defBonus;
            int diff = atkPower - defAtk;
            if (diff > 0) { defenderDies = true; lpLost = diff; logMsg = $"{attackerName} derrota a {defenderName}: {diff} de daño."; }
            else if (diff < 0) { attackerDies = true; attackerWeaker = true; lpLost = -diff; lpOnAttacker = true; logMsg = $"¡{attackerName} cae ante {defenderName}! {-diff} de daño."; }
            else { attackerDies = true; defenderDies = true; logMsg = $"¡Empate! {attackerName} y {defenderName} se destruyen mutuamente."; }
        }

        // Estadísticas para el rango: ataque EFECTIVO (destruir un monstruo rival en ATK)
        // y defensa EFECTIVA (tu monstruo en DEF sobrevive al ataque).
        if (!inDefense && defenderDies) attacker.RegisterEffectiveAttack();
        if (inDefense && !defenderDies) defender.RegisterEffectiveDefense();

        // ── Cámara previa al combate ──
        if (attackerIsPlayer)
        {
            yield return board.MoveCamera(DuelBoard3D.CameraView.Play, 0.5f);
        }
        else
        {
            // El rival ataca: la cámara baja a TU campo y su "selector" recorre tus
            // monstruos hasta el objetivo, elevándolo con el mismo pulso fluido que tú
            // (el atacante quedó con resaltado fijo). Luego lo asienta antes del corte.
            yield return board.MoveCamera(DuelBoard3D.CameraView.PlayerFieldFromOpp, 0.6f);
            yield return OppWalkSelectMonster(playerSide: true, defSlot);
            screen.ShowFieldBar(attackerCard, bottom: true);
            screen.ShowTargetBar(defenderCard, faceDown: false);
            yield return new WaitForSeconds(0.5f);
            board.ClearSelectionAnim();

            // Antes de la animación de batalla, la cámara regresa a la MANO del rival
            // (como el jugador vuelve a "Play"); al terminar volverá a su campo.
            yield return board.MoveCamera(DuelBoard3D.CameraView.OpponentHand, 0.5f);
        }

        // HUD: info del ATACANTE (barra de campo) y de la carta ATACADA (barra objetivo),
        // las MISMAS barras que en la selección (sin duplicar).
        screen.ShowFieldBar(attackerCard, bottom: true);
        screen.ShowTargetBar(defenderCard, faceDown: false);

        Vector2 atkScreen = board.MonsterSlotScreenPos(attackerIsPlayer, atkSlot);
        Vector2 defScreen = board.MonsterSlotScreenPos(!attackerIsPlayer, defSlot);

        // ── Aplica el estado lógico ──
        if (defenderDies) defender.RemoveMonster(defSlot);
        if (attackerDies) attacker.RemoveMonster(atkSlot);
        if (lpLost > 0) { if (lpOnAttacker) attacker.TakeDamage(lpLost); else defender.TakeDamage(lpLost); }

        // ── Cinemática de combate (cartas al frente, corte/destello/fuego, LP) ──
        // El "ataque" suena aquí; el corte/destrucción/daño suenan DENTRO de la
        // cinemática (DuelScreen) para que sincronicen con cada golpe.
        DuelAudio.Play(DuelAudio.Sfx.Attack);
        yield return screen.PlayCombatCinematic(
            attackerCard, atkScreen, defenderCard, defScreen,
            new DuelScreen.CombatCine
            {
                attackerDies = attackerDies,
                defenderDies = defenderDies,
                lpLost = lpLost,
                attackerWeaker = attackerWeaker,
                attackerAtk = atkShown,
                attackerDef = atkDefShown,
                attackerBoost = atkBonus,
                defenderAtk = defShown,
                defenderDef = defDefShown,
                defenderBoost = defBonus,
                attackerIsPlayer = attackerIsPlayer,
            });

        screen.Log(logMsg);
        screen.UpdateLP(Player.LP, Opponent.LP);
        board.SyncField(Player, Opponent);
        screen.HideFieldBar();
        screen.HideTargetBar();

        // ── La cámara vuelve al tablero ──
        yield return board.MoveCamera(
            attackerIsPlayer ? DuelBoard3D.CameraView.PlayerField : DuelBoard3D.CameraView.OpponentMonsterZone, 0.5f);
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

        // Música de victoria/derrota (reemplaza la de fondo).
        if (win) DuelAudio.Victory(); else DuelAudio.Defeat();

        // 1) Desaparece el HUD y las manos, y la cámara va a la mano del GANADOR:
        //    queda solo el campo en esa posición.
        yield return DuelTween.Parallel(this,
            screen.FadeOutHudAndHands(0.7f),
            board.MoveCamera(win ? DuelBoard3D.CameraView.Play : DuelBoard3D.CameraView.OpponentHand, 0.9f));

        // 2) "GANASTE"/"PERDISTE": las letras entran en vórtice desde los costados y
        //    forman la palabra en el centro de la pantalla.
        yield return screen.PlayWinLoseVortex(win);

        // Estadísticas del duelo — tabla TÚ vs RIVAL (fuente monoespaciada por <mspace>).
        string Row(string label, int tu, int rival) =>
            $"{label,-20}<color=#FFD98A>{tu,5}</color>   <color=#8AC7FF>{rival,5}</color>\n";
        string stats =
            "<mspace=0.6em>" +
            $"<b>{"",-20}<color=#FFD98A>{"TÚ",5}</color>   <color=#8AC7FF>{"RIVAL",5}</color></b>\n" +
            Row("Ataques", Player.AttacksMade, Opponent.AttacksMade) +
            Row("  · directos", Player.DirectAttacks, Opponent.DirectAttacks) +
            Row("Monstruos jugados", Player.MonstersPlayed, Opponent.MonstersPlayed) +
            Row("Monstruos perdidos", Player.MonstersDestroyed, Opponent.MonstersDestroyed) +
            Row("Fusiones", Player.FusionsPerformed, Opponent.FusionsPerformed) +
            Row("Equipos", Player.EquipsApplied, Opponent.EquipsApplied) +
            Row("Magias", Player.SpellsUsed, Opponent.SpellsUsed) +
            Row("Trampas puestas", Player.TrapsSet, Opponent.TrapsSet) +
            Row("Trampas activadas", Player.TrapsActivated, Opponent.TrapsActivated) +
            Row("Daño recibido", Player.DamageTaken, Opponent.DamageTaken) +
            Row("LP final", Player.LP, Opponent.LP) +
            "</mspace>\n" +
            $"Turnos: {Player.TurnsPlayed + Opponent.TurnsPlayed}    ·    {reason}";

        // ── Puntuación estilo Forbidden Memories (base 50 + modificadores) ──
        int turns = Player.TurnsPlayed + Opponent.TurnsPlayed;
        bool wonByDeckOut = win && Opponent.DeckOut;
        bool wonByLP = win && !Opponent.DeckOut && Opponent.LP <= 0;
        DuelScore duelScore = RankEvaluator.Evaluate(
            turns, Player.EffectiveAttacks, Player.EffectiveDefenses,
            Player.FusionsPerformed, Player.EquipsApplied, Player.SpellsUsed, Player.TrapsActivated,
            wonByLP, wonByDeckOut, wonByExodia: false);

        // Recompensas + starships (solo al ganar).
        var rewards = new List<CardData>();
        int starshipsEarned = 0, starshipsTotal = PlayerCollection.Instance != null ? PlayerCollection.Instance.Starships : 0;
        if (win)
        {
            Phase = DuelPhase.RewardPhase;
            rewards = RankEvaluator.SelectRewards(duelScore, _opponent, _overrides);
            starshipsEarned = RankEvaluator.StarshipsFor(duelScore);

            if (PlayerCollection.Instance != null)
            {
                PlayerCollection.Instance.BeginBatch();
                try
                {
                    if (_opponent != null)
                        PlayerCollection.Instance.RecordDuelResult(_opponent.opponentId, won: true, score: duelScore.score);
                    PlayerCollection.Instance.AddStarships(starshipsEarned);
                    foreach (var r in rewards) if (r != null) PlayerCollection.Instance.AddCopy(r.cardId);
                }
                finally { PlayerCollection.Instance.EndBatch(); }
                starshipsTotal = PlayerCollection.Instance.Starships;
            }
            Phase = DuelPhase.SavePhase;
        }
        else if (_opponent != null)
        {
            PlayerCollection.Instance?.RecordDuelResult(_opponent.opponentId, won: false);
        }

        // La cámara GIRA alrededor del campo mientras aparece el modal.
        board.StartFieldOrbit();

        screen.ShowResultModal(win, duelScore, stats, rewards, starshipsEarned, starshipsTotal,
            onRematch: Rematch,
            onMenu: () => GameNavigator.EnsureExists().ToMainMenu(),
            allowRematch: _opponent != null);
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
        DuelAudio.Ensure();
        // La música PROPIA del rival (OpponentData.battleMusic) tiene prioridad; si no
        // tiene, se usa la del banco de audio (Resources/DuelAudioBank).
        if (_opponent != null && _opponent.battleMusic != null)
            DuelAudio.Music(_opponent.battleMusic);
        else
            DuelAudio.Music();
    }
}
