using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reemplaza a tu Library.cs original. Misma idea de instanciar un slot por
/// carta, pero ahora muestra las 722 SIEMPRE (catálogo completo) y el estado
/// visual de cada una depende de PlayerCollection, no de si existe el asset.
/// </summary>
public class LibraryManager : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject cardSlotPrefab; // debe tener LibraryCardSlot

    [Header("Contenedor")]
    [SerializeField] private Transform gridContainer; // el ScrollView Content

    [Header("Encabezado")]
    [SerializeField] private TMP_Text headerText; // "Total: 722  Descubiertas: 548  Completitud: 75.9%"

    [Header("Búsqueda y orden")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private TMP_Dropdown sortDropdown; // mismo orden que CardSortOption

    [Header("Detalle")]
    [SerializeField] private CardDetailPanel detailPanel;

    private CardFilterCriteria _criteria = new();
    private CardSortOption _sort = CardSortOption.IdAsc;
    private readonly List<LibraryCardSlot> _spawnedSlots = new();

    void Start()
    {
        LibraryCatalog.EnsureLoaded();

        EnsureResponsive();

        if (searchInput != null)
            searchInput.onValueChanged.AddListener(OnSearchChanged);

        if (sortDropdown != null)
            sortDropdown.onValueChanged.AddListener(OnSortChanged);

        RefreshGrid();
        UpdateHeader();
    }

    /// <summary>
    /// Garantiza que el canvas del catálogo mantenga el diseño de referencia
    /// dentro de cuadro en cualquier resolución/relación de aspecto, añadiendo
    /// <see cref="ResponsiveCanvasMatch"/> al CanvasScaler si aún no lo tiene.
    /// </summary>
    private void EnsureResponsive()
    {
        CanvasScaler scaler = null;
        if (gridContainer != null) scaler = gridContainer.GetComponentInParent<CanvasScaler>();
        if (scaler == null) scaler = FindObjectOfType<CanvasScaler>();

        if (scaler != null && scaler.GetComponent<ResponsiveCanvasMatch>() == null)
            scaler.gameObject.AddComponent<ResponsiveCanvasMatch>();
    }

    // ── Eventos de UI ────────────────────────────────────────────────────

    private void OnSearchChanged(string value)
    {
        _criteria.searchText = value;
        RefreshGrid();
    }

    private void OnSortChanged(int index)
    {
        _sort = (CardSortOption)index;
        RefreshGrid();
    }

    /// <summary>Llama esto desde tus checkboxes/dropdowns de filtro, pasando una
    /// copia ya modificada de los criterios actuales (o muta _criteria directo
    /// si prefieres exponerla con una propiedad pública).</summary>
    public void ApplyFilters(CardFilterCriteria newCriteria)
    {
        _criteria = newCriteria;
        RefreshGrid();
    }

    public CardFilterCriteria CurrentCriteria => _criteria;

    /// <summary>Fuerza un refresco del grid sin cambiar filtros ni orden. Útil para
    /// herramientas externas (ej. botones de debug) después de mutar PlayerCollection.</summary>
    public void RefreshNow() => RefreshGrid();

    // ── Construcción del grid ────────────────────────────────────────────

    private void RefreshGrid()
    {
        foreach (var slot in _spawnedSlots)
            Destroy(slot.gameObject);
        _spawnedSlots.Clear();

        List<LibraryEntry> entries = LibraryQueryService.Query(_criteria, _sort);

        foreach (var entry in entries)
        {
            GameObject go = Instantiate(cardSlotPrefab, gridContainer);
            var slot = go.GetComponent<LibraryCardSlot>();
            slot.Setup(entry, OnSlotClicked);
            _spawnedSlots.Add(slot);
        }

        UpdateHeader();
    }

    private void OnSlotClicked(LibraryEntry entry, RectTransform sourceRect)
    {
        // Regla pedida: si está bloqueada, no se puede ver nada de ella.
        if (entry.state == CardState.Locked)
        {
            Debug.Log($"LibraryManager: '{entry.card.cardName}' está bloqueada, no se abre detalle.");
            return;
        }

        if (detailPanel == null)
        {
            Debug.LogWarning("LibraryManager: el campo 'detailPanel' no está asignado en el Inspector.");
            return;
        }

        detailPanel.Show(entry, sourceRect);
    }

    private void UpdateHeader()
    {
        if (headerText == null) return;

        var (total, discovered, owned, completion) = LibraryQueryService.GetGlobalStats();
        headerText.text = $"Total cartas: {total}   Descubiertas: {discovered}   " +
                           $"Poseídas: {owned}   Completitud: {completion:0.0}%";
    }
}