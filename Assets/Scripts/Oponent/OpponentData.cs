using UnityEngine;

/// <summary>
/// Catálogo de un oponente/duelista. Igual que CardData: esto es fijo,
/// el progreso (si está desbloqueado o no) vive en PlayerCollection.
/// </summary>
[CreateAssetMenu(fileName = "NewOpponent", menuName = "YGO/Opponent Data")]
public class OpponentData : ScriptableObject
{
    public int opponentId;
    public string opponentName;
    public Sprite portrait;
}
