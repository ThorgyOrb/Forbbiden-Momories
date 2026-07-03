using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Model3DViewer : MonoBehaviour
{
    [Header("Raíz del modal del visor")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootGroup;

    [Header("Render 3D")]
    [SerializeField] private Camera modelCamera;
    [SerializeField] private RawImage displayImage;
    [SerializeField] private Transform pivotPoint;

    [Header("Texto")]
    [SerializeField] private TMP_Text monsterNameText;

    [Header("Cierre")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backdropButton;

    [Header("Rotación")]
    [SerializeField] private float dragRotateSpeed = 0.3f;
    [SerializeField] private float autoRotateSpeed = 15f;
    [SerializeField] private float autoRotateDelay = 1.5f;

    [Header("Modelo")]
    [SerializeField] private float modelScale = 1f;
    [SerializeField] private Vector3 modelLocalOffset = Vector3.zero;

    [Header("Cortinas")]
    [SerializeField] private RectTransform curtainLeft;
    [SerializeField] private RectTransform curtainRight;
    [SerializeField] private CanvasGroup curtainLeftGroup;
    [SerializeField] private CanvasGroup curtainRightGroup;
    [SerializeField] private float curtainFadeInDuration = 0.3f;
    [SerializeField] private float openDuration = 0.5f;
    [SerializeField] private float closeDuration = 0.35f;
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve closeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float modelEntranceExtraSpinY = 360f;

    private GameObject _currentModelInstance;
    private CardData _currentCard;
    private bool _isDragging;
    private float _timeSinceLastDrag;
    private EventTrigger _dragTrigger;
    private Coroutine _animRoutine;

    // Posiciones cerradas (las que tienen en el editor = cubriendo la pantalla)
    private Vector2 _leftClosedPos;
    private Vector2 _rightClosedPos;

    // Posiciones abiertas (fuera de pantalla hacia cada lado)
    private Vector2 _leftOpenPos;
    private Vector2 _rightOpenPos;

    // Rotación local original del prefab instanciado (ej. -90 en X que trae Meshy),
    // para no perderla al animar la entrada del modelo.
    private Quaternion _modelBaseRotation = Quaternion.identity;

    void Awake()
    {
        if (root != null) root.SetActive(true);
        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        if (modelCamera != null) modelCamera.gameObject.SetActive(false);

        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (backdropButton != null) backdropButton.onClick.AddListener(Hide);

        SetupDragRotation();
    }

    void Start()
    {
        // Se calcula en Start y no en Awake porque rect.width necesita un
        // frame para estar disponible correctamente en elementos de Layout.
        if (curtainLeft != null)
        {
            _leftClosedPos = curtainLeft.anchoredPosition;
            // Abierta = desplazada su propio ancho hacia la IZQUIERDA
            _leftOpenPos = _leftClosedPos + new Vector2(-curtainLeft.rect.width, 0f);
            // Arranca en posición abierta (invisible, fuera de pantalla)
            curtainLeft.anchoredPosition = _leftOpenPos;
        }

        if (curtainRight != null)
        {
            _rightClosedPos = curtainRight.anchoredPosition;
            // Abierta = desplazada su propio ancho hacia la DERECHA
            _rightOpenPos = _rightClosedPos + new Vector2(curtainRight.rect.width, 0f);
            curtainRight.anchoredPosition = _rightOpenPos;
        }

        // Alpha inicial en 0: se usan para el fade-in de la próxima apertura
        if (curtainLeftGroup != null) curtainLeftGroup.alpha = 0f;
        if (curtainRightGroup != null) curtainRightGroup.alpha = 0f;
    }

    // ── API pública ──────────────────────────────────────────────────────

    public void Show(CardData card)
    {
        if (card == null || card.monsterModelPrefab == null)
        {
            Debug.LogWarning($"Model3DViewer: '{card?.cardName}' no tiene monsterModelPrefab asignado.");
            return;
        }

        _currentCard = card;
        SpawnModel(card);

        if (monsterNameText != null) monsterNameText.text = card.cardName;
        if (modelCamera != null) modelCamera.gameObject.SetActive(true);

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = true;
        }

        _timeSinceLastDrag = 0f;
        _isDragging = false;

        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(PlayOpenAnimation());
    }

    public void Hide()
    {
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        if (rootGroup != null) rootGroup.interactable = false;
        _animRoutine = StartCoroutine(PlayCloseAnimation());
    }

    // ── Animaciones ──────────────────────────────────────────────────────

    private IEnumerator PlayOpenAnimation()
    {
        // Las cortinas aparecen ya CERRADAS (cubriendo la pantalla) pero
        // totalmente invisibles, y se revelan con un desvanecido (fade-in).
        if (curtainLeft != null) curtainLeft.anchoredPosition = _leftClosedPos;
        if (curtainRight != null) curtainRight.anchoredPosition = _rightClosedPos;
        if (curtainLeftGroup != null) curtainLeftGroup.alpha = 0f;
        if (curtainRightGroup != null) curtainRightGroup.alpha = 0f;

        // Modelo listo para mostrarse detrás de las cortinas
        if (_currentModelInstance != null)
        {
            _currentModelInstance.transform.localScale = Vector3.zero;
            _currentModelInstance.transform.localRotation =
                _modelBaseRotation * Quaternion.Euler(0f, modelEntranceExtraSpinY, 0f);
        }

        // Paso 1: desvanecido del visor completo y de las cortinas (aparecen cerradas, sin deslizarse)
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(curtainFadeInDuration, 0.0001f);
            float e = Mathf.Clamp01(t);

            if (rootGroup != null) rootGroup.alpha = e;
            if (curtainLeftGroup != null) curtainLeftGroup.alpha = e;
            if (curtainRightGroup != null) curtainRightGroup.alpha = e;

            yield return null;
        }

        if (rootGroup != null) rootGroup.alpha = 1f;
        if (curtainLeftGroup != null) curtainLeftGroup.alpha = 1f;
        if (curtainRightGroup != null) curtainRightGroup.alpha = 1f;

        // Paso 2: cortinas se abren deslizándose hacia sus lados (izq→izquierda, der→derecha)
        // en paralelo con el modelo que crece girando.
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(openDuration, 0.0001f);
            float e = openCurve.Evaluate(Mathf.Clamp01(t));

            // Izquierda se va a la izquierda, derecha se va a la derecha
            if (curtainLeft != null)
                curtainLeft.anchoredPosition = Vector2.Lerp(_leftClosedPos, _leftOpenPos, e);
            if (curtainRight != null)
                curtainRight.anchoredPosition = Vector2.Lerp(_rightClosedPos, _rightOpenPos, e);

            // Modelo crece girando
            if (_currentModelInstance != null)
            {
                _currentModelInstance.transform.localScale =
                    Vector3.LerpUnclamped(Vector3.zero, Vector3.one * modelScale, e);
                _currentModelInstance.transform.localRotation = Quaternion.Slerp(
                    _modelBaseRotation * Quaternion.Euler(0f, modelEntranceExtraSpinY, 0f),
                    _modelBaseRotation, Mathf.Clamp01(e));
            }

            yield return null;
        }

        // Snap finales
        if (curtainLeft != null) curtainLeft.anchoredPosition = _leftOpenPos;
        if (curtainRight != null) curtainRight.anchoredPosition = _rightOpenPos;
        if (_currentModelInstance != null)
        {
            _currentModelInstance.transform.localScale = Vector3.one * modelScale;
            _currentModelInstance.transform.localRotation = _modelBaseRotation;
        }

        _animRoutine = null;
    }

    private IEnumerator PlayCloseAnimation()
    {
        // Cortinas entran desde afuera hacia el centro (tapan el visor)
        if (curtainLeft != null) curtainLeft.anchoredPosition = _leftOpenPos;
        if (curtainRight != null) curtainRight.anchoredPosition = _rightOpenPos;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(closeDuration, 0.0001f);
            float e = closeCurve.Evaluate(Mathf.Clamp01(t));

            if (curtainLeft != null)
                curtainLeft.anchoredPosition = Vector2.Lerp(_leftOpenPos, _leftClosedPos, e);
            if (curtainRight != null)
                curtainRight.anchoredPosition = Vector2.Lerp(_rightOpenPos, _rightClosedPos, e);

            yield return null;
        }

        if (curtainLeft != null) curtainLeft.anchoredPosition = _leftClosedPos;
        if (curtainRight != null) curtainRight.anchoredPosition = _rightClosedPos;

        // Apagar el visor detrás de las cortinas cerradas
        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }
        if (modelCamera != null) modelCamera.gameObject.SetActive(false);
        DespawnModel();
        _currentCard = null;

        // Cortinas salen hacia afuera (abriéndose de nuevo para dejar la pantalla limpia)
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(openDuration, 0.0001f);
            float e = openCurve.Evaluate(Mathf.Clamp01(t));

            if (curtainLeft != null)
                curtainLeft.anchoredPosition = Vector2.Lerp(_leftClosedPos, _leftOpenPos, e);
            if (curtainRight != null)
                curtainRight.anchoredPosition = Vector2.Lerp(_rightClosedPos, _rightOpenPos, e);

            yield return null;
        }

        if (curtainLeft != null) curtainLeft.anchoredPosition = _leftOpenPos;
        if (curtainRight != null) curtainRight.anchoredPosition = _rightOpenPos;

        _animRoutine = null;
    }

    // ── Instanciado del modelo ───────────────────────────────────────────

    private void SpawnModel(CardData card)
    {
        DespawnModel();
        if (pivotPoint == null) { Debug.LogWarning("Model3DViewer: 'pivotPoint' no asignado."); return; }

        pivotPoint.localRotation = Quaternion.identity;
        _currentModelInstance = Instantiate(card.monsterModelPrefab, pivotPoint);
        _currentModelInstance.transform.localPosition = modelLocalOffset;

        // Guardamos la rotación con la que vino el prefab (ej. -90 en X)
        // ANTES de tocarla, para usarla como destino de la animación de entrada.
        _modelBaseRotation = _currentModelInstance.transform.localRotation;

        _currentModelInstance.transform.localScale = Vector3.zero;
        SetLayerRecursively(_currentModelInstance.transform, pivotPoint.gameObject.layer);
    }

    private void DespawnModel()
    {
        if (_currentModelInstance != null) { Destroy(_currentModelInstance); _currentModelInstance = null; }
    }

    private static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayerRecursively(child, layer);
    }

    // ── Rotación por arrastre ────────────────────────────────────────────

    private void SetupDragRotation()
    {
        if (displayImage == null) return;
        _dragTrigger = displayImage.GetComponent<EventTrigger>()
                    ?? displayImage.gameObject.AddComponent<EventTrigger>();

        AddTrigger(EventTriggerType.BeginDrag, _ => _isDragging = true);
        AddTrigger(EventTriggerType.Drag, OnDrag);
        AddTrigger(EventTriggerType.EndDrag, _ => { _isDragging = false; _timeSinceLastDrag = 0f; });
    }

    private void AddTrigger(EventTriggerType type, System.Action<BaseEventData> cb)
    {
        var e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(new UnityEngine.Events.UnityAction<BaseEventData>(cb));
        _dragTrigger.triggers.Add(e);
    }

    private void OnDrag(BaseEventData data)
    {
        if (pivotPoint == null || _animRoutine != null) return;
        pivotPoint.Rotate(Vector3.up, -((PointerEventData)data).delta.x * dragRotateSpeed, Space.World);
    }

    void Update()
    {
        if (pivotPoint == null || _currentModelInstance == null) return;
        if (rootGroup != null && rootGroup.alpha <= 0f) return;
        if (_animRoutine != null || _isDragging) return;

        _timeSinceLastDrag += Time.unscaledDeltaTime;
        if (_timeSinceLastDrag >= autoRotateDelay)
            pivotPoint.Rotate(Vector3.up, autoRotateSpeed * Time.unscaledDeltaTime, Space.World);
    }
}