using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BattleField))]
public sealed class BattleFieldView : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float tileHeight = 0.15f;
    [SerializeField, Range(0f, 0.3f)] private float tileGap = 0.06f;
    [SerializeField] private Color tileColor = new(0.24f, 0.27f, 0.31f, 1f);
    [SerializeField] private Color edgeColor = new(0.14f, 0.16f, 0.19f, 1f);
    [SerializeField] private Color hoverColor = new(0.45f, 0.5f, 0.58f, 1f);
    [SerializeField] private Color selectedColor = new(0.2f, 0.55f, 1f, 1f);
    [SerializeField] private Color moveColor = new(0.14f, 0.62f, 0.42f, 1f);
    [SerializeField] private Color attackColor = new(0.9f, 0.18f, 0.12f, 1f);

    private const string VisualRootName = "Grid Visuals";
    private BattleField battleField;
    private readonly Dictionary<GridCoordinate, GridCellView> cellViews = new();
    private readonly HashSet<GridCoordinate> moveCells = new();
    private readonly HashSet<GridCoordinate> attackCells = new();
    private GridCoordinate? selectedCell;
    private GridCoordinate? hoverCell;
    private float presentationBrightness = 1f;

    private void OnEnable()
    {
        battleField = GetComponent<BattleField>();
        Transform root = transform.Find(VisualRootName);
        if (root == null) Rebuild();
        else
        {
            CacheCellViews(root);
            RefreshColors();
        }
    }

    [ContextMenu("Rebuild 3D Grid")]
    public void Rebuild()
    {
        if (battleField == null) battleField = GetComponent<BattleField>();
        Transform previous = transform.Find(VisualRootName);
        if (previous != null)
        {
            if (Application.isPlaying) Destroy(previous.gameObject);
            else DestroyImmediate(previous.gameObject);
        }

        GameObject rootObject = new(VisualRootName);
        rootObject.transform.SetParent(transform, false);

        CreateBase(rootObject.transform);
        float size = Mathf.Max(0.05f, battleField.CellSize - tileGap);
        for (int x = 0; x < battleField.Width; x++)
        for (int z = 0; z < battleField.Height; z++)
        {
            GridCoordinate coordinate = new(x, z);
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"Cell {x},{z}";
            tile.transform.SetParent(rootObject.transform, false);
            tile.transform.position = battleField.CoordinateToWorld(coordinate) - transform.up * (tileHeight * 0.5f + 0.1f);
            tile.transform.rotation = transform.rotation;
            tile.transform.localScale = new Vector3(size, tileHeight, size);
            RemoveCollider(tile);
            Renderer renderer = tile.GetComponent<Renderer>();
            tile.AddComponent<GridCellView>().Configure(coordinate, renderer);
        }

        CacheCellViews(rootObject.transform);
        RefreshColors();
    }

    public void SetInteractionHighlights(
        GridCoordinate? selected,
        IEnumerable<GridCoordinate> movement,
        IEnumerable<GridCoordinate> attacks,
        GridCoordinate? hover)
    {
        selectedCell = selected;
        hoverCell = hover;
        moveCells.Clear();
        attackCells.Clear();
        if (movement != null) moveCells.UnionWith(movement);
        if (attacks != null) attackCells.UnionWith(attacks);
        RefreshColors();
    }

    public void ClearInteractionHighlights()
    {
        selectedCell = null;
        hoverCell = null;
        moveCells.Clear();
        attackCells.Clear();
        RefreshColors();
    }

    public void SetPresentationBrightness(float brightness)
    {
        presentationBrightness = Mathf.Clamp01(brightness);
        RefreshColors();
    }

    private void CreateBase(Transform parent)
    {
        GameObject boardBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boardBase.name = "Board Base";
        boardBase.transform.SetParent(parent, false);
        boardBase.transform.localPosition = new Vector3(0f, -tileHeight - 0.13f, 0f);
        boardBase.transform.localScale = new Vector3(
            battleField.Width * battleField.CellSize + 0.2f,
            0.12f,
            battleField.Height * battleField.CellSize + 0.2f);
        RemoveCollider(boardBase);
        SetColor(boardBase.GetComponent<Renderer>(), edgeColor);
    }

    private void CacheCellViews(Transform root)
    {
        cellViews.Clear();
        foreach (GridCellView view in root.GetComponentsInChildren<GridCellView>(true))
            cellViews[view.Coordinate] = view;
    }

    private void RefreshColors()
    {
        foreach ((GridCoordinate coordinate, GridCellView view) in cellViews)
        {
            Color color = tileColor;
            if (hoverCell == coordinate) color = hoverColor;
            if (moveCells.Contains(coordinate)) color = moveColor;
            if (selectedCell == coordinate) color = selectedColor;
            if (attackCells.Contains(coordinate)) color = attackColor;
            view.SetColor(Dim(color));
        }

        Transform root = transform.Find(VisualRootName);
        if (root != null)
        {
            Renderer baseRenderer = root.Find("Board Base")?.GetComponent<Renderer>();
            SetColor(baseRenderer, Dim(edgeColor));
        }
    }

    private Color Dim(Color color)
    {
        color.r *= presentationBrightness;
        color.g *= presentationBrightness;
        color.b *= presentationBrightness;
        return color;
    }

    private static void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }
    }

    private static void SetColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        MaterialPropertyBlock block = new();
        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }
}
