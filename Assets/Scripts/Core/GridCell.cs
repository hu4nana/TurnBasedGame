public sealed class GridCell
{
    public GridCoordinate Coordinate { get; }
    public GridOccupant Occupant { get; private set; }
    public bool IsOccupied => Occupant != null;

    public GridCell(GridCoordinate coordinate)
    {
        Coordinate = coordinate;
    }

    internal void SetOccupant(GridOccupant occupant)
    {
        Occupant = occupant;
    }

    internal bool Clear(GridOccupant occupant)
    {
        if (Occupant != occupant) return false;
        Occupant = null;
        return true;
    }
}
