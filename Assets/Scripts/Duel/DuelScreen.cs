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
        if (_handCursorPulse == null) _handCursorPulse = StartCoroutine(PulseHandCursor());
    }

    public void HideHandCursor()
    {
        if (_handCursorRT != null) _handCursorRT.gameObject.SetActive(false);
        if (_handCursorPulse != null) { StopCoroutine(_handCursorPulse); _handCursorPulse = null; }
    }

    private void EnsureHandCursor()
    {
        if (_handCursorRT != null || handContainer == null) return;
        var go = new GameObject("HandCursor", typeof(RectTransform));
        go.transform.SetParent(handContainer, false);
        _handCursorRT = (RectTransform)go.transform;
        _handCursorRT.anchorMin = _handCursorRT.anchorMax = _handCursorRT.pivot = new Vector2(0.5f, 0f);
        _handCursorRT.sizeDelta = new Vector2(64, 64);
        _handCursorRT.localRotation = Quaternion.Euler(0f, 0f, -45f); // apunta ↗ a la carta

        // Punta de flecha: dos barras doradas en "∧".
        MakeCursorBar(-13f, 45f);
        MakeCursorBar(13f, -45f);
    }

    private void MakeCursorBar(float x, float angle)
    {
        var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(_handCursorRT, false);
        var rt = (RectTransform)bar.transform;
        rt.sizeDelta = new Vector2(13f, 46f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        var img = bar.GetComponent<Image>();
        img.color = new Color(0.98f, 0.85f, 0.45f);
        img.raycastTarget = false;
    }

    private IEnumerator PulseHandCursor()
    {
        while (_handCursorRT != null)
        {
            float k = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
            _handCursorRT.localScale = Vector3.one * (1f + 0.14f * k);
            yield return null;
        }
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
