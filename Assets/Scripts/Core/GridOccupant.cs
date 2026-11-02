using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GridOccupant : MonoBehaviour
{
    [Header("Battle Identity")]
    [SerializeField] private BattleFaction faction;
    [SerializeField, Min(0)] private int moveRange = 3;
    [SerializeField, Min(0)] private int threatRange = 1;
    [SerializeField] private TacticalState tacticalState = TacticalState.Guard;

    public BattleFaction Faction => faction;
    public int MoveRange => moveRange;
    public int ThreatRange => threatRange;
    public TacticalState State => tacticalState;
    public BattleField Board { get; private set; }
    public GridCoordinate Coordinate { get; private set; }
    public bool IsPlaced => Board != null;

    public event Action<GridOccupant, TacticalState, TacticalState> StateChanged;
    public event Action<GridOccupant, Vector3, Vector3> Moved;

    public void Configure(BattleFaction newFaction, int newMoveRange, int newThreatRange)
    {
        faction = newFaction;
        moveRange = Mathf.Max(0, newMoveRange);
        threatRange = Mathf.Max(0, newThreatRange);
    }

    internal void Place(BattleField board, GridCoordinate coordinate, Vector3 worldPosition)
    {
        bool wasPlaced = Board != null;
        Vector3 previousPosition = transform.position;
        Board = board;
        Coordinate = coordinate;
        transform.SetPositionAndRotation(worldPosition, board.transform.rotation);
        if (wasPlaced && Application.isPlaying && previousPosition != worldPosition)
            Moved?.Invoke(this, previousPosition, worldPosition);
    }

    internal void RemoveFromBoard(BattleField board)
    {
        if (Board != board) return;
        Board = null;
    }

    internal void SetTacticalState(TacticalState nextState)
    {
        if (tacticalState == nextState) return;
        TacticalState previous = tacticalState;
        tacticalState = nextState;
        StateChanged?.Invoke(this, previous, nextState);
    }
}
