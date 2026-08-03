using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// El OVERLAY 2D del duelo 3D: mano (cartas completas con CardDisplay), LP,
/// fase/turno, log, paneles contextuales (acciones de carta, Estrella
/// Guardiana, monstruo en campo), botones de fase, presentación de duelistas
/// y la secuencia de resultado (banner animado → estadísticas + premios).
///
/// El campo vive en 3D (<see cref="DuelBoard3D"/>); aquí solo está la interfaz.
/// No contiene reglas: reenvía clics al <see cref="DuelController"/>.
/// </summary>
public class DuelScreen : MonoBehaviour
{
    // ── Cabecera ─────────────────────────────────────────────────────────
    [Header("Cabecera")]
    [SerializeField] private TextMeshProUGUI opponentNameText;
    [SerializeField] private TextMeshProUGUI opponentLPText;
    [SerializeField] private TextMeshProUGUI playerLPText;
    [SerializeField] private TextMeshProUGUI opponentCountText;   // cartas restantes en mazo
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI phaseText;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI terrainText;         // valor dentro de la caja CAMPO

    [Header("Log")]
    [SerializeField] private TextMeshProUGUI logText;

    // ── Mano ─────────────────────────────────────────────────────────────
    [Header("Mano")]
    [SerializeField] private Transform handContainer;
    [SerializeField] private DuelHandCardView handTemplate;   // inactiva, se clona

    // ── Barra de info de carta (abajo, estilo FM) ────────────────────────
    [Header("Barra de info de carta")]
    [SerializeField] private GameObject infoBar;
    [SerializeField] private TextMeshProUGUI infoNameText;
    [SerializeField] private TextMeshProUGUI infoStatsText;    // "ATK 800  DEF 700" o categoría
    [SerializeField] private TextMeshProUGUI infoStarText;     // estrellas guardianas
    [SerializeField] private TextMeshProUGUI infoLevelText;    // nivel
    [SerializeField] private Image infoAttributeIcon;
    [SerializeField] private Image infoTypeIcon;
    [SerializeField] private CardIconConfig iconConfig;

    // ── Panel de acción (carta de mano) ──────────────────────────────────
    [Header("Panel de acción")]
    [SerializeField] private GameObject actionPanel;
    [SerializeField] private TextMeshProUGUI actionTitleText;
    [SerializeField] private Button btnSummonAtk;
    [SerializeField] private Button btnSummonDef;
    [SerializeField] private Button btnSetAtk;
    [SerializeField] private Button btnSetDef;
    [SerializeField] private Button btnCastSpell;
    [SerializeField] private Button btnSetTrap;
    [SerializeField] private Button btnCancelAction;

    // ── Panel de Estrella Guardiana ──────────────────────────────────────
    [Header("Panel de Estrella Guardiana")]
    [SerializeField] private GameObject starPanel;
    [SerializeField] private TextMeshProUGUI starTitleText;
    [SerializeField] private Button btnStarA;
    [SerializeField] private Button btnStarB;
    [SerializeField] private Button btnCancelStar;

    // ── Panel de monstruo propio en campo ────────────────────────────────
    [Header("Panel de campo")]
    [SerializeField] private GameObject fieldPanel;
    [SerializeField] private TextMeshProUGUI fieldTitleText;
    [SerializeField] private Button btnChangePosition;
    [SerializeField] private Button btnReveal;
    [SerializeField] private Button btnCancelField;

    // ── Botones de fase ──────────────────────────────────────────────────
    [Header("Botones Main Phase")]
    [SerializeField] private GameObject mainButtons;
    [SerializeField] private Button btnFuse;
    [SerializeField] private Button btnConfirmFusion;
    [SerializeField] private Button btnGoBattle;
    [SerializeField] private Button btnEndTurn;

    [Header("Botones Battle Phase")]
    [SerializeField] private GameObject battleButtons;
    [SerializeField] private Button btnDirectAttack;
    [SerializeField] private Button btnEndBattle;

    // ── Overlays ─────────────────────────────────────────────────────────
    [Header("Presentación")]
    [SerializeField] private GameObject introPanel;
    [SerializeField] private TextMeshProUGUI introNameText;
    [SerializeField] private Image introPortrait;

    [Header("Resultado")]
    [SerializeField] private GameObject resultBanner;            // "¡VICTORIA!" grande
    [SerializeField] private TextMeshProUGUI resultBannerText;
    [SerializeField] private GameObject resultPanel;             // caja de estadísticas
    [SerializeField] private TextMeshProUGUI resultTitleText;
    [SerializeField] private TextMeshProUGUI statsText;          // estadísticas del duelo
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private GameObject rewardGroup;
    [SerializeField] private Image rewardArt;
    [SerializeField] private TextMeshProUGUI rewardNameText;
    [SerializeField] private Button btnRematch;
    [SerializeField] private Button btnBackMenu;

    // ── Eventos / botones expuestos ──────────────────────────────────────
    public event Action<int> OnHandCardClicked;

    public Button BtnSummonAtk => btnSummonAtk;
    public Button BtnSummonDef => btnSummonDef;
    public Button BtnSetAtk => btnSetAtk;
    public Button BtnSetDef => btnSetDef;
    public Button BtnCastSpell => btnCastSpell;
    public Button BtnSetTrap => btnSetTrap;
    public Button BtnCancelAction => btnCancelAction;
    public Button BtnStarA => btnStarA;
    public Button BtnStarB => btnStarB;
    public Button BtnCancelStar => btnCancelStar;
    public Button BtnChangePosition => btnChangePosition;
    public Button BtnReveal => btnReveal;
    public Button BtnCancelField => btnCancelField;
    public Button BtnFuse => btnFuse;
    public Button BtnConfirmFusion => btnConfirmFusion;
    public Button BtnGoBattle => btnGoBattle;
    public Button BtnEndTurn => btnEndTurn;
    public Button BtnDirectAttack => btnDirectAttack;
    public Button BtnEndBattle => btnEndBattle;
    public Button BtnRematch => btnRematch;
    public Button BtnBackMenu => btnBackMenu;

    private readonly List<DuelHandCardView> _handViews = new();

    void Awake()
    {
        if (handTemplate != null) handTemplate.gameObject.SetActive(false);
        HideActionPanel();
        HideStarPanel();
        HideFieldPanel();
        if (introPanel != null) introPanel.SetActive(false);
        if (resultBanner != null) resultBanner.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    // ── Cabecera / estado ────────────────────────────────────────────────

    public void SetOpponentName(string name)
    {
        if (opponentNameText != null) opponentNameText.text = name;
    }

    public void UpdateLP(int playerLP, int opponentLP)
    {
        if (playerLPText != null) playerLPText.text = playerLP.ToString();
        if (opponentLPText != null) opponentLPText.text = opponentLP.ToString();
    }

    /// <summary>Cartas restantes en el mazo de cada duelista (contador del HUD).</summary>
    public void UpdateCounts(int playerDeckCount, int opponentDeckCount)
    {
        if (playerCountText != null) playerCountText.text = playerDeckCount.ToString();
        if (opponentCountText != null) opponentCountText.text = opponentDeckCount.ToString();
    }

    public void ShowPhase(string phase)
    {
        if (phaseText != null) phaseText.text = phase;
    }

    public void ShowTurn(string turn)
    {
        if (turnText != null) turnText.text = turn;
    }

    public void SetTerrain(TerrainType terrain)
    {
        if (terrainText != null)
            terrainText.text = terrain == TerrainType.Neutral ? "—" : terrain.ToString();
    }

    public void Log(string message)
    {
        if (logText == null) { Debug.Log($"[Duelo] {message}"); return; }
        var lines = new List<string>(logText.text.Split('\n'));
        lines.Add(message);
        while (lines.Count > 7) lines.RemoveAt(0);
        logText.text = string.Join("\n", lines).TrimStart('\n');
    }

    // ── Mano ─────────────────────────────────────────────────────────────

    // Las cartas se colocan a mano (fila centrada) para poder animar el robo sin
    // que el HorizontalLayoutGroup las reubique de golpe. Paso amplio para que la
    // mano se despliegue A LO LARGO DE TODA LA PANTALLA (no apiñada en el centro).
    private const float HandStep = 350f;
    private bool _handLayoutReady;

    /// <summary>Desactiva el HorizontalLayoutGroup: el posicionado lo llevamos aquí.</summary>
    private void EnsureManualHandLayout()
    {
        if (_handLayoutReady || handContainer == null) return;
        var hlg = handContainer.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;

        // Ajuste de posición del contenedor de la mano (inspector: Top 11 / Bottom -11).
        var rt = (RectTransform)handContainer;
        rt.offsetMax = new Vector2(rt.offsetMax.x, -11f);
        rt.offsetMin = new Vector2(rt.offsetMin.x, -11f);

        _handLayoutReady = true;
    }

    /// <summary>Posición X (centrada) de la carta i de una mano de n cartas.</summary>
    private static float HandSlotX(int i, int n) => (i - (n - 1) * 0.5f) * HandStep;

    /// <summary>Crea una vista de carta de mano cableada (clic + hover + índice).</summary>
    private DuelHandCardView BuildHandView(CardData card, int index)
    {
        var go = Instantiate(handTemplate.gameObject, handContainer);
        go.SetActive(true);
        var view = go.GetComponent<DuelHandCardView>();
        view.Setup(card);
        view.OnHover = ShowCardInfo;   // al posar el puntero → barra de info

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);

        if (view.Button != null)
            view.Button.onClick.AddListener(() => OnHandCardClicked?.Invoke(index));
        return view;
    }

    public void RefreshHand(List<CardData> hand)
    {
        EnsureManualHandLayout();
        _raisedView = null;   // la carta alzada (si había) se reconstruye abajo

        foreach (var v in _handViews)
            if (v != null) Destroy(v.gameObject);
        _handViews.Clear();

        if (handContainer == null || handTemplate == null) return;

        for (int i = 0; i < hand.Count; i++)
        {
            var view = BuildHandView(hand[i], i);
            ((RectTransform)view.transform).anchoredPosition = new Vector2(HandSlotX(i, hand.Count), 0f);
            _handViews.Add(view);
        }
    }

    /// <summary>
    /// Posición EN PANTALLA (píxeles) de la carta de mano indicada. El canvas es
    /// ScreenSpaceOverlay, así que su RectTransform.position ya está en píxeles. Se usa
    /// para que la carta 3D "se levante" justo desde donde está en la mano. Si el índice
    /// no es válido, devuelve un punto abajo-centro como respaldo.
    /// </summary>
    public Vector3 HandCardScreenPos(int index)
    {
        if (index >= 0 && index < _handViews.Count && _handViews[index] != null)
            return ((RectTransform)_handViews[index].transform).position;
        return new Vector3(Screen.width * 0.5f, Screen.height * 0.12f, 0f);
    }

    /// <summary>Muestra/oculta UNA carta de la mano (para que "salga" al levantarse en 3D).</summary>
    public void SetHandCardVisible(int index, bool visible)
    {
        if (index < 0 || index >= _handViews.Count || _handViews[index] == null) return;
        var cg = _handViews[index].GetComponent<CanvasGroup>();
        if (cg == null) cg = _handViews[index].gameObject.AddComponent<CanvasGroup>();
        cg.alpha = visible ? 1f : 0f;
    }

