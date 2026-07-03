using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador central del duelo. Implementa la máquina de estados:
/// Setup > Draw > Main > Battle > End > CheckWin > Reward > Save
///
/// Colócalo en un GameObject vacío "DuelManager" en la escena Duel.
/// Configura las referencias en el Inspector.
/// </summary>
public class DuelManager : MonoBehaviour
{
    // ── Singleton de escena ────────────────────────────────────
    public static DuelManager Instance { get; private set; }

    // ── Configuración (asignar en Inspector) ───────────────────
    [Header("Configuración")]
    [SerializeField] private DuelConfig config;
    [SerializeField] private FusionDatabase fusionDb;
    [SerializeField] private List<CardData> playerDeck;   // mazo del jugador

    [Header("UI (referencias a DuelUI)")]
    [SerializeField] private DuelUI ui;

    // ── Estado del duelo ───────────────────────────────────────
    public DuelPhase Phase { get; private set; }
    public DuelResult Result { get; private set; } = DuelResult.None;
    public bool IsPlayerTurn { get; private set; } = true;

    public Duelist Player { get; private set; }
    public Duelist Opponent { get; private set; }

    private DuelAI _ai;
    private TerrainType _terrain;

    // Guardian Star activa en la invocación actual
    private GuardianStar _pendingGuardianStar;

    // Solo se permite UNA invocación/set/fusión de monstruo por turno (regla estándar).
    // Se resetea al entrar a la Main Phase de cada turno (ver RunTurn).
    private bool _hasSummonedThisTurn = false;

