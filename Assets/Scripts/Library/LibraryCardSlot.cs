using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Componente complementario que vive en el MISMO GameObject que CardDisplay
/// (el prefab real de la carta, con holo effect, frame, rarity gem, reverso, etc.).
/// </summary>
[RequireComponent(typeof(CardDisplay))]
public class LibraryCardSlot : MonoBehaviour
{
    [Header("Referencia del mismo prefab")]
    [SerializeField] private CardDisplay cardDisplay;

    [Header("Extras propios de Library (opcionales)")]
    [SerializeField] private TMP_Text copiesBadge;
    [SerializeField] private GameObject favoriteIcon;
    [SerializeField] private GameObject lockedHint;

    private LibraryEntry _entry;
    private Action<LibraryEntry, RectTransform> _onClick;
    private bool _eventsRegistered;

    void Reset()
    {
        if (cardDisplay == null) cardDisplay = GetComponent<CardDisplay>();
    }

    public void Setup(LibraryEntry entry, Action<LibraryEntry, RectTransform> onClick)
    {
        _entry = entry;
        _onClick = onClick;

        bool locked = entry.state == CardState.Locked;

        if (cardDisplay == null) cardDisplay = GetComponent<CardDisplay>();

        cardDisplay.Setup(entry.card);
        cardDisplay.SetPosition(locked ? CardPosition.FaceDownAttack : CardPosition.FaceUpAttack);

        if (lockedHint != null) lockedHint.SetActive(locked);

        if (copiesBadge != null)
        {
            copiesBadge.gameObject.SetActive(!locked);
            if (!locked) copiesBadge.text = $"x{entry.Copies}";
        }

        if (favoriteIcon != null)
            favoriteIcon.SetActive(!locked && entry.IsFavorite);

        RegisterClick();
    }

    private void RegisterClick()
    {
        if (_eventsRegistered) return;
        _eventsRegistered = true;

        var trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = gameObject.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        // Pasamos nuestro propio RectTransform: es el punto de origen del vuelo de la carta.
        entry.callback.AddListener((_) => _onClick?.Invoke(_entry, (RectTransform)transform));
        trigger.triggers.Add(entry);
    }
}