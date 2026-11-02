using UnityEngine;

[DisallowMultipleComponent]
public sealed class GridCellView : MonoBehaviour
{
    [SerializeField] private GridCoordinate coordinate;
    [SerializeField] private Renderer tileRenderer;

    public GridCoordinate Coordinate => coordinate;

    public void Configure(GridCoordinate newCoordinate, Renderer renderer)
    {
        coordinate = newCoordinate;
        tileRenderer = renderer;
    }

    public void SetColor(Color color)
    {
        if (tileRenderer == null) return;
        MaterialPropertyBlock block = new();
        tileRenderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        tileRenderer.SetPropertyBlock(block);
    }
}
