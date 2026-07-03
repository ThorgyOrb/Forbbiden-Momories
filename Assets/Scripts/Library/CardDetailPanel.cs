using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal de detalle de carta con la secuencia de animación estilo Forbidden
/// Memories: la carta "salta" fuera de su icono en el grid, vuela en diagonal
/// creciendo y rotando, se acomoda en su posición final, y revela su frente
/// a mitad de un giro real en el eje Y (no Z), mientras el panel de info se
/// va llenando progresivamente.
///
/// Fases (ver referencia visual):
///  1. Reposo - no hay nada que animar aquí, ocurre antes de Show().
///  1b. Mini-flip de salida: la carta, que en el grid se ve de frente, hace
///      un giro rápido frente→reverso justo antes de despegar - así no
///      "aparece de la nada" boca abajo.
///  2. Despegue: la carta sale del slot, diagonal arriba-derecha, leve giro Z.
///  3. Acercamiento: crece más, el giro Z se endereza, empieza el giro Y.
///  4. Giro: gira 0°→90° en Y mostrando el reverso, se acomoda en posición.
///  5-6. Colocación: con el reverso visible, se llenan Tipo y Guardian Star.
///  7. Revelado: salto a -90°, gira -90°→0° mostrando el frente, se llenan
///     Imagen, ATK, DEF y Descripción.
///  (cierre, simétrico) ... 7'. Mini-flip de entrada: justo antes de
///      desvanecerse en el slot, gira reverso→frente para coincidir con la
///      carta real del grid que queda debajo (evita el "flash" al desaparecer).
/// </summary>
public class CardDetailPanel : MonoBehaviour
{
    [Header("Raíz del modal")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private CanvasGroup backdropGroup;
    [SerializeField] private RectTransform flyingCard;
    [SerializeField] private Image flyingCardImage;
    [SerializeField] private RectTransform infoPanel;

    [Header("Placeholder sobre el slot original")]
    [SerializeField] private RectTransform cardPlaceholder; // Image negra, mismo tamaño que el slot

    [Header("Sprite genérico de reverso (para el vuelo, antes de mostrar el frente)")]
    [SerializeField] private Sprite genericCardBack;

    [Header("Contenido final (texto)")]
    [SerializeField] private Image cardArt;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text idText;
    [SerializeField] private TMP_Text atkText;
    [SerializeField] private TMP_Text defText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text attributeText;
    [SerializeField] private TMP_Text copiesText;
    [SerializeField] private TMP_Text sourcesText;

    [Header("Grupos que se revelan progresivamente")]
    [SerializeField] private GameObject typeAndGuardianGroup; // typeText + guardian stars
    [SerializeField] private GameObject statsAndArtGroup;     // cardArt + atk/def/copies/sources

    [Header("Cierre")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backdropButton;

    [Header("Visor 3D")]
    [SerializeField] private Button view3DButton;
    [SerializeField] private Model3DViewer model3DViewer;

    [Header("Timings (segundos)")]
    [SerializeField] private float introFlipDuration = 0.12f; // Fase 1b - mini-flip frente->reverso antes de despegar
    [SerializeField] private float introVerticalOffset = 40f; // corrige el desfase visual al fijar la altura en 190
    [SerializeField] private float takeoffDuration = 0.40f;   // Fase 2
    [SerializeField] private float approachDuration = 0.28f;  // Fase 3 + mitad de Fase 4 (back)
    [SerializeField] private float revealDuration = 0.50f;    // resto de Fase 4 (front) + Fase 7
    [SerializeField] private float closeDuration = 0.16f;
    [SerializeField] private float outroFlipDuration = 0.12f; // Fase 7' - mini-flip reverso->frente al aterrizar
    [SerializeField] private float infoPanelSlideDuration = 0.40f; // = takeoff + approach aprox.

    [Header("Valores visuales")]
    [SerializeField] private Vector2 finalCardSize = new Vector2(320, 460);
    [SerializeField] private float overshootScale = 1.6f;    // "explosión hacia cámara" de la Fase 3
    [SerializeField] private float takeoffGrowth = 1.7f;      // crecimiento durante el despegue (Fase 2)
    [SerializeField] private float maxTilt = 35f;             // inclinación Z de la Fase 2
    [SerializeField] private float infoPanelOffscreenOffset = 1920f; // cuánto entra desde la derecha

    [Header("Posición final de la carta")]
    [SerializeField] private Vector2 cardFinalPosition = new Vector2(-600f, 2f); // dónde queda la carta dentro del modal al terminar de abrir

    private Coroutine _routine;
    private Coroutine _slideRoutine;
    private RectTransform _modalRootRect;
    private Canvas _modalCanvas;
    private CanvasGroup _statsAndArtCanvasGroup; // fade-in suave para cardArt y stats
    private Vector2 _infoPanelRestPos;
    private LibraryEntry _lastEntry;
    private RectTransform _lastSourceRect;
    private Vector2 _lastStartPos;
    private Vector2 _lastStartSize;
    private Vector2 _lastFinalPos;

    void Awake()
    {
        _modalRootRect = root.GetComponent<RectTransform>();
        _modalCanvas = root.GetComponentInParent<Canvas>();
        if (_modalCanvas != null) _modalCanvas = _modalCanvas.rootCanvas;
        _infoPanelRestPos = infoPanel.anchoredPosition; // posición diseñada en el editor = destino del slide

        // statsAndArtGroup necesita un CanvasGroup para poder hacer fade-in
        // en vez de aparecer de golpe con SetActive a mitad del giro.
        if (statsAndArtGroup != null)
        {
            _statsAndArtCanvasGroup = statsAndArtGroup.GetComponent<CanvasGroup>();
            if (_statsAndArtCanvasGroup == null)
                _statsAndArtCanvasGroup = statsAndArtGroup.AddComponent<CanvasGroup>();
        }

        // Arranca oculto vía CanvasGroup, NO vía SetActive(false) - así el
        // GameObject sigue activeInHierarchy y los coroutines pueden correr
        // sin problema la primera vez que se llama Show().
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        ResetVisualState();

        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (backdropButton != null) backdropButton.onClick.AddListener(Hide);

        if (view3DButton != null) view3DButton.onClick.AddListener(OnView3DClicked);
    }

    /// <summary>
    /// Abre el visor 3D con el modelo del monstruo actual. No cierra este modal -
    /// el visor se dibuja encima, igual que en FM cuando entrabas a inspeccionar
    /// la carta desde la Library.
    /// </summary>
    private void OnView3DClicked()
    {
        if (_lastEntry == null || _lastEntry.card == null) return;
        if (model3DViewer == null)
        {
            Debug.LogWarning("CardDetailPanel: 'model3DViewer' no está asignado en el Inspector.");
            return;
        }
        model3DViewer.Show(_lastEntry.card);
    }

    public void Show(LibraryEntry entry, RectTransform sourceRect)
    {
        if (entry.state == CardState.Locked) { Hide(); return; }
        if (root == null) { Debug.LogWarning("CardDetailPanel: 'root' no asignado."); return; }

        if (_routine != null) StopCoroutine(_routine);

        _lastEntry = entry;
        _lastSourceRect = sourceRect;

        // IMPORTANTE: estos valores se calculan AQUÍ, de forma síncrona,
        // y no dentro de la corrutina PlaySequence. Si los calculáramos
        // dentro de la corrutina y Hide() llegara a ejecutarse antes de
        // que esa línea corriera (p.ej. clicks rápidos interrumpiendo la
        // corrutina anterior), PlayClose() usaría los valores de la carta
        // ANTERIOR para regresar la carta NUEVA, rompiendo la posición.
        _lastStartPos = WorldToModalLocal(sourceRect);
        _lastStartSize = sourceRect.rect.size;
        _lastFinalPos = cardFinalPosition;

        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = true;

        _routine = StartCoroutine(PlaySequence(entry, sourceRect));
    }

    public void Hide()
    {
        if (root == null || rootGroup.alpha <= 0f) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(PlayClose());
    }

    // ── Secuencia completa ───────────────────────────────────────────────

    /// <summary>
    /// Deja todo en el estado "reposo, oculto" - se llama tanto al INICIO de
    /// cada apertura (por si la animación anterior se interrumpió a medias)
    /// como al FINAL de cada cierre. Centralizar esto evita que rotación,
    /// tamaño, sprite o grupos visibles queden a medio camino entre una
    /// animación y la siguiente.
    /// </summary>
    private void ResetVisualState()
    {
        flyingCard.gameObject.SetActive(false);
        flyingCard.localRotation = Quaternion.identity;
        flyingCard.localScale = Vector3.one;
        flyingCard.sizeDelta = Vector2.zero;

        if (typeAndGuardianGroup != null) typeAndGuardianGroup.SetActive(false);

        // statsAndArtGroup se queda ACTIVO (para que su CanvasGroup pueda
        // animar), pero con alpha 0 e ignorando clicks/raycasts hasta que
        // se revele. Así evitamos el "pop" brusco y el frame con el sprite
        // viejo visible.
        if (statsAndArtGroup != null) statsAndArtGroup.SetActive(true);
        if (_statsAndArtCanvasGroup != null)
        {
            _statsAndArtCanvasGroup.alpha = 0f;
            _statsAndArtCanvasGroup.blocksRaycasts = false;
            _statsAndArtCanvasGroup.interactable = false;
        }

        // Limpia el sprite final para que nunca quede colgado el de la carta
        // anterior mientras statsAndArtGroup está oculto.
        if (cardArt != null) cardArt.sprite = null;

        if (cardPlaceholder != null) cardPlaceholder.gameObject.SetActive(false);

        infoPanel.anchoredPosition = _infoPanelRestPos + new Vector2(infoPanelOffscreenOffset, 0f);

        backdropGroup.alpha = 0f;
        backdropGroup.blocksRaycasts = false;
    }

    private IEnumerator PlaySequence(LibraryEntry entry, RectTransform sourceRect)
    {
        var card = entry.card;

        // Por si la animación anterior (apertura o cierre) quedó a medias.
        ResetVisualState();

        FillStaticText(entry);

        backdropGroup.alpha = 0f;
        backdropGroup.blocksRaycasts = true;

        // El panel de info arranca fuera de pantalla a la derecha (ya lo dejó
        // así ResetVisualState) y entra deslizándose mientras la carta vuela.
        if (_slideRoutine != null) StopCoroutine(_slideRoutine);
        _slideRoutine = StartCoroutine(SlideInfoPanel());

        // Usamos los valores ya calculados en Show() de forma síncrona,
        // NO los recalculamos ni reasignamos aquí.
        Vector2 startPos = _lastStartPos;
        Vector2 startSize = _lastStartSize;
        Vector2 midPos = startPos + new Vector2(Screen.width * 0.12f, Screen.height * 0.10f);
        Vector2 finalPos = _lastFinalPos;

        flyingCard.SetAsLastSibling();

        // introPos se declara aquí para que esté disponible tanto para el
        // placeholder como para la animación del flyingCard que viene abajo.
        Vector2 introPos = startPos + new Vector2(0f, introVerticalOffset);

        // Placeholder negro sobre el slot original: tapa la carta real del
        // grid mientras flyingCard vuela, para que no se vea raro tener dos
        // cartas en el mismo sitio. Se desactiva al terminar el cierre.
        if (cardPlaceholder != null)
        {
            cardPlaceholder.anchoredPosition = introPos;
            cardPlaceholder.sizeDelta = new Vector2(startSize.x, 190f);
            cardPlaceholder.gameObject.SetActive(true);
            // NO se mueve en la jerarquía — ya está como primer hijo de ModalRoot
            // (índice 0), por debajo de Backdrop, FlyingCard y DetailPanelBox.
        }

        // ── Fases 1b + 2 fusionadas: la carta sale moviéndose inmediatamente
        // mostrando el frente, y el giro (frente -> reverso) ocurre DURANTE
        // el trayecto hacia midPos - nunca se queda quieta para girar. ──
        flyingCard.gameObject.SetActive(true);
        flyingCardImage.sprite = card.artwork;
        flyingCard.localScale = Vector3.one;
        flyingCard.anchoredPosition = introPos;
        flyingCard.sizeDelta = new Vector2(startSize.x, 190f);
        flyingCard.localRotation = Quaternion.identity;

        const float flipInPortion = 0.55f; // el giro ocupa el primer 55% del trayecto
        bool swappedToBack = false;

        yield return Animate(takeoffDuration + introFlipDuration, p =>
        {
            // Posición y tamaño avanzan a lo largo de TODA la fase
            float ep = EaseOutQuad(p);
            flyingCard.anchoredPosition = Vector2.Lerp(introPos, midPos, ep);
            flyingCard.sizeDelta = Vector2.Lerp(
                new Vector2(startSize.x, 190f),
                startSize * takeoffGrowth, ep);

            backdropGroup.alpha = Mathf.Lerp(0f, 0.6f, p);

            // El giro frente->reverso ocurre en el primer 55% del trayecto
            if (p < flipInPortion)
            {
                float rp = p / flipInPortion;
                if (rp < 0.5f)
                {
                    float re = EaseInQuad(rp / 0.5f);
                    flyingCard.localRotation = Quaternion.Euler(0, Mathf.Lerp(0f, 90f, re), Mathf.Lerp(0f, maxTilt * 0.3f, ep));
                }
                else
                {
                    if (!swappedToBack)
                    {
                        swappedToBack = true;
                        flyingCardImage.sprite = genericCardBack;
                        flyingCard.localRotation = Quaternion.Euler(0, -90f, 0);
                    }
                    float re = EaseOutQuad((rp - 0.5f) / 0.5f);
                    flyingCard.localRotation = Quaternion.Euler(0, Mathf.Lerp(-90f, 0f, re), Mathf.Lerp(maxTilt * 0.3f, maxTilt, ep));
                }
            }
            else
            {
                // Giro completado, sólo avanza la inclinación Z
                flyingCard.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(maxTilt * 0.5f, maxTilt, ep));
            }
        });

        // Seguridad: si la fase terminó antes del salto (duración muy corta)
        if (!swappedToBack) flyingCardImage.sprite = genericCardBack;
        flyingCard.localRotation = Quaternion.Euler(0, 0, maxTilt);

        // ── Fase 3: acercamiento/"explosión" + empieza el giro Y, se endereza el tilt ──
        Vector2 approachStart = flyingCard.anchoredPosition;
        Vector2 approachSize = flyingCard.sizeDelta;
        yield return Animate(approachDuration, p =>
        {
            float e = EaseInOutQuad(p);
            flyingCard.anchoredPosition = Vector2.Lerp(approachStart, finalPos, e);
            float scaleT = Mathf.Sin(p * Mathf.PI); // crece y luego se asienta (overshoot)
            Vector2 size = Vector2.LerpUnclamped(approachSize, finalCardSize, e);
            flyingCard.sizeDelta = Vector2.LerpUnclamped(size, size * overshootScale, scaleT * 0.4f);
            float tilt = Mathf.Lerp(maxTilt, 0f, e);
            float yRot = Mathf.Lerp(0f, 90f, e); // 0 -> 90, mostrando el reverso (Fase 4, primera mitad)
            flyingCard.localRotation = Quaternion.Euler(0, yRot, tilt);
        });

        // En este punto la carta está "de canto" (90°) con el reverso. Aquí
        // se revela Tipo + Guardian Star (Fase 5-6), mientras sigue boca abajo.
        if (typeAndGuardianGroup != null) typeAndGuardianGroup.SetActive(true);

        // ── Salto del truco de flip: -90° con el sprite ya cambiado a frente ──
        flyingCardImage.sprite = card.artwork;
        flyingCard.localRotation = Quaternion.Euler(0, -90f, 0);
        flyingCard.sizeDelta = finalCardSize;
        flyingCard.anchoredPosition = finalPos;

        bool spriteAssigned = false;

        // ── Fase 7: -90° -> 0°, el frente se revela, se llenan ATK/DEF/Imagen ──
        yield return Animate(revealDuration, p =>
        {
            float e = EaseOutQuad(p);
            flyingCard.localRotation = Quaternion.Euler(0, Mathf.Lerp(-90f, 0f, e), 0);

            // El sprite se asigna ANTES de que el grupo sea visible (alpha
            // sigue en 0 en ese instante), así nunca hay un frame donde se
            // vea el sprite de la carta anterior.
            if (p > 0.4f && !spriteAssigned)
            {
                spriteAssigned = true;
                if (cardArt != null) cardArt.sprite = card.artwork;
            }

            // Fade-in suave del grupo completo (cardArt + atk/def/copies/sources)
            // en la segunda mitad de la animación, en vez de un SetActive
            // instantáneo que se ve como un "pop" en medio del giro.
            if (_statsAndArtCanvasGroup != null)
            {
                float fadeT = Mathf.InverseLerp(0.4f, 1f, p);
                _statsAndArtCanvasGroup.alpha = fadeT;
            }
        });

        if (_statsAndArtCanvasGroup != null)
        {
            _statsAndArtCanvasGroup.alpha = 1f;
            _statsAndArtCanvasGroup.blocksRaycasts = true;
            _statsAndArtCanvasGroup.interactable = true;
        }

        // Respaldo por si revealDuration es 0 o el callback nunca llegó a p > 0.4f.
        if (!spriteAssigned && cardArt != null) cardArt.sprite = card.artwork;

        flyingCard.localRotation = Quaternion.identity;

        // El "flyingCard" ya terminó su trabajo - lo ocultamos y dejamos que
        // cardArt (dentro del layout normal del panel) muestre la imagen final.
        flyingCard.gameObject.SetActive(false);

        _routine = null;
    }

    /// <summary>Desliza infoPanel desde fuera de pantalla (derecha) hasta su
    /// posición de reposo, en paralelo al vuelo de la carta.</summary>
    private IEnumerator SlideInfoPanel()
    {
        Vector2 from = infoPanel.anchoredPosition;
        yield return Animate(infoPanelSlideDuration, p =>
        {
            float e = EaseOutQuad(p);
            infoPanel.anchoredPosition = Vector2.Lerp(from, _infoPanelRestPos, e);
        });
        infoPanel.anchoredPosition = _infoPanelRestPos;
        _slideRoutine = null;
    }

    private IEnumerator PlayClose()
    {
        if (_slideRoutine != null) StopCoroutine(_slideRoutine);

        var card = _lastEntry?.card;
        bool hasFlightData = card != null && _lastSourceRect != null;

        float startAlpha = backdropGroup.alpha;
        Vector2 panelFrom = infoPanel.anchoredPosition;
        Vector2 panelTo = _infoPanelRestPos + new Vector2(infoPanelOffscreenOffset, 0f);

        if (hasFlightData)
        {
            flyingCard.SetAsLastSibling();
            flyingCard.gameObject.SetActive(true);
            flyingCardImage.sprite = card.artwork;
            flyingCard.localRotation = Quaternion.identity;
            flyingCard.anchoredPosition = _lastFinalPos;
            flyingCard.sizeDelta = finalCardSize;

            // Ocultar stats inmediatamente
            if (_statsAndArtCanvasGroup != null)
            {
                _statsAndArtCanvasGroup.alpha = 0f;
                _statsAndArtCanvasGroup.blocksRaycasts = false;
                _statsAndArtCanvasGroup.interactable = false;
            }
            if (typeAndGuardianGroup != null) typeAndGuardianGroup.SetActive(false);

            // ── Fase A: salida explosiva ──
            // La carta se lanza desde su posición final mientras gira
            // frente→reverso. El tilt Z va de 0 hacia positivo (misma
            // dirección que la apertura) para que ambos giros se perciban
            // como el mismo sentido de rotación.
            Vector2 launchTarget = _lastFinalPos + new Vector2(-Screen.width * 0.08f, Screen.height * 0.06f);
            bool swappedOnOut = false;
            const float flipOutPortion = 0.6f;

            yield return Animate(revealDuration * 0.55f, p =>
            {
                float ep = EaseOutQuad(p);

                flyingCard.anchoredPosition = Vector2.Lerp(_lastFinalPos, launchTarget, ep);
                flyingCard.sizeDelta = Vector2.Lerp(finalCardSize, _lastStartSize * takeoffGrowth, ep);
                infoPanel.anchoredPosition = Vector2.Lerp(panelFrom, panelTo, ep);
                backdropGroup.alpha = Mathf.Lerp(startAlpha, startAlpha * 0.4f, ep);

                if (p < flipOutPortion)
                {
                    float rp = p / flipOutPortion;
                    if (rp < 0.5f)
                    {
                        float re = EaseInQuad(rp / 0.5f);
                        // Y sube de 0→90, tilt Z sube de 0→maxTilt (misma dirección que apertura)
                        flyingCard.localRotation = Quaternion.Euler(0,
                            Mathf.Lerp(0f, 90f, re),
                            Mathf.Lerp(0f, maxTilt, ep));
                    }
                    else
                    {
                        if (!swappedOnOut)
                        {
                            swappedOnOut = true;
                            flyingCardImage.sprite = genericCardBack;
                            flyingCard.localRotation = Quaternion.Euler(0, -90f, 0);
                        }
                        float re = EaseOutQuad((rp - 0.5f) / 0.5f);
                        // Y sube de -90→0, tilt Z continúa subiendo hacia maxTilt
                        flyingCard.localRotation = Quaternion.Euler(0,
                            Mathf.Lerp(-90f, 0f, re),
                            Mathf.Lerp(0f, maxTilt, ep));
                    }
                }
                else
                {
                    // Giro completado, tilt se mantiene en maxTilt
                    flyingCard.localRotation = Quaternion.Euler(0, 0, maxTilt);
                }
            });

            if (!swappedOnOut) flyingCardImage.sprite = genericCardBack;
            flyingCard.localRotation = Quaternion.Euler(0, 0, maxTilt);

            // ── Fase B: arco de regreso con giro reverso→frente en el trayecto ──
            // El tilt Z baja de maxTilt→0 mientras la carta viaja de regreso,
            // mismo sentido que la Fase A (la inclinación se "deshace" en vez
            // de ir hacia el otro lado).
            Vector2 arcStart = flyingCard.anchoredPosition;
            Vector2 arcSize = flyingCard.sizeDelta;
            Vector2 landingPos = _lastStartPos + new Vector2(0f, introVerticalOffset);
            Vector2 landingSize = new Vector2(_lastStartSize.x, 190f);

            bool swappedToFront = false;
            const float flipReturnPortion = 0.40f;

            yield return Animate(takeoffDuration * 0.9f, p =>
            {
                float ep = EaseInQuad(p);
                flyingCard.anchoredPosition = Vector2.Lerp(arcStart, landingPos, ep);
                flyingCard.sizeDelta = Vector2.Lerp(arcSize, landingSize, ep);
                backdropGroup.alpha = Mathf.Lerp(startAlpha * 0.4f, 0f, ep);

                if (p < flipReturnPortion)
                {
                    float rp = p / flipReturnPortion;
                    if (rp < 0.5f)
                    {
                        float re = EaseInQuad(rp / 0.5f);
                        // Y sube de 0→90 (mismo sentido que Fase A), tilt baja de maxTilt→0
                        flyingCard.localRotation = Quaternion.Euler(0,
                            Mathf.Lerp(0f, 90f, re),
                            Mathf.Lerp(maxTilt, 0f, EaseOutQuad(p / flipReturnPortion)));
                    }
                    else
                    {
                        if (!swappedToFront)
                        {
                            swappedToFront = true;
                            flyingCardImage.sprite = card.artwork;
                            flyingCard.localRotation = Quaternion.Euler(0, -90f, 0);
                        }
                        float re = EaseOutQuad((rp - 0.5f) / 0.5f);
                        // Y sube de -90→0 (mismo sentido que antes), tilt continúa bajando a 0
                        flyingCard.localRotation = Quaternion.Euler(0,
                            Mathf.Lerp(-90f, 0f, re),
                            Mathf.Lerp(maxTilt, 0f, EaseOutQuad(p / flipReturnPortion)));
                    }
                }
                else
                {
                    // Giro terminado, tilt ya en 0
                    flyingCard.localRotation = Quaternion.identity;
                }
            });

            if (!swappedToFront) flyingCardImage.sprite = card.artwork;

            flyingCard.gameObject.SetActive(false);
        }
        else
        {
            // Respaldo si no hay datos de vuelo (ej. Hide() llamado sin Show() previo).
            yield return Animate(closeDuration, p =>
            {
                float e = EaseOutQuad(p);
                backdropGroup.alpha = Mathf.Lerp(startAlpha, 0f, p);
                infoPanel.anchoredPosition = Vector2.Lerp(panelFrom, panelTo, e);
            });
        }

        backdropGroup.alpha = 0f;
        backdropGroup.blocksRaycasts = false;
        infoPanel.anchoredPosition = panelTo;
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        ResetVisualState();
        _routine = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private IEnumerator Animate(float duration, System.Action<float> onProgress)
    {
        float t = 0f;
        if (duration <= 0f) { onProgress(1f); yield break; }

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            onProgress(Mathf.Clamp01(t / duration));
            yield return null;
        }
        onProgress(1f);
    }

    private static float EaseOutQuad(float p) => 1f - (1f - p) * (1f - p);
    private static float EaseInQuad(float p) => p * p;
    private static float EaseInOutQuad(float p) => p < 0.5f ? 2f * p * p : 1f - Mathf.Pow(-2f * p + 2f, 2f) / 2f;

    private Vector2 WorldToModalLocal(RectTransform source)
    {
        // Overlay no usa cámara (debe ser null). Cualquier otro modo (Screen
        // Space - Camera o World Space) SÍ necesita la cámara real de CADA
        // canvas -- el de origen para world->screen, el del modal para
        // screen->local, ya que pueden ser canvases distintos con cámaras
        // distintas. Usar null o la cámara equivocada da coordenadas
        // "casi correctas" que se sienten desfasadas/random según dónde
        // esté el elemento en pantalla.
        Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
        if (sourceCanvas != null) sourceCanvas = sourceCanvas.rootCanvas;

        Camera sourceCam = (sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? sourceCanvas.worldCamera : null;

        Camera modalCam = (_modalCanvas != null && _modalCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? _modalCanvas.worldCamera : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCam, source.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_modalRootRect, screenPoint, modalCam, out Vector2 local);
        return local;
    }

    private void FillStaticText(LibraryEntry entry)
    {
        var card = entry.card;
        if (nameText != null) nameText.text = card.cardName;
        if (idText != null) idText.text = $"ID: {card.cardId:000}";
        if (atkText != null) atkText.text = $"ATK: {card.baseAtk}";
        if (defText != null) defText.text = $"DEF: {card.baseDef}";
        if (levelText != null) levelText.text = $"Nivel: {card.stars}";
        if (typeText != null) typeText.text = $"Tipo: {card.monsterType}";
        if (attributeText != null) attributeText.text = $"Atributo: {card.attribute}";
        if (copiesText != null) copiesText.text = $"Copias: {entry.Copies}";
        if (sourcesText != null) sourcesText.text = BuildSourcesText(card, entry.state);

        // El botón sólo tiene sentido si esta carta ES un monstruo Y tiene un
        // modelo 3D asignado en su CardData. Magias/Equipos nunca lo muestran,
        // y monstruos aún sin modelar tampoco.
        if (view3DButton != null)
            view3DButton.gameObject.SetActive(card.IsMonster && card.monsterModelPrefab != null);
    }

    private string BuildSourcesText(CardData card, CardState state)
    {
        var visible = LibraryQueryService.GetVisibleSources(card, state);

        if (card.sources.Count == 0) return "Obtenida:\n- Desconocido";
        if (visible.Count == 0) return "Obtenida:\n- Información oculta";

        var sb = new StringBuilder("Obtenida:\n");
        foreach (var s in visible) sb.AppendLine($"- {DescribeSource(s)}");
        return sb.ToString();
    }

    private string DescribeSource(CardSourceEntry s)
    {
        if (!string.IsNullOrEmpty(s.description)) return s.description;

        if (s.sourceType == CardSourceType.Drop || s.sourceType == CardSourceType.Trade)
        {
            var opponent = LibraryCatalog.GetOpponent(s.opponentId);
            return opponent != null ? opponent.opponentName : $"Oponente #{s.opponentId}";
        }
        return s.sourceType.ToString();
    }
}