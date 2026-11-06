using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Reusable geometry buffers and owned resources for one board display layer.</summary>
internal sealed class GridMeshLayer : IDisposable
{
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private readonly MeshFilter filter;
    private readonly MeshRenderer renderer;
    private readonly GameObject ownedObject;
    private readonly Mesh mesh;
    private readonly MaterialPropertyBlock properties = new();
    private readonly List<Vector3> vertices = new();
    private readonly List<Color> colors = new();
    private readonly List<int> indices = new();

    public GridMeshLayer(MeshFilter filter, MeshRenderer renderer, GameObject ownedObject = null)
    {
        this.filter = filter;
        this.renderer = renderer;
        this.ownedObject = ownedObject;
        mesh = new Mesh { name = renderer.name + " Display", hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic();
        filter.sharedMesh = mesh;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = short.MinValue;
    }

    public static GridMeshLayer CreateChild(Transform parent, string name)
    {
        GameObject child = new(name) { hideFlags = HideFlags.HideAndDontSave };
        child.transform.SetParent(parent, false);
        return new GridMeshLayer(child.AddComponent<MeshFilter>(), child.AddComponent<MeshRenderer>(), child);
    }

    public void Configure(Material material, float brightness)
    {
        if (renderer.sharedMaterial != material) renderer.sharedMaterial = material;
        SetBrightness(brightness);
    }

    public void SetBrightness(float value)
    {
        properties.SetFloat(BrightnessId, value);
        renderer.SetPropertyBlock(properties);
    }

    public void Begin() { vertices.Clear(); colors.Clear(); indices.Clear(); }
    public void AddLine(Vector3 a, Vector3 b, Color color)
    {
        indices.Add(vertices.Count); AddVertex(a, color);
        indices.Add(vertices.Count); AddVertex(b, color);
    }

    public void AddQuad(Vector3 center, float size, Color color)
    {
        float half = size * 0.5f;
        int start = vertices.Count;
        AddVertex(center + new Vector3(-half, 0, -half), color);
        AddVertex(center + new Vector3(-half, 0, half), color);
        AddVertex(center + new Vector3(half, 0, half), color);
        AddVertex(center + new Vector3(half, 0, -half), color);
        indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
        indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
    }

    private void AddVertex(Vector3 position, Color color)
    {
        vertices.Add(position);
        colors.Add(QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color);
    }

    public void Upload(MeshTopology topology)
    {
        mesh.Clear();
        mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices); mesh.SetColors(colors);
        mesh.SetIndices(indices, topology, 0);
        mesh.RecalculateBounds();
        renderer.enabled = indices.Count > 0;
    }

    public void SetVisible(bool visible) => renderer.enabled = visible;
    public void SetLocalPosition(Vector3 position) => renderer.transform.localPosition = position;

    public void Dispose()
    {
        if (filter != null && filter.sharedMesh == mesh) filter.sharedMesh = null;
        DestroyOwned(mesh);
        if (ownedObject != null) DestroyOwned(ownedObject);
    }

    private static void DestroyOwned(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(value);
        else UnityEngine.Object.DestroyImmediate(value);
    }
}
