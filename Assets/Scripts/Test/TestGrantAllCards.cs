using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Escena de PRUEBA: un botón que da al jugador TODAS las cartas del juego, 3 copias
/// de cada una. Lee el catálogo completo con <see cref="LibraryCatalog.AllCards"/> y las
/// añade a la colección persistente vía <see cref="PlayerCollection.AddCopy"/> (el mismo
/// almacén que usa el Constructor de Deck, así que quedan disponibles para armar mazos).
///
/// Es IDEMPOTENTE: deja cada carta EXACTAMENTE en 3 copias (solo añade lo que falte), así
/// pulsar el botón varias veces no acumula copias de más. Construye su propia UI en runtime,
/// de modo que la escena solo necesita este componente (lo monta el menú de editor).
/// </summary>
public class TestGrantAllCards : MonoBehaviour
{
    private const int CopiesPerCard = 3;

    [SerializeField] private Button grantButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private TMP_Text statusText;

    void Start()
    {
        PlayerCollection.EnsureExists();
        if (grantButton == null || statusText == null) BuildUI();

        if (grantButton != null) grantButton.onClick.AddListener(GrantAllCards);
        if (resetButton != null) resetButton.onClick.AddListener(ResetCollection);

        int total = LibraryCatalog.TotalCount;
        UpdateStatus($"Catálogo: {total} cartas.\n" +
                     $"Pulsa \"Obtener todas las cartas (x{CopiesPerCard})\" para conseguirlas.");
    }

    /// <summary>Deja cada carta del catálogo en <see cref="CopiesPerCard"/> copias.</summary>
    public void GrantAllCards()
    {
        PlayerCollection.EnsureExists();
        var col = PlayerCollection.Instance;

        int cards = 0, added = 0;
        foreach (var card in LibraryCatalog.AllCards)
        {
            if (card == null) continue;
            int need = CopiesPerCard - col.GetCopies(card.cardId);
            if (need > 0) { col.AddCopy(card.cardId, need); added += need; }   // AddCopy ya guarda
            cards++;
        }

        UpdateStatus($"¡Listo! {cards} cartas del catálogo a {CopiesPerCard} copias cada una " +
                     $"({added} copias añadidas).\nGuardado en la colección persistente.");
        Debug.Log($"TestGrantAllCards: {cards} cartas → {CopiesPerCard} copias c/u ({added} añadidas).");
    }

    /// <summary>Vacía la colección (para volver a probar desde cero).</summary>
    public void ResetCollection()
    {
        PlayerCollection.EnsureExists();
        PlayerCollection.Instance.ResetCollection();
        PlayerCollection.Instance.Save();   // ResetCollection borra el archivo; re-guardar deja uno vacío
        UpdateStatus("Colección reiniciada (0 cartas).\nPulsa Obtener para volver a llenarla.");
    }

    private void UpdateStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    // ── UI mínima construida en runtime ──────────────────────────────────

    private void BuildUI()
    {
        var canvasGO = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var bg = MakeImage(canvasGO.transform, "BG", new Color(0.06f, 0.07f, 0.12f, 1f));
        Stretch(bg.rectTransform);

        var title = MakeText(canvasGO.transform, "Title", "ESCENA DE TEST — COLECCIÓN", 64f, FontStyles.Bold,
                             new Color(1f, 0.86f, 0.4f));
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1400f, 120f));
        title.alignment = TextAlignmentOptions.Center;

        grantButton = MakeButton(canvasGO.transform, $"Obtener todas las cartas (x{CopiesPerCard})",
                                 new Vector2(0f, 70f), new Color(0.85f, 0.68f, 0.22f));
        resetButton = MakeButton(canvasGO.transform, "Reiniciar colección",
                                 new Vector2(0f, -70f), new Color(0.5f, 0.16f, 0.16f));

        var st = MakeText(canvasGO.transform, "Status", "", 34f, FontStyles.Normal, Color.white);
        Place(st.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(1500f, 320f));
        st.alignment = TextAlignmentOptions.Top;
        statusText = st;
    }

    private static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private static TMP_Text MakeText(Transform parent, string name, string text, float size,
                                     FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button MakeButton(Transform parent, string label, Vector2 anchoredPos, Color color)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        var btn = go.GetComponent<Button>();
        Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), anchoredPos, new Vector2(720f, 110f));

        var txt = MakeText(go.transform, "Label", label, 38f, FontStyles.Bold, new Color(0.06f, 0.06f, 0.08f));
        Stretch(txt.rectTransform);
        return btn;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
