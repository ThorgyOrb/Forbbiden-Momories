using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuración de un duelo específico (rival, terreno, recompensas).
/// Crea uno por cada oponente en: Assets > Create > YGO > Duel Config
/// </summary>
/// <summary>
/// Configuración de UN duelo concreto (el "encuentro"). NO redefine al rival:
/// solo lo ENLAZA (via <see cref="opponent"/>) y permite ajustes puntuales de
/// esta pelea. Todo lo que "es" el rival (mazo, IA, música, arena, recompensas)
/// vive en su <see cref="OpponentData"/>.
///
/// Modelo mental:  OpponentData = quién es el rival · DuelConfig = esta partida.
/// </summary>
[CreateAssetMenu(fileName = "NewDuel", menuName = "YGO/Duel Config")]
public class DuelConfig : ScriptableObject
{
    [Header("Rival")]
    [Tooltip("El oponente de este duelo: define mazo, IA, música, arena y recompensas.")]
    public OpponentData opponent;

    [Header("Overrides opcionales SOLO de este duelo")]
    [Tooltip("Si NO es Neutral, fuerza el terreno de esta pelea por encima de la arena del " +
             "rival (ej. un duelo de historia en un lugar concreto).")]
    public TerrainType terrainOverride = TerrainType.Neutral;

    [Tooltip("Si tiene entradas, reemplaza las recompensas del rival SOLO en este duelo " +
             "(ej. carta garantizada de una batalla de historia). Vacío = usa las del OpponentData.")]
    public RewardTable rewardOverride = new();
}

[System.Serializable]
public class DropEntry
{
    public CardData card;
    [Range(0f, 1f)] public float probability = 0.1f;
}

public enum TerrainType
{
    Neutral,
    Forest,
    Mountain,
    Sea,
    Dark,
    Wasteland,
    Meadow,
    Yami
}
