using System;
using UnityEngine;

/// <summary>
/// Banco de audio GLOBAL del juego: efectos de interfaz comunes a todas las pantallas y
/// el mapa de música de fondo por escena. Vive en Resources con el nombre "GameAudioBank"
/// para que <see cref="GameAudio"/> lo cargue solo (menú YGO > Audio > Crear Game Audio
/// Bank). Cualquier clip que dejes vacío simplemente no suena.
///
/// El Duelo NO se gestiona aquí: sigue usando su propio <see cref="DuelAudioBank"/> con
/// todos los efectos de invocar/fusionar/atacar. Este banco cubre todo lo demás (menú,
/// colección, constructor de deck, duelo libre, historia...) y por eso NO debe incluir
/// "DuelScene" en <see cref="sceneMusic"/>: el duelo pone su propia música (con la
/// prioridad del rival, ver OpponentData.battleMusic) a través de DuelAudio.
/// </summary>
[CreateAssetMenu(fileName = "GameAudioBank", menuName = "YGO/Game Audio Bank")]
public class GameAudioBank : ScriptableObject
{
    [Header("Efectos de interfaz (todas las escenas)")]
    public AudioClip uiClick;      // confirmar / pulsar un botón
    public AudioClip uiHover;      // pasar el cursor sobre un botón
    public AudioClip uiBack;       // volver / cancelar
    public AudioClip uiToggle;     // interruptor (pantalla completa, idioma...)
    public AudioClip uiError;      // acción no disponible todavía
    [Range(0f, 1f)] public float uiVolume = 0.8f;

    [Header("Música de fondo")]
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Tooltip("Duración del fundido cruzado al cambiar de pista, en segundos.")]
    public float musicFadeSeconds = 0.6f;

    [Header("Música por escena")]
    [Tooltip("El nombre debe coincidir EXACTO con el de la escena (ver GameScenes). " +
             "Deja 'DuelScene' fuera de esta lista: el duelo gestiona su propia música " +
             "a través de DuelAudio / DuelAudioBank.")]
    public SceneMusicEntry[] sceneMusic = Array.Empty<SceneMusicEntry>();

    /// <summary>Clip de música asignado a una escena por nombre, o null si no hay ninguno.</summary>
    public AudioClip GetMusicForScene(string sceneName)
    {
        if (sceneMusic == null || string.IsNullOrEmpty(sceneName)) return null;
        foreach (var entry in sceneMusic)
            if (entry != null && entry.sceneName == sceneName) return entry.clip;
        return null;
    }
}

[Serializable]
public class SceneMusicEntry
{
    public string sceneName;
    public AudioClip clip;
}
