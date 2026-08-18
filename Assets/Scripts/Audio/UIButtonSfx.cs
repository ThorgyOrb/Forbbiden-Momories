using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Enlaza el clic (y opcionalmente el hover) de un <see cref="Button"/> a los efectos de
/// interfaz comunes de <see cref="GameAudio"/>. Es una llamada estática de una línea en
/// vez de un componente que arrastrar en el Inspector, para poder engancharla tanto a
/// botones fijos de la escena (junto al resto de WireButtons/Wire) como a botones
/// instanciados en tiempo real (tarjetas de colección, entradas de rival, filas de deck...).
///
/// Uso:  UIButtonSfx.Hook(miBoton);               // clic + hover
///       UIButtonSfx.Hook(miBoton, hover: false);  // solo clic (recomendado en grillas
///                                                  // con muchas tarjetas, para no añadir
///                                                  // un EventTrigger por instancia)
/// </summary>
public static class UIButtonSfx
{
    public static void Hook(Button button, bool hover = true)
    {
        if (button == null) return;
        button.onClick.AddListener(GameAudio.Click);
        if (hover) HookHover(button.gameObject);
    }

    /// <summary>Engancha solo el sonido de hover a cualquier objeto interactivo (Button, Toggle, Slider...).</summary>
    public static void HookHover(GameObject go)
    {
        if (go == null) return;
        var trigger = go.GetComponent<EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ => GameAudio.Hover());
        trigger.triggers.Add(entry);
    }
}
