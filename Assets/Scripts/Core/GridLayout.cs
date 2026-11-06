using System;
using UnityEngine;

/// <summary>Immutable geometry shared by board rules, input and rendering.</summary>
public readonly struct GridLayout : IEquatable<GridLayout>
{
    public int Width { get; }
    public int Height { get; }
    public float CellSize { get; }
    public float CellGap { get; }
    public float SurfaceHeight { get; }
    public int Count => Width * Height;

    public GridLayout(int width, int height, float cellSize, float cellGap, float surfaceHeight)
    {
        if (width < 1 || height < 1 || (long)width * height > 1000000)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (!float.IsFinite(cellSize) || cellSize < 0.25f ||
            !float.IsFinite(cellGap) || cellGap < 0f || cellGap >= cellSize ||
            !float.IsFinite(surfaceHeight))
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        Width = width;
        Height = height;
        CellSize = cellSize;
        CellGap = cellGap;
        SurfaceHeight = surfaceHeight;
    }

    public bool Contains(GridCoordinate cell) =>
        cell.X >= 0 && cell.X < Width && cell.Z >= 0 && cell.Z < Height;

    public int Index(GridCoordinate cell) => cell.Z * Width + cell.X;

    public Vector3 CellCenter(GridCoordinate cell) => new(
        (cell.X - (Width - 1) * 0.5f) * CellSize,
        SurfaceHeight,
        (cell.Z - (Height - 1) * 0.5f) * CellSize);

    public bool TryLocalToCell(Vector3 local, out GridCoordinate cell)
    {
        cell = new GridCoordinate(
            Mathf.FloorToInt(local.x / CellSize + Width * 0.5f),
            Mathf.FloorToInt(local.z / CellSize + Height * 0.5f));
        return Contains(cell);
    }

    public bool Equals(GridLayout other) => Width == other.Width && Height == other.Height &&
        CellSize == other.CellSize && CellGap == other.CellGap && SurfaceHeight == other.SurfaceHeight;
    public override bool Equals(object obj) => obj is GridLayout other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Width, Height, CellSize, CellGap, SurfaceHeight);
}
