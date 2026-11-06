using System.Collections.Generic;

/// <summary>Owns occupancy. No scene objects, transforms or rendering operations.</summary>
public sealed class GridBoard
{
    private GridCell[] cells;
    private readonly Dictionary<GridOccupant, GridCoordinate> positions = new();
    public GridLayout Layout { get; private set; }
    public IEnumerable<GridOccupant> Occupants => positions.Keys;

    public GridBoard(GridLayout layout)
    {
        Layout = layout;
        cells = CreateCells(layout);
    }

    public GridCell GetCell(GridCoordinate cell) => Layout.Contains(cell) ? cells[Layout.Index(cell)] : null;

    internal bool TryResize(GridLayout layout)
    {
        foreach (GridCoordinate cell in positions.Values)
            if (!layout.Contains(cell)) return false;
        GridCell[] next = CreateCells(layout);
        foreach (var entry in positions) next[layout.Index(entry.Value)].SetOccupant(entry.Key);
        cells = next;
        Layout = layout;
        return true;
    }

    internal bool TryPlace(GridOccupant occupant, GridCoordinate cell)
    {
        GridCell destination = GetCell(cell);
        if (occupant == null || destination == null || destination.IsOccupied) return false;
        if (positions.TryGetValue(occupant, out GridCoordinate previous)) GetCell(previous).Clear(occupant);
        destination.SetOccupant(occupant);
        positions[occupant] = cell;
        return true;
    }

    internal bool TryRemove(GridOccupant occupant)
    {
        if (occupant == null || !positions.TryGetValue(occupant, out GridCoordinate cell)) return false;
        GetCell(cell).Clear(occupant);
        return positions.Remove(occupant);
    }

    private static GridCell[] CreateCells(GridLayout layout)
    {
        GridCell[] result = new GridCell[layout.Count];
        for (int z = 0; z < layout.Height; z++)
        for (int x = 0; x < layout.Width; x++)
        {
            GridCoordinate cell = new(x, z);
            result[layout.Index(cell)] = new GridCell(cell);
        }
        return result;
    }
}
