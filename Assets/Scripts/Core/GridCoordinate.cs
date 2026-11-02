using System;
using UnityEngine;

[Serializable]
public struct GridCoordinate : IEquatable<GridCoordinate>
{
    [SerializeField] private int x;
    [SerializeField] private int z;

    public int X => x;
    public int Z => z;

    public GridCoordinate(int x, int z)
    {
        this.x = x;
        this.z = z;
    }

    public int ManhattanDistance(GridCoordinate other) =>
        Mathf.Abs(x - other.x) + Mathf.Abs(z - other.z);

    public bool Equals(GridCoordinate other) => x == other.x && z == other.z;
    public override bool Equals(object obj) => obj is GridCoordinate other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(x, z);
    public override string ToString() => $"({x}, {z})";

    public static bool operator ==(GridCoordinate left, GridCoordinate right) => left.Equals(right);
    public static bool operator !=(GridCoordinate left, GridCoordinate right) => !left.Equals(right);
}
