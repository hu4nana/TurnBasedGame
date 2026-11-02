using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleField : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField, Min(1)] private int width = 10;
    [SerializeField, Min(1)] private int height = 8;
    [SerializeField, Min(0.25f)] private float cellSize = 1.5f;
    [SerializeField, Min(0f)] private float occupantHeight = 0.1f;

    private GridCell[,] cells;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;
    public IEnumerable<GridOccupant> Occupants
    {
        get
        {
            EnsureGrid();
            for (int x = 0; x < width; x++)
            for (int z = 0; z < height; z++)
                if (cells[x, z].Occupant != null)
                    yield return cells[x, z].Occupant;
        }
    }

    public event Action OccupancyChanged;

    private void Awake()
    {
        EnsureGrid();
        RegisterSceneOccupants();
    }

    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        cellSize = Mathf.Max(0.25f, cellSize);
        cells = null;
    }

    public bool IsInside(GridCoordinate coordinate) =>
        coordinate.X >= 0 && coordinate.X < width &&
        coordinate.Z >= 0 && coordinate.Z < height;

    public GridCell GetCell(GridCoordinate coordinate)
    {
        EnsureGrid();
        return IsInside(coordinate) ? cells[coordinate.X, coordinate.Z] : null;
    }

    public List<GridCoordinate> GetReachableCells(GridOccupant occupant)
    {
        List<GridCoordinate> result = new();
        if (occupant == null || occupant.Board != this || occupant.MoveRange <= 0) return result;

        Queue<(GridCoordinate coordinate, int distance)> frontier = new();
        HashSet<GridCoordinate> visited = new() { occupant.Coordinate };
        frontier.Enqueue((occupant.Coordinate, 0));

        GridCoordinate[] directions =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        while (frontier.Count > 0)
        {
            (GridCoordinate current, int distance) = frontier.Dequeue();
            if (distance >= occupant.MoveRange) continue;

            foreach (GridCoordinate direction in directions)
            {
                GridCoordinate next = new(current.X + direction.X, current.Z + direction.Z);
                if (!IsInside(next) || !visited.Add(next)) continue;

                GridCell cell = GetCell(next);
                if (cell.IsOccupied) continue;

                result.Add(next);
                frontier.Enqueue((next, distance + 1));
            }
        }

        return result;
    }

    public List<GridCoordinate> GetThreatenedEnemyCells(GridOccupant attacker)
    {
        List<GridCoordinate> result = new();
        if (attacker == null || attacker.Board != this) return result;

        foreach (GridOccupant occupant in Occupants)
            if (ThreatEvaluator.CanThreaten(attacker, occupant))
                result.Add(occupant.Coordinate);
        return result;
    }

    public Vector3 CoordinateToWorld(GridCoordinate coordinate)
    {
        float offsetX = (width - 1) * cellSize * 0.5f;
        float offsetZ = (height - 1) * cellSize * 0.5f;
        Vector3 local = new(
            coordinate.X * cellSize - offsetX,
            occupantHeight,
            coordinate.Z * cellSize - offsetZ);
        return transform.TransformPoint(local);
    }

    public bool TryWorldToCoordinate(Vector3 worldPosition, out GridCoordinate coordinate)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        float offsetX = width * cellSize * 0.5f;
        float offsetZ = height * cellSize * 0.5f;
        int x = Mathf.FloorToInt((local.x + offsetX) / cellSize);
        int z = Mathf.FloorToInt((local.z + offsetZ) / cellSize);
        coordinate = new GridCoordinate(x, z);
        return IsInside(coordinate);
    }

    public bool RayToCoordinate(Ray ray, out GridCoordinate coordinate)
    {
        Plane plane = new(transform.up, transform.position);
        if (plane.Raycast(ray, out float distance))
            return TryWorldToCoordinate(ray.GetPoint(distance), out coordinate);

        coordinate = default;
        return false;
    }

    public bool TryPlace(GridOccupant occupant, GridCoordinate coordinate)
    {
        if (occupant == null) return false;
        GridCell destination = GetCell(coordinate);
        if (destination == null || destination.IsOccupied) return false;

        if (occupant.IsPlaced)
        {
            if (occupant.Board != this) return false;
            GridCell previous = GetCell(occupant.Coordinate);
            if (previous != null) previous.Clear(occupant);
        }

        destination.SetOccupant(occupant);
        occupant.Place(this, coordinate, CoordinateToWorld(coordinate));
        OccupancyChanged?.Invoke();
        return true;
    }

    public bool TrySpawn(GameObject prefab, GridCoordinate coordinate, out GridOccupant occupant)
    {
        occupant = null;
        if (prefab == null || GetCell(coordinate)?.IsOccupied != false) return false;

        GameObject instance = Instantiate(prefab);
        occupant = instance.GetComponent<GridOccupant>();
        if (occupant != null && TryPlace(occupant, coordinate)) return true;

        if (Application.isPlaying) Destroy(instance);
        else DestroyImmediate(instance);
        occupant = null;
        return false;
    }

    public bool TryMove(GridOccupant occupant, GridCoordinate destination) =>
        occupant != null && occupant.Board == this && TryPlace(occupant, destination);

    public bool TryRemove(GridOccupant occupant)
    {
        if (occupant == null || occupant.Board != this) return false;
        GridCell cell = GetCell(occupant.Coordinate);
        if (cell == null || !cell.Clear(occupant)) return false;

        occupant.RemoveFromBoard(this);
        OccupancyChanged?.Invoke();
        return true;
    }

    private void EnsureGrid()
    {
        if (cells != null && cells.GetLength(0) == width && cells.GetLength(1) == height)
            return;

        cells = new GridCell[width, height];
        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++)
            cells[x, z] = new GridCell(new GridCoordinate(x, z));
    }

    private void RegisterSceneOccupants()
    {
        GridOccupant[] sceneOccupants = GetComponentsInChildren<GridOccupant>(true);
        foreach (GridOccupant occupant in sceneOccupants)
        {
            if (TryWorldToCoordinate(occupant.transform.position, out GridCoordinate coordinate))
                TryPlace(occupant, coordinate);
        }
    }
}
