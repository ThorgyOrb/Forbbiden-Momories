using UnityEngine;

/// <summary>
/// Banco de audio del duelo: música de fondo + un clip por cada acción. Rellénalo con
/// TUS AudioClips en el asset (menú YGO > Audio > Crear Duel Audio Bank, o
/// Create > YGO > Duel Audio Bank). Debe vivir en una carpeta <b>Resources</b> con el
/// nombre "DuelAudioBank" para que <see cref="DuelAudio"/> lo cargue solo.
///
/// Cualquier clip que dejes vacío simplemente NO suena (todo es null-safe): puedes ir
/// rellenándolos poco a poco.
/// </summary>
[CreateAssetMenu(fileName = "DuelAudioBank", menuName = "YGO/Duel Audio Bank")]
public class DuelAudioBank : ScriptableObject
{
    [Header("Música de fondo")]
    public AudioClip bgm;          // suena en bucle durante el duelo
    public AudioClip victoryBgm;   // al ganar
    public AudioClip defeatBgm;    // al perder
    [Range(0f, 1f)] public float bgmVolume = 0.5f;

    [Header("Volumen de efectos")]
    [Range(0f, 1f)] public float sfxVolume = 0.85f;

    [Header("Interfaz / selección")]
    public AudioClip cursorMove;   // mover el cursor entre cartas/casillas
    public AudioClip select;       // confirmar (A)
    public AudioClip cancel;       // volver (S / Esc)
    public AudioClip guardianStar; // elegir Estrella Guardiana

    [Header("Cartas / campo")]
    public AudioClip draw;         // robar carta
    public AudioClip summon;       // invocar monstruo
    public AudioClip flip;         // voltear/revelar carta
    public AudioClip setCard;      // colocar magia/trampa boca abajo
    public AudioClip spell;        // activar magia
    public AudioClip trap;         // activar trampa

    [Header("Fusión / equipo")]
    public AudioClip fusionStart;  // inicia una fusión
    public AudioClip fuse;         // dos cartas se fusionan (destello)
    public AudioClip equip;        // absorción de equipo

    [Header("Combate")]
    public AudioClip attack;       // se lanza el ataque
    public AudioClip slash;        // impacto / corte
    public AudioClip destroy;      // un monstruo es destruido (fuego)
    public AudioClip damage;       // pérdida de LP

    [Header("Turno / fase")]
    public AudioClip phase;        // cambio de fase
    public AudioClip turnStart;    // inicia un turno
}
