using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuración de un duelo específico (rival, terreno, recompensas).
/// Crea uno por cada oponente en: Assets > Create > YGO > Duel Config
/// </summary>
[CreateAssetMenu(fileName = "NewDuel", menuName = "YGO/Duel Config")]
public class DuelConfig : ScriptableObject
{
    [Header("Oponente")]
    public int opponentId;
    public string opponentName;
    [Range(0, 3)] public int aiLevel = 1;   // 0 = fácil … 3 = difícil

    [Header("Terreno")]
    public TerrainType terrain = TerrainType.Neutral;

    [Header("Mazo del oponente")]
    public List<CardData> opponentDeck = new();

    [Header("Recompensas (drops)")]
    public List<DropEntry> powDrops = new();   // drop POW
    public List<DropEntry> tecDrops = new();   // drop TEC
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
