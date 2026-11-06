using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class BattleField : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField, Min(1)] private int width = 10;
    [SerializeField, Min(1)] private int height = 8;
    [SerializeField, Min(0.25f)] private float cellSize = 1.5f;
    [SerializeField, Min(0f)] private float occupantHeight = 0.1f;
    [SerializeField, Min(0f)] private float cellGap = 0.06f;

    private GridBoard board;
    private bool synchronizing;
    private readonly Queue<(GridCoordinate coordinate, int distance)> frontier = new();
    private readonly HashSet<GridCoordinate> visited = new();
    private readonly List<GridCoordinate> reachableBuffer = new();
    private static readonly GridCoordinate[] Directions = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;
    public GridLayout Layout { get { EnsureGrid(); return board.Layout; } }
    public IEnumerable<GridOccupant> Occupants { get { EnsureGrid(); return board.Occupants; } }

    public event Action OccupancyChanged;
    public event Action LayoutChanged;
    public event Action RulesChanged;

    internal void NotifyRulesChanged(GridOccupant unit)
    {
        if (unit.Board == this) RulesChanged?.Invoke();
    }

    private void OnEnable() => SynchronizeLayout();

    // ExecuteAlways can build the board before all scene components finish activation.
    private void Start() { if (Application.isPlaying) RegisterSceneOccupants(); }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= ApplyInspectorLayout;
#endif
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        // OnValidate may run on the loading thread; touch scene objects only on delayCall.
        UnityEditor.EditorApplication.delayCall -= ApplyInspectorLayout;
        UnityEditor.EditorApplication.delayCall += ApplyInspectorLayout;
#endif
    }

#if UNITY_EDITOR
    private void ApplyInspectorLayout()
    {
        if (this == null || !isActiveAndEnabled) return;
        if (!SynchronizeLayout())
            Debug.LogWarning("Grid resize rejected: an occupied cell would be outside the board.", this);
    }