    /// <summary>
    /// Roba una carta a la mano: entra deslizándose desde el borde derecho y SE
    /// QUEDA en su sitio (no desaparece). Las cartas ya presentes se recolocan al
    /// nuevo centro a la vez. La carta es real (clicable), no un clon temporal.
    /// </summary>
    public IEnumerator AnimateDrawToHand(CardData card)
    {
        if (handContainer == null || handTemplate == null || card == null) yield break;
        EnsureManualHandLayout();

        int index = _handViews.Count;
        var view = BuildHandView(card, index);
        _handViews.Add(view);

        int n = _handViews.Count;
        ((RectTransform)view.transform).anchoredPosition = new Vector2(1200f, 0f); // fuera, derecha

        // Punto de partida (posición actual) y destino (fila centrada de n cartas).
        var starts = new Vector2[n];
        var targets = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            starts[i] = ((RectTransform)_handViews[i].transform).anchoredPosition;
            targets[i] = new Vector2(HandSlotX(i, n), 0f);
        }

        const float dur = 0.34f;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur; k = k * k * (3f - 2f * k); // smoothstep
            for (int i = 0; i < n; i++)
                if (_handViews[i] != null)
                    ((RectTransform)_handViews[i].transform).anchoredPosition =
                        Vector2.LerpUnclamped(starts[i], targets[i], k);
            yield return null;
        }
        for (int i = 0; i < n; i++)
            if (_handViews[i] != null)
                ((RectTransform)_handViews[i].transform).anchoredPosition = targets[i];
    }

    // ── Mano del RIVAL (UI 2D, abajo, solo dorsos) ───────────────────────
    // Se renderiza en el MISMO canvas, MISMA posición (abajo) y MISMA vista de carta
    // que tu mano, con las MISMAS animaciones, pero boca abajo (dorso) y NO interactiva.
    // Solo una mano se ve a la vez: la del jugador de turno (la otra se limpia/oculta).

    private readonly List<DuelHandCardView> _opponentHandViews = new();
    private RectTransform _oppHandContainer;
    private Vector2 _oppHandHome;   // posición "arriba" del contenedor (para restaurar tras deslizarlo)

    /// <summary>Contenedor de la mano del rival: copia EXACTA del de tu mano (abajo).</summary>
    private void EnsureOpponentHandContainer()
    {
        if (_oppHandContainer != null || handContainer == null) return;
        EnsureManualHandLayout();
        var src = (RectTransform)handContainer;
        var go = new GameObject("OpponentHand", typeof(RectTransform));
        _oppHandContainer = (RectTransform)go.transform;
        _oppHandContainer.SetParent(src.parent, false);   // mismo canvas
        _oppHandContainer.anchorMin = src.anchorMin;
        _oppHandContainer.anchorMax = src.anchorMax;
        _oppHandContainer.pivot = src.pivot;
        // Usa la posición HOME de tu mano (no la actual, que puede estar oculta abajo
        // por SetHandVisible(false) justo antes de mostrar la del rival).
        _oppHandHome = _handHomeCached ? _handHomePos : src.anchoredPosition;
        _oppHandContainer.anchoredPosition = _oppHandHome;
        _oppHandContainer.sizeDelta = src.sizeDelta;
        _oppHandContainer.localScale = src.localScale;
    }

    private DuelHandCardView BuildOpponentHandView(CardData card)
    {
        var go = Instantiate(handTemplate.gameObject, _oppHandContainer);
        go.SetActive(true);
        var view = go.GetComponent<DuelHandCardView>();
        view.Setup(card);
        view.SetFace(true);                        // dorso (no se ve qué carta es)
        if (view.Button != null) view.Button.interactable = false; // no interactiva
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f); // igual que tu mano
        return view;
    }

    /// <summary>Rehace la mano del rival (dorsos) desde su lista de cartas.</summary>
    public void RefreshOpponentHand(List<CardData> hand)
    {
        EnsureOpponentHandContainer();
        if (_oppHandContainer != null) _oppHandContainer.anchoredPosition = _oppHandHome; // por si se deslizó
        foreach (var v in _opponentHandViews)
            if (v != null) Destroy(v.gameObject);
        _opponentHandViews.Clear();
        if (hand == null || handTemplate == null || _oppHandContainer == null) return;

        for (int i = 0; i < hand.Count; i++)
        {
            var view = BuildOpponentHandView(hand[i]);
            ((RectTransform)view.transform).anchoredPosition = new Vector2(HandSlotX(i, hand.Count), 0f);
            _opponentHandViews.Add(view);
        }
    }

    /// <summary>Retira toda la mano del rival (al volver la cámara a tu lado).</summary>
    public void ClearOpponentHand()
    {
        foreach (var v in _opponentHandViews)
            if (v != null) Destroy(v.gameObject);
        _opponentHandViews.Clear();
    }

    /// <summary>Oculta/muestra UNA carta de la mano del rival (para que "salga" al
    /// levantarse en 3D, sin duplicado). Igual que <see cref="SetHandCardVisible"/>.</summary>
    public void SetOpponentHandCardVisible(int index, bool visible)
    {
        if (index < 0 || index >= _opponentHandViews.Count || _opponentHandViews[index] == null) return;
        var cg = _opponentHandViews[index].GetComponent<CanvasGroup>();
        if (cg == null) cg = _opponentHandViews[index].gameObject.AddComponent<CanvasGroup>();
        cg.alpha = visible ? 1f : 0f;
    }

    /// <summary>La mano del rival se desliza hacia abajo hasta salir de pantalla
    /// (igual que tu <see cref="SlideHandDown"/>, al invocar).</summary>
    public IEnumerator SlideOpponentHandDown(float duration = 0.3f)
    {
        EnsureOpponentHandContainer();
        if (_oppHandContainer == null) yield break;
        Vector2 from = _oppHandContainer.anchoredPosition;
        Vector2 to = _oppHandHome + new Vector2(0f, -560f);
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            float k = e / duration; k = k * k * (3f - 2f * k);
            _oppHandContainer.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            yield return null;
        }
        _oppHandContainer.anchoredPosition = to;
    }

    /// <summary>Roba una carta a la mano del rival: un dorso entra desde la derecha
    /// (idéntico a tu <see cref="AnimateDrawToHand"/>).</summary>
    public IEnumerator AnimateOpponentDraw(CardData card)
    {
        EnsureOpponentHandContainer();
        if (handTemplate == null || _oppHandContainer == null || card == null) yield break;
        _oppHandContainer.anchoredPosition = _oppHandHome;   // por si quedó deslizada abajo

        int n = _opponentHandViews.Count + 1;
        var view = BuildOpponentHandView(card);
        _opponentHandViews.Add(view);
        ((RectTransform)view.transform).anchoredPosition = new Vector2(1200f, 0f); // fuera, derecha

        var starts = new Vector2[n];
        var targets = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            starts[i] = ((RectTransform)_opponentHandViews[i].transform).anchoredPosition;
            targets[i] = new Vector2(HandSlotX(i, n), 0f);
        }

        const float dur = 0.34f;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur; k = k * k * (3f - 2f * k);
            for (int i = 0; i < n; i++)
                if (_opponentHandViews[i] != null)
                    ((RectTransform)_opponentHandViews[i].transform).anchoredPosition =
                        Vector2.LerpUnclamped(starts[i], targets[i], k);
            yield return null;
        }
        for (int i = 0; i < n; i++)
            if (_opponentHandViews[i] != null)
                ((RectTransform)_opponentHandViews[i].transform).anchoredPosition = targets[i];
    }

    // ── Control por teclado: cursor de mano ──────────────────────────────

    private RectTransform _handCursorRT;
    private Coroutine _handCursorPulse;

    /// <summary>
    /// Punta de flecha en la esquina inferior-izquierda de la carta apuntando
    /// hacia ella (la punta pisa un poco la carta). Late suavemente.
    /// </summary>
    public void ShowHandCursor(int index)
    {
        EnsureHandCursor();
        int n = _handViews.Count;
        if (n == 0) { HideHandCursor(); return; }
        index = Mathf.Clamp(index, 0, n - 1);

        _handCursorRT.SetAsLastSibling();
        _handCursorRT.gameObject.SetActive(true);
        // Esquina inferior-izquierda de la carta (ancho 210, pivote 0.5/0),
        // con la punta un poco encima de la carta.
        _handCursorRT.anchoredPosition = new Vector2(HandSlotX(index, n) - 86f, 34f);
        if (_handCursorPulse == null) _handCursorPulse = StartCoroutine(PulseCursor(_handCursorRT));
    }

    public void HideHandCursor()
    {
        if (_handCursorRT != null) _handCursorRT.gameObject.SetActive(false);
        if (_handCursorPulse != null) { StopCoroutine(_handCursorPulse); _handCursorPulse = null; }
    }

    private void EnsureHandCursor()
    {
        if (_handCursorRT != null || handContainer == null) return;
        _handCursorRT = BuildHandCursor(handContainer);
    }

    /// <summary>Construye una flecha-cursor (dos barras doradas en "∧") bajo un contenedor.</summary>
    private RectTransform BuildHandCursor(Transform parent)
    {
        var go = new GameObject("HandCursor", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(64, 64);
        rt.localRotation = Quaternion.Euler(0f, 0f, -45f); // apunta ↗ a la carta
        MakeCursorBar(rt, -13f, 45f);
        MakeCursorBar(rt, 13f, -45f);
        return rt;
    }

    private void MakeCursorBar(RectTransform parent, float x, float angle)
    {
        var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        var rt = (RectTransform)bar.transform;
        rt.sizeDelta = new Vector2(13f, 46f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        var img = bar.GetComponent<Image>();
        img.color = new Color(0.98f, 0.85f, 0.45f);
        img.raycastTarget = false;
    }

    private IEnumerator PulseCursor(RectTransform rt)
    {
        while (rt != null && rt.gameObject.activeSelf)
        {
            float k = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
            rt.localScale = Vector3.one * (1f + 0.14f * k);
            yield return null;
        }
    }

    // ── Cursor de la mano del RIVAL (misma flecha, sobre la carta que elige la IA) ──
    private RectTransform _oppHandCursorRT;
    private Coroutine _oppHandCursorPulse;

    /// <summary>Marca (flecha) la carta que el rival va a jugar, en su mano.</summary>
    public void ShowOpponentHandCursor(int index)
    {
        EnsureOpponentHandContainer();
        if (_oppHandCursorRT == null && _oppHandContainer != null)
            _oppHandCursorRT = BuildHandCursor(_oppHandContainer);
        int n = _opponentHandViews.Count;
        if (n == 0 || _oppHandCursorRT == null) return;
        index = Mathf.Clamp(index, 0, n - 1);

        _oppHandCursorRT.SetAsLastSibling();
        _oppHandCursorRT.gameObject.SetActive(true);
        _oppHandCursorRT.anchoredPosition = new Vector2(HandSlotX(index, n) - 86f, 34f);
        if (_oppHandCursorPulse == null) _oppHandCursorPulse = StartCoroutine(PulseCursor(_oppHandCursorRT));
    }

    public void HideOpponentHandCursor()
    {
        if (_oppHandCursorRT != null) _oppHandCursorRT.gameObject.SetActive(false);
        if (_oppHandCursorPulse != null) { StopCoroutine(_oppHandCursorPulse); _oppHandCursorPulse = null; }
    }

    /// <summary>Posición EN PANTALLA (píxeles) de la carta i de la mano del rival.</summary>
    public Vector3 OpponentHandCardScreenPos(int index)
    {
        if (index >= 0 && index < _opponentHandViews.Count && _opponentHandViews[index] != null)
            return ((RectTransform)_opponentHandViews[index].transform).position;
        return new Vector3(Screen.width * 0.5f, Screen.height * 0.12f, 0f);
    }

    // ── Carta alzada al centro + flechas de volteo ───────────────────────

    private DuelHandCardView _raisedView;
    private TextMeshProUGUI _flipLeft, _flipRight;

    /// <summary>
    /// Levanta la carta elegida hasta <paramref name="to"/> (respecto al centro
    /// del canvas), a la escala indicada. Se conserva el MISMO tamaño que en la
    /// mano usando scale = 1.
    /// </summary>
    public IEnumerator RaiseHandCard(int index, Vector2 to, float scale)
    {
        if (index < 0 || index >= _handViews.Count || _handViews[index] == null) yield break;
        var view = _handViews[index];
        var rt = (RectTransform)view.transform;

        // Re-anclar al centro del canvas conservando la posición visual.
        Vector3 world = rt.position;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.position = world;
        rt.SetAsLastSibling();

        // La carta alzada es solo visual (el duelo es por teclado): que NO capture
        // el puntero, o el hover reactivaría el InfoBar durante la fase de estrella.
        // (No usar ?? con componentes de Unity: no respeta el == sobrecargado.)
        var cg = view.GetComponent<CanvasGroup>();
        if (cg == null) cg = view.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        Vector2 from = rt.anchoredPosition;
        Vector3 s0 = rt.localScale, s1 = Vector3.one * scale;
        const float dur = 0.3f;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur; k = k * k * (3f - 2f * k);
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            rt.localScale = Vector3.LerpUnclamped(s0, s1, k);
            yield return null;
        }
        rt.anchoredPosition = to;
        rt.localScale = s1;
        _raisedView = view;
    }

    /// <summary>
    /// Alza TODAS las cartas marcadas para fusión con el MISMO tamaño y zona que la
    /// carta única (2D, escala 1, en fila centrada a Y=-50), aguanta un beat y las
    /// disuelve hacia arriba como transición al vórtice 3D. Así seleccionar fusión se
    /// ve igual que seleccionar una sola carta. Las vistas se destruyen luego con
    /// RefreshHand (siguen en _handViews).
    /// </summary>
    public IEnumerator RaiseFusionCards(List<int> indices)
    {
        var rts = new List<RectTransform>();
        var cgs = new List<CanvasGroup>();
        foreach (int index in indices)
        {
            if (index < 0 || index >= _handViews.Count || _handViews[index] == null) continue;
            var view = _handViews[index];
            var rt = (RectTransform)view.transform;

            // Mismo re-anclado que RaiseHandCard: al centro del canvas conservando la
            // posición visual, para que el alzado arranque idéntico al de la carta única.
            Vector3 world = rt.position;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.position = world;
            rt.SetAsLastSibling();

            var cg = view.GetComponent<CanvasGroup>();
            if (cg == null) cg = view.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;

            rts.Add(rt);
            cgs.Add(cg);
        }
        if (rts.Count == 0) yield break;

        // Destino: fila centrada a Y=-50, escala 1 (misma zona/tamaño que la carta única).
        const float spacing = 230f, raiseY = -50f;
        float x0 = -(rts.Count - 1) * spacing * 0.5f;
        var from = new List<Vector2>();
        var s0 = new List<Vector3>();
        var to = new List<Vector2>();
        for (int i = 0; i < rts.Count; i++)
        {
            from.Add(rts[i].anchoredPosition);
            s0.Add(rts[i].localScale);
            to.Add(new Vector2(x0 + i * spacing, raiseY));
        }

        // 1) Alzar (mismo timing y easing que RaiseHandCard).
        const float rise = 0.3f;
        for (float e = 0f; e < rise; e += Time.deltaTime)
        {
            float k = e / rise; k = k * k * (3f - 2f * k);
            for (int i = 0; i < rts.Count; i++)
            {
                rts[i].anchoredPosition = Vector2.LerpUnclamped(from[i], to[i], k);
                rts[i].localScale = Vector3.LerpUnclamped(s0[i], Vector3.one, k);
            }
            yield return null;
        }
        for (int i = 0; i < rts.Count; i++) { rts[i].anchoredPosition = to[i]; rts[i].localScale = Vector3.one; }

        // 2) Beat para leer las cartas seleccionadas.
        yield return new WaitForSeconds(0.3f);

        // 3) Ascienden y se disuelven → el relevo lo toma el vórtice 3D sobre la mesa.
        const float outDur = 0.28f;
        for (float e = 0f; e < outDur; e += Time.deltaTime)
        {
            float k = e / outDur;
            for (int i = 0; i < rts.Count; i++)
            {
                if (rts[i] == null) continue;
                rts[i].anchoredPosition = Vector2.LerpUnclamped(to[i], to[i] + new Vector2(0f, 300f), k);
                rts[i].localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, k);
                cgs[i].alpha = 1f - k;
            }
            yield return null;
        }
    }

    /// <summary>Proyecta un punto de mundo a coordenadas locales del canvas.</summary>
    private Vector2 ToCanvas(Camera cam, Vector3 world)
    {
        Vector3 s = cam.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, s, null, out Vector2 local);
        return local;
    }

    /// <summary>
    /// Lanzamiento + CAÍDA + acostado en un solo movimiento continuo (cámara
    /// QUIETA): la carta sube y CAE con gravedad (acelera al bajar, no flota) hasta
    /// su casilla, y en el último tramo se ACUESTA sobre la mesa (escala no uniforme
    /// hasta el tamaño con el que se ve la carta 3D tumbada, proyectando sus bordes)
    /// para que el cambio por la 3D sea imperceptible.
    /// </summary>
    public IEnumerator FlyRaisedAndLand(Camera cam, Vector3 worldPos, float duration = 0.7f)
    {
        if (_raisedView == null || cam == null) yield break;
        var rt = (RectTransform)_raisedView.transform;
        float w = rt.rect.width, h = rt.rect.height;

        // Tamaño en pantalla de la carta 3D TUMBADA (ancho en X, largo en Z).
        const float halfW = 0.75f, halfZ = 1.05f;
        float wPix = Vector2.Distance(ToCanvas(cam, worldPos + Vector3.left * halfW),
                                      ToCanvas(cam, worldPos + Vector3.right * halfW));
        float hPix = Vector2.Distance(ToCanvas(cam, worldPos + Vector3.back * halfZ),
                                      ToCanvas(cam, worldPos + Vector3.forward * halfZ));
        Vector3 flatScale = new Vector3(wPix / w, hPix / h, 1f);
        Vector2 slotCenter = ToCanvas(cam, worldPos);

        // Trayectoria por CENTROS (Bézier con k LINEAL = arco de gravedad: lento
        // arriba, rápido abajo → la caída acelera de forma natural).
        Vector3 startScale = rt.localScale;
        Vector2 fromCenter = rt.anchoredPosition + new Vector2(0f, h * startScale.y * 0.5f);
        Vector2 control = new Vector2((fromCenter.x + slotCenter.x) * 0.5f,
                                      Mathf.Max(fromCenter.y, slotCenter.y) + 560f);

        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            float k = e / duration;                 // LINEAL → gravedad
            float u = 1f - k;
            Vector2 center = u * u * fromCenter + 2f * u * k * control + k * k * slotCenter;
            // La carta se acuesta en el ÚLTIMO tercio de la caída.
            float flatK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.66f) / 0.34f));
            Vector3 scale = Vector3.Lerp(startScale, flatScale, flatK);
            rt.localScale = scale;
            rt.anchoredPosition = center - new Vector2(0f, h * scale.y * 0.5f);  // pivote base → centro
            yield return null;
        }
        rt.localScale = flatScale;
        rt.anchoredPosition = slotCenter - new Vector2(0f, h * flatScale.y * 0.5f);
    }

    /// <summary>Desvanece la carta alzada (fundido cruzado con el monstruo 3D).</summary>
    public IEnumerator FadeOutRaised(float duration = 0.22f)
    {
        if (_raisedView == null) yield break;
        var cg = _raisedView.GetComponent<CanvasGroup>();
        if (cg == null) yield break;
        float a0 = cg.alpha;
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            cg.alpha = Mathf.Lerp(a0, 0f, e / duration);
            yield return null;
        }
        cg.alpha = 0f;
    }

    /// <summary>Baja la carta alzada de vuelta a su hueco en la mano (para no
    /// tapar el campo mientras se elige la casilla). Luego el controlador llama
    /// a RefreshHand para dejarla exacta.</summary>
    public IEnumerator LowerRaisedToHand(int index, int handCount)
    {
        if (_raisedView == null) yield break;
        var rt = (RectTransform)_raisedView.transform;
        Vector2 from = rt.anchoredPosition;
        Vector2 to = new Vector2(HandSlotX(index, handCount), -427f);   // hueco de la mano
        Vector3 s0 = rt.localScale, s1 = Vector3.one;
        const float dur = 0.25f;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur; k = k * k * (3f - 2f * k);
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            rt.localScale = Vector3.LerpUnclamped(s0, s1, k);
            yield return null;
        }
        _raisedView = null;
    }

    /// <summary>Voltea la carta alzada (encoge en X → cambia cara → expande).</summary>
    public IEnumerator FlipRaised(bool faceDown)
    {
        if (_raisedView == null) yield break;
        var rt = (RectTransform)_raisedView.transform;
        float sx = rt.localScale.x;
        yield return ScaleX(rt, sx, 0f, 0.12f);
        _raisedView.SetFace(faceDown);
        yield return ScaleX(rt, 0f, sx, 0.12f);
    }

    private static IEnumerator ScaleX(RectTransform rt, float from, float to, float dur)
    {
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            var s = rt.localScale; s.x = Mathf.Lerp(from, to, e / dur); rt.localScale = s;
            yield return null;
        }
        var f = rt.localScale; f.x = to; rt.localScale = f;
    }

    /// <summary>Flechas &lt; &gt; a los costados (a la altura <paramref name="y"/>):
    /// indican que ←/→ voltea la carta.</summary>
    public void ShowFlipArrows(bool show, float y = 0f)
    {
        if (show && _flipLeft == null)
        {
            _flipLeft = MakeFlipArrow("FlipArrowL", "<", -330f);
            _flipRight = MakeFlipArrow("FlipArrowR", ">", 330f);
        }
        if (_flipLeft != null)
        {
            _flipLeft.gameObject.SetActive(show);
            if (show) _flipLeft.rectTransform.anchoredPosition = new Vector2(-330f, y);
        }
        if (_flipRight != null)
        {
            _flipRight.gameObject.SetActive(show);
            if (show) _flipRight.rectTransform.anchoredPosition = new Vector2(330f, y);
        }
    }

    private TextMeshProUGUI MakeFlipArrow(string name, string glyph, float x)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.text = glyph;
        t.fontSize = 150;
        t.fontStyle = FontStyles.Bold;
        t.color = new Color(0.98f, 0.85f, 0.45f);
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(160, 200);
        go.transform.SetAsLastSibling();
        return t;
    }

    // ── Insignias de fusión (número de orden sobre la carta) ─────────────

    public void ShowFusionBadge(int index, int order)
    {
        if (index >= 0 && index < _handViews.Count && _handViews[index] != null)
            _handViews[index].ShowFusionBadge(order);
    }

    public void ClearFusionBadges()
    {
        foreach (var v in _handViews)
            if (v != null) v.HideFusionBadge();
    }

    // ── Retirada de la mano (se arrastra hacia abajo) ────────────────────

    private Vector2 _handHomePos;
    private bool _handHomeCached;
    private bool _handHidden;

    /// <summary>La mano completa se desliza hacia abajo hasta salir de pantalla.</summary>
    public IEnumerator SlideHandDown(float duration = 0.3f)
    {
        if (handContainer == null) yield break;
        var rt = (RectTransform)handContainer;
        if (!_handHomeCached) { _handHomePos = rt.anchoredPosition; _handHomeCached = true; }
        if (_handHidden) yield break;
        _handHidden = true;
        HideHandCursor();

        Vector2 from = rt.anchoredPosition;
        Vector2 to = _handHomePos + new Vector2(0f, -560f);
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            float k = e / duration; k = k * k * (3f - 2f * k);
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    /// <summary>Muestra/oculta la mano al instante (al empezar tu turno vuelve).</summary>
    public void SetHandVisible(bool on)
    {
        if (handContainer == null) return;
        var rt = (RectTransform)handContainer;
        if (!_handHomeCached) { _handHomePos = rt.anchoredPosition; _handHomeCached = true; }
        rt.anchoredPosition = on ? _handHomePos : _handHomePos + new Vector2(0f, -560f);
        _handHidden = !on;
    }

    /// <summary>Mueve la carta alzada a otra posición del canvas (fase de estrella).</summary>
    public IEnumerator MoveRaisedTo(Vector2 target, float duration = 0.3f)
    {
        if (_raisedView == null) yield break;
        var rt = (RectTransform)_raisedView.transform;
        Vector2 from = rt.anchoredPosition;
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            float k = e / duration; k = k * k * (3f - 2f * k);
            rt.anchoredPosition = Vector2.LerpUnclamped(from, target, k);
            yield return null;
        }
        rt.anchoredPosition = target;
    }

    // ── Barras de info del CAMPO y del OBJETIVO ──────────────────────────
    // Mismo diseño y misma información que el InfoBar de la mano (nombre, ATK/DEF
    // o categoría, estrellas, nivel, iconos de atributo/tipo). Se crean al vuelo.

    private static readonly Color BarGold   = new Color(0.86f, 0.72f, 0.35f);
    private static readonly Color BarBright = new Color(0.98f, 0.85f, 0.45f);
    private static readonly Color BarLight  = new Color(0.93f, 0.94f, 0.98f);
    private static readonly Color BarFill   = new Color(0.05f, 0.06f, 0.14f, 0.97f);

    private class InfoBar
    {
        public RectTransform root;
        public TextMeshProUGUI name, stats, star, level;
        public Image attr, type;
    }

    private InfoBar _fieldBar, _targetBar;
    private Coroutine _targetBarSlide;

    /// <summary>
    /// Barra de info del CAMPO propio. bottom=false → sobre la mano (elección de
    /// casilla); bottom=true → al fondo (batalla, con la mano oculta).
    /// </summary>
    public void ShowFieldBar(CardData card, bool bottom)
    {
        _fieldBar ??= BuildInfoBar("FieldInfoBar");
        SetBarRect(_fieldBar.root, bottom ? 0.0f : 0.335f, bottom ? 0.10f : 0.435f);
        if (!bottom)
        {
            // Sobre la mano (inspector: Top -35 / Bottom 35) → sube 35 px.
            _fieldBar.root.offsetMax = new Vector2(0f, 35f);
            _fieldBar.root.offsetMin = new Vector2(0f, 35f);
        }
        _fieldBar.root.gameObject.SetActive(true);
        FillInfoBar(_fieldBar, card, hidden: false);
    }

    public void HideFieldBar()
    {
        if (_fieldBar != null) _fieldBar.root.gameObject.SetActive(false);
    }

    /// <summary>Barra del OBJETIVO rival: sube deslizándose justo encima de la
    /// barra de campo (fondo). faceDown oculta los datos.</summary>
    public void ShowTargetBar(CardData card, bool faceDown)
    {
        _targetBar ??= BuildInfoBar("TargetInfoBar");
        SetBarRect(_targetBar.root, 0.10f, 0.20f);
        bool wasVisible = _targetBar.root.gameObject.activeSelf;
        _targetBar.root.gameObject.SetActive(true);
        FillInfoBar(_targetBar, card, hidden: faceDown);

        if (!wasVisible)
        {
            if (_targetBarSlide != null) StopCoroutine(_targetBarSlide);
            _targetBarSlide = StartCoroutine(SlideBarUp(_targetBar.root));
        }
        else _targetBar.root.anchoredPosition = Vector2.zero;
    }

    public void HideTargetBar()
    {
        if (_targetBarSlide != null) { StopCoroutine(_targetBarSlide); _targetBarSlide = null; }
        if (_targetBar != null) _targetBar.root.gameObject.SetActive(false);
    }

    private IEnumerator SlideBarUp(RectTransform bar)
    {
        const float dur = 0.25f;
        Vector2 to = Vector2.zero, from = new Vector2(0f, -170f);
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur; k = k * k * (3f - 2f * k);
            bar.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            yield return null;
        }
        bar.anchoredPosition = to;
        _targetBarSlide = null;
    }

    /// <summary>Rellena la barra con los mismos datos que <see cref="ShowCardInfo"/>.</summary>
    private void FillInfoBar(InfoBar bar, CardData card, bool hidden)
    {
        if (card == null)
        {
            bar.name.text = "Casilla libre";
            bar.stats.text = ""; bar.star.text = ""; bar.level.text = "";
            bar.attr.enabled = bar.type.enabled = false;
            return;
        }
        if (hidden)
        {
            bar.name.text = "Carta boca abajo";
            bar.stats.text = "? ? ?"; bar.star.text = ""; bar.level.text = "";
            bar.attr.enabled = bar.type.enabled = false;
            return;
        }

        bool monster = card.IsMonster;
        bar.name.text = card.cardName;
        bar.stats.text = monster ? $"ATK {card.baseAtk}    DEF {card.baseDef}" : card.CategoryLabel;
        bar.star.text = monster ? $"★ {card.starA} / {card.starB}" : "";
        bar.level.text = (monster && card.stars > 0) ? $"Niv {card.stars}" : "";

        var aSprite = (monster && iconConfig != null) ? iconConfig.GetAttributeIcon(card.attribute) : null;
        bar.attr.sprite = aSprite; bar.attr.enabled = aSprite != null;
        var tSprite = (monster && iconConfig != null) ? iconConfig.GetTypeIcon(card.monsterType) : null;
        bar.type.sprite = tSprite; bar.type.enabled = tSprite != null;
    }

    /// <summary>Construye una barra con el MISMO diseño que el InfoBar de la mano.</summary>
    private InfoBar BuildInfoBar(string name)
    {
        var border = new GameObject(name + "Border", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(transform, false);
        var bImg = border.GetComponent<Image>(); bImg.color = BarGold; bImg.raycastTarget = false;

        var fill = new GameObject(name, typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(border.transform, false);
        var fImg = fill.GetComponent<Image>(); fImg.color = BarFill; fImg.raycastTarget = false;
        var fillRT = (RectTransform)fill.transform;
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = new Vector2(3, 3); fillRT.offsetMax = new Vector2(-3, -3);

        var bar = new InfoBar { root = (RectTransform)border.transform };
        bar.name  = BarText("Name", fillRT, 34, BarBright, TextAlignmentOptions.Left, 0.02f, 0.44f);
        bar.name.fontStyle = FontStyles.Bold;
        bar.stats = BarText("Stats", fillRT, 30, BarLight, TextAlignmentOptions.Center, 0.45f, 0.66f);
        bar.attr  = BarIcon("Attr", fillRT, 0.67f, 0.715f);
        bar.type  = BarIcon("Type", fillRT, 0.72f, 0.765f);
        bar.star  = BarText("Star", fillRT, 28, BarGold, TextAlignmentOptions.Center, 0.77f, 0.92f);
        bar.level = BarText("Level", fillRT, 28, BarLight, TextAlignmentOptions.Right, 0.92f, 0.985f);
        return bar;
    }

    private TextMeshProUGUI BarText(string name, RectTransform parent, float size, Color color,
                                    TextAlignmentOptions align, float xMin, float xMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(xMin, 0.08f); rt.anchorMax = new Vector2(xMax, 0.92f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return t;
    }

    private Image BarIcon(string name, RectTransform parent, float xMin, float xMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.preserveAspect = true; img.enabled = false; img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(xMin, 0.15f); rt.anchorMax = new Vector2(xMax, 0.85f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return img;
    }

    private static void SetBarRect(RectTransform rt, float yMin, float yMax)
    {
        rt.anchorMin = new Vector2(0f, yMin);
        rt.anchorMax = new Vector2(1f, yMax);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ── Estrella Guardiana por teclado (↑/↓ resalta, A confirma) ─────────

    public void HighlightStar(bool aSelected)
    {
        SetStarButtonState(btnStarA, aSelected);
        SetStarButtonState(btnStarB, !aSelected);
    }

    private static void SetStarButtonState(Button b, bool on)
    {
        if (b == null) return;
        b.transform.localScale = on ? Vector3.one * 1.06f : Vector3.one;
        var label = b.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.color = on ? new Color(0.98f, 0.85f, 0.45f) : new Color(0.58f, 0.60f, 0.70f);
    }

    // ── Barra de info de carta ───────────────────────────────────────────

    /// <summary>Muestra los datos de la carta en la barra inferior (estilo FM).</summary>
    public void ShowCardInfo(CardData card)
    {
        if (card == null) { HideCardInfo(); return; }
        if (infoBar != null) infoBar.SetActive(true);

        bool monster = card.IsMonster;
        if (infoNameText != null) infoNameText.text = card.cardName;
        if (infoStatsText != null)
            infoStatsText.text = monster ? $"ATK {card.baseAtk}    DEF {card.baseDef}" : card.CategoryLabel;
        if (infoStarText != null)
            infoStarText.text = monster ? $"★ {card.starA} / {card.starB}" : "";
        if (infoLevelText != null)
            infoLevelText.text = (monster && card.stars > 0) ? $"Niv {card.stars}" : "";

        if (infoAttributeIcon != null)
        {
            var s = (monster && iconConfig != null) ? iconConfig.GetAttributeIcon(card.attribute) : null;
            infoAttributeIcon.sprite = s;
            infoAttributeIcon.enabled = s != null;
        }
        if (infoTypeIcon != null)
        {
            var s = (monster && iconConfig != null) ? iconConfig.GetTypeIcon(card.monsterType) : null;
            infoTypeIcon.sprite = s;
            infoTypeIcon.enabled = s != null;
        }
    }

    public void HideCardInfo()
    {
        if (infoBar != null) infoBar.SetActive(false);
    }

    /// <summary>Muestra el HUD de info VISIBLE pero VACÍO (sin datos). Se usa para el
    /// rival mientras invoca: no se revela nada hasta que la carta quede boca arriba.</summary>
    public void ShowCardInfoBlank()
    {
        if (infoBar != null) infoBar.SetActive(true);
        if (infoNameText != null) infoNameText.text = "";
        if (infoStatsText != null) infoStatsText.text = "";
        if (infoStarText != null) infoStarText.text = "";
        if (infoLevelText != null) infoLevelText.text = "";
        if (infoAttributeIcon != null) infoAttributeIcon.enabled = false;
        if (infoTypeIcon != null) infoTypeIcon.enabled = false;
    }

    // ── Cinemática de COMBATE ────────────────────────────────────────────
    // Fondo negro; ambas cartas dejan el campo y se agrandan al frente (ATACANTE a la
    // DERECHA); corte + destello sobre la perdedora y LP desde el brillo; fuego consume a
    // la destruida; la(s) superviviente(s) vuelven a su sitio. Todo en el canvas 2D.

    /// <summary>Resultado del combate para escenificarlo.</summary>
    public struct CombatCine
    {
        public bool attackerDies;
        public bool defenderDies;
        public int  lpLost;          // 0 = sin daño de batalla
        public bool attackerWeaker;  // corte primero al atacado (destello), luego al atacante
        public int  attackerAtk;     // ATK ACTUAL a mostrar (antes del boost de estrella)
        public int  attackerDef;     // DEF ACTUAL a mostrar
        public int  attackerBoost;   // +ATK por Estrella Guardiana (0 = sin boost)
        public int  defenderAtk;
        public int  defenderDef;
        public int  defenderBoost;
    }

    [SerializeField] private float cineCardWidthFrac = 0.42f;   // ancho de cada carta (fracción del canvas)

    private RectTransform CineParent =>
        (RectTransform)(handContainer != null && handContainer.parent != null ? handContainer.parent : transform);

    public IEnumerator PlayCombatCinematic(
        CardData attackerCard, Vector2 attackerFieldScreen,
        CardData defenderCard, Vector2 defenderFieldScreen,
        CombatCine outcome)
    {
        var parent = CineParent;
        if (parent == null || handTemplate == null) yield break;

        var root = new GameObject("CombatCine", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        StretchFull(root);
        root.SetAsLastSibling();

        var bg = MakeSolid(root, new Color(0f, 0f, 0f, 0f));
        StretchFull((RectTransform)bg.transform);

        float rw = root.rect.width;
        float cardW = cineCardWidthFrac * rw;
        Vector2 rightPos = new Vector2( rw * 0.24f, 0f);   // ATACANTE
        Vector2 leftPos  = new Vector2(-rw * 0.24f, 0f);   // ATACADA

        var atkRT = BuildCineCard(root, attackerCard, cardW);
        var defRT = BuildCineCard(root, defenderCard, cardW);
        // Las cartas muestran su ATK/DEF ACTUAL (con terreno/equipos/buffs), no la base.
        var atkView = atkRT.GetComponent<DuelHandCardView>();
        var defView = defRT.GetComponent<DuelHandCardView>();
        if (atkView != null) atkView.SetCurrentStats(outcome.attackerAtk, outcome.attackerDef);
        if (defView != null) defView.SetCurrentStats(outcome.defenderAtk, outcome.defenderDef);
        Vector2 atkStart = ScreenToLocal(root, attackerFieldScreen);
        Vector2 defStart = ScreenToLocal(root, defenderFieldScreen);
        atkRT.anchoredPosition = atkStart; atkRT.localScale = Vector3.one * 0.12f;
        defRT.anchoredPosition = defStart; defRT.localScale = Vector3.one * 0.12f;
        float atkFull = CineScaleFor(atkRT, cardW), defFull = CineScaleFor(defRT, cardW);

        // Entran: dejan el campo y se agrandan al frente mientras el fondo se oscurece.
        const float inDur = 0.5f;
        for (float e = 0f; e < inDur; e += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, e / inDur);
            bg.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.92f, k));
            atkRT.anchoredPosition = Vector2.LerpUnclamped(atkStart, rightPos, k);
            defRT.anchoredPosition = Vector2.LerpUnclamped(defStart, leftPos, k);
            atkRT.localScale = Vector3.one * Mathf.Lerp(0.12f, atkFull, k);
            defRT.localScale = Vector3.one * Mathf.Lerp(0.12f, defFull, k);
            yield return null;
        }
        bg.color = new Color(0f, 0f, 0f, 0.92f);
        atkRT.anchoredPosition = rightPos; atkRT.localScale = Vector3.one * atkFull;
        defRT.anchoredPosition = leftPos;  defRT.localScale = Vector3.one * defFull;
        yield return new WaitForSeconds(0.15f);

        // ── Boost de Estrella Guardiana (glow + número que sube), ya en posición ──
        if (outcome.attackerBoost > 0) yield return StarBoostFx(root, atkRT, outcome.attackerAtk, outcome.attackerBoost);
        if (outcome.defenderBoost > 0) yield return StarBoostFx(root, defRT, outcome.defenderAtk, outcome.defenderBoost);

        // ── Choque ──
        if (outcome.attackerWeaker)
        {
            yield return FlashOver(root, defRT);   // solo destello sobre la atacada
            yield return SlashOver(root, atkRT);   // luego corte sobre el atacante
            if (outcome.lpLost > 0) yield return ShowCineLP(root, outcome.lpLost, rightPos);
        }
        else
        {
            yield return SlashOver(root, defRT);   // corte sobre la atacada
            if (outcome.attackerDies) yield return SlashOver(root, atkRT); // empate: también al atacante
            if (outcome.lpLost > 0) yield return ShowCineLP(root, outcome.lpLost, leftPos);
        }

        // ── Fuego sobre las destruidas ──
        var fires = new List<IEnumerator>();
        if (outcome.attackerDies) fires.Add(FireConsume(root, atkRT));
        if (outcome.defenderDies) fires.Add(FireConsume(root, defRT));
        if (fires.Count > 0) yield return DuelTween.Parallel(this, fires.ToArray());

        // ── Las supervivientes vuelven a su sitio ──
        var backs = new List<IEnumerator>();
        if (!outcome.attackerDies) backs.Add(CineReturn(atkRT, ScreenToLocal(root, attackerFieldScreen)));
        if (!outcome.defenderDies) backs.Add(CineReturn(defRT, ScreenToLocal(root, defenderFieldScreen)));
        if (backs.Count > 0) yield return DuelTween.Parallel(this, backs.ToArray());

        const float outDur = 0.35f;
        for (float e = 0f; e < outDur; e += Time.deltaTime)
        { bg.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.92f, 0f, e / outDur)); yield return null; }
        Destroy(root.gameObject);
    }

    /// <summary>
    /// Cinemática de ATAQUE DIRECTO: la carta atacante se coloca a la IZQUIERDA y a la
    /// DERECHA estalla un RESPLANDOR con el daño. El color del resplandor es un "semáforo
    /// de daño": verde (poco) → amarillo → rojo (mucho), y más intenso/grande cuanto
    /// mayor sea el daño. Luego la carta vuelve a su sitio.
    /// </summary>
    public IEnumerator PlayDirectAttackCinematic(
        CardData attackerCard, Vector2 attackerFieldScreen, int damage, int atkShown, int defShown)
    {
        var parent = CineParent;
        if (parent == null || handTemplate == null) yield break;

        var root = new GameObject("DirectCine", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        StretchFull(root);
        root.SetAsLastSibling();

        var bg = MakeSolid(root, new Color(0f, 0f, 0f, 0f));
        StretchFull((RectTransform)bg.transform);

        float rw = root.rect.width;
        float cardW = cineCardWidthFrac * rw;
        Vector2 leftPos  = new Vector2(-rw * 0.22f, 0f);   // CARTA a la izquierda
        Vector2 rightPos = new Vector2( rw * 0.24f, 0f);   // RESPLANDOR + DAÑO a la derecha

        // Semáforo de daño: 0 → verde, medio → amarillo, alto → rojo (HSV de hue 0.33→0).
        float sev = Mathf.Clamp01(damage / 3000f);
        Color dmgColor = Color.HSVToRGB(Mathf.Lerp(0.33f, 0f, sev), 1f, 1f);

        var atkRT = BuildCineCard(root, attackerCard, cardW);
        var atkView = atkRT.GetComponent<DuelHandCardView>();
        if (atkView != null) atkView.SetCurrentStats(atkShown, defShown);   // ATK/DEF actuales en la carta
        Vector2 atkStart = ScreenToLocal(root, attackerFieldScreen);
        atkRT.anchoredPosition = atkStart; atkRT.localScale = Vector3.one * 0.12f;
        float atkFull = CineScaleFor(atkRT, cardW);

        // Entra: deja el campo y se agranda a la IZQUIERDA mientras el fondo se oscurece.
        const float inDur = 0.5f;
        for (float e = 0f; e < inDur; e += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, e / inDur);
            bg.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.92f, k));
            atkRT.anchoredPosition = Vector2.LerpUnclamped(atkStart, leftPos, k);
            atkRT.localScale = Vector3.one * Mathf.Lerp(0.12f, atkFull, k);
            yield return null;
        }
        bg.color = new Color(0f, 0f, 0f, 0.92f);
        atkRT.anchoredPosition = leftPos; atkRT.localScale = Vector3.one * atkFull;
        yield return new WaitForSeconds(0.12f);

        // Embestida de la carta hacia la derecha + fogonazo del color del daño.
        yield return DirectStrike(root, atkRT, leftPos, rightPos, dmgColor, sev);

        // Resplandor + daño (a la derecha), del color del semáforo.
        if (damage > 0) yield return ShowDamageBurst(root, damage, rightPos, dmgColor, sev);

        // La carta vuelve a su sitio del campo y el fondo se aclara.
        yield return CineReturn(atkRT, ScreenToLocal(root, attackerFieldScreen));

        const float outDur = 0.35f;
        for (float e = 0f; e < outDur; e += Time.deltaTime)
        { bg.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.92f, 0f, e / outDur)); yield return null; }
        Destroy(root.gameObject);
    }

    /// <summary>La carta embiste desde la izquierda hacia el punto del resplandor (derecha)
    /// y regresa, con un fogonazo de pantalla teñido del color del daño.</summary>
    private IEnumerator DirectStrike(RectTransform root, RectTransform card,
                                     Vector2 home, Vector2 target, Color color, float severity)
    {
        Vector3 baseS = card.localScale;
        Vector2 lungeTo = Vector2.Lerp(home, target, 0.5f);   // avanza hacia el resplandor

        var screenFlash = MakeSolid(root, new Color(color.r, color.g, color.b, 0f));
        StretchFull((RectTransform)screenFlash.transform);

        const float dur = 0.42f;
        const float peak = 0.45f;   // fracción de ida (embestida) vs. vuelta
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur;
            float m = k < peak
                ? Mathf.Pow(k / peak, 2f)                                   // ida acelerada
                : 1f - Mathf.SmoothStep(0f, 1f, (k - peak) / (1f - peak));  // vuelta suave
            card.anchoredPosition = Vector2.LerpUnclamped(home, lungeTo, m);

            float hit = Mathf.Clamp01(1f - Mathf.Abs(k - peak) / 0.3f);
            screenFlash.color = new Color(color.r, color.g, color.b, hit * Mathf.Lerp(0.25f, 0.6f, severity));
            card.localScale = baseS * (1f + 0.05f * hit);
            yield return null;
        }
        card.anchoredPosition = home; card.localScale = baseS;
        Destroy(screenFlash.gameObject);
    }

    /// <summary>Resplandor radial + número de daño en <paramref name="pos"/>, del color del
    /// semáforo. A más daño (<paramref name="severity"/>): resplandor más grande/intenso,
    /// anillo de choque, número más grande y una sacudida de cámara más fuerte.</summary>
    private IEnumerator ShowDamageBurst(RectTransform root, int amount, Vector2 pos, Color color, float severity)
    {
        float rw = root.rect.width;
        float glowSize = rw * Mathf.Lerp(0.34f, 0.66f, severity);   // más grande a más daño

        // Anillo de choque que se expande (más notorio a mayor daño).
        var ring = MakeGlow(root, new Color(color.r, color.g, color.b, 0f));
        var rrt = (RectTransform)ring.transform;
        rrt.pivot = rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(glowSize, glowSize);
        rrt.anchoredPosition = pos;

        // Núcleo del resplandor.
        var glow = MakeGlow(root, new Color(color.r, color.g, color.b, 0f));
        var grt = (RectTransform)glow.transform;
        grt.pivot = grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.sizeDelta = new Vector2(glowSize, glowSize);
        grt.anchoredPosition = pos;

        // Número de daño (más grande cuanto mayor el daño), del color del semáforo.
        var numGO = new GameObject("DmgNum", typeof(RectTransform));
        numGO.transform.SetParent(root, false);
        var nrt = (RectTransform)numGO.transform;
        nrt.pivot = nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
        nrt.sizeDelta = new Vector2(rw * 0.5f, 300f);
        nrt.anchoredPosition = pos;
        var num = numGO.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) num.font = TMP_Settings.defaultFontAsset;
        num.fontStyle = FontStyles.Bold;
        num.alignment = TextAlignmentOptions.Center;
        num.raycastTarget = false;
        num.color = color;
        num.fontSize = Mathf.Lerp(100f, 200f, severity);

        // Impacto: el número cuenta de 0 al daño mientras el resplandor pulsa y el
        // anillo de choque se expande (todo más intenso cuanto mayor el daño).
        const float upDur = 0.55f;
        for (float e = 0f; e < upDur; e += Time.deltaTime)
        {
            float k = e / upDur;
            int val = Mathf.RoundToInt(Mathf.Lerp(0f, amount, Mathf.SmoothStep(0f, 1f, k)));
            num.text = $"-{val}";
            float pulse = Mathf.Sin(Mathf.Clamp01(k / 0.5f) * Mathf.PI);
            float aCore = Mathf.Lerp(0.25f, 1f, k) * (0.7f + 0.3f * pulse);
            glow.color = new Color(color.r, color.g, color.b, aCore);
            grt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.3f + 0.6f * severity, k);
            // anillo: se expande y se desvanece
            ring.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.7f, 0f, k) * (0.5f + 0.5f * severity));
            rrt.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.6f + 1.5f * severity, k);
            nrt.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(k / 0.3f));
            yield return null;
        }
        num.text = $"-{amount}";

        yield return new WaitForSeconds(0.35f);

        const float outDur = 0.35f;
        for (float e = 0f; e < outDur; e += Time.deltaTime)
        {
            float a = 1f - e / outDur;
            var c = num.color; c.a = a; num.color = c;
            glow.color = new Color(color.r, color.g, color.b, a * 0.85f);
            yield return null;
        }
        Destroy(ring.gameObject); Destroy(glow.gameObject); Destroy(numGO);
    }

    /// <summary>
    /// Cinemática de ACTIVACIÓN DE TRAMPA (estilo combate): la trampa entra a la
    /// izquierda y el monstruo que la disparó (atacante/invocado) a la derecha, a
    /// pantalla completa sobre fondo oscuro. La trampa fulgura al activarse; si su
    /// efecto DESTRUYE al monstruo, este recibe un corte y arde en fuego naranja; y
    /// la trampa siempre se desvanece en FUEGO ROSA. Si el monstruo sobrevive, vuelve
    /// a su casilla.
    /// </summary>
    public IEnumerator PlayTrapCinematic(
        CardData trapCard, Vector2 trapScreen,
        CardData triggerCard, Vector2 triggerScreen, bool destroysTrigger)
    {
        var parent = CineParent;
        if (parent == null || handTemplate == null) yield break;

        var root = new GameObject("TrapCine", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        StretchFull(root);
        root.SetAsLastSibling();

        var bg = MakeSolid(root, new Color(0f, 0f, 0f, 0f));
        StretchFull((RectTransform)bg.transform);

        float rw = root.rect.width;
        float cardW = cineCardWidthFrac * rw;
        bool hasTrigger = triggerCard != null;

        Vector2 trapPos = hasTrigger ? new Vector2(-rw * 0.24f, 0f) : Vector2.zero;  // trampa izq (o centro)
        Vector2 trigPos = new Vector2(rw * 0.24f, 0f);                                // atacante der

        var trapRT = BuildCineCard(root, trapCard, cardW);   // BuildCineCard la deja boca arriba
        Vector2 trapStart = ScreenToLocal(root, trapScreen);
        trapRT.anchoredPosition = trapStart; trapRT.localScale = Vector3.one * 0.12f;
        float trapFull = CineScaleFor(trapRT, cardW);

        RectTransform trigRT = null; Vector2 trigStart = default; float trigFull = 1f;
        if (hasTrigger)
        {
            trigRT = BuildCineCard(root, triggerCard, cardW);
            trigStart = ScreenToLocal(root, triggerScreen);
            trigRT.anchoredPosition = trigStart; trigRT.localScale = Vector3.one * 0.12f;
            trigFull = CineScaleFor(trigRT, cardW);
        }

        // Entran + fondo se oscurece.
        const float inDur = 0.5f;
        for (float e = 0f; e < inDur; e += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, e / inDur);
            bg.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.92f, k));
            trapRT.anchoredPosition = Vector2.LerpUnclamped(trapStart, trapPos, k);
            trapRT.localScale = Vector3.one * Mathf.Lerp(0.12f, trapFull, k);
            if (hasTrigger)
            {
                trigRT.anchoredPosition = Vector2.LerpUnclamped(trigStart, trigPos, k);
                trigRT.localScale = Vector3.one * Mathf.Lerp(0.12f, trigFull, k);
            }
            yield return null;
        }
        bg.color = new Color(0f, 0f, 0f, 0.92f);
        trapRT.anchoredPosition = trapPos; trapRT.localScale = Vector3.one * trapFull;
        if (hasTrigger) { trigRT.anchoredPosition = trigPos; trigRT.localScale = Vector3.one * trigFull; }
        yield return new WaitForSeconds(0.12f);

        // Fulgor rosa de activación sobre la trampa.
        yield return TrapActivateFlash(root, trapRT);

        // Si destruye al monstruo: corte + arde en fuego naranja.
        if (hasTrigger && destroysTrigger)
        {
            yield return SlashOver(root, trigRT);
            yield return FireConsume(root, trigRT);   // el atacante arde (naranja) y se destruye
            trigRT = null;
        }

        // La trampa SIEMPRE se desvanece en FUEGO ROSA.
        yield return FireConsume(root, trapRT, PinkFire);

        // El monstruo, si sobrevive, vuelve a su casilla.
        if (hasTrigger && trigRT != null)
            yield return CineReturn(trigRT, ScreenToLocal(root, triggerScreen));

        const float outDur = 0.35f;
        for (float e = 0f; e < outDur; e += Time.deltaTime)
        { bg.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.92f, 0f, e / outDur)); yield return null; }
        Destroy(root.gameObject);
    }

    /// <summary>Fulgor ROSA de activación: resplandor radial + fogonazo de pantalla + latido.</summary>
    private IEnumerator TrapActivateFlash(RectTransform root, RectTransform card)
    {
        float cw = card.rect.width * card.localScale.x;
        Vector3 baseS = card.localScale;
        Color pink = new Color(1f, 0.35f, 0.78f);

        var glow = MakeGlow(root, WithA(pink, 0f));
        var grt = (RectTransform)glow.transform;
        grt.pivot = grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.sizeDelta = new Vector2(cw * 2.2f, cw * 2.2f);
        grt.anchoredPosition = card.anchoredPosition;

        var screenFlash = MakeSolid(root, WithA(pink, 0f));
        StretchFull((RectTransform)screenFlash.transform);

        const float dur = 0.42f;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur;
            float p = Mathf.Sin(k * Mathf.PI);
            glow.color = WithA(pink, p);
            grt.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.4f, k);
            screenFlash.color = WithA(pink, p * 0.4f);
            card.localScale = baseS * (1f + 0.06f * p);
            yield return null;
        }
        card.localScale = baseS;
        Destroy(glow.gameObject); Destroy(screenFlash.gameObject);
    }

    // ── Helpers de la cinemática ──
    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static Image MakeSolid(Transform parent, Color color)
    {
        var go = new GameObject("Solid", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color; img.raycastTarget = false;
        return img;
    }

    /// <summary>Imagen con sprite RADIAL (brillo suave) — para destellos, llamas y boost.</summary>
    private static Image MakeGlow(Transform parent, Color color)
    {
        var img = MakeSolid(parent, color);
        img.sprite = CineGlowSprite();
        return img;
    }

    private static Sprite _cineGlow;
    private static Sprite CineGlowSprite()
    {
        if (_cineGlow != null) return _cineGlow;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a;   // núcleo brillante con halo suave
                px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        _cineGlow = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 64f);
        return _cineGlow;
    }

    private RectTransform BuildCineCard(Transform parent, CardData card, float targetWidth)
    {
        var go = Instantiate(handTemplate.gameObject, parent);
        go.SetActive(true);
        var view = go.GetComponent<DuelHandCardView>();
        view.Setup(card);
        view.SetFace(false);   // los combatientes ya están revelados: boca arriba
        if (view.Button != null) view.Button.interactable = false;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    /// <summary>Escala local para que la carta mida <paramref name="targetWidth"/> de ancho.</summary>
    private static float CineScaleFor(RectTransform rt, float targetWidth)
    {
        float native = rt.rect.width;
        if (native < 1f) native = ((RectTransform)rt).sizeDelta.x;
        return native > 1f ? targetWidth / native : 1f;
    }

    private static Vector2 ScreenToLocal(RectTransform root, Vector2 screenPt)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPt, null, out Vector2 local);
        return local;
    }

    /// <summary>Corte diagonal (hoja de luz que barre la carta) + destello radial +
    /// sacudida — el golpe.</summary>
    private IEnumerator SlashOver(RectTransform root, RectTransform card)
    {
        Vector2 c = card.anchoredPosition;
        float cw = card.rect.width * card.localScale.x, ch = card.rect.height * card.localScale.y;
        Vector3 baseS = card.localScale;

        // Hoja de luz (fina, con núcleo radial estirado) que cruza en diagonal.
        var slash = MakeGlow(root, new Color(1f, 1f, 1f, 0f));
        var srt = (RectTransform)slash.transform;
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(cw * 2.0f, Mathf.Max(16f, ch * 0.14f));
        srt.localRotation = Quaternion.Euler(0, 0, -38f);

        // Destello radial en el punto de impacto.
        var burst = MakeGlow(root, new Color(1f, 0.98f, 0.85f, 0f));
        var brt = (RectTransform)burst.transform;
        brt.pivot = brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(cw * 1.6f, cw * 1.6f);
        brt.anchoredPosition = c;

        // Fogonazo breve de TODA la pantalla al impactar.
        var screenFlash = MakeSolid(root, new Color(1f, 1f, 1f, 0f));
        StretchFull((RectTransform)screenFlash.transform);

        const float dur = 0.34f;
        Vector2 from = c + new Vector2(-cw * 0.95f, ch * 0.95f);
        Vector2 to   = c + new Vector2( cw * 0.95f, -ch * 0.95f);
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur;
            srt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            slash.color = new Color(1f, 1f, 1f, Mathf.Sin(Mathf.Clamp01(k / 0.45f) * Mathf.PI));
            float bk = Mathf.Sin(k * Mathf.PI);
            burst.color = new Color(1f, 0.98f, 0.85f, bk);
            brt.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.6f, k);          // estallido grande
            // fogonazo de pantalla, fuerte al cruzar la hoja y baja rápido
            float sf = Mathf.Clamp01(1f - Mathf.Abs(k - 0.4f) / 0.4f);
            screenFlash.color = new Color(1f, 1f, 1f, sf * 0.55f);
            // sacudida más fuerte de la carta golpeada
            card.anchoredPosition = c + (Vector2)(UnityEngine.Random.insideUnitCircle * (cw * 0.06f) * bk);
            yield return null;
        }
        card.anchoredPosition = c; card.localScale = baseS;
        Destroy(slash.gameObject); Destroy(burst.gameObject); Destroy(screenFlash.gameObject);
    }

    /// <summary>Solo destello radial sobre la carta (sin corte).</summary>
    private IEnumerator FlashOver(RectTransform root, RectTransform card)
    {
        float cw = card.rect.width * card.localScale.x;
        var flash = MakeGlow(root, new Color(1f, 1f, 1f, 0f));
        var frt = (RectTransform)flash.transform;
        frt.pivot = frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.sizeDelta = new Vector2(cw * 1.5f, cw * 1.5f);
        frt.anchoredPosition = card.anchoredPosition;
        const float dur = 0.24f;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur;
            flash.color = new Color(1f, 1f, 1f, Mathf.Sin(k * Mathf.PI) * 0.9f);
            frt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.5f, k);
            yield return null;
        }
        Destroy(flash.gameObject);
    }

    /// <summary>Boost de Estrella Guardiana: un brillo dorado baja a la carta y el ATK
    /// DE LA PROPIA CARTA (no una etiqueta aparte) sube de <paramref name="atk"/> a
    /// atk+boost, con un "+boost ★" que destella y se desvanece.</summary>
    private IEnumerator StarBoostFx(RectTransform root, RectTransform card, int atk, int boost)
    {
        float cw = card.rect.width * card.localScale.x, ch = card.rect.height * card.localScale.y;
        Vector2 c = card.anchoredPosition;
        Vector3 baseS = card.localScale;

        var view = card.GetComponent<DuelHandCardView>();
        int def = (view != null && view.Display != null) ? view.Display.GetCurrentDef() : 0;

        var glow = MakeGlow(root, new Color(1f, 0.85f, 0.3f, 0f));
        var grt = (RectTransform)glow.transform;
        grt.pivot = grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.sizeDelta = new Vector2(cw * 2.2f, cw * 2.2f);
        Vector2 gFrom = c + new Vector2(0f, ch * 0.95f);
        grt.anchoredPosition = gFrom;

        // Etiqueta pequeña "+boost ★" (solo el incremento; el total va en la carta).
        var numGO = new GameObject("BoostNum", typeof(RectTransform));
        numGO.transform.SetParent(root, false);
        var nrt = (RectTransform)numGO.transform;
        nrt.pivot = nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
        nrt.sizeDelta = new Vector2(cw * 1.3f, 200f);
        nrt.anchoredPosition = c + new Vector2(0f, ch * 0.62f);
        var num = numGO.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) num.font = TMP_Settings.defaultFontAsset;
        num.fontSize = 90; num.fontStyle = FontStyles.Bold;
        num.alignment = TextAlignmentOptions.Center;
        num.color = new Color(1f, 0.86f, 0.4f); num.raycastTarget = false;
        num.text = $"+{boost} ★";

        // 1) el brillo baja a la carta
        const float inDur = 0.35f;
        for (float e = 0f; e < inDur; e += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, e / inDur);
            grt.anchoredPosition = Vector2.LerpUnclamped(gFrom, c, k);
            glow.color = new Color(1f, 0.85f, 0.3f, Mathf.Lerp(0f, 0.9f, k));
            grt.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.7f, k);
            yield return null;
        }
        // 2) impacto: el ATK DE LA CARTA sube de atk a atk+boost; la carta late.
        const float upDur = 0.6f;
        for (float e = 0f; e < upDur; e += Time.deltaTime)
        {
            float k = e / upDur;
            int val = Mathf.RoundToInt(Mathf.Lerp(atk, atk + boost, k));
            if (view != null) view.SetCurrentStats(val, def);
            glow.color = new Color(1f, 0.85f, 0.3f, Mathf.Lerp(0.9f, 0f, k));
            grt.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.6f, k);
            card.localScale = baseS * (1f + 0.06f * Mathf.Sin(k * Mathf.PI));
            nrt.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, Mathf.Clamp01(k / 0.3f));
            yield return null;
        }
        if (view != null) view.SetCurrentStats(atk + boost, def);
        card.localScale = baseS;
        yield return new WaitForSeconds(0.3f);
        const float outDur = 0.3f;
        for (float e = 0f; e < outDur; e += Time.deltaTime)
        { var col = num.color; col.a = 1f - e / outDur; num.color = col; yield return null; }
        Destroy(glow.gameObject); Destroy(numGO);
    }

    /// <summary>LP perdidos que emergen del brillo, suben y se desvanecen.</summary>
    private IEnumerator ShowCineLP(RectTransform root, int amount, Vector2 localPos)
    {
        var go = new GameObject("CineLP", typeof(RectTransform));
        go.transform.SetParent(root, false);
        var rt = (RectTransform)go.transform;
        rt.pivot = rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(600, 200);
        rt.anchoredPosition = localPos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = $"-{amount} LP";
        tmp.fontSize = 120; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.35f, 0.3f);
        tmp.raycastTarget = false;

        const float dur = 0.9f;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur;
            rt.anchoredPosition = localPos + new Vector2(0f, Mathf.Lerp(0f, 140f, k));
            float a = k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f;
            var col = tmp.color; col.a = Mathf.Clamp01(a); tmp.color = col;
            rt.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, Mathf.Clamp01(k / 0.2f));
            yield return null;
        }
        Destroy(go);
    }

    /// <summary>Paleta de un fuego (llamas, resplandor, ignición, brasas).</summary>
    private struct FirePalette { public Color[] flames; public Color glow, ignite, ember; }

    private static readonly FirePalette OrangeFire = new FirePalette
    {
        flames = new[] { new Color(1f, 0.9f, 0.4f), new Color(1f, 0.6f, 0.15f), new Color(1f, 0.32f, 0.08f) },
        glow = new Color(1f, 0.5f, 0.15f), ignite = new Color(1f, 0.85f, 0.4f), ember = new Color(1f, 0.75f, 0.3f),
    };

    /// <summary>Fuego ROSA/mágico — para desvanecer una trampa al activarse.</summary>
    private static readonly FirePalette PinkFire = new FirePalette
    {
        flames = new[] { new Color(1f, 0.6f, 0.9f), new Color(1f, 0.3f, 0.72f), new Color(0.86f, 0.12f, 0.55f) },
        glow = new Color(1f, 0.3f, 0.72f), ignite = new Color(1f, 0.72f, 0.95f), ember = new Color(1f, 0.5f, 0.85f),
    };

    private static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, a);

    /// <summary>Fuego que consume la carta: llamas radiales suben y titilan, un resplandor
    /// palpita y la carta se ennegrece y desvanece. La paleta define el color (naranja
    /// por defecto; rosa para las trampas).</summary>
    private IEnumerator FireConsume(RectTransform root, RectTransform card, FirePalette? palette = null)
    {
        var pal = palette ?? OrangeFire;
        float cw = card.rect.width * card.localScale.x, ch = card.rect.height * card.localScale.y;
        Vector2 c = card.anchoredPosition;

        var cg = card.GetComponent<CanvasGroup>();
        if (cg == null) cg = card.gameObject.AddComponent<CanvasGroup>();

        // Resplandor radial GRANDE detrás/alrededor de la carta.
        var glow = MakeGlow(root, WithA(pal.glow, 0f));
        var grt = (RectTransform)glow.transform;
        grt.pivot = grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.sizeDelta = new Vector2(cw * 3.4f, ch * 3.4f);
        grt.anchoredPosition = c;

        // Fogonazo inicial de ignición.
        var ignite = MakeGlow(root, WithA(pal.ignite, 0f));
        var irt = (RectTransform)ignite.transform;
        irt.pivot = irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(cw * 2.6f, cw * 2.6f);
        irt.anchoredPosition = c;

        // Capa oscura que "carboniza" la carta (sube de abajo hacia arriba).
        var char_ = MakeSolid(root, new Color(0.04f, 0.02f, 0.01f, 0f));
        var crt = (RectTransform)char_.transform;
        crt.pivot = new Vector2(0.5f, 0f);
        crt.sizeDelta = new Vector2(cw, ch);
        crt.anchoredPosition = c + new Vector2(0f, -ch * 0.5f);

        // Llamas: MUCHOS blobs radiales grandes que suben alto, se estrechan y titilan.
        const int N = 30;
        var fl = new List<RectTransform>();
        var flCol = new List<Color>();
        var flSpd = new List<float>();
        var flPh = new List<float>();
        var palFlames = pal.flames;
        for (int i = 0; i < N; i++)
        {
            var f = MakeGlow(root, Color.white);
            var frt = (RectTransform)f.transform;
            frt.pivot = new Vector2(0.5f, 0.5f);
            float s = cw * UnityEngine.Random.Range(0.28f, 0.6f);
            frt.sizeDelta = new Vector2(s, s);
            frt.anchoredPosition = c + new Vector2(UnityEngine.Random.Range(-cw * 0.5f, cw * 0.5f),
                                                   -ch * 0.5f + UnityEngine.Random.Range(-ch * 0.12f, ch * 0.15f));
            var col = palFlames[UnityEngine.Random.Range(0, palFlames.Length)];
            f.color = col; fl.Add(frt); flCol.Add(col);
            flSpd.Add(UnityEngine.Random.Range(1.4f, 2.6f));   // suben más rápido/alto
            flPh.Add(UnityEngine.Random.Range(0f, 6.28f));
        }

        // Brasas: puntitos que salen disparados hacia arriba y a los lados.
        const int Emb = 18;
        var emb = new List<RectTransform>();
        var embVel = new List<Vector2>();
        for (int i = 0; i < Emb; i++)
        {
            var d = MakeGlow(root, WithA(pal.ember, 1f));
            var drt = (RectTransform)d.transform;
            drt.pivot = new Vector2(0.5f, 0.5f);
            float s = cw * UnityEngine.Random.Range(0.03f, 0.07f);
            drt.sizeDelta = new Vector2(s, s);
            drt.anchoredPosition = c + new Vector2(UnityEngine.Random.Range(-cw * 0.4f, cw * 0.4f), -ch * 0.4f);
            emb.Add(drt);
            embVel.Add(new Vector2(UnityEngine.Random.Range(-cw * 0.5f, cw * 0.5f), ch * UnityEngine.Random.Range(1.8f, 3.2f)));
        }

        const float dur = 1.15f;
        Vector2 cardBottom = c + new Vector2(0f, -ch * 0.5f);
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur;
            // FRENTE de quemado que sube de abajo hacia arriba (0→1); la carta desaparece
            // conforme el frente avanza (no un fade uniforme).
            float front = Mathf.Clamp01(k / 0.8f);
            cg.alpha = 1f - front;
            char_.color = new Color(0.04f, 0.02f, 0.01f, 0.97f);
            crt.sizeDelta = new Vector2(cw * 1.02f, front * ch);   // el carbón cubre hasta el frente
            glow.color = WithA(pal.glow, Mathf.Sin(k * Mathf.PI) * 1f);
            ignite.color = WithA(pal.ignite, Mathf.Clamp01(1f - k / 0.22f) * 0.95f);
            irt.localScale = Vector3.one * Mathf.Lerp(0.6f, 2f, Mathf.Clamp01(k / 0.22f));

            float frontY = cardBottom.y + front * ch;   // altura del frente de fuego
            for (int i = 0; i < fl.Count; i++)
            {
                float flick = 0.7f + 0.3f * Mathf.Sin(e * 22f + flPh[i]);
                // Las llamas se concentran EN el frente de quemado y suben desde ahí.
                var p = fl[i].anchoredPosition;
                p.x += Mathf.Sin(e * 10f + flPh[i]) * cw * 0.2f * Time.deltaTime;
                p.y += ch * flSpd[i] * Time.deltaTime;
                // atrae la base de la llama hacia el frente (para que "coma" la carta)
                float targetY = frontY + UnityEngine.Random.Range(0f, ch * 0.25f);
                p.y = Mathf.Lerp(p.y, targetY, 0.04f);
                fl[i].anchoredPosition = p;
                float sc = Mathf.Lerp(1.5f, 0.35f, k) * flick;
                fl[i].localScale = new Vector3(sc * 0.9f, sc * 1.6f, 1f);   // llama alargada
                var col = flCol[i]; col.a = Mathf.Clamp01(1f - k * 0.9f) * flick; fl[i].GetComponent<Image>().color = col;
            }
            for (int i = 0; i < emb.Count; i++)
            {
                emb[i].anchoredPosition += embVel[i] * Time.deltaTime;
                embVel[i] += new Vector2(0f, -ch * 1.2f * Time.deltaTime);   // gravedad leve
                var im = emb[i].GetComponent<Image>();
                var col = im.color; col.a = Mathf.Clamp01(1f - k); im.color = col;
            }
            yield return null;
        }
        foreach (var f in fl) if (f != null) Destroy(f.gameObject);
        foreach (var d in emb) if (d != null) Destroy(d.gameObject);
        if (glow != null) Destroy(glow.gameObject);
        if (ignite != null) Destroy(ignite.gameObject);
        if (char_ != null) Destroy(char_.gameObject);
        if (card != null) Destroy(card.gameObject);
    }

    /// <summary>La superviviente vuelve (encogiendo) a su posición del campo.</summary>
    private IEnumerator CineReturn(RectTransform card, Vector2 fieldLocal)
    {
        Vector2 from = card.anchoredPosition;
        Vector3 fromS = card.localScale;
        const float dur = 0.4f;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, e / dur);
            card.anchoredPosition = Vector2.LerpUnclamped(from, fieldLocal, k);
            card.localScale = Vector3.Lerp(fromS, Vector3.one * 0.12f, k);
            yield return null;
        }
        if (card != null) Destroy(card.gameObject);
    }

    public void SetHandHighlight(int index, bool on)
    {
        if (index >= 0 && index < _handViews.Count && _handViews[index] != null)
            _handViews[index].SetHighlight(on);
    }

    public void ClearHandHighlights()
    {
        foreach (var v in _handViews)
            if (v != null) v.SetHighlight(false);
    }

    // ── Paneles contextuales ─────────────────────────────────────────────

    public void ShowActionPanel(string title, bool canSummon, bool canCast, bool canSetTrap)
    {
        if (actionPanel != null) actionPanel.SetActive(true);
        if (actionTitleText != null) actionTitleText.text = title;
        SetActive(btnSummonAtk, canSummon);
        SetActive(btnSummonDef, canSummon);
        SetActive(btnSetAtk, canSummon);
        SetActive(btnSetDef, canSummon);
        SetActive(btnCastSpell, canCast);
        SetActive(btnSetTrap, canSetTrap);
    }

    public void HideActionPanel()
    {
        if (actionPanel != null) actionPanel.SetActive(false);
    }

    /// <summary>
    /// Panel de Estrella Guardiana: muestra las dos estrellas de la carta en
    /// los botones A/B. El controlador escucha BtnStarA/BtnStarB.
    /// </summary>
    public void ShowStarPanel(CardData card)
    {
        if (starPanel != null)
        {
            starPanel.SetActive(true);
            // Debajo de la carta alzada: la carta ocupa el centro-arriba y el
            // panel de estrella queda en la franja inferior.
            var rt = (RectTransform)starPanel.transform;
            rt.anchoredPosition = new Vector2(0f, -300f);
        }
        if (starTitleText != null) starTitleText.text = $"Estrella Guardiana de\n{card.cardName}";
        SetButtonLabel(btnStarA, $"★ {card.starA}");
        SetButtonLabel(btnStarB, $"★ {card.starB}");
    }

    public void HideStarPanel()
    {
        if (starPanel != null) starPanel.SetActive(false);
    }

    public void ShowFieldPanel(string title, bool canChangePosition, bool canReveal)
    {
        if (fieldPanel != null) fieldPanel.SetActive(true);
        if (fieldTitleText != null) fieldTitleText.text = title;
        SetActive(btnChangePosition, canChangePosition);
        SetActive(btnReveal, canReveal);
    }

    public void HideFieldPanel()
    {
        if (fieldPanel != null) fieldPanel.SetActive(false);
    }

    // ── Grupos de botones de fase ────────────────────────────────────────

    public void ShowMainButtons(bool show, bool fusionMode = false)
    {
        if (mainButtons != null) mainButtons.SetActive(show);
        SetActive(btnConfirmFusion, show && fusionMode);
        SetActive(btnFuse, show && !fusionMode);
        if (btnGoBattle != null) btnGoBattle.interactable = !fusionMode;
        if (btnEndTurn != null) btnEndTurn.interactable = !fusionMode;
    }

    public void ShowBattleButtons(bool show, bool directAttackEnabled = false)
    {
        if (battleButtons != null) battleButtons.SetActive(show);
        if (btnDirectAttack != null) btnDirectAttack.interactable = directAttackEnabled;
    }

    // ── Presentación ─────────────────────────────────────────────────────

    public void ShowIntro(string opponentName, Sprite portrait)
    {
        if (introPanel != null) introPanel.SetActive(true);
        if (introNameText != null) introNameText.text = opponentName;
        if (introPortrait != null)
        {
            introPortrait.sprite = portrait;
            introPortrait.enabled = portrait != null;
        }
    }

    public void HideIntro()
    {
        if (introPanel != null) introPanel.SetActive(false);
    }

    // ── Velo negro de entrada (pantalla TOTALMENTE negra → se disuelve) ───

    private CanvasGroup _blackoutCg;

    /// <summary>Cubre toda la pantalla de negro (o lo quita). Se crea al vuelo.</summary>
    public void SetBlackout(bool on)
    {
        EnsureBlackout();
        _blackoutCg.gameObject.SetActive(true);
        _blackoutCg.alpha = on ? 1f : 0f;
        if (!on) _blackoutCg.gameObject.SetActive(false);
    }

    /// <summary>Disuelve el velo negro de 1 a 0 (revela el tablero poco a poco).</summary>
    public IEnumerator FadeFromBlack(float duration)
    {
        EnsureBlackout();
        _blackoutCg.gameObject.SetActive(true);
        _blackoutCg.alpha = 1f;
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            _blackoutCg.alpha = 1f - (e / duration);
            yield return null;
        }
        _blackoutCg.alpha = 0f;
        _blackoutCg.gameObject.SetActive(false);
    }

    /// <summary>
    /// Crea (una vez) una imagen negra a pantalla completa. Va como PRIMER hijo
    /// del canvas: tapa el mundo 3D pero deja el HUD por encima, para que los
    /// datos del rival/CAMPO/LP puedan aparecer mientras la escena sigue negra.
    /// </summary>
    private void EnsureBlackout()
    {
        if (_blackoutCg != null) return;
        var go = new GameObject("IntroBlackout", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.transform.SetAsFirstSibling();                // bajo el HUD, sobre el 3D
        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        _blackoutCg = go.GetComponent<CanvasGroup>();
    }

    // ── Presentación: HUD con desvanecido + contador de LP ───────────────

    private List<CanvasGroup> _introHudGroups;

    /// <summary>
    /// Prepara el HUD para la presentación: los datos del rival, la caja de
    /// CAMPO, la caja de LP y el log quedan invisibles (alpha 0) y los LP en 0,
    /// listos para <see cref="FadeInHud"/> + <see cref="AnimateLPCountUp"/>.
    /// </summary>
    public void PrepareIntroHud()
    {
        _introHudGroups = new List<CanvasGroup>();

        void Add(Component c)
        {
            if (c == null) return;
            var cg = c.gameObject.GetComponent<CanvasGroup>();
            if (cg == null) cg = c.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            _introHudGroups.Add(cg);
        }

        // Cajas con borde (el padre del relleno donde viven los textos).
        if (terrainText != null) Add(terrainText.transform.parent.parent);   // caja CAMPO
        if (playerLPText != null) Add(playerLPText.transform.parent.parent); // caja LP
        Add(opponentNameText);
        Add(phaseText);
        Add(turnText);
        if (logText != null) Add(logText.transform.parent);                  // panel de log

        UpdateLP(0, 0);
        HideCardInfo();
    }

    /// <summary>Los datos del rival/CAMPO/LP aparecen con un desvanecido suave.</summary>
    public IEnumerator FadeInHud(float duration)
    {
        if (_introHudGroups == null) yield break;
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            float k = e / duration; k = k * k * (3f - 2f * k); // smoothstep
            foreach (var g in _introHudGroups) if (g != null) g.alpha = k;
            yield return null;
        }
        foreach (var g in _introHudGroups) if (g != null) g.alpha = 1f;
    }

    /// <summary>Contador de LP: ambos marcadores suben de 0 hasta su valor (estilo FM).</summary>
    public IEnumerator AnimateLPCountUp(int playerTarget, int opponentTarget, float duration)
    {
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            float k = e / duration;   // lineal, como la ruleta de FM
            UpdateLP(Mathf.RoundToInt(playerTarget * k), Mathf.RoundToInt(opponentTarget * k));
            yield return null;
        }
        UpdateLP(playerTarget, opponentTarget);
    }

    // ── Resultado ────────────────────────────────────────────────────────

    /// <summary>
    /// Banner animado de victoria/derrota: el texto aparece enorme y cae a su
    /// tamaño con un pulso. Se espera con yield antes de mostrar estadísticas.
    /// </summary>
    public IEnumerator PlayResultBanner(bool win)
    {
        if (resultBanner == null || resultBannerText == null) yield break;

        resultBanner.SetActive(true);
        resultBannerText.text = win ? "¡VICTORIA!" : "DERROTA…";
        resultBannerText.color = win ? new Color(0.98f, 0.85f, 0.45f) : new Color(0.75f, 0.30f, 0.32f);

        var rt = resultBannerText.rectTransform;
        const float dur = 0.7f;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float k = e / dur;
            float s = Mathf.LerpUnclamped(3.2f, 1f, 1f - (1f - k) * (1f - k)); // ease-out
            rt.localScale = new Vector3(s, s, 1f);
            resultBannerText.alpha = k;
            yield return null;
        }
        rt.localScale = Vector3.one;
        resultBannerText.alpha = 1f;

        yield return new WaitForSeconds(1.1f);
        resultBanner.SetActive(false);
    }

    /// <summary>Caja final: título, estadísticas del duelo y botones.</summary>
    public void ShowResultPanel(string title, string stats, bool allowRematch)
    {
        if (resultPanel != null) resultPanel.SetActive(true);
        if (resultTitleText != null) resultTitleText.text = title;
        if (statsText != null) statsText.text = stats;
        if (rankText != null) rankText.text = "";
        if (rewardGroup != null) rewardGroup.SetActive(false);
        SetActive(btnRematch, allowRematch);
    }

    public void ShowRank(DuelRank rank, int score)
    {
        if (rankText != null) rankText.text = $"Rango: {rank}    Puntuación: {score}";
    }

    public void ShowReward(CardData reward)
    {
        if (rewardGroup != null) rewardGroup.SetActive(true);
        if (rewardArt != null)
        {
            rewardArt.sprite = reward != null ? reward.artwork : null;
            rewardArt.enabled = reward != null && reward.artwork != null;
        }
        if (rewardNameText != null)
            rewardNameText.text = reward != null ? $"¡Obtuviste: {reward.cardName}!" : "Esta vez no hubo drop.";
    }

    // ── Utilidad ─────────────────────────────────────────────────────────

    private static void SetActive(Button b, bool on)
    {
        if (b != null) b.gameObject.SetActive(on);
    }

    private static void SetButtonLabel(Button b, string text)
    {
        if (b == null) return;
        var label = b.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = text;
    }
}
