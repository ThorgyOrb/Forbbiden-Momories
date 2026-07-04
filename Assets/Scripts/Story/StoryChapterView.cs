using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista de UN capítulo en la hoja de ruta de la campaña: número, nombre del
/// rival (o "???" si aún está bloqueado), retrato y un estado (Derrotado / Actual
/// / Bloqueado). Tocarlo selecciona ese capítulo en el panel de detalle.
///
/// El <see cref="StoryController"/> clona la plantilla que tiene este componente
/// y llama a <see cref="Setup"/> por cada rival de la campaña.
/// </summary>
public class StoryChapterView : MonoBehaviour
{
    [SerializeField] private Button selectButton;   // toda la fila
    [SerializeField] private Image portrait;
    [SerializeField] private Image statusDot;        // color según estado
    [SerializeField] private TextMeshProUGUI chapterText;   // "Capítulo 3"
    [SerializeField] private TextMeshProUGUI nameText;      // nombre o "???"
    [SerializeField] private TextMeshProUGUI statusText;    // "Derrotado" / "Actual" / "Bloqueado"

    static readonly Color DoneColor    = new Color(0.45f, 0.80f, 0.45f);
    static readonly Color CurrentColor = new Color(0.98f, 0.85f, 0.45f);
    static readonly Color LockedColor  = new Color(0.45f, 0.47f, 0.55f);

    public void Setup(int index, OpponentData opp, StoryService.ChapterState state, Action onSelect)
    {
        bool locked = state == StoryService.ChapterState.Locked;

        if (chapterText != null) chapterText.text = $"Capítulo {index + 1}";

        if (nameText != null)
            nameText.text = locked ? "???" : opp.opponentName;

        if (portrait != null)
        {
            portrait.sprite = locked ? null : opp.portrait;
            portrait.enabled = !locked && opp.portrait != null;
            // Silueta oscura si está bloqueado o sin retrato.
            portrait.color = locked ? new Color(0f, 0f, 0f, 0.55f) : Color.white;
        }

        Color c = state switch
        {
            StoryService.ChapterState.Completed => DoneColor,
            StoryService.ChapterState.Current   => CurrentColor,
            _                                   => LockedColor
        };
        if (statusDot != null) statusDot.color = c;
        if (statusText != null)
        {
            statusText.text = state switch
            {
                StoryService.ChapterState.Completed => "Derrotado",
                StoryService.ChapterState.Current   => "Actual",
                _                                   => "Bloqueado"
            };
            statusText.color = c;
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            // Los capítulos bloqueados no se pueden inspeccionar.
            selectButton.interactable = !locked;
            if (!locked) selectButton.onClick.AddListener(() => onSelect?.Invoke());
        }
    }
}