    // ── Ciclo de vida ──────────────────────────────────────────

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        StartDuel();
    }

    // ────────────────────────────────────────────────────────────
    //  SETUP
    // ────────────────────────────────────────────────────────────

    private void StartDuel()
    {
        Phase = DuelPhase.Setup;
        _terrain = config.terrain;

        // Crear participantes
        Player = new Duelist("Jugador", isHuman: true);
        Opponent = new Duelist(config.opponentName, isHuman: false);

        // Cargar mazos
        Player.LoadDeck(new List<CardData>(playerDeck));
        Opponent.LoadDeck(new List<CardData>(config.opponentDeck));

        // Barajar
        Player.ShuffleDeck();
        Opponent.ShuffleDeck();

        // IA
        _ai = new DuelAI(config.aiLevel, fusionDb);

        // UI inicial
        ui.SetTerrain(_terrain);
        ui.UpdateLP(Player.LP, Opponent.LP);
        ui.Log($"Duelo contra {config.opponentName} — ¡Comienza!");

        // Robo inicial (los dos jugadores roban 5)
        Player.DrawUpToFive();
        Opponent.DrawUpToFive();
        ui.RefreshHand(Player.Hand);

        IsPlayerTurn = true;
        StartCoroutine(RunTurn());
    }

    // ────────────────────────────────────────────────────────────
    //  LOOP DE TURNO
    // ────────────────────────────────────────────────────────────

    private IEnumerator RunTurn()
    {
        // ── DRAW PHASE ──────────────────────────────────────────
        Phase = DuelPhase.DrawPhase;
        ui.ShowPhase("Draw Phase");

        if (IsPlayerTurn)
        {
            var drawn = Player.DrawUpToFive();
            ui.AnimateDraw(drawn);
            ui.RefreshHand(Player.Hand);
            if (Player.DeckOut) { EndDuel(DuelResult.OpponentWin, "Deck Out"); yield break; }
        }
        else
        {
            Opponent.DrawUpToFive();
            if (Opponent.DeckOut) { EndDuel(DuelResult.PlayerWin, "¡Oponente sin cartas!"); yield break; }
        }

        yield return new WaitForSeconds(0.6f);

        // ── MAIN PHASE ──────────────────────────────────────────
        Phase = DuelPhase.MainPhase;
        ui.ShowPhase("Main Phase");
        _hasSummonedThisTurn = false; // nueva oportunidad de invocar este turno

        if (IsPlayerTurn)
        {
            // El jugador humano toma el control. El turno avanza cuando
            // llama a PlayerEndMainPhase() desde un botón de la UI.
            ui.EnableMainPhaseInput(true);
            yield break;   // ← el coroutine se reanuda desde PlayerEndMainPhase
        }
        else
        {
            yield return StartCoroutine(RunAIMainPhase());
        }
    }

    // ────────────────────────────────────────────────────────────
    //  ACCIONES DEL JUGADOR (llamadas desde botones de la UI)
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// El jugador invoca un monstruo de su mano BOCA ARRIBA.
    /// Llamar desde UI al seleccionar una carta + botón "Invocar".
    /// </summary>
    public void PlayerSummon(CardData card, GuardianStar chosenStar, CardPosition pos)
    {
        if (Phase != DuelPhase.MainPhase || !IsPlayerTurn) return;
        if (_hasSummonedThisTurn) { ui.Log("Ya invocaste un monstruo este turno."); return; }
        if (!Player.Hand.Contains(card)) return;
        if (!card.IsMonster) { ui.Log($"{card.cardName} no es un monstruo invocable."); return; }
        if (pos == CardPosition.FaceDownAttack || pos == CardPosition.FaceDownDefense)
        {
            ui.Log("Usa 'Set' para invocar boca abajo, no 'Invocar'.");
            return;
        }

        Player.Hand.Remove(card);
        int atk = CombatCalculator.CalculateAtk(card, chosenStar, GetOpponentBestStar(), _terrain);
        int def = CombatCalculator.CalculateDef(card);
        int slot = Player.PlaceMonster(card, pos, atk, def);
        _hasSummonedThisTurn = true;

        ui.Log($"¡{card.cardName} invocado! ATK efectivo: {atk} / DEF: {def}");
        ui.RefreshField(Player, Opponent);
        ui.RefreshHand(Player.Hand);
    }

    /// <summary>
    /// El jugador invoca un monstruo de su mano BOCA ABAJO ("Set"). El ATK/DEF
    /// efectivo se calcula y fija en este momento (igual que una invocación
    /// normal), pero la carta queda oculta hasta que sea atacada o el dueño
    /// decida revelarla manualmente (ver PlayerRevealMonster).
    /// </summary>
    public void PlayerSetMonster(CardData card, GuardianStar chosenStar, CardPosition faceDownPos)
    {
        if (Phase != DuelPhase.MainPhase || !IsPlayerTurn) return;
        if (_hasSummonedThisTurn) { ui.Log("Ya invocaste un monstruo este turno."); return; }
        if (!Player.Hand.Contains(card)) return;
        if (!card.IsMonster) { ui.Log($"{card.cardName} no es un monstruo invocable."); return; }
        if (faceDownPos != CardPosition.FaceDownAttack && faceDownPos != CardPosition.FaceDownDefense)
        {
            ui.Log("Posición inválida para Set.");
            return;
        }

        Player.Hand.Remove(card);
        int atk = CombatCalculator.CalculateAtk(card, chosenStar, GetOpponentBestStar(), _terrain);
        int def = CombatCalculator.CalculateDef(card);
        Player.PlaceMonster(card, faceDownPos, atk, def);
        _hasSummonedThisTurn = true;

        // Log deliberadamente genérico: no revela el nombre de la carta colocada
        // boca abajo, para no filtrar información que el oponente no debería ver.
        ui.Log($"{Player.Name} coloca un monstruo boca abajo.");
        ui.RefreshField(Player, Opponent);
        ui.RefreshHand(Player.Hand);
    }

    /// <summary>
    /// El dueño decide voltear manualmente uno de sus propios monstruos boca
    /// abajo. Solo se puede hacer en Main Phase y solo sobre cartas propias.
    /// </summary>
    public void PlayerRevealMonster(int slot)
    {
        if (Phase != DuelPhase.MainPhase || !IsPlayerTurn) return;
        if (!Player.IsMonsterFaceDown(slot)) return;

        string name = Player.MonsterZone[slot].cardName;
        Player.RevealMonster(slot);
        ui.Log($"{Player.Name} revela: {name}.");
        ui.RefreshField(Player, Opponent);
    }

    /// <summary>
    /// El jugador juega una carta mágica (Spell) de su mano. Las cartas de
    /// Equipo NO se juegan así — se usan como material de fusión (ver PlayerFuse).
    /// </summary>
    public void PlayerCastSpell(CardData card)
    {
        if (Phase != DuelPhase.MainPhase || !IsPlayerTurn) return;
        if (!Player.Hand.Contains(card)) return;
        if (!card.IsSpell) { ui.Log($"{card.cardName} no es una carta mágica."); return; }

        Player.Hand.Remove(card);
        Player.PlaceSpell(card);

        string message = SpellEffectResolver.Resolve(card, Player, Opponent);
        ui.Log(message);

        ui.UpdateLP(Player.LP, Opponent.LP);
        ui.RefreshField(Player, Opponent);
        ui.RefreshHand(Player.Hand);
        CheckDefeated();
    }

    /// <summary>Fusiona cartas de la mano del jugador.</summary>
    public void PlayerFuse(List<CardData> materials)
    {
        if (Phase != DuelPhase.MainPhase || !IsPlayerTurn) return;
        if (_hasSummonedThisTurn) { ui.Log("Ya invocaste/fusionaste un monstruo este turno."); return; }
        if (materials == null || materials.Count < 2)
        {
            ui.Log("Selecciona al menos 2 cartas para fusionar.");
            return;
        }

        // Las cartas de tipo Spell no participan en fusión — se ignoran aquí
        // para que el log y la remoción de mano queden alineados con lo que
        // ResolveChain realmente proceso.
        var usable = new List<CardData>();
        foreach (var m in materials)
            if (m != null && !m.IsSpell) usable.Add(m);

        if (usable.Count < 2)
        {
            ui.Log("Selecciona al menos 2 cartas (monstruo/equipo) para fusionar.");
            return;
        }

        // La cadena se resuelve estrictamente en el orden en que el jugador
        // seleccionó las cartas: ((m0+m1)+m2)+m3... Cada paso aplica la
        // prioridad: receta específica → receta por categoría → equipo → absorción.
        var chain = fusionDb.ResolveChain(usable);

        if (chain.FinalResult == null)
        {
            ui.Log("No hay fusión posible con esas cartas.");
            return;
        }

        // Log detallado de cada paso, para que el jugador vea qué pasó
        // con cada carta (especialmente útil cuando hay absorción).
        CardData stepCurrent = usable[0];
        for (int i = 0; i < chain.Steps.Count; i++)
        {
            var step = chain.Steps[i];
            var nextCard = usable[i + 1];
            switch (step.Type)
            {
                case FusionStepType.Specific:
                    ui.Log($"  {stepCurrent.cardName} + {nextCard.cardName} → ¡Fusión! {step.Result.cardName}");
                    break;
                case FusionStepType.Category:
                    ui.Log($"  {stepCurrent.cardName} + {nextCard.cardName} → Fusión por categoría: {step.Result.cardName}");
                    break;
                case FusionStepType.Equip:
                    ui.Log($"  {stepCurrent.cardName} equipado con {nextCard.cardName} (+{step.EquipAtkBonusApplied} ATK / +{step.EquipDefBonusApplied} DEF)");
                    break;
                case FusionStepType.Absorption:
                    var absorbed = (step.Result == stepCurrent) ? nextCard : stepCurrent;
                    ui.Log($"  Sin fusión entre {stepCurrent.cardName} y {nextCard.cardName} — {absorbed.cardName} es absorbida.");
                    break;
            }
            stepCurrent = step.Result;
        }

        // Todas las cartas usadas en la cadena salen de la mano (ya sea porque
        // se transformaron, se equiparon, o fueron absorbidas — todas se consumen).
        foreach (var c in usable)
            Player.Hand.Remove(c);

        Player.RegisterFusion();
        int atk = CombatCalculator.CalculateAtk(chain.FinalResult, chain.FinalResult.starA, GetOpponentBestStar(), _terrain)
                  + chain.TotalEquipAtkBonus;
        int def = CombatCalculator.CalculateDef(chain.FinalResult, chain.TotalEquipDefBonus);
        Player.PlaceMonster(chain.FinalResult, CardPosition.FaceUpAttack, atk, def);
        _hasSummonedThisTurn = true;

        ui.Log($"¡Fusión completada! → {chain.FinalResult.cardName} (ATK {atk} / DEF {def})");
        ui.RefreshField(Player, Opponent);
        ui.RefreshHand(Player.Hand);
    }

    /// <summary>El jugador termina la Main Phase y pasa a Battle.</summary>
    public void PlayerEndMainPhase()
    {
        if (Phase != DuelPhase.MainPhase || !IsPlayerTurn) return;
        ui.EnableMainPhaseInput(false);
        StartCoroutine(RunBattlePhase());
    }

    /// <summary>El jugador declara un ataque. defSlot = -1 para ataque directo.</summary>
    public void PlayerAttack(int atkSlot, int defSlot)
    {
        if (Phase != DuelPhase.BattlePhase || !IsPlayerTurn) return;
        ResolveCombat(Player, Opponent, atkSlot, defSlot);
        ui.RefreshField(Player, Opponent);
        ui.UpdateLP(Player.LP, Opponent.LP);
        CheckDefeated();
    }

    /// <summary>El jugador termina la Battle Phase.</summary>
    public void PlayerEndBattle()
    {
        if (Phase != DuelPhase.BattlePhase || !IsPlayerTurn) return;
        StartCoroutine(RunEndPhase());
    }

    // ────────────────────────────────────────────────────────────
    //  IA
    // ────────────────────────────────────────────────────────────

    private IEnumerator RunAIMainPhase()
    {
        yield return new WaitForSeconds(1f);

        var action = _ai.DecideMainAction(Opponent, Player, _terrain);

        switch (action.Type)
        {
            case AIActionType.Summon:
                var star = _ai.ChooseGuardianStar(action.Card, Player);
                Opponent.Hand.Remove(action.Card);
                int atk = CombatCalculator.CalculateAtk(action.Card, star, GetPlayerBestStar(), _terrain);
                int def = CombatCalculator.CalculateDef(action.Card);
                Opponent.PlaceMonster(action.Card, CardPosition.FaceUpAttack, atk, def);
                ui.Log($"Oponente invoca {action.Card.cardName} (ATK {atk} / DEF {def})");
                break;

            case AIActionType.Fuse:
                foreach (var m in action.FuseMaterials)
                    Opponent.Hand.Remove(m);
                Opponent.RegisterFusion();
                int fatk = CombatCalculator.CalculateAtk(action.FuseResult, action.FuseResult.starA, GetPlayerBestStar(), _terrain);
                int fdef = CombatCalculator.CalculateDef(action.FuseResult);
                Opponent.PlaceMonster(action.FuseResult, CardPosition.FaceUpAttack, fatk, fdef);
                ui.Log($"¡Oponente fusiona! → {action.FuseResult.cardName} (ATK {fatk} / DEF {fdef})");
                break;

            case AIActionType.Pass:
                ui.Log("Oponente pasa.");
                break;
        }

        ui.RefreshField(Player, Opponent);
        yield return new WaitForSeconds(0.8f);
        yield return StartCoroutine(RunBattlePhase());
    }

    private IEnumerator RunAIBattlePhase()
    {
        var attacks = _ai.DecideAttacks(Opponent, Player, _terrain);
        foreach (var (atkSlot, defSlot) in attacks)
        {
            yield return new WaitForSeconds(0.8f);
            ResolveCombat(Opponent, Player, atkSlot, defSlot);
            ui.RefreshField(Player, Opponent);
            ui.UpdateLP(Player.LP, Opponent.LP);
            if (CheckDefeated()) yield break;
        }
        yield return StartCoroutine(RunEndPhase());
    }

    // ────────────────────────────────────────────────────────────
    //  BATTLE PHASE
    // ────────────────────────────────────────────────────────────

    private IEnumerator RunBattlePhase()
    {
        Phase = DuelPhase.BattlePhase;
        ui.ShowPhase("Battle Phase");

        if (IsPlayerTurn)
        {
            ui.EnableBattleInput(true);
            yield break;   // el jugador declara ataques manualmente
        }
        else
        {
            yield return StartCoroutine(RunAIBattlePhase());
        }
    }

    // ────────────────────────────────────────────────────────────
    //  COMBATE
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Si el monstruo en "slot" está boca abajo, lo revela y lo loguea.
    /// No hace nada si ya estaba boca arriba.
    /// </summary>
    private void RevealIfFaceDown(Duelist owner, int slot)
    {
        if (!owner.IsMonsterFaceDown(slot)) return;
        string name = owner.MonsterZone[slot].cardName;
        owner.RevealMonster(slot);
        ui.Log($"¡Carta boca abajo de {owner.Name} revelada: {name}!");
    }

    /// <summary>
    /// Resuelve un combate declarado. Reglas:
    ///
    ///  Si el defensor está boca abajo (Set), primero se REVELA: pasa a su
    ///  versión boca arriba equivalente (FaceDownAttack→FaceUpAttack,
    ///  FaceDownDefense→FaceUpDefense) y a partir de ahí el combate se
    ///  calcula con normalidad. Si sobrevive, queda revelado permanentemente.
    ///
    ///  Defensor en ATAQUE → se compara ATK vs ATK:
    ///    atacante mayor → defensor destruido, defensor recibe daño = diferencia.
    ///    defensor mayor → atacante destruido, atacante recibe daño = diferencia.
    ///    empate         → ambos destruidos, sin daño.
    ///
    ///  Defensor en DEFENSA → se compara ATK (atacante) vs DEF (defensor):
    ///    ATK > DEF  → defensor destruido, SIN daño de LP a nadie.
    ///    ATK < DEF  → nadie se destruye, atacante recibe daño = (DEF - ATK).
    ///    ATK == DEF → no pasa nada, ningún daño, defensor sobrevive.
    /// </summary>
    private void ResolveCombat(Duelist attacker, Duelist defender, int atkSlot, int defSlot)
    {
        if (attacker.MonsterZone[atkSlot] == null) return;

        string attackerName = attacker.MonsterZone[atkSlot].cardName;
        int atkPower = attacker.MonsterCurrentAtk[atkSlot];

        if (defSlot == -1)
        {
            // Ataque directo
            int damage = atkPower;
            defender.TakeDamage(damage);
            ui.Log($"{attackerName} ataca directamente. Daño: {damage}");
            return;
        }

        if (defender.MonsterZone[defSlot] == null) return;

        // ── Revelación de carta boca abajo ───────────────────────────────
        // Si el objetivo está en Set, se revela ANTES de calcular cualquier
        // estadística de combate. A partir de aquí MonsterPositions ya
        // refleja la cara correcta para el resto del método.
        RevealIfFaceDown(defender, defSlot);

        string defenderName = defender.MonsterZone[defSlot].cardName;
        bool defenderInDefense = defender.MonsterPositions[defSlot] == CardPosition.FaceUpDefense
                               || defender.MonsterPositions[defSlot] == CardPosition.FaceDownDefense;

        if (defenderInDefense)
        {
            int defPower = defender.MonsterCurrentDef[defSlot];
            int diff = atkPower - defPower;

            if (diff > 0)
            {
                // Atacante supera la DEF: defensor destruido, sin daño de LP a nadie.
                defender.RemoveMonster(defSlot);
                ui.Log($"{attackerName} destruye a {defenderName} (en Defensa). Sin daño de batalla.");
            }
            else if (diff < 0)
            {
                // DEF mayor que el ATK: nadie se destruye, el atacante recibe el excedente.
                // El monstruo revelado permanece boca arriba (ya se aplicó en RevealIfFaceDown).
                attacker.TakeDamage(-diff);
                ui.Log($"{attackerName} (ATK {atkPower}) no penetra la Defensa de {defenderName} (DEF {defPower}). " +
                       $"{attacker.Name} recibe {-diff} de daño.");
            }
            else
            {
                // Empate exacto: no pasa nada.
                ui.Log($"{attackerName} (ATK {atkPower}) empata con la Defensa de {defenderName} (DEF {defPower}). Sin efecto.");
            }
            return;
        }

        // Defensor en modo Ataque: ATK vs ATK.
        int defAtkPower = defender.MonsterCurrentAtk[defSlot];
        int diffAtk = atkPower - defAtkPower;

        if (diffAtk > 0)
        {
            defender.TakeDamage(diffAtk);
            defender.RemoveMonster(defSlot);
            ui.Log($"{attackerName} derrota a {defenderName} — Daño: {diffAtk}");
        }
        else if (diffAtk < 0)
        {
            attacker.TakeDamage(-diffAtk);
            attacker.RemoveMonster(atkSlot);
            ui.Log($"¡{attackerName} es destruido por {defenderName}! Daño: {-diffAtk}");
        }
        else
        {
            attacker.RemoveMonster(atkSlot);
            defender.RemoveMonster(defSlot);
            ui.Log($"¡Empate entre {attackerName} y {defenderName}! Ambos destruidos.");
        }
    }

    // ────────────────────────────────────────────────────────────
    //  END PHASE
    // ────────────────────────────────────────────────────────────

    private IEnumerator RunEndPhase()
    {
        Phase = DuelPhase.EndPhase;
        ui.ShowPhase("End Phase");
        ui.EnableBattleInput(false);

        if (IsPlayerTurn) Player.EndTurn();
        else Opponent.EndTurn();

        yield return new WaitForSeconds(0.5f);

        if (CheckDefeated()) yield break;

        // Cambio de turno
        IsPlayerTurn = !IsPlayerTurn;
        ui.Log(IsPlayerTurn ? "— Tu turno —" : $"— Turno de {Opponent.Name} —");
        StartCoroutine(RunTurn());
    }

    // ────────────────────────────────────────────────────────────
    //  CHECK WIN
    // ────────────────────────────────────────────────────────────

    private bool CheckDefeated()
    {
        if (Player.IsDefeated) { EndDuel(DuelResult.OpponentWin, "¡Derrota!"); return true; }
        if (Opponent.IsDefeated) { EndDuel(DuelResult.PlayerWin, "¡Victoria!"); return true; }
        return false;
    }

    // ────────────────────────────────────────────────────────────
    //  FIN DEL DUELO → REWARD → SAVE
    // ────────────────────────────────────────────────────────────

    private void EndDuel(DuelResult result, string message)
    {
        Result = result;
        Phase = DuelPhase.CheckWin;
        ui.ShowResult(result, message);

        if (result == DuelResult.PlayerWin)
            StartCoroutine(RunRewardPhase());
    }

    private IEnumerator RunRewardPhase()
    {
        Phase = DuelPhase.RewardPhase;
        yield return new WaitForSeconds(1.5f);

        // Calcular rango
        DuelRank rank = RankEvaluator.Evaluate(
            Player.LP,
            Opponent.LP,
            Player.DamageTaken,
            Player.FusionsPerformed,
            Player.SpellsUsed,
            Player.TurnsPlayed);

        ui.ShowRank(rank);

        // Seleccionar recompensa
        CardData reward = RankEvaluator.SelectReward(rank, config);

        yield return new WaitForSeconds(1f);

        if (reward != null)
        {
            ui.ShowReward(reward);
            PlayerCollection.Instance.AddCopy(reward.cardId);
            PlayerCollection.Instance.UnlockOpponent(config.opponentId);
        }
        else
        {
            ui.Log("Esta vez no hubo drop.");
        }

        Phase = DuelPhase.SavePhase;
        PlayerCollection.Instance.Save();
        ui.Log("Progreso guardado.");
    }

    // ────────────────────────────────────────────────────────────
    //  HELPERS
    // ────────────────────────────────────────────────────────────

    private GuardianStar GetOpponentBestStar()
    {
        foreach (var m in Opponent.MonsterZone)
            if (m != null) return m.starA;
        return GuardianStar.Sun;
    }

    private GuardianStar GetPlayerBestStar()
    {
        foreach (var m in Player.MonsterZone)
            if (m != null) return m.starA;
        return GuardianStar.Sun;
    }
}