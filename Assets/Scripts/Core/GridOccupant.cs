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
    [SerializeField, HideInInspector] private GridCoordinate coordinate;
    [SerializeField, HideInInspector] private bool hasSavedCoordinate;

    public BattleFaction Faction => faction;
    public int MoveRange => moveRange;
    public int ThreatRange => threatRange;
    public TacticalState State => tacticalState;
    public BattleField Board { get; private set; }
    public GridCoordinate Coordinate => coordinate;
    public bool HasSavedCoordinate => hasSavedCoordinate;
    public bool IsPlaced => Board != null;

    public event Action<GridOccupant, TacticalState, TacticalState> StateChanged;
    public event Action<GridOccupant, Vector3, Vector3> Moved;
    public event Action<GridOccupant> RulesChanged;

    public void Configure(BattleFaction newFaction, int newMoveRange, int newThreatRange)
    {
        newMoveRange = Mathf.Max(0, newMoveRange);
        newThreatRange = Mathf.Max(0, newThreatRange);
        if (faction == newFaction && moveRange == newMoveRange && threatRange == newThreatRange) return;
        faction = newFaction;
        moveRange = newMoveRange;
        threatRange = newThreatRange;
        NotifyRulesChanged();
    }

    private void NotifyRulesChanged()
    {
        RulesChanged?.Invoke(this);
        if (Board != null) Board.NotifyRulesChanged(this);
    }

    private void OnValidate()
    {
        moveRange = Mathf.Max(0, moveRange);
        threatRange = Mathf.Max(0, threatRange);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= ApplyInspectorRules;
        UnityEditor.EditorApplication.delayCall += ApplyInspectorRules;
#endif
    }

#if UNITY_EDITOR
    private void ApplyInspectorRules() { if (this != null) NotifyRulesChanged(); }
#endif

    internal void Place(BattleField board, GridCoordinate coordinate, Vector3 worldPosition)
    {
        bool wasPlaced = Board != null;
        Vector3 previousPosition = transform.position;
        Board = board;
        this.coordinate = coordinate;
        hasSavedCoordinate = true;
        transform.SetPositionAndRotation(worldPosition, board.transform.rotation);
        if (wasPlaced && Application.isPlaying && previousPosition != worldPosition)
            Moved?.Invoke(this, previousPosition, worldPosition);
    }

    internal void RemoveFromBoard(BattleField board)
    {
        if (Board != board) return;
        Board = null;
        hasSavedCoordinate = false;
        GetComponent<GridOccupantVisualizer>()?.BindManager(null);
        SetTacticalState(TacticalState.Guard);
    }

    internal void SnapToBoard(BattleField board, Vector3 worldPosition)
    {
        Board = board;
        transform.SetPositionAndRotation(worldPosition, board.transform.rotation);
    }

    private void OnDestroy()
    {
        if (Board != null) Board.TryRemove(this);
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= ApplyInspectorRules;
#endif
        if (Application.isPlaying && Board != null) Board.TryRemove(this);
    }

    internal void SetTacticalState(TacticalState nextState)
    {
        if (tacticalState == nextState) return;
        TacticalState previous = tacticalState;
        tacticalState = nextState;
        StateChanged?.Invoke(this, previous, nextState);
    }
}
