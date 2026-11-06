using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Independent lifetimes for fixed lines, range geometry and one movable hover quad.</summary>
[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(BattleField), typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class BattleFieldView : MonoBehaviour
{
    [SerializeField] private Material gridMaterial;
    [SerializeField, Range(0f, 1f)] private float overlayOpacity = 0.3f;
    [SerializeField] private Color lineColor = new(0.48f, 0.53f, 0.6f, 1f);
    [SerializeField] private Color hoverColor = new(0.45f, 0.5f, 0.58f, 0.18f);
    [SerializeField] private Color selectedColor = new(0.2f, 0.55f, 1f, 0.4f);
    [SerializeField] private Color moveColor = new(0.14f, 0.62f, 0.42f, 0.25f);
    [SerializeField] private Color attackColor = new(0.9f, 0.18f, 0.12f, 0.4f);

    [Flags] private enum Highlight { Move = 1, Attack = 2, Selected = 4 }
    private readonly Dictionary<GridCoordinate, Highlight> ranges = new();
    private readonly Dictionary<GridCoordinate, Highlight> pending = new();
    private BattleField field;
    private GridMeshLayer grid;
    private GridMeshLayer range;
    private GridMeshLayer cursor;
    private GridCoordinate? hover;
    private float brightness = 1f;

    public int CellCount => field != null ? field.Layout.Count : 0;
    public int GridBuildCount { get; private set; }
    public int RangeBuildCount { get; private set; }
    public int HoverBuildCount { get; private set; }
    public int HoverMoveCount { get; private set; }

    private void OnEnable()
    {
        field = GetComponent<BattleField>();
        field.LayoutChanged += Rebuild;
        field.SynchronizeLayout();
        Rebuild();
    }

    private void OnDisable()
    {
        if (field != null) field.LayoutChanged -= Rebuild;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= RebuildIfActive;
#endif
        grid?.Dispose(); range?.Dispose(); cursor?.Dispose();
        grid = range = cursor = null;
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= RebuildIfActive;
        UnityEditor.EditorApplication.delayCall += RebuildIfActive;
#endif
    }

    private void RebuildIfActive() { if (this != null && isActiveAndEnabled) Rebuild(); }
    public void SetMaterial(Material material) { gridMaterial = material; Rebuild(); }

    /// <summary>Only layout/settings changes rebuild fixed geometry.</summary>
    public void Rebuild()
    {
        if (!TryGetComponent(out MeshFilter filter) || !TryGetComponent(out MeshRenderer renderer)) return;
        if (field == null) field = GetComponent<BattleField>();
        grid ??= new GridMeshLayer(filter, renderer);
        range ??= GridMeshLayer.CreateChild(transform, "Range Overlay");
        cursor ??= GridMeshLayer.CreateChild(transform, "Hover Overlay");
        grid.Configure(gridMaterial, brightness);
        range.Configure(gridMaterial, brightness);
        cursor.Configure(gridMaterial, brightness);
        GridLayout layout = field.Layout;
        grid.Begin();
        float left = -layout.Width * layout.CellSize * 0.5f;
        float near = -layout.Height * layout.CellSize * 0.5f;
        float y = layout.SurfaceHeight;
        for (int x = 0; x <= layout.Width; x++)
            grid.AddLine(new Vector3(left + x * layout.CellSize, y, near),
                new Vector3(left + x * layout.CellSize, y, -near), lineColor);
        for (int z = 0; z <= layout.Height; z++)
            grid.AddLine(new Vector3(left, y, near + z * layout.CellSize),
                new Vector3(-left, y, near + z * layout.CellSize), lineColor);
        grid.Upload(MeshTopology.Lines);
        GridBuildCount++;
        RebuildRanges();
        cursor.Begin();
        Color color = hoverColor; color.a *= overlayOpacity;
        cursor.AddQuad(Vector3.zero, layout.CellSize, color);
        cursor.Upload(MeshTopology.Triangles);
        HoverBuildCount++;
        UpdateHoverTransform();
    }

    public void SetRanges(GridCoordinate? selected, IEnumerable<GridCoordinate> movement,
        IEnumerable<GridCoordinate> attacks)
    {
        pending.Clear();
        if (movement != null) foreach (GridCoordinate cell in movement) AddFlag(cell, Highlight.Move);
        if (attacks != null) foreach (GridCoordinate cell in attacks) AddFlag(cell, Highlight.Attack);
        if (selected.HasValue) AddFlag(selected.Value, Highlight.Selected);
        bool equal = pending.Count == ranges.Count;
        if (equal)
            foreach (var entry in pending)
                if (!ranges.TryGetValue(entry.Key, out Highlight flags) || flags != entry.Value) { equal = false; break; }
        if (equal) return;
        ranges.Clear();
        foreach (var entry in pending) ranges.Add(entry.Key, entry.Value);
        if (range != null) RebuildRanges();
    }

    private void AddFlag(GridCoordinate cell, Highlight flag)
    {
        pending.TryGetValue(cell, out Highlight previous);
        pending[cell] = previous | flag;
    }

    private void RebuildRanges()
    {
        range.Begin();
        GridLayout layout = field.Layout;
        foreach (var entry in ranges)
        {
            if (!layout.Contains(entry.Key)) continue;
            Highlight flags = entry.Value;
            Color color = (flags & Highlight.Attack) != 0 ? attackColor :
                (flags & Highlight.Selected) != 0 ? selectedColor : moveColor;
            color.a *= overlayOpacity;
            range.AddQuad(layout.CellCenter(entry.Key) + Vector3.up * 0.002f, layout.CellSize, color);
        }
        range.Upload(MeshTopology.Triangles);
        RangeBuildCount++;
    }

    public void SetHover(GridCoordinate? cell)
    {
        if (cell == hover) return;
        hover = cell;
        UpdateHoverTransform();
    }

    private void UpdateHoverTransform()
    {
        if (cursor == null) return;
        bool visible = hover.HasValue && field.Layout.Contains(hover.Value);
        cursor.SetVisible(visible);
        if (visible)
        {
            cursor.SetLocalPosition(field.Layout.CellCenter(hover.Value) + Vector3.up * 0.004f);
            HoverMoveCount++;
        }
    }

    // Compatibility for editor tools; input uses separate range/hover methods.
    public void SetInteractionHighlights(GridCoordinate? selected, IEnumerable<GridCoordinate> movement,
        IEnumerable<GridCoordinate> attacks, GridCoordinate? cell)
    {
        SetRanges(selected, movement, attacks);
        SetHover(cell);
    }

    public void ClearInteractionHighlights() { SetRanges(null, null, null); SetHover(null); }
    public void SetPresentationBrightness(float value)
    {
        value = Mathf.Clamp01(value);
        if (brightness == value) return;
        brightness = value;
        grid?.SetBrightness(value); range?.SetBrightness(value); cursor?.SetBrightness(value);
    }
}
