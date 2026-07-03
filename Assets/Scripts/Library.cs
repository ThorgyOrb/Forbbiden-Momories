using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class Library : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Prefab")]
    [SerializeField] private GameObject cardPrefab;

    [Header("Preview UI - Artwork")]
    [SerializeField] private GameObject previewPanel;   // panel que aparece al hover
    [SerializeField] private Image previewArt;
    [SerializeField] private TMP_Text previewName;
    [SerializeField] private TMP_Text previewType;

    [Header("Preview UI - Stats")]
    [SerializeField] private TMP_Text previewAtkValue;
    [SerializeField] private TMP_Text previewDefValue;

    [Header("Preview UI - Guardian Stars")]
    [SerializeField] private TMP_Text previewStarAText;
    [SerializeField] private TMP_Text previewStarBText;

    [Header("Contenedor de cartas")]
    [SerializeField] private Transform gridContainer;  // el ScrollView Content

    // cache interno
    private CardData[] _allCards;
    private Dictionary<GameObject, CardData> _cardMap = new();

    void Start()
    {
        // 1. Cargar todos los ScriptableObjects desde Resources/Cards/Data/
        _allCards = Resources.LoadAll<CardData>("Cards/Data");
        if (_allCards.Length == 0)
        {
            Debug.LogWarning("Library: no se encontraron CardData en Resources/Cards/Data/");
            return;
        }

        // 2. Instanciar una carta por cada CardData
        foreach (CardData data in _allCards)
        {
            GameObject go = Instantiate(cardPrefab, gridContainer);
            go.name = data.cardName;
            go.GetComponent<CardDisplay>().Setup(data);

            // guardamos referencia para el hover
            _cardMap[go] = data;

            // agregar listener de click (para seleccionar carta)
            var trigger = go.AddComponent<EventTrigger>();
            AddEventTrigger(trigger, EventTriggerType.PointerEnter, (e) => OnCardHover(go));
            AddEventTrigger(trigger, EventTriggerType.PointerExit, (e) => HidePreview());
            AddEventTrigger(trigger, EventTriggerType.PointerClick, (e) => OnCardClick(go));
        }

        HidePreview();
    }

    // ── Hover — muestra el preview de la carta ──────────────────────────
    private void OnCardHover(GameObject cardGO)
    {
        if (!_cardMap.TryGetValue(cardGO, out CardData data)) return;

        previewPanel.SetActive(true);

        if (previewArt != null) previewArt.sprite = data.artwork;
        if (previewName != null) previewName.text = data.cardName;
        if (previewType != null) previewType.text = data.monsterType.ToString();

        if (previewAtkValue != null) previewAtkValue.text = data.baseAtk.ToString();
        if (previewDefValue != null) previewDefValue.text = data.baseDef.ToString();

        if (previewStarAText != null) previewStarAText.text = data.starA.ToString();
        if (previewStarBText != null) previewStarBText.text = data.starB.ToString();
    }

    private void HidePreview() => previewPanel.SetActive(false);

    // ── Click — aquí agregarás lógica de agregar al mazo, etc. ──────────
    private void OnCardClick(GameObject cardGO)
    {
        if (!_cardMap.TryGetValue(cardGO, out CardData data)) return;
        Debug.Log($"Carta seleccionada: {data.cardName} | ATK {data.baseAtk} / DEF {data.baseDef}");
        // TODO: OnCardSelected?.Invoke(data);  ← evento para el mazo
    }

    // ── Helpers ──────────────────────────────────────────────────────────
    private void AddEventTrigger(EventTrigger trigger, EventTriggerType type,
                                  System.Action<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener((e) => action(e));
        trigger.triggers.Add(entry);
    }

    // IPointerEnterHandler / Exit en el contenedor global (opcional)
    public void OnPointerEnter(PointerEventData e) { }
    public void OnPointerExit(PointerEventData e) => HidePreview();
}