#endif

    public bool TrySetLayout(GridLayout layout)
    {
        EnsureGrid();
        if (layout.Equals(board.Layout)) return true;
        if (!board.TryResize(layout)) return false;
        CopyLayout(layout);
        RepositionOccupants();
        LayoutChanged?.Invoke();
        OccupancyChanged?.Invoke();
        return true;
    }

    public bool SynchronizeLayout()
    {
        if (synchronizing) return true;
        synchronizing = true;
        try
        {
            width = Mathf.Clamp(width, 1, 512);
            height = Mathf.Clamp(height, 1, 512);
            cellSize = float.IsFinite(cellSize) ? Mathf.Max(0.25f, cellSize) : 1.5f;
            cellGap = float.IsFinite(cellGap) ? Mathf.Clamp(cellGap, 0f, cellSize - 0.01f) : 0.06f;
            occupantHeight = float.IsFinite(occupantHeight) ? Mathf.Max(0f, occupantHeight) : 0.1f;
            GridLayout requested = new(width, height, cellSize, cellGap, occupantHeight);
            if (board == null)
            {
                board = new GridBoard(requested);
                RegisterSceneOccupants();
                LayoutChanged?.Invoke();
                return true;
            }
            if (TrySetLayout(requested)) return true;
            CopyLayout(board.Layout);
            return false;
        }
        finally { synchronizing = false; }
    }

    private void CopyLayout(GridLayout layout)
    {
        width = layout.Width;
        height = layout.Height;
        cellSize = layout.CellSize;
        cellGap = layout.CellGap;
        occupantHeight = layout.SurfaceHeight;
    }

    private void RepositionOccupants()
    {
        foreach (GridOccupant occupant in board.Occupants)
            occupant.SnapToBoard(this, CoordinateToWorld(occupant.Coordinate));
    }

    public bool IsInside(GridCoordinate coordinate) =>
        coordinate.X >= 0 && coordinate.X < width &&
        coordinate.Z >= 0 && coordinate.Z < height;

    public GridCell GetCell(GridCoordinate coordinate)
    {
        EnsureGrid();
        return board.GetCell(coordinate);
    }

    public List<GridCoordinate> GetReachableCells(GridOccupant occupant)
    {
        List<GridCoordinate> result = new();
        FillReachableCells(occupant, result);
        return result;
    }

    public void FillReachableCells(GridOccupant occupant, List<GridCoordinate> result)
    {
        result.Clear();
        if (occupant == null || !occupant.isActiveAndEnabled || occupant.Board != this || occupant.MoveRange <= 0) return;
        frontier.Clear();
        visited.Clear();
        visited.Add(occupant.Coordinate);
        frontier.Enqueue((occupant.Coordinate, 0));

        while (frontier.Count > 0)
        {
            (GridCoordinate current, int distance) = frontier.Dequeue();
            if (distance >= occupant.MoveRange) continue;

            foreach (GridCoordinate direction in Directions)
            {
                GridCoordinate next = new(current.X + direction.X, current.Z + direction.Z);
                if (!IsInside(next) || !visited.Add(next)) continue;

                GridCell cell = GetCell(next);
                if (cell.IsOccupied) continue;

                result.Add(next);
                frontier.Enqueue((next, distance + 1));
            }
        }

    }

    public List<GridCoordinate> GetThreatenedEnemyCells(GridOccupant attacker)
    {
        List<GridCoordinate> result = new();
        FillThreatenedEnemyCells(attacker, result);
        return result;
    }

    public void FillThreatenedEnemyCells(GridOccupant attacker, List<GridCoordinate> result)
    {
        result.Clear();
        if (attacker == null || attacker.Board != this) return;

        foreach (GridOccupant occupant in Occupants)
            if (ThreatEvaluator.CanThreaten(attacker, occupant))
                result.Add(occupant.Coordinate);
    }

    public Vector3 CoordinateToWorld(GridCoordinate coordinate)
    {
        return transform.TransformPoint(Layout.CellCenter(coordinate));
    }

    public bool TryWorldToCoordinate(Vector3 worldPosition, out GridCoordinate coordinate)
    {
        return Layout.TryLocalToCell(transform.InverseTransformPoint(worldPosition), out coordinate);
    }

    public bool RayToCoordinate(Ray ray, out GridCoordinate coordinate)
    {
        Plane plane = new(transform.up, transform.TransformPoint(Vector3.up * Layout.SurfaceHeight));
        if (plane.Raycast(ray, out float distance))
            return TryWorldToCoordinate(ray.GetPoint(distance), out coordinate);

        coordinate = default;
        return false;
    }

    public bool TryPlace(GridOccupant occupant, GridCoordinate coordinate)
    {
        if (occupant == null || (Application.isPlaying && !occupant.isActiveAndEnabled)) return false;
        if (occupant.TryGetComponent(out Character character) && character.IsDead) return false;
        EnsureGrid();
        if (occupant.IsPlaced && occupant.Board != this) return false;
        if (!board.TryPlace(occupant, coordinate)) return false;
        occupant.transform.SetParent(transform, true);
        occupant.Place(this, coordinate, CoordinateToWorld(coordinate));
        occupant.GetComponent<GridOccupantVisualizer>()?.BindManager(GetComponent<CharacterVisualManager>());
        OccupancyChanged?.Invoke();
        return true;
    }

    public bool TrySpawn(GameObject prefab, GridCoordinate coordinate, out GridOccupant occupant)
    {
        occupant = null;
        if (prefab == null || GetCell(coordinate)?.IsOccupied != false) return false;

        GameObject instance = Instantiate(prefab, transform);
        occupant = instance.GetComponent<GridOccupant>();
        if (occupant != null && TryPlace(occupant, coordinate)) return true;

        if (Application.isPlaying) Destroy(instance);
        else DestroyImmediate(instance);
        occupant = null;
        return false;
    }

    public bool TryMove(GridOccupant occupant, GridCoordinate destination)
    {
        FillReachableCells(occupant, reachableBuffer);
        return reachableBuffer.Contains(destination) && TryPlace(occupant, destination);
    }

    public bool ForceMove(GridOccupant occupant, GridCoordinate destination) =>
        occupant != null && occupant.Board == this && TryPlace(occupant, destination);

    public bool TryRemove(GridOccupant occupant)
    {
        if (occupant == null || occupant.Board != this) return false;
        EnsureGrid();
        if (!board.TryRemove(occupant)) return false;

        occupant.RemoveFromBoard(this);
        OccupancyChanged?.Invoke();
        return true;
    }

    private void EnsureGrid()
    {
        if (board == null) SynchronizeLayout();
    }

    private void RegisterSceneOccupants()
    {
        GridOccupant[] sceneOccupants = GetComponentsInChildren<GridOccupant>(true);
        foreach (GridOccupant occupant in sceneOccupants)
        {
            if (occupant.Board == this) continue;
            if (Application.isPlaying && !occupant.isActiveAndEnabled) continue;
            GridCoordinate coordinate = occupant.Coordinate;
            if (!occupant.HasSavedCoordinate &&
                !TryWorldToCoordinate(occupant.transform.position, out coordinate)) continue;
            if (!TryPlace(occupant, coordinate))
                Debug.LogWarning($"Could not register {occupant.name} at {coordinate}.", occupant);
        }
    }
}